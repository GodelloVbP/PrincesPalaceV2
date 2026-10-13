# Round 2 triage (rev 2 → rev 3), by the plan author (`senior`), 2026-10-13

C = `REVIEW_R2_CONTEXT.md`, B = `REVIEW_R2_BLIND.md`. Reviewer numbers were re-checked before adoption.

Largest change: skip is gone. A gear page is **Take** a card or **Hone instead** (+1 on that card's slot). Neither produces the other, so neither dominates, and the choice no longer depends on gold. No reviewer has seen this mechanic.

| # | Result | Note |
|---|---|---|
| C1 slot plus lost on empty | applied | `EquipmentLoadout.cs:167-181` confirmed; honing moves to `RunSnapshot.slotHoning` |
| C2 shop floor can't fire | applied | 32g confirmed; one affordability card at `FloorTier`, Uncommon |
| C3 requirements filter | applied | as-if-worn through the existing fixpoint, inert fallback |
| C4 deep-power claim | applied | cap is content `maxTier`; enemy freeze at step 200; "small share" withdrawn |
| C5 gold fades by realm 5 | applied as owner Q2 | ×18 by step 40 confirmed |
| C6 necklace rate | applied | retuned: step 0.02, cap 5 → 10.9% / 20.1% / 33.3% effective |
| C7 pendingPicks shape | applied | `PendingPage` struct |
| C8 phase holes | applied | run map amendments in plan §5; I2a after run map P2 |
| C9 shop p, Favor | applied | p 0.35 / 0.42 / 0.45 |
| C10 hone exploit | applied | price per plus and per step, slot-agnostic |
| C11 33% necklace | applied | 1 − (7/8)³ |
| C12 weapon slots | applied | Weapon2 only once Weapon1 is filled |
| C13 score | applied | 23/30, 24 with Q2 (a) |
| C14 gates | applied | |
| B1 flat deep rewards | applied as owner Q1 | rec: climb to step 200 with enemies |
| B2 skip dominated | applied | skip replaced by Hone instead |
| B3 hone exploit | applied | as C10 |
| B4 no deep gold sink | applied as owner Q2 | rec: all prices scale with the fight-gold curve |
| B5 pity underspecified | applied | |
| B6 page friction | applied | ≤ 2 pages per settlement; "loses: …" line |
| B7 shop model | applied | |
| B8 gates/ownership | applied | |
| B9 stacking | applied | holder-pair table |
| B10 pendingPicks persisted | applied | `[NonSerialized]` |
| B11 class ordering | applied | as C9 |
| B12 free Rest hone | applied | accepted explicitly |
| B13 undefined terms | applied | |
| B14 HasRelic count | refined | ~36 call sites |

Unresolved, recorded in the plan: runs may never end past step 200 (enemies freeze, levels grow); gear's real deep share unmeasured (I5); quality p, necklace chances and pity step are author's calls pinned for tests.
