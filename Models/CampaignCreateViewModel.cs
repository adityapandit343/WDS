using System.ComponentModel.DataAnnotations;

namespace WhatsAppBusinessInbox.Models
{
    public class CampaignCreateViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Campaign name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Template")]
        public string TemplateName { get; set; } = string.Empty;

        [Display(Name = "Template language")]
        public string TemplateLanguage { get; set; } = "en";

        [Display(Name = "Body variables (JSON)")]
        public string? TemplateVariablesJson { get; set; } = "{}";

        /// <summary>all = every opted-in contact, tag = only contacts carrying the tag.</summary>
        [Display(Name = "Audience")]
        public string AudienceMode { get; set; } = "all";

        [Display(Name = "Tag")]
        public string? Tag { get; set; }
    }
}
