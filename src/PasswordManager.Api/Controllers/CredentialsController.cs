using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;
using PasswordManager.Api.Services;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/sites/{siteId:guid}/credentials")]
[Authorize]
public class CredentialsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly AccessControlService _access;

    public CredentialsController(AppDbContext db, AccessControlService access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<CredentialDto>>> List(Guid siteId)
    {
        if (await _access.GetRoleForSiteAsync(CurrentUserId, siteId) == AccessRole.None)
            return Forbid();

        var credentials = await _db.Credentials
            .Where(c => c.SiteId == siteId && !c.IsDeleted)
            .Select(c => new CredentialDto(c.Id, c.SiteId, c.EncryptedLabel, c.EncryptedUsername, c.EncryptedPassword, c.EncryptedNotes, c.UpdatedAt))
            .ToListAsync();
        return Ok(credentials);
    }

    [HttpPost]
    public async Task<ActionResult<CredentialDto>> Create(Guid siteId, UpsertCredentialRequest request)
    {
        if (await _access.GetRoleForSiteAsync(CurrentUserId, siteId) != AccessRole.Write)
            return Forbid();

        var credential = new Credential
        {
            SiteId = siteId,
            EncryptedLabel = request.EncryptedLabel,
            EncryptedUsername = request.EncryptedUsername,
            EncryptedPassword = request.EncryptedPassword,
            EncryptedNotes = request.EncryptedNotes
        };
        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync();

        return Ok(new CredentialDto(credential.Id, credential.SiteId, credential.EncryptedLabel,
            credential.EncryptedUsername, credential.EncryptedPassword, credential.EncryptedNotes, credential.UpdatedAt));
    }

    [HttpPut("{credentialId:guid}")]
    public async Task<IActionResult> Update(Guid siteId, Guid credentialId, UpsertCredentialRequest request)
    {
        if (await _access.GetRoleForSiteAsync(CurrentUserId, siteId) != AccessRole.Write)
            return Forbid();

        var credential = await _db.Credentials.SingleOrDefaultAsync(c => c.Id == credentialId && c.SiteId == siteId);
        if (credential is null) return NotFound();

        credential.EncryptedLabel = request.EncryptedLabel;
        credential.EncryptedUsername = request.EncryptedUsername;
        credential.EncryptedPassword = request.EncryptedPassword;
        credential.EncryptedNotes = request.EncryptedNotes;
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{credentialId:guid}")]
    public async Task<IActionResult> Delete(Guid siteId, Guid credentialId)
    {
        if (await _access.GetRoleForSiteAsync(CurrentUserId, siteId) != AccessRole.Write)
            return Forbid();

        var credential = await _db.Credentials.SingleOrDefaultAsync(c => c.Id == credentialId && c.SiteId == siteId);
        if (credential is null) return NotFound();

        credential.IsDeleted = true;
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
