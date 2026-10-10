using Microsoft.AspNetCore.DataProtection;

namespace WhatsAppBusinessInbox.Services
{
    public class DataProtectionCredentialProtector : ICredentialProtector
    {
        private readonly IDataProtector _protector;

        public DataProtectionCredentialProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("WhatsAppBusinessInbox.Credentials.v1");
        }

        public string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return string.Empty;
            return _protector.Protect(plaintext);
        }

        public string Unprotect(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
            try
            {
                return _protector.Unprotect(ciphertext);
            }
            catch
            {
                // Value may be an unencrypted legacy/config value; return as-is.
                return ciphertext;
            }
        }
    }
}
