# Frontend Components

Custom UI components built with Vue 3, TailwindCSS, and Radix UI primitives.

## Search
The central search interface for the Heimdall ecosystem. Supports unified tagging, asynchronous results, and advanced filtering.

### Props
* **placeholder**: `string` - Optional placeholder text.
* **immediate**: `boolean` - Whether to trigger search immediately on mount.

### Events
* **search**: `[query: string]` - Fired whenever the search query changes (debounced).

### Usage
```vue
<Search 
  placeholder="Search machines..." 
  :immediate="true" 
  @search="onSearch" 
/>
```

## ClientDetailsModal
A comprehensive modal for viewing and managing a specific Client PC node. Displays hardware specs, software packages, and system activity.

### Props
* **client**: `Object` - The Client PC object to display.
* **isOpen**: `boolean` - Controls the visibility of the modal.

### Events
* **close**: Fired when the user dismisses the modal.

## InteractiveMap
Renders a CAD layout (DXF) and overlays interactive anchors for machines and PCs.

### Props
* **dxfUrl**: `string` - Path to the DXF file.
* **highlightedHandles**: `string[]` - List of CAD handles to highlight on the map.

### Events
* **select-node**: Fired when a user clicks on an anchored asset.