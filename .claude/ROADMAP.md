# Gasoholic — Product Roadmap

> Living document. Updated as roadmap interviews and planning sessions progress.
> Last updated: 2026-04-02

---

## Vision

A public SaaS fuel fillup tracker for anyone who drives a liquid-fuel vehicle (gasoline, diesel, E85). Differentiated by **depth of personal analytics** and **AI-powered insights** — richer than Fuelly, smarter than GasBuddy, fast at the pump.

---

## Foundational Decisions

| Decision | Choice | Notes |
|---|---|---|
| Target user | Anyone with a liquid-fuel vehicle | Not gasoline-only; diesel + E85 in scope |
| Differentiation | Personal analytics + AI/LLM insights | Not community/social (network effects trap) |
| Monetization | Freemium | Free: fillup log + basic MPG. Paid: AI insights, analytics, forecasting |
| Platform priority | Mobile-first, always | Pump is the acquisition moment; desktop is a bonus |
| Truckers/IFTA | Out of scope for now | IFTA is a different product category (B2B compliance). Revisit if diesel user base grows |

---

## AI/LLM Feature Plan (ship in order)

1. **Structured computed insights** — always-on, no API cost
   - MPG trend (rolling average, % change over N fills)
   - Monthly/annual fuel cost
   - Best station by avg price paid
   - Cost per mile

2. **Anomaly detection + narrative** — triggered on each new fillup
   - Detects fills significantly above/below baseline
   - Surfaces plain-language explanation: "This tank was 18% worse than your 90-day average"
   - Free tier: alert only. Paid tier: detailed breakdown + suggestions

3. **Natural language query** — on-demand, premium tier
   - "How much did I spend on gas last year?"
   - "Which station gives me the best MPG?"
   - Powered by Claude API, answers from user's own data

---

## Additional Data Model (phased)

### Phase 1 — Vehicle specs (one-time entry, high ROI)
- Year, make, model (auto-populated via NHTSA or similar API)
- EPA estimated MPG (city/highway/combined)
- Engine size / fuel tank capacity
- Unlocks: "You're 12% below EPA estimate" — the first compelling comparison

### Phase 2 — All optional enrichment
- Maintenance events (oil change, tire rotation, air filter) → MPG correlation
- Trip context (highway/city, AC, load) → explains variance
- Fuel brand/station loyalty → falls partly out of existing GPS data
- Vehicle specs already captured in Phase 1

---

## Feature Tiers (draft — subject to grilling)

### Free
- Fillup log (unlimited)
- Basic MPG per fill
- Multiple vehicles
- PWA (installable, offline logging)

### Paid (price TBD)
- AI anomaly detection with narrative
- Natural language query
- Cost trends + forecasting
- EPA comparison
- Data export (CSV/PDF)
- Maintenance log

---

## Open Questions (still being grilled)

- [ ] What features land in free vs. paid tier specifically?
- [ ] Social/community features? (gas price reporting, regional comparisons)
- [ ] Export and integrations? (CSV, Apple Health, IFTTT, etc.)
- [ ] Native app vs. PWA only?
- [ ] Onboarding flow for new users?
- [ ] Vehicle data sourcing (manual entry vs. VIN lookup vs. NHTSA API)?

---
