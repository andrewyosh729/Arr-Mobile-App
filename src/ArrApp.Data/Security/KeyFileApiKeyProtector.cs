using System.Security.Cryptography;

namespace ArrApp.Data.Security;

/// <summary>
/// Fallback for platforms with no OS key store wired up (Linux/macOS desktop). The AES key lives in a file next to
/// the database with owner-only permissions. This stops casual reads of <c>arr.db</c> but NOT someone with access to the
/// whole user profile, so it is weaker than DPAPI / Keystore / Keychain. Mobile and Windows builds do not use it.
/// </summary>
public static class KeyFileApiKeyProtector {
    public static AesGcmApiKeyProtector Create(string keyFilePath) {
        byte[] key;
        if (File.Exists(keyFilePath)) {
            key = File.ReadAllBytes(keyFilePath);
        } else {
            Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)!);
            key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(keyFilePath, key);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return new AesGcmApiKeyProtector(key);
    }
}
