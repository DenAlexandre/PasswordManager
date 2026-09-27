namespace PasswordManager.Api.Models;

// Grants a user a role on a SiteGroup. EncryptedGroupKey is the SiteGroup's AES symmetric key,
// wrapped (RSA-OAEP) with this user's public key - the key-wrapping ("invite") is performed
// client-side by an existing member/admin who already holds the group key in cleartext in memory.
public class UserSiteGroupAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid SiteGroupId { get; set; }
    public SiteGroup? SiteGroup { get; set; }

    public AccessRole Role { get; set; } = AccessRole.None;
    public string EncryptedGroupKey { get; set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}
