# PRD: Vehicle Maintenance History Tracking

## Problem Statement

As a vehicle owner using Gasoholic, I have no way to record when I had maintenance work done on my cars. I track fuel fillups and mileage, but there's nowhere to log an oil change or tire rotation — so I have to rely on paper receipts, the sticker on my windshield, or memory to know what was done and when. I want that history in the same app where I track my fuel.

## Solution

Add a **Maintenance** tab to the app where users can log completed service events for each vehicle. Each record captures the type of service, the date, the odometer reading at the time, and the cost. Notes about the shop or any additional details are optional. The feature is scoped per vehicle, using the same auto-selector already present in the nav bar.

There is no scheduling, reminder, or "next due" logic in this version — the goal is a clean, accurate service history log.

## User Stories

1. As a vehicle owner, I want a dedicated Maintenance tab so that I can access service history separately from my fuel log.
2. As a vehicle owner, I want the Maintenance tab to use the same auto-selector as the rest of the app so that switching vehicles shows the correct history without extra navigation.
3. As a vehicle owner, I want to add a maintenance record so that I can log a completed service event for the currently selected vehicle.
4. As a vehicle owner, I want to select a maintenance type from a fixed list so that my records are consistent and easy to scan.
5. As a vehicle owner, I want Oil Change as an available maintenance type so that I can track my oil change history.
6. As a vehicle owner, I want Tire Rotation as an available maintenance type so that I can track my tire rotation history.
7. As a vehicle owner, I want to record the date a service was performed so that I know when each event happened.
8. As a vehicle owner, I want to record the odometer reading at the time of service so that I can see how many miles were on the car when each service was done.
9. As a vehicle owner, I want to record the cost of a service so that I can track what I've spent on maintenance.
10. As a vehicle owner, I want to optionally add notes to a maintenance record so that I can capture the shop name, technician, or any relevant details.
11. As a vehicle owner, I want to see all maintenance records for the selected vehicle in a table so that I can quickly review the full service history.
12. As a vehicle owner, I want maintenance records sorted by odometer descending (most recent first) so that the latest service is always at the top, consistent with the fillups log.
13. As a vehicle owner, I want to edit an existing maintenance record so that I can correct a mistake after saving.
14. As a vehicle owner, I want to delete a maintenance record so that I can remove an entry logged in error.
15. As a vehicle owner, I want a confirmation step before deleting a maintenance record so that I don't accidentally lose history.
16. As a vehicle owner, I want to see an empty state message when no maintenance records exist so that I understand I need to add my first record.
17. As a vehicle owner, I want the Maintenance tab to be inaccessible (or show a prompt) when no vehicle is selected so that I'm never looking at an ambiguous empty state.
18. As a vehicle owner, I want the cost field to be required so that every record has a financial value attached.
19. As a vehicle owner, I want the notes field to be optional so that I don't have to fill it in when there's nothing extra to say.
20. As a vehicle owner, I want the maintenance type list to include common service types beyond oil change and tire rotation so that I can log other routine work as the list grows.

## Implementation Decisions

### New backend model: `MaintenanceRecord`
- Belongs to an `Auto` (foreign key, cascade delete)
- Fields: `Id`, `AutoId`, `Type` (enum), `PerformedAt` (DateTime UTC), `Odometer` (decimal), `Cost` (decimal), `Notes` (string, nullable)
- Follows the same ownership/auth pattern as `Fillup`: requests are scoped by `AutoId` and the auto must belong to the authenticated user

### New enum: `MaintenanceType`
Initial values (extensible):
- `OilChange`
- `TireRotation`
- `BrakeInspection`
- `AirFilter`
- `CabinFilter`
- `WiperBlades`
- `TireReplacement`
- `BatteryReplacement`
- `Other`

### New API endpoints: `/api/autos/{autoId}/maintenance`
- `GET /api/autos/{autoId}/maintenance` — list all records for the auto, sorted by odometer descending
- `POST /api/autos/{autoId}/maintenance` — create a record
- `PUT /api/autos/{autoId}/maintenance/{id}` — update a record
- `DELETE /api/autos/{autoId}/maintenance/{id}` — delete a record
- All endpoints follow the same auth/ownership guard pattern as `FillupEndpoints`

### Request/response shape
- Request body: `{ type, performedAt, odometer, cost, notes? }`
- Create response: `{ id }` only (same pattern as fillups)
- List response: full record objects including all fields

### EF Core migration
- Add `MaintenanceRecords` table to `AppDbContext`
- Add cascade delete on the `Auto → MaintenanceRecord` relationship

### Frontend: new `MaintenanceService`
- Mirrors `FillupsService`: signals for records list and loading state
- Methods: `loadRecords(autoId)`, `createRecord(autoId, data)`, `updateRecord(autoId, id, data)`, `deleteRecord(autoId, id)`

### Frontend: new `MaintenanceComponent` (tab page)
- Uses the shared auto-selector signal (`currentAutoId`) from `AutosService`
- Loads records reactively via effect when `currentAutoId` changes (same pattern as `FillupsComponent`)
- Table columns: Date, Type, Odometer, Cost, Notes, Actions (Edit / Delete)
- "+ Add Record" button opens `MaintenanceModalComponent`
- Empty state when no records exist for the selected auto

### Frontend: new `MaintenanceModalComponent`
- Shared add/edit modal (same pattern as `FillupModalComponent`)
- Fields: PerformedAt (date input), Type (dropdown), Odometer (number), Cost (number), Notes (textarea, optional)

### Frontend: `MaintenanceTypePipe`
- Display pipe to convert enum value to human-readable label (e.g. `OilChange` → "Oil Change")
- Follows same pattern as the existing `fuelType` pipe

### Routing
- Add `/app/maintenance` route alongside `/app/log` and `/app/autos`
- Add "Maintenance" tab to the `AppShellComponent` nav

## Testing Decisions

**What makes a good test here:** Tests should verify observable behavior through the API contract — correct records returned for the right auto, ownership enforcement (can't read/write another user's records), cascade delete when an auto is deleted. Do not test EF internals or Angular component implementation details.

**Modules to test:**
- `MaintenanceEndpoints` (backend integration tests) — verify CRUD behavior, auth guard, ownership scoping, and cascade delete from auto deletion. Follow the pattern of any existing endpoint tests in the project.
- `MaintenanceService` (frontend unit tests) — verify signal state updates correctly after each CRUD operation. Follow the pattern of `FillupsService` tests if they exist.

## Out of Scope

- Maintenance scheduling, reminders, or "next due" calculations
- Mileage-based or time-based interval configuration
- Notifications or push alerts for upcoming service
- Aggregated cost reporting or charts
- Custom / free-text maintenance types (only the fixed enum list)
- Linking maintenance records to fillup records
- Exporting maintenance history

## Further Notes

- The fixed `MaintenanceType` enum should be designed for easy extension — adding new types in a future version should only require a migration and a frontend label addition, not a schema redesign.
- Cost is stored as a decimal (like `PricePerGallon` in `Fillup`) to avoid floating-point issues.
- The "Other" enum value is included to give users an escape hatch until the list grows to cover their use cases.
- Sort order (odometer descending) matches the fillups list for a consistent mental model.
