using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;
using WhatsAppBusinessInbox.Services;

namespace WhatsAppBusinessInbox.Controllers
{
    [Authorize]
    public class ContactsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ContactsController(ApplicationDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Contacts.Where(c => c.TenantId == _tenantProvider.TenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Name.Contains(search) || c.Phone.Contains(search));
            }

            return View(await query.OrderByDescending(c => c.LastMessageAt).ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Contact contact)
        {
            if (ModelState.IsValid)
            {
                contact.CreatedAt = DateTime.UtcNow;
                contact.TenantId = _tenantProvider.TenantId;
                if (contact.OptIn) contact.OptInDate = DateTime.UtcNow;

                var duplicate = await _context.Contacts.AnyAsync(
                    c => c.Phone == contact.Phone && c.TenantId == contact.TenantId);
                if (duplicate)
                {
                    ModelState.AddModelError(nameof(Contact.Phone), "A contact with this phone number already exists.");
                    return View(contact);
                }

                _context.Add(contact);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(contact);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);
            if (contact == null) return NotFound();
            return View(contact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Contact contact)
        {
            if (id != contact.Id) return NotFound();

            var existing = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                existing.Name = contact.Name;
                existing.Phone = contact.Phone;
                existing.Tags = contact.Tags;
                if (contact.OptIn && !existing.OptIn)
                {
                    existing.OptIn = true;
                    existing.OptInDate = DateTime.UtcNow;
                }
                else
                {
                    existing.OptIn = contact.OptIn;
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(contact);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);
            if (contact == null) return NotFound();
            return View(contact);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantProvider.TenantId);
            if (contact == null) return NotFound();

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ImportError"] = "Please choose a CSV file.";
                return RedirectToAction(nameof(Index));
            }

            var tenantId = _tenantProvider.TenantId;
            var existingPhones = await _context.Contacts
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.Phone)
                .ToListAsync();

            var imported = 0;
            var skipped = 0;
            var lineNumber = 0;

            using var reader = new StreamReader(file.OpenReadStream());
            while (!reader.EndOfStream)
            {
                var line = (await reader.ReadLineAsync())?.Trim();
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = SplitCsvLine(line);

                // Skip a header row (e.g. "Name,Phone,Tags").
                if (lineNumber == 1 && !LooksLikePhone(values.ElementAtOrDefault(1)))
                {
                    continue;
                }

                if (values.Count < 2) { skipped++; continue; }

                var name = values[0].Trim();
                var phone = values[1].Trim();
                var tags = values.Count > 2 ? values[2].Trim() : null;

                if (string.IsNullOrWhiteSpace(phone) || existingPhones.Contains(phone))
                {
                    skipped++;
                    continue;
                }

                _context.Contacts.Add(new Contact
                {
                    Name = string.IsNullOrWhiteSpace(name) ? phone : name,
                    Phone = phone,
                    Tags = string.IsNullOrWhiteSpace(tags) ? null : tags,
                    OptIn = true,
                    OptInDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    TenantId = tenantId
                });

                existingPhones.Add(phone);
                imported++;
            }

            await _context.SaveChangesAsync();
            TempData["ImportSuccess"] = $"Imported {imported} contact(s). Skipped {skipped}.";
            return RedirectToAction(nameof(Index));
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            foreach (var ch in line)
            {
                switch (ch)
                {
                    case '"':
                        inQuotes = !inQuotes;
                        break;
                    case ',' when !inQuotes:
                        result.Add(current.ToString());
                        current.Clear();
                        break;
                    default:
                        current.Append(ch);
                        break;
                }
            }

            result.Add(current.ToString());
            return result;
        }

        private static bool LooksLikePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.Count(char.IsDigit) >= 6;
        }
    }
}
