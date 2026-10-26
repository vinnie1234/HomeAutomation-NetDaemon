using System.Security.Cryptography;
using Automation.Helpers;

namespace TestAutomation.Helpers;

/// <summary>
/// A reference implementation of WhatsApp's media encryption (the inverse of <see cref="WhatsAppMediaDecryptor"/>),
/// written independently so a bug shared between production code and a test fake can't hide a defect. Shared by
/// tests that need to simulate a downloaded, still-encrypted WhatsApp media file.
/// </summary>
public static class WhatsAppMediaTestEncryptor
{
    public static byte[] Encrypt(byte[] plaintext, byte[] mediaKey, WhatsAppMediaDecryptor.MediaKind kind)
    {
        var info = System.Text.Encoding.UTF8.GetBytes($"WhatsApp {kind} Keys");
        var expanded = HKDF.DeriveKey(HashAlgorithmName.SHA256, mediaKey, outputLength: 112, salt: null, info: info);
        var iv = expanded[..16];
        var cipherKey = expanded[16..48];
        var macKey = expanded[48..80];

        using var aes = Aes.Create();
        aes.Key = cipherKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var macInput = iv.Concat(ciphertext).ToArray();
        var mac = HMACSHA256.HashData(macKey, macInput)[..10];

        return [.. ciphertext, .. mac];
    }
}
