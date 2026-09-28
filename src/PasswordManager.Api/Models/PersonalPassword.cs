namespace PasswordManager.Api.Models;

// Single-owner counterpart to Credential - not attached to any SiteGroup/Site, never shared.
// All Encrypted* fields hold a self-contained AES-256-GCM blob (nonce + ciphertext + tag, base64),
// encrypted client-side with the owning user's personal-vault key. The server never sees plaintext.
public class PersonalPassword
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string EncryptedLabel { get; set; } = string.Empty;
    public string EncryptedUsername { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? EncryptedUrl { get; set; }
    public string? EncryptedNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}
