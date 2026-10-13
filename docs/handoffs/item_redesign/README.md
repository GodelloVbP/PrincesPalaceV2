# Item redesign — inputs done, plan v1 not started

Owner goal: the item and item-reward system "does not feel good"; redesign it.
Process: `docs/WORKFLOW.md` Plans (v1 → review → v2 → review → v3).

## State

| File | What | Caveat |
|---|---|---|
| `item_audit.md` | Current system: map, content inventory, reward moments, ranked problems D1–D10 (design) and C1–C7 (code), owner constraints | `calc` figures are derived from cited constants; two items flagged unverified |
| `research_theory.md` | Design theory, ~30 tests, 15-item scoring checklist | Web fetch was blocked: `[S]` = search summary, `[K]` = memory, unverified |
| `research_games.md` | 11 acclaimed games, comparison table, 12 ranked patterns, anti-patterns | `[V]` = search-verified, `[M]` = memory; don't pin `[M]` numbers in docs or tests |

Plan v1 was not written: the `senior` author was stopped before writing.

## Next step

Launch `senior` to write `docs/PLAN_ITEMS.md` v1:

- `Escalation: architecture - redesigns the item/reward data model and the reward-offer contract across Domain, Core, content generation and screens`
- `Context: fresh because (b): previous author stopped; start from docs/handoffs/item_redesign/`
- Read the three reports above first.
- Plan contents:
  1. Diagnosis scored against the theory checklist.
  2. Target design: taxonomy, reward moments and choice structure, rarity, party fit, inventory pressure, power curve, economy.
  3. Architecture (the owner's core requirement: simplest structure, no duplicate code, shared structures every reward source calls, easy to add items and effects):
     - one item definition model;
     - one trigger→condition→effect vocabulary;
     - one offer pipeline (pool → filter → weighted sample with pity → present → resolve) for fights, shop, relic draft, events and reward tracks;
     - one seeded RNG policy;
     - content via `ContentData/*.json`;
     - screens via `Domain/UiKit/Screens/`;
     - classes extended or deleted, named by path;
     - files touched to add an item or effect, before vs after.
  4. Phasing with gates, test rewrites, save compatibility, bot impact and per-phase routing.
  5. Risks, open owner decisions and out of scope.
- End with an empty "Review disposition" section.
- Write the file section by section so an interruption cannot lose it.

Date: 2026-10-13.
