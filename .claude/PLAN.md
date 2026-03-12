# Gasoholic — App Spec & Execution Plan

## Context

Building a local-only .NET 10 ASP.NET Core API + Vanilla HTML/JS frontend for tracking vehicle fuel fillups. The app is demo-grade: email-only login (no password), cookie-based sessions, SQLite via EF Core. Users can own multiple autos, each with its own fillup log showing MPG computed per entry.

This plan is structured as sequential todo items with acceptance criteria. Each task is optimized for use with the ralph-loop plugin.

**How to run all remaining tasks sequentially:**
```
/ralph-loop "$(cat .claude/PLAN.md)" --completion-promise "TESTS COMPLETE"
```

**Loop behavior:** Each iteration, find the first task that still has unchecked `[ ]` acceptance criteria. Implement it fully, check off each criterion as it passes, write or update its Playwright e2e test, then create the git commit. Do NOT output the completion promise until every task (1–10) has all criteria checked `[x]` and all commits exist.

---

## Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| API style | ASP.NET Core Minimal API |
| ORM | EF Core 10 + SQLite provider |
| Auth | Cookie-based session (`AddSession` + in-memory or DB-backed) |
| Frontend | Vanilla HTML + CSS + JS (no build step, no framework) |
| RWD | CSS Grid / Flexbox, mobile-first |

---

## Data Model

### Users
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Email | string | unique, login key |

### Autos
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| UserId | int FK → Users | |
| Brand | string | e.g. Toyota |
| Model | string | e.g. Camry |
| Plate | string | license plate |
| Odometer | decimal | current odometer reading |

### Fillups
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| AutoId | int FK → Autos | |
| FilledAt | DateTime | date + time of fillup |
| Location | string? | station name / address from reverse geocode |
| Latitude | double? | from browser GPS |
| Longitude | double? | from browser GPS |
| FuelType | enum | Regular, MidGrade, Premium, Diesel, E85 |
| PricePerGallon | decimal | |
| Gallons | decimal | |
| Odometer | decimal | at time of fillup |
| IsPartialFill | bool | tank not filled to full |

**Computed (not stored):** `MPG = (this.Odometer - prev.Odometer) / this.Gallons` — only meaningful when `IsPartialFill = false` on the current entry.

---

## API Endpoints

```
POST   /auth/login           { email }           → 200 + Set-Cookie
POST   /auth/logout                              → 200 + clears cookie
GET    /auth/me                                  → { email }

GET    /api/autos                                → Auto[]
POST   /api/autos            { brand, model, plate, odometer }
PUT    /api/autos/{id}       { brand, model, plate, odometer }
DELETE /api/autos/{id}

GET    /api/autos/{autoId}/fillups               → FillupRow[] (newest first, includes mpg)
POST   /api/autos/{autoId}/fillups   { ... }
PUT    /api/autos/{autoId}/fillups/{id}  { ... }
DELETE /api/autos/{autoId}/fillups/{id}
```

---

## Frontend Pages

1. **`/` (index.html)** — Login: email input + submit → redirect to `/app.html`
2. **`/app.html`** — Shell: nav with auto selector dropdown + logout link
   - **Log tab** (default): fillup table, newest first, `+ Add Fillup` button
   - **Autos tab**: auto cards with edit/delete, `+ Add Auto` button
3. **Modals / inline forms** for add/edit of both Autos and Fillups

### Fillup Table Columns
`Date/Time | Location | Fuel Type | $/gal | Gal | Odometer | MPG*`

`*` shows `—` for partial fills or when no prior full fill exists.

### GPS flow
Browser `navigator.geolocation.getCurrentPosition()` → coordinates stored → reverse geocode via browser-side call to `https://nominatim.openstreetmap.org/reverse` → populate Location field as display hint (editable).

---

## Execution Todo List

> Each task requires all acceptance criteria to pass before closing.

---

### Task 1 — Project Scaffold

