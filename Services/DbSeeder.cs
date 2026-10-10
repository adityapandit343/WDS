using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;

namespace WhatsAppBusinessInbox.Services
{
    /// <summary>
    /// Applies pending migrations and seeds the roles / default admin user and
    /// default tenant. Invoked once at application startup.
    /// </summary>
    public static class DbSeeder
    {
        public const string AdminRole = "Admin";
        public const string AgentRole = "Agent";

        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            await context.Database.MigrateAsync();

            foreach (var role in new[] { AdminRole, AgentRole })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // The default tenant is created by identity insert; its generated id
            // is the value used as TenantProvider.DefaultTenantId (1 on a fresh DB).
            if (!await context.Tenants.AnyAsync())
            {
                context.Tenants.Add(new Tenant
                {
                    Name = "Default Tenant"
                });
                await context.SaveChangesAsync();
            }

            var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@example.com";
            var adminPassword = configuration["Seed:AdminPassword"] ?? "Admin@123";

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                if (!result.Succeeded)
                {
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(admin, AdminRole))
            {
                await userManager.AddToRoleAsync(admin, AdminRole);
            }

            var claims = await userManager.GetClaimsAsync(admin);
            if (!claims.Any(c => c.Type == TenantProvider.TenantClaimType))
            {
                await userManager.AddClaimAsync(
                    admin,
                    new Claim(TenantProvider.TenantClaimType, TenantProvider.DefaultTenantId.ToString()));
            }
        }
    }
}
