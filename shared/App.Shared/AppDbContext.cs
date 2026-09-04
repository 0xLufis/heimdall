using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Cryptography;
using System.Text;

namespace App.Shared.Data;

/// <summary>
/// Value converter for AES-256-GCM string encryption and decryption.
/// Used for sensitive database properties such as FloorPlan.SvgContent and SoftwareAsset.LicenseKey.
/// </summary>
public class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter() : base(
        v => Encrypt(v),
        v => Decrypt(v))
    { }

    /// <summary>
    /// Encrypts plain text using AES-256-GCM.
    /// Payload structure: Nonce (12B) + Tag (16B) + CipherText (variable). Encoded as Base64.
    /// </summary>
    public static string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        try
        {
            byte[] key = GetEncryptionKey();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[plainBytes.Length];

            using var aesGcm = new AesGcm(key, 16);
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

            byte[] result = new byte[nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, result, nonce.Length + tag.Length, cipherText.Length);

            return Convert.ToBase64String(result);
        }
        catch
        {
            return plainText;
        }
    }

    /// <summary>
    /// Decrypts AES-256-GCM Base64 payload.
    /// Falls back to unencrypted text if payload format is invalid or decryption fails.
    /// </summary>
    public static string? Decrypt(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return encryptedText;
        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            if (encryptedBytes.Length < 28) return encryptedText; // 12 nonce + 16 tag minimum

            byte[] key = GetEncryptionKey();
            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[encryptedBytes.Length - 28];

            Buffer.BlockCopy(encryptedBytes, 0, nonce, 0, 12);
            Buffer.BlockCopy(encryptedBytes, 12, tag, 0, 16);
            Buffer.BlockCopy(encryptedBytes, 28, cipherText, 0, cipherText.Length);

            byte[] plainBytes = new byte[cipherText.Length];
            using var aesGcm = new AesGcm(key, 16);
            aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return encryptedText;
        }
    }

    private static byte[] GetEncryptionKey()
    {
        string? envKey = Environment.GetEnvironmentVariable("HEIMDALL_ENCRYPTION_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(envKey));
        }

        string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CRITICAL SECURITY ERROR: HEIMDALL_ENCRYPTION_KEY must be configured via environment in Production environments.");
        }

        // Development/Test fallback key with explicit security warning
        return SHA256.HashData(Encoding.UTF8.GetBytes("Heimdall_AES256_GCM_DevSecretKey_32B"));
    }
}

