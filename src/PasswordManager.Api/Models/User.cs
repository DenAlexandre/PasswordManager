namespace PasswordManager.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;

    // Login password hash (Argon2id) - authenticates against the API. Distinct from the master password.
    public string PasswordHash { get; set; } = string.Empty;

    // Zero-knowledge vault key material. The server only ever stores/serves these opaque blobs.
    public string MasterPasswordSalt { get; set; } = string.Empty;
    public int KdfIterations { get; set; } = 3;
    public int KdfMemoryKb { get; set; } = 65536;
    public int KdfParallelism { get; set; } = 4;
    public string PublicKey { get; set; } = string.Empty;
    public string EncryptedPrivateKey { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Self-registration email verification (see AuthController.Register/VerifyEmail).
    public bool IsEmailVerified { get; set; }
    public string? EmailVerificationCode { get; set; }
    public DateTimeOffset? EmailVerificationCodeExpiresAt { get; set; }
    public int EmailVerificationAttempts { get; set; }

    // RSA-wrapped (for this user's own public key) AES-256 key for their personal vault -
    // a single-owner space distinct from shared SiteGroups, never wrapped for anyone else.
    public string? EncryptedPersonalVaultKey { get; set; }

    public List<UserSiteGroupAccess> SiteGroupAccesses { get; set; } = new();
}
