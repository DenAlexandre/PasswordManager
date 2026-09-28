using PasswordManager.Api.Models;

namespace PasswordManager.Api.Dtos;

public record UserSummaryDto(Guid Id, string Email, bool IsAdmin, bool IsActive, bool VaultSetUp, DateTimeOffset CreatedAt, string? PublicKey);

public record UpdateUserRequest(bool? IsAdmin, bool? IsActive);

public record SiteGroupDto(Guid Id, string Name, string? Description, DateTimeOffset UpdatedAt);

// The creator's client generates the AES group key locally and wraps it for itself immediately.
public record CreateSiteGroupRequest(string Name, string? Description, string EncryptedGroupKeyForCreator);

public record UpdateSiteGroupRequest(string Name, string? Description);

public record SiteDto(Guid Id, Guid SiteGroupId, Guid? ParentSiteId, string Name, string? Url, string? Notes, DateTimeOffset UpdatedAt);

public record UpsertSiteRequest(string Name, string? Url, string? Notes, Guid? ParentSiteId);

// Full-system backup export: a group's entire Sites/Credentials tree regardless of the exporting
// admin's own membership (see AdminSiteGroupsController.GetExportData).
public record AdminSiteGroupExportDataDto(List<SiteDto> Sites, List<CredentialDto> Credentials);

public record AccessGrantDto(Guid UserId, string UserEmail, Guid SiteGroupId, AccessRole Role, DateTimeOffset GrantedAt, string EncryptedGroupKey);

// EncryptedGroupKey must be produced client-side (by the admin or an existing member who already
// holds the group's symmetric key) by wrapping it with the target user's RSA public key.
public record GrantAccessRequest(Guid UserId, AccessRole Role, string EncryptedGroupKey);

public record UpdateAccessRoleRequest(AccessRole Role);

public record UserAccessDto(Guid SiteGroupId, string SiteGroupName, AccessRole Role);
