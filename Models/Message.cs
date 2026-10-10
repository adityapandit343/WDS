using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    public class Message
    {
        public int Id { get; set; }
        
        public int ContactId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string WaMessageId { get; set; } = string.Empty;
        
        [Required]
        public string Direction { get; set; } = "In"; // In or Out
        
        public string? Type { get; set; } // text, image, document, button, interactive
        
        public string? Body { get; set; }
        
        public string? TemplateName { get; set; }
        
        public string? Status { get; set; } = "queued"; // queued, sent, delivered, read, failed
        
        public string? ErrorMessage { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public int TenantId { get; set; }
        
        [ForeignKey("ContactId")]
        public virtual Contact Contact { get; set; } = null!;
    }
}
