using Microsoft.AspNetCore.DataProtection;
using System.Text;

namespace PppPricing.API.Services;

public interface ICredentialEncryptionService
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] cipherText);
}

public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly IDataProtector _protector;

    public CredentialEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("StoreCredentials.v1");
    }

    public byte[] Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return Array.Empty<byte>();

        var protectedData = _protector.Protect(plainText);
        return Encoding.UTF8.GetBytes(protectedData);
    }

    public string Decrypt(byte[] cipherText)
    {
        if (cipherText == null || cipherText.Length == 0)
            return string.Empty;

        var protectedData = Encoding.UTF8.GetString(cipherText);
        return _protector.Unprotect(protectedData);
    }
}
