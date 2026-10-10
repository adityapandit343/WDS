using System.Threading.Channels;

namespace WhatsAppBusinessInbox.Services
{
    /// <summary>In-memory queue used to hand campaigns to the background sender.</summary>
    public interface ICampaignQueue
    {
        ValueTask EnqueueAsync(int campaignId);
        IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken);
    }

    public class CampaignQueue : ICampaignQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

        public ValueTask EnqueueAsync(int campaignId) => _channel.Writer.WriteAsync(campaignId);

        public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken)
            => _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
