using System.Security.Cryptography;
using Automation.Helpers;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Helpers;

public class WhatsAppMediaDecryptorTests
{
    [Fact]
    public void Decrypt_RoundTripsDataEncryptedTheSameWayWhatsAppDoes()
    {
        var mediaKey = RandomNumberGenerator.GetBytes(32);
        var mediaKeyBase64 = Convert.ToBase64String(mediaKey);
        var plaintext = "This is the content of a WhatsApp voice note"u8.ToArray();

        var encrypted = WhatsAppMediaTestEncryptor.Encrypt(plaintext, mediaKey, WhatsAppMediaDecryptor.MediaKind.Audio);

        var decrypted = WhatsAppMediaDecryptor.Decrypt(encrypted, mediaKeyBase64, WhatsAppMediaDecryptor.MediaKind.Audio);

        decrypted.Should().Equal(plaintext);
    }

    [Fact]
    public void Decrypt_WithTheWrongMediaKind_FailsTheMacCheck()
    {
        var mediaKey = RandomNumberGenerator.GetBytes(32);
        var mediaKeyBase64 = Convert.ToBase64String(mediaKey);
        var plaintext = "Audio bytes"u8.ToArray();
        var encrypted = WhatsAppMediaTestEncryptor.Encrypt(plaintext, mediaKey, WhatsAppMediaDecryptor.MediaKind.Audio);

        var act = () => WhatsAppMediaDecryptor.Decrypt(encrypted, mediaKeyBase64, WhatsAppMediaDecryptor.MediaKind.Image);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithATamperedFile_FailsTheMacCheck()
    {
        var mediaKey = RandomNumberGenerator.GetBytes(32);
        var mediaKeyBase64 = Convert.ToBase64String(mediaKey);
        var encrypted = WhatsAppMediaTestEncryptor.Encrypt("Audio bytes"u8.ToArray(), mediaKey, WhatsAppMediaDecryptor.MediaKind.Audio);
        encrypted[0] ^= 0xFF;

        var act = () => WhatsAppMediaDecryptor.Decrypt(encrypted, mediaKeyBase64, WhatsAppMediaDecryptor.MediaKind.Audio);

        act.Should().Throw<CryptographicException>();
    }
}
