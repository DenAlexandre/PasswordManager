using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;

namespace PasswordManager.Maui.Services;

public record GroupRestorePlanItem(Guid GroupId, string Name, bool AlreadyExists, int SitesToCreate, int CredentialsToCreate);

public record GroupRestoreResult(
    Guid GroupId, string Name, bool Skipped, string? SkipReason,
    int SitesCreated, int CredentialsCreated, List<string> Warnings, List<string> Errors);

// Builds/reads the disaster-recovery backup file and drives its restore onto the server.
// See CLAUDE.md's zero-knowledge model: export only ever moves already-existing ciphertext
// (credentials + RSA-wrapped group keys) into a file; restore only ever decrypts in-memory on
// the importing user's own device, using a key unwrapped with their own private key.
public class BackupService
{
    private readonly ApiClient _api;
    private readonly LocalCacheDb _cache;
    private readonly VaultSession _session;
    private readonly SyncService _sync;
    private readonly IFileSaver _fileSaver;

    public BackupService(ApiClient api, LocalCacheDb cache, VaultSession session, SyncService sync, IFileSaver fileSaver)
    {
        _api = api;
        _cache = cache;
        _session = session;
        _sync = sync;
        _fileSaver = fileSaver;
    }

    public Task<List<CachedSiteGroup>> GetExportCandidatesAsync() => _cache.GetSiteGroupsAsync();

    public async Task<BackupFileV1?> BuildExportAsync(IReadOnlyCollection<Guid> siteGroupIds)
    {
        var identity = await _cache.GetIdentityAsync();
        if (identity is null) return null;

        var selected = (await _cache.GetSiteGroupsAsync()).Where(g => siteGroupIds.Contains(g.Id));

        var backupGroups = new List<BackupSiteGroup>();
        foreach (var group in selected)
        {
            var accessList = await _api.GetAccessAsync(group.Id) ?? new List<AccessGrantDto>();
            var members = accessList
                .Select(a => new BackupMember(a.UserId, a.UserEmail, a.Role, a.EncryptedGroupKey))
                .ToList();

            var sites = await _cache.GetSitesAsync(group.Id);
            var backupSites = sites.Select(s => new BackupSite(s.Id, s.ParentSiteId, s.Name, s.Url, s.Notes)).ToList();

            var backupCredentials = new List<BackupCredential>();
            foreach (var site in sites)
            {
                var credentials = await _cache.GetCredentialsAsync(site.Id);
                backupCredentials.AddRange(credentials.Select(c => new BackupCredential(
                    c.Id, c.SiteId, c.EncryptedLabel, c.EncryptedUsername, c.EncryptedPassword, c.EncryptedUrl, c.EncryptedNotes)));
            }

            backupGroups.Add(new BackupSiteGroup(group.Id, group.Name, group.Description, members, backupSites, backupCredentials));
        }

        return new BackupFileV1(BackupFormat.CurrentVersion, DateTimeOffset.UtcNow, identity.UserId, identity.Email, backupGroups);
    }

    public async Task<(bool Success, string? FilePath)> SaveToFileAsync(BackupFileV1 backup)
    {
        try
        {
            await Permissions.RequestAsync<Permissions.StorageWrite>();
        }
        catch (FeatureNotSupportedException)
        {
            // Not required on this platform (iOS/Windows/MacCatalyst) - the system save dialog handles access.
        }

        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var fileName = $"passwordmanager-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var result = await _fileSaver.SaveAsync(fileName, stream, CancellationToken.None);
        return (result.IsSuccessful, result.IsSuccessful ? result.FilePath : result.Exception?.Message);
    }

    public async Task<(BackupFileV1? Backup, string? Error)> LoadFromFileAsync(Stream fileStream)
    {
        try
        {
            var backup = await JsonSerializer.DeserializeAsync<BackupFileV1>(fileStream);
            if (backup is null) return (null, "Fichier illisible.");
            if (backup.FormatVersion > BackupFormat.CurrentVersion)
                return (null, $"Ce fichier a été créé par une version plus récente de l'application (format v{backup.FormatVersion}).");
            return (backup, null);
        }
        catch (JsonException)
        {
            return (null, "Ce fichier n'est pas une sauvegarde valide.");
        }
    }

