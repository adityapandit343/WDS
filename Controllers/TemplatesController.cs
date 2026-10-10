using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    [Authorize]
    public class TemplatesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<TemplatesController> _logger;

        public TemplatesController(
            ApplicationDbContext context,
            IWhatsAppService whatsAppService,
            ITenantProvider tenantProvider,
            ILogger<TemplatesController> logger)
        {
            _context = context;
            _whatsAppService = whatsAppService;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var templates = await _context.Templates
                .Where(t => t.TenantId == _tenantProvider.TenantId)
                .OrderByDescending(t => t.SyncedAt)
                .ToListAsync();
            return View(templates);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync()
        {
            var tenantId = _tenantProvider.TenantId;

            try
            {
                var remoteTemplates = await _whatsAppService.GetMessageTemplatesAsync(tenantId);

                foreach (var template in remoteTemplates)
                {
                    var name = template["name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
                    var language = template["language"]?.ToString() ?? "en";

                    var existing = await _context.Templates
                        .FirstOrDefaultAsync(t => t.Name == name && t.Language == language && t.TenantId == tenantId);

                    var bodyText = ExtractBodyText(template);
                    var buttonsJson = ExtractButtons(template);

                    if (existing == null)
                    {
                        _context.Templates.Add(new Template
                        {
                            Name = name,
                            Language = language,
                            Category = template["category"]?.ToString(),
                            Status = template["status"]?.ToString(),
                            BodyText = bodyText,
                            ButtonsJson = buttonsJson,
                            SyncedAt = DateTime.UtcNow,
                            TenantId = tenantId
                        });
                    }
                    else
                    {
                        existing.Status = template["status"]?.ToString();
                        existing.Category = template["category"]?.ToString();
                        existing.BodyText = bodyText ?? existing.BodyText;
                        existing.ButtonsJson = buttonsJson ?? existing.ButtonsJson;
                        existing.SyncedAt = DateTime.UtcNow;
                        _context.Update(existing);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SyncMessage"] = $"Synced {remoteTemplates.Count} template(s) from Meta.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Template sync failed for tenant {TenantId}", tenantId);
                TempData["SyncError"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Create()
        {
            return View(new Template());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Template template)
        {
            if (ModelState.IsValid)
            {
                template.SyncedAt = DateTime.UtcNow;
                template.TenantId = _tenantProvider.TenantId;
                template.Status ??= "pending";
                _context.Add(template);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(template);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var template = await _context.Templates
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == _tenantProvider.TenantId);
            if (template == null) return NotFound();
            return View(template);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var template = await _context.Templates
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == _tenantProvider.TenantId);
            if (template == null) return NotFound();

            _context.Templates.Remove(template);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private static string? ExtractBodyText(JToken template)
        {
            var components = template["components"] as JArray;
            var body = components?.FirstOrDefault(c => c["type"]?.ToString() == "BODY");
            return body?["text"]?.ToString();
        }

        private static string? ExtractButtons(JToken template)
        {
            var components = template["components"] as JArray;
            var buttons = components?.FirstOrDefault(c => c["type"]?.ToString() == "BUTTONS");
            var buttonsArray = buttons?["buttons"] as JArray;
            return buttonsArray == null ? null : JsonConvert.SerializeObject(buttonsArray);
        }
    }
}
