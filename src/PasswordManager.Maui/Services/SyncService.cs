using PasswordManager.Maui.Data;

namespace PasswordManager.Maui.Services;

public class SyncService
{
    private readonly ApiClient _api;
    private readonly LocalCacheDb _cache;

    public SyncService(ApiClient api, LocalCacheDb cache)
    {
        _api = api;
        _cache = cache;
    }

    // Pulls the full accessible dataset and refreshes the local encrypted cache. Safe to call
    // opportunistically (app start, resume, pull-to-refresh) - silently no-ops when offline so
    // the UI can keep serving whatever is already cached.
    public async Task<bool> SyncAsync()
    {
        var response = await _api.SyncAsync(since: null);
        if (response is null) return false;

        await _cache.ReplaceSiteGroupsAsync(response.SiteGroups.Select(g => new CachedSiteGroup
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            Role = (int)g.Role,
            EncryptedGroupKey = g.EncryptedGroupKey
        }));

        await _cache.UpsertSitesAsync(response.Sites.Select(s => new CachedSite
        {
            Id = s.Id,
            SiteGroupId = s.SiteGroupId,
            ParentSiteId = s.ParentSiteId,
            Name = s.Name,
            Url = s.Url,
            Notes = s.Notes,
            UpdatedAt = s.UpdatedAt,
            IsDeleted = s.IsDeleted
        }));

        await _cache.UpsertCredentialsAsync(response.Credentials.Select(c => new CachedCredential
        {
            Id = c.Id,
            SiteId = c.SiteId,
            EncryptedLabel = c.EncryptedLabel,
            EncryptedUsername = c.EncryptedUsername,
            EncryptedPassword = c.EncryptedPassword,
            EncryptedUrl = c.EncryptedUrl,
            EncryptedNotes = c.EncryptedNotes,
            UpdatedAt = c.UpdatedAt,
            IsDeleted = c.IsDeleted
        }));

        return true;
    }
}
