using System.ComponentModel.DataAnnotations;

namespace WhatsAppBusinessInbox.Models
{
    public class TenantCredentialsViewModel
    {
        public int TenantId { get; set; }

        [Display(Name = "Tenant")]
        public string TenantName { get; set; } = string.Empty;

        [Display(Name = "Phone number id")]
        [StringLength(50)]
        public string? PhoneNumberId { get; set; }

        [Display(Name = "WhatsApp Business Account id")]
        [StringLength(50)]
        public string? BusinessAccountId { get; set; }

        [Display(Name = "API version")]
        [StringLength(20)]
        public string? ApiVersion { get; set; } = "v21.0";

        [Display(Name = "Webhook verify token")]
        [StringLength(100)]
        public string? VerifyToken { get; set; }

        /// <summary>Write-only. Leave blank to keep the stored value.</summary>
        [Display(Name = "Access token")]
        public string? AccessToken { get; set; }

        /// <summary>Write-only. Leave blank to keep the stored value.</summary>
        [Display(Name = "App secret")]
        public string? AppSecret { get; set; }

        public bool HasAccessToken { get; set; }
        public bool HasAppSecret { get; set; }
    }
}
