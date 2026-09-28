namespace PasswordManager.Api.Models;

// A file attachment in a user's personal vault. EncryptedFileName/EncryptedContent are
// self-contained AES-256-GCM blobs (base64), encrypted client-side with the user's personal-vault
// key - the server never sees plaintext bytes. ContentType/FileSizeBytes stay in the clear (low
// sensitivity metadata, same tier as Site.Name/Url), needed for listing/UI without decrypting.
public class PersonalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string EncryptedFileName { get; set; } = string.Empty;
    public string EncryptedContent { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}
