# Gasoholic

A Progressive Web App (PWA) for tracking vehicle fuel consumption and MPG across multiple vehicles.

**Live:** https://gas.sdir.cc
**Source:** This repository

**Stack:** Angular 17+ · .NET 10 · EF Core · SQLite (local) / Azure SQL (production)

## Quick Start

```bash
# Build Angular frontend
cd client && npm install && npm run build && cd ..

# Start .NET backend (serves Angular app)
dotnet run

# Open http://localhost:5082
```

See [Running locally](#running-locally) below for detailed setup.

## Documentation

| Document | Purpose |
|---|---|
| **[DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md)** | Navigation hub for all docs |
| **[PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md)** | How offline sync, caching, and cross-tab messaging work |
| **[DEPLOYMENT.md](DEPLOYMENT.md)** | Production deployment on Azure |
| **[AZURE_SETUP_CHECKLIST.md](AZURE_SETUP_CHECKLIST.md)** | Step-by-step Azure infrastructure setup |
| **[.claude/PLAN.md](.claude/PLAN.md)** | Complete task list (Tasks 1–21, all complete) |

## Tech Stack

### Backend
| Technology | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0.102 | Runtime & development environment |
| **ASP.NET Core** | 10.0 | Minimal API framework |
| **Entity Framework Core** | 10.0.4/10.0.5 | ORM for database operations |
| **SQLite** | (via EF Core) | Primary database (local dev) |
| **SQL Server** | (optional) | Production database (Azure) |
| **Azure Communication Email** | 1.0.1 | Transactional email for magic link auth |
| **Distributed Caching** | Built-in | Session storage & cache layer |

### Frontend
| Technology | Version | Purpose |
|---|---|---|
| **Angular** | 17+ | Standalone components, signals, reactive state |
| **TypeScript** | 5.x+ | Type-safe application code |
| **Angular PWA** | Built-in | Service worker, offline sync, caching |
| **Reactive Forms** | Built-in | Form validation & auto/fillup modals |
| **RxJS** | Built-in | Reactive programming patterns |
| **Progressive Web App** | W3C-compliant | Offline capability, installable, push notifications |
| **IndexedDB** | Browser API | Offline fillup queue persistence |
| **Service Worker** | Angular NGSW | Asset caching, offline fallback, updates |

### Testing & Quality Assurance
| Technology | Version | Purpose |
|---|---|---|
| **Playwright** | 1.52.0 | End-to-end browser automation testing |
| **Node.js** | 22.0+ (via @types/node) | E2E test environment |
| **C# Integration Tests** | MSTest | 67 comprehensive test cases |

### Infrastructure & DevOps
| Technology | Purpose |
|---|---|
| **Docker** | Containerized deployment |
| **Azure Key Vault** | Secrets management & smoke testing |
| **Session-based Auth** | HttpOnly cookie-based authentication |
| **CORS** | Cross-origin resource sharing (configurable) |
| **Forwarded Headers** | Proxy support for load balancers |

### Architecture Patterns
- **Minimal APIs** – Lightweight endpoint declarations
- **Dependency Injection** – Built-in DI container
- **Environment-specific Configuration** – Development/Production profiles
- **Database Migrations** – Auto-run on startup
- **Hosting Services** – Background verification cleanup tasks

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [npm 10+](https://www.npmjs.com/) (comes with Node.js)

## Running locally

### Quick start (one-time setup)

```bash
# 1. Build the Angular frontend
cd client
npm install
npm run build
cd ..

# 2. Start the .NET backend
dotnet run
```

The app starts on **`http://localhost:5082`**. Open it in your browser — the login page loads automatically.

**⚠️ If you see a blank page or error:**
- Verify Angular built correctly: check that `wwwroot/browser/index.html` exists
- Hard refresh your browser: `Ctrl+Shift+R` (Chrome) or `Cmd+Shift+R` (Mac)
- Check browser console (`F12` → **Console**) for errors
- Wait 2–3 seconds for the page to load

The SQLite database (`gasoholic.db`) is created on first run. No manual migrations needed.

### What happens

- **`npm run build`** in `/client/` compiles Angular and outputs to `../wwwroot/browser/`
- **`dotnet run`** starts the .NET API, which serves the Angular app from `wwwroot/` as static files
- Routes like `/app/log` and `/app/autos` are handled by Angular (SPA routing via fallback to `index.html`)
- API routes like `/api/autos` are handled by .NET

### Development workflow (faster rebuilds)

If you're actively developing, use two terminal windows:

**Terminal 1 — Angular dev server (with live reload):**
```bash
cd client
npm start
# Runs on http://localhost:4200
# Auto-rebuilds on file changes
```

**Terminal 2 — .NET backend:**
```bash
dotnet run
# API on http://localhost:5082
```

Then open `http://localhost:4200` in your browser. Angular dev server proxies `/api/` requests to the .NET backend.

**Note:** The .NET dev server (Terminal 2) also serves the Angular app at `http://localhost:5082`, but with slower rebuild times. Use `localhost:4200` for faster iteration.

### Production-like local testing

If you want to test the exact production setup (single `dotnet run`):

```bash
# Build Angular once
cd client && npm run build && cd ..

# Start .NET (serves both API + Angular frontend)
dotnet run

# Open http://localhost:5082
```

This is how the Docker container runs — single process, both backend + frontend.

## Resetting the database

```bash
./reset-db.sh
```

Deletes `gasoholic.db` (and any WAL files) after confirmation. A fresh database is created automatically on the next `dotnet run`.

Alternatively, just delete the file manually:
```bash
rm gasoholic.db*
```

## Troubleshooting

### "Cannot find module" or npm errors

```bash
cd client
npm install
cd ..
```

### Angular not building

```bash
cd client
npm run build --verbose
cd ..
```

Check for TypeScript errors. If service worker issues, ensure `@angular/service-worker` is installed:
```bash
npm list @angular/service-worker
```

### Port already in use

If `dotnet run` fails with "Address already in use", the port 5082 is occupied:

```bash
# Find what's using port 5082
lsof -i :5082

# Kill the process (macOS/Linux)
kill -9 <PID>

# Then retry: dotnet run
```

### Database locked

If you get "database is locked" errors:

```bash
# Stop dotnet run (Ctrl+C)
# Then:
./reset-db.sh
# And start again:
dotnet run
```

This deletes the corrupted database and creates a fresh one.

### "Index out of range" or migration errors

```bash
# Verify migrations applied
dotnet ef migrations list

# If needed, reset to clean state
./reset-db.sh
```

### Tests time out or fail to connect

```bash
# Make sure .NET is not already running on port 5100
# (playwright.config.ts uses port 5100 for tests)

# Then run tests:
cd e2e && npm test
```

If tests still fail, check that port 5100 is free:
```bash
lsof -i :5100
```

## Running e2e tests

### One-time setup

```bash
# Build Angular first
cd client && npm run build && cd ..

# Install Playwright dependencies
cd e2e
npm install
npx playwright install chromium
```

### Run tests

```bash
cd e2e
npx playwright test
```

**Expected behavior:**
- Playwright starts the .NET backend automatically (via `playwright.config.ts`)
- Runs all test suites against http://localhost:5100
- Reports pass/fail for each test

**Test coverage:**
- Login flow with magic links
- Auto management (create, edit, delete)
- Fillup log (add, edit, delete)
- PWA features (service worker, offline fallback, manifest)
- Responsive design (mobile, tablet, desktop viewports)
- Full happy-path user journey

### Run specific test

```bash
npx playwright test tests/login.spec.ts
```

### Debug mode (opens browser)

```bash
npx playwright test --debug --headed
```

### View test results

```bash
npx playwright show-report
```

## Testing offline features

The app is a Progressive Web App (PWA) with offline capabilities. Test them locally:

### 1. Offline fillup creation & sync

1. Start the app: `dotnet run` (after `npm run build`)
2. Open `http://localhost:5082` in Chrome/Firefox
3. Log in (use any email)
4. Open **DevTools** (`F12`)
5. Go to **Network** tab
6. Check **Offline** checkbox
7. Click "Add Fillup"
8. Fill in the form and submit
9. **Expected:** Toast says "Offline — fillup saved locally"
10. Fillup appears in table with **⏳ pending** badge
11. Go back to **Network**, **uncheck Offline**
12. **Expected:** Toast says "Syncing...", pending badge disappears
13. Fillup now has a permanent ID

See [PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md) for detailed offline feature documentation.

### 2. Offline navigation

1. Load `/app/log` (to cache the page)
2. Go **Offline** in DevTools
3. Click to `/app/autos` tab
4. **Expected:** Page loads (cached), autos appear
5. Go **Online**
6. **Expected:** Page refreshes with fresh data

### 3. Service worker

1. Open **DevTools** → **Application** → **Service Workers**
2. **Expected:** One registered service worker in "activated and running" state
3. Go **Offline** and navigate
4. **Expected:** Pages load from cache (no network error)

### Troubleshooting offline features

If offline features don't work:
1. Ensure you ran `npm run build` (service worker is bundled in the dist)
2. Hard refresh: `Ctrl+Shift+R` (Chrome) or `Cmd+Shift+R` (Mac)
3. Clear site data: DevTools → Application → Clear storage
4. Check browser console for errors

## Smoke test (production deployment)

To test the live deployment on Azure:

```bash
SECRET=$(az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv)
./smoke-test.sh https://gas.sdir.cc $SECRET
```

This runs 14 steps covering the full happy path: health check, login, create auto, add fillups, verify MPG, edit, delete, logout.