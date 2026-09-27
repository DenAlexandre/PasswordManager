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

    // Distinguishes "session expired" (401) from "vault not set up yet" (404) from "offline" (no
    // response at all) - collapsing these into a single null, as GetAsync does, previously caused
    // an expired token to be misread as "this account has no vault yet".
    public async Task<(bool Unauthorized, VaultKeyMaterialResponse? Data)> GetKeyMaterialWithAuthCheckAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/auth/key-material");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return (true, null);
            if (!response.IsSuccessStatusCode) return (false, null);
            return (false, await response.Content.ReadFromJsonAsync<VaultKeyMaterialResponse>());
        }
        catch (HttpRequestException)
        {
            return (false, null); // offline - caller falls back to the local cache
        }
    }

    public async Task<bool> SetupVaultAsync(VaultSetupRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/vault-setup", request);
        return response.IsSuccessStatusCode;
    }

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

    public Task<bool> UpdateSiteAsync(Guid siteGroupId, Guid siteId, UpsertSiteRequest request) =>
        PutAsync($"api/sitegroups/{siteGroupId}/sites/{siteId}", request);

    public Task<bool> DeleteSiteAsync(Guid siteGroupId, Guid siteId) =>
        DeleteAsync($"api/sitegroups/{siteGroupId}/sites/{siteId}");

    // Surfaces the server's reason (e.g. "folder not empty") instead of collapsing it to a bool.
    public async Task<(bool Success, string? Error)> DeleteSiteWithReasonAsync(Guid siteGroupId, Guid siteId)
    {
        var response = await _http.DeleteAsync($"api/sitegroups/{siteGroupId}/sites/{siteId}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Credentials ---
    public Task<List<CredentialDto>?> GetCredentialsAsync(Guid siteId) =>
        GetAsync<List<CredentialDto>>($"api/sites/{siteId}/credentials");

    public Task<CredentialDto?> CreateCredentialAsync(Guid siteId, UpsertCredentialRequest request) =>
        PostAsync<CredentialDto>($"api/sites/{siteId}/credentials", request);

    public Task<bool> UpdateCredentialAsync(Guid siteId, Guid credentialId, UpsertCredentialRequest request) =>
        PutAsync($"api/sites/{siteId}/credentials/{credentialId}", request);

    public Task<bool> DeleteCredentialAsync(Guid siteId, Guid credentialId) =>
        DeleteAsync($"api/sites/{siteId}/credentials/{credentialId}");

    // --- Admin ---
    public Task<List<UserSummaryDto>?> GetUsersAsync() =>
        GetAsync<List<UserSummaryDto>>("api/admin/users");

    public Task<UserSummaryDto?> CreateUserAsync(CreateUserRequest request) =>
        PostAsync<UserSummaryDto>("api/admin/users", request);

    public Task<bool> UpdateUserAsync(Guid id, UpdateUserRequest request) =>
        PutAsync($"api/admin/users/{id}", request);

    public Task<List<UserAccessDto>?> GetUserAccessAsync(Guid userId) =>
        GetAsync<List<UserAccessDto>>($"api/admin/users/{userId}/access");

    public Task<bool> DeleteUserAsync(Guid id) =>
        DeleteAsync($"api/admin/users/{id}");

    public Task<List<SiteGroupDto>?> GetSiteGroupsAsync() =>
        GetAsync<List<SiteGroupDto>>("api/admin/sitegroups");

    public Task<SiteGroupDto?> CreateSiteGroupAsync(CreateSiteGroupRequest request) =>
        PostAsync<SiteGroupDto>("api/admin/sitegroups", request);

    public Task<bool> UpdateSiteGroupAsync(Guid id, UpdateSiteGroupRequest request) =>
        PutAsync($"api/admin/sitegroups/{id}", request);

    public Task<bool> DeleteSiteGroupAsync(Guid id) =>
        DeleteAsync($"api/admin/sitegroups/{id}");

    public async Task<(bool Success, string? Error)> DeleteSiteGroupWithReasonAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/admin/sitegroups/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public Task<List<AccessGrantDto>?> GetAccessAsync(Guid siteGroupId) =>
        GetAsync<List<AccessGrantDto>>($"api/admin/sitegroups/{siteGroupId}/access");

    public async Task<bool> GrantAccessAsync(Guid siteGroupId, GrantAccessRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/admin/sitegroups/{siteGroupId}/access", request);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> RevokeAccessAsync(Guid siteGroupId, Guid userId) =>
        DeleteAsync($"api/admin/sitegroups/{siteGroupId}/access/{userId}");

    // Role-only change - no re-wrapping needed, the group key stays the same.
    public Task<bool> UpdateAccessRoleAsync(Guid siteGroupId, Guid userId, UpdateAccessRoleRequest request) =>
        PutAsync($"api/admin/sitegroups/{siteGroupId}/access/{userId}", request);

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

    private async Task<bool> PutAsync(string url, object body)
    {
        var response = await _http.PutAsJsonAsync(url, body);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> DeleteAsync(string url)
    {
        var response = await _http.DeleteAsync(url);
        return response.IsSuccessStatusCode;
    }
}
