using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/admin/sitegroups")]
[Authorize(Policy = "AdminOnly")]
public class AdminSiteGroupsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public AdminSiteGroupsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<SiteGroupDto>>> List()
    {
        var groups = await _db.SiteGroups
            .OrderBy(g => g.Name)
            .Select(g => new SiteGroupDto(g.Id, g.Name, g.Description, g.UpdatedAt))
            .ToListAsync();
        return Ok(groups);
    }

    // Creates the group and immediately grants the creating admin Write access, storing the
    // group key wrapped for their own public key (generated client-side before calling this).
    [HttpPost]
    public async Task<ActionResult<SiteGroupDto>> Create(CreateSiteGroupRequest request)
    {
        var group = new SiteGroup { Name = request.Name, Description = request.Description };
        _db.SiteGroups.Add(group);
        _db.UserSiteGroupAccesses.Add(new UserSiteGroupAccess
        {
            UserId = CurrentUserId,
            SiteGroupId = group.Id,
            Role = AccessRole.Write,
            EncryptedGroupKey = request.EncryptedGroupKeyForCreator
        });
        await _db.SaveChangesAsync();

        return Ok(new SiteGroupDto(group.Id, group.Name, group.Description, group.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSiteGroupRequest request)
    {
        var group = await _db.SiteGroups.FindAsync(id);
        if (group is null) return NotFound();

        group.Name = request.Name;
        group.Description = request.Description;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _db.SiteGroups.FindAsync(id);
        if (group is null) return NotFound();

        // Deleting a group hard-deletes everything inside it (sites/credentials cascade) for every
        // member - require it to be emptied first rather than silently wiping data.
        if (await _db.Sites.AnyAsync(s => s.SiteGroupId == id && !s.IsDeleted))
            return Conflict("Ce groupe n'est pas vide : supprimez d'abord ses dossiers.");

        _db.SiteGroups.Remove(group);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/access")]
    public async Task<ActionResult<List<AccessGrantDto>>> ListAccess(Guid id)
    {
        var grants = await _db.UserSiteGroupAccesses
            .Where(a => a.SiteGroupId == id)
            .Include(a => a.User)
            .Select(a => new AccessGrantDto(a.UserId, a.User!.Email, a.SiteGroupId, a.Role, a.GrantedAt))
            .ToListAsync();
        return Ok(grants);
    }

    // EncryptedGroupKey must be produced by a client that already holds the group key
    // (the admin, if a member, or any existing member re-wrapping it for the new user).
    [HttpPost("{id:guid}/access")]
    public async Task<IActionResult> GrantAccess(Guid id, GrantAccessRequest request)
    {
        if (!await _db.SiteGroups.AnyAsync(g => g.Id == id)) return NotFound("Site group not found.");
        if (!await _db.Users.AnyAsync(u => u.Id == request.UserId)) return NotFound("User not found.");

        var existing = await _db.UserSiteGroupAccesses
            .SingleOrDefaultAsync(a => a.UserId == request.UserId && a.SiteGroupId == id);

        if (existing is null)
        {
            _db.UserSiteGroupAccesses.Add(new UserSiteGroupAccess
            {
                UserId = request.UserId,
                SiteGroupId = id,
                Role = request.Role,
                EncryptedGroupKey = request.EncryptedGroupKey
            });
        }
        else
        {
            existing.Role = request.Role;
            existing.EncryptedGroupKey = request.EncryptedGroupKey;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/access/{userId:guid}")]
    public async Task<IActionResult> UpdateAccessRole(Guid id, Guid userId, UpdateAccessRoleRequest request)
    {
        var access = await _db.UserSiteGroupAccesses
            .SingleOrDefaultAsync(a => a.UserId == userId && a.SiteGroupId == id);
        if (access is null) return NotFound();

        access.Role = request.Role;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/access/{userId:guid}")]
    public async Task<IActionResult> RevokeAccess(Guid id, Guid userId)
    {
        var access = await _db.UserSiteGroupAccesses
            .SingleOrDefaultAsync(a => a.UserId == userId && a.SiteGroupId == id);
        if (access is null) return NotFound();

        _db.UserSiteGroupAccesses.Remove(access);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
