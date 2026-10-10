using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    /// <summary>Role-based administration: platform stats, tenants, credentials and users.</summary>
    [Authorize(Roles = DbSeeder.AdminRole)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICredentialProtector _protector;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(
            ApplicationDbContext context,
            ICredentialProtector protector,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _protector = protector;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TenantCount = await _context.Tenants.CountAsync();
            ViewBag.ContactCount = await _context.Contacts.CountAsync();
            ViewBag.MessageCount = await _context.Messages.CountAsync();
            ViewBag.TemplateCount = await _context.Templates.CountAsync();
            ViewBag.CampaignCount = await _context.Campaigns.CountAsync();
            ViewBag.FeedbackCount = await _context.Feedbacks.CountAsync();
            ViewBag.SatisfiedCount = await _context.Feedbacks.CountAsync(f => f.Status == "satisfied");
            ViewBag.UnsatisfiedCount = await _context.Feedbacks.CountAsync(f => f.Status == "unsatisfied");
            ViewBag.UserCount = await _userManager.Users.CountAsync();

            var tenants = await _context.Tenants
                .Include(t => t.Credentials)
                .OrderBy(t => t.Id)
                .ToListAsync();

            return View(tenants);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTenant(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.Tenants.Add(new Tenant
                {
                    Name = name.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                TempData["AdminMessage"] = $"Tenant '{name.Trim()}' created.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Credentials(int id)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Credentials)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null) return NotFound();

            var credential = tenant.Credentials.FirstOrDefault();
            var model = new TenantCredentialsViewModel
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                PhoneNumberId = credential?.PhoneNumberId,
                BusinessAccountId = credential?.BusinessAccountId,
                ApiVersion = credential?.ApiVersion ?? "v21.0",
                VerifyToken = credential?.VerifyToken,
                HasAccessToken = !string.IsNullOrEmpty(credential?.EncryptedAccessToken),
                HasAppSecret = !string.IsNullOrEmpty(credential?.EncryptedAppSecret)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Credentials(TenantCredentialsViewModel model)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Credentials)
                .FirstOrDefaultAsync(t => t.Id == model.TenantId);

            if (tenant == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.TenantName = tenant.Name;
                return View(model);
            }

            var credential = tenant.Credentials.FirstOrDefault();
            if (credential == null)
            {
                credential = new TenantCredential { TenantId = tenant.Id };
                _context.TenantCredentials.Add(credential);
            }

            credential.PhoneNumberId = model.PhoneNumberId?.Trim();
            credential.BusinessAccountId = model.BusinessAccountId?.Trim();
            credential.ApiVersion = string.IsNullOrWhiteSpace(model.ApiVersion) ? "v21.0" : model.ApiVersion.Trim();
            credential.VerifyToken = model.VerifyToken?.Trim();
            credential.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(model.AccessToken))
            {
                credential.EncryptedAccessToken = _protector.Protect(model.AccessToken.Trim());
            }

            if (!string.IsNullOrWhiteSpace(model.AppSecret))
            {
                credential.EncryptedAppSecret = _protector.Protect(model.AppSecret.Trim());
            }

            await _context.SaveChangesAsync();
            TempData["AdminMessage"] = $"Credentials updated for '{tenant.Name}'.";
            return RedirectToAction(nameof(Credentials), new { id = tenant.Id });
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var rows = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var claims = await _userManager.GetClaimsAsync(user);
                var tenantId = claims.FirstOrDefault(c => c.Type == TenantProvider.TenantClaimType)?.Value
                               ?? TenantProvider.DefaultTenantId.ToString();
                rows.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = roles,
                    TenantId = tenantId
                });
            }

            return View(rows);
        }
    }
}
