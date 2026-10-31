namespace ArrApp.Core;

/// <summary>
/// Encrypts API keys before they reach SQLite. Implementations are platform-specific
/// (DPAPI on Windows, Android Keystore, iOS Keychain); see F-109.
/// </summary>
public interface IApiKeyProtector {
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
