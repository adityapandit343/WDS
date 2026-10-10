using System.Security.Claims;

namespace WhatsAppBusinessInbox.Services
{
    public class TenantProvider : ITenantProvider
    {
        public const string TenantClaimType = "TenantId";
        public const int DefaultTenantId = 1;

        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int TenantId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var value = user?.FindFirst(TenantClaimType)?.Value;

                if (int.TryParse(value, out var tenantId) && tenantId > 0)
                {
                    return tenantId;
                }

                return DefaultTenantId;
            }
        }
    }
}
