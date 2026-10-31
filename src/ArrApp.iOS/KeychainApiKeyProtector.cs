using System;
using System.Security.Cryptography;
using ArrApp.Core;
using ArrApp.Data.Security;
using Foundation;
using Security;

namespace ArrApp.iOS;

/// <summary>
/// Keeps a random AES-256 key in the iOS Keychain (this device only, available after first unlock) and uses it for
/// AES-GCM. NOTE: compile-checked in CI only; it has not been run on a device or simulator (F-002d).
/// </summary>
public sealed class KeychainApiKeyProtector : IApiKeyProtector {
    private const string Service = "ArrApp";
    private const string Account = "apikey-master";

    private readonly AesGcmApiKeyProtector _inner = new(GetOrCreateKey());

    public string Protect(string plainText) => _inner.Protect(plainText);
    public string Unprotect(string protectedText) => _inner.Unprotect(protectedText);

    private static byte[] GetOrCreateKey() {
        var query = new SecRecord(SecKind.GenericPassword) { Service = Service, Account = Account };
        var existing = SecKeyChain.QueryAsRecord(query, out var status);
        if (status == SecStatusCode.Success && existing?.ValueData is { } data)
            return data.ToArray();

        var key = RandomNumberGenerator.GetBytes(32);
        var record = new SecRecord(SecKind.GenericPassword) {
            Service = Service,
            Account = Account,
            ValueData = NSData.FromArray(key),
            Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly,
        };
        var add = SecKeyChain.Add(record);
        if (add != SecStatusCode.Success)
            throw new InvalidOperationException($"Could not store the encryption key in the Keychain ({add}).");
        return key;
    }
}