    // Diffs the backup against live server state - callers should have just run SyncService.SyncAsync()
    // so this reflects current truth rather than a possibly-stale cache.
    public async Task<List<GroupRestorePlanItem>> BuildRestorePreviewAsync(BackupFileV1 backup)
    {
        var existingGroups = (await _cache.GetSiteGroupsAsync()).ToDictionary(g => g.Id);

        var plan = new List<GroupRestorePlanItem>();
        foreach (var group in backup.SiteGroups)
        {
            if (existingGroups.ContainsKey(group.Id))
            {
                var existingSiteIds = (await _cache.GetSitesAsync(group.Id)).Select(s => s.Id).ToHashSet();
                var missingSites = group.Sites.Count(s => !existingSiteIds.Contains(s.Id));

                var existingCredentialIds = new HashSet<Guid>();
                foreach (var siteId in existingSiteIds)
                    existingCredentialIds.UnionWith((await _cache.GetCredentialsAsync(siteId)).Select(c => c.Id));
                var missingCredentials = group.Credentials.Count(c => !existingCredentialIds.Contains(c.Id));

                plan.Add(new GroupRestorePlanItem(group.Id, group.Name, true, missingSites, missingCredentials));
            }
            else
            {
                plan.Add(new GroupRestorePlanItem(group.Id, group.Name, false, group.Sites.Count, group.Credentials.Count));
            }
        }
        return plan;
    }

    public async Task<List<GroupRestoreResult>> RestoreAsync(BackupFileV1 backup, IReadOnlyCollection<Guid> selectedGroupIds)
    {
        var existingGroups = (await _cache.GetSiteGroupsAsync()).ToDictionary(g => g.Id);
        var results = new List<GroupRestoreResult>();

        foreach (var group in backup.SiteGroups.Where(g => selectedGroupIds.Contains(g.Id)))
        {
            results.Add(existingGroups.TryGetValue(group.Id, out var existing)
                ? await RestoreExistingGroupAsync(group, existing)
                : await RestoreMissingGroupAsync(group));
        }

        await _sync.SyncAsync();
        return results;
    }

    // Group still exists under its original Id - its ciphertext is still valid under its unchanged
    // key, so only the missing sites/credentials are recreated, verbatim, no re-encryption. Any
    // member with Write access can do this; no admin endpoints involved.
    private async Task<GroupRestoreResult> RestoreExistingGroupAsync(BackupSiteGroup group, CachedSiteGroup existing)
    {
        var errors = new List<string>();

        var existingSites = await _cache.GetSitesAsync(existing.Id);
        var oldToNewSiteId = existingSites.ToDictionary(s => s.Id, s => s.Id);

        var existingCredentialIds = new HashSet<Guid>();
        foreach (var site in existingSites)
            existingCredentialIds.UnionWith((await _cache.GetCredentialsAsync(site.Id)).Select(c => c.Id));

        var missingSites = group.Sites.Where(s => !oldToNewSiteId.ContainsKey(s.Id)).ToList();
        var sitesCreated = await CreateSitesAsync(existing.Id, missingSites, oldToNewSiteId, errors);

        var credentialsCreated = 0;
        foreach (var cred in group.Credentials.Where(c => !existingCredentialIds.Contains(c.Id)))
        {
            if (!oldToNewSiteId.TryGetValue(cred.SiteId, out var newSiteId))
            {
                errors.Add("Un identifiant a été ignoré (site associé absent).");
                continue;
            }

            var request = new UpsertCredentialRequest(
                cred.EncryptedLabel, cred.EncryptedUsername, cred.EncryptedPassword, cred.EncryptedUrl, cred.EncryptedNotes);
            var created = await _api.CreateCredentialAsync(newSiteId, request);
            if (created is not null) credentialsCreated++;
            else errors.Add("Un identifiant n'a pas pu être restauré (droits d'écriture requis).");
        }

        return new GroupRestoreResult(group.Id, group.Name, false, null, sitesCreated, credentialsCreated, new List<string>(), errors);
    }

    // Group is fully missing - recreating it and re-granting member access requires admin rights
    // (existing endpoints are AdminOnly). Attempted regardless of session.IsAdmin and reported as
    // "skipped - requires admin" on failure, rather than pre-gating on the client.
    private async Task<GroupRestoreResult> RestoreMissingGroupAsync(BackupSiteGroup group)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        var myMember = group.Members.FirstOrDefault(m => m.UserId == _session.UserId);
        if (myMember is null)
            return new GroupRestoreResult(group.Id, group.Name, true,
                "vous n'étiez pas membre de cette database dans la sauvegarde.", 0, 0, warnings, errors);

        // Old key, needed only transiently to decrypt this group's credentials below.
        var oldGroupKey = _session.GetOrUnwrapGroupKey(group.Id, myMember.EncryptedGroupKey);

        var keyMaterial = await _api.GetKeyMaterialAsync();
        if (keyMaterial is null)
            return new GroupRestoreResult(group.Id, group.Name, true,
                "impossible de récupérer votre clé publique actuelle.", 0, 0, warnings, errors);

