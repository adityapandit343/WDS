using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    public class Contact
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;
        
        public string? Tags { get; set; }
        
        public bool OptIn { get; set; } = false;
        
        public DateTime? OptInDate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastMessageAt { get; set; }
        
        public int TenantId { get; set; }
        
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<CampaignRecipient> CampaignRecipients { get; set; } = new List<CampaignRecipient>();
    }
}
