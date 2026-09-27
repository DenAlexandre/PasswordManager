using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Models;

namespace PasswordManager.Maui.Services;

// Holds vault secrets in memory only, for the lifetime of an unlocked session. Nothing here
// is ever persisted in cleartext - locking the vault (or exiting the app) discards it all.
public class VaultSession
{
    private byte[]? _privateKeyDer;
    private readonly Dictionary<Guid, byte[]> _groupKeys = new();

    public Guid? UserId { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsUnlocked => _privateKeyDer is not null;

    public void SetIdentity(Guid userId, bool isAdmin)
    {
        UserId = userId;
        IsAdmin = isAdmin;
    }

    public void Unlock(byte[] privateKeyDer)
    {
        _privateKeyDer = privateKeyDer;
    }

    public void Lock()
    {
        _privateKeyDer = null;
        _groupKeys.Clear();
    }

    public byte[] GetOrUnwrapGroupKey(Guid siteGroupId, string encryptedGroupKey)
    {
        if (_groupKeys.TryGetValue(siteGroupId, out var cached)) return cached;
        if (_privateKeyDer is null) throw new InvalidOperationException("Vault is locked.");

        var key = RsaKeyWrapping.UnwrapKey(_privateKeyDer, encryptedGroupKey);
        _groupKeys[siteGroupId] = key;
        return key;
    }

    public string WrapGroupKeyForPublicKey(Guid siteGroupId, string recipientPublicKeyPem)
    {
        if (!_groupKeys.TryGetValue(siteGroupId, out var key))
            throw new InvalidOperationException("Group key not loaded - open the group first.");
        return RsaKeyWrapping.WrapKey(recipientPublicKeyPem, key);
    }

    public (Guid SiteGroupId, byte[] Key) CreateNewGroupKey(Guid siteGroupId)
    {
        var key = AesGcmCipher.NewKey();
        _groupKeys[siteGroupId] = key;
        return (siteGroupId, key);
    }
}
