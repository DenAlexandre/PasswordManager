using System.Security.Cryptography;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Models;

namespace PasswordManager.Maui.Services;

// Pick/encrypt/upload and download/decrypt/open documents for the personal vault. Reuses
// AesGcmCipher.EncryptBytes/DecryptBytes as-is (no streaming variant exists, so this is only
// practical for reasonably small files - see MaxFileSizeBytes) and the same FilePicker pattern
// already used for backup import (see BackupImportViewModel).
public class DocumentService
{
    private const long MaxFileSizeBytes = 20_000_000; // ~20 MB - see plan's scope-cut note

    private readonly ApiClient _api;
    private readonly VaultSession _session;

    public DocumentService(ApiClient api, VaultSession session)
    {
        _api = api;
        _session = session;
    }

    public async Task<(bool Success, string? Error)> PickAndUploadAsync()
    {
        if (_session.PersonalVaultKey is not { } key)
            return (false, "Coffre personnel non initialisé.");

        var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".doc", ".docx", ".pdf", ".jpg", ".jpeg", ".png" } },
            { DevicePlatform.Android, new[] { "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/pdf", "image/jpeg", "image/png" } },
            { DevicePlatform.iOS, new[] { "com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document", "com.adobe.pdf", "public.jpeg", "public.png" } },
            { DevicePlatform.MacCatalyst, new[] { "com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document", "com.adobe.pdf", "public.jpeg", "public.png" } },
        });

        var pickResult = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choisir un document",
            FileTypes = fileTypes
        });
        if (pickResult is null) return (false, null); // user cancelled, not an error

        byte[] bytes;
        using (var stream = await pickResult.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        if (bytes.LongLength > MaxFileSizeBytes)
            return (false, $"Fichier trop volumineux (max {MaxFileSizeBytes / 1_000_000} Mo).");

        var encryptedFileName = AesGcmCipher.Encrypt(key, pickResult.FileName);
        var encryptedContent = AesGcmCipher.EncryptBytes(key, bytes);
        var contentType = string.IsNullOrEmpty(pickResult.ContentType) ? "application/octet-stream" : pickResult.ContentType;

        var uploaded = await _api.UploadPersonalDocumentAsync(new UploadPersonalDocumentRequest(
            encryptedFileName, encryptedContent, contentType, bytes.LongLength));

        return uploaded is not null ? (true, null) : (false, "Échec de l'envoi (êtes-vous en ligne ?).");
    }

    // decryptedFileName is passed in already-decrypted by the caller (which already decrypts it
    // for display in the documents list) rather than re-decrypting it here.
    public async Task<(bool Success, string? Error)> DownloadAndOpenAsync(Guid documentId, string decryptedFileName)
    {
        if (_session.PersonalVaultKey is not { } key)
            return (false, "Coffre personnel non initialisé.");

        var content = await _api.GetPersonalDocumentContentAsync(documentId);
        if (content is null) return (false, "Impossible de récupérer le document.");

        byte[] bytes;
        try
        {
            bytes = AesGcmCipher.DecryptBytes(key, content.EncryptedContent);
        }
        catch (CryptographicException)
        {
            return (false, "Échec du déchiffrement.");
        }

        var safeFileName = Path.GetFileName(decryptedFileName);
        var tempPath = Path.Combine(FileSystem.CacheDirectory, safeFileName);
        await File.WriteAllBytesAsync(tempPath, bytes);

        await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(tempPath) });
        return (true, null);
    }
}
