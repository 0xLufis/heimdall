# API Reference

The Heimdall backend provides a set of RESTful endpoints built with ASP.NET Core 9.0.

## Base URL
All API requests should be prefixed with `/api`. When accessing from the Nuxt frontend, use the Nitro proxy at `/api/proxy/*` to automatically include authentication cookies.

## Endpoints

### 1. Client PCs (`/api/ClientPc`)
Manages the physical computers reporting to the system.

*   `GET /api/ClientPc`
    *   **Description**: Retrieves a list of all known Client PCs.
    *   **Returns**: Array of `ClientPc` objects (Includes assigned `Machines`).
*   `GET /api/ClientPc/{id}`
    *   **Description**: Gets a specific Client PC by its GUID.
*   `PUT /api/ClientPc/{id}`
    *   **Description**: Updates a Client PC's relationships (e.g., assigning it to a `Machine` or a `PinnedObjectHandle`).

### 2. Machines / Stations (`/api/Machine`)
Manages the logical stations on the factory floor.

*   `GET /api/Machine`
    *   **Description**: Retrieves all configured machines.
*   `POST /api/Machine`
    *   **Description**: Creates a new logical machine (requires `CustomIdentifier`).
*   `PUT /api/Machine/{id}`
    *   **Description**: Updates an existing machine (useful for updating the `PinnedObjectHandle` on the DXF map).

### 3. Inventory (`/api/Inventory`)
Manages the hardware and software components.

*   `GET /api/Inventory/hardware`
    *   **Description**: Retrieves all hardware components, including Manufacturer and Supplier details.
*   `GET /api/Inventory/hardware/search`
    *   **Description**: Advanced search against the JSONB `TechnicalSpecs`.
    *   **Query Parameters**: `category` (e.g., "Industrial Robot"), `manufacturer`, `minTorque`, `interfaceType`.
*   `POST /api/Inventory/hardware`
    *   **Description**: Creates a new hardware component.
*   `DELETE /api/Inventory/hardware/{id}`
    *   **Description**: Removes a hardware component.
*   `GET /api/Inventory/software`
    *   **Description**: Retrieves all software licenses/components.

### 4. Authentication (`/api/Auth`)
Verifies active sessions created by the Better-Auth frontend plugin.

*   `GET /api/Auth/me`
    *   **Description**: Validates the `better-auth.session_token` cookie and returns the user's claims (Roles, Email, Name) decoded by the PostgreSQL database.
