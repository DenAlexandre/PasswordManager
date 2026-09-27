using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Models;

namespace PasswordManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<SiteGroup> SiteGroups => Set<SiteGroup>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<UserSiteGroupAccess> UserSiteGroupAccesses => Set<UserSiteGroupAccess>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Site>(e =>
        {
            e.HasOne(s => s.SiteGroup)
                .WithMany(g => g.Sites)
                .HasForeignKey(s => s.SiteGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.ParentSite)
                .WithMany()
                .HasForeignKey(s => s.ParentSiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Credential>(e =>
        {
            e.HasOne(c => c.Site)
                .WithMany(s => s.Credentials)
                .HasForeignKey(c => c.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSiteGroupAccess>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.SiteGroupId }).IsUnique();
            e.HasOne(a => a.User)
                .WithMany(u => u.SiteGroupAccesses)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.SiteGroup)
                .WithMany(g => g.UserAccesses)
                .HasForeignKey(a => a.SiteGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