        var tempId = Guid.NewGuid();
        var (_, newGroupKey) = _session.CreateNewGroupKey(tempId);
        var wrappedForSelf = RsaKeyWrapping.WrapKey(keyMaterial.PublicKey, newGroupKey);

        var (createdGroup, createError) = await _api.CreateSiteGroupWithReasonAsync(
            new CreateSiteGroupRequest(group.Name, group.Description, wrappedForSelf));
        if (createdGroup is null)
            return new GroupRestoreResult(group.Id, group.Name, true,
                $"droits administrateur requis ({createError}).", 0, 0, warnings, errors);

        _session.GetOrUnwrapGroupKey(createdGroup.Id, wrappedForSelf);

        var users = await _api.GetUsersAsync() ?? new List<UserSummaryDto>();
        foreach (var member in group.Members.Where(m => m.UserId != _session.UserId))
        {
            var user = users.FirstOrDefault(u => u.Id == member.UserId);
            if (user?.PublicKey is null)
            {
                warnings.Add($"{member.UserEmail} : accès non restauré (compte introuvable ou coffre jamais initialisé).");
                continue;
            }
            var wrapped = RsaKeyWrapping.WrapKey(user.PublicKey, newGroupKey);
            var granted = await _api.GrantAccessAsync(createdGroup.Id, new GrantAccessRequest(member.UserId, member.Role, wrapped));
            if (!granted) warnings.Add($"{member.UserEmail} : l'octroi d'accès a échoué.");
        }

        var oldToNewSiteId = new Dictionary<Guid, Guid>();
        var sitesCreated = await CreateSitesAsync(createdGroup.Id, group.Sites, oldToNewSiteId, errors);

        var credentialsCreated = 0;
        foreach (var cred in group.Credentials)
        {
            if (!oldToNewSiteId.TryGetValue(cred.SiteId, out var newSiteId))
            {
                errors.Add("Un identifiant a été ignoré (site associé non recréé).");
                continue;
            }

            string label, username, password;
            string? url, notes;
            try
            {
                label = AesGcmCipher.Decrypt(oldGroupKey, cred.EncryptedLabel);
                username = AesGcmCipher.Decrypt(oldGroupKey, cred.EncryptedUsername);
                password = AesGcmCipher.Decrypt(oldGroupKey, cred.EncryptedPassword);
                url = cred.EncryptedUrl is null ? null : AesGcmCipher.Decrypt(oldGroupKey, cred.EncryptedUrl);
                notes = cred.EncryptedNotes is null ? null : AesGcmCipher.Decrypt(oldGroupKey, cred.EncryptedNotes);
            }
            catch (CryptographicException)
            {
                errors.Add("Un identifiant n'a pas pu être déchiffré (clé de database incorrecte).");
                continue;
            }

            var request = new UpsertCredentialRequest(
                AesGcmCipher.Encrypt(newGroupKey, label),
                AesGcmCipher.Encrypt(newGroupKey, username),
                AesGcmCipher.Encrypt(newGroupKey, password),
                url is null ? null : AesGcmCipher.Encrypt(newGroupKey, url),
                notes is null ? null : AesGcmCipher.Encrypt(newGroupKey, notes));

            var created = await _api.CreateCredentialAsync(newSiteId, request);
            if (created is not null) credentialsCreated++;
            else errors.Add("Un identifiant n'a pas pu être restauré.");
        }

        return new GroupRestoreResult(group.Id, group.Name, false, null, sitesCreated, credentialsCreated, warnings, errors);
    }

    // Iteratively creates sites whose parent is already resolved (root sites first, then their
    // children, etc.) - avoids a recursive tree-sort. Terminates in at most depth-of-tree passes.
    private async Task<int> CreateSitesAsync(
        Guid targetGroupId, List<BackupSite> sitesToCreate, Dictionary<Guid, Guid> oldToNewSiteId, List<string> errors)
    {
        var created = 0;
        var pending = new List<BackupSite>(sitesToCreate);
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var site in pending.ToList())
            {
                if (site.ParentSiteId is not null && !oldToNewSiteId.ContainsKey(site.ParentSiteId.Value))
                    continue;

                var newParentId = site.ParentSiteId is null ? (Guid?)null : oldToNewSiteId[site.ParentSiteId.Value];
                var result = await _api.CreateSiteAsync(targetGroupId, new UpsertSiteRequest(site.Name, site.Url, site.Notes, newParentId));
                if (result is not null) { oldToNewSiteId[site.Id] = result.Id; created++; }
                else errors.Add($"Site « {site.Name} » : échec de la création.");

                pending.Remove(site);
                progressed = true;
            }

            if (!progressed)
            {
                foreach (var s in pending) errors.Add($"Site « {s.Name} » : site parent introuvable, ignoré.");
                break;
            }
        }
        return created;
    }
}
