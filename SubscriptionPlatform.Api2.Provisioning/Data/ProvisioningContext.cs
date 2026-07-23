using Microsoft.EntityFrameworkCore;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api2.Provisioning.Data;

public class ProvisioningContext : DbContext
{
    public ProvisioningContext(DbContextOptions<ProvisioningContext> options)
        : base(options) { }

    public DbSet<UserAccess> UserAccesses { get; set; }
    public DbSet<CoursePermission> CoursePermissions { get; set; }
    public DbSet<DeadLetterMessage> DeadLetterMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.PlanType)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.ActivatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_user_access_userid")
                .IsUnique();

            entity.HasIndex(e => e.Email)
                .HasDatabaseName("idx_user_access_email");
        });

        modelBuilder.Entity<CoursePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.CourseId)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.UnlockedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.UserId, e.CourseId })
                .HasDatabaseName("idx_course_permission_user_course")
                .IsUnique();

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_course_permission_userid");
        });

        modelBuilder.Entity<DeadLetterMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.MessageBody).IsRequired();
            entity.Property(e => e.ErrorReason)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ReceivedAt)
                .HasDatabaseName("idx_dlm_received_at");
        });
    }
}
