# Gasoholic PWA — Offline Features Guide

## Overview

The Gasoholic Angular app is a Progressive Web App (PWA) with offline-first architecture. Users can create fillups while offline, and the app automatically syncs them to the server when connectivity returns. Data cached during earlier sessions remains accessible offline.

This document covers three core offline capabilities:

1. **Offline Fillup Creation & Sync Queue**
2. **Offline Navigation with Cached Data**
3. **Cross-Tab Sync Communication via BroadcastChannel**

---

## Feature 1: Offline Fillup Creation & Sync Queue

### What it does

When a user creates a fillup while offline:
- The fillup is saved locally to **IndexedDB** (the browser's persistent storage)
- The modal closes and shows a toast: *"Fillup saved offline. Will sync when online."*
- The fillup appears in the table with a **pending** state (visual indicator)
- When connectivity returns, the app automatically syncs all pending fillups to the server
- Once synced, the pending state is removed and the fillup ID is updated

### How it works — Technical flow

#### 1. IndexedDB Storage (SyncQueueService)

The `SyncQueueService` manages an IndexedDB database with the following structure:

```
Database: gasoholic-sync
Object Store: pending-fillups
├─ Key path: id (unique identifier like "pending-1711234567890-0.456")
└─ Each pending fillup contains:
   ├─ id: string (auto-generated)
   ├─ autoId: number (which vehicle)
   ├─ filledAt: string (ISO datetime)
   ├─ fuelType: number (0=Regular, 1=MidGrade, 2=Premium, 3=Diesel, 4=E85)
   ├─ pricePerGallon: number
   ├─ gallons: number
   ├─ odometer: number
   ├─ isPartialFill: boolean
   └─ timestamp: number (when it was queued)
```

#### 2. Creating a fillup offline

When the fillup modal submits and the network is unavailable:

```javascript
// FillupsService.createFillup() fails with network error
try {
  const created = await this.http.post(`/api/autos/${autoId}/fillups`, fillup).toPromise();
  // ... if offline, HttpClient throws error
} catch (err) {
  // Fallback: enqueue to IndexedDB
  const pendingId = await this.syncQueueService.addPending({
    autoId,
    filledAt: fillup.filledAt,
    fuelType: fillup.fuelType,
    pricePerGallon: fillup.pricePerGallon,
    gallons: fillup.gallons,
    odometer: fillup.odometer,
    isPartialFill: fillup.isPartialFill
  });

  // Show pending row in the table with pendingId
  // Toast: "Offline — will sync when online"
}
```

#### 3. Syncing when online

Two mechanisms trigger sync:

**a) Window `online` event (primary)**

```javascript
window.addEventListener('online', async () => {
  const pending = await this.syncQueueService.getPending();
  for (const fillup of pending) {
    try {
      const created = await this.http.post(`/api/autos/${fillup.autoId}/fillups`, {
        filledAt: fillup.filledAt,
        fuelType: fillup.fuelType,
        pricePerGallon: fillup.pricePerGallon,
        gallons: fillup.gallons,
        odometer: fillup.odometer,
        isPartialFill: fillup.isPartialFill
      }).toPromise();

      // Success: remove from queue, update table row with real ID
      await this.syncQueueService.removePending(fillup.id);
      this.updateTableRow(fillup.id, created.id); // Replace pending with real

    } catch (err) {
      // Network error again — leave in queue, will retry next online event
      console.error(`Failed to sync fillup ${fillup.id}:`, err);
    }
  }
});
```

**b) Manual sync button (optional)**

Users can manually trigger sync by clicking a "Sync" button if auto-sync doesn't trigger (e.g., poor connectivity).

### User experience

**Offline scenario:**

1. User is at a gas pump with no cell service
2. Opens the app (previously loaded pages are cached)
3. Clicks "Add Fillup"
4. Fills in the form and submits
5. Toast appears: *"Offline — fillup saved locally. Will sync when online."*
6. Fillup appears in the table with a **⏳ pending** badge
7. User leaves the pump, connectivity returns (wifi at home)
8. Toast appears: *"Syncing fillups..."*
9. Pending badge disappears, fillup now has a permanent ID
10. Toast: *"2 fillups synced"*

**What the user doesn't see:**
- No error message
- No "failed to save" state
- The app just works, offline or online

### Storage limits

IndexedDB is subject to browser quota:
- **Firefox:** 10% of disk (10GB typical)
- **Chrome:** 50% of available disk (up to 30GB typical)
- **Safari:** 50MB per domain

For Gasoholic (small fillup records), a single offline queue can hold 10,000+ fillups before hitting limits.

### Clearing the queue

If a sync fails repeatedly (server error, not network), the user can manually clear the queue:

```javascript
await this.syncQueueService.clearAll(); // Nukes all pending fillups (recovery only)
```