**Work:**
- `dotnet new webapi -n Gasoholic --no-openapi` inside repo root
- Add packages: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`
- Configure `Program.cs`: add session middleware, CORS for `localhost` dev, static file serving from `wwwroot/`
- Add `appsettings.json` with SQLite connection string pointing to `gasoholic.db`

**Acceptance criteria:**
- [x] `dotnet run` starts without errors
- [x] `curl http://localhost:5000/` returns 200 or 404 (not 500)
- [x] `gasoholic.db` file is created on first run (empty schema OK at this stage)
- [x] git commit created: `feat: task 1 — project scaffold`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 2 — EF Core Data Model + Migrations

**Work:**
- Create `Models/` folder: `User.cs`, `Auto.cs`, `Fillup.cs` with all columns from spec
- Create `Data/AppDbContext.cs` with all three `DbSet<>` and unique index on `User.Email`
- Add `FuelType` enum to `Models/Enums.cs`
- Run `dotnet ef migrations add InitialCreate` and `dotnet ef database update`

**Acceptance criteria:**
- [x] `dotnet ef migrations list` shows `InitialCreate` as applied
- [x] SQLite DB contains tables `Users`, `Autos`, `Fillups` with correct columns (verify via `sqlite3 gasoholic.db .schema`)
- [x] No EF warnings about unmapped properties at startup
- [x] git commit created: `feat: task 2 — data model and migrations`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 3 — Auth Endpoints

**Work:**
- `POST /auth/login`: look up or create `User` by email, store `userId` in session, return 200
- `POST /auth/logout`: clear session, return 200
- `GET /auth/me`: return `{ email }` if session valid, else 401
- Add `RequireAuth` helper / filter reused by all `/api/*` routes

**Acceptance criteria:**
- [x] `POST /auth/login` with `{ "email": "test@test.com" }` returns 200 and `Set-Cookie` header
- [x] `GET /auth/me` with that cookie returns `{ "email": "test@test.com" }`
- [x] `GET /auth/me` without cookie returns 401
- [x] `POST /auth/logout` clears session; subsequent `GET /auth/me` returns 401
- [x] git commit created: `feat: task 3 — auth endpoints`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 4 — Autos API

**Work:**
- `GET /api/autos` — return all autos for session user
- `POST /api/autos` — create auto, link to session user
- `PUT /api/autos/{id}` — update; 403 if not owner
- `DELETE /api/autos/{id}` — delete (cascade fillups); 403 if not owner

**Acceptance criteria:**
- [x] POST creates an auto; GET returns it in the list
- [x] PUT updates brand/model/plate/odometer
- [x] DELETE removes the auto and returns 204
- [x] A second user (different session) cannot PUT/DELETE another user's auto (returns 403)
- [x] git commit created: `feat: task 4 — autos API`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 5 — Fillups API

**Work:**
- `GET /api/autos/{autoId}/fillups` — return fillups newest-first; compute `mpg` per row
- `POST /api/autos/{autoId}/fillups` — create fillup
- `PUT /api/autos/{autoId}/fillups/{id}` — update
- `DELETE /api/autos/{autoId}/fillups/{id}` — delete

**MPG computation logic (server-side):**
- Sort all fillups for auto by `Odometer` ascending
- For each fillup, find the most recent prior full fill (`IsPartialFill = false`)
- `mpg = (this.Odometer - prior.Odometer) / sum of gallons between them` — or null if no prior full fill

**Acceptance criteria:**
- [x] POST fillup, then GET returns it as first row
- [x] Two consecutive full fills: GET shows correct MPG on second entry
- [x] Partial fill shows `null` mpg in response
- [x] DELETE fillup returns 204; it no longer appears in GET
- [x] Auto must belong to session user; otherwise 403
- [x] git commit created: `feat: task 5 — fillups API with MPG`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test or curl) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 6 — Login Page (`wwwroot/index.html`)

**Work:**
- Clean single-column centered form: email input + "Sign In" button
- On submit: `POST /auth/login`, then redirect to `/app.html`
- On load: `GET /auth/me` — if already logged in, redirect immediately
- RWD: looks correct on 375px and 1280px

**Acceptance criteria:**
- [x] Entering email and clicking Sign In redirects to `/app.html`
- [x] Visiting `index.html` when already logged in skips to `/app.html`
- [x] Form is usable on mobile (375px width) without horizontal scroll
- [x] git commit created: `feat: task 6 — login page`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 7 — App Shell + Autos Management (`wwwroot/app.html`)

