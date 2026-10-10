using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    /// <summary>
    /// Meta WhatsApp Cloud API webhook. GET performs the subscription handshake,
    /// POST receives message + status notifications after HMAC-SHA256 signature
    /// verification.
    /// </summary>
    [AllowAnonymous]
    public class WebhookController : Controller
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public WebhookController(
            IWhatsAppService whatsAppService,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _whatsAppService = whatsAppService;
            _context = context;
            _configuration = configuration;
        }

        // GET /Webhook?hub.mode=subscribe&hub.verify_token=...&hub.challenge=...
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var mode = Request.Query["hub.mode"].ToString();
            var token = Request.Query["hub.verify_token"].ToString();
            var challenge = Request.Query["hub.challenge"].ToString();

            if (mode == "subscribe" && await IsValidVerifyTokenAsync(token))
            {
                return Content(challenge, "text/plain");
            }

            return StatusCode(StatusCodes.Status403Forbidden);
        }

        private async Task<bool> IsValidVerifyTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;

            var configured = _configuration["WhatsApp:VerifyToken"];
            if (!string.IsNullOrEmpty(configured) && token == configured) return true;

            return await _context.TenantCredentials.AnyAsync(c => c.VerifyToken == token);
        }

        // POST /Webhook
        [HttpPost]
        public async Task<IActionResult> Index([FromHeader(Name = "X-Hub-Signature-256")] string? signature)
        {
            // The body must be read as the raw bytes that were signed, so read the
            // stream ourselves instead of letting model binding consume it.
            Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest();
            }

            JObject payload;
            try
            {
                payload = JObject.Parse(body);
            }
            catch (Exception)
            {
                return BadRequest();
            }

            var firstChange = (payload["entry"] as JArray)?.FirstOrDefault()?["changes"] as JArray;
            var phoneNumberId = firstChange?.FirstOrDefault()?["value"]?["metadata"]?["phone_number_id"]?.ToString();
            var tenantId = await _whatsAppService.ResolveTenantByPhoneNumberIdAsync(phoneNumberId ?? string.Empty)
                           ?? TenantProvider.DefaultTenantId;

            if (!await _whatsAppService.VerifyWebhookSignatureAsync(tenantId, signature ?? string.Empty, body))
            {
                return Unauthorized();
            }

            foreach (var entry in payload["entry"] ?? new JArray())
            {
                foreach (var change in entry["changes"] ?? new JArray())
                {
                    if (change["field"]?.ToString() != "messages") continue;

                    var value = change["value"];
                    if (value?["statuses"] != null)
                    {
                        await _whatsAppService.ProcessMessageStatusAsync(tenantId, payload);
                    }
                    else if (value?["messages"] != null)
                    {
                        await _whatsAppService.ProcessIncomingMessageAsync(tenantId, payload);
                    }
                }
            }

            // Meta expects a fast 200 regardless of processing outcome.
            return Ok();
        }
    }
}
