# Gasoholic — Local Development Guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)
- No other installs needed — SQLite is bundled via NuGet, no separate DB server required

## Running locally

```bash
dotnet run
```

Open [http://localhost:5000](http://localhost:5000). The SQLite database (`gasoholic.db`) is created automatically on first run via `Database.Migrate()` in `Program.cs`.

Email magic links are logged to the console in development — no real email is sent. Click the link from the terminal output to log in.

## Environment variables

| Variable | Default | Notes |
|---|---|---|
| `DATABASE_PROVIDER` | `sqlite` | `sqlite` or `sqlserver` |
| `ConnectionStrings__DefaultConnection` | `Data Source=gasoholic.db` | SQLite path |
| `CORS_ORIGINS` | `http://localhost:5000,https://localhost:5001` | Comma-separated allowed origins |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Controls email sending, session behavior |

In `Development` mode: magic links print to console, sessions use in-memory cache, SQLite is the default database.

## Build

```bash
dotnet build          # debug
dotnet build -c Release
```

## Testing the container locally

```bash
# Build (Apple Silicon — arm64):
docker buildx build --platform linux/arm64 -t gasoholic --load .

# Run:
docker run --platform linux/arm64 \
  -e DATABASE_PROVIDER=sqlite \
  -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/gasoholic.db" \
  -p 8080:8080 gasoholic

# Health check:
curl http://localhost:8080/health   # → {"status":"ok"}
```

---

## Project structure

```
gasoholic.csproj          .NET 10 project file — packages, SDK version
Program.cs                Entry point: DI registration, middleware, route mapping
appsettings.json          Base config (SQLite connection string, session timeout)
appsettings.Development.json  Dev-only overrides

Data/
  AppDbContext.cs         EF Core DbContext — all three DbSets, model config

Endpoints/
  AuthEndpoints.cs        /auth/login, /auth/logout, /auth/me, /auth/verify, /auth/resend
  AutoEndpoints.cs        /api/autos CRUD
  FillupEndpoints.cs      /api/autos/{id}/fillups CRUD + MPG computation
  SmokeTestEndpoints.cs   /auth/dev-login (only active when SMOKE_TEST_SECRET is set)

Models/
  User.cs                 User entity (Id, Email, EmailVerified, CreatedAt)
  Auto.cs                 Auto entity (Brand, Model, Plate, Odometer)
  Fillup.cs               Fillup entity (all fuel fields, GPS coords, partial fill flag)
  VerificationToken.cs    Magic link token entity (GUID, expiry, used timestamp)
  Enums.cs                FuelType enum: Regular, MidGrade, Premium, Diesel, E85

Migrations/               EF Core migration files — never edit by hand
  *_InitialCreate.cs      Base schema (Users, Autos, Fillups)
  *_AddEmailVerification.cs  Adds EmailVerified + VerificationTokens table

wwwroot/
  index.html              Login page — email input, magic link pending state
  app.html                Main app shell — auto selector, Log tab, Autos tab

infra/
  main.bicep              Azure infrastructure as code (ACR, Container App, KV, ACS)

.github/
  workflows/
    azure-deploy.yml      CI/CD: build image → push to ACR → update Container App → smoke test

Dockerfile                Multi-stage build: SDK → publish → aspnet runtime, port 8080
smoke-test.sh             End-to-end bash test script against any deployed URL
deploy.sh                 One-command provision + deploy to Azure
```

## How code flows from dev to production

1. **Write** — edit `.cs` files in `Endpoints/`, `Models/`, `Data/`; edit HTML/JS in `wwwroot/`
2. **Test locally** — `dotnet run`, hit `localhost:5000`
3. **Commit + push to `main`** — GitHub Actions picks it up automatically
4. **CI builds** — `docker buildx build --platform linux/amd64` using the `Dockerfile`
5. **CI pushes** — image tagged with the git short SHA → `gasoholicacr.azurecr.io/gasoholic:<sha>`
6. **CI deploys** — `az containerapp update --image <new-tag>` creates a new Container App revision
7. **Smoke test runs** — `smoke-test.sh` hits the live URL, verifies all 14 steps pass

Schema changes require an EF Core migration:

```bash
dotnet ef migrations add <MigrationName>
# commit the generated file in Migrations/
# migrations run automatically on startup via Database.Migrate() in Program.cs
```
