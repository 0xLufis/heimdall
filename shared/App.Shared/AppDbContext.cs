using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Shared.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain Sets
    public DbSet<ClientPc> ClientPcs { get; set; }
    public DbSet<Component> Components { get; set; }
    public DbSet<FloorPlan> FloorPlans { get; set; }
    public DbSet<Machine> Machines { get; set; }
    public DbSet<SoftwareComponent> SoftwareComponents { get; set; }
    public DbSet<HardwareComponent> HardwareComponents { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Manufacturer> Manufacturers { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Manufacturer
        modelBuilder.Entity<Manufacturer>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure Supplier
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure HardwareComponent
        modelBuilder.Entity<HardwareComponent>(entity =>
        {
            entity.Property(e => e.TechnicalSpecs).HasColumnType("jsonb");
            
            entity.HasOne(e => e.Manufacturer)
                  .WithMany()
                  .HasForeignKey(e => e.ManufacturerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure SoftwareComponent
        modelBuilder.Entity<SoftwareComponent>(entity =>
        {
            entity.HasOne(e => e.Manufacturer)
                  .WithMany()
                  .HasForeignKey(e => e.ManufacturerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Machine
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.Property(e => e.HwComponents).HasColumnType("jsonb");
            entity.Property(e => e.SwComponents).HasColumnType("jsonb");
            entity.HasIndex(e => e.CustomIdentifier).IsUnique();
        });

        // Configure UserRole
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.Property(e => e.Privileges).HasColumnType("jsonb");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure JSONB columns for ClientPc
        modelBuilder.Entity<ClientPc>(entity =>
        {
            entity.Property(e => e.HardwareConfig).HasColumnType("jsonb");
            entity.Property(e => e.SoftwareConfig).HasColumnType("jsonb");
            entity.Property(e => e.CustomDataPoints).HasColumnType("jsonb");
            entity.Property(e => e.Predecessors).HasColumnType("jsonb");

            // Relationships
            entity.HasMany(e => e.Machines)
                  .WithMany(m => m.ClientPcs)
                  .UsingEntity(j => j.ToTable("ClientPcMachine"));

            // Indexes for faster lookups
            entity.HasIndex(e => e.Hostname).IsUnique();
            entity.HasIndex(e => e.MacAddress).IsUnique();
            
            // Link to FloorPlan
            entity.HasIndex(e => e.FloorPlanId);
            entity.HasIndex(e => e.PinnedObjectHandle);
        });

        // Configure FloorPlan
        modelBuilder.Entity<FloorPlan>(entity =>
        {
            entity.Property(e => e.Anchors).HasColumnType("jsonb");
            entity.HasIndex(e => e.Name);
        });

        // Configure JSONB column for Components
        modelBuilder.Entity<Component>(entity =>
        {
            entity.Property(e => e.AdminManagedFields).HasColumnType("jsonb");
        });
    }
}
