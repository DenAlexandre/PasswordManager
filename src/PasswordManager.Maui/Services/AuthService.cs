using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;

namespace PasswordManager.Maui.Services;

public enum LoginOutcome { InvalidCredentials, VaultSetupRequired, NeedsUnlock }

public enum SessionRestoreResult { NoSession, VaultSetupRequired, VaultReady }

public class AuthService
{
    private const string TokenKey = "access_token";
    private const string UserIdKey = "user_id";
    private const string IsAdminKey = "is_admin";

    private readonly ApiClient _api;
    private readonly VaultSession _session;
    private readonly LocalCacheDb _cache;

    public AuthService(ApiClient api, VaultSession session, LocalCacheDb cache)
    {
        _api = api;
        _session = session;
        _cache = cache;
    }

    public async Task<LoginOutcome> LoginAsync(string email, string password)
    {
        var response = await _api.LoginAsync(email, password);
        if (response is null) return LoginOutcome.InvalidCredentials;

        await ApplyLoginResponseAsync(response);
        return response.VaultSetupRequired ? LoginOutcome.VaultSetupRequired : LoginOutcome.NeedsUnlock;
    }

    // Self-registration: creates the account and emails a verification code. No session yet -
    // the account can't log in until VerifyEmailAsync succeeds.
    public Task<(bool Success, string? Error)> RegisterAsync(string email, string password) =>
        _api.RegisterAsync(email, password);

    public Task<bool> ResendVerificationAsync(string email) => _api.ResendVerificationAsync(email);

    // Verification succeeds with the same LoginResponse shape as Login/SetupAdmin, so this mirrors
    // LoginAsync's session bookkeeping exactly - the caller then proceeds straight to vault setup.
    public async Task<(bool Success, string? Error)> VerifyEmailAsync(string email, string code)
    {
        var (response, error) = await _api.VerifyEmailAsync(email, code);
        if (response is null) return (false, error);

        await ApplyLoginResponseAsync(response);
        return (true, null);
    }

    private async Task ApplyLoginResponseAsync(LoginResponse response)
    {
        await SecureStorage.SetAsync(TokenKey, response.AccessToken);
        await SecureStorage.SetAsync(UserIdKey, response.UserId.ToString());
        await SecureStorage.SetAsync(IsAdminKey, response.IsAdmin.ToString());
        _api.SetAccessToken(response.AccessToken);
        _session.SetIdentity(response.UserId, response.IsAdmin);
    }

