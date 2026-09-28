using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;
using PasswordManager.Api.Services;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int VerificationCodeMinutes = 15;
    private const int MaxVerificationAttempts = 5;

    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenService _jwt;
    private readonly IEmailSender _email;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, PasswordHasher hasher, JwtTokenService jwt, IEmailSender email, ILogger<AuthController> logger)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _logger = logger;
        _email = email;
    }

    // One-time bootstrap: only works while the Users table is empty. Kept as the only way to
    // create the very first admin - self-registration (below) never grants admin rights.
    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> SetupAdmin(SetupAdminRequest request)
    {
        if (await _db.Users.AnyAsync())
            return Conflict("Setup already completed.");

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(request.Password),
            IsAdmin = true,
            IsActive = true,
            IsEmailVerified = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(_jwt.CreateAccessToken(user), user.Id, user.IsAdmin, VaultSetupRequired: true));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");
        if (!user.IsEmailVerified)
            return Unauthorized("Email not verified.");

        var vaultSetupRequired = string.IsNullOrEmpty(user.PublicKey);
        return Ok(new LoginResponse(_jwt.CreateAccessToken(user), user.Id, user.IsAdmin, vaultSetupRequired));
    }

    // Self-registration: the only way (besides the one-time /setup bootstrap) to create an
    // account. Always non-admin - further admins are promoted manually via AdminUsersController.
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
            return BadRequest("Adresse email invalide.");
        if (request.Password.Length < 8)
            return BadRequest("Le mot de passe doit contenir au moins 8 caractères.");
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict("A user with this email already exists.");

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            IsAdmin = false,
            IsActive = true,
            IsEmailVerified = false
        };
        SetNewVerificationCode(user);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SendVerificationEmailAsync(user);

        return NoContent();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    public async Task<ActionResult<LoginResponse>> VerifyEmail(VerifyEmailRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null || user.IsEmailVerified)
            return BadRequest("Impossible de vérifier cette adresse.");

        if (user.EmailVerificationAttempts >= MaxVerificationAttempts)
            return BadRequest("Trop de tentatives - demandez un nouveau code.");

        if (user.EmailVerificationCode != request.Code || user.EmailVerificationCodeExpiresAt is null
            || user.EmailVerificationCodeExpiresAt < DateTimeOffset.UtcNow)
        {
            user.EmailVerificationAttempts++;
            await _db.SaveChangesAsync();
            return BadRequest("Code invalide ou expiré.");
        }

        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiresAt = null;
        user.EmailVerificationAttempts = 0;
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(_jwt.CreateAccessToken(user), user.Id, user.IsAdmin, VaultSetupRequired: true));
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-public")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null || user.IsEmailVerified)
            return NoContent(); // don't reveal whether the account exists / is already verified

        SetNewVerificationCode(user);
        await _db.SaveChangesAsync();

        await SendVerificationEmailAsync(user);

        return NoContent();
    }

    [HttpGet("key-material")]
    [Authorize]
    public async Task<ActionResult<VaultKeyMaterialResponse>> GetKeyMaterial()
    {
        var user = await CurrentUserAsync();
        if (user is null || string.IsNullOrEmpty(user.PublicKey))
            return NotFound("Vault not set up yet.");

        return Ok(new VaultKeyMaterialResponse(
            user.MasterPasswordSalt,
            user.KdfIterations,
            user.KdfMemoryKb,
            user.KdfParallelism,
            user.PublicKey,
            user.EncryptedPrivateKey,
            user.EncryptedPersonalVaultKey));
    }

    // First-login flow: client generates an RSA keypair locally, derives a key from the chosen
    // master password, encrypts the private key, and uploads only the public material + ciphertext.
    // Also uploads the (self-wrapped) personal-vault key generated in the same step.
    [HttpPost("vault-setup")]
    [Authorize]
    public async Task<IActionResult> SetupVault(VaultSetupRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (!string.IsNullOrEmpty(user.PublicKey))
            return Conflict("Vault already set up.");

        user.MasterPasswordSalt = request.MasterPasswordSalt;
        user.KdfIterations = request.KdfIterations;
        user.KdfMemoryKb = request.KdfMemoryKb;
        user.KdfParallelism = request.KdfParallelism;
        user.PublicKey = request.PublicKey;
        user.EncryptedPrivateKey = request.EncryptedPrivateKey;
        user.EncryptedPersonalVaultKey = request.EncryptedPersonalVaultKey;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Lazily provisions a personal-vault key for accounts that completed vault setup before this
    // feature existed (admin-bootstrapped or previously admin-created users).
    [HttpPost("personal-vault-key")]
    [Authorize]
    public async Task<IActionResult> SetPersonalVaultKey(SetPersonalVaultKeyRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (!string.IsNullOrEmpty(user.EncryptedPersonalVaultKey))
            return Conflict("Personal vault key already set.");

        user.EncryptedPersonalVaultKey = request.EncryptedPersonalVaultKey;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void SetNewVerificationCode(User user)
    {
        user.EmailVerificationCode = Random.Shared.Next(100_000, 1_000_000).ToString();
        user.EmailVerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(VerificationCodeMinutes);
        user.EmailVerificationAttempts = 0;
    }

    // The account row is already committed by the time this runs - a transient SMTP failure
    // (misconfiguration, relay down) must not turn into a 500 that hides a successfully created
    // account. The user can always retry via resend-verification once mail delivery is fixed.
    private async Task SendVerificationEmailAsync(User user)
    {
        try
        {
            await _email.SendAsync(user.Email, "Vérifiez votre adresse email",
                $"Votre code de vérification est : {user.EmailVerificationCode}\n\nCe code expire dans {VerificationCodeMinutes} minutes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
        }
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new MailAddress(email); return true; }
        catch (FormatException) { return false; }
    }

    private Task<User?> CurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var id)) return Task.FromResult<User?>(null);
        return _db.Users.SingleOrDefaultAsync(u => u.Id == id)!;
    }
}
