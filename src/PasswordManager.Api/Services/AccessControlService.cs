using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Models;

namespace PasswordManager.Api.Services;

public class AccessControlService
{
    private readonly AppDbContext _db;

    public AccessControlService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AccessRole> GetRoleAsync(Guid userId, Guid siteGroupId)
    {
        var access = await _db.UserSiteGroupAccesses
            .SingleOrDefaultAsync(a => a.UserId == userId && a.SiteGroupId == siteGroupId);
        return access?.Role ?? AccessRole.None;
    }

    public async Task<AccessRole> GetRoleForSiteAsync(Guid userId, Guid siteId)
    {
        var siteGroupId = await _db.Sites
            .Where(s => s.Id == siteId)
            .Select(s => s.SiteGroupId)
            .SingleOrDefaultAsync();
        if (siteGroupId == Guid.Empty) return AccessRole.None;
        return await GetRoleAsync(userId, siteGroupId);
    }
}
