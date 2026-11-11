using DineDesk.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DineDesk.Api.Data;

public class DineDeskDbContext : DbContext
{
    public DineDeskDbContext(DbContextOptions<DineDeskDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemIngredient> MenuItemIngredients => Set<MenuItemIngredient>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Tab> Tabs => Set<Tab>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<HappyHourWindow> HappyHourWindows => Set<HappyHourWindow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureConcurrencyToken(modelBuilder.Entity<Ingredient>().Property(i => i.RowVersion));
        ConfigureConcurrencyToken(modelBuilder.Entity<MenuItem>().Property(m => m.RowVersion));
        ConfigureConcurrencyToken(modelBuilder.Entity<Tab>().Property(t => t.RowVersion));
        ConfigureConcurrencyToken(modelBuilder.Entity<OrderItem>().Property(oi => oi.RowVersion));

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<MenuItemIngredient>()
            .HasKey(mi => new { mi.MenuItemId, mi.IngredientId });

        modelBuilder.Entity<MenuItemIngredient>()
            .HasOne(mi => mi.MenuItem)
            .WithMany(m => m.Recipe)
            .HasForeignKey(mi => mi.MenuItemId);

        modelBuilder.Entity<MenuItemIngredient>()
            .HasOne(mi => mi.Ingredient)
            .WithMany(i => i.UsedIn)
            .HasForeignKey(mi => mi.IngredientId);

        modelBuilder.Entity<DiningTable>()
            .HasIndex(t => t.Number)
            .IsUnique();

        modelBuilder.Entity<Tab>()
            .HasOne(t => t.DiningTable)
            .WithMany(dt => dt.Tabs)
            .HasForeignKey(t => t.DiningTableId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tab>()
            .HasOne(t => t.OpenedByUser)
            .WithMany()
            .HasForeignKey(t => t.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Tab)
            .WithMany(t => t.Orders)
            .HasForeignKey(o => o.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.MenuItem)
            .WithMany()
            .HasForeignKey(oi => oi.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Tab)
            .WithMany(t => t.Payments)
            .HasForeignKey(p => p.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        foreach (var decimalProperty in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            decimalProperty.SetColumnType("decimal(18,2)");
        }
    }

    private void ConfigureConcurrencyToken(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<byte[]> property)
    {
        if (Database.IsSqlServer())
        {
            property.IsRowVersion();
        }
        else
        {
            // No native rowversion/timestamp column on this provider: keep it a plain column
            // (ValueGenerated stays Never, so it's never excluded from INSERT/UPDATE) and stamp
            // a fresh token ourselves on every save - see StampPortableRowVersions below.
            property.IsConcurrencyToken();
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (!Database.IsSqlServer())
        {
            StampPortableRowVersions();
        }

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (!Database.IsSqlServer())
        {
            StampPortableRowVersions();
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampPortableRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var rowVersionProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(Ingredient.RowVersion));
            if (rowVersionProperty is not null)
            {
                rowVersionProperty.CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
