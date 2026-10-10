using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ITenantProvider _tenantProvider;

        public ChatController(
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
            var contacts = await ContactsForTenant().ToListAsync();
            return View(contacts);
        }

        public async Task<IActionResult> Chat(int id)
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);
            if (contact == null) return NotFound();

            var messages = await _context.Messages
                .Where(m => m.ContactId == id && m.TenantId == _tenantProvider.TenantId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            ViewBag.Contact = contact;
            ViewBag.Contacts = await ContactsForTenant().ToListAsync();
            return View(messages);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int contactId, int? lastId)
        {
            var tenantId = _tenantProvider.TenantId;

            var query = _context.Messages
                .Where(m => m.ContactId == contactId && m.TenantId == tenantId);

            if (lastId.HasValue)
            {
                query = query.Where(m => m.Id > lastId.Value);
            }

            var messages = await query
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Direction,
                    m.Type,
                    m.Body,
                    m.Status,
                    m.CreatedAt
                })
                .ToListAsync();

            return Json(messages);
        }

        [HttpPost]
        [EnableRateLimiting("whatsapp-send")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int contactId, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return Json(new { success = false, error = "Message body is required." });
            }

            var tenantId = _tenantProvider.TenantId;
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId);
            if (contact == null) return NotFound();

            try
            {
                var waMessageId = await _whatsAppService.SendTextMessageAsync(contact.Phone, body, tenantId);

                var message = new Message
                {
                    ContactId = contactId,
                    WaMessageId = string.IsNullOrEmpty(waMessageId) ? $"local-{Guid.NewGuid():N}" : waMessageId,
                    Direction = "Out",
                    Type = "text",
                    Body = body,
                    Status = "sent",
                    CreatedAt = DateTime.UtcNow,
                    TenantId = tenantId
                };

                _context.Messages.Add(message);
                contact.LastMessageAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    messageId = message.Id,
                    direction = message.Direction,
                    status = message.Status,
                    createdAt = message.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private IQueryable<Contact> ContactsForTenant() =>
            _context.Contacts
                .Where(c => c.TenantId == _tenantProvider.TenantId)
                .OrderByDescending(c => c.LastMessageAt);
    }
}
