using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhatsAppBusinessInbox.Models;

namespace WhatsAppBusinessInbox.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<CampaignRecipient> CampaignRecipients { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<TenantCredential> TenantCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Contact unique phone per tenant
            builder.Entity<Contact>()
                .HasIndex(c => new { c.Phone, c.TenantId })
                .IsUnique();

            // Configure Message unique WaMessageId
            builder.Entity<Message>()
                .HasIndex(m => m.WaMessageId)
                .IsUnique();

            // A phone number id maps to exactly one tenant.
            builder.Entity<TenantCredential>()
                .HasIndex(t => t.PhoneNumberId)
                .IsUnique()
                .HasFilter("[PhoneNumberId] IS NOT NULL");

            builder.Entity<TenantCredential>()
                .HasOne(t => t.Tenant)
                .WithMany(t => t.Credentials)
                .HasForeignKey(t => t.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CampaignRecipient>()
                .HasIndex(r => new { r.CampaignId, r.ContactId });
        }
    }
}
