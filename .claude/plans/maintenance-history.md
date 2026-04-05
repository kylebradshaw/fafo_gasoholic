# Plan: Vehicle Maintenance History Tracking

> Source PRD: `.claude/prd/maintenance-history.md`

## Context

Users can track fuel fillups and mileage in Gasoholic but have no way to log maintenance events (oil changes, tire rotations, etc.). This plan adds a Maintenance tab with full CRUD, following the same patterns already established by the fillups feature.

## Architectural decisions

- **Routes**: `GET/POST /api/autos/{autoId}/maintenance`, `PUT/DELETE /api/autos/{autoId}/maintenance/{id}` — mirrors fillup endpoints
- **Frontend route**: `/app/maintenance` alongside `/app/log` and `/app/autos`
- **Schema**: `MaintenanceRecords` table with FK to `Autos`, cascade delete. `MaintenanceType` enum stored as string (same as `FuelType`)
- **Key models**: `MaintenanceRecord` entity, `MaintenanceType` enum
- **Auth/ownership**: Same pattern as `FillupEndpoints` — `RequireAuth` + verify `auto.UserId == userId`
- **Request/response**: POST/PUT accept `{ type, performedAt, odometer, cost, notes? }`, POST returns `{ id }`, GET returns full record list sorted by odometer desc
- **Frontend state**: Signals pattern matching `FillupsService` — private writable signal + public readonly, `firstValueFrom` for HTTP

---

## Phase 1: Schema + Read Endpoint + Empty Tab

**User stories**: 1, 2, 11, 12, 16, 17

### What to build

A complete read path from database to UI. Create the `MaintenanceType` enum and `MaintenanceRecord` model, add the `MaintenanceRecords` DbSet to `AppDbContext` with string conversion for the enum and cascade delete from Auto, and generate an EF migration. Wire up the `GET /api/autos/{autoId}/maintenance` endpoint returning records sorted by odometer descending, using the same auth/ownership guard as `FillupEndpoints`. On the frontend, add a `MaintenanceService` with signals for records and loading state plus a `loadRecords(autoId)` method. Create the `MaintenanceComponent` that reactively loads records when `currentAutoId` changes (same effect pattern as `FillupsComponent`), shows a table with columns Date, Type, Odometer, Cost, Notes, Actions, and displays an empty state message when no records exist. Add the `MaintenanceTypePipe` to render enum values as human-readable labels. Add the `/app/maintenance` route and a "Maintenance" tab link in `AppShellComponent`.

### Key files to modify/create

- `Models/Enums.cs` — add `MaintenanceType` enum
- `Models/MaintenanceRecord.cs` — new entity
- `Data/AppDbContext.cs` — add DbSet, configure relationship + string conversion
- `Migrations/` — new migration
- `Endpoints/MaintenanceEndpoints.cs` — GET endpoint + `MapMaintenanceEndpoints`
- `Program.cs` — register `MapMaintenanceEndpoints()`
- `client/src/app/core/services/maintenance.service.ts` — new service with signals
- `client/src/app/core/pipes/maintenance-type.pipe.ts` — new pipe
- `client/src/app/features/maintenance/maintenance.component.ts` — new component
- `client/src/app/features/app-shell/app-shell.component.ts` — add tab link
- `client/src/app/app.routes.ts` — add route

### Acceptance criteria

- [ ] `MaintenanceType` enum exists with all 9 values (OilChange through Other)
- [ ] `MaintenanceRecord` entity has Id, AutoId, Type, PerformedAt, Odometer, Cost, Notes (nullable)
- [ ] Migration creates `MaintenanceRecords` table with FK + cascade delete
- [ ] `GET /api/autos/{autoId}/maintenance` returns records sorted by odometer desc, enforces auth + ownership
- [ ] "Maintenance" tab appears in the nav bar and routes to `/app/maintenance`
- [ ] Component shows table headers and empty state when no records exist
- [ ] Component reloads records when the auto selector changes
- [ ] Tab is inaccessible / shows prompt when no vehicle is selected

---

## Phase 2: Create Record

**User stories**: 3, 4, 5, 6, 7, 8, 9, 10, 18, 19, 20

### What to build

The full create flow. Add the `POST /api/autos/{autoId}/maintenance` endpoint that validates required fields (type, performedAt, odometer, cost) and returns `{ id }`. Extend `MaintenanceService` with `createRecord(autoId, data)` that POSTs then reloads the list. Build `MaintenanceModalComponent` in add mode with fields: Type (dropdown of all `MaintenanceType` values), PerformedAt (date input), Odometer (number), Cost (number), Notes (textarea, optional). Wire the "+ Add Record" button in `MaintenanceComponent` to open the modal, and on save call the service and show a toast.

### Key files to modify/create

- `Endpoints/MaintenanceEndpoints.cs` — add POST handler + request DTO
- `client/src/app/core/services/maintenance.service.ts` — add `createRecord`
- `client/src/app/features/maintenance/maintenance-modal/maintenance-modal.component.ts` — new modal component
- `client/src/app/features/maintenance/maintenance.component.ts` — add button + modal wiring

### Acceptance criteria

- [ ] `POST /api/autos/{autoId}/maintenance` creates a record and returns `{ id }`
- [ ] POST enforces auth + ownership, validates required fields
- [ ] "+ Add Record" button opens the modal
- [ ] Modal has dropdown with all maintenance types, date input, odometer, cost, and optional notes
- [ ] Cost field is required
- [ ] Notes field is optional
- [ ] After save, the records list reloads and the new record appears in the table
- [ ] Toast notification confirms creation

---

## Phase 3: Edit + Delete

**User stories**: 13, 14, 15

### What to build

Complete the CRUD. Add `PUT /api/autos/{autoId}/maintenance/{id}` and `DELETE /api/autos/{autoId}/maintenance/{id}` endpoints with auth/ownership guards. Extend `MaintenanceService` with `updateRecord` and `deleteRecord` methods. Wire the modal in edit mode (pre-fills form from existing record). Add Edit and Delete action buttons to each table row. Delete shows a confirmation step before proceeding.

### Key files to modify/create

- `Endpoints/MaintenanceEndpoints.cs` — add PUT + DELETE handlers
- `client/src/app/core/services/maintenance.service.ts` — add `updateRecord`, `deleteRecord`
- `client/src/app/features/maintenance/maintenance-modal/maintenance-modal.component.ts` — edit mode support
- `client/src/app/features/maintenance/maintenance.component.ts` — edit/delete button wiring + confirm dialog

### Acceptance criteria

- [ ] `PUT /api/autos/{autoId}/maintenance/{id}` updates a record, enforces auth + ownership
- [ ] `DELETE /api/autos/{autoId}/maintenance/{id}` deletes a record, enforces auth + ownership
- [ ] Edit button opens modal pre-filled with existing record data
- [ ] After edit save, the table reflects the updated record
- [ ] Delete button shows a confirmation before deleting
- [ ] After delete confirmation, the record is removed from the table
- [ ] Toast notifications confirm edit and delete

---

## Verification

After all phases:
1. **Backend**: Run `dotnet build` to confirm compilation. Run existing tests if present.
2. **Migration**: Start the app and confirm the `MaintenanceRecords` table is created via auto-migration.
3. **Manual E2E**: Log in, select a vehicle, navigate to Maintenance tab, add a record, verify it appears in the table, edit it, delete it with confirmation. Switch vehicles and confirm records are scoped correctly.
4. **Auth**: Verify that unauthenticated requests to `/api/autos/{autoId}/maintenance` return 401, and requests for another user's auto return 403.
