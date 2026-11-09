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
        byte[] key = GetEncryptionKey();
        try
        {
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
        catch (CryptographicException)
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
        byte[] key = GetEncryptionKey();
        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            if (encryptedBytes.Length < 28) return encryptedText; // 12 nonce + 16 tag minimum

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
            if (envKey.Length < 32)
            {
                throw new InvalidOperationException("CRITICAL SECURITY ERROR: HEIMDALL_ENCRYPTION_KEY must be at least 32 characters long.");
            }
            return SHA256.HashData(Encoding.UTF8.GetBytes(envKey));
        }

        string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        bool isExplicitProdOrStaging = string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(env, "Staging", StringComparison.OrdinalIgnoreCase);

        bool isTestHost = AppDomain.CurrentDomain.FriendlyName.Contains("testhost", StringComparison.OrdinalIgnoreCase);

        if (isExplicitProdOrStaging || (!isTestHost && !string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase) && !string.Equals(env, "Test", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("CRITICAL SECURITY ERROR: HEIMDALL_ENCRYPTION_KEY must be configured via environment in non-development environments.");
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
    public DbSet<KnownTag> KnownTags { get; set; }
    public DbSet<InventoryItemTag> InventoryItemTags { get; set; }
    public DbSet<QueuedAgentCommand> QueuedAgentCommands { get; set; }
    public DbSet<AgentEvent> AgentEvents { get; set; }
    public DbSet<MachineGroup> MachineGroups { get; set; }
    public DbSet<TechnicianRule> TechnicianRules { get; set; }
    public DbSet<ShiftAbsence> ShiftAbsences { get; set; }
    
    // Machine Components, Recipes & Assets
    public DbSet<GenericTemplate> GenericTemplates { get; set; }
    public DbSet<ComponentRecipe> ComponentRecipes { get; set; }
    public DbSet<MachineComponent> MachineComponents { get; set; }
    public DbSet<ComponentPartHistory> ComponentPartHistories { get; set; }
    public DbSet<MachineServiceLog> MachineServiceLogs { get; set; }
    public DbSet<LicenseAsset> LicenseAssets { get; set; }
    public DbSet<MachineDocument> MachineDocuments { get; set; }

    // Delegations, Skills & Personnel
    public DbSet<MachineDelegationBinding> MachineDelegationBindings { get; set; }
    public DbSet<DelegationRecipe> DelegationRecipes { get; set; }
    public DbSet<SkillDefinition> SkillDefinitions { get; set; }
    public DbSet<PersonnelSkill> PersonnelSkills { get; set; }
    public DbSet<PersonnelGroup> PersonnelGroups { get; set; }
    public DbSet<PersonnelGroupMember> PersonnelGroupMembers { get; set; }
    public DbSet<SecurityGroup> SecurityGroups { get; set; }
    public DbSet<SecurityGroupMember> SecurityGroupMembers { get; set; }
    public DbSet<FunctionalSetting> FunctionalSettings { get; set; }
    public DbSet<MachineGroupMembership> MachineGroupMemberships { get; set; }
    public DbSet<DelegationMaintenanceGroup> DelegationMaintenanceGroups { get; set; }
    public DbSet<DelegationMachineGroup> DelegationMachineGroups { get; set; }
    
    // System Governance, Identity & PKI Sets
    public DbSet<SecurityGroupMapping> SecurityGroupMappings { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<ClientCertificateRecord> ClientCertificates { get; set; }
    public DbSet<OuCertificateRule> OuCertificateRules { get; set; }
    public DbSet<SchemaVersionManifest> SchemaVersionManifests { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<MalformedTelemetryRecord> MalformedTelemetryRecords { get; set; }
    public DbSet<AdOuGovernance> AdOuGovernances { get; set; }
    public DbSet<DiagnosticSnapshot> DiagnosticSnapshots { get; set; }
    
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

            var stringListConverter = new ValueConverter<List<string>, string>(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => SafeDeserializeJson<List<string>>(v) ?? new List<string>());

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

            modelBuilder.Entity<ComponentRecipe>()
                .Property(e => e.AutoTags)
                .HasConversion(stringListConverter);

            modelBuilder.Entity<MachineComponent>()
                .Property(e => e.Tags)
                .HasConversion(stringListConverter);

            // Converters for ClientPc POCOs with safe deserialization
            modelBuilder.Entity<ClientPc>().Property(e => e.FreeDiskSpace)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => SafeDeserializeJson<DiskSpaceInfo>(v));

            modelBuilder.Entity<ClientPc>().Property(e => e.MonitoringConfig)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => SafeDeserializeJson<ResourceMonitoringConfig>(v));

            modelBuilder.Entity<ClientPc>().Property(e => e.ResourceAverages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => SafeDeserializeJson<ResourceAverages>(v));

            modelBuilder.Entity<ClientPc>().Property(e => e.AlertingLimits)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => SafeDeserializeJson<AlertingLimits>(v));

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

            // Link to installed Machine (Station)
            entity.HasOne(e => e.Machine)
                  .WithMany()
                  .HasForeignKey(e => e.MachineId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Many-to-Many with ResponsibleTeam
            entity.HasMany(e => e.ResponsibleTeams)
                  .WithMany(t => t.ManagedItems)
                  .UsingEntity(j => j.ToTable("ItemResponsibilities"));
        });

        // Configure Machine (TPT on stations)
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.ToTable("stations");

            entity.HasMany(e => e.Components)
                  .WithOne(c => c.Machine)
                  .HasForeignKey(c => c.MachineId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.DelegationBindings)
                  .WithOne(b => b.Machine)
                  .HasForeignKey(b => b.MachineId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.MaintenanceTickets)
                  .WithOne(t => t.Machine)
                  .HasForeignKey(t => t.MachineId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.ServiceLogs)
                  .WithOne(l => l.Machine)
                  .HasForeignKey(l => l.MachineId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ParentMachine)
                  .WithMany(e => e.SubMachines)
                  .HasForeignKey(e => e.ParentMachineId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SubLineGroup)
                  .WithMany()
                  .HasForeignKey(e => e.SubLineGroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.LineGroup)
                  .WithMany()
                  .HasForeignKey(e => e.LineGroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProjectGroup)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectGroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.BusinessUnitGroup)
                  .WithMany()
                  .HasForeignKey(e => e.BusinessUnitGroupId)
                  .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<DiagnosticSnapshot>(entity =>
        {
            entity.ToTable("diagnostic_snapshots");
            entity.HasIndex(e => new { e.ClientPcId, e.CapturedAtUtc });
            entity.HasIndex(e => e.OrganizationId);
            entity.HasOne(e => e.ClientPc)
                  .WithMany()
                  .HasForeignKey(e => e.ClientPcId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.HasIndex(e => e.IsEscalated);
            entity.HasIndex(e => e.IsLineStop);
            entity.HasIndex(e => e.ResponsibleDepartment);
            entity.Property(e => e.PendingReason).HasConversion<string>();
            entity.Property(e => e.EscalationHandoverState).HasConversion<string>();

            entity.HasOne(e => e.Equipment)
                  .WithMany()
                  .HasForeignKey(e => e.EquipmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ClientPc)
                  .WithMany()
                  .HasForeignKey(e => e.ClientPcId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Machine)
                  .WithMany(m => m.MaintenanceTickets)
                  .HasForeignKey(e => e.MachineId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TargetComponent)
                  .WithMany()
                  .HasForeignKey(e => e.TargetComponentId)
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

        // Configure Universal Generic Templates & Recipes
        modelBuilder.Entity<GenericTemplate>(entity =>
        {
            entity.ToTable("generic_templates");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.TemplateType);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.ExtendsTemplateId);
            entity.HasIndex(e => e.OrganizationId);

            entity.HasOne(e => e.ExtendsTemplate)
                  .WithMany()
                  .HasForeignKey(e => e.ExtendsTemplateId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Component Recipes
        modelBuilder.Entity<ComponentRecipe>(entity =>
        {
            entity.ToTable("component_recipes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RecipeCode).IsUnique();
            entity.Property(e => e.Category).HasConversion<string>();

            entity.HasOne(e => e.ExtendsRecipe)
                  .WithMany()
                  .HasForeignKey(e => e.ExtendsRecipeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Machine Components
        modelBuilder.Entity<MachineComponent>(entity =>
        {
            entity.ToTable("machine_components");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineId, e.FunctionalSlotIdentifier }).IsUnique();
            entity.Property(e => e.Category).HasConversion<string>();

            entity.HasOne(e => e.ComponentRecipe)
                  .WithMany(r => r.InstantiatedComponents)
                  .HasForeignKey(e => e.ComponentRecipeId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CurrentPart)
                  .WithMany()
                  .HasForeignKey(e => e.CurrentPartId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.PartHistory)
                  .WithOne(h => h.Component)
                  .HasForeignKey(h => h.ComponentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Component Part Histories
        modelBuilder.Entity<ComponentPartHistory>(entity =>
        {
            entity.ToTable("component_part_histories");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ComponentId);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.SwappedAt);
            entity.Property(e => e.Reason).HasConversion<string>();
            entity.Property(e => e.PreviousPartDisposition).HasConversion<string>();
        });

        // Configure Machine Service Logs
        modelBuilder.Entity<MachineServiceLog>(entity =>
        {
            entity.ToTable("machine_service_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Configure License Assets
        modelBuilder.Entity<LicenseAsset>(entity =>
        {
            entity.ToTable("license_assets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineId);
            entity.Property(e => e.LicenseKeyEncrypted).HasConversion(encryptedConverter);
        });

        // Configure Machine Documents
        modelBuilder.Entity<MachineDocument>(entity =>
        {
            entity.ToTable("machine_documents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineId);
        });

        // Configure Machine Delegation Bindings
        modelBuilder.Entity<MachineDelegationBinding>(entity =>
        {
            entity.ToTable("machine_delegation_bindings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineId, e.DelegationRecipeId, e.Discipline }).IsUnique();
            entity.Property(e => e.Discipline).HasConversion<string>();

            entity.HasOne(e => e.DelegationRecipe)
                  .WithMany(r => r.Bindings)
                  .HasForeignKey(e => e.DelegationRecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Delegation Recipes
        modelBuilder.Entity<DelegationRecipe>(entity =>
        {
            entity.ToTable("delegation_recipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Discipline).HasConversion<string>();

            entity.HasOne(e => e.PrimaryPersonnelGroup)
                  .WithMany()
                  .HasForeignKey(e => e.PrimaryPersonnelGroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.EscalationGroup)
                  .WithMany()
                  .HasForeignKey(e => e.EscalationGroupId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Skill Definitions
        modelBuilder.Entity<SkillDefinition>(entity =>
        {
            entity.ToTable("skill_definitions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Category).HasConversion<string>();
        });

        // Configure Personnel Skills
        modelBuilder.Entity<PersonnelSkill>(entity =>
        {
            entity.ToTable("personnel_skills");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.SkillId }).IsUnique();

            entity.HasOne(e => e.Skill)
                  .WithMany()
                  .HasForeignKey(e => e.SkillId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Personnel Groups
        modelBuilder.Entity<PersonnelGroup>(entity =>
        {
            entity.ToTable("personnel_groups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Discipline).HasConversion<string>();

            entity.HasMany(e => e.Members)
                  .WithOne(m => m.PersonnelGroup)
                  .HasForeignKey(m => m.PersonnelGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Personnel Group Members
        modelBuilder.Entity<PersonnelGroupMember>(entity =>
        {
            entity.ToTable("personnel_group_members");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PersonnelGroupId, e.UserId }).IsUnique();
        });

        // Configure Security Groups
        modelBuilder.Entity<SecurityGroup>(entity =>
        {
            entity.ToTable("security_groups");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ParentGroup)
                  .WithMany()
                  .HasForeignKey(e => e.ParentGroupId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Members)
                  .WithOne(m => m.SecurityGroup)
                  .HasForeignKey(m => m.SecurityGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Security Group Members
        modelBuilder.Entity<SecurityGroupMember>(entity =>
        {
            entity.ToTable("security_group_members");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SecurityGroupId, e.UserId }).IsUnique();
        });

        // Configure Functional Settings
        modelBuilder.Entity<FunctionalSetting>(entity =>
        {
            entity.ToTable("functional_settings");
            entity.HasKey(e => e.Key);
        });

        // Configure Machine Groups
        modelBuilder.Entity<MachineGroup>(entity =>
        {
            entity.ToTable("machine_groups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GroupType).HasConversion<string>();

            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Machine Group Memberships
        modelBuilder.Entity<MachineGroupMembership>(entity =>
        {
            entity.ToTable("machine_group_memberships");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GroupId, e.MachineId }).IsUnique();

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Memberships)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Machine)
                  .WithMany()
                  .HasForeignKey(e => e.MachineId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Delegation Maintenance Groups (referring to 1 or more maintenance groups)
        modelBuilder.Entity<DelegationMaintenanceGroup>(entity =>
        {
            entity.ToTable("delegation_maintenance_groups");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DelegationRecipeId, e.MaintenanceGroupId }).IsUnique();

            entity.HasOne(e => e.DelegationRecipe)
                  .WithMany(r => r.MaintenanceGroups)
                  .HasForeignKey(e => e.DelegationRecipeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MaintenanceGroup)
                  .WithMany()
                  .HasForeignKey(e => e.MaintenanceGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Delegation Machine Groups (referring to 1 or more machine groups)
        modelBuilder.Entity<DelegationMachineGroup>(entity =>
        {
            entity.ToTable("delegation_machine_groups");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DelegationRecipeId, e.MachineGroupId }).IsUnique();

            entity.HasOne(e => e.DelegationRecipe)
                  .WithMany(r => r.MachineGroups)
                  .HasForeignKey(e => e.DelegationRecipeId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MachineGroup)
                  .WithMany()
                  .HasForeignKey(e => e.MachineGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Multi-Tenant Global Query Filters
        modelBuilder.Entity<BaseInventoryItem>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<ClientPc>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<MaintenanceTicket>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<PersonnelGroup>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<SecurityGroup>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<AgentEvent>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<QueuedAgentCommand>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == null || e.OrganizationId == CurrentOrganizationId);
    }

    private static T? SafeDeserializeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, (System.Text.Json.JsonSerializerOptions?)null);
        }
        catch
        {
            return null;
        }
    }
}
