namespace PasswordManager.Api.Models;

public class SiteGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Site> Sites { get; set; } = new();
    public List<UserSiteGroupAccess> UserAccesses { get; set; } = new();
}
