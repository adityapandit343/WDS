using Newtonsoft.Json.Linq;
using WhatsAppBusinessInbox.Models;

namespace WhatsAppBusinessInbox.Services
{
    /// <summary>A WhatsApp interactive reply button (id + visible title).</summary>
    public record ReplyButton(string Id, string Title);

    /// <summary>Resolved credentials for a tenant.</summary>
    public record WhatsAppConnection(
        int TenantId,
        string PhoneNumberId,
        string AccessToken,
        string ApiVersion,
        string AppSecret,
        string VerifyToken,
        string BusinessAccountId);

    public interface IWhatsAppService
    {
        /// <summary>Verifies the <c>X-Hub-Signature-256</c> header for a tenant.</summary>
        Task<bool> VerifyWebhookSignatureAsync(int tenantId, string signature, string payload);

        /// <summary>Maps an incoming <c>phone_number_id</c> to a tenant, or null if unknown.</summary>
        Task<int?> ResolveTenantByPhoneNumberIdAsync(string phoneNumberId);

        Task ProcessIncomingMessageAsync(int tenantId, JObject payload);
        Task ProcessMessageStatusAsync(int tenantId, JObject payload);

        Task<string> SendTextMessageAsync(string to, string body, int tenantId);
        Task<string> SendTemplateMessageAsync(string to, string templateName, string language, Dictionary<string, string> variables, int tenantId);
        Task<string> SendInteractiveButtonsAsync(string to, string body, IReadOnlyList<ReplyButton> buttons, int tenantId);

        /// <summary>Sends the post-call satisfaction question with Yes/No quick reply buttons.</summary>
        Task<string> SendFeedbackRequestAsync(string to, string callId, int tenantId);

        /// <summary>Fetches message templates from the Meta Graph API for a tenant.</summary>
        Task<JArray> GetMessageTemplatesAsync(int tenantId);
    }
}
