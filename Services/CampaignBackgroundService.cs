using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WhatsAppBusinessInbox.Data;
using WhatsAppBusinessInbox.Models;

namespace WhatsAppBusinessInbox.Services
{
    /// <summary>
    /// Long-running <see cref="BackgroundService"/> that performs bulk campaign
    /// sends outside of the request pipeline. Campaign ids are pushed onto
    /// <see cref="ICampaignQueue"/> by <c>CampaignsController</c>.
    /// </summary>
    public class CampaignBackgroundService : BackgroundService
    {
        private readonly ICampaignQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CampaignBackgroundService> _logger;

        public CampaignBackgroundService(
            ICampaignQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<CampaignBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Campaign background service started.");

            await foreach (var campaignId in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessCampaignAsync(campaignId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing campaign {CampaignId}", campaignId);
                }
            }
        }

        private async Task ProcessCampaignAsync(int campaignId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

            var campaign = await context.Campaigns
                .Include(c => c.Recipients)
                .ThenInclude(r => r.Contact)
                .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);

            if (campaign == null || campaign.Status == "completed")
            {
                return;
            }

            var variables = DeserializeVariables(campaign.TemplateVariablesJson);

            campaign.Status = "running";
            campaign.StartedAt ??= DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            foreach (var recipient in campaign.Recipients.Where(r => r.Status is null or "pending"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var waMessageId = await whatsApp.SendTemplateMessageAsync(
                        recipient.Contact.Phone,
                        campaign.TemplateName ?? string.Empty,
                        campaign.TemplateLanguage,
                        variables,
                        campaign.TenantId);

                    recipient.Status = "sent";
                    recipient.WaMessageId = waMessageId;
                    recipient.Error = null;
                    campaign.SentCount++;

                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    recipient.Status = "failed";
                    recipient.Error = ex.Message;
                    campaign.FailedCount++;
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning(ex, "Campaign {CampaignId}: failed to send to contact {ContactId}", campaignId, recipient.ContactId);
                }

                // Be gentle with the Graph API rate limits.
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }

            campaign.Status = "completed";
            campaign.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        private static Dictionary<string, string> DeserializeVariables(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
    }
}
