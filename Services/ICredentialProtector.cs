namespace WhatsAppBusinessInbox.Services
{
    /// <summary>
    /// Encrypts/decrypts tenant secrets at rest (WhatsApp access tokens and app
    /// secrets). Backed by the ASP.NET Core Data Protection API so the values
    /// are never stored as plain text in the database.
    /// </summary>
    public interface ICredentialProtector
    {
        string Protect(string plaintext);
        string Unprotect(string ciphertext);
    }
}
