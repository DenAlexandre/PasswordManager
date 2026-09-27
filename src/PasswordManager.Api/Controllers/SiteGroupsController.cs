using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;

namespace PasswordManager.Api.Controllers;

// Non-admin view: the site groups the *current* user has been granted access to,
// including the wrapped group key needed to decrypt their contents client-side.
[ApiController]
[Route("api/sitegroups")]
[Authorize]
public class SiteGroupsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public SiteGroupsController(AppDbContext db)
    {
        _db = db;
    }

    public record MySiteGroupDto(Guid Id, string Name, string? Description, AccessRole Role, string EncryptedGroupKey);

    [HttpGet("mine")]
    public async Task<ActionResult<List<MySiteGroupDto>>> Mine()
    {
        var groups = await _db.UserSiteGroupAccesses
            .Where(a => a.UserId == CurrentUserId && a.Role != AccessRole.None)
            .Include(a => a.SiteGroup)
            .Select(a => new MySiteGroupDto(a.SiteGroupId, a.SiteGroup!.Name, a.SiteGroup.Description, a.Role, a.EncryptedGroupKey))
            .ToListAsync();
        return Ok(groups);
    }
}
