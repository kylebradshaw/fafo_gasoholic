# Local Development Quick Start

Copy-paste commands to get Gasoholic running on your machine for testing and development.

---

## First Time Setup (5 minutes)

### Clone the repository

```bash
git clone https://github.com/anthropics/gasoholic.git
cd gasoholic

# Install prerequisites
# - .NET 10 SDK: https://dotnet.microsoft.com/download
# - Node.js 22+: https://nodejs.org/
```

### Start with one command

```bash
./start.sh
```

This **automatically**:
- Installs Angular dependencies
- Starts Angular dev server on http://localhost:4200 (with live reload)
- Starts .NET API on http://localhost:5082
- Proxies `/api/` requests from Angular to .NET

**Open http://localhost:4200** in your browser. Angular dev server will auto-reload when you save files.

**Expected output:**
```
info: Gasoholic.Program[0]
      Now listening on: http://localhost:5082
```

Open **http://localhost:5082** in your browser. The login page loads automatically.

**If you see a blank page or error:**
- Wait 2–3 seconds for the app to fully load
- Check browser console for errors: `F12` → **Console** tab
- Verify Angular was built: check that `wwwroot/browser/index.html` exists
- Hard refresh: `Ctrl+Shift+R` (Chrome) or `Cmd+Shift+R` (Mac)

**Database:** `gasoholic.db` is created automatically on first run (SQLite).

---

## Different Startup Modes

### Development mode (recommended) — fastest iteration

```bash
./start.sh
# or explicitly:
./start.sh --dev
```

- **Angular dev server:** http://localhost:4200 (live reload)
- **.NET API:** http://localhost:5082
- **When to use:** Active development, making frequent changes
- **Open:** http://localhost:4200 in browser

Changes to Angular files rebuild and refresh **instantly**. Backend changes require restarting (Ctrl+C and `./start.sh` again).

### Production mode — test production build

```bash
./start.sh --prod
```

- **Builds Angular** to production bundle in `wwwroot/browser/`
- **Starts .NET** serving static files on http://localhost:5082
- **When to use:** Testing production setup, CI/CD verification
- **Open:** http://localhost:5082 in browser

Single process, single terminal. Tests the exact setup used in deployment.

### Quick mode — minimal startup

```bash
./start.sh --quick
```

- **Just starts .NET** on http://localhost:5082
- **When to use:** Angular already built, just testing backend changes
- **Open:** http://localhost:5082 in browser

Assumes `wwwroot/browser/index.html` already exists.

### Manual two-terminal setup (if preferred)

If you prefer explicit control over two processes:

**Terminal 1:**
```bash
cd client && npm start
# Angular dev server on http://localhost:4200
```

**Terminal 2:**
```bash
dotnet run
# .NET API on http://localhost:5082
```

### Help and options

```bash
./start.sh --help
# Shows all modes, options, and examples
```

Options:
- `--skip-install` — Skip npm install if already done
- `--help` — Show help message

---

## Common Development Tasks

### Add a database migration

```bash
# Create a new migration
dotnet ef migrations add YourMigrationName

# Apply to local database
dotnet ef database update

# Commit the migration code
git add Migrations/
git commit -m "migration: YourMigrationName"
```

### Reset the database (start fresh)

```bash
# Delete the SQLite database
./reset-db.sh
# Or manually:
rm gasoholic.db*

# Next time you run `dotnet run`, a fresh database is created
```

### Build Angular without starting .NET

```bash
cd client
npm run build
# Output goes to ../wwwroot/browser/
```

### Check for TypeScript errors

```bash
cd client
npm run lint
# or for strict type checking:
npm run build -- --configuration=production
```

### Run unit tests (Angular)

```bash
cd client
npm test
```

### Format code (Angular)

```bash
cd client
npm run format
# Prettier runs on all .ts and .html files
```

---

## Testing Locally

### E2E tests (full happy path)

```bash
cd e2e

# One-time setup
npm install
npx playwright install chromium

# Run all tests
npx playwright test

# Run specific test
npx playwright test tests/login.spec.ts

# Debug mode (opens browser)
npx playwright test --debug --headed

# View test results
npx playwright show-report
```

Tests automatically start the .NET backend on port 5100.

### Test offline features manually

