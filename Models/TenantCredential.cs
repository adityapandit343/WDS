using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    /// <summary>
    /// Per-tenant WhatsApp Cloud API credentials. Secrets (access token and app
    /// secret) are stored encrypted at rest using the Data Protection API.
    /// </summary>
    public class TenantCredential
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        [StringLength(50)]
        public string? PhoneNumberId { get; set; }

        [StringLength(50)]
        public string? BusinessAccountId { get; set; }

        [StringLength(20)]
        public string? ApiVersion { get; set; } = "v21.0";

        [StringLength(100)]
        public string? VerifyToken { get; set; }

        /// <summary>Data Protection encrypted access token.</summary>
        public string? EncryptedAccessToken { get; set; }

        /// <summary>Data Protection encrypted app secret used for webhook signature verification.</summary>
        public string? EncryptedAppSecret { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; } = null!;
    }
}
