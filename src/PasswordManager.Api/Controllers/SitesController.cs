using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;
using PasswordManager.Api.Services;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/sitegroups/{siteGroupId:guid}/sites")]
[Authorize]
public class SitesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly AccessControlService _access;

    public SitesController(AppDbContext db, AccessControlService access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<SiteDto>>> List(Guid siteGroupId)
    {
        if (await _access.GetRoleAsync(CurrentUserId, siteGroupId) == AccessRole.None)
            return Forbid();

        var sites = await _db.Sites
            .Where(s => s.SiteGroupId == siteGroupId && !s.IsDeleted)
            .Select(s => new SiteDto(s.Id, s.SiteGroupId, s.Name, s.Url, s.Notes, s.UpdatedAt))
            .ToListAsync();
        return Ok(sites);
    }

    [HttpPost]
    public async Task<ActionResult<SiteDto>> Create(Guid siteGroupId, UpsertSiteRequest request)
    {
        if (await _access.GetRoleAsync(CurrentUserId, siteGroupId) != AccessRole.Write)
            return Forbid();

        var site = new Site
        {
            SiteGroupId = siteGroupId,
            Name = request.Name,
            Url = request.Url,
            Notes = request.Notes
        };
        _db.Sites.Add(site);
        await _db.SaveChangesAsync();

        return Ok(new SiteDto(site.Id, site.SiteGroupId, site.Name, site.Url, site.Notes, site.UpdatedAt));
    }

    [HttpPut("{siteId:guid}")]
    public async Task<IActionResult> Update(Guid siteGroupId, Guid siteId, UpsertSiteRequest request)
    {
        if (await _access.GetRoleAsync(CurrentUserId, siteGroupId) != AccessRole.Write)
            return Forbid();

        var site = await _db.Sites.SingleOrDefaultAsync(s => s.Id == siteId && s.SiteGroupId == siteGroupId);
        if (site is null) return NotFound();

        site.Name = request.Name;
        site.Url = request.Url;
        site.Notes = request.Notes;
        site.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{siteId:guid}")]
    public async Task<IActionResult> Delete(Guid siteGroupId, Guid siteId)
    {
        if (await _access.GetRoleAsync(CurrentUserId, siteGroupId) != AccessRole.Write)
            return Forbid();

        var site = await _db.Sites.SingleOrDefaultAsync(s => s.Id == siteId && s.SiteGroupId == siteGroupId);
        if (site is null) return NotFound();

        site.IsDeleted = true;
        site.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
