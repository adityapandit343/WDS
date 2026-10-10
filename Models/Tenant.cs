using System.ComponentModel.DataAnnotations;

namespace WhatsAppBusinessInbox.Models
{
    /// <summary>
    /// Represents a single business/account using the platform. Every domain
    /// entity is partitioned by <see cref="Models.Contact.TenantId"/> and friends.
    /// </summary>
    public class Tenant
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TenantCredential> Credentials { get; set; } = new List<TenantCredential>();
    }
}
