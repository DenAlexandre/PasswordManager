namespace PasswordManager.Api.Models;

public class Site
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteGroupId { get; set; }
    public SiteGroup? SiteGroup { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }

    public List<Credential> Credentials { get; set; } = new();
}
