using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;

namespace PasswordManager.Api.Controllers;

// Pulls everything the client needs to refresh its encrypted local cache. Writes made while
// offline are simply replayed by the client against the regular Sites/Credentials endpoints
// once connectivity returns - this endpoint only ever reads.
[ApiController]
[Route("api/sync")]
[Authorize]
public class SyncController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public SyncController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<SyncResponse>> Get([FromQuery] DateTimeOffset? since)
    {
        var serverTime = DateTimeOffset.UtcNow;

        var accesses = await _db.UserSiteGroupAccesses
            .Where(a => a.UserId == CurrentUserId && a.Role != AccessRole.None)
            .Include(a => a.SiteGroup)
            .ToListAsync();

        var groupIds = accesses.Select(a => a.SiteGroupId).ToList();

        var siteGroups = accesses
            .Select(a => new SyncSiteGroupDto(a.SiteGroupId, a.SiteGroup!.Name, a.SiteGroup.Description, a.Role, a.EncryptedGroupKey))
            .ToList();

        var sitesQuery = _db.Sites.Where(s => groupIds.Contains(s.SiteGroupId));
        if (since is not null) sitesQuery = sitesQuery.Where(s => s.UpdatedAt > since);
        var sites = await sitesQuery
            .Select(s => new SyncSiteDto(s.Id, s.SiteGroupId, s.Name, s.Url, s.Notes, s.UpdatedAt, s.IsDeleted))
            .ToListAsync();

        var siteIds = await _db.Sites.Where(s => groupIds.Contains(s.SiteGroupId)).Select(s => s.Id).ToListAsync();
        var credentialsQuery = _db.Credentials.Where(c => siteIds.Contains(c.SiteId));
        if (since is not null) credentialsQuery = credentialsQuery.Where(c => c.UpdatedAt > since);
        var credentials = await credentialsQuery
            .Select(c => new SyncCredentialDto(c.Id, c.SiteId, c.EncryptedLabel, c.EncryptedUsername, c.EncryptedPassword, c.EncryptedUrl, c.EncryptedNotes, c.UpdatedAt, c.IsDeleted))
            .ToListAsync();

        return Ok(new SyncResponse(serverTime, siteGroups, sites, credentials));
    }
}
