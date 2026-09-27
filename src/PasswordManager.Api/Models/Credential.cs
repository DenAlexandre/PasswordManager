namespace PasswordManager.Api.Models;

// All Encrypted* fields hold a self-contained AES-256-GCM blob (nonce + ciphertext + tag, base64),
// encrypted client-side with the owning SiteGroup's symmetric key. The server never sees plaintext.
public class Credential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    public string EncryptedLabel { get; set; } = string.Empty;
    public string EncryptedUsername { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? EncryptedNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}
