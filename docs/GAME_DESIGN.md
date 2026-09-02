# Game Design (high level)

This document lists the systems envisioned for the finished game, and is
explicit about what exists in code today versus what is planned. **No
system below marked FUTURE has any implementation in this repository.**
Their presence here is roadmap context for future phases and for AI
agents planning subsequent work — see `AGENTS.md`.

## Implemented in this delivery (Phase 0 + Phase 1)

Nothing from the list below. Phase 1 is purely the simulation kernel
(time, scheduling, ids, randomness, commands, events, persistence
foundation) — see `SIMULATION_MODEL.md` and `DOMAIN_MODEL.md`.

## Planned systems (all FUTURE, phase TBD per `ROADMAP.md`)

### People and households — FUTURE
- Individual people with careers, income, and consumption
- Households/families and their shared finances
- Population represented at varying resolution (see `SIMULATION_MODEL.md`
  §"Simulation LOD")

### Employment and careers — FUTURE
- Jobs, salaries, career progression
- Active professions: taxi driver, rideshare/app driver, bus driver,
  courier, truck driver, hearse/funeral driver, etc.

### Companies — FUTURE
- Founding and operating companies
- Hiring workers, running physical establishments
- Production and consumption of goods

### Property and real estate — FUTURE
- Owning land, constructing and managing buildings
- Buying, renting, financing property
- A real estate market with prices influenced by the broader economy

### Banking and finance — FUTURE
- Bank accounts, savings, debt, loans, mortgages
- Investing; a stock market; eventually other financial markets
  (explicitly including future crypto/other markets as later work, not
  named systems today)

### Vehicles and transportation — FUTURE
- Owning and driving vehicles
- Operating transportation-related professions (see Employment above)

### Geography and jurisdiction — FUTURE
- Multiple cities, later multiple states/provinces and countries
- Distinct currencies and legislation per jurisdiction

### Formal and informal economy — FUTURE
- A formal, regulated economy (implemented first)
- Eventually an underground/clandestine economy
- Eventually enforcement, crime, a judicial system, and possibly combat

## Systemic interconnection (the core differentiator)

The defining goal is that these systems, once they exist, share one
economic reality. Illustrative future scenarios (not implemented):

- A company closing a factory can destroy jobs, reduce regional income,
  reduce consumption, raise loan delinquency, affect rents and property
  prices, ripple into other companies' results, move equity prices, and
  change public tax revenue.
- A central bank raising interest rates can raise the cost of credit and
  mortgages, cool real estate demand, reduce investment and consumption,
  trigger bankruptcies, and move financial markets.

Realizing this requires the deterministic, shared kernel built in
Phase 1 — a common clock, a common scheduler, and a common source of
reproducible randomness that future systems can safely build on.
