## 1 Shift from data-driven only approach, as features or new machine types can be added overtime, no need for universal coverege rn

    Pivot from the strictly data-driven component - machine - station tree towards a class library of standard machinery with the ability to tweak it's properties
    From components like motors, framing profiles, grippers, welding, vision, screwing, dispensing, feeding, pick and place machines robots and such to more abstract machines.
    A machine should be able to implement multiple interfaces(such as tester, voltage, application, EOL etc. or visual inspection and assembly, robot etc.)
    I want to cover all machines/components that might appear in the production of PCBAs and electronics as a first stepping stones for a tier 1 OEM of auto electronics

## 2 Have the frontend follow the previous point

    Less relience on JSON builders, with custom UI built for machines
    Keep a generic machine type that let's users build a machine from components
    See if class generation could work from users provided data(If a machine has some feature auto implement the interface with some base class as an injected dependency, some combined screwing machine implments IScrewDriver which makes it so that my class has an injected ScrewerBase object.
    Add toggling of machine types in-use, so orgs that don't use welding robots don't see that as a filter.

---

## 3 Completed Implementations & Architecture

- [x] **System Default Templates Catalog & Engine (`GenericTemplateService.cs`)**:
  - Implemented pre-created templates for industrial electronics OEM machines and components (Conveyors, CNC Milling, Automated Screwdrivers, Dispensing Cells, Laser Welders, Vision Inspection, EOL Test Benches).
  - Multi-interface inheritance and parameter schema validation.
  - Organization machine type filtering toggles (`useOrganizationMachineTypes.ts`).
- [x] **User Dedications & Delegation Recipe Engine (`DelegationRecipeService.cs`)**:
  - Dedicated technician assignment, inheritance hierarchy across Machine Groups, Sub-lines, and Stations.
  - User dedication management in `/dashboard/users/[id].vue`.
- [x] **Lua Automation & Scripting Sandbox (`LuaAutomationService.cs`)**:
  - Sandboxed execution of automation logic, recipe evaluation, and UI extension script actions.
- [x] **CAD 3D Digital Twin Binding (`useCadModelViewer.ts` & `station_digital_twin.glb`)**:
  - Hierarchical 3D mesh node to hardware component binding, raycast selection, and live telemetry overlays.
- [x] **CAD 2D Plant Layout Telemetry Overlays (`StationTelemetryBubble.vue` & `useStationTelemetry.ts`)**:
  - Expandable HUD bubbles with real-time OEE, Yield, Parts/Hour, and SLA maintenance ticket alerts.
