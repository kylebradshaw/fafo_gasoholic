#!/usr/bin/env bash
# smoke-test.sh — end-to-end happy path test for deployed Gasoholic app
# Usage: ./smoke-test.sh <base-url> <smoke-test-secret>
# Example: ./smoke-test.sh https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io mysecret
set -euo pipefail

BASE_URL="${1:-}"
SMOKE_SECRET="${2:-}"
TEST_EMAIL="smoke-test-$(date +%s)@example.com"
COOKIE_JAR=$(mktemp)
PASS=0
FAIL=0

cleanup() { rm -f "$COOKIE_JAR"; }
trap cleanup EXIT

# ── Helpers ────────────────────────────────────────────────────────────────

ok() { echo "  PASS  $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL  $1 — $2"; FAIL=$((FAIL + 1)); }

assert_status() {
  local label="$1" expected="$2" actual="$3" body="$4"
  if [ "$actual" = "$expected" ]; then
    ok "$label (HTTP $actual)"
  else
    fail "$label" "expected HTTP $expected, got $actual — body: $body"
  fi
}

req() {
  # req METHOD PATH [-H extra-header] [-d body]
  local method="$1" path="$2"; shift 2
  curl -s -b "$COOKIE_JAR" -c "$COOKIE_JAR" \
    -X "$method" "${BASE_URL}${path}" \
    -H "Content-Type: application/json" \
    "$@"
}

req_status() {
  local method="$1" path="$2"; shift 2
  curl -s -o /tmp/smoke_body -w "%{http_code}" \
    -b "$COOKIE_JAR" -c "$COOKIE_JAR" \
    -X "$method" "${BASE_URL}${path}" \
    -H "Content-Type: application/json" \
    "$@"
}

# ── Preflight ───────────────────────────────────────────────────────────────

if [ -z "$BASE_URL" ]; then
  echo "Usage: $0 <base-url> [smoke-test-secret]"
  echo "Example: $0 https://gasoholic.example.azurecontainerapps.io mysecret"
  exit 1
fi

echo "Smoke testing: $BASE_URL"
echo "Test email:    $TEST_EMAIL"
echo ""

# ── Step 1: Health check ────────────────────────────────────────────────────

echo "[1] Health check"
STATUS=$(req_status GET /health)
BODY=$(cat /tmp/smoke_body)
assert_status "GET /health" "200" "$STATUS" "$BODY"

# ── Step 2: /auth/me without session → 401 ─────────────────────────────────

echo "[2] /auth/me (unauthenticated)"
STATUS=$(req_status GET /auth/me)
assert_status "GET /auth/me unauthenticated" "401" "$STATUS" "$(cat /tmp/smoke_body)"

# ── Step 3: Dev login ───────────────────────────────────────────────────────

echo "[3] Dev login"
if [ -z "$SMOKE_SECRET" ]; then
  fail "POST /auth/dev-login" "no SMOKE_TEST_SECRET provided — cannot authenticate"
else
  STATUS=$(req_status POST /auth/dev-login \
    -H "X-Smoke-Test-Secret: $SMOKE_SECRET" \
    -d "{\"email\":\"$TEST_EMAIL\"}")
  BODY=$(cat /tmp/smoke_body)
  assert_status "POST /auth/dev-login" "200" "$STATUS" "$BODY"
fi

# ── Step 4: /auth/me with session → 200 ────────────────────────────────────

echo "[4] /auth/me (authenticated)"
STATUS=$(req_status GET /auth/me)
BODY=$(cat /tmp/smoke_body)
assert_status "GET /auth/me authenticated" "200" "$STATUS" "$BODY"

# ── Step 5: Create auto ─────────────────────────────────────────────────────

echo "[5] Create auto"
STATUS=$(req_status POST /api/autos \
  -d '{"brand":"Toyota","model":"Smoke Test","plate":"TST001","odometer":10000}')
