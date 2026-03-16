# User Guide: Heimdall Dashboard

Welcome to the Heimdall Plant Management System. This platform allows administrators and engineers to monitor Client PCs, manage industrial hardware inventory, and map these assets to physical floor plans.

## 1. Interactive Plant Map (`/dashboard/map`)
The Plant Map allows you to visually assign logic stations and computers to their physical location on the factory floor using your CAD (DXF) drawings.

### How to use:
- **Navigation**: Click and drag to pan across the layout. Use your mouse wheel to zoom in and out.
- **Pinning a Station**: 
  1. Click on a highlighted machine block on the SVG layout.
  2. The sidebar will prompt you to enter a **Station / Machine ID** (e.g., `Assembly-Robot-Cell-1`).
  3. Select an active **Client PC** from the dropdown to assign it to this station.
  4. Click **Save Pin Assignment**.
- **Finding PCs**: The "Currently Pinned" list shows all active assignments. Clicking an item will instantly center and highlight the machine on the map.

## 2. Client PC Management (`/dashboard/clients`)
This panel shows a live view of all agents reporting to the Heimdall server.

- **Status**: The dot indicates if the machine is online (pulsing green) or offline (gray).
- **Search**: You can search by Hostname, MAC Address, or the custom Station ID you assigned on the Plant Map.
- **Live Preview**: When you hover your mouse over a Client PC in the table, the map on the right side of the screen will automatically highlight exactly where that computer is located on the factory floor.

## 3. Inventory Management (`/dashboard/inventory`)
Heimdall provides a flexible inventory system for tracking industrial equipment (Hardware) and licensing (Software).

### Features:
- **Categorization**: Switch between Hardware and Software tabs.
- **Smart Filtering**: Use the dropdowns to filter by specific industrial categories (e.g., Sensors, Industrial Robots, Screwdrivers).
- **Technical Specifications**: Depending on the category, specific technical details will be visible directly in the table:
    - *Industrial Robots*: Payload (kg) and Reach (mm).
    - *Screwdrivers*: Max Torque (Nm).
    - *Sensors*: Interface types and resolutions.
- **Adding Assets**: Click "Add Component" to register a new item to the database.
