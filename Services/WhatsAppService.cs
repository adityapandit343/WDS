using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;

namespace WhatsAppBusinessInbox.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICredentialProtector _protector;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            IConfiguration configuration,
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ICredentialProtector protector,
            ILogger<WhatsAppService> logger)
        {
            _configuration = configuration;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _protector = protector;
            _logger = logger;
        }

        // ---------------------------------------------------------------------
        // Credentials / tenant resolution
        // ---------------------------------------------------------------------

        public async Task<WhatsAppConnection> GetConnectionAsync(int tenantId)
        {
            var credential = await _context.TenantCredentials
                .FirstOrDefaultAsync(c => c.TenantId == tenantId);

            var defaultTenant = TenantProvider.DefaultTenantId;
            var configMatchesTenant = tenantId == defaultTenant;

            var phoneNumberId = credential?.PhoneNumberId;
            var businessAccountId = credential?.BusinessAccountId;
            var apiVersion = credential?.ApiVersion;
            var verifyToken = credential?.VerifyToken;

            var accessToken = credential?.EncryptedAccessToken != null
                ? _protector.Unprotect(credential.EncryptedAccessToken)
                : null;

            var appSecret = credential?.EncryptedAppSecret != null
                ? _protector.Unprotect(credential.EncryptedAppSecret)
                : null;

            // Fall back to appsettings for the default tenant when a value is missing.
            if (configMatchesTenant)
            {
                phoneNumberId ??= _configuration["WhatsApp:PhoneNumberId"];
                businessAccountId ??= _configuration["WhatsApp:BusinessAccountId"];
                apiVersion ??= _configuration["WhatsApp:ApiVersion"];
                verifyToken ??= _configuration["WhatsApp:VerifyToken"];
                accessToken = string.IsNullOrEmpty(accessToken) ? _configuration["WhatsApp:Token"] : accessToken;
                appSecret = string.IsNullOrEmpty(appSecret) ? _configuration["WhatsApp:AppSecret"] : appSecret;
            }

            return new WhatsAppConnection(
                tenantId,
                phoneNumberId ?? string.Empty,
                accessToken ?? string.Empty,
                string.IsNullOrEmpty(apiVersion) ? "v21.0" : apiVersion!,
                appSecret ?? string.Empty,
                verifyToken ?? string.Empty,
                businessAccountId ?? string.Empty);
        }

        public async Task<int?> ResolveTenantByPhoneNumberIdAsync(string phoneNumberId)
        {
            if (string.IsNullOrEmpty(phoneNumberId)) return null;

            var credential = await _context.TenantCredentials
                .FirstOrDefaultAsync(c => c.PhoneNumberId == phoneNumberId);

            return credential?.TenantId;
        }

        // ---------------------------------------------------------------------
        // Incoming webhooks
        // ---------------------------------------------------------------------

        public async Task<bool> VerifyWebhookSignatureAsync(int tenantId, string signature, string payload)
        {
            var connection = await GetConnectionAsync(tenantId);
            var appSecret = connection.AppSecret;

            if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(signature))
            {
                // Nothing to verify against; reject in production-like setups.
                _logger.LogWarning("Webhook signature verification skipped: no app secret configured for tenant {TenantId}", tenantId);
                return false;
            }

            var key = Encoding.UTF8.GetBytes(appSecret);
            using var hmac = new HMACSHA256(key);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = "sha256=" + BitConverter.ToString(computedHash).Replace("-", "").ToLowerInvariant();

            // Constant-time comparison to avoid timing attacks.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(signature));
        }

        public async Task ProcessIncomingMessageAsync(int tenantId, JObject payload)
        {
            foreach (var entry in payload["entry"] ?? new JArray())
            {
                foreach (var change in entry["changes"] ?? new JArray())
                {
                    if (change["field"]?.ToString() != "messages") continue;

                    var value = change["value"];
                    foreach (var msg in value?["messages"] ?? new JArray())
                    {
                        try
                        {
                            await ProcessSingleMessageAsync(tenantId, msg);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process incoming message for tenant {TenantId}", tenantId);
                        }
                    }
                }
            }
        }

        private async Task ProcessSingleMessageAsync(int tenantId, JToken msg)
        {
            var phone = msg["from"]?.ToString();
            var waMessageId = msg["id"]?.ToString();
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(waMessageId)) return;

            if (await _context.Messages.AnyAsync(m => m.WaMessageId == waMessageId)) return;

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Phone == phone && c.TenantId == tenantId);

            if (contact == null)
            {
                contact = new Contact
                {
                    Name = phone,
                    Phone = phone,
                    CreatedAt = DateTime.UtcNow,
                    TenantId = tenantId
                };
                _context.Contacts.Add(contact);
            }

            var type = msg["type"]?.ToString() ?? "text";
            var body = ExtractBody(msg, type);
            var buttonId = ExtractButtonId(msg, type);

            var message = new Message
            {
                ContactId = contact.Id,
                Contact = contact,
                WaMessageId = waMessageId,
                Direction = "In",
                Type = type,
                Body = body,
                Status = "received",
                CreatedAt = DateTime.UtcNow,
                TenantId = tenantId
            };

            _context.Messages.Add(message);
            contact.LastMessageAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(buttonId))
            {
                await TryApplyFeedbackReplyAsync(tenantId, phone, buttonId, body);
            }
        }

        private static string ExtractBody(JToken msg, string type)
        {
            return type switch
            {
                "text" => msg["text"]?["body"]?.ToString() ?? string.Empty,
                "image" => msg["image"]?["caption"]?.ToString() ?? "[Image]",
                "document" => msg["document"]?["caption"]?.ToString() ?? "[Document]",
                "audio" => "[Audio]",
                "video" => msg["video"]?["caption"]?.ToString() ?? "[Video]",
                "button" => msg["button"]?["text"]?.ToString() ?? string.Empty,
                "interactive" => msg["interactive"]?["button_reply"]?["title"]?.ToString()
                                 ?? msg["interactive"]?["list_reply"]?["title"]?.ToString()
                                 ?? string.Empty,
                _ => string.Empty
            };
        }

        private static string? ExtractButtonId(JToken msg, string type)
        {
            if (type == "button")
            {
                return msg["button"]?["payload"]?.ToString();
            }

            if (type == "interactive")
            {
                return msg["interactive"]?["button_reply"]?["id"]?.ToString()
                       ?? msg["interactive"]?["list_reply"]?["id"]?.ToString();
            }

            return null;
        }

        private async Task TryApplyFeedbackReplyAsync(int tenantId, string phone, string buttonId, string responseText)
        {
            // Expected payload format: feedback:{callId}:{yes|no}
            var parts = buttonId.Split(':');
            if (parts.Length < 3 || !parts[0].Equals("feedback", StringComparison.OrdinalIgnoreCase)) return;

            var callId = parts[1];
            var answer = parts[2];

            var feedback = await _context.Feedbacks
                .Where(f => f.TenantId == tenantId && f.CallId == callId && f.Phone == phone)
                .OrderByDescending(f => f.Id)
                .FirstOrDefaultAsync();

            if (feedback == null) return;

            feedback.Satisfied = answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
            feedback.Status = feedback.Satisfied ? "satisfied" : "unsatisfied";
            feedback.ResponseText = responseText;
            feedback.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task ProcessMessageStatusAsync(int tenantId, JObject payload)
        {
            foreach (var entry in payload["entry"] ?? new JArray())
            {
                foreach (var change in entry["changes"] ?? new JArray())
                {
                    if (change["field"]?.ToString() != "messages") continue;

                    foreach (var status in change["value"]?["statuses"] ?? new JArray())
                    {
                        var waMessageId = status["id"]?.ToString();
                        var msgStatus = status["status"]?.ToString();
                        if (string.IsNullOrEmpty(waMessageId)) continue;

                        var message = await _context.Messages
                            .FirstOrDefaultAsync(m => m.WaMessageId == waMessageId);

                        if (message == null) continue;

                        message.Status = msgStatus;
                        if (msgStatus == "failed" && status["errors"] is JArray errors && errors.Count > 0)
                        {
                            message.ErrorMessage = errors[0]?["message"]?.ToString();
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        // ---------------------------------------------------------------------
        // Outgoing messages
        // ---------------------------------------------------------------------

        public async Task<string> SendTextMessageAsync(string to, string body, int tenantId)
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to,
                type = "text",
                text = new { preview_url = false, body }
            };

            return await SendToMetaAsync(payload, tenantId);
        }

        public async Task<string> SendTemplateMessageAsync(string to, string templateName, string language, Dictionary<string, string> variables, int tenantId)
        {
            var components = new List<object>();

            if (variables.Count > 0)
            {
                var parameters = variables
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => new { type = "text", text = kvp.Value })
                    .ToList();

                components.Add(new { type = "body", parameters });
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = string.IsNullOrEmpty(language) ? "en" : language },
                    components
                }
            };

            return await SendToMetaAsync(payload, tenantId);
        }

        public async Task<string> SendInteractiveButtonsAsync(string to, string body, IReadOnlyList<ReplyButton> buttons, int tenantId)
        {
            var buttonPayload = buttons.Select(b => new
            {
                type = "reply",
                reply = new { id = b.Id, title = Truncate(b.Title, 20) }
            }).ToList();

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    body = new { text = body },
                    action = new { buttons = buttonPayload }
                }
            };

            return await SendToMetaAsync(payload, tenantId);
        }

        public async Task<string> SendFeedbackRequestAsync(string to, string callId, int tenantId)
        {
            var buttons = new List<ReplyButton>
            {
                new($"feedback:{callId}:yes", "Yes, satisfied"),
                new($"feedback:{callId}:no", "Not satisfied")
            };

            return await SendInteractiveButtonsAsync(
                to,
                "Thanks for your recent call. Were you satisfied with the support you received?",
                buttons,
                tenantId);
        }

        public async Task<JArray> GetMessageTemplatesAsync(int tenantId)
        {
            var connection = await GetConnectionAsync(tenantId);
            if (string.IsNullOrEmpty(connection.BusinessAccountId) || string.IsNullOrEmpty(connection.AccessToken))
            {
                throw new InvalidOperationException("WhatsApp Business Account id / access token is not configured.");
            }

            var url = $"https://graph.facebook.com/{connection.ApiVersion}/{connection.BusinessAccountId}/message_templates?limit=200";

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.AccessToken);

            using var response = await client.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch templates: {responseContent}");
            }

            var result = JObject.Parse(responseContent);
            return result["data"] as JArray ?? new JArray();
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private async Task<string> SendToMetaAsync(object payload, int tenantId)
        {
            var connection = await GetConnectionAsync(tenantId);

            if (string.IsNullOrEmpty(connection.PhoneNumberId) || string.IsNullOrEmpty(connection.AccessToken))
            {
                throw new InvalidOperationException("WhatsApp PhoneNumberId / access token is not configured for this tenant.");
            }

            var url = $"https://graph.facebook.com/{connection.ApiVersion}/{connection.PhoneNumberId}/messages";

            var client = _httpClientFactory.CreateClient();
            var json = JsonConvert.SerializeObject(payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.AccessToken);

            using var response = await client.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Meta send failed ({Status}): {Body}", response.StatusCode, responseContent);
                throw new Exception($"Failed to send message: {responseContent}");
            }

            var result = JObject.Parse(responseContent);
            return result["messages"]?.First?["id"]?.ToString() ?? string.Empty;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value[..max];
        }
    }
}
