using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Models;

namespace PasswordManager.Api.Controllers;

// Every query here is implicitly scoped to CurrentUserId - unlike SiteGroups/Credentials, there is
// no sharing to model, so no AccessControlService/role checks are needed: a personal vault has
// exactly one owner.
[ApiController]
[Route("api/personal-vault")]
[Authorize]
public class PersonalVaultController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public PersonalVaultController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("passwords")]
    public async Task<ActionResult<List<PersonalPasswordDto>>> ListPasswords()
    {
        var passwords = await _db.PersonalPasswords
            .Where(p => p.UserId == CurrentUserId && !p.IsDeleted)
            .Select(p => new PersonalPasswordDto(p.Id, p.EncryptedLabel, p.EncryptedUsername, p.EncryptedPassword, p.EncryptedUrl, p.EncryptedNotes, p.UpdatedAt))
            .ToListAsync();
        return Ok(passwords);
    }

    [HttpPost("passwords")]
    public async Task<ActionResult<PersonalPasswordDto>> CreatePassword(UpsertPersonalPasswordRequest request)
    {
        var password = new PersonalPassword
        {
            UserId = CurrentUserId,
            EncryptedLabel = request.EncryptedLabel,
            EncryptedUsername = request.EncryptedUsername,
            EncryptedPassword = request.EncryptedPassword,
            EncryptedUrl = request.EncryptedUrl,
            EncryptedNotes = request.EncryptedNotes
        };
        _db.PersonalPasswords.Add(password);
        await _db.SaveChangesAsync();

        return Ok(new PersonalPasswordDto(password.Id, password.EncryptedLabel, password.EncryptedUsername,
            password.EncryptedPassword, password.EncryptedUrl, password.EncryptedNotes, password.UpdatedAt));
    }

    [HttpPut("passwords/{id:guid}")]
    public async Task<IActionResult> UpdatePassword(Guid id, UpsertPersonalPasswordRequest request)
    {
        var password = await _db.PersonalPasswords.SingleOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (password is null) return NotFound();

        password.EncryptedLabel = request.EncryptedLabel;
        password.EncryptedUsername = request.EncryptedUsername;
        password.EncryptedPassword = request.EncryptedPassword;
        password.EncryptedUrl = request.EncryptedUrl;
        password.EncryptedNotes = request.EncryptedNotes;
        password.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("passwords/{id:guid}")]
    public async Task<IActionResult> DeletePassword(Guid id)
    {
        var password = await _db.PersonalPasswords.SingleOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (password is null) return NotFound();

        password.IsDeleted = true;
        password.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("documents")]
    public async Task<ActionResult<List<PersonalDocumentDto>>> ListDocuments()
    {
        var documents = await _db.PersonalDocuments
            .Where(d => d.UserId == CurrentUserId && !d.IsDeleted)
            .Select(d => new PersonalDocumentDto(d.Id, d.EncryptedFileName, d.ContentType, d.FileSizeBytes, d.UpdatedAt))
            .ToListAsync();
        return Ok(documents);
    }

    [HttpGet("documents/{id:guid}/content")]
    public async Task<ActionResult<PersonalDocumentContentResponse>> GetDocumentContent(Guid id)
    {
        var document = await _db.PersonalDocuments.SingleOrDefaultAsync(d => d.Id == id && d.UserId == CurrentUserId && !d.IsDeleted);
        if (document is null) return NotFound();

        return Ok(new PersonalDocumentContentResponse(document.Id, document.EncryptedContent));
    }

    [HttpPost("documents")]
    public async Task<ActionResult<PersonalDocumentDto>> UploadDocument(UploadPersonalDocumentRequest request)
    {
        var document = new PersonalDocument
        {
            UserId = CurrentUserId,
            EncryptedFileName = request.EncryptedFileName,
            EncryptedContent = request.EncryptedContent,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes
        };
        _db.PersonalDocuments.Add(document);
        await _db.SaveChangesAsync();

        return Ok(new PersonalDocumentDto(document.Id, document.EncryptedFileName, document.ContentType, document.FileSizeBytes, document.UpdatedAt));
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var document = await _db.PersonalDocuments.SingleOrDefaultAsync(d => d.Id == id && d.UserId == CurrentUserId);
        if (document is null) return NotFound();

        document.IsDeleted = true;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
