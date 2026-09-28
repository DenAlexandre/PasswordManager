namespace PasswordManager.Api.Dtos;

public record PersonalPasswordDto(
    Guid Id,
    string EncryptedLabel,
    string EncryptedUsername,
    string EncryptedPassword,
    string? EncryptedUrl,
    string? EncryptedNotes,
    DateTimeOffset UpdatedAt);

public record UpsertPersonalPasswordRequest(
    string EncryptedLabel,
    string EncryptedUsername,
    string EncryptedPassword,
    string? EncryptedUrl,
    string? EncryptedNotes);

// Metadata only - never the encrypted content, so listing stays cheap. Fetch content separately
// via GET .../documents/{id}/content.
public record PersonalDocumentDto(
    Guid Id,
    string EncryptedFileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset UpdatedAt);

public record UploadPersonalDocumentRequest(
    string EncryptedFileName,
    string EncryptedContent,
    string ContentType,
    long FileSizeBytes);

public record PersonalDocumentContentResponse(Guid Id, string EncryptedContent);
