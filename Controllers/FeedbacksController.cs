using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    /// <summary>
    /// Post-call feedback flow: sends the customer an interactive Yes/No quick
    /// reply message and lets staff review the responses. The customer's tap is
    /// captured by the webhook and applied to the matching <see cref="Feedback"/> row.
    /// </summary>
    [Authorize]
    public class FeedbacksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ITenantProvider _tenantProvider;

        public FeedbacksController(
            ApplicationDbContext context,
            IWhatsAppService whatsAppService,
            ITenantProvider tenantProvider)
        {
            _context = context;
            _whatsAppService = whatsAppService;
            _tenantProvider = tenantProvider;
        }

        public async Task<IActionResult> Index()
        {
            var tenantId = _tenantProvider.TenantId;
            var feedbacks = await _context.Feedbacks
                .Where(f => f.TenantId == tenantId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            ViewBag.Total = feedbacks.Count;
            ViewBag.Satisfied = feedbacks.Count(f => f.Status == "satisfied");
            ViewBag.Unsatisfied = feedbacks.Count(f => f.Status == "unsatisfied");
            ViewBag.Pending = feedbacks.Count(f => f.Status == "pending");

            return View(feedbacks);
        }

        [HttpGet]
        public IActionResult Send()
        {
            return View(new FeedbackSendViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("whatsapp-send")]
        public async Task<IActionResult> Send(FeedbackSendViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var tenantId = _tenantProvider.TenantId;
            var callId = string.IsNullOrWhiteSpace(model.CallId)
                ? $"call-{Guid.NewGuid():N}"
                : model.CallId.Trim();

            var feedback = new Feedback
            {
                Phone = model.Phone.Trim(),
                CallId = callId,
                Satisfied = false,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                TenantId = tenantId
            };

            try
            {
                var waMessageId = await _whatsAppService.SendFeedbackRequestAsync(feedback.Phone, callId, tenantId);

                _context.Feedbacks.Add(feedback);

                // Record the outbound message only when a contact record exists
                // (Messages.ContactId is a required foreign key).
                var contact = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.Phone == feedback.Phone && c.TenantId == tenantId);

                if (contact != null)
                {
                    _context.Messages.Add(new Message
                    {
                        ContactId = contact.Id,
                        WaMessageId = string.IsNullOrEmpty(waMessageId) ? $"local-{Guid.NewGuid():N}" : waMessageId,
                        Direction = "Out",
                        Type = "interactive",
                        Body = "Post-call feedback request",
                        Status = "sent",
                        CreatedAt = DateTime.UtcNow,
                        TenantId = tenantId
                    });

                    contact.LastMessageAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                TempData["FeedbackMessage"] = $"Feedback request sent to {feedback.Phone} (call {callId}).";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }
    }
}
