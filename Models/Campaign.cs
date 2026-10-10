using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    public class Campaign
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? TemplateName { get; set; }

        [StringLength(10)]
        public string TemplateLanguage { get; set; } = "en";

        /// <summary>JSON object with default body variables, e.g. {"1":"Customer"}.</summary>
        public string? TemplateVariablesJson { get; set; }

        /// <summary>draft, running, completed.</summary>
        [StringLength(20)]
        public string Status { get; set; } = "draft";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int TotalCount { get; set; } = 0;

        public int SentCount { get; set; } = 0;

        public int FailedCount { get; set; } = 0;

        public int TenantId { get; set; }

        public virtual ICollection<CampaignRecipient> Recipients { get; set; } = new List<CampaignRecipient>();
    }
}
