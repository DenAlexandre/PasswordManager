namespace PasswordManager.Maui.Models;

public enum AccessRole { None = 0, Read = 1, Write = 2 }

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, Guid UserId, bool IsAdmin, bool VaultSetupRequired);

public record VaultKeyMaterialResponse(
    string MasterPasswordSalt, int KdfIterations, int KdfMemoryKb, int KdfParallelism,
    string PublicKey, string EncryptedPrivateKey);

public record VaultSetupRequest(
    string MasterPasswordSalt, int KdfIterations, int KdfMemoryKb, int KdfParallelism,
    string PublicKey, string EncryptedPrivateKey);

public record MySiteGroupDto(Guid Id, string Name, string? Description, AccessRole Role, string EncryptedGroupKey);

public record SiteDto(Guid Id, Guid SiteGroupId, Guid? ParentSiteId, string Name, string? Url, string? Notes, DateTimeOffset UpdatedAt);
public record UpsertSiteRequest(string Name, string? Url, string? Notes, Guid? ParentSiteId);

public record CredentialDto(
    Guid Id, Guid SiteId, string EncryptedLabel, string EncryptedUsername,
    string EncryptedPassword, string? EncryptedUrl, string? EncryptedNotes, DateTimeOffset UpdatedAt);

public record UpsertCredentialRequest(
    string EncryptedLabel, string EncryptedUsername, string EncryptedPassword, string? EncryptedUrl, string? EncryptedNotes);

public record SyncSiteGroupDto(Guid Id, string Name, string? Description, AccessRole Role, string EncryptedGroupKey);
public record SyncSiteDto(Guid Id, Guid SiteGroupId, Guid? ParentSiteId, string Name, string? Url, string? Notes, DateTimeOffset UpdatedAt, bool IsDeleted);
public record SyncCredentialDto(
    Guid Id, Guid SiteId, string EncryptedLabel, string EncryptedUsername,
    string EncryptedPassword, string? EncryptedUrl, string? EncryptedNotes, DateTimeOffset UpdatedAt, bool IsDeleted);
public record SyncResponse(DateTimeOffset ServerTime, List<SyncSiteGroupDto> SiteGroups, List<SyncSiteDto> Sites, List<SyncCredentialDto> Credentials);

// Admin
public record UserSummaryDto(Guid Id, string Email, bool IsAdmin, bool IsActive, bool VaultSetUp, DateTimeOffset CreatedAt, string? PublicKey);
public record CreateUserRequest(string Email, string TemporaryPassword, bool IsAdmin);
public record UpdateUserRequest(bool? IsAdmin, bool? IsActive);
public record SiteGroupDto(Guid Id, string Name, string? Description, DateTimeOffset UpdatedAt);
public record CreateSiteGroupRequest(string Name, string? Description, string EncryptedGroupKeyForCreator);
public record UpdateSiteGroupRequest(string Name, string? Description);
public record AccessGrantDto(Guid UserId, string UserEmail, Guid SiteGroupId, AccessRole Role, DateTimeOffset GrantedAt);
public record GrantAccessRequest(Guid UserId, AccessRole Role, string EncryptedGroupKey);
public record UpdateAccessRoleRequest(AccessRole Role);
public record UserAccessDto(Guid SiteGroupId, string SiteGroupName, AccessRole Role);
