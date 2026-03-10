using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Shared.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain Sets
    public DbSet<ClientPc> ClientPcs { get; set; }
    public DbSet<Component> Components { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure JSONB columns for ClientPc
        modelBuilder.Entity<ClientPc>(entity =>
        {
            entity.Property(e => e.HardwareConfig).HasColumnType("jsonb");
            entity.Property(e => e.SoftwareConfig).HasColumnType("jsonb");
            entity.Property(e => e.CustomDataPoints).HasColumnType("jsonb");
            entity.Property(e => e.Predecessors).HasColumnType("jsonb");

            // Indexes for faster lookups
            entity.HasIndex(e => e.Hostname).IsUnique();
            entity.HasIndex(e => e.MacAddress).IsUnique();
        });

        // Configure JSONB column for Components
        modelBuilder.Entity<Component>(entity =>
        {
            entity.Property(e => e.AdminManagedFields).HasColumnType("jsonb");
        });
    }
}
