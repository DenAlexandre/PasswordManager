using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenService _jwt;

    public AuthController(AppDbContext db, PasswordHasher hasher, JwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    // One-time bootstrap: only works while the Users table is empty.
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
            IsActive = true
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

        var vaultSetupRequired = string.IsNullOrEmpty(user.PublicKey);
        return Ok(new LoginResponse(_jwt.CreateAccessToken(user), user.Id, user.IsAdmin, vaultSetupRequired));
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
            user.EncryptedPrivateKey));
    }

    // First-login flow: client generates an RSA keypair locally, derives a key from the chosen
    // master password, encrypts the private key, and uploads only the public material + ciphertext.
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
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private Task<User?> CurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var id)) return Task.FromResult<User?>(null);
        return _db.Users.SingleOrDefaultAsync(u => u.Id == id)!;
    }
}
