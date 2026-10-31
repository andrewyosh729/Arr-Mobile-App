using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using ArrApp.Core;

namespace ArrApp.Data.Security;

/// <summary>Windows Data Protection API, scoped to the current Windows user: other users and other machines can't decrypt.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiApiKeyProtector : IApiKeyProtector {
    private const string Prefix = "dpapi1:";
    private static readonly byte[] Entropy = "ArrApp.ApiKey.v1"u8.ToArray();

    public string Protect(string plainText)
        => Prefix + Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser));

    public string Unprotect(string protectedText) {
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("Not a DPAPI protected value.");

        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText[Prefix.Length..]), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
