using System.Security.Cryptography;
using ArrApp.Data.Security;

namespace ArrApp.Data.Tests;

public class ApiKeyProtectorTests {
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void AesGcm_Round_Trips_And_Hides_The_Plain_Text() {
        var protector = new AesGcmApiKeyProtector(NewKey());

        var protectedText = protector.Protect("abcdef0123456789");

        Assert.DoesNotContain("abcdef0123456789", protectedText);
        Assert.Equal("abcdef0123456789", protector.Unprotect(protectedText));
    }

    [Fact]
    public void AesGcm_Uses_A_Fresh_Nonce_Every_Time() {
        var protector = new AesGcmApiKeyProtector(NewKey());

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void AesGcm_Handles_Empty_And_Unicode() {
        var protector = new AesGcmApiKeyProtector(NewKey());

        Assert.Equal("", protector.Unprotect(protector.Protect("")));
        Assert.Equal("kéy-日本-🔑", protector.Unprotect(protector.Protect("kéy-日本-🔑")));
    }

    [Fact]
    public void AesGcm_Rejects_Tampered_Data() {
        var protector = new AesGcmApiKeyProtector(NewKey());
        var text = protector.Protect("secret");
        var bytes = Convert.FromBase64String(text["aesgcm1:".Length..]);
        bytes[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect("aesgcm1:" + Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void AesGcm_Rejects_A_Different_Key() {
        var text = new AesGcmApiKeyProtector(NewKey()).Protect("secret");

        Assert.ThrowsAny<CryptographicException>(() => new AesGcmApiKeyProtector(NewKey()).Unprotect(text));
    }

    [Fact]
    public void AesGcm_Rejects_Foreign_And_Truncated_Values() {
        var protector = new AesGcmApiKeyProtector(NewKey());

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect("plain-text-key"));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect("aesgcm1:AAAA"));
    }

    [Fact]
    public void AesGcm_Requires_A_32_Byte_Key() {
        Assert.Throws<ArgumentException>(() => new AesGcmApiKeyProtector(new byte[16]));
    }

    [Fact]
    public void KeyFile_Creates_The_Key_Once_And_Reuses_It() {
        var dir = Path.Combine(Path.GetTempPath(), "arrapp-key-test-" + Guid.NewGuid().ToString("N"));
        try {
            var path = Path.Combine(dir, "sub", "apikey.key");
            var text = KeyFileApiKeyProtector.Create(path).Protect("secret");

            Assert.Equal(32, new FileInfo(path).Length);
            Assert.Equal("secret", KeyFileApiKeyProtector.Create(path).Unprotect(text));
        } finally {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Dpapi_Round_Trips_On_Windows() {
        if (!OperatingSystem.IsWindows())
            return;

        var protector = new DpapiApiKeyProtector();
        var text = protector.Protect("secret");

        Assert.DoesNotContain("secret", text);
        Assert.Equal("secret", protector.Unprotect(text));
    }

    [Fact]
    public async Task Repository_Returns_Empty_Key_Instead_Of_Failing_When_A_Key_Cannot_Be_Decrypted() {
        await using var t = new TestDb();
        var server = TestDb.Server("Home");
        await t.Repository.AddAsync(server);
        await (await t.Database.GetConnectionAsync()).ExecuteAsync("UPDATE ServerConnection SET ProtectedApiKey = 'garbage'");

        var loaded = Assert.Single(await t.Repository.GetAllAsync());

        Assert.Equal("", loaded.ApiKey);
        Assert.Equal("Home", loaded.Name);
    }
}
