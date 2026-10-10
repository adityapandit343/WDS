using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    [Authorize]
    public class CampaignsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly ICampaignQueue _campaignQueue;

        public CampaignsController(
            ApplicationDbContext context,
            ITenantProvider tenantProvider,
            ICampaignQueue campaignQueue)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _campaignQueue = campaignQueue;
        }

        public async Task<IActionResult> Index()
        {
            var campaigns = await _context.Campaigns
                .Where(c => c.TenantId == _tenantProvider.TenantId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(campaigns);
        }

        public async Task<IActionResult> Details(int id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.Recipients)
                .ThenInclude(r => r.Contact)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);

            if (campaign == null) return NotFound();
            return View(campaign);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CampaignCreateViewModel();
            await PopulateTemplatesAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CampaignCreateViewModel model)
        {
            var tenantId = _tenantProvider.TenantId;

            if (!string.IsNullOrWhiteSpace(model.TemplateVariablesJson))
            {
                try
                {
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(model.TemplateVariablesJson);
                }
                catch
                {
                    ModelState.AddModelError(nameof(model.TemplateVariablesJson), "Variables must be a JSON object, e.g. {\"1\":\"Alice\"}.");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateTemplatesAsync(model);
                return View(model);
            }

            var audience = _context.Contacts
                .Where(c => c.TenantId == tenantId && c.OptIn);

            if (model.AudienceMode == "tag" && !string.IsNullOrWhiteSpace(model.Tag))
            {
                audience = audience.Where(c => c.Tags != null && c.Tags.Contains(model.Tag));
            }

            var recipients = await audience.ToListAsync();
            if (recipients.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "No opted-in contacts match the selected audience.");
                await PopulateTemplatesAsync(model);
                return View(model);
            }

            var campaign = new Campaign
            {
                Name = model.Name,
                TemplateName = model.TemplateName,
                TemplateLanguage = string.IsNullOrEmpty(model.TemplateLanguage) ? "en" : model.TemplateLanguage,
                TemplateVariablesJson = string.IsNullOrWhiteSpace(model.TemplateVariablesJson) ? "{}" : model.TemplateVariablesJson,
                Status = "draft",
                CreatedAt = DateTime.UtcNow,
                TotalCount = recipients.Count,
                TenantId = tenantId
            };

            foreach (var contact in recipients)
            {
                campaign.Recipients.Add(new CampaignRecipient
                {
                    ContactId = contact.Id,
                    Status = "pending"
                });
            }

            _context.Campaigns.Add(campaign);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = campaign.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("whatsapp-send")]
        public async Task<IActionResult> Start(int id)
        {
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);

            if (campaign == null) return NotFound();

            if (campaign.Status == "running")
            {
                TempData["CampaignError"] = "Campaign is already running.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (campaign.Status == "completed")
            {
                TempData["CampaignError"] = "Campaign has already completed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            campaign.Status = "running";
            campaign.StartedAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _campaignQueue.EnqueueAsync(campaign.Id);

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateTemplatesAsync(CampaignCreateViewModel model)
        {
            var templates = await _context.Templates
                .Where(t => t.TenantId == _tenantProvider.TenantId)
                .OrderBy(t => t.Name)
                .ToListAsync();

            ViewBag.Templates = new SelectList(templates, nameof(Template.Name), nameof(Template.Name), model.TemplateName);
            ViewBag.Tags = await _context.Contacts
                .Where(c => c.TenantId == _tenantProvider.TenantId && c.Tags != null)
                .Select(c => c.Tags!)
                .Distinct()
                .ToListAsync();
        }
    }
}
