using System.ComponentModel.DataAnnotations;

namespace WhatsAppBusinessInbox.Models
{
    public class FeedbackSendViewModel
    {
        [Required]
        [StringLength(20)]
        [Display(Name = "Customer phone (with country code)")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Call reference id")]
        [StringLength(100)]
        public string? CallId { get; set; }
    }
}
