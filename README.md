# Gasoholic

A local fuel fillup tracker. Track MPG across multiple vehicles.

Source code powering https://gas.sdir.cc

**Stack:** .NET 10 Minimal API · EF Core · SQLite · Vanilla HTML/JS

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