/// <summary>
/// Represents the database context for the Heimdall application, providing access to all entities.
/// Configures entity mappings, relationships, graph-relational edges, encrypted fields, and EF Core GIN indexes for JSONB columns.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Active organization identifier for multi-tenant query filtering.
    /// When populated, automatically filters tenant-scoped entities.
    /// </summary>
    public string? CurrentOrganizationId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by this context.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Domain Sets
    public DbSet<BaseInventoryItem> InventoryItems { get; set; }
    public DbSet<ClientPc> ClientPcs { get; set; }
    public DbSet<Machine> Machines { get; set; }
    public DbSet<StationController> StationControllers { get; set; }
    public DbSet<HardwareComponent> HardwareComponents { get; set; }
    public DbSet<SoftwareAsset> SoftwareAssets { get; set; }
    public DbSet<SoftwareComponent> SoftwareComponents { get; set; }
    public DbSet<PcHardware> PcHardwares { get; set; }
    public DbSet<EquipmentInterconnect> EquipmentInterconnects { get; set; }
    public DbSet<MaintenanceTicket> MaintenanceTickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }
    public DbSet<ResponsibleTeam> ResponsibleTeams { get; set; }
    
    public DbSet<FloorPlan> FloorPlans { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Manufacturer> Manufacturers { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<QueuedAgentCommand> QueuedAgentCommands { get; set; }
    public DbSet<AgentEvent> AgentEvents { get; set; }
    
    // System Governance, Identity & PKI Sets
    public DbSet<SecurityGroupMapping> SecurityGroupMappings { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<ClientCertificateRecord> ClientCertificates { get; set; }
    public DbSet<OuCertificateRule> OuCertificateRules { get; set; }
    public DbSet<SchemaVersionManifest> SchemaVersionManifests { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<MalformedTelemetryRecord> MalformedTelemetryRecords { get; set; }
    
    // Auth Sets (Managed by Better-Auth, excluded from migrations)
    public DbSet<AuthUser> AuthUsers { get; set; }
    public DbSet<AuthSession> AuthSessions { get; set; }
    public DbSet<AuthOrganization> AuthOrganizations { get; set; }
    public DbSet<AuthMember> AuthMembers { get; set; }

    /// <summary>
    /// Configures the schema needed for the model.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema for backend entities
        modelBuilder.HasDefaultSchema("backend");

        bool isInMemory = Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        var encryptedConverter = new EncryptedStringConverter();
        var nonNullEncryptedConverter = new ValueConverter<string, string>(
            v => EncryptedStringConverter.Encrypt(v) ?? string.Empty,
            v => EncryptedStringConverter.Decrypt(v) ?? string.Empty);

        if (isInMemory)
        {
            // In-memory doesn me-not support JsonDocument or JSONB POCOs as native types
            // Use a value converter to store it as a string
            var jsonConverter = new ValueConverter<System.Text.Json.JsonDocument?, string?>(
                v => v == null ? null : v.RootElement.GetRawText(),
                v => v == null ? null : System.Text.Json.JsonDocument.Parse(v, default));

            modelBuilder.Entity<BaseInventoryItem>()
                .Property(e => e.Metadata)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<UserRole>()
                .Property(e => e.Privileges)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<ClientPc>()
                .Property(e => e.SystemMetadata)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<ClientPc>()
                .Property(e => e.OuTags)
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

            modelBuilder.Entity<StationController>()
                .Property(e => e.Metadata)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<EquipmentInterconnect>()
                .Property(e => e.Metadata)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<MaintenanceTicket>()
                .Property(e => e.Metadata)
                .HasConversion(jsonConverter);
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
                entity.HasIndex(e => e.Metadata).HasMethod("gin");
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
                entity.Property(e => e.SystemMetadata).HasColumnType("jsonb");
                entity.Property(e => e.OuTags).HasColumnType("jsonb");
                entity.HasIndex(e => e.SystemMetadata).HasMethod("gin");
            }

            entity.HasMany(e => e.ControlledMachines)
                  .WithMany(m => m.Controllers)
                  .UsingEntity<StationController>(
                      j => j.HasOne(sc => sc.Machine).WithMany(m => m.StationControllers).HasForeignKey(sc => sc.MachineId),
                      j => j.HasOne(sc => sc.ClientPc).WithMany(c => c.StationControllers).HasForeignKey(sc => sc.ClientPcId),
                      j =>
                      {
                          j.ToTable("StationControllers");
                          j.HasKey(sc => sc.Id);
                          j.HasIndex(sc => new { sc.MachineId, sc.ClientPcId }).IsUnique();
                      });

            entity.HasIndex(e => e.MacAddress).IsUnique();
            entity.HasIndex(e => e.Hostname);
            entity.HasIndex(e => e.OrganizationId);

            entity.HasMany(e => e.PendingCommands)
                  .WithOne()
                  .HasForeignKey(c => c.ClientPcId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Events)
                  .WithOne()
                  .HasForeignKey(e => e.ClientPcId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ResponsibleTeams)
                  .WithMany()
                  .UsingEntity(j => j.ToTable("PcResponsibilities"));
        });

        // Configure StationController
        modelBuilder.Entity<StationController>(entity =>
        {
            entity.ToTable("StationControllers");
            entity.HasKey(sc => sc.Id);

            if (!isInMemory)
            {
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                entity.HasIndex(e => e.Metadata).HasMethod("gin");
            }
        });

        // Configure QueuedAgentCommand
        modelBuilder.Entity<QueuedAgentCommand>(entity =>
        {
            entity.ToTable("queued_agent_commands");
            entity.HasIndex(e => e.ClientPcId);
            entity.HasIndex(e => e.OrganizationId);
        });

        modelBuilder.Entity<AgentEvent>(entity =>
        {
            entity.ToTable("agent_events");
            entity.HasIndex(e => new { e.ClientPcId, e.Timestamp });
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.OrganizationId);
        });

        // Configure HardwareComponent
        modelBuilder.Entity<HardwareComponent>(entity =>
        {
            entity.ToTable("hardware_assets"); // TPT
            
            entity.HasMany(e => e.Firmware)
                  .WithOne()
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SoftwareAsset
        modelBuilder.Entity<SoftwareAsset>(entity =>
        {
            entity.ToTable("software_assets"); // TPT

            // Encrypted string property for SoftwareAsset.LicenseKey
            entity.Property(e => e.LicenseKey)
                  .HasConversion(encryptedConverter);
        });

        // Configure SoftwareComponent
        modelBuilder.Entity<SoftwareComponent>(entity =>
        {
            entity.ToTable("software_components"); // TPT
        });

        // Configure PcHardware
        modelBuilder.Entity<PcHardware>(entity =>
        {
            entity.ToTable("pc_hardware"); // TPT
        });

        // Configure EquipmentInterconnect
        modelBuilder.Entity<EquipmentInterconnect>(entity =>
        {
            entity.ToTable("equipment_interconnects");
            entity.HasKey(e => e.Id);

            if (!isInMemory)
            {
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                entity.HasIndex(e => e.Metadata).HasMethod("gin");
            }

            entity.HasOne(e => e.SourceEquipment)
                  .WithMany()
                  .HasForeignKey(e => e.SourceEquipmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetEquipment)
                  .WithMany()
                  .HasForeignKey(e => e.TargetEquipmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure MaintenanceTicket
        modelBuilder.Entity<MaintenanceTicket>(entity =>
        {
            entity.ToTable("maintenance_tickets");
            entity.HasKey(e => e.Id);

            if (!isInMemory)
            {
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
                entity.HasIndex(e => e.Metadata).HasMethod("gin");
            }

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.AssignedTo);
            entity.HasIndex(e => e.OrganizationId);

            entity.HasOne(e => e.Equipment)
                  .WithMany()
                  .HasForeignKey(e => e.EquipmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ClientPc)
                  .WithMany()
                  .HasForeignKey(e => e.ClientPcId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Machine)
                  .WithMany()
                  .HasForeignKey(e => e.MachineId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Comments)
                  .WithOne(c => c.MaintenanceTicket)
                  .HasForeignKey(c => c.MaintenanceTicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.MaintenanceTicket)
                  .HasForeignKey(a => a.MaintenanceTicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TicketComment
        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.ToTable("ticket_comments");
            entity.HasKey(c => c.Id);
        });

        // Configure TicketAttachment
        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.ToTable("ticket_attachments");
            entity.HasKey(a => a.Id);
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

            // Encrypted string property for FloorPlan.SvgContent
            entity.Property(e => e.SvgContent)
                  .HasConversion(nonNullEncryptedConverter);
        });

        modelBuilder.Entity<FloorPlanAnchor>(entity =>
        {
            entity.HasNoKey();
        });

        // Configure System Governance & PKI Entities
        modelBuilder.Entity<SecurityGroupMapping>(entity =>
        {
            entity.ToTable("security_group_mappings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GroupIdentifier);
            entity.HasIndex(e => e.OrganizationId);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<ClientCertificateRecord>(entity =>
        {
            entity.ToTable("client_certificates");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Thumbprint);
            entity.HasIndex(e => e.CommonName);
            entity.HasIndex(e => e.ClientPcId);
        });

        modelBuilder.Entity<OuCertificateRule>(entity =>
        {
            entity.ToTable("ou_certificate_rules");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OuPath);
        });

        modelBuilder.Entity<SchemaVersionManifest>(entity =>
        {
            entity.ToTable("schema_version_manifest");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SchemaVersion);
        });

        // Configure Auditing & Dead-Letter Quarantine Entities
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.OrganizationId);
        });

        modelBuilder.Entity<MalformedTelemetryRecord>(entity =>
        {
            entity.ToTable("malformed_telemetry_quarantine");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IngestionChannel);
            entity.HasIndex(e => e.QuarantinedAt);
            entity.HasIndex(e => e.OrganizationId);
        });

        // Multi-Tenant Global Query Filters
        modelBuilder.Entity<BaseInventoryItem>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<ClientPc>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<MaintenanceTicket>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<AgentEvent>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<QueuedAgentCommand>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);
    }
}
