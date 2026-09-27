using System.Security.Cryptography;

namespace PasswordManager.Maui.Crypto;

// Wraps/unwraps per-SiteGroup AES keys with a user's RSA keypair (RSA-OAEP/SHA-256),
// mirroring the Bitwarden org "collection key" invite pattern.
public static class RsaKeyWrapping
{
    private const int KeySizeBits = 3072;

    public static (string PublicKeyPem, byte[] PrivateKeyDer) GenerateKeyPair()
    {
        using var rsa = RSA.Create(KeySizeBits);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        var privateKeyDer = rsa.ExportPkcs8PrivateKey();
        return (publicKeyPem, privateKeyDer);
    }

    public static string WrapKey(string publicKeyPem, byte[] symmetricKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var wrapped = rsa.Encrypt(symmetricKey, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(wrapped);
    }

    public static byte[] UnwrapKey(byte[] privateKeyDer, string wrappedKeyBase64)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyDer, out _);
        var wrapped = Convert.FromBase64String(wrappedKeyBase64);
        return rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256);
    }
}
