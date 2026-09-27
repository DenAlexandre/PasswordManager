using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;
using PasswordManager.Api.Services;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;

    public AdminUsersController(AppDbContext db, PasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserSummaryDto>>> List()
    {
        var users = await _db.Users
            .OrderBy(u => u.Email)
            .Select(u => new UserSummaryDto(u.Id, u.Email, u.IsAdmin, u.IsActive, u.PublicKey != "", u.CreatedAt,
                u.PublicKey == "" ? null : u.PublicKey))
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserSummaryDto>> Create(CreateUserRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict("A user with this email already exists.");

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.TemporaryPassword),
            IsAdmin = request.IsAdmin,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new UserSummaryDto(user.Id, user.Email, user.IsAdmin, user.IsActive, false, user.CreatedAt, null));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (request.IsAdmin is not null) user.IsAdmin = request.IsAdmin.Value;
        if (request.IsActive is not null) user.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (id == CurrentUserId) return BadRequest("Vous ne pouvez pas supprimer votre propre compte.");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        _db.Users.Remove(user); // cascades to UserSiteGroupAccesses (see AppDbContext)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Reverse lookup of AdminSiteGroupsController's per-group access list - lets the admin UI
    // show "which groups does this user have access to" on a user-centric management screen.
    [HttpGet("{id:guid}/access")]
    public async Task<ActionResult<List<UserAccessDto>>> GetAccess(Guid id)
    {
        var access = await _db.UserSiteGroupAccesses
            .Where(a => a.UserId == id)
            .Include(a => a.SiteGroup)
            .Select(a => new UserAccessDto(a.SiteGroupId, a.SiteGroup!.Name, a.Role))
            .ToListAsync();
        return Ok(access);
    }
}
