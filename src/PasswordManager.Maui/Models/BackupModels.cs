namespace PasswordManager.Maui.Models;

public static class BackupFormat
{
    public const int CurrentVersion = 1;
}

public record BackupFileV1(
    int FormatVersion,
    DateTimeOffset ExportedAt,
    Guid ExportedByUserId,
    string ExportedByEmail,
    List<BackupSiteGroup> SiteGroups);

public record BackupSiteGroup(
    Guid Id,
    string Name,
    string? Description,
    List<BackupMember> Members,
    List<BackupSite> Sites,
    List<BackupCredential> Credentials);

public record BackupMember(Guid UserId, string UserEmail, AccessRole Role, string EncryptedGroupKey);

public record BackupSite(Guid Id, Guid? ParentSiteId, string Name, string? Url, string? Notes);

public record BackupCredential(
    Guid Id,
    Guid SiteId,
    string EncryptedLabel,
    string EncryptedUsername,
    string EncryptedPassword,
    string? EncryptedUrl,
    string? EncryptedNotes);
