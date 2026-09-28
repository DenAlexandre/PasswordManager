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
    string EncryptedPrivateKey,
    string? EncryptedPersonalVaultKey);

public record VaultSetupRequest(
    string MasterPasswordSalt,
    int KdfIterations,
    int KdfMemoryKb,
    int KdfParallelism,
    string PublicKey,
    string EncryptedPrivateKey,
    string EncryptedPersonalVaultKey);

// Self-registration: creates the user unverified and emails a 6-digit code (see AuthController.Register).
public record RegisterRequest(string Email, string Password);

public record VerifyEmailRequest(string Email, string Code);

public record ResendVerificationRequest(string Email);

public record SetPersonalVaultKeyRequest(string EncryptedPersonalVaultKey);
