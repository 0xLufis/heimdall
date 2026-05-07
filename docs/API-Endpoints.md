# API Endpoints

## Dashboard Controller
`GET /api/Dashboard`

Retrieves a unified data package for the dashboard, including stats, recent clients, and security events.

* **Response**: `200 OK` with `DashboardDto`.
* **Security**: Requires Authorization.

## Client PC Controller
`GET /api/ClientPc`

Retrieves a list of all edge nodes with their basic status and spatial handles.

* **Response**: `200 OK` with `List<ClientPcDto>`.
* **Security**: Public (Optional/Dev) or Authorized.

## Inventory Controller
`GET /api/Inventory/search?query={q}`

Executes a tag-based Omni-Search across all inventory assets.

* **Parameters**: `query` (string) - Supporting tags like `manufacturer:X`.
* **Response**: `200 OK` with `List<InventoryItemDto>`.