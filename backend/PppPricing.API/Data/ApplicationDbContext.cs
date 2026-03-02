using Microsoft.EntityFrameworkCore;
using PppPricing.Domain.Models;

namespace PppPricing.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<StoreConnection> StoreConnections => Set<StoreConnection>();
    public DbSet<App> Apps => Set<App>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPrice> SubscriptionPrices => Set<SubscriptionPrice>();
    public DbSet<PppMultiplier> PppMultipliers => Set<PppMultiplier>();
    public DbSet<PriceChange> PriceChanges => Set<PriceChange>();
    public DbSet<PricingIndexRawData> PricingIndexRawData => Set<PricingIndexRawData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FirebaseUid).IsUnique();
            entity.HasIndex(e => e.Email);
        });

        // StoreConnection
        modelBuilder.Entity<StoreConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.StoreType }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.StoreConnections)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // App
        modelBuilder.Entity<App>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Apps)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StoreConnection)
                .WithMany(sc => sc.Apps)
                .HasForeignKey(e => e.StoreConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Subscription
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.App)
                .WithMany(a => a.Subscriptions)
                .HasForeignKey(e => e.AppId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SubscriptionPrice
        modelBuilder.Entity<SubscriptionPrice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SubscriptionId, e.RegionCode }).IsUnique();
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Prices)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PppMultiplier
        modelBuilder.Entity<PppMultiplier>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Unique constraint: RegionCode + UserId + IndexType + PlanType (for Netflix plan variants)
            entity.HasIndex(e => new { e.RegionCode, e.UserId, e.IndexType, e.PlanType }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.CustomMultipliers)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PricingIndexRawData
        modelBuilder.Entity<PricingIndexRawData>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Unique constraint: IndexType + RegionCode + PlanType (for Netflix variants)
            entity.HasIndex(e => new { e.IndexType, e.RegionCode, e.PlanType }).IsUnique();
        });

        // PriceChange
        modelBuilder.Entity<PriceChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.PriceChanges)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany(u => u.PriceChanges)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
