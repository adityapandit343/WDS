using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppBusinessInbox.Models
{
    /// <summary>
    /// Post-call satisfaction feedback. A row is created as <c>pending</c> when
    /// the feedback request (interactive quick-reply buttons) is sent, then
    /// updated when the customer taps one of the buttons.
    /// </summary>
    public class Feedback
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string CallId { get; set; } = string.Empty;

        public bool Satisfied { get; set; }

        /// <summary>pending, satisfied, unsatisfied.</summary>
        [StringLength(20)]
        public string Status { get; set; } = "pending";

        /// <summary>Free text reply captured alongside the button tap, if any.</summary>
        public string? ResponseText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        public int TenantId { get; set; }
    }
}