This is intentionally not exposed in the UI — only for edge cases where the backend is corrupted.

---

## Feature 2: Offline Navigation with Cached Data

### What it does

The service worker caches the app shell (HTML, JS, CSS) and API responses. When the user goes offline:
- Previously visited pages still load (no blank screen, no error page)
- Fillup table shows the last loaded data (from cache)
- Navigation between `/app/log` and `/app/autos` works
- API calls that were cached remain available

### How it works — Caching Strategy

The caching strategy is defined in `client/ngsw-config.json`:

```json
{
  "assetGroups": [
    {
      "name": "app",
      "installMode": "prefetch",
      "resources": {
        "files": [
          "/favicon.ico",
          "/index.html",
          "/*.css",
          "/*.js"
        ]
      }
    },
    {
      "name": "assets",
      "installMode": "lazy",
      "resources": {
        "files": ["/assets/**", "/**//*"]
      }
    }
  ],
  "dataGroups": [
    {
      "name": "api",
      "urls": ["/api/**", "/auth/**"],
      "cacheConfig": {
        "strategy": "network-first",
        "maxAge": "1h",
        "maxSize": 100
      }
    }
  ]
}
```

#### Asset Caching

- **`app` group (prefetch):** Downloaded immediately on first visit
  - `index.html` — the root document
  - All `.js` and `.css` bundles
  - `favicon.ico`
  - Kept indefinitely (updates on next app version)

- **`assets` group (lazy):** Downloaded on demand
  - Image files, fonts (in `/assets/**`)
  - Cached only when accessed
  - Useful for reducing initial load time

#### API Caching

- **Strategy:** `network-first` — try the network first, fall back to cache if offline
- **Max age:** 1 hour — cached responses older than 1 hour are refreshed on next request
- **Max size:** 100 responses — keeps the largest 100 API responses, discards older ones
- **URLs cached:** `/api/**` and `/auth/**`

### User experience — Offline navigation

**Scenario: User loads fillups, goes offline, navigates between tabs**

1. User navigates to `/app/log`, table loads (e.g., 5 fillups fetched from API)
2. User switches to `/app/autos` tab
3. User closes browser and goes on a trip (offline)
4. Opens browser, clicks "Add Fillup" button (cached, works)
5. Navigates to `/app/autos` tab (cached, shows last loaded autos)
6. Table shows the same 5 fillups from step 1 (cached from 1 hour ago)
7. Tries to add a new auto → offline, fails gracefully with toast
8. Adds a fillup instead → enqueued to IndexedDB (Feature 1)
9. Reconnects → fillups auto-sync, new auto creation still fails (shows retry button)

### Cache invalidation

**Automatic (time-based):**
- API responses expire after 1 hour
- Next request to an endpoint refreshes the cache
- Users see fresh data within 1 hour of reconnecting

**Manual (app update):**
- Deploying new code increments the service worker version
- Old caches are automatically cleared
- User gets the new app without manual refresh (PWA update prompt appears)

**User-triggered:**
```javascript
// If user clicks "refresh" button in UI
location.reload(); // Force network request, bypass cache
```

---

## Feature 3: Cross-Tab Sync Communication via BroadcastChannel

### What it does

If the user has the app open in multiple browser tabs:
- One tab creates a fillup while offline
- When connectivity returns and sync completes, **all other tabs** are notified
- Other tabs automatically refresh their fillup table to show the newly synced data
- No manual refresh needed

### How it works

#### BroadcastChannel API

The `BroadcastChannel` API allows tabs of the same origin to send messages:

```javascript
const channel = new BroadcastChannel('gasoholic-sync');

// Tab A (completed sync):
channel.postMessage({
  type: 'SYNC_COMPLETED',
  autoId: 5,
  syncedCount: 3
});

// Tab B (listens):
channel.onmessage = async (event) => {
  if (event.data.type === 'SYNC_COMPLETED') {
    // Refresh fillup table for auto 5
    await this.fillupsService.loadFillups(5);
  }
};
```

#### Sync completion flow

1. **Tab A:** User creates fillup offline
   - Fillup stored in IndexedDB (visible only to Tab A)
   - Pending row shows in table

2. **Tab A:** Connectivity returns
   - Service worker detects `online` event
   - Loops through pending fillups, POSTs each to `/api/autos/{autoId}/fillups`
   - For each successful sync:
     - Removes from IndexedDB
     - Broadcasts: `{ type: 'SYNC_COMPLETED', autoId: 5 }`

3. **Tab B:** Receives broadcast message
   - Checks if its current auto ID matches the synced auto
   - Calls `fillupsService.loadFillups(5)` to refresh the table
   - Table updates with the newly synced fillup from the server

4. **Both tabs:** Now in sync without user action

### Browser support