    // Restores a previously authenticated session (app relaunch) without re-entering the login
    // password, and checks whether the vault still needs first-time setup. An expired/invalid
    // token (401) forces a real re-login rather than being misread as "vault not set up yet" -
    // those are two very different situations that must not be confused.
    public async Task<SessionRestoreResult> TryRestoreSessionAsync()
    {
        var token = await SecureStorage.GetAsync(TokenKey);
        var userIdStr = await SecureStorage.GetAsync(UserIdKey);
        var isAdminStr = await SecureStorage.GetAsync(IsAdminKey);
        if (token is null || userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return SessionRestoreResult.NoSession;

        _api.SetAccessToken(token);
        _session.SetIdentity(userId, bool.TryParse(isAdminStr, out var isAdmin) && isAdmin);

        var (unauthorized, keyMaterial) = await _api.GetKeyMaterialWithAuthCheckAsync();
        if (unauthorized)
        {
            await LogoutAsync();
            return SessionRestoreResult.NoSession;
        }
        if (keyMaterial is not null) return SessionRestoreResult.VaultReady;

        // Server unreachable (offline) - fall back to whatever the local cache already knows.
        var cached = await _cache.GetIdentityAsync();
        return cached is not null ? SessionRestoreResult.VaultReady : SessionRestoreResult.VaultSetupRequired;
    }

    // First-time vault creation: generates the RSA keypair locally and uploads only ciphertext + public key.
    // Also generates the personal-vault AES key in the same step, wrapped for self only.
    public async Task<bool> SetupVaultAsync(string masterPassword)
    {
        var (publicKeyPem, privateKeyDer) = RsaKeyWrapping.GenerateKeyPair();
        var salt = KeyDerivation.NewSalt();
        const int iterations = 3, memoryKb = 65536, parallelism = 4;

        var masterKey = KeyDerivation.DeriveMasterKey(masterPassword, salt, iterations, memoryKb, parallelism);
        var encryptedPrivateKey = AesGcmCipher.EncryptBytes(masterKey, privateKeyDer);
        var encryptedPersonalVaultKey = RsaKeyWrapping.WrapKey(publicKeyPem, AesGcmCipher.NewKey());

        var ok = await _api.SetupVaultAsync(new VaultSetupRequest(
            Convert.ToBase64String(salt), iterations, memoryKb, parallelism, publicKeyPem, encryptedPrivateKey, encryptedPersonalVaultKey));
        if (!ok) return false;

        await _cache.SaveIdentityAsync(new CachedIdentity
        {
            UserId = _session.UserId!.Value,
            IsAdmin = _session.IsAdmin,
            MasterPasswordSalt = Convert.ToBase64String(salt),
            KdfIterations = iterations,
            KdfMemoryKb = memoryKb,
            KdfParallelism = parallelism,
            PublicKey = publicKeyPem,
            EncryptedPrivateKey = encryptedPrivateKey,
            LastSyncedAt = DateTimeOffset.UtcNow
        });

        _session.Unlock(privateKeyDer);
        _session.UnlockPersonalVaultKey(encryptedPersonalVaultKey);
        return true;
    }

    // Unlocks the vault. Tries the server first (to pick up the latest key material and refresh
    // the local cache), transparently falling back to the cached copy when offline.
    public async Task<bool> UnlockAsync(string masterPassword)
    {
        var keyMaterial = await _api.GetKeyMaterialAsync();

        string salt; int iterations, memoryKb, parallelism; string encryptedPrivateKey;
        if (keyMaterial is not null)
        {
            salt = keyMaterial.MasterPasswordSalt;
            iterations = keyMaterial.KdfIterations;
            memoryKb = keyMaterial.KdfMemoryKb;
            parallelism = keyMaterial.KdfParallelism;
            encryptedPrivateKey = keyMaterial.EncryptedPrivateKey;

            await _cache.SaveIdentityAsync(new CachedIdentity
            {
                UserId = _session.UserId!.Value,
                IsAdmin = _session.IsAdmin,
                MasterPasswordSalt = salt,
                KdfIterations = iterations,
                KdfMemoryKb = memoryKb,
                KdfParallelism = parallelism,
                PublicKey = keyMaterial.PublicKey,
                EncryptedPrivateKey = encryptedPrivateKey,
                LastSyncedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            var cached = await _cache.GetIdentityAsync();
            if (cached is null) return false;
            salt = cached.MasterPasswordSalt;
            iterations = cached.KdfIterations;
            memoryKb = cached.KdfMemoryKb;
            parallelism = cached.KdfParallelism;
            encryptedPrivateKey = cached.EncryptedPrivateKey;
        }

        try
        {
            var masterKey = KeyDerivation.DeriveMasterKey(masterPassword, Convert.FromBase64String(salt), iterations, memoryKb, parallelism);
            var privateKeyDer = AesGcmCipher.DecryptBytes(masterKey, encryptedPrivateKey);
            _session.Unlock(privateKeyDer);

            // Only available when online (not cached locally - the personal vault is online-only)
            // and only once the user has opened it for the first time (lazy-provisioned).
            if (!string.IsNullOrEmpty(keyMaterial?.EncryptedPersonalVaultKey))
                _session.UnlockPersonalVaultKey(keyMaterial.EncryptedPersonalVaultKey);

            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false; // wrong master password
        }
    }

    public async Task LogoutAsync()
    {
        SecureStorage.Remove(TokenKey);
        SecureStorage.Remove(UserIdKey);
        SecureStorage.Remove(IsAdminKey);
        _api.SetAccessToken(null);
        _session.Lock();
    }
}
