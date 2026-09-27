namespace PasswordManager.Api.Dtos;

public record SetupAdminRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    Guid UserId,
    bool IsAdmin,
    bool VaultSetupRequired);

public record VaultKeyMaterialResponse(
    string MasterPasswordSalt,
    int KdfIterations,
    int KdfMemoryKb,
    int KdfParallelism,
    string PublicKey,
    string EncryptedPrivateKey);

public record VaultSetupRequest(
    string MasterPasswordSalt,
    int KdfIterations,
    int KdfMemoryKb,
    int KdfParallelism,
    string PublicKey,
    string EncryptedPrivateKey);