| Browser | Support | Notes |
|---------|---------|-------|
| Chrome/Edge | ✅ Full | All versions |
| Firefox | ✅ Full | All versions |
| Safari | ⚠️ Limited | iOS 15.1+, macOS 12.1+; disabled in private browsing |
| IE 11 | ❌ No | Falls back to single-tab mode |

**Fallback for unsupported browsers:**
- BroadcastChannel is wrapped in a try-catch
- If unsupported, the app still works — just doesn't notify other tabs
- Single-tab users (most) are unaffected

### Edge cases

**Different auto selected in Tab B:**
```javascript
// Tab A syncs auto 5, Tab B is viewing auto 3
if (event.data.autoId === currentlyViewedAutoId) {
  // Only refresh if we're viewing the same auto
  await fillupsService.loadFillups(currentlyViewedAutoId);
}
```

**Offline in Tab B:**
```javascript
// Tab A syncs, Tab B is offline
// BroadcastChannel message arrives (queued by OS)
// Tab B comes online, receives the message
// Tab B calls loadFillups()
// Network request succeeds, table refreshes
```

**Multiple pending fillups in different tabs:**
```javascript
// Tab A: Pending fillup for auto 5
// Tab B: Pending fillup for auto 3
// Both come online, both sync
// Each broadcasts its own SYNC_COMPLETED message
// Both tabs refresh their respective tables
```

---

## Testing Offline Features

### Local testing (browser DevTools)

#### Test Feature 1: Offline fillup creation & sync

1. Open the app in Chrome/Firefox DevTools
2. Go to **Network** tab
3. Check **Offline** checkbox
4. Navigate to `/app/log` (already cached, loads fine)
5. Click "Add Fillup"
6. Fill in form: date, time, price, gallons, odometer
7. Submit
8. **Expected:** Toast "Offline — fillup saved locally"
9. Fillup appears in table with **⏳ pending** badge
10. Go to **Network** tab, **uncheck** Offline
11. **Expected:** Toast "Syncing fillups..." then "1 fillup synced"
12. Pending badge disappears

#### Test Feature 3: Cross-tab sync

1. Open app in **Tab A**
2. Open same URL in **Tab B** (same site)
3. Go offline in DevTools (Tab A or B)
4. In Tab A: Add a fillup
5. In Tab B: Look at the table (should NOT show new fillup yet)
6. Go online
7. **Expected in Tab B:** Table automatically refreshes and shows the new fillup from Tab A

### E2E test coverage

See `e2e/tests/pwa.spec.ts` for current coverage:
- ✅ Service worker registered
- ✅ Manifest.webmanifest served correctly
- ✅ Offline navigation (go offline, navigate to different route, page still loads)
- ⚠️ **NOT YET TESTED:** Offline fillup creation, IndexedDB queue, sync on online
- ⚠️ **NOT YET TESTED:** BroadcastChannel cross-tab messaging