**Work:**
- Nav bar: app name, auto selector `<select>`, logout button
- Two tabs: "Log" and "Autos"
- **Autos tab**: list of user's autos as cards (brand + model + plate + odometer); each card has Edit and Delete buttons; `+ Add Auto` opens modal form
- Modal form: brand, model, plate, odometer fields; Save and Cancel
- On logout: `POST /auth/logout` → redirect to `index.html`
- On load: `GET /auth/me` → if 401 redirect to `index.html`

**Acceptance criteria:**
- [ ] Adding an auto via modal appears in the list and in the nav selector
- [ ] Editing an auto updates the card
- [ ] Deleting an auto removes it from list and selector
- [ ] Logout clears session and redirects to login
- [ ] Unauthenticated visit to `/app.html` redirects to `index.html`
- [ ] git commit created: `feat: task 7 — app shell and autos management`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 8 — Fillup Log Tab

**Work:**
- When an auto is selected: `GET /api/autos/{autoId}/fillups` → render table
- Table columns: `Date/Time | Location | Fuel Type | $/gal | Gal | Odometer | MPG`
- MPG cell: display value to 1 decimal, or `—` if null
- `+ Add Fillup` button opens add-fillup modal
- Each row: Edit (pencil icon) and Delete (trash icon)
- Auto selector change reloads the table for the newly selected auto

**Acceptance criteria:**
- [ ] Table renders all fillups for selected auto, newest first
- [ ] Switching auto selector reloads the table with correct data
- [ ] MPG shows computed value or `—` correctly
- [ ] Delete row removes it without page reload
- [ ] Table is scrollable/readable on 375px mobile
- [ ] git commit created: `feat: task 8 — fillup log tab`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 9 — Add/Edit Fillup Modal + GPS

**Work:**
- Modal fields: Date (date input, default today), Time (time input, default now), Location (text, pre-populated by GPS), Fuel Type (`<select>`: Regular, Mid-grade, Premium, Diesel, E85), Price/gal (number), Gallons (number), Odometer (number), Partial Fill (checkbox)
- On modal open: call `navigator.geolocation.getCurrentPosition()` → POST coords to reverse-geocode via Nominatim → populate Location field; show spinner while loading; gracefully handle denial
- On save: POST or PUT to API, close modal, refresh table

**Acceptance criteria:**
- [ ] Opening the modal triggers GPS prompt; Location populates with a human-readable string if permission granted
- [ ] If GPS denied, Location field is empty and editable
- [ ] Saving a new fillup appends it to the top of the log
- [ ] Editing an existing fillup updates the row in place
- [ ] All required fields validated client-side before submit (no blank price/gal/odometer)
- [ ] git commit created: `feat: task 9 — add/edit fillup modal with GPS`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 10 — Polish & RWD Pass

**Work:**
- Ensure consistent spacing, typography, and color on all pages
- Verify table horizontal scroll on narrow viewports
- Add basic loading states (skeleton or spinner) during API calls
- Ensure modal is full-screen on mobile, centered card on desktop
- Test happy path end-to-end: login → add auto → add 3 fillups → verify MPG → edit → delete

**Acceptance criteria:**
- [ ] No layout breakage at 375px, 768px, 1280px
- [ ] End-to-end happy path completes without errors in browser console
- [ ] All API errors surface a visible (non-alert) error message to user
- [ ] `dotnet run` + open browser → fully functional with zero manual setup steps beyond that
- [ ] git commit created: `feat: task 10 — polish and RWD pass`

**Completion signal:** When all acceptance criteria above are satisfied (verified via Playwright e2e test covering the full happy path) and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

## Loop Execution Notes

**Start the full sequential loop (Tasks 5–10) with:**
```
/ralph-loop "$(cat .claude/PLAN.md)" --completion-promise "TESTS COMPLETE"
```

- Each iteration: find the first task with unchecked `[ ]` criteria → implement → check off each criterion → write/run Playwright test → **git commit** → loop again
- Only Task 10's completion emits `<promise>TESTS COMPLETE</promise>` to end the loop
- Commit message format: `feat: task N — <short description>`
- Tasks 1–4 are already complete; the loop will skip them automatically (all `[x]`)
- MPG logic in Task 5 is the most complex — get it right before the UI builds on top
