using ERP.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Identity.Infrastructure.Persistence;

public class IdentityDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public const string Schema = "sec";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Map ASP.NET Core Identity to clean 'sec_' table names
        builder.Entity<AppUser>(b =>
        {
            b.ToTable("sec_users");
            b.Property(u => u.FullName).HasMaxLength(150);
        });

        builder.Entity<AppRole>(b =>
        {
            b.ToTable("sec_roles");
            b.Property(r => r.Description).HasMaxLength(250);
        });

        builder.Entity<IdentityUserRole<Guid>>().ToTable("sec_user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("sec_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("sec_user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("sec_role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("sec_user_tokens");

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("sec_refresh_tokens");
            b.HasKey(r => r.Id);
            b.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
            b.HasIndex(r => r.TokenHash).IsUnique();

            b.HasOne(r => r.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
