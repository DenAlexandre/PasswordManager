using System.Net.Http.Headers;
using System.Net.Http.Json;
using PasswordManager.Maui.Models;

namespace PasswordManager.Maui.Services;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public void SetBaseAddress(string baseUrl)
    {
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public void SetAccessToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    // --- Auth ---
    public Task<LoginResponse?> SetupAdminAsync(string email, string password) =>
        PostAsync<LoginResponse>("api/auth/setup", new LoginRequest(email, password));

    public Task<LoginResponse?> LoginAsync(string email, string password) =>
        PostAsync<LoginResponse>("api/auth/login", new LoginRequest(email, password));

    public Task<VaultKeyMaterialResponse?> GetKeyMaterialAsync() =>
        GetAsync<VaultKeyMaterialResponse>("api/auth/key-material");

    public Task SetupVaultAsync(VaultSetupRequest request) =>
        _http.PostAsJsonAsync("api/auth/vault-setup", request);

    // --- Site groups / sync ---
    public Task<List<MySiteGroupDto>?> GetMySiteGroupsAsync() =>
        GetAsync<List<MySiteGroupDto>>("api/sitegroups/mine");

    public Task<SyncResponse?> SyncAsync(DateTimeOffset? since) =>
        GetAsync<SyncResponse>(since is null ? "api/sync" : $"api/sync?since={Uri.EscapeDataString(since.Value.ToString("o"))}");

    // --- Sites ---
    public Task<List<SiteDto>?> GetSitesAsync(Guid siteGroupId) =>
        GetAsync<List<SiteDto>>($"api/sitegroups/{siteGroupId}/sites");

    public Task<SiteDto?> CreateSiteAsync(Guid siteGroupId, UpsertSiteRequest request) =>
        PostAsync<SiteDto>($"api/sitegroups/{siteGroupId}/sites", request);

    public Task UpdateSiteAsync(Guid siteGroupId, Guid siteId, UpsertSiteRequest request) =>
        _http.PutAsJsonAsync($"api/sitegroups/{siteGroupId}/sites/{siteId}", request);

    public Task DeleteSiteAsync(Guid siteGroupId, Guid siteId) =>
        _http.DeleteAsync($"api/sitegroups/{siteGroupId}/sites/{siteId}");

    // --- Credentials ---
    public Task<List<CredentialDto>?> GetCredentialsAsync(Guid siteId) =>
        GetAsync<List<CredentialDto>>($"api/sites/{siteId}/credentials");

    public Task<CredentialDto?> CreateCredentialAsync(Guid siteId, UpsertCredentialRequest request) =>
        PostAsync<CredentialDto>($"api/sites/{siteId}/credentials", request);

    public Task UpdateCredentialAsync(Guid siteId, Guid credentialId, UpsertCredentialRequest request) =>
        _http.PutAsJsonAsync($"api/sites/{siteId}/credentials/{credentialId}", request);

    public Task DeleteCredentialAsync(Guid siteId, Guid credentialId) =>
        _http.DeleteAsync($"api/sites/{siteId}/credentials/{credentialId}");

    // --- Admin ---
    public Task<List<UserSummaryDto>?> GetUsersAsync() =>
        GetAsync<List<UserSummaryDto>>("api/admin/users");

    public Task<UserSummaryDto?> CreateUserAsync(CreateUserRequest request) =>
        PostAsync<UserSummaryDto>("api/admin/users", request);

    public Task UpdateUserAsync(Guid id, UpdateUserRequest request) =>
        _http.PutAsJsonAsync($"api/admin/users/{id}", request);

    public Task<List<SiteGroupDto>?> GetSiteGroupsAsync() =>
        GetAsync<List<SiteGroupDto>>("api/admin/sitegroups");

    public Task<SiteGroupDto?> CreateSiteGroupAsync(CreateSiteGroupRequest request) =>
        PostAsync<SiteGroupDto>("api/admin/sitegroups", request);

    public Task DeleteSiteGroupAsync(Guid id) =>
        _http.DeleteAsync($"api/admin/sitegroups/{id}");

    public Task<List<AccessGrantDto>?> GetAccessAsync(Guid siteGroupId) =>
        GetAsync<List<AccessGrantDto>>($"api/admin/sitegroups/{siteGroupId}/access");

    public Task GrantAccessAsync(Guid siteGroupId, GrantAccessRequest request) =>
        _http.PostAsJsonAsync($"api/admin/sitegroups/{siteGroupId}/access", request);

    public Task RevokeAccessAsync(Guid siteGroupId, Guid userId) =>
        _http.DeleteAsync($"api/admin/sitegroups/{siteGroupId}/access/{userId}");

    private async Task<T?> GetAsync<T>(string url)
    {
        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<T?> PostAsync<T>(string url, object body)
    {
        var response = await _http.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
