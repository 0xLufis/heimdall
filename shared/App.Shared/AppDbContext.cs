using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Shared.Data;

/// <summary>
/// Represents the database context for the Heimdall application, providing access to all entities.
/// Configures entity mappings, relationships, and PostgreSQL-specific JSONB column types.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by this context.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain Sets
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="ClientPc"/> entities.
    /// </summary>
    public DbSet<ClientPc> ClientPcs { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="Component"/> entities.
    /// </summary>
    public DbSet<Component> Components { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="FloorPlan"/> entities.
    /// </summary>
    public DbSet<FloorPlan> FloorPlans { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="Machine"/> entities.
    /// </summary>
    public DbSet<Machine> Machines { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="SoftwareComponent"/> entities.
    /// </summary>
    public DbSet<SoftwareComponent> SoftwareComponents { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="HardwareComponent"/> entities.
    /// </summary>
    public DbSet<HardwareComponent> HardwareComponents { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="UserRole"/> entities.
    /// </summary>
    public DbSet<UserRole> UserRoles { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="Manufacturer"/> entities.
    /// </summary>
    public DbSet<Manufacturer> Manufacturers { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="Supplier"/> entities.
    /// </summary>
    public DbSet<Supplier> Suppliers { get; set; }
    
    // Auth Sets (Managed by Better-Auth, excluded from migrations)
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="AuthUser"/> entities.
    /// </summary>
    public DbSet<AuthUser> AuthUsers { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="AuthSession"/> entities.
    /// </summary>
    public DbSet<AuthSession> AuthSessions { get; set; }

    /// <summary>
    /// Configures the schema needed for the model.
    /// This method is called for each context created.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        bool isInMemory = Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        // Configure Auth entities (Better-Auth) - Exclude from migrations as they are managed externally
        modelBuilder.Entity<AuthUser>(entity => {
            entity.ToTable("user", "heimdall_dev_db", t => t.ExcludeFromMigrations());
        });
        modelBuilder.Entity<AuthSession>(entity => {
            entity.ToTable("session", "heimdall_dev_db", t => t.ExcludeFromMigrations());
        });

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
            if (!isInMemory)
            {
                entity.Property(e => e.TechnicalSpecs).HasColumnType("jsonb");
            }
            else
            {
                entity.Ignore(e => e.TechnicalSpecs);
            }
            
            entity.HasOne(e => e.Manufacturer)
                  .WithMany()
                  .HasForeignKey(e => e.ManufacturerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Recursive relationship
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId);
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
            
            // Recursive relationship
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId);
        });

        // Configure Machine
        modelBuilder.Entity<Machine>(entity =>
        {
            if (!isInMemory)
            {
                entity.Property(e => e.HwComponents).HasColumnType("jsonb");
                entity.Property(e => e.SwComponents).HasColumnType("jsonb");
            }
            else
            {
                entity.Ignore(e => e.HwComponents);
                entity.Ignore(e => e.SwComponents);
            }
            entity.HasIndex(e => e.CustomIdentifier).IsUnique();
        });

        // Configure UserRole
        modelBuilder.Entity<UserRole>(entity =>
        {
            if (!isInMemory)
            {
                entity.Property(e => e.Privileges).HasColumnType("jsonb");
            }
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure JSONB columns for ClientPc
        modelBuilder.Entity<ClientPc>(entity =>
        {
            if (!isInMemory)
            {
                entity.Property(e => e.HardwareConfig).HasColumnType("jsonb");
                entity.Property(e => e.SoftwareConfig).HasColumnType("jsonb");
                entity.Property(e => e.CustomDataPoints).HasColumnType("jsonb");
                entity.Property(e => e.Predecessors).HasColumnType("jsonb");
            }
            else
            {
                entity.Ignore(e => e.CustomDataPoints);
                entity.Ignore(e => e.Predecessors);
                // When in-memory, we must explicitly tell EF they are owned types 
                // because they don't have PKs and aren't automatically mapped as JSONB
                entity.OwnsOne(e => e.HardwareConfig);
                entity.OwnsOne(e => e.SoftwareConfig);
            }

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
            if (!isInMemory)
            {
                entity.Property(e => e.Anchors).HasColumnType("jsonb");
            }
            else
            {
                entity.Ignore(e => e.Anchors);
            }
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<FloorPlanAnchor>(entity =>
        {
            entity.HasNoKey();
        });

        modelBuilder.Entity<PcPredecessor>(entity =>
        {
            entity.HasNoKey();
        });

        // Configure JSONB column for Components
        modelBuilder.Entity<Component>(entity =>
        {
            if (!isInMemory)
            {
                entity.Property(e => e.AdminManagedFields).HasColumnType("jsonb");
            }
            else
            {
                entity.Ignore(e => e.AdminManagedFields);
            }
        });
    }
}