1. Open DevTools (`F12`)
2. Go to **Network** tab
3. Check **Offline** checkbox
4. Try creating a fillup
5. **Expected:** Toast says "Offline — fillup saved locally"
6. Fillup appears with **⏳ pending** badge
7. Uncheck **Offline**
8. **Expected:** Toast says "Syncing...", then pending badge disappears

See [PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md) for more offline testing scenarios.

### Test service worker

1. Open DevTools → **Application** → **Service Workers**
2. **Expected:** One registered service worker in "activated and running" state
3. Go **Offline** and refresh
4. **Expected:** Page loads from cache (no error)

---

## Port Reference

| Port | Service | URL |
|------|---------|-----|
| `4200` | Angular dev server (with live reload) | http://localhost:4200 |
| `5082` | .NET API + static files | http://localhost:5082 |
| `5100` | .NET API (for E2E tests) | http://localhost:5100 |

---

## Troubleshooting

### "Cannot find module" when running npm

```bash
cd client
npm install
cd ..
```

### Angular build fails with TypeScript errors

```bash
cd client
npm run build -- --verbose
```

Look for errors in the output. Common issues:
- Missing imports: add `import` statements
- Type errors: check component properties match interfaces
- Service injection: ensure services are provided in root

### Port already in use

```bash
# Find what's using the port
lsof -i :5082
# Kill it (get the PID from above)
kill -9 <PID>
# Then retry
dotnet run
```

### "Database is locked" error

```bash
# Stop dotnet run (Ctrl+C)
./reset-db.sh
dotnet run
```

### "Index out of range" when starting .NET

```bash
# Corrupted database, reset it
./reset-db.sh
dotnet run
```

### E2E tests time out connecting to server

```bash
# Make sure port 5100 is free (tests use this port)
lsof -i :5100
# If something is using it, kill it
kill -9 <PID>
# Then retry
cd e2e && npx playwright test
```

---

## Useful Commands

### Check .NET version

```bash
dotnet --version
```

### Check Node version

```bash
node --version
npm --version
```

### See running processes on a port

```bash
lsof -i :5082
```

### Kill a running .NET process

```bash
# macOS/Linux
pkill -f "dotnet run"

# Windows
taskkill /f /im dotnet.exe
```

### See all git branches

```bash
git branch -a
```

### See recent commits

```bash
git log --oneline -10
```

---

## Development Best Practices

### Commit frequently

```bash
git add .
git commit -m "feat: describe your change"
```

### Create a branch for features

```bash
git checkout -b feature/your-feature-name
# ... make changes ...
git push origin feature/your-feature-name
# Create PR on GitHub
```

### Before pushing, test locally

```bash
# Build everything
cd client && npm run build && cd ..

# Run E2E tests
cd e2e && npx playwright test && cd ..

# Verify dotnet build
dotnet build

# Then push
git push origin your-branch
```

### Database changes require migrations

If you modify `Models/`, update `Data/AppDbContext.cs`, etc.:

```bash
dotnet ef migrations add YourMigrationName
dotnet ef database update
git add Migrations/ Models/ Data/
git commit -m "migration: YourMigrationName"
```

---

## Next Steps

1. **Run locally:** Follow the [First Time Setup](#first-time-setup-5-minutes) section above
2. **Explore the code:**
   - Backend: `Endpoints/`, `Models/`, `Services/`
   - Frontend: `client/src/app/`
   - Tests: `e2e/tests/`
3. **Make a change:** Try adding a feature or fixing a bug
4. **Test it:** Run E2E tests, test offline features, verify the build
5. **Submit a PR:** Push your branch and create a pull request

---

## Documentation

For more details, see:
- **[README.md](README.md)** — Project overview and tech stack
- **[PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md)** — How offline sync works
- **[DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md)** — Link to all docs
- **[.claude/PLAN.md](.claude/PLAN.md)** — All 21 completed tasks

---

## Questions?

- **Stuck?** Check the [Troubleshooting](#troubleshooting) section above
- **Want to deploy?** See [DEPLOYMENT.md](DEPLOYMENT.md)
- **Need to set up Azure?** See [AZURE_SETUP_CHECKLIST.md](AZURE_SETUP_CHECKLIST.md)
- **File an issue** on GitHub

---

**Last updated:** 2026-03-27

Happy coding! 🚀
