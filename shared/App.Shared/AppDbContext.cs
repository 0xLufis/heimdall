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
    public DbSet<BaseInventoryItem> InventoryItems { get; set; }
    public DbSet<ClientPc> ClientPcs { get; set; }
    public DbSet<Machine> Machines { get; set; }
    public DbSet<HardwareComponent> HardwareComponents { get; set; }
    public DbSet<SoftwareComponent> SoftwareComponents { get; set; }
    public DbSet<PcHardware> PcHardwares { get; set; }
    public DbSet<ResponsibleTeam> ResponsibleTeams { get; set; }
    
    public DbSet<FloorPlan> FloorPlans { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Manufacturer> Manufacturers { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<QueuedAgentCommand> QueuedAgentCommands { get; set; }
    public DbSet<AgentEvent> AgentEvents { get; set; }
    
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
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="AuthOrganization"/> entities.
    /// </summary>
    public DbSet<AuthOrganization> AuthOrganizations { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="DbSet{TEntity}"/> for <see cref="AuthMember"/> entities.
    /// </summary>
    public DbSet<AuthMember> AuthMembers { get; set; }

    /// <summary>
    /// Configures the schema needed for the model.
    /// This method is called for each context created.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema for backend entities
        modelBuilder.HasDefaultSchema("backend");

        bool isInMemory = Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        if (isInMemory)
        {
            // In-memory doesn't support JsonDocument or JSONB POCOs as native types
            // Use a value converter to store it as a string
            var jsonConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.Text.Json.JsonDocument?, string?>(
                v => v == null ? null : v.RootElement.GetRawText(),
                v => v == null ? null : System.Text.Json.JsonDocument.Parse(v, default));

            modelBuilder.Entity<BaseInventoryItem>()
                .Property(e => e.Metadata)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<UserRole>()
                .Property(e => e.Privileges)
                .HasConversion(jsonConverter);

            // Converters for ClientPc POCOs
            modelBuilder.Entity<ClientPc>().Property(e => e.FreeDiskSpace)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<DiskSpaceInfo>(v, (System.Text.Json.JsonSerializerOptions?)null));

            modelBuilder.Entity<ClientPc>().Property(e => e.MonitoringConfig)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<ResourceMonitoringConfig>(v, (System.Text.Json.JsonSerializerOptions?)null));

            modelBuilder.Entity<ClientPc>().Property(e => e.ResourceAverages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<ResourceAverages>(v, (System.Text.Json.JsonSerializerOptions?)null));

            modelBuilder.Entity<ClientPc>().Property(e => e.AlertingLimits)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<AlertingLimits>(v, (System.Text.Json.JsonSerializerOptions?)null));
        }

        // Configure Auth entities (Better-Auth) - Exclude from migrations as they are managed externally
        modelBuilder.Entity<AuthUser>(entity => {
            entity.ToTable("user", "auth", t => t.ExcludeFromMigrations());
        });
        modelBuilder.Entity<AuthSession>(entity => {
            entity.ToTable("session", "auth", t => t.ExcludeFromMigrations());
        });
        modelBuilder.Entity<AuthOrganization>(entity => {
            entity.ToTable("organization", "auth", t => t.ExcludeFromMigrations());
        });
        modelBuilder.Entity<AuthMember>(entity => {
            entity.ToTable("member", "auth", t => t.ExcludeFromMigrations());
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

        // Configure BaseInventoryItem (Root of TPT)
        modelBuilder.Entity<BaseInventoryItem>(entity =>
        {
            entity.ToTable("inventory_items");
            
            if (!isInMemory)
            {
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
            }
            
            entity.HasOne(e => e.Manufacturer)
                  .WithMany()
                  .HasForeignKey(e => e.ManufacturerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Recursive relationship (Tree)
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Link to ClientPc
            entity.HasOne(e => e.ClientPc)
                  .WithMany(p => p.InventoryItems)
                  .HasForeignKey(e => e.ClientPcId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Many-to-Many with ResponsibleTeam
            entity.HasMany(e => e.ResponsibleTeams)
                  .WithMany(t => t.ManagedItems)
                  .UsingEntity(j => j.ToTable("ItemResponsibilities"));
        });

        // Configure Machine (Station)
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.ToTable("stations"); // TPT
        });

        // Configure ClientPc (Standalone)
        modelBuilder.Entity<ClientPc>(entity =>
        {
            entity.ToTable("client_pcs");
            entity.HasKey(e => e.Id);
            
            if (!isInMemory)
            {
                entity.Property(e => e.FreeDiskSpace).HasColumnType("jsonb");
                entity.Property(e => e.MonitoringConfig).HasColumnType("jsonb");
                entity.Property(e => e.ResourceAverages).HasColumnType("jsonb");
                entity.Property(e => e.AlertingLimits).HasColumnType("jsonb");
            }

            entity.HasMany(e => e.ControlledMachines)
                  .WithMany(m => m.Controllers)
                  .UsingEntity(j => j.ToTable("StationControllers"));

            entity.HasIndex(e => e.MacAddress).IsUnique();
            entity.HasIndex(e => e.Hostname);

            entity.HasMany(e => e.PendingCommands)
                  .WithOne()
                  .HasForeignKey(c => c.ClientPcId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Events)
                  .WithOne()
                  .HasForeignKey(e => e.ClientPcId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ResponsibleTeams)
                  .WithMany() // Or if we want back-ref on ResponsibleTeam, we'd add it there
                  .UsingEntity(j => j.ToTable("PcResponsibilities"));
        });

        // Configure QueuedAgentCommand
        modelBuilder.Entity<QueuedAgentCommand>(entity =>
        {
            entity.ToTable("queued_agent_commands");
        });

        modelBuilder.Entity<AgentEvent>(entity =>
        {
            entity.ToTable("agent_events");
        });

        // Configure HardwareComponent
        modelBuilder.Entity<HardwareComponent>(entity =>
        {
            entity.ToTable("hardware_assets"); // TPT
            
            entity.HasMany(e => e.Firmware)
                  .WithOne() // Relationship is now purely hierarchical via ParentId
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SoftwareComponent
        modelBuilder.Entity<SoftwareComponent>(entity =>
        {
            entity.ToTable("software_assets"); // TPT
        });

        // Configure PcHardware
        modelBuilder.Entity<PcHardware>(entity =>
        {
            entity.ToTable("pc_hardware"); // TPT
        });

        // Configure ResponsibleTeam
        modelBuilder.Entity<ResponsibleTeam>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
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

    }
}
