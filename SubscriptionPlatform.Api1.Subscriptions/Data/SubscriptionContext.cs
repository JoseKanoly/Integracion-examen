using Microsoft.EntityFrameworkCore;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api1.Subscriptions.Data;

public class SubscriptionContext : DbContext
{
    public SubscriptionContext(DbContextOptions<SubscriptionContext> options)
        : base(options) { }

    public DbSet<UserSubscription> UserSubscriptions { get; set; }
    public DbSet<PendingMessage> PendingMessages { get; set; }
    public DbSet<PaymentHistoryEntry> PaymentHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PendingMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.MessageBody).IsRequired();
            entity.Property(e => e.IsProcessed).HasDefaultValue(false);

            entity.HasIndex(e => new { e.IsProcessed, e.CreatedAt })
                .HasDatabaseName("idx_pending_messages_status_date");
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.PlanType)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Basic");
            entity.Property(e => e.SubscriptionDate)
                .HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);

            entity.HasIndex(e => e.Email)
                .HasDatabaseName("idx_user_subscription_email")
                .IsUnique();
        });

        modelBuilder.Entity<PaymentHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.PlanType)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.PaymentMethod)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Completado");
            entity.Property(e => e.PaidAt)
                .HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(10,2)");

            entity.HasIndex(e => new { e.UserId, e.PaidAt })
                .HasDatabaseName("idx_payment_history_user_date");
        });
    }
}