BODY=$(cat /tmp/smoke_body)
assert_status "POST /api/autos" "201" "$STATUS" "$BODY"
AUTO_ID=$(echo "$BODY" | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')
if [ -z "$AUTO_ID" ]; then
  fail "Extract autoId" "could not parse id from: $BODY"
else
  ok "Extract autoId=$AUTO_ID"
fi

# ── Step 6: Get autos → auto appears ───────────────────────────────────────

echo "[6] List autos"
STATUS=$(req_status GET /api/autos)
BODY=$(cat /tmp/smoke_body)
assert_status "GET /api/autos" "200" "$STATUS" "$BODY"
if echo "$BODY" | grep -q "Smoke Test"; then
  ok "Auto appears in list"
else
  fail "Auto appears in list" "not found in: $BODY"
fi

# ── Step 7: Add first fillup (full tank, odometer 10000) ───────────────────

echo "[7] Add first fillup (base, no MPG expected)"
STATUS=$(req_status POST "/api/autos/$AUTO_ID/fillups" \
  -d "{\"filledAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",\"fuelType\":0,\"pricePerGallon\":3.50,\"gallons\":10.0,\"odometer\":10000,\"isPartialFill\":false}")
BODY=$(cat /tmp/smoke_body)
assert_status "POST fillup 1" "201" "$STATUS" "$BODY"
FILLUP1_ID=$(echo "$BODY" | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')

# ── Step 8: Add second fillup (full tank, 300 miles later → MPG = 30) ──────

echo "[8] Add second fillup (expect MPG ≈ 30)"
STATUS=$(req_status POST "/api/autos/$AUTO_ID/fillups" \
  -d "{\"filledAt\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",\"fuelType\":0,\"pricePerGallon\":3.60,\"gallons\":10.0,\"odometer\":10300,\"isPartialFill\":false}")
BODY=$(cat /tmp/smoke_body)
assert_status "POST fillup 2" "201" "$STATUS" "$BODY"
FILLUP2_ID=$(echo "$BODY" | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')

# ── Step 9: Get fillups → MPG computed ─────────────────────────────────────

echo "[9] Get fillups (verify MPG)"
STATUS=$(req_status GET "/api/autos/$AUTO_ID/fillups")
BODY=$(cat /tmp/smoke_body)
assert_status "GET /api/autos/$AUTO_ID/fillups" "200" "$STATUS" "$BODY"
if echo "$BODY" | grep -q '"mpg":30'; then
  ok "MPG = 30 computed correctly"
elif echo "$BODY" | grep -qP '"mpg":\d+'; then
  ok "MPG is computed (non-null)"
else
  fail "MPG computed" "mpg not found in: $BODY"
fi

# ── Step 10: Edit auto ──────────────────────────────────────────────────────

echo "[10] Edit auto"
STATUS=$(req_status PUT "/api/autos/$AUTO_ID" \
  -d '{"brand":"Toyota","model":"Smoke Test Updated","plate":"TST001","odometer":10300}')
BODY=$(cat /tmp/smoke_body)
assert_status "PUT /api/autos/$AUTO_ID" "200" "$STATUS" "$BODY"

# ── Step 11: Delete one fillup ──────────────────────────────────────────────

echo "[11] Delete fillup 1"
STATUS=$(req_status DELETE "/api/autos/$AUTO_ID/fillups/$FILLUP1_ID")
assert_status "DELETE fillup 1" "204" "$STATUS" ""

# ── Step 12: Delete auto (cascades fillups) ─────────────────────────────────

echo "[12] Delete auto"
STATUS=$(req_status DELETE "/api/autos/$AUTO_ID")
assert_status "DELETE /api/autos/$AUTO_ID" "204" "$STATUS" ""

# ── Step 13: Logout ─────────────────────────────────────────────────────────

echo "[13] Logout"
STATUS=$(req_status POST /auth/logout)
assert_status "POST /auth/logout" "200" "$STATUS" ""

# ── Step 14: /auth/me after logout → 401 ────────────────────────────────────

echo "[14] /auth/me after logout"
STATUS=$(req_status GET /auth/me)
assert_status "GET /auth/me post-logout" "401" "$STATUS" ""

# ── Summary ─────────────────────────────────────────────────────────────────

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Results: $PASS passed, $FAIL failed"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

[ "$FAIL" -eq 0 ] && echo "  ALL TESTS PASSED" && exit 0
echo "  SMOKE TEST FAILED" && exit 1
