# Frontend Composables

Composables manage reactive state and external API interactions in the Nuxt 3 application.

## useDashboard
Manages the live dashboard state and telemetry data.

* **Stats**: High-level metrics for the Stats Grid.
* **RecentClients**: List of the most recently active edge nodes.
* **SecurityEvents**: Activity feed of recent system/security events.
* **Loading**: Indicator for active background fetch operations.
* **Methods**:
    * `refreshDashboard()`: Manually triggers a data synchronization with the backend.

## useSearch
Provides the logic for the Omni-Search interface.

* **Query**: The current search string.
* **Results**: Reactive list of `SearchResult` objects.
* **Recommendations**: Dynamic tagging suggestions based on the current input.
* **Methods**:
    * `performSearch(query)`: Executes the search against the backend API.
    * `fetchKeys()`: Retrieves valid metadata keys for tagging recommendations.