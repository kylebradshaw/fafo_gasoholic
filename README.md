# Gasoholic

A local fuel fillup tracker. Track MPG across multiple vehicles.

Source code powering https://gas.sdir.cc

**Stack:** .NET 10 Minimal API · EF Core · SQLite · Vanilla HTML/JS

## Tech Stack

### Backend
| Technology | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0.102 | Runtime & development environment |
| **ASP.NET Core** | 10.0 | Minimal API framework |
| **Entity Framework Core** | 10.0.4/10.0.5 | ORM for database operations |
| **SQLite** | (via EF Core) | Primary database |
| **SQL Server** | (optional provider) | Production database alternative |
| **Azure Communication Email** | 1.0.1 | Transactional email for verification |
| **Distributed Caching** | Built-in | Session storage & cache layer |

### Frontend
| Technology | Purpose |
|---|---|
| **Vanilla HTML/CSS/JavaScript** | No framework – lightweight static UI |
| **Progressive Web App (PWA)** | Offline capability & installable app |
| **System-ui Font Stack** | Native font rendering across platforms |
| **Responsive Design** | Mobile-first with viewport constraints |

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

## Running locally

```bash
dotnet run
```

The API starts on `http://localhost:5082`. Open that URL in your browser — the login page loads automatically.

The SQLite database (`gasoholic.db`) is created on first run. No migrations or setup steps required.

## Resetting the database

```bash
./reset-db.sh
```

Deletes `gasoholic.db` (and any WAL files) after confirmation. A fresh database is created automatically on the next `dotnet run`.

## Running e2e tests

```bash
cd e2e
npm install
npx playwright install chromium
npx playwright test
```

Tests start the server automatically if it isn't already running.

## Smoke test

  az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv

  Then run:

  ./smoke-test.sh <website> <secret>