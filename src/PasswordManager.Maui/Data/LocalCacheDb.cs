using SQLite;

namespace PasswordManager.Maui.Data;

public class LocalCacheDb
{
    private readonly SQLiteAsyncConnection _connection;

    public LocalCacheDb()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "vault-cache.db3");
        _connection = new SQLiteAsyncConnection(path);
        _connection.CreateTableAsync<CachedIdentity>().Wait();
        _connection.CreateTableAsync<CachedSiteGroup>().Wait();
        _connection.CreateTableAsync<CachedSite>().Wait();
        _connection.CreateTableAsync<CachedCredential>().Wait();
        _connection.CreateTableAsync<OutboxItem>().Wait();
    }

    public async Task<CachedIdentity?> GetIdentityAsync() =>
        await _connection.Table<CachedIdentity>().FirstOrDefaultAsync() as CachedIdentity;

    public Task SaveIdentityAsync(CachedIdentity identity) =>
        _connection.InsertOrReplaceAsync(identity);

    public Task<List<CachedSiteGroup>> GetSiteGroupsAsync() =>
        _connection.Table<CachedSiteGroup>().ToListAsync();

    public Task<List<CachedSite>> GetSitesAsync(Guid siteGroupId) =>
        _connection.Table<CachedSite>().Where(s => s.SiteGroupId == siteGroupId && !s.IsDeleted).ToListAsync();

    public Task<List<CachedSite>> GetAllSitesAsync() =>
        _connection.Table<CachedSite>().Where(s => !s.IsDeleted).ToListAsync();

    public Task<List<CachedCredential>> GetCredentialsAsync(Guid siteId) =>
        _connection.Table<CachedCredential>().Where(c => c.SiteId == siteId && !c.IsDeleted).ToListAsync();

    public async Task ReplaceSiteGroupsAsync(IEnumerable<CachedSiteGroup> groups)
    {
        await _connection.DeleteAllAsync<CachedSiteGroup>();
        await _connection.InsertAllAsync(groups);
    }

    public async Task UpsertSitesAsync(IEnumerable<CachedSite> sites)
    {
        foreach (var site in sites) await _connection.InsertOrReplaceAsync(site);
    }

    public async Task UpsertCredentialsAsync(IEnumerable<CachedCredential> credentials)
    {
        foreach (var credential in credentials) await _connection.InsertOrReplaceAsync(credential);
    }

    public Task EnqueueOutboxAsync(OutboxItem item) => _connection.InsertAsync(item);

    public Task<List<OutboxItem>> GetOutboxAsync() => _connection.Table<OutboxItem>().OrderBy(i => i.Id).ToListAsync();

    public Task RemoveOutboxItemAsync(OutboxItem item) => _connection.DeleteAsync(item);
}
