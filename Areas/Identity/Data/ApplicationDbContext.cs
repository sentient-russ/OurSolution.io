using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using os.Areas.Identity.Data;
using os.Models;

namespace os.Areas.Identity.Data;
public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : base(options)
    {

    }
    public virtual DbSet<os.Models.SpeakerModel> Speakers { get; set; } = default!;
    public virtual DbSet<os.Models.AnnouncementModel> Announcements { get; set; } = default!;
    public virtual DbSet<os.Models.MeetingModel> Meetings { get; set; } = default!;
    public virtual DbSet<os.Models.PageVisitModel> PageVisits { get; set; } = default!;
    //the next section overrides the default db naming
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("Identity");
        builder.Entity<AppUser>(entity => { entity.ToTable(name: "Users"); });
        builder.Entity<IdentityRole>(entity => { entity.ToTable(name: "Roles"); });
        builder.Entity<IdentityUserRole<string>>(entity => { entity.ToTable(name: "UserRoles"); });
        builder.Entity<IdentityUserClaim<string>>(entity => { entity.ToTable(name: "UserClaims"); });
        builder.Entity<IdentityUserLogin<string>>(entity => { entity.ToTable(name: "UserLogins"); });
        builder.Entity<IdentityRoleClaim<string>>(entity => { entity.ToTable(name: "RoleClaims"); });
        builder.Entity<IdentityUserToken<string>>(entity => { entity.ToTable(name: "UserTokens"); });
    }
    public DbSet<os.Models.AppUserModel> AppUserModel { get; set; } = default!;
    public DbSet<os.Models.LogModel> SecurityLog { get; set; } = default!;
}

internal class ApplicationUserEntityConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(256);
        builder.Property(u => u.LastName).HasMaxLength(256);
        builder.Property(u => u.Address).HasMaxLength(256);
        builder.Property(u => u.City).HasMaxLength(256);
        builder.Property(u => u.State).HasMaxLength(256);
        builder.Property(u => u.Zip).HasMaxLength(256);
        builder.Property(u => u.BellyButtonBirthday).HasMaxLength(256);
        builder.Property(u => u.AABirthday).HasMaxLength(256);
        builder.Property(u => u.PhoneNumber).HasMaxLength(256);
        builder.Property(u => u.UserRole).HasMaxLength(256);
        builder.Property(u => u.ActiveStatus);
    }
}

