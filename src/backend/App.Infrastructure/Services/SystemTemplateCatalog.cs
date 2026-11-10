namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Contracts.Templates;
using App.Shared.Entities;

/// <summary>
/// Comprehensive catalog of system-provided default blueprints for Machines and Components,
/// specifically modeled for Tier 1 Automotive Electronics & PCBA Manufacturing.
/// </summary>
public static class SystemTemplateCatalog
{
    private static GenericTemplate BuildTemplate(
        string id,
        string type,
        string code,
        string name,
        string description,
        string category,
        string icon,
        List<string> tags,
        List<TemplateFieldDefinitionDto> schema,
        Dictionary<string, object?> defaultValues)
    {
        return new GenericTemplate
        {
            Id = Guid.Parse(id),
            TemplateType = type,
            Code = code,
            Name = name,
            Description = description,
            Category = category,
            Icon = icon,
            Version = "1.0.0",
            Tags = tags,
            SchemaJson = JsonSerializer.Serialize(schema),
            DefaultValuesJson = JsonSerializer.Serialize(defaultValues),
            IsActive = true,
            IsBuiltin = true,
            OrganizationId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static IReadOnlyList<GenericTemplate> All
    {
        get
        {
            var list = new List<GenericTemplate>();
            list.AddRange(GetMachineTemplates());
            list.AddRange(GetComponentTemplates());
            list.AddRange(GetSupportTemplates());
            return list;
        }
    }

    public static IReadOnlyList<GenericTemplate> GetMachineTemplates()
    {
        return new List<GenericTemplate>
        {
            // 1. SMT Solder Paste Printer
            BuildTemplate(
                "10000000-0000-0000-0000-000000000001",
                GenericTemplateTypes.Machine,
                "MCH-SMT-PRINT-01",
                "High-Precision Solder Paste Stencil Printer",
                "Automated in-line stencil printer with dual squeegee closed-loop pressure control, vision fiducial alignment, and integrated 2D SPI paste inspection.",
                "SMT",
                "Factory",
                new List<string> { "SMT", "Printer", "Stencil", "DEK", "SPI", "PCBA" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "squeegee_pressure_kg", Label = "Squeegee Pressure", FieldType = "number", Required = true, DefaultValue = 7.5, Unit = "kg", Min = 1.0, Max = 25.0, Section = "Process Parameters", Order = 1 },
                    new() { Key = "print_speed_mm_s", Label = "Printing Speed", FieldType = "number", Required = true, DefaultValue = 45.0, Unit = "mm/s", Min = 5.0, Max = 200.0, Section = "Process Parameters", Order = 2 },
                    new() { Key = "separation_speed_mm_s", Label = "Stencil Separation Speed", FieldType = "number", DefaultValue = 1.2, Unit = "mm/s", Min = 0.1, Max = 15.0, Section = "Process Parameters", Order = 3 },
                    new() { Key = "solder_paste_alloy", Label = "Solder Paste Type", FieldType = "select", Required = true, DefaultValue = "SAC305 Type 4", Section = "Consumables", Order = 4, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "SAC305 (Sn96.5 Ag3.0 Cu0.5) Type 4", Value = "SAC305 Type 4" },
                        new() { Label = "SAC305 Type 5 (Ultra-Fine Pitch)", Value = "SAC305 Type 5" },
                        new() { Label = "Sn63/Pb37 Leaded (Aerospace/Specialty)", Value = "Sn63Pb37" },
                        new() { Label = "Low-Temp Bi-In Alloy (Sn42Bi58)", Value = "Sn42Bi58" }
                    }},
                    new() { Key = "auto_wipe_interval_boards", Label = "Automatic Stencil Wipe Interval", FieldType = "number", DefaultValue = 5, Unit = "boards", Min = 1, Max = 50, Section = "Maintenance", Order = 5 },
                    new() { Key = "smema_interface_version", Label = "Board Transport Interface", FieldType = "select", DefaultValue = "IPC-HERMES-9852", Section = "Connectivity", Order = 6, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "IPC-HERMES-9852 (Ethernet/JSON)", Value = "IPC-HERMES-9852" },
                        new() { Label = "IPC-SMEMA-9851 (Hardware I/O)", Value = "IPC-SMEMA-9851" }
                    }},
                    new() { Key = "target_cycle_time_s", Label = "Target Cycle Time", FieldType = "number", DefaultValue = 18.0, Unit = "s", Min = 8.0, Max = 60.0, Section = "Performance", Order = 7 }
                },
                new Dictionary<string, object?>
                {
                    { "squeegee_pressure_kg", 7.5 },
                    { "print_speed_mm_s", 45.0 },
                    { "separation_speed_mm_s", 1.2 },
                    { "solder_paste_alloy", "SAC305 Type 4" },
                    { "auto_wipe_interval_boards", 5 },
                    { "smema_interface_version", "IPC-HERMES-9852" },
                    { "target_cycle_time_s", 18.0 }
                }),

            // 2. 3D Solder Paste Inspection (SPI)
            BuildTemplate(
                "10000000-0000-0000-0000-000000000002",
                GenericTemplateTypes.Machine,
                "MCH-SMT-SPI-01",
                "In-Line 3D Solder Paste Inspection Machine",
                "High-speed 3D Moiré fringe interferometry machine for true volumetric measurement of printed solder paste deposits, height, area, and bridging detection.",
                "Inspection",
                "Eye",
                new List<string> { "SMT", "SPI", "3D", "Inspection", "KohYoung", "Quality" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "min_volume_pct", Label = "Minimum Volume Lower Limit", FieldType = "number", Required = true, DefaultValue = 70.0, Unit = "%", Min = 40.0, Max = 100.0, Section = "Inspection Limits", Order = 1 },
                    new() { Key = "max_volume_pct", Label = "Maximum Volume Upper Limit", FieldType = "number", Required = true, DefaultValue = 140.0, Unit = "%", Min = 100.0, Max = 200.0, Section = "Inspection Limits", Order = 2 },
                    new() { Key = "nominal_paste_height_um", Label = "Nominal Stencil Thickness (Z)", FieldType = "number", Required = true, DefaultValue = 120.0, Unit = "µm", Min = 80.0, Max = 200.0, Section = "Inspection Limits", Order = 3 },
                    new() { Key = "bridge_detection_sensitivity", Label = "Solder Bridging Sensitivity", FieldType = "select", DefaultValue = "High", Section = "Algorithm", Order = 4, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "Standard (Pitch > 0.5mm)", Value = "Standard" },
                        new() { Label = "High (Fine-Pitch 0.3mm)", Value = "High" },
                        new() { Label = "Ultra (01005 / WLCSP)", Value = "Ultra" }
                    }},
                    new() { Key = "feedback_to_printer", Label = "Closed-Loop Printer Offset Feedback", FieldType = "boolean", DefaultValue = true, Section = "Integration", Order = 5 }
                },
                new Dictionary<string, object?>
                {
                    { "min_volume_pct", 70.0 },
                    { "max_volume_pct", 140.0 },
                    { "nominal_paste_height_um", 120.0 },
                    { "bridge_detection_sensitivity", "High" },
                    { "feedback_to_printer", true }
                }),

            // 3. High-Speed SMT Pick & Place Chip Mounter
            BuildTemplate(
                "10000000-0000-0000-0000-000000000003",
                GenericTemplateTypes.Machine,
                "MCH-SMT-PNP-01",
                "High-Speed SMT Chip Placement Mounter",
                "Multi-gantry rotary head surface mount placement machine delivering up to 90,000 CPH for discrete 01005/0201 chips and high-density IC packages.",
                "SMT",
                "Factory",
                new List<string> { "SMT", "PickAndPlace", "Fuji", "ASM", "Siplace", "Panasonic" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "cph_rating", Label = "Nominal Placement Speed", FieldType = "number", Required = true, DefaultValue = 75000, Unit = "CPH", Min = 10000, Max = 150000, Section = "Specifications", Order = 1 },
                    new() { Key = "feeder_slots_8mm", Label = "Feeder Capacity (8mm equivalent)", FieldType = "number", Required = true, DefaultValue = 120, Unit = "slots", Min = 40, Max = 250, Section = "Specifications", Order = 2 },
                    new() { Key = "placement_accuracy_um", Label = "Alignment Accuracy (3 Sigma)", FieldType = "number", DefaultValue = 25.0, Unit = "µm", Min = 5.0, Max = 50.0, Section = "Specifications", Order = 3 },
                    new() { Key = "vacuum_check_before_placement", Label = "Part Pickup Vacuum Verification", FieldType = "boolean", DefaultValue = true, Section = "Quality", Order = 4 },
                    new() { Key = "supported_smallest_chip", Label = "Smallest Component Geometry", FieldType = "select", DefaultValue = "01005 (0402 Metric)", Section = "Tooling", Order = 5, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "01005 (0402 Metric)", Value = "01005" },
                        new() { Label = "0201 (0603 Metric)", Value = "0201" },
                        new() { Label = "0402 (1005 Metric)", Value = "0402" }
                    }}
                },
                new Dictionary<string, object?>
                {
                    { "cph_rating", 75000 },
                    { "feeder_slots_8mm", 120 },
                    { "placement_accuracy_um", 25.0 },
                    { "vacuum_check_before_placement", true },
                    { "supported_smallest_chip", "01005" }
                }),

            // 4. Multi-Function Odd-Form Mounter
            BuildTemplate(
                "10000000-0000-0000-0000-000000000004",
                GenericTemplateTypes.Machine,
                "MCH-SMT-PNP-02",
                "Odd-Form & Heavy Component Placement Station",
                "High-force placement system for automotive connectors, electrolytic capacitors, transformers, and shielded metal housings with force-monitored insertion.",
                "SMT",
                "Factory",
                new List<string> { "SMT", "OddForm", "Connectors", "Insertion", "Assembly" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "max_insertion_force_n", Label = "Maximum Insertion Force", FieldType = "number", Required = true, DefaultValue = 100.0, Unit = "N", Min = 5.0, Max = 300.0, Section = "Force Control", Order = 1 },
                    new() { Key = "max_component_weight_g", Label = "Maximum Component Weight", FieldType = "number", DefaultValue = 150.0, Unit = "g", Min = 10.0, Max = 1000.0, Section = "Payload", Order = 2 },
                    new() { Key = "feeder_type", Label = "Feeder System", FieldType = "select", DefaultValue = "Tray + Bowl", Section = "Feeder", Order = 3, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "JEDEC Matrix Tray Elevator", Value = "Tray" },
                        new() { Label = "Vibratory Stick / Tube Feeder", Value = "Stick" },
                        new() { Label = "Combined Tray & Bowl Feeder", Value = "Tray + Bowl" }
                    }},
                    new() { Key = "lead_clinch_enabled", Label = "Under-Board Lead Clinching", FieldType = "boolean", DefaultValue = false, Section = "Tooling", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "max_insertion_force_n", 100.0 },
                    { "max_component_weight_g", 150.0 },
                    { "feeder_type", "Tray + Bowl" },
                    { "lead_clinch_enabled", false }
                }),

            // 5. 10-Zone Nitrogen Reflow Oven
            BuildTemplate(
                "10000000-0000-0000-0000-000000000005",
                GenericTemplateTypes.Machine,
                "MCH-SMT-REFLOW-01",
                "10-Zone Nitrogen Convection Reflow Oven",
                "High-stability forced convection reflow oven with 10 top/bottom heating zones, 3 dual-circuit cooling zones, and closed-loop nitrogen O2 residual regulation.",
                "Soldering",
                "Flame",
                new List<string> { "SMT", "Reflow", "Nitrogen", "Rehm", "Heller", "Thermal" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "heating_zones_count", Label = "Heating Zones", FieldType = "number", Required = true, DefaultValue = 10, Unit = "zones", Min = 6, Max = 16, Section = "Oven Geometry", Order = 1 },
                    new() { Key = "nitrogen_o2_ppm_limit", Label = "Maximum Oxygen Residual", FieldType = "number", Required = true, DefaultValue = 500.0, Unit = "ppm", Min = 50.0, Max = 2000.0, Section = "Atmosphere", Order = 2 },
                    new() { Key = "peak_reflow_temp_c", Label = "Target Peak Temperature", FieldType = "number", Required = true, DefaultValue = 245.0, Unit = "°C", Min = 180.0, Max = 320.0, Section = "Thermal Profile", Order = 3 },
                    new() { Key = "conveyor_speed_cm_min", Label = "Conveyor Belt Speed", FieldType = "number", DefaultValue = 95.0, Unit = "cm/min", Min = 30.0, Max = 250.0, Section = "Transport", Order = 4 },
                    new() { Key = "center_board_support", Label = "Center Board Sag Support Rail", FieldType = "boolean", DefaultValue = true, Section = "Transport", Order = 5 }
                },
                new Dictionary<string, object?>
                {
                    { "heating_zones_count", 10 },
                    { "nitrogen_o2_ppm_limit", 500.0 },
                    { "peak_reflow_temp_c", 245.0 },
                    { "conveyor_speed_cm_min", 95.0 },
                    { "center_board_support", true }
                }),

            // 6. Post-Reflow 3D Automated Optical Inspection (AOI)
            BuildTemplate(
                "10000000-0000-0000-0000-000000000006",
                GenericTemplateTypes.Machine,
                "MCH-AOI-3D-01",
                "Post-Reflow 3D Automated Optical Inspection",
                "High-resolution 3D AOI inspection cell with multi-directional phase-shift fringe projection for solder fillet meniscus inspection and component coplanarity.",
                "Inspection",
                "Eye",
                new List<string> { "SMT", "AOI", "3D", "Quality", "Inspection", "SolderDefects" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "optical_resolution_um", Label = "Camera Resolution", FieldType = "select", Required = true, DefaultValue = "10um", Section = "Optics", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "15 µm (Standard SMT)", Value = "15um" },
                        new() { Label = "10 µm (Fine-Pitch & 0201)", Value = "10um" },
                        new() { Label = "7 µm (High-Density Auto ECU)", Value = "7um" }
                    }},
                    new() { Key = "tombstone_detection", Label = "Tombstone & Lifted Lead Detection", FieldType = "boolean", DefaultValue = true, Section = "Defect Classification", Order = 2 },
                    new() { Key = "polarity_verification", Label = "Component Polarity / Mark Verification", FieldType = "boolean", DefaultValue = true, Section = "Defect Classification", Order = 3 },
                    new() { Key = "max_inspection_speed_cm2_s", Label = "Inspection Area Speed", FieldType = "number", DefaultValue = 45.0, Unit = "cm²/s", Min = 10.0, Max = 120.0, Section = "Performance", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "optical_resolution_um", "10um" },
                    { "tombstone_detection", true },
                    { "polarity_verification", true },
                    { "max_inspection_speed_cm2_s", 45.0 }
                }),

            // 7. 3D Automated X-Ray Inspection (AXI)
            BuildTemplate(
                "10000000-0000-0000-0000-000000000007",
                GenericTemplateTypes.Machine,
                "MCH-AXI-3D-01",
                "In-Line 3D Automated Computed Tomography X-Ray",
                "High-voltage microfocus X-ray inspection system with digital planar computed laminography for hidden BGA void inspection and QFN thermal pad wetting.",
                "Inspection",
                "Eye",
                new List<string> { "AXI", "XRay", "BGA", "Voids", "IPC-A-610", "Quality" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "xray_voltage_kv", Label = "Tube Acceleration Voltage", FieldType = "number", Required = true, DefaultValue = 130.0, Unit = "kV", Min = 60.0, Max = 160.0, Section = "X-Ray Source", Order = 1 },
                    new() { Key = "max_bga_void_pct_limit", Label = "Maximum Allowed BGA Voiding (IPC Class 3)", FieldType = "number", Required = true, DefaultValue = 25.0, Unit = "%", Min = 10.0, Max = 35.0, Section = "Acceptance Criteria", Order = 2 },
                    new() { Key = "hip_head_in_pillow_check", Label = "Head-in-Pillow (HiP) Defect Detection", FieldType = "boolean", DefaultValue = true, Section = "Acceptance Criteria", Order = 3 },
                    new() { Key = "radiation_interlock_safe", Label = "Radiation Cabinet Interlock Class 1", FieldType = "boolean", DefaultValue = true, Section = "Safety", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "xray_voltage_kv", 130.0 },
                    { "max_bga_void_pct_limit", 25.0 },
                    { "hip_head_in_pillow_check", true },
                    { "radiation_interlock_safe", true }
                }),

            // 8. Robotic Selective Soldering System
            BuildTemplate(
                "10000000-0000-0000-0000-000000000008",
                GenericTemplateTypes.Machine,
                "MCH-SLD-SELECT-01",
                "Automated Robotic Selective Soldering System",
                "Multi-axis selective mini-wave soldering machine with drop-jet micro-fluxer, quartz bottom preheaters, and nitrogen shrouded titanium mini-pot.",
                "Soldering",
                "Flame",
                new List<string> { "SelectiveSolder", "THT", "Nitrogen", "Ersa", "Nordson" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "solder_pot_temp_c", Label = "Solder Pot Temperature", FieldType = "number", Required = true, DefaultValue = 295.0, Unit = "°C", Min = 240.0, Max = 380.0, Section = "Process", Order = 1 },
                    new() { Key = "nozzle_diameter_mm", Label = "Solder Wave Nozzle Diameter", FieldType = "select", Required = true, DefaultValue = "4.0mm", Section = "Tooling", Order = 2, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "2.5 mm (High Density)", Value = "2.5mm" },
                        new() { Label = "4.0 mm (Standard)", Value = "4.0mm" },
                        new() { Label = "8.0 mm (Heavy Power Pin)", Value = "8.0mm" }
                    }},
                    new() { Key = "dropjet_flux_volume_nl", Label = "Drop-Jet Flux Shot Volume", FieldType = "number", DefaultValue = 12.0, Unit = "nL", Min = 2.0, Max = 50.0, Section = "Fluxing", Order = 3 },
                    new() { Key = "nitrogen_cover_flow_lpm", Label = "Nitrogen Blanket Flow Rate", FieldType = "number", DefaultValue = 35.0, Unit = "L/min", Min = 10.0, Max = 80.0, Section = "Atmosphere", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "solder_pot_temp_c", 295.0 },
                    { "nozzle_diameter_mm", "4.0mm" },
                    { "dropjet_flux_volume_nl", 12.0 },
                    { "nitrogen_cover_flow_lpm", 35.0 }
                }),

            // 9. Nitrogen Wave Soldering Machine
            BuildTemplate(
                "10000000-0000-0000-0000-000000000009",
                GenericTemplateTypes.Machine,
                "MCH-SLD-WAVE-01",
                "Nitrogen Tunnel Full-Wave Soldering Machine",
                "High-volume wave soldering plant for power electronics and automotive THT connectors with chip and lambda wave, convection preheaters, and spray fluxer.",
                "Soldering",
                "Flame",
                new List<string> { "WaveSolder", "THT", "HighVolume", "AutomotivePower" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "solder_bath_capacity_kg", Label = "Bath Solder Capacity", FieldType = "number", DefaultValue = 450, Unit = "kg", Min = 200, Max = 900, Section = "Pot", Order = 1 },
                    new() { Key = "wave_height_mm", Label = "Main Wave Crest Height", FieldType = "number", DefaultValue = 12.5, Unit = "mm", Min = 5.0, Max = 25.0, Section = "Wave Profile", Order = 2 },
                    new() { Key = "conveyor_angle_deg", Label = "Conveyor Incline Angle", FieldType = "number", DefaultValue = 7.0, Unit = "deg", Min = 4.0, Max = 9.0, Section = "Transport", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "solder_bath_capacity_kg", 450 },
                    { "wave_height_mm", 12.5 },
                    { "conveyor_angle_deg", 7.0 }
                }),

            // 10. CNC PCB Depaneling Router
            BuildTemplate(
                "10000000-0000-0000-0000-000000000010",
                GenericTemplateTypes.Machine,
                "MCH-PCB-ROUTER-01",
                "In-Line High-Speed CNC PCB Depaneling Router",
                "Automated dual-table PCB singulation router equipped with a 60,000 RPM electric spindle, downward dust vacuum extractor, and optical bit-break detection.",
                "Depaneling",
                "Factory",
                new List<string> { "Depaneling", "Router", "Singulation", "PCBA", "Spindle" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "spindle_speed_rpm", Label = "Spindle Milling Velocity", FieldType = "number", Required = true, DefaultValue = 55000, Unit = "RPM", Min = 20000, Max = 80000, Section = "Spindle", Order = 1 },
                    new() { Key = "milling_bit_diameter_mm", Label = "Router Bit Diameter", FieldType = "select", Required = true, DefaultValue = "1.5mm", Section = "Tooling", Order = 2, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "1.0 mm (Tight Clearance)", Value = "1.0mm" },
                        new() { Label = "1.5 mm (Standard Diamond Cut)", Value = "1.5mm" },
                        new() { Label = "2.0 mm (Heavy Panel Tab)", Value = "2.0mm" }
                    }},
                    new() { Key = "feed_rate_mm_s", Label = "Cutting Feed Rate", FieldType = "number", DefaultValue = 35.0, Unit = "mm/s", Min = 5.0, Max = 80.0, Section = "Dynamics", Order = 3 },
                    new() { Key = "vacuum_pressure_kpa", Label = "Dust Extraction Vacuum", FieldType = "number", DefaultValue = -18.5, Unit = "kPa", Min = -30.0, Max = -5.0, Section = "Extraction", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "spindle_speed_rpm", 55000 },
                    { "milling_bit_diameter_mm", "1.5mm" },
                    { "feed_rate_mm_s", 35.0 },
                    { "vacuum_pressure_kpa", -18.5 }
                }),

            // 11. UV Laser PCB Singulation Cell
            BuildTemplate(
                "10000000-0000-0000-0000-000000000011",
                GenericTemplateTypes.Machine,
                "MCH-PCB-LASER-01",
                "Ultra-Short Pulse UV Laser PCB Singulation Cell",
                "Zero-mechanical-stress 355nm UV laser depaneling cell for delicate ceramic substrate, flex-rigid circuit singulation, and sensor PCBA separation.",
                "Depaneling",
                "Factory",
                new List<string> { "Laser", "Depaneling", "UV", "Singulation", "FlexPCB" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "laser_wavelength_nm", Label = "Laser Wavelength", FieldType = "number", DefaultValue = 355, Unit = "nm", Section = "Laser", Order = 1 },
                    new() { Key = "laser_power_watts", Label = "Output Optical Power", FieldType = "number", Required = true, DefaultValue = 18.0, Unit = "W", Min = 5.0, Max = 40.0, Section = "Laser", Order = 2 },
                    new() { Key = "cutting_kerf_width_um", Label = "Cut Kerf Width", FieldType = "number", DefaultValue = 20.0, Unit = "µm", Section = "Quality", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "laser_wavelength_nm", 355 },
                    { "laser_power_watts", 18.0 },
                    { "cutting_kerf_width_um", 20.0 }
                }),

            // 12. Selective Conformal Coating Machine
            BuildTemplate(
                "10000000-0000-0000-0000-000000000012",
                GenericTemplateTypes.Machine,
                "MCH-DISP-COAT-01",
                "Automated Selective Conformal Coating System",
                "Multi-axis selective conformal coating robot with non-atomized film coater, swirl spray valve, and needle dispenser for harsh automotive environment protection.",
                "Dispensing",
                "Factory",
                new List<string> { "ConformalCoating", "Dispensing", "Nordson", "PVA", "MoistureProtection" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "coating_material", Label = "Coating Material Type", FieldType = "select", Required = true, DefaultValue = "UV-Cure Acrylate", Section = "Material", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "UV-Cure Acrylate (HumiSeal UV40)", Value = "UV-Cure Acrylate" },
                        new() { Label = "Solvent Polyurethane (PUR)", Value = "Polyurethane" },
                        new() { Label = "Silicone Elastic Gel", Value = "Silicone" }
                    }},
                    new() { Key = "film_width_mm", Label = "Film Applicator Fan Width", FieldType = "number", DefaultValue = 12.0, Unit = "mm", Min = 3.0, Max = 25.0, Section = "Nozzle", Order = 2 },
                    new() { Key = "tank_pressure_bar", Label = "Material Reservoir Pressure", FieldType = "number", DefaultValue = 2.2, Unit = "bar", Min = 0.5, Max = 6.0, Section = "Pneumatics", Order = 3 },
                    new() { Key = "uv_inspection_light", Label = "Integrated Blacklight (UV) Inspection", FieldType = "boolean", DefaultValue = true, Section = "Quality", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "coating_material", "UV-Cure Acrylate" },
                    { "film_width_mm", 12.0 },
                    { "tank_pressure_bar", 2.2 },
                    { "uv_inspection_light", true }
                }),

            // 13. 2K Thermal Gap Filler Dispenser
            BuildTemplate(
                "10000000-0000-0000-0000-000000000013",
                GenericTemplateTypes.Machine,
                "MCH-DISP-GAP-01",
                "2-Component High-Viscosity Thermal Gap Filler Dispenser",
                "Automated progressive cavity dispensing machine for abrasive thermal interface materials (TIM), potting heatsink gap fillers in automotive inverters and ECUs.",
                "Dispensing",
                "Factory",
                new List<string> { "TIM", "GapFiller", "Thermal", "2KDispense", "Inverter", "ECU" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "mixing_ratio_a_b", Label = "Volumetric Mix Ratio (A:B)", FieldType = "string", Required = true, DefaultValue = "1:1", Section = "Dispensing", Order = 1 },
                    new() { Key = "dispense_rate_ml_s", Label = "Dispense Delivery Rate", FieldType = "number", Required = true, DefaultValue = 3.5, Unit = "mL/s", Min = 0.2, Max = 15.0, Section = "Dispensing", Order = 2 },
                    new() { Key = "feed_drum_pressure_bar", Label = "Drum Ram Pressure", FieldType = "number", DefaultValue = 4.5, Unit = "bar", Min = 1.0, Max = 8.0, Section = "Supply", Order = 3 },
                    new() { Key = "scale_calibration_check", Label = "Automated Tare Gravimetric Calibration", FieldType = "boolean", DefaultValue = true, Section = "Quality", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "mixing_ratio_a_b", "1:1" },
                    { "dispense_rate_ml_s", 3.5 },
                    { "feed_drum_pressure_bar", 4.5 },
                    { "scale_calibration_check", true }
                }),

            // 14. Vacuum Potting & Encapsulation Machine
            BuildTemplate(
                "10000000-0000-0000-0000-000000000014",
                GenericTemplateTypes.Machine,
                "MCH-DISP-POT-01",
                "Automated Vacuum Resin Potting & Encapsulation Station",
                "Degassing vacuum chamber resin encapsulation cell for moisture-proof sealing of automotive sensors, radar units, and high-voltage junction boxes.",
                "Dispensing",
                "Factory",
                new List<string> { "VacuumPotting", "Epoxy", "Encapsulation", "Radar", "Sensors" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "vacuum_level_mbar", Label = "Chamber Vacuum Level", FieldType = "number", Required = true, DefaultValue = 5.0, Unit = "mbar", Min = 0.5, Max = 50.0, Section = "Vacuum", Order = 1 },
                    new() { Key = "chamber_dwell_time_s", Label = "Vacuum Dwell Time", FieldType = "number", DefaultValue = 45, Unit = "s", Min = 10, Max = 300, Section = "Cycle", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "vacuum_level_mbar", 5.0 },
                    { "chamber_dwell_time_s", 45 }
                }),

            // 15. Compliant Pin Servo Press-Fit Machine
            BuildTemplate(
                "10000000-0000-0000-0000-000000000015",
                GenericTemplateTypes.Machine,
                "MCH-PRESS-FIT-01",
                "Automated Servo-Electric Compliant Pin Press-Fit Machine",
                "Precision servo press station for solder-free compliant pin (eye-of-the-needle) automotive connector insertion with real-time force vs displacement curve validation.",
                "Assembly",
                "Factory",
                new List<string> { "PressFit", "CompliantPin", "ServoPress", "Connector", "ForceDistance" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "max_pressing_force_kn", Label = "Nominal Ram Force Rating", FieldType = "number", Required = true, DefaultValue = 35.0, Unit = "kN", Min = 5.0, Max = 100.0, Section = "Mechanical", Order = 1 },
                    new() { Key = "insertion_speed_mm_s", Label = "Press Velocity", FieldType = "number", Required = true, DefaultValue = 5.0, Unit = "mm/s", Min = 0.5, Max = 25.0, Section = "Dynamics", Order = 2 },
                    new() { Key = "curve_envelope_tolerance_pct", Label = "Force-Distance Corridor Tolerance", FieldType = "number", DefaultValue = 8.0, Unit = "%", Min = 2.0, Max = 20.0, Section = "Quality", Order = 3 },
                    new() { Key = "laser_bent_pin_check", Label = "Pre-Press Bent Pin Laser Verification", FieldType = "boolean", DefaultValue = true, Section = "Pre-Inspection", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "max_pressing_force_kn", 35.0 },
                    { "insertion_speed_mm_s", 5.0 },
                    { "curve_envelope_tolerance_pct", 8.0 },
                    { "laser_bent_pin_check", true }
                }),

            // 16. Robotic Multi-Spindle Automated Screwing Station
            BuildTemplate(
                "10000000-0000-0000-0000-000000000016",
                GenericTemplateTypes.Machine,
                "MCH-SCREW-AUTO-01",
                "Robotic Multi-Spindle Automated Screwing Station",
                "Multi-axis automated fastening station with torque-angle closed loop servo screwdrivers, automatic blow-feeding, and bit wear life cycle tracking.",
                "Fastening",
                "Wrench",
                new List<string> { "Screwing", "Fastening", "Torque", "AtlasCopco", "Desoutter", "ECU" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "target_torque_nm", Label = "Target Tightening Torque", FieldType = "number", Required = true, DefaultValue = 3.8, Unit = "Nm", Min = 0.5, Max = 25.0, Section = "Tightening", Order = 1 },
                    new() { Key = "torque_tolerance_pct", Label = "Torque Window Tolerance", FieldType = "number", Required = true, DefaultValue = 5.0, Unit = "%", Min = 1.0, Max = 15.0, Section = "Tightening", Order = 2 },
                    new() { Key = "target_angle_deg", Label = "Target Angle Monitoring", FieldType = "number", DefaultValue = 180.0, Unit = "deg", Min = 30.0, Max = 1080.0, Section = "Tightening", Order = 3 },
                    new() { Key = "spindle_count", Label = "Active Spindles", FieldType = "number", DefaultValue = 4, Unit = "spindles", Min = 1, Max = 16, Section = "Tooling", Order = 4 },
                    new() { Key = "screw_blow_feed_verify", Label = "Pneumatic Screw Infeed Optical Check", FieldType = "boolean", DefaultValue = true, Section = "Feeder", Order = 5 }
                },
                new Dictionary<string, object?>
                {
                    { "target_torque_nm", 3.8 },
                    { "torque_tolerance_pct", 5.0 },
                    { "target_angle_deg", 180.0 },
                    { "spindle_count", 4 },
                    { "screw_blow_feed_verify", true }
                }),

            // 17. In-Circuit Test (ICT) Station
            BuildTemplate(
                "10000000-0000-0000-0000-000000000017",
                GenericTemplateTypes.Machine,
                "MCH-TEST-ICT-01",
                "In-Line Bed-of-Nails In-Circuit Test (ICT) Station",
                "Pneumatic fixture bed-of-nails ICT test station with 2048 test channels, open/short matrix testing, analog in-circuit measurement, and IEEE 1149.1 boundary scan.",
                "Testing",
                "Activity",
                new List<string> { "ICT", "BedOfNails", "Testing", "Keysight", "Teradyne", "PCBA" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "channel_count", Label = "Available Test Channels", FieldType = "number", Required = true, DefaultValue = 1024, Unit = "pins", Min = 128, Max = 4096, Section = "Architecture", Order = 1 },
                    new() { Key = "boundary_scan_enabled", Label = "JTAG / Boundary Scan Test Active", FieldType = "boolean", DefaultValue = true, Section = "Digital Test", Order = 2 },
                    new() { Key = "fixture_actuation_type", Label = "Fixture Actuation Mechanism", FieldType = "select", DefaultValue = "Pneumatic Dual-Stage", Section = "Fixture", Order = 3, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "Pneumatic Dual-Stage (ICT + FCT)", Value = "Pneumatic Dual-Stage" },
                        new() { Label = "Vacuum Draw-Down", Value = "Vacuum" },
                        new() { Label = "Servo Motor Press", Value = "Servo Motor" }
                    }}
                },
                new Dictionary<string, object?>
                {
                    { "channel_count", 1024 },
                    { "boundary_scan_enabled", true },
                    { "fixture_actuation_type", "Pneumatic Dual-Stage" }
                }),

            // 18. 400V/800V High-Voltage Dielectric Withstand Tester
            BuildTemplate(
                "10000000-0000-0000-0000-000000000018",
                GenericTemplateTypes.Machine,
                "MCH-TEST-HIPOT-01",
                "High-Voltage Dielectric Withstand & Insulation Tester",
                "Automotive EV high-voltage insulation resistance, dielectric withstand (Hi-Pot up to 5000V DC/AC), and galvanic isolation tester for inverters and battery packs.",
                "Testing",
                "ShieldAlert",
                new List<string> { "HiPot", "HighVoltage", "Insulation", "Safety", "EV", "Inverter" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "test_voltage_vdc", Label = "Test Voltage (DC)", FieldType = "number", Required = true, DefaultValue = 2500, Unit = "V", Min = 500, Max = 6000, Section = "HV Parameters", Order = 1 },
                    new() { Key = "max_leakage_current_ma", Label = "Max Allowed Leakage Current", FieldType = "number", Required = true, DefaultValue = 2.0, Unit = "mA", Min = 0.1, Max = 20.0, Section = "HV Parameters", Order = 2 },
                    new() { Key = "min_insulation_mohm", Label = "Minimum Insulation Resistance", FieldType = "number", Required = true, DefaultValue = 100.0, Unit = "MΩ", Min = 10.0, Max = 10000.0, Section = "HV Parameters", Order = 3 },
                    new() { Key = "safety_light_curtain_active", Label = "Class 1 High-Voltage Enclosure Safety Interlock", FieldType = "boolean", DefaultValue = true, Section = "Safety", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "test_voltage_vdc", 2500 },
                    { "max_leakage_current_ma", 2.0 },
                    { "min_insulation_mohm", 100.0 },
                    { "safety_light_curtain_active", true }
                }),

            // 19. Automotive ECU End-of-Line (EOL) Tester
            BuildTemplate(
                "10000000-0000-0000-0000-000000000019",
                GenericTemplateTypes.Machine,
                "MCH-TEST-EOL-01",
                "Automotive ECU Functional End-of-Line (EOL) Tester",
                "Comprehensive final functional tester equipped with CAN-FD, LIN, Automotive Ethernet (100BASE-T1), high-speed firmware bootloader flashing, and DTC validation.",
                "Testing",
                "Activity",
                new List<string> { "EOL", "CAN-FD", "Ethernet", "Flashing", "DTC", "ECU", "FunctionalTest" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "vehicle_bus_type", Label = "Primary Vehicle Bus Interface", FieldType = "select", Required = true, DefaultValue = "CAN-FD", Section = "Communications", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "CAN-FD (ISO 11898-1)", Value = "CAN-FD" },
                        new() { Label = "Automotive Ethernet (100BASE-T1 / SOME/IP)", Value = "Automotive Ethernet" },
                        new() { Label = "LIN Bus (ISO 17987)", Value = "LIN" },
                        new() { Label = "FlexRay (ISO 17458)", Value = "FlexRay" }
                    }},
                    new() { Key = "firmware_flashing_enabled", Label = "In-Fixture High-Speed Firmware Flashing", FieldType = "boolean", DefaultValue = true, Section = "Programming", Order = 2 },
                    new() { Key = "power_rail_voltage_v", Label = "Supply Rail Voltage (KL30)", FieldType = "number", DefaultValue = 13.8, Unit = "V", Min = 9.0, Max = 48.0, Section = "Power", Order = 3 },
                    new() { Key = "dtc_readout_verify", Label = "Diagnostic Trouble Code (DTC) Clearing Check", FieldType = "boolean", DefaultValue = true, Section = "Diagnostics", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "vehicle_bus_type", "CAN-FD" },
                    { "firmware_flashing_enabled", true },
                    { "power_rail_voltage_v", 13.8 },
                    { "dtc_readout_verify", true }
                }),

            // 20. Laser Marking & Traceability Cell
            BuildTemplate(
                "10000000-0000-0000-0000-000000000020",
                GenericTemplateTypes.Machine,
                "MCH-MARK-TRACE-01",
                "In-Line Fiber Laser Marking & 2D DataMatrix Cell",
                "Automated laser engraving station applying ISO/IEC 16022 2D DataMatrix serial codes directly onto PCB solder mask and aluminum housings with inline grading.",
                "SMT",
                "Factory",
                new List<string> { "LaserMarking", "Traceability", "DataMatrix", "Keyence", "Trumpf" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "laser_type", Label = "Laser Source", FieldType = "select", DefaultValue = "Pulsed Fiber 20W", Section = "Source", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "Pulsed Fiber 20W (Metal & PCB)", Value = "Pulsed Fiber 20W" },
                        new() { Label = "UV Laser 5W (Cold Marking Plastic)", Value = "UV Laser 5W" },
                        new() { Label = "CO2 Laser 30W (Organic/FR4)", Value = "CO2 Laser 30W" }
                    }},
                    new() { Key = "min_datamatrix_grade", Label = "Minimum ISO 15415 Quality Grade", FieldType = "select", DefaultValue = "B", Section = "Quality", Order = 2, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "Grade A (4.0)", Value = "A" },
                        new() { Label = "Grade B (3.0)", Value = "B" },
                        new() { Label = "Grade C (2.0)", Value = "C" }
                    }}
                },
                new Dictionary<string, object?>
                {
                    { "laser_type", "Pulsed Fiber 20W" },
                    { "min_datamatrix_grade", "B" }
                }),

            // 21. Ultrasonic Housing Welder
            BuildTemplate(
                "10000000-0000-0000-0000-000000000021",
                GenericTemplateTypes.Machine,
                "MCH-WELD-PLAST-01",
                "Ultrasonic Plastic Housing Welding & Staking Machine",
                "20 kHz servo-driven ultrasonic welding station for sealing hermetic automotive plastic housings and staking connector pins with energy/collapse depth triggers.",
                "Assembly",
                "Factory",
                new List<string> { "Ultrasonic", "Welding", "Branson", "Herrmann", "Plastics" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "welding_frequency_khz", Label = "Ultrasonic Frequency", FieldType = "number", DefaultValue = 20, Unit = "kHz", Section = "Acoustics", Order = 1 },
                    new() { Key = "weld_mode", Label = "Primary Control Mode", FieldType = "select", DefaultValue = "Energy Mode", Section = "Process", Order = 2, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "Energy Mode (Joules)", Value = "Energy Mode" },
                        new() { Label = "Collapse Depth (mm)", Value = "Collapse Depth" },
                        new() { Label = "Peak Power (Watts)", Value = "Peak Power" }
                    }},
                    new() { Key = "energy_target_joules", Label = "Target Weld Energy", FieldType = "number", DefaultValue = 450, Unit = "J", Min = 50, Max = 2500, Section = "Process", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "welding_frequency_khz", 20 },
                    { "weld_mode", "Energy Mode" },
                    { "energy_target_joules", 450 }
                }),

            // 22. Differential Pressure Decay Leak Tester
            BuildTemplate(
                "10000000-0000-0000-0000-000000000022",
                GenericTemplateTypes.Machine,
                "MCH-LEAK-TEST-01",
                "Differential Pressure Decay & Vacuum Leak Tester",
                "High-precision sealing integrity validation machine ensuring IP67 and IP69K waterproof ratings for automotive electronic enclosures and battery modules.",
                "Testing",
                "Activity",
                new List<string> { "LeakTest", "PressureDecay", "IP67", "IP69K", "ATEQ", "Sealing" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "test_pressure_kpa", Label = "Chamber Test Pressure", FieldType = "number", Required = true, DefaultValue = 30.0, Unit = "kPa", Min = 5.0, Max = 250.0, Section = "Pressure", Order = 1 },
                    new() { Key = "max_leak_rate_pa_s", Label = "Maximum Allowed Decay Rate", FieldType = "number", Required = true, DefaultValue = 4.5, Unit = "Pa/s", Min = 0.5, Max = 50.0, Section = "Acceptance", Order = 2 },
                    new() { Key = "settling_time_s", Label = "Thermal Equalization Settling Time", FieldType = "number", DefaultValue = 6.0, Unit = "s", Min = 1.0, Max = 30.0, Section = "Timing", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "test_pressure_kpa", 30.0 },
                    { "max_leak_rate_pa_s", 4.5 },
                    { "settling_time_s", 6.0 }
                }),

            // 23. 6-Axis Robotic Handling Cell
            BuildTemplate(
                "10000000-0000-0000-0000-000000000023",
                GenericTemplateTypes.Machine,
                "MCH-ROBOT-CELL-01",
                "6-Axis Articulated Robotic Assembly & Handling Cell",
                "Flexible articulated robotic handling cell with 6 degrees of freedom, vision-guided part pickup, dual tool-changer, and integrated safety light curtains.",
                "Robotics",
                "Cpu",
                new List<string> { "Robotics", "KUKA", "ABB", "Fanuc", "Handling", "6Axis" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "robot_kinematics", Label = "Manipulator Kinematics", FieldType = "select", Required = true, DefaultValue = "6-Axis Articulated", Section = "Robot", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "6-Axis Articulated Industrial", Value = "6-Axis Articulated" },
                        new() { Label = "4-Axis High-Speed SCARA", Value = "4-Axis SCARA" },
                        new() { Label = "Collaborative Robot (Cobot)", Value = "Collaborative Robot" }
                    }},
                    new() { Key = "payload_capacity_kg", Label = "Rated Payload", FieldType = "number", Required = true, DefaultValue = 10.0, Unit = "kg", Min = 1.0, Max = 60.0, Section = "Robot", Order = 2 },
                    new() { Key = "reach_radius_mm", Label = "Maximum Working Reach", FieldType = "number", DefaultValue = 1100, Unit = "mm", Min = 400, Max = 2500, Section = "Robot", Order = 3 },
                    new() { Key = "safety_stop_category", Label = "Safety Stop Classification", FieldType = "select", DefaultValue = "Cat 4 / PLe", Section = "Safety", Order = 4, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "Category 4 / PLe (Dual Channel)", Value = "Cat 4 / PLe" },
                        new() { Label = "Category 3 / PLd", Value = "Cat 3 / PLd" }
                    }}
                },
                new Dictionary<string, object?>
                {
                    { "robot_kinematics", "6-Axis Articulated" },
                    { "payload_capacity_kg", 10.0 },
                    { "reach_radius_mm", 1100 },
                    { "safety_stop_category", "Cat 4 / PLe" }
                }),

            // 24. SMEMA / Hermes Board Conveyor & Buffer
            BuildTemplate(
                "10000000-0000-0000-0000-000000000024",
                GenericTemplateTypes.Machine,
                "MCH-CONV-SMEMA-01",
                "SMEMA / Hermes PCB Conveyor & FIFO Buffer Tower",
                "Anti-static ESD belt PCB transport conveyor with motorized rail width adjustment, optical board arrival sensors, and vertical FIFO/LIFO 20-slot buffer rack.",
                "Assembly",
                "Layers",
                new List<string> { "Conveyor", "SMEMA", "Hermes", "Buffer", "Transport", "ESD" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "conveyor_speed_m_min", Label = "Belt Transport Speed", FieldType = "number", DefaultValue = 15.0, Unit = "m/min", Min = 2.0, Max = 40.0, Section = "Transport", Order = 1 },
                    new() { Key = "buffer_capacity_slots", Label = "Buffer Tower Slots", FieldType = "number", DefaultValue = 20, Unit = "slots", Min = 5, Max = 60, Section = "Buffer", Order = 2 },
                    new() { Key = "width_motorized_adjust", Label = "Automatic Recipe Rail Width Adjustment", FieldType = "boolean", DefaultValue = true, Section = "Adjustment", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "conveyor_speed_m_min", 15.0 },
                    { "buffer_capacity_slots", 20 },
                    { "width_motorized_adjust", true }
                })
        };
    }

    public static IReadOnlyList<GenericTemplate> GetComponentTemplates()
    {
        return new List<GenericTemplate>
        {
            // 1. High-Speed Dynamic Servo Axis
            BuildTemplate(
                "20000000-0000-0000-0000-000000000001",
                GenericTemplateTypes.Component,
                "CMP-SERVO-01",
                "High-Speed Dynamic Servo Axis",
                "Standard industrial multi-turn absolute encoder servo actuator for positioning gantry and pick-and-place arms.",
                "Motion",
                "Cpu",
                new List<string> { "Servo", "Motion", "Beckhoff", "EtherCAT" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "axis_code", Label = "Axis Identifier", FieldType = "string", Required = true, DefaultValue = "AXIS-X1", Section = "Identity", Order = 1 },
                    new() { Key = "rated_torque_nm", Label = "Rated Torque", FieldType = "number", Required = true, DefaultValue = 12.5, Unit = "Nm", Min = 0.5, Max = 250.0, Section = "Specifications", Order = 2 },
                    new() { Key = "max_rpm", Label = "Maximum Velocity", FieldType = "number", Required = true, DefaultValue = 3000, Unit = "RPM", Min = 100, Max = 12000, Section = "Specifications", Order = 3 },
                    new() { Key = "bus_protocol", Label = "Fieldbus Protocol", FieldType = "select", Required = true, DefaultValue = "EtherCAT", Section = "Connectivity", Order = 4, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "EtherCAT (CoE)", Value = "EtherCAT" },
                        new() { Label = "PROFINET IRT", Value = "PROFINET" },
                        new() { Label = "CANopen DS402", Value = "CANopen" }
                    }},
                    new() { Key = "holding_brake", Label = "Mechanical Holding Brake", FieldType = "boolean", DefaultValue = true, Section = "Safety", Order = 5 }
                },
                new Dictionary<string, object?>
                {
                    { "axis_code", "AXIS-X1" },
                    { "rated_torque_nm", 12.5 },
                    { "max_rpm", 3000 },
                    { "bus_protocol", "EtherCAT" },
                    { "holding_brake", true }
                }),

            // 2. Closed-Loop Stepper Motor & Drive
            BuildTemplate(
                "20000000-0000-0000-0000-000000000002",
                GenericTemplateTypes.Component,
                "CMP-STEPPER-01",
                "Closed-Loop Stepper Motor & Drive Module",
                "NEMA 23/34 integrated stepper motor with optical encoder feedback, eliminating stall errors on conveyor guides and feeder tracks.",
                "Motion",
                "Cpu",
                new List<string> { "Stepper", "Motion", "NEMA", "Actuator" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "frame_size", Label = "NEMA Frame Size", FieldType = "select", DefaultValue = "NEMA 23", Section = "Physical", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "NEMA 17", Value = "NEMA 17" },
                        new() { Label = "NEMA 23", Value = "NEMA 23" },
                        new() { Label = "NEMA 34", Value = "NEMA 34" }
                    }},
                    new() { Key = "holding_torque_nm", Label = "Holding Torque", FieldType = "number", DefaultValue = 2.8, Unit = "Nm", Min = 0.2, Max = 15.0, Section = "Power", Order = 2 },
                    new() { Key = "microstepping_resolution", Label = "Microstepping Steps/Rev", FieldType = "number", DefaultValue = 3200, Section = "Drive", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "frame_size", "NEMA 23" },
                    { "holding_torque_nm", 2.8 },
                    { "microstepping_resolution", 3200 }
                }),

            // 3. Precision Ball-Screw Linear Actuator Slide
            BuildTemplate(
                "20000000-0000-0000-0000-000000000003",
                GenericTemplateTypes.Component,
                "CMP-LINEAR-01",
                "Precision Ball-Screw Linear Actuator Slide",
                "Rigid aluminum extrusion linear axis with ground ball screw drive, recirculating linear guides, and inductive end limit switches.",
                "Motion",
                "Cpu",
                new List<string> { "LinearActuator", "BallScrew", "THK", "Festo", "Mechanics" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "stroke_length_mm", Label = "Effective Stroke Length", FieldType = "number", Required = true, DefaultValue = 500, Unit = "mm", Min = 50, Max = 3000, Section = "Dimensions", Order = 1 },
                    new() { Key = "screw_lead_pitch_mm", Label = "Ball Screw Lead Pitch", FieldType = "number", DefaultValue = 10, Unit = "mm", Min = 2, Max = 40, Section = "Drive", Order = 2 },
                    new() { Key = "repeatability_um", Label = "Repeatable Positioning Accuracy", FieldType = "number", DefaultValue = 5.0, Unit = "µm", Section = "Accuracy", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "stroke_length_mm", 500 },
                    { "screw_lead_pitch_mm", 10 },
                    { "repeatability_um", 5.0 }
                }),

            // 4. ISO Compact Double-Acting Pneumatic Cylinder
            BuildTemplate(
                "20000000-0000-0000-0000-000000000004",
                GenericTemplateTypes.Component,
                "CMP-PNEUM-CYL-01",
                "Compact Double-Acting Pneumatic Cylinder",
                "ISO 21287 standard space-saving aluminum profile cylinder with magnetic piston ring for contactless reed/solid-state position sensors.",
                "Pneumatics",
                "Cpu",
                new List<string> { "Pneumatic", "Cylinder", "SMC", "Festo", "Clamping" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "piston_diameter_mm", Label = "Piston Bore Diameter", FieldType = "number", Required = true, DefaultValue = 32, Unit = "mm", Min = 12, Max = 100, Section = "Pneumatics", Order = 1 },
                    new() { Key = "cylinder_stroke_mm", Label = "Stroke Length", FieldType = "number", Required = true, DefaultValue = 50, Unit = "mm", Min = 5, Max = 500, Section = "Pneumatics", Order = 2 },
                    new() { Key = "theoretical_force_6bar_n", Label = "Theoretical Extension Force at 6 bar", FieldType = "number", DefaultValue = 482, Unit = "N", Section = "Force", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "piston_diameter_mm", 32 },
                    { "cylinder_stroke_mm", 50 },
                    { "theoretical_force_6bar_n", 482 }
                }),

            // 5. 5/2 Pilot Solenoid Valve Manifold
            BuildTemplate(
                "20000000-0000-0000-0000-000000000005",
                GenericTemplateTypes.Component,
                "CMP-PNEUM-VALVE-01",
                "IO-Link Solenoid Directional Valve Terminal",
                "Modular pneumatic valve block supporting 5/2 and 5/3 directional valves with integrated IO-Link communication and digital diagnostics.",
                "Pneumatics",
                "Cpu",
                new List<string> { "Valve", "Pneumatic", "IOLink", "Festo", "SMC", "Terminal" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "valve_slice_count", Label = "Number of Valve Slices", FieldType = "number", DefaultValue = 8, Unit = "slices", Min = 2, Max = 24, Section = "Capacity", Order = 1 },
                    new() { Key = "response_time_ms", Label = "Switching Response Time", FieldType = "number", DefaultValue = 12, Unit = "ms", Min = 2, Max = 50, Section = "Dynamics", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "valve_slice_count", 8 },
                    { "response_time_ms", 12 }
                }),

            // 6. 2-Finger Pneumatic Parallel Gripper
            BuildTemplate(
                "20000000-0000-0000-0000-000000000006",
                GenericTemplateTypes.Component,
                "CMP-GRIP-PARALLEL-01",
                "2-Finger Pneumatic Precision Parallel Gripper",
                "T-slot guided heavy-duty parallel gripper delivering high gripping force and repeat accuracy for robotic part handling and station nests.",
                "Tooling",
                "Wrench",
                new List<string> { "Gripper", "Schunk", "EndEffector", "Robotics", "Tooling" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "stroke_per_finger_mm", Label = "Stroke per Jaw", FieldType = "number", Required = true, DefaultValue = 10.0, Unit = "mm", Min = 2.0, Max = 50.0, Section = "Mechanics", Order = 1 },
                    new() { Key = "gripping_force_n", Label = "Closing Gripping Force", FieldType = "number", Required = true, DefaultValue = 250.0, Unit = "N", Min = 20.0, Max = 2000.0, Section = "Force", Order = 2 },
                    new() { Key = "part_presence_sensor", Label = "Part Presence Optical/Magnetic Interlock", FieldType = "boolean", DefaultValue = true, Section = "Safety", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "stroke_per_finger_mm", 10.0 },
                    { "gripping_force_n", 250.0 },
                    { "part_presence_sensor", true }
                }),

            // 7. Venturi Vacuum Ejector with Blow-Off
            BuildTemplate(
                "20000000-0000-0000-0000-000000000007",
                GenericTemplateTypes.Component,
                "CMP-VACUUM-EJECT-01",
                "Venturi Vacuum Ejector with Fast Blow-Off Pulse",
                "Pneumatic vacuum generator for pick-and-place suction cups with integrated air economizer function and positive blow-off release valve.",
                "Pneumatics",
                "Cpu",
                new List<string> { "Vacuum", "Ejector", "Schmalz", "Festo", "Suction" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "max_vacuum_pct", Label = "Maximum Vacuum Level", FieldType = "number", DefaultValue = 85.0, Unit = "%", Min = 50.0, Max = 95.0, Section = "Vacuum", Order = 1 },
                    new() { Key = "suction_rate_lpm", Label = "Suction Flow Rate", FieldType = "number", DefaultValue = 42.0, Unit = "L/min", Min = 10.0, Max = 150.0, Section = "Flow", Order = 2 },
                    new() { Key = "blow_off_pulse_ms", Label = "Blow-off Pulse Duration", FieldType = "number", DefaultValue = 80, Unit = "ms", Min = 10, Max = 500, Section = "Timing", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "max_vacuum_pct", 85.0 },
                    { "suction_rate_lpm", 42.0 },
                    { "blow_off_pulse_ms", 80 }
                }),

            // 8. Electric Torque-Controlled Tightening Spindle
            BuildTemplate(
                "20000000-0000-0000-0000-000000000008",
                GenericTemplateTypes.Component,
                "CMP-SCREW-SPINDLE-01",
                "Electric Torque-Controlled Tightening Spindle",
                "Brushless DC servo tightening spindle with redundant transducer torque/angle measurement for safety-critical VDI/VDE 2862 Class A joints.",
                "Tooling",
                "Wrench",
                new List<string> { "Spindle", "Screwdriver", "AtlasCopco", "Desoutter", "VDI2862" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "max_torque_nm", Label = "Spindle Maximum Torque", FieldType = "number", Required = true, DefaultValue = 10.0, Unit = "Nm", Min = 1.0, Max = 50.0, Section = "Torque", Order = 1 },
                    new() { Key = "max_speed_rpm", Label = "Free Running Velocity", FieldType = "number", DefaultValue = 1500, Unit = "RPM", Min = 100, Max = 3500, Section = "Speed", Order = 2 },
                    new() { Key = "bit_holder_standard", Label = "Bit Holder Drive Type", FieldType = "select", DefaultValue = "1/4 Hex Quick Change", Section = "Tooling", Order = 3, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "1/4 Inch Hex Quick-Change (DIN 3126-F6.3)", Value = "1/4 Hex Quick Change" },
                        new() { Label = "3/8 Inch Square Drive", Value = "3/8 Square" }
                    }}
                },
                new Dictionary<string, object?>
                {
                    { "max_torque_nm", 10.0 },
                    { "max_speed_rpm", 1500 },
                    { "bit_holder_standard", "1/4 Hex Quick Change" }
                }),

            // 9. Pneumatic Screw Blow Feeder
            BuildTemplate(
                "20000000-0000-0000-0000-000000000009",
                GenericTemplateTypes.Component,
                "CMP-SCREW-FEEDER-01",
                "Step Feeder & Pneumatic Screw Blow Tube",
                "Singulated screw presentation module delivering screws via pressurized air tubing into the screwdriver nosepiece.",
                "Tooling",
                "Wrench",
                new List<string> { "ScrewFeeder", "BlowFeed", "Deprag", "Feeding" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "screw_thread_size", Label = "Screw Thread Designation", FieldType = "select", Required = true, DefaultValue = "M3", Section = "Fastener", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "M2.0 / M2.5 Micro", Value = "M2" },
                        new() { Label = "M3.0 (Automotive Standard)", Value = "M3" },
                        new() { Label = "M4.0 / M5.0 Structural", Value = "M4" }
                    }},
                    new() { Key = "bowl_fill_sensor", Label = "Bowl Low-Level Warning Sensor", FieldType = "boolean", DefaultValue = true, Section = "Sensors", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "screw_thread_size", "M3" },
                    { "bowl_fill_sensor", true }
                }),

            // 10. Piezoelectric Jet Dispensing Valve
            BuildTemplate(
                "20000000-0000-0000-0000-000000000010",
                GenericTemplateTypes.Component,
                "CMP-DISP-JETVALVE-01",
                "High-Frequency Piezo Jet Dispensing Valve",
                "Contact-free micro-dispensing valve capable of jetting medium-to-high viscosity fluids up to 500 dots/sec with integrated nozzle heater.",
                "Tooling",
                "Wrench",
                new List<string> { "JetValve", "Piezo", "Nordson", "Dispensing", "MicroDot" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "max_jet_frequency_hz", Label = "Maximum Jetting Frequency", FieldType = "number", DefaultValue = 400, Unit = "Hz", Min = 50, Max = 1000, Section = "Dynamics", Order = 1 },
                    new() { Key = "nozzle_heater_temp_c", Label = "Nozzle Heater Setpoint", FieldType = "number", DefaultValue = 55.0, Unit = "°C", Min = 20.0, Max = 130.0, Section = "Thermal", Order = 2 },
                    new() { Key = "tappet_tip_diameter_mm", Label = "Ceramic Tappet Ball Diameter", FieldType = "number", DefaultValue = 1.0, Unit = "mm", Section = "Nozzle", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "max_jet_frequency_hz", 400 },
                    { "nozzle_heater_temp_c", 55.0 },
                    { "tappet_tip_diameter_mm", 1.0 }
                }),

            // 11. Progressive Cavity Auger Pump
            BuildTemplate(
                "20000000-0000-0000-0000-000000000011",
                GenericTemplateTypes.Component,
                "CMP-DISP-AUGER-01",
                "Progressive Cavity Micro-Dispense Auger Pump",
                "Volumetric eccentric screw positive displacement pump for particle-filled pastes and abrasive thermal adhesives without shear damage.",
                "Tooling",
                "Wrench",
                new List<string> { "AugerPump", "ViscoTec", "Volumetric", "Paste" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "dispense_volume_per_rev_ml", Label = "Volume per Revolution", FieldType = "number", DefaultValue = 0.05, Unit = "mL/rev", Section = "Pumping", Order = 1 },
                    new() { Key = "suck_back_feature", Label = "Negative Rotation Suck-Back (Snuff-Back)", FieldType = "boolean", DefaultValue = true, Section = "Control", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "dispense_volume_per_rev_ml", 0.05 },
                    { "suck_back_feature", true }
                }),

            // 12. Titanium Solder Mini-Wave Pot
            BuildTemplate(
                "20000000-0000-0000-0000-000000000012",
                GenericTemplateTypes.Component,
                "CMP-SLD-POT-01",
                "Titanium Selective Solder Mini-Wave Pot Assembly",
                "Electromagnetic solder pump and heated titanium crucible bath designed for lead-free SAC305 alloy with inert nitrogen shroud nozzle.",
                "Tooling",
                "Flame",
                new List<string> { "SolderPot", "Titanium", "Selective", "Ersa", "Crucible" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "pot_heater_power_w", Label = "Heating Cartridge Power", FieldType = "number", DefaultValue = 1200, Unit = "W", Section = "Thermal", Order = 1 },
                    new() { Key = "pump_motor_rpm", Label = "Impeller Pump RPM", FieldType = "number", DefaultValue = 2800, Unit = "RPM", Section = "Pump", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "pot_heater_power_w", 1200 },
                    { "pump_motor_rpm", 2800 }
                }),

            // 13. GigE Machine Vision Camera
            BuildTemplate(
                "20000000-0000-0000-0000-000000000013",
                GenericTemplateTypes.Component,
                "CMP-CAM-GIGE-01",
                "GigE Vision Area Scan Machine Vision Camera",
                "Global shutter CMOS industrial camera with GigE Vision and GenICam protocol compliance for fiducial alignment and barcode tracking.",
                "Optics",
                "Eye",
                new List<string> { "Camera", "Vision", "Basler", "GigEVision", "Optics" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "sensor_resolution_mp", Label = "Image Resolution", FieldType = "select", DefaultValue = "5.0MP", Section = "Sensor", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "2.3 MP (1920x1200)", Value = "2.3MP" },
                        new() { Label = "5.0 MP (2448x2048)", Value = "5.0MP" },
                        new() { Label = "12.0 MP (4096x3000)", Value = "12.0MP" }
                    }},
                    new() { Key = "shutter_type", Label = "Shutter Type", FieldType = "string", DefaultValue = "Global Shutter", Section = "Sensor", Order = 2 },
                    new() { Key = "max_fps", Label = "Maximum Frame Rate", FieldType = "number", DefaultValue = 75, Unit = "FPS", Section = "Speed", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "sensor_resolution_mp", "5.0MP" },
                    { "shutter_type", "Global Shutter" },
                    { "max_fps", 75 }
                }),

            // 14. 3D Laser Triangulation Profile Sensor
            BuildTemplate(
                "20000000-0000-0000-0000-000000000014",
                GenericTemplateTypes.Component,
                "CMP-CAM-3D-01",
                "3D Laser Triangulation Profile Sensor",
                "High-speed blue laser 3D profiler measuring height variations, step heights, and coplanarity on shiny metallic pins and solder joints.",
                "Optics",
                "Eye",
                new List<string> { "LaserProfiler", "3D", "Keyence", "Gocator", "Optics" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "z_measurement_range_mm", Label = "Z-Axis Measurement Range", FieldType = "number", DefaultValue = 40.0, Unit = "mm", Section = "Range", Order = 1 },
                    new() { Key = "x_field_of_view_mm", Label = "X-Axis Width of View", FieldType = "number", DefaultValue = 65.0, Unit = "mm", Section = "Range", Order = 2 },
                    new() { Key = "z_repeatability_um", Label = "Z Measurement Repeatability", FieldType = "number", DefaultValue = 0.4, Unit = "µm", Section = "Accuracy", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "z_measurement_range_mm", 40.0 },
                    { "x_field_of_view_mm", 65.0 },
                    { "z_repeatability_um", 0.4 }
                }),

            // 15. Bi-Telecentric Optical Lens
            BuildTemplate(
                "20000000-0000-0000-0000-000000000015",
                GenericTemplateTypes.Component,
                "CMP-LENS-TELE-01",
                "Bi-Telecentric Optical Lens",
                "Zero-perspective optical lens providing constant magnification regardless of object distance, essential for precision 2D metrology.",
                "Optics",
                "Eye",
                new List<string> { "Lens", "Telecentric", "OptoEngineering", "Metrology" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "optical_magnification", Label = "Optical Magnification", FieldType = "number", DefaultValue = 0.5, Section = "Optics", Order = 1 },
                    new() { Key = "working_distance_mm", Label = "Working Distance", FieldType = "number", DefaultValue = 135.0, Unit = "mm", Section = "Optics", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "optical_magnification", 0.5 },
                    { "working_distance_mm", 135.0 }
                }),

            // 16. Multi-Channel LED Ring Light
            BuildTemplate(
                "20000000-0000-0000-0000-000000000016",
                GenericTemplateTypes.Component,
                "CMP-LIGHT-RING-01",
                "Programmable Multi-Channel LED Ring Light",
                "Segmented 4-quadrant LED ring illuminator with independent intensity and strobe trigger for shadow-free component inspection.",
                "Optics",
                "Eye",
                new List<string> { "Lighting", "LED", "CCS", "Illumination", "Strobe" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "led_color_wavelength", Label = "LED Emission Wavelength", FieldType = "select", DefaultValue = "White 6500K", Section = "Lighting", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "White (6500K)", Value = "White 6500K" },
                        new() { Label = "Red (630nm)", Value = "Red 630nm" },
                        new() { Label = "Blue (470nm)", Value = "Blue 470nm" }
                    }},
                    new() { Key = "quadrant_independent_control", Label = "4-Quadrant Independent Control", FieldType = "boolean", DefaultValue = true, Section = "Control", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "led_color_wavelength", "White 6500K" },
                    { "quadrant_independent_control", true }
                }),

            // 17. CMOS Laser Displacement Distance Sensor
            BuildTemplate(
                "20000000-0000-0000-0000-000000000017",
                GenericTemplateTypes.Component,
                "CMP-SENSOR-LASER-01",
                "CMOS Laser Displacement Distance Sensor",
                "Compact triangulation distance sensor with digital display and 4-20mA / IO-Link output for PCB height check and nest seating confirmation.",
                "Sensors",
                "Activity",
                new List<string> { "Sensor", "Laser", "Keyence", "Omron", "Displacement" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "reference_distance_mm", Label = "Reference Center Distance", FieldType = "number", DefaultValue = 50.0, Unit = "mm", Section = "Sensor", Order = 1 },
                    new() { Key = "measurement_span_mm", Label = "Measurement Range (+/-)", FieldType = "number", DefaultValue = 10.0, Unit = "mm", Section = "Sensor", Order = 2 },
                    new() { Key = "repeatability_um", Label = "Repeatability", FieldType = "number", DefaultValue = 1.5, Unit = "µm", Section = "Accuracy", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "reference_distance_mm", 50.0 },
                    { "measurement_span_mm", 10.0 },
                    { "repeatability_um", 1.5 }
                }),

            // 18. Industrial RFID Read/Write Transceiver Head
            BuildTemplate(
                "20000000-0000-0000-0000-000000000018",
                GenericTemplateTypes.Component,
                "CMP-SENSOR-RFID-01",
                "Industrial RFID Read/Write Transceiver Head",
                "13.56 MHz HF RFID reader for workpiece carrier / pallet identification according to ISO 15693 standards.",
                "Sensors",
                "Activity",
                new List<string> { "RFID", "Pallet", "Balluff", "Turck", "Identification" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "rfid_frequency_mhz", Label = "Carrier Frequency", FieldType = "number", DefaultValue = 13.56, Unit = "MHz", Section = "RFID", Order = 1 },
                    new() { Key = "sensing_range_mm", Label = "Read/Write Operating Distance", FieldType = "number", DefaultValue = 45.0, Unit = "mm", Section = "Range", Order = 2 },
                    new() { Key = "housing_protection", Label = "IP Enclosure Rating", FieldType = "string", DefaultValue = "IP67", Section = "Physical", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "rfid_frequency_mhz", 13.56 },
                    { "sensing_range_mm", 45.0 },
                    { "housing_protection", "IP67" }
                }),

            // 19. Ingun Spring Contact Test Probe
            BuildTemplate(
                "20000000-0000-0000-0000-000000000019",
                GenericTemplateTypes.Component,
                "CMP-TEST-PIN-01",
                "Gold-Plated Ingun Spring Contact Test Probe",
                "Precision spring contact probe with gold-plated barrel and beryllium copper tip for ICT and functional test fixtures.",
                "Tooling",
                "Activity",
                new List<string> { "TestProbe", "Ingun", "PogoPin", "ICT", "Fixture" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "tip_style", Label = "Probe Tip Geometry", FieldType = "select", DefaultValue = "4-Point Crown", Section = "Tip", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "4-Point Crown (Solder Pad Penetration)", Value = "4-Point Crown" },
                        new() { Label = "Serrated Self-Cleaning", Value = "Serrated" },
                        new() { Label = "Spear Point (Vias)", Value = "Spear Point" }
                    }},
                    new() { Key = "spring_force_n", Label = "Nominal Spring Force", FieldType = "number", DefaultValue = 2.0, Unit = "N", Section = "Mechanical", Order = 2 },
                    new() { Key = "max_current_a", Label = "Continuous Current Rating", FieldType = "number", DefaultValue = 5.0, Unit = "A", Section = "Electrical", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "tip_style", "4-Point Crown" },
                    { "spring_force_n", 2.0 },
                    { "max_current_a", 5.0 }
                }),

            // 20. 6-Axis Articulated Robot Arm Manipulator
            BuildTemplate(
                "20000000-0000-0000-0000-000000000020",
                GenericTemplateTypes.Component,
                "CMP-ROBOT-ARM-01",
                "6-Axis Articulated Industrial Robot Arm Manipulator",
                "Compact articulated robotic arm with internal cable routing and high path accuracy for testing and assembly cells.",
                "Robotics",
                "Cpu",
                new List<string> { "Robot", "Manipulator", "KUKA", "Fanuc", "ABB" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "axis_count", Label = "Degrees of Freedom", FieldType = "number", DefaultValue = 6, Section = "Kinematics", Order = 1 },
                    new() { Key = "max_payload_kg", Label = "Payload at Wrist Flange", FieldType = "number", DefaultValue = 7.0, Unit = "kg", Section = "Payload", Order = 2 },
                    new() { Key = "max_reach_mm", Label = "Maximum Reach", FieldType = "number", DefaultValue = 911, Unit = "mm", Section = "Geometry", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "axis_count", 6 },
                    { "max_payload_kg", 7.0 },
                    { "max_reach_mm", 911 }
                }),

            // 21. Heavy-Duty Aluminum Structural Profile
            BuildTemplate(
                "20000000-0000-0000-0000-000000000021",
                GenericTemplateTypes.Component,
                "CMP-FRAME-PROF-01",
                "Modular Aluminum Framing Profile 45x45",
                "Anodized structural aluminum framing extrusion with standard 10mm T-slot for machine base frames and protective guarding.",
                "Tooling",
                "Layers",
                new List<string> { "Framing", "Profile", "BoschRexroth", "Item", "Structure" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "profile_dimension", Label = "Profile Cross-Section", FieldType = "select", DefaultValue = "45x45", Section = "Dimensions", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "40x40 mm (Item Standard)", Value = "40x40" },
                        new() { Label = "45x45 mm (Bosch Rexroth)", Value = "45x45" },
                        new() { Label = "45x90 mm (Heavy Stiffener)", Value = "45x90" }
                    }},
                    new() { Key = "t_slot_width_mm", Label = "T-Slot Width", FieldType = "number", DefaultValue = 10, Unit = "mm", Section = "Slot", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "profile_dimension", "45x45" },
                    { "t_slot_width_mm", 10 }
                }),

            // 22. Type 4 Safety Light Curtain with Muting
            BuildTemplate(
                "20000000-0000-0000-0000-000000000022",
                GenericTemplateTypes.Component,
                "CMP-SAFETY-CURTAIN-01",
                "Type 4 Safety Light Curtain with Muting",
                "Optoelectronic protective safety barrier with finger/hand resolution and integrated muting sensors for safe pallet transport openings.",
                "Safety",
                "ShieldAlert",
                new List<string> { "Safety", "LightCurtain", "Sick", "Pilz", "OSSD" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "protective_height_mm", Label = "Protective Field Height", FieldType = "number", DefaultValue = 600, Unit = "mm", Min = 150, Max = 1800, Section = "Dimensions", Order = 1 },
                    new() { Key = "resolution_optical_mm", Label = "Optical Resolution", FieldType = "select", DefaultValue = "14mm", Section = "Resolution", Order = 2, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "14 mm (Finger Protection)", Value = "14mm" },
                        new() { Label = "30 mm (Hand Protection)", Value = "30mm" }
                    }},
                    new() { Key = "ossd_outputs", Label = "Dual Redundant OSSD Outputs", FieldType = "boolean", DefaultValue = true, Section = "Electrical", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "protective_height_mm", 600 },
                    { "resolution_optical_mm", "14mm" },
                    { "ossd_outputs", true }
                }),

            // 23. Smart Electric 8mm Tape Feeder
            BuildTemplate(
                "20000000-0000-0000-0000-000000000023",
                GenericTemplateTypes.Component,
                "CMP-FEEDER-TAPE-01",
                "Smart Electric 8mm Tape Feeder",
                "Microprocessor-controlled electric component tape feeder with optical reel barcode reader and automatic pitch indexing.",
                "Tooling",
                "Boxes",
                new List<string> { "Feeder", "TapeFeeder", "Fuji", "ASM", "SMT" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "tape_width_mm", Label = "Tape Carrier Width", FieldType = "select", DefaultValue = "8mm", Section = "Tape", Order = 1, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "8 mm (0201 - 0805 passives)", Value = "8mm" },
                        new() { Label = "12 mm (SOT23 / small ICs)", Value = "12mm" },
                        new() { Label = "16 mm / 24 mm (Power ICs)", Value = "16mm" }
                    }},
                    new() { Key = "pitch_indexing_mm", Label = "Pitch Indexing Increment", FieldType = "select", DefaultValue = "2mm", Section = "Drive", Order = 2, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "2 mm", Value = "2mm" },
                        new() { Label = "4 mm", Value = "4mm" }
                    }}
                },
                new Dictionary<string, object?>
                {
                    { "tape_width_mm", "8mm" },
                    { "pitch_indexing_mm", "2mm" }
                }),

            // 24. Overhead AC Static Eliminator Ionizing Blower
            BuildTemplate(
                "20000000-0000-0000-0000-000000000024",
                GenericTemplateTypes.Component,
                "CMP-IONIZER-FAN-01",
                "Overhead AC Static Eliminator Ionizing Blower",
                "High-frequency AC corona discharge static neutralizer for sensitive automotive ESD class 0 semiconductor handling areas.",
                "Safety",
                "ShieldAlert",
                new List<string> { "Ionizer", "ESD", "StaticNeutralizer", "Keyence", "Cleanroom" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "decay_time_seconds", Label = "Static Decay Time (1000V -> 100V)", FieldType = "number", DefaultValue = 1.0, Unit = "s", Section = "Performance", Order = 1 },
                    new() { Key = "ion_balance_v", Label = "Ion Offset Voltage", FieldType = "number", DefaultValue = 5.0, Unit = "+/- V", Section = "Performance", Order = 2 }
                },
                new Dictionary<string, object?>
                {
                    { "decay_time_seconds", 1.0 },
                    { "ion_balance_v", 5.0 }
                })
        };
    }

    public static IReadOnlyList<GenericTemplate> GetSupportTemplates()
    {
        return new List<GenericTemplate>
        {
            // Level 2 Controls Delegation
            BuildTemplate(
                "33333333-3333-3333-3333-333333333333",
                GenericTemplateTypes.Delegation,
                "DLG-CTRL-01",
                "Level 2 PLC & Controls Incident Dispatch",
                "Rapid response delegation matrix for automation controller faults, bus desynchronization, and safety halts.",
                "Controls",
                "Users",
                new List<string> { "Controls", "PLC", "Escalation", "TwinCAT" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "discipline", Label = "Engineering Discipline", FieldType = "string", Required = true, DefaultValue = "Controls", Section = "Dispatch", Order = 1 },
                    new() { Key = "escalation_timeout_mins", Label = "Escalation Timeout", FieldType = "number", Required = true, DefaultValue = 20, Unit = "min", Min = 5, Max = 120, Section = "SLA & Timeouts", Order = 2 },
                    new() { Key = "shift_coverage", Label = "Allowed Shift Patterns", FieldType = "select", Required = true, DefaultValue = "AllShifts", Section = "Scheduling", Order = 3, Options = new List<TemplateFieldOptionDto>
                    {
                        new() { Label = "24/7 All Shifts + On-Call", Value = "AllShifts" },
                        new() { Label = "Day Shift Only (08:00 - 17:00)", Value = "DayShiftOnly" },
                        new() { Label = "Weekend On-Call", Value = "WeekendOnCall" }
                    }},
                    new() { Key = "required_certification", Label = "Required Certification", FieldType = "string", DefaultValue = "CERT-TWINCAT3-PRO", Section = "Skills", Order = 4 },
                    new() { Key = "auto_sms_alert", Label = "Send SMS Notification to SPOC", FieldType = "boolean", DefaultValue = true, Section = "Notifications", Order = 5 }
                },
                new Dictionary<string, object?>
                {
                    { "discipline", "Controls" },
                    { "escalation_timeout_mins", 20 },
                    { "shift_coverage", "AllShifts" },
                    { "required_certification", "CERT-TWINCAT3-PRO" },
                    { "auto_sms_alert", true }
                }),

            // Beckhoff AX5000 Telemetry Profile
            BuildTemplate(
                "44444444-4444-4444-4444-444444444444",
                GenericTemplateTypes.Telemetry,
                "TEL-DRIVE-01",
                "Beckhoff AX5000 Real-Time Drive Telemetry",
                "High-frequency operational metrics ingestion for motor temperature, DC link voltage, and torque utilization.",
                "Drives",
                "Activity",
                new List<string> { "Telemetry", "Drive", "Motors", "Beckhoff" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "polling_interval_ms", Label = "Polling Interval", FieldType = "number", Required = true, DefaultValue = 250, Unit = "ms", Min = 50, Max = 10000, Section = "Sampling", Order = 1 },
                    new() { Key = "deadband_pct", Label = "Deadband Filter Threshold", FieldType = "number", DefaultValue = 1.5, Unit = "%", Min = 0.0, Max = 20.0, Section = "Filtering", Order = 2 },
                    new() { Key = "temp_alarm_c", Label = "Motor Over-Temp Alarm", FieldType = "number", Required = true, DefaultValue = 85.0, Unit = "°C", Min = 40.0, Max = 140.0, Section = "Alarms", Order = 3 }
                },
                new Dictionary<string, object?>
                {
                    { "polling_interval_ms", 250 },
                    { "deadband_pct", 1.5 },
                    { "temp_alarm_c", 85.0 }
                }),

            // Following Error Maintenance Ticket
            BuildTemplate(
                "55555555-5555-5555-5555-555555555555",
                GenericTemplateTypes.Ticket,
                "TCK-POS-01",
                "Axis Position Following Error Fault",
                "Automated maintenance ticket created when servo encoder divergence exceeds allowed contouring corridor.",
                "Error",
                "ShieldAlert",
                new List<string> { "Ticket", "Motion", "Fault", "FollowingError" },
                new List<TemplateFieldDefinitionDto>
                {
                    new() { Key = "error_code", Label = "PLC Error Code", FieldType = "string", Required = true, DefaultValue = "E-MOT-01", Section = "Classification", Order = 1 },
                    new() { Key = "short_description", Label = "Short Description", FieldType = "string", Required = true, DefaultValue = "Axis Position Divergence Fault", Section = "Classification", Order = 2 },
                    new() { Key = "fb_block_name", Label = "TwinCAT Function Block", FieldType = "string", DefaultValue = "FB_AxisControl", Section = "Diagnostic State", Order = 3 },
                    new() { Key = "halt_machine_on_breach", Label = "Lockout Machine Until Remediation", FieldType = "boolean", DefaultValue = true, Section = "Safety", Order = 4 }
                },
                new Dictionary<string, object?>
                {
                    { "error_code", "E-MOT-01" },
                    { "short_description", "Axis Position Divergence Fault" },
                    { "fb_block_name", "FB_AxisControl" },
                    { "halt_machine_on_breach", true }
                })
        };
    }
}
