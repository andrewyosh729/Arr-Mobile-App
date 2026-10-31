using System;
using System.Linq;
using Android.Runtime;
using Android.Security.Keystore;
using ArrApp.Core;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace ArrApp.Android;

/// <summary>
/// AES-256-GCM using a key generated inside the Android Keystore. The key material never enters the app process and
/// cannot be exported, so a copy of the app's data directory alone is not enough to read the API keys.
/// Output: <c>keystore1:</c> + base64(iv | ciphertext+tag).
/// </summary>
public sealed class AndroidKeystoreApiKeyProtector : IApiKeyProtector {
    private const string Prefix = "keystore1:";
    private const string Provider = "AndroidKeyStore";
    private const string Alias = "arrapp_apikey";
    private const string Transformation = "AES/GCM/NoPadding";
    private const int IvSize = 12;
    private const int TagBits = 128;

    private readonly ISecretKey _key = GetOrCreateKey();

    public string Protect(string plainText) {
        var cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(CipherMode.EncryptMode, _key);
        var iv = cipher.GetIV()!;
        var encrypted = cipher.DoFinal(System.Text.Encoding.UTF8.GetBytes(plainText))!;
        return Prefix + Convert.ToBase64String(iv.Concat(encrypted).ToArray());
    }

    public string Unprotect(string protectedText) {
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Not a Keystore protected value.");

        var data = Convert.FromBase64String(protectedText[Prefix.Length..]);
        var cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(CipherMode.DecryptMode, _key, new GCMParameterSpec(TagBits, data, 0, IvSize));
        var plain = cipher.DoFinal(data, IvSize, data.Length - IvSize)!;
        return System.Text.Encoding.UTF8.GetString(plain);
    }

    private static ISecretKey GetOrCreateKey() {
        var store = KeyStore.GetInstance(Provider)!;
        store.Load(null);

        if (!store.ContainsAlias(Alias)) {
            var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, Provider)!;
            generator.Init(new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)!
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
                .SetKeySize(256)!
                .Build());
            generator.GenerateKey();
        }

        // Java objects need JavaCast; a plain C# cast fails here.
        return store.GetKey(Alias, null)!.JavaCast<ISecretKey>()!;
    }
}
