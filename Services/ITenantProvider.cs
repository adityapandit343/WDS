namespace WhatsAppBusinessInbox.Services
{
    /// <summary>
    /// Resolves the tenant the current request belongs to. The tenant id is read
    /// from the authenticated user's <c>TenantId</c> claim, falling back to the
    /// default tenant (1) for anonymous requests / demo usage.
    /// </summary>
    public interface ITenantProvider
    {
        int TenantId { get; }
    }
}
