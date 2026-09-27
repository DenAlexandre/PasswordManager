using SQLite;

namespace PasswordManager.Maui.Data;

// Every sensitive column below already holds ciphertext produced by the zero-knowledge crypto
// layer (AES-256-GCM for vault contents, RSA-OAEP-wrapped for group keys, master-key-encrypted
// for the private key). The cache file itself is not additionally full-disk encrypted, but a
// raw copy of it reveals nothing without the user's master password.
public class CachedIdentity
{
    [PrimaryKey] public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string MasterPasswordSalt { get; set; } = string.Empty;
    public int KdfIterations { get; set; }
    public int KdfMemoryKb { get; set; }
    public int KdfParallelism { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public string EncryptedPrivateKey { get; set; } = string.Empty;
    public DateTimeOffset LastSyncedAt { get; set; }
}

public class CachedSiteGroup
{
    [PrimaryKey] public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Role { get; set; }
    public string EncryptedGroupKey { get; set; } = string.Empty;
}

public class CachedSite
{
    [PrimaryKey] public Guid Id { get; set; }
    [Indexed] public Guid SiteGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class CachedCredential
{
    [PrimaryKey] public Guid Id { get; set; }
    [Indexed] public Guid SiteId { get; set; }
    public string EncryptedLabel { get; set; } = string.Empty;
    public string EncryptedUsername { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? EncryptedNotes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

// Queued writes made while offline; replayed against the API in order once connectivity returns.
public class OutboxItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string OperationJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
