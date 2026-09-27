using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Maui.Crypto;

// Produces/consumes a single self-contained base64 blob: nonce (12 bytes) || ciphertext || tag (16 bytes).
public static class AesGcmCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Encrypt(byte[] key, string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        var blob = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, blob, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, blob, NonceSize + ciphertext.Length, TagSize);
        return Convert.ToBase64String(blob);
    }

    public static string Decrypt(byte[] key, string blobBase64)
    {
        var blob = Convert.FromBase64String(blobBase64);
        var nonce = blob[..NonceSize];
        var tag = blob[^TagSize..];
        var ciphertext = blob[NonceSize..^TagSize];
        var plainBytes = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public static string EncryptBytes(byte[] key, byte[] plainBytes)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        var blob = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, blob, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, blob, NonceSize + ciphertext.Length, TagSize);
        return Convert.ToBase64String(blob);
    }

    public static byte[] DecryptBytes(byte[] key, string blobBase64)
    {
        var blob = Convert.FromBase64String(blobBase64);
        var nonce = blob[..NonceSize];
        var tag = blob[^TagSize..];
        var ciphertext = blob[NonceSize..^TagSize];
        var plainBytes = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plainBytes);
        return plainBytes;
    }

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);
}
