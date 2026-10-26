using System.Security.Cryptography;

namespace Automation.Helpers;

/// <summary>
/// Decrypts media files downloaded from WhatsApp (e.g. voice notes). WhatsApp end-to-end encrypts media
/// separately from the message itself: the message only carries a <c>mediaKey</c>, from which the actual
/// AES key, IV and HMAC key are derived with HKDF. See e.g. the WhatsApp Business API media encryption
/// documentation, or the Baileys/whatsapp-web.js source, for the (reverse-engineered) algorithm.
/// </summary>
public static class WhatsAppMediaDecryptor
{
    /// <summary>The HKDF "info" string differs per media kind and determines the derived keys.</summary>
    public enum MediaKind
    {
        Image,
        Video,
        Audio,
        Document
    }

    /// <summary>
    /// Decrypts a WhatsApp media file.
    /// </summary>
    /// <param name="encrypted">The raw bytes downloaded from the media URL: AES-CBC ciphertext followed by a 10-byte HMAC tag.</param>
    /// <param name="mediaKey">The base64-encoded <c>mediaKey</c> from the message.</param>
    /// <param name="kind">The kind of media, which determines the HKDF info string.</param>
    /// <returns>The decrypted file content.</returns>
    /// <exception cref="CryptographicException">The HMAC tag does not match: the file is corrupt, or the key/kind is wrong.</exception>
    public static byte[] Decrypt(ReadOnlySpan<byte> encrypted, string mediaKey, MediaKind kind)
    {
        if (encrypted.Length <= MacLength)
            throw new CryptographicException("The encrypted media is too short to contain a MAC");

        var ciphertext = encrypted[..^MacLength];
        var mac = encrypted[^MacLength..];

        var expanded = ExpandMediaKey(Convert.FromBase64String(mediaKey), kind);
        var iv = expanded.AsSpan(0, 16);
        var cipherKey = expanded.AsSpan(16, 32);
        var macKey = expanded.AsSpan(48, 32);

        var expectedMac = ComputeMac(macKey, iv, ciphertext);
        if (!CryptographicOperations.FixedTimeEquals(expectedMac, mac))
            throw new CryptographicException("The media MAC does not match; it may be corrupt, or the key/kind is wrong");

        using var aes = Aes.Create();
        aes.Key = [.. cipherKey];
        aes.IV = [.. iv];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock([.. ciphertext], 0, ciphertext.Length);
    }

    /// <summary>The length, in bytes, of the HMAC-SHA256 tag WhatsApp appends to encrypted media.</summary>
    private const int MacLength = 10;

    /// <summary>
    /// Expands the 32-byte <c>mediaKey</c> into 112 bytes with HKDF-SHA256: IV (16), cipher key (32),
    /// MAC key (32) and a sideloaded data key (32, unused here).
    /// </summary>
    private static byte[] ExpandMediaKey(byte[] mediaKey, MediaKind kind)
    {
        var info = System.Text.Encoding.UTF8.GetBytes($"WhatsApp {kind} Keys");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, mediaKey, outputLength: 112, salt: null, info: info);
    }

    private static byte[] ComputeMac(ReadOnlySpan<byte> macKey, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> ciphertext)
    {
        var data = new byte[iv.Length + ciphertext.Length];
        iv.CopyTo(data);
        ciphertext.CopyTo(data.AsSpan(iv.Length));

        Span<byte> fullMac = stackalloc byte[32];
        HMACSHA256.HashData(macKey, data, fullMac);
        return [.. fullMac[..MacLength]];
    }
}
