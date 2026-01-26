---
name: scaffold-get-by-id
description: Adds a new GET operation to fetch a single resource by ID. Handles 404s, caching, and localization.
---

Follow this guide to implement a **Get By ID** endpoint in the ApiService.

1.  **Prerequisites**
    -   Ensure the Projection exists (`/scaffold-single-stream-projection` or similar).

2.  **Create Endpoint**
    -   Create/Update `src/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`
    -   **Method**: `MapGet`
    -   **Logic**: `GetOrCreateLocalizedAsync` -> `LoadAsync` -> Map to DTO
    -   **Template**: `templates/GetByIdEndpoint.cs`

3.  **Client Integration**
    -   Create `IGet{Resource}Endpoint.cs` in Client project.

## Related Skills
- `/scaffold-list-query`: For fetching lists of resources.