**To add missing tests:** See the [PWA E2E Testing Roadmap](#appendix-pwa-e2e-testing-roadmap) section below.

---

## Troubleshooting

### Fillup stays "pending" after going online

**Cause:** Service worker didn't detect the `online` event or sync failed silently.

**Fix:**
1. Check browser console for errors: `DevTools → Console`
2. Check service worker logs: `DevTools → Application → Service Workers`
3. Manually click "Sync now" button (if implemented)
4. Hard refresh: `Ctrl+Shift+R` (Chrome) or `Cmd+Shift+R` (Mac)

### Offline table shows stale data (> 1 hour old)

**Cause:** API cache expired after 1 hour.

**Fix:**
- Go online and navigate to the route to refresh
- Or manually click "Refresh" button
- Data will auto-refresh within 1 hour of reconnecting

### BroadcastChannel doesn't notify other tabs

**Cause:**
1. Browser doesn't support BroadcastChannel (Safari < 15.1)
2. Other tab is in private/incognito mode (Safari blocks BroadcastChannel in private)
3. Tab isn't subscribed to the channel (code bug)

**Fix:**
- Manually refresh the other tab: `F5`
- Use non-private mode (Safari)
- Check browser console for errors

### IndexedDB not persisting (fillups disappear on reload)

**Cause:**
1. Browser private/incognito mode (IndexedDB disabled)
2. Browser quota exceeded (storage full)
3. User cleared site data (DevTools → Application → Clear site data)

**Fix:**
- Use normal (non-private) browsing mode
- Clear browser cache: DevTools → Application → Clear site data
- Reduce other storage on disk

---

## Performance Impact

### Offline readiness

- **Service worker installation:** ~50 KB extra network (first visit, then cached)
- **IndexedDB lookups:** <1ms per fillup
- **Sync batch processing:** ~200ms per fillup (network dependent)

### Cache size

- **App shell:** ~300 KB (HTML, JS, CSS bundles)
- **API responses:** ~10 KB × 100 max (1 MB total)
- **IndexedDB queue:** ~1 KB per fillup × 10,000 max (10 MB total)

### Typical user impact

- **First visit:** +50 KB download (service worker), then ~2 MB stored locally (one-time)
- **Offline usage:** No network, instant table loads, seamless fillup creation
- **Sync on reconnect:** Depends on queue size; 10 fillups sync in <1 second

---

## Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| IndexedDB for queue (not LocalStorage) | LocalStorage has 5-10 MB limit; IndexedDB scales to 50+ MB |
| Window `online` event + polling (not just BackgroundSync API) | BackgroundSync unsupported on Safari; polling covers all browsers |
| Network-first API cache (not stale-while-revalidate) | Data freshness > instant response; users expect recent fillups |
| BroadcastChannel for cross-tab sync (not localStorage events) | BroadcastChannel is event-driven; localStorage polling is slow |
| Pending row badge (not toast notification) | Persistent visual indicator; user doesn't miss the state |

---

## Appendix: PWA E2E Testing Roadmap

### Test: Offline fillup creation → IndexedDB → sync on online

```typescript
test('offline fillup creation and sync', async ({ page, context }) => {
  await context.addCookies(cookieState.cookies);
  await page.goto('/app/log');

  // Wait for table to load
  await expect(page.locator('tbody tr')).toHaveCount(n);

  // Go offline
  await context.setOffline(true);

  // Open fillup modal and fill in
  await page.click('#addFillupBtn');
  await page.fill('#fillupDate', '2026-03-27');
  await page.fill('#fillupPrice', '3.50');
  await page.fill('#fillupGallons', '10.0');
  await page.fill('#fillupOdometer', '50100');

  // Submit
  await page.click('#fillupForm button[type="submit"]');

  // Check for toast: "Offline"
  await expect(page.locator('[role="alert"]')).toContainText(/offline|saved locally/i);

  // Check that pending row appears
  await expect(page.locator('tbody tr')).toHaveCount(n + 1);
  const pendingRow = page.locator('tbody tr').first();
  await expect(pendingRow).toContainText('⏳'); // Pending badge

  // Inspect IndexedDB to confirm stored
  const pending = await page.evaluate(async () => {
    const req = indexedDB.open('gasoholic-sync');
    return new Promise(resolve => {
      req.onsuccess = () => {
        const tx = req.result.transaction('pending-fillups');
        const store = tx.objectStore('pending-fillups');
        store.getAll().onsuccess = e => resolve(e.target.result);
      };
    });
  });
  expect(pending.length).toBeGreaterThan(0);

  // Go online
  await context.setOffline(false);

  // Wait for sync toast
  await expect(page.locator('[role="alert"]')).toContainText(/synced|sync/i, { timeout: 5000 });

  // Check that pending badge is gone
  await expect(pendingRow).not.toContainText('⏳');

  // Verify table count increased (sync completed)
  await expect(page.locator('tbody tr')).toHaveCount(n + 1);
});
```

### Test: Cross-tab sync via BroadcastChannel

```typescript
test('cross-tab sync notification', async ({ browser }) => {
  const context = await browser.newContext();
  const page1 = await context.newPage();
  const page2 = await context.newPage();

  // Both pages log in
  await devLogin(page1, 'tab1@example.com');
  await devLogin(page2, 'tab2@example.com');

  // Page 1: Navigate to fillup log, load table
  await page1.goto('/app/log');
  const initialCount1 = await page1.locator('tbody tr').count();

  // Page 2: Navigate to same log
  await page2.goto('/app/log');
  const initialCount2 = await page2.locator('tbody tr').count();

  expect(initialCount1).toBe(initialCount2);

  // Page 1: Go offline, create fillup
  await page1.context().setOffline(true);
  await page1.click('#addFillupBtn');
  // ... fill form ...
  await page1.click('#fillupForm button[type="submit"]');

  // Page 2: Fillup not visible yet (offline in page 1, not synced)
  await expect(page2.locator('tbody tr')).toHaveCount(initialCount2);

  // Page 1: Go online, sync happens
  await page1.context().setOffline(false);

  // Page 2: Table should auto-refresh via BroadcastChannel
  await expect(page2.locator('tbody tr')).toHaveCount(initialCount2 + 1, { timeout: 5000 });
});
```

---

## References

- [MDN — Web Workers & Service Workers](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)
- [MDN — IndexedDB](https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API)
- [MDN — BroadcastChannel](https://developer.mozilla.org/en-US/docs/Web/API/Broadcast_Channel_API)
- [Angular — Service Worker](https://angular.io/guide/service-worker-intro)
- [Google — Workbox & ngsw-config](https://developers.google.com/web/tools/workbox)
