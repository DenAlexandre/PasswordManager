using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace PasswordManager.Maui.Crypto;

// Derives the vault master key from the user's master password. This key never leaves the
// device and is never transmitted to the server - only used locally to decrypt the user's
// RSA private key, which in turn unwraps per-SiteGroup AES keys.
public static class KeyDerivation
{
    public const int KeySizeBytes = 32;

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(16);

    public static byte[] DeriveMasterKey(string masterPassword, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(masterPassword))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb
        };
        return argon2.GetBytes(KeySizeBytes);
    }
}
