namespace PasswordManager.Api.Dtos;

public record CredentialDto(
    Guid Id,
    Guid SiteId,
    string EncryptedLabel,
    string EncryptedUsername,
    string EncryptedPassword,
    string? EncryptedNotes,
    DateTimeOffset UpdatedAt);

public record UpsertCredentialRequest(
    string EncryptedLabel,
    string EncryptedUsername,
    string EncryptedPassword,
    string? EncryptedNotes);
