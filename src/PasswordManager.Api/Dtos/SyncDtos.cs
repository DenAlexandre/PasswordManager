using PasswordManager.Api.Models;

namespace PasswordManager.Api.Dtos;

public record SyncSiteGroupDto(Guid Id, string Name, string? Description, AccessRole Role, string EncryptedGroupKey);

public record SyncSiteDto(Guid Id, Guid SiteGroupId, string Name, string? Url, string? Notes, DateTimeOffset UpdatedAt, bool IsDeleted);

public record SyncCredentialDto(
    Guid Id, Guid SiteId, string EncryptedLabel, string EncryptedUsername,
    string EncryptedPassword, string? EncryptedUrl, string? EncryptedNotes, DateTimeOffset UpdatedAt, bool IsDeleted);

public record SyncResponse(
    DateTimeOffset ServerTime,
    List<SyncSiteGroupDto> SiteGroups,
    List<SyncSiteDto> Sites,
    List<SyncCredentialDto> Credentials);
