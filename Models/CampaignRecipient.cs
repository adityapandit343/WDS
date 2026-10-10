using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    public class CampaignRecipient
    {
        public int Id { get; set; }
        
        public int CampaignId { get; set; }
        
        public int ContactId { get; set; }
        
        public string? Status { get; set; } // pending, sent, failed
        
        public string? WaMessageId { get; set; }
        
        public string? Error { get; set; }
        
        [ForeignKey("CampaignId")]
        public virtual Campaign Campaign { get; set; } = null!;
        
        [ForeignKey("ContactId")]
        public virtual Contact Contact { get; set; } = null!;
    }
}
