using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    public class Template
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(10)]
        public string Language { get; set; } = "en";
        
        public string? Category { get; set; }
        
        public string? Status { get; set; } // approved, rejected, pending
        
        public string? BodyText { get; set; }
        
        public string? ButtonsJson { get; set; }
        
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
        
        public int TenantId { get; set; }
    }
}
