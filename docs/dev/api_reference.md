# API Reference

The Heimdall backend provides a set of RESTful endpoints built with ASP.NET Core 9.0.

## Base URL
All API requests should be prefixed with `/api`. When accessing from the Nuxt frontend, use the Nitro proxy at `/api/proxy/*` to automatically include authentication cookies.

## Endpoints

### 1. Client PCs (`/api/ClientPc`)
Manages the physical computers reporting to the system.

*   `GET /api/ClientPc`
    *   **Description**: Retrieves a list of all known Client PCs, including their associated Machines.
    *   **Returns**: `200 OK` with an array of <c>ClientPc</c> objects.
*   `GET /api/ClientPc/{id}`
    *   **Description**: Retrieves a specific Client PC by its GUID.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Client PC.
    *   **Returns**: `200 OK` with a <c>ClientPc</c> object, or `404 Not Found`.
*   `POST /api/ClientPc`
    *   **Description**: Creates a new Client PC.
    *   **Body**: <c>ClientPc</c> object.
    *   **Returns**: `201 Created` with the newly created <c>ClientPc</c> object.
*   `PUT /api/ClientPc/{id}`
    *   **Description**: Updates a Client PC's pinned object handle and associated machines.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Client PC to update.
    *   **Body**: <c>ClientPc</c> object containing the updated data.
    *   **Returns**: `204 No Content` if successful, or `404 Not Found`.
*   `DELETE /api/ClientPc/{id}`
    *   **Description**: Deletes a specific Client PC by its ID.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Client PC to delete.
    *   **Returns**: `204 No Content` if successful, or `404 Not Found`.

### 2. Machines / Stations (`/api/Machine`)
Manages the logical stations on the factory floor.

*   `GET /api/Machine`
    *   **Description**: Retrieves all configured machines.
    *   **Returns**: `200 OK` with an array of <c>Machine</c> objects.
*   `POST /api/Machine`
    *   **Description**: Creates a new logical machine (requires `CustomIdentifier`).
    *   **Body**: <c>Machine</c> object.
    *   **Returns**: `200 OK` with the newly created <c>Machine</c> object.
*   `PUT /api/Machine/{id}`
    *   **Description**: Updates an existing machine (useful for updating the `PinnedObjectHandle` on the DXF map).
    *   **Parameters**: `id` (GUID) - The unique identifier of the Machine to update.
    *   **Body**: <c>Machine</c> object containing the updated data.
    *   **Returns**: `204 No Content` if successful, or `400 Bad Request` if IDs do not match.

### 3. Inventory (`/api/Inventory`)
Manages the hardware and software components. Requires `admin` role for modifications.

*   `GET /api/Inventory/hardware`
    *   **Description**: Retrieves all hardware components, including Manufacturer and Supplier details.
    *   **Returns**: `200 OK` with an array of <c>HardwareComponent</c> objects.
*   `GET /api/Inventory/hardware/search`
    *   **Description**: Advanced search against the JSONB `TechnicalSpecs`.
    *   **Query Parameters**: `category` (string, e.g., "Industrial Robot"), `manufacturer` (string), `minTorque` (double), `interfaceType` (string).
    *   **Returns**: `200 OK` with a filtered array of <c>HardwareComponent</c> objects.
*   `POST /api/Inventory/hardware`
    *   **Description**: Creates a new hardware component.
    *   **Body**: <c>HardwareComponent</c> object.
    *   **Returns**: `201 Created` with the newly created <c>HardwareComponent</c> object.
*   `PUT /api/Inventory/hardware/{id}`
    *   **Description**: Updates an existing Hardware Component.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Hardware Component to update.
    *   **Body**: <c>HardwareComponent</c> object with updated data.
    *   **Returns**: `204 No Content` if successful, or `400 Bad Request` if IDs do not match.
*   `DELETE /api/Inventory/hardware/{id}`
    *   **Description**: Deletes a specific hardware component.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Hardware Component to delete.
    *   **Returns**: `204 No Content` if successful, or `404 Not Found`.
*   `GET /api/Inventory/software`
    *   **Description**: Retrieves all software licenses/components, including Manufacturer and Supplier details.
    *   **Returns**: `200 OK` with an array of <c>SoftwareComponent</c> objects.
*   `POST /api/Inventory/software`
    *   **Description**: Creates a new software component.
    *   **Body**: <c>SoftwareComponent</c> object.
    *   **Returns**: `201 Created` with the newly created <c>SoftwareComponent</c> object.
*   `PUT /api/Inventory/software/{id}`
    *   **Description**: Updates an existing Software Component.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Software Component to update.
    *   **Body**: <c>SoftwareComponent</c> object with updated data.
    *   **Returns**: `204 No Content` if successful, or `400 Bad Request` if IDs do not match.
*   `DELETE /api/Inventory/software/{id}`
    *   **Description**: Deletes a specific software component.
    *   **Parameters**: `id` (GUID) - The unique identifier of the Software Component to delete.
    *   **Returns**: `204 No Content` if successful, or `404 Not Found`.

### 4. Organizations (`/api/Organization`)
Exposes organization and tenancy data pulled from the `auth` schema.

*   `GET /api/Organization/my-organizations`
    *   **Description**: Retrieves a list of organizations the current authenticated user belongs to.
    *   **Returns**: `200 OK` with an array of <c>AuthOrganization</c> objects.
*   `GET /api/Organization/{id}`
    *   **Description**: Retrieves full details (including members) for a specific organization.
    *   **Parameters**: `id` (string) - The unique identifier of the organization.
    *   **Returns**: `200 OK` with an <c>AuthOrganization</c> object, or `404 Not Found`.

### 5. Authentication (`/api/Auth`)
Provides endpoints for verifying user sessions.

*   `GET /api/Auth/me`
    *   **Description**: Validates the `better-auth.session_token` cookie and returns user details and the `OrgId` claim if an organization is active.
    *   **Authorization**: Requires an active Better-Auth session cookie.
    *   **Returns**: `200 OK` with user details, or `401 Unauthorized`.
