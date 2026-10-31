using System.Security.Cryptography;
using System.Text;
using ArrApp.Core;

namespace ArrApp.Data.Security;

/// <summary>
/// Authenticated encryption with a caller-supplied 256-bit key. Output: <c>aesgcm1:</c> + base64(nonce | tag | ciphertext).
/// Where the key itself lives (Keychain, key file) is the caller's concern.
/// </summary>
public sealed class AesGcmApiKeyProtector : IApiKeyProtector {
    private const string Prefix = "aesgcm1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmApiKeyProtector(byte[] key) {
        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes (AES-256).", nameof(key));
        _key = [.. key];
    }

    public string Protect(string plainText) {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string protectedText) {
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("Not an AES-GCM protected value.");

        var data = Convert.FromBase64String(protectedText[Prefix.Length..]);
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Protected value is truncated.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var cipher = data.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
