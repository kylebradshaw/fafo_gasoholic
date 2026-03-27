# Gasoholic Documentation Index

Complete reference for understanding, operating, and deploying the Gasoholic PWA fuel tracker.

---

## Core Documentation

### [README.md](README.md)
Quick start and project overview. Start here for a 2-minute intro.

### [.claude/PLAN.md](.claude/PLAN.md)
Complete task execution plan (Tasks 1–21). Reference for:
- What features are implemented
- Acceptance criteria for each task
- Architecture decisions

### [.claude/CLAUDE.md](.claude/CLAUDE.md)
Project-specific guidelines for Claude Code. Ignore if you're not using Claude.

---

## Operational Guides

### [DEPLOYMENT.md](DEPLOYMENT.md) ⭐ **Read this first for production**

How to deploy and maintain the live app. Covers:
- Local vs. production architecture (SQLite vs. Azure SQL)
- Daily deployments (just `git push`)
- Smoke testing
- Database migrations
- Email domain setup
- Container image cleanup

**Key sections:**
- [How to deploy](#how-to-deploy) — push code, GitHub Actions handles the rest
- [Azure — what's where](#azure--whats-where-and-why) — architecture diagram
- [Email Domain Setup](#email-domain-setup-task-18) — custom domain for magic links
- [Troubleshooting](#troubleshooting)

---

### [AZURE_SETUP_CHECKLIST.md](AZURE_SETUP_CHECKLIST.md) ⭐ **Use this for first-time setup**

Step-by-step executable guide for provisioning Gasoholic on Azure from scratch.

**When to use this:**
- First time deploying to Azure
- Rebuilding after resource deletion
- Setting up infrastructure from scratch

**What it covers:**
- **Phase 1:** Infrastructure setup (ACR, Container Apps, Key Vault, SQL)
- **Phase 2:** Email configuration (Azure Communication Services + custom domain)
- **Phase 3:** Docker image build and push
- **Phase 4:** Deployment verification (health checks, smoke tests)
- **Phase 5:** GitHub Actions CI/CD setup

**Each step is copy-paste ready:**
```bash
# Example: Run full infrastructure deploy
./deploy.sh --infra-only
```

---

## Feature Documentation

### [PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md) ⭐ **Understand offline capabilities**

Deep dive into how the app works offline. Essential for:
- Offline fillup creation and auto-sync
- Service worker caching strategy
- Cross-tab synchronization
- Local testing of offline features

**Features covered:**

1. **Offline Fillup Creation & Sync Queue**
   - Create fillups while offline → stored in IndexedDB
   - Auto-sync when connectivity returns
   - Pending status indicator in UI

2. **Offline Navigation with Cached Data**
   - Previously visited pages load offline (no blank screen)
   - Data cached for 1 hour
   - Service worker caching strategy explained

3. **Cross-Tab Sync Communication**
   - Multiple browser tabs stay in sync
   - BroadcastChannel messaging
   - Auto-refresh when other tabs sync

**Includes:**
- Technical architecture diagrams (code flow)
- Browser DevTools testing steps
- E2E test examples (copy-paste ready)
- Troubleshooting guide
- Performance impact analysis

---

### [PLAN.md](.claude/PLAN.md) (Tasks 1–21)

Reference for what's implemented. Each task includes:
- Acceptance criteria (all checked ✅)
- Architecture decisions
- Files changed

**Example:**
- **Task 21 — Angular 17+ PWA Migration:** Full Angular rewrite, service worker, offline sync, push notifications
- **Task 18 — ACS Custom Domain:** Email deliverability via `gas.sdir.cc`
- **Task 13 — Email Verification:** Magic link authentication

---

## Development Workflow

### [FAFO.md](.claude/FAFO.md)
Session-by-session log of work completed. Useful for:
- Understanding what changed and when
- Reviewing past decisions
- Tracing bugs to specific sessions

---

## Quick Reference

### Architecture at a glance

| Layer | Tech | Status |
|-------|------|--------|
| **Frontend** | Angular 17+, standalone components, signals | ✅ Complete |
| **Backend** | .NET 10, Minimal API, EF Core | ✅ Complete |
| **Database** | SQLite (local), Azure SQL Server (prod) | ✅ Complete |
| **Auth** | Magic link via Azure Communication Services | ✅ Complete |
| **PWA** | Service worker, offline sync, push notifications | ✅ Complete |
| **Hosting** | Azure Container Apps + ACR | ✅ Complete |
| **CI/CD** | GitHub Actions → `git push` deploys | ✅ Complete |

### Key URLs

| Environment | URL |
|---|---|
| **Live** | https://gas.sdir.cc |
| **Container App** | https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io |
| **Local dev** | http://localhost:5000 |

### File locations

| What | Path |
|---|---|
| Angular app | `/client/` |
| .NET backend | `/Gasoholic.csproj` |
| E2E tests | `/e2e/` |
| Infrastructure | `/infra/main.bicep` |
| Deployment script | `/deploy.sh` |
| Smoke test | `/smoke-test.sh` |

---

## Common Tasks

### Deploy a code change

```bash
git add .
git commit -m "feat: your change"
git push origin main
# GitHub Actions deploys automatically
```

### Run the app locally

```bash
# Terminal 1: Build Angular
cd client
npm run build

# Terminal 2: Run .NET
cd ..
dotnet run
# Open http://localhost:5000
```

### Test offline features

1. Open DevTools (`F12`)
2. Go to **Network** tab
3. Check **Offline**
4. Create a fillup
5. Uncheck **Offline**
6. Watch it sync automatically

### View live app logs

```bash
az containerapp logs show --name gasoholic --resource-group gasoholic-rg --follow
```

### Run smoke test

```bash
SECRET=$(az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv)
./smoke-test.sh https://gas.sdir.cc $SECRET
```

### Add a database migration

```bash
dotnet ef migrations add YourMigrationName
dotnet ef database update
git add .
git commit -m "migration: YourMigrationName"
git push  # Deploys automatically
```

---

## Decision Log

Key architectural decisions and why they were made:

| Decision | Rationale | Doc |
|---|---|---|
| Angular standalone components | Cleaner, no NgModules | [PLAN.md — Task 21](PLAN.md) |
| IndexedDB for offline queue (not LocalStorage) | 50+ MB capacity vs 5–10 MB limit | [PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md) |
| Azure SQL in West US 2 (not East US) | East US quota exhausted at signup | [DEPLOYMENT.md](DEPLOYMENT.md) |
| Service worker `network-first` caching | Data freshness > speed; fresh > stale | [PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md) |
| BroadcastChannel for cross-tab sync | Event-driven, not polling-based | [PWA_OFFLINE_FEATURES.md](PWA_OFFLINE_FEATURES.md) |

---

## Troubleshooting Guide

### App won't build locally

See: [PLAN.md — Task 21](PLAN.md) for Angular build setup

### Offline features not working

See: [PWA_OFFLINE_FEATURES.md — Troubleshooting](PWA_OFFLINE_FEATURES.md#troubleshooting)

### Azure deployment stuck

See: [AZURE_SETUP_CHECKLIST.md — Troubleshooting](AZURE_SETUP_CHECKLIST.md#troubleshooting)

### Emails landing in spam

See: [DEPLOYMENT.md — Email Domain Setup](DEPLOYMENT.md#email-domain-setup-task-18)

### Database migration failed

1. Check logs: `dotnet ef migrations list`
2. Verify connection string is set correctly
3. See [DEPLOYMENT.md — Database migrations](DEPLOYMENT.md#schema-migrations)

---

## Next Steps

### If you're new to this project

1. Read [README.md](README.md) for 2-minute overview
2. Read [PLAN.md](.claude/PLAN.md) to see what's built
3. Run locally: `dotnet run` + `cd client && npm run build`
4. Try offline: Open DevTools → Network → Offline, create a fillup, go online

### If you're deploying to Azure

1. Read [DEPLOYMENT.md](DEPLOYMENT.md) for architecture overview
2. Follow [AZURE_SETUP_CHECKLIST.md](AZURE_SETUP_CHECKLIST.md) step-by-step
3. Run: `./deploy.sh`
4. Verify: `./smoke-test.sh <live-url> <secret>`

### If you're adding a feature

1. Check [PLAN.md](.claude/PLAN.md) — is it Tasks 1–21, or new?
2. If new: create a Task 22+
3. Read [DEPLOYMENT.md — How to deploy](DEPLOYMENT.md#every-day-deploys--just-push)
4. Implement, test locally, push

### If you're investigating a bug

1. Check [FAFO.md](.claude/FAFO.md) for recent session notes
2. Check GitHub issues
3. Run smoke test: `./smoke-test.sh <url>`
4. Check app logs: `az containerapp logs show ...`
5. Refer to relevant doc (offline? auth? database?)

---

## Documentation Maintenance

Keeping docs up to date:

- **After deploying a major feature:** Update [PLAN.md](.claude/PLAN.md) acceptance criteria
- **After changing infrastructure:** Update [AZURE_SETUP_CHECKLIST.md](AZURE_SETUP_CHECKLIST.md)
- **After fixing a bug:** Note it in [FAFO.md](.claude/FAFO.md) session log
- **After adding a new doc:** Add it to this index

---

## Links

- **GitHub:** https://github.com/anthropics/gasoholic
- **Azure Portal:** https://portal.azure.com
- **Live App:** https://gas.sdir.cc
- **Angular Docs:** https://angular.io/docs
- **.NET Docs:** https://learn.microsoft.com/en-us/dotnet/
- **PWA Docs:** https://web.dev/progressive-web-apps/

---

**Last updated:** 2026-03-27

Questions? Check the relevant doc above, then file a GitHub issue.
