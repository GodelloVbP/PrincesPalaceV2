# Plan: The Petting Zoo event (rev 3)

## Context

The Petting Zoo is the first real event. Rev 1 (chat only) put it behind the dialogue stage
and a new actor system. Rev 2 moved it onto the existing EventScreen but left contracts
unsettled. A reviewer then found the peacock outcomes fail the validator, Kinship's hook
skipped Phoenix Egg, pools and hit reactions, the Cold One placement was unstated, and the
favor seam was the wrong one. This revision settles each contract before any code.

**Release target (Q2, reviewer's recommendation adopted):** two milestones.
- **M1 prototype** — all mechanics, the real step 1 text, steps 2–10 visibly placeholder, no
  set-piece art. Never counts as shipped.
- **M2 shipped** — all ten steps written, set-piece art in place, the frequency decision
  applied, and a playable walkthrough recorded.

Step 0 on approval: save this file as `docs/PLAN_PETTING_ZOO.md`.

## Behavioural contracts

### Appearance
Once per run (`eventsSeen`), any floor, uniform among eligible events (`EventRoll.cs:21-47`).
The arc takes **10 qualifying pets**: Kinship arrives on the 10th; the 11th only shows the
post-arc branch. Whether 10 is acceptable is decided on P0's numbers (D1).

### Page graph — every transition, nothing implicit
| Page | Choices (≤4) → destination |
|---|---|
| `zoo` (opens) | Pet the sheep → step/petter pages · Strut for the peacock [CHA ≥20, greyed] → Leave with result · Feed the fawns → Leave with result · Crack open a cold one [level ≥15, greyed] → Leave with result |
| `step1_pair`, `step1_solo`, `step2`…`step10`, `post_arc` | "Carry on" → `zoo_after_pet` |
| `petter` | "Bjorn pets" [inParty bear, alive, hidden] → `zoo_after_pet` · "Odette pets" [inParty owl, alive, hidden] → `zoo_after_pet` · "Leave the sheep be" → `zoo_after_pet` (declining uses up the sheep for this visit; back to `zoo` let a first-available bot loop zoo ↔ petter forever) |
| `zoo_after_pet` | Peacock · Fawns · Cold one (same as `zoo`) · "Say goodbye" → Leave |

- **Petting never ends the visit, and it can happen only once per visit:** every route that
  heals goes to `zoo_after_pet`, which has no sheep row. The page body there says so:
  "Pet, then pick one more thing to do."
- **Pet the sheep:** the counter `zoo_sheep` is bumped in **outcome** effects and gated on the
  pre-increment value. First match wins, and the last outcome is unconditional:
  1. inParty sheep (alive) + counter max 0 + inParty owl → heal Shawn to full, +1 → `step1_pair`
  2. inParty sheep (alive) + counter max 0 → heal, +1 → `step1_solo`
  3. inParty sheep (alive) + counter min k max k (k = 1..8) → heal, +1 → `step{k+1}`
  4. inParty sheep (alive) + counter min 9 max 9 → heal, +1, grant Kinship → `step10`
  5. inParty sheep (alive) + counter min 10 → heal, +1, grant Kinship → `post_arc`
  6. unconditional → `petter` (Shawn absent or downed; counter unchanged)
- **Heals never revive.** A downed character never appears as a petter.
- **Player-facing Kinship text** in `step10`/`post_arc`: "Future visits with Shawn grant
  Kinship for that run." Kinship is run-scoped (`RunSnapshot.relicIds`). Finishing the arc
  earns eligibility, not permanent possession (D7).

### Peacock (content; validator-legal)
The choice is gated at Charisma ≥20. Outcomes: ≥30 → +200 … ≥21 → +110, then an
**unconditional** +100 (`EventEntryResolver.cs:348` requires this). "Any member" plus
descending order means the highest Charisma counts; the cap of 30 is built in.

### Fawns — +10 Prince's favor for the run
- The bonus is added at the save-aware boundary `ItemOfferRoll.CurrentSquadFavor`
  (`ItemOfferRoll.cs:160`), after the squad max (`:141`). That one point feeds item-offer
  rolls (`RunOrchestrator.cs:603`), shop stock (`RunOrchestrator.Shop.cs:410`) and the bot trace.
- Shop stock that already exists (persisted `shopStock`) is not rerolled; stock generated
  later in the run sees the bonus. It ends with the run.

### Cold one — at the moment the player can act, the special pool is full
- The special pool is the signature pool if the character has one, else the primary pool (D4):
  Shawn → wool, Bjorn → fury, Odette → mana.
- The fill is the **last step of `OpenTurnFor`** (`FightSession.Riders.cs:371-411`). It runs
  after the primary tick, Runic ward conversion, status ticks (poison), transforms and the
  signature gain.
  - The ward is computed from pre-fill mana, so no bigger ward.
  - Poison has already ticked, so the pool is full when control arrives.
  - A transform that swaps pools is filled in its new form.
- Extra actions (`ReopenTurnFor`, `:453`) **do not refill**, consistent with AUDIT #113's
  "an extra action re-pays nothing".
- The buff is active while `run.legStartStep` equals the step stored when it was granted. It
  covers a boss fight if the event came before it (owner accepted as strong).

### Kinship — cancels one positive damage packet (Q1, reviewer's recommendation adopted)
- **What:** the first packet with amount > 0 that reaches Shawn's `ApplyAndCountDamage`
  (`FightSession.Ledger.cs:77`) each fight. That is after dodge and after ward (both inside
  the pipeline), so a dodged or fully warded blow never reaches it. Any source counts: direct
  hits, DoT, Shatter, splash (D3).
- **Multi-packet attacks:** only that packet is cancelled; a rider packet from the same attack
  lands. The relic text says "the first blow", and a test pins it.
- **Hook position:** directly after `NoteDamageForPools` (`:94`) and **before** the Phoenix Egg
  checks (`:101-116`).
  - Pools still hear the blow: it was thrown, per that comment's own rule.
  - A lethal first packet is cancelled and the egg does **not** hatch; the egg's once-per-combat
    lock is left unused.
  - Skipped when Shawn is already an egg shell (the shell absorbs by its own rule).
- **What the cancel skips:** Cursed Idol bonus and stacks, World-Ender's Crown check,
  `ApplyDamageDetailed` (so no wool absorb), `RecordAbsorbed`, Berserker's Vest, and the
  damage-taken ledger.
- **Representation:** a `DamageResult` with a new `Cancelled` flag, zero to health and
  **zero `Absorbed`**, so no false wool/shield report. One combat log line: "Kinship turns the
  blow aside." Callers that key off `toHealth > 0` see nothing.
- **Bearer:** Shawn only, via the relic field `bearer`. **It applies to effect checks only**
  (`HasRelic`). The resolver refuses a relic that has both `bearer` and modifiers, because the
  adapter flattens modifiers party-wide (`FightEncounterAdapter.cs:462`). No broader promise.
- **Channel:** relic field `draftable: false`, filtered in `AvailableRelicOptions`
  (`RunOrchestrator.cs:91`), which covers both the draft and the shop shelf.

## Code changes
1. `healPercent` gains an optional `character` (one member, never revives). `inParty` gains
   an optional `alive`. The `relic` effect adds the id to `run.relicIds` if it is not already held.
2. `RunSnapshot.eventBuffs: List<{kind, amount, legStartStep}>`, where −1 means the whole run.
   Two kinds: `princesFavor` and `fillSpecialPool`. Pruned on load beside `relicIds`
   (`SaveData.cs:804`). The fight receives the active buffs through `FightEncounterAdapter`
   the way relics arrive.
3. Relic fields `draftable` and `bearer`, plus `RelicEffect.Kinship` with a per-fight spent-set
   (the Bloodlust pattern, `FightSession.Riders.cs:318-333`), and the `Cancelled` flag.
4. `fillSpecialPool` at the end of `OpenTurnFor`.
5. Regenerate `docs/CONTENT_SCHEMA.md` for the new raw fields. Update the effect and
   requirement tables in `docs/EVENTS.md`.

## Phases, ownership and gates
The three phases run one at a time; implementation-only overlap is allowed where noted. Each
agent iterates with the dotnet `[D]` loop and focused `tools/test.ps1 <area>` only. There is
**one** `run_tests_parallel.ps1` gate per milestone on the combined tree, announced first to the
dialogue-stage and Spell-VFX sessions, which share the TestRunner.

| Phase | Owner | Files | Waits on |
|---|---|---|---|
| P0 | verifier runs `tools/bot.ps1` (N runs); orchestrator computes | Distribution of **runs until the 10th qualifying pet** = per-run P(Event node reached) × P(Zoo \| event pool) × P(Shawn alive and fielded at the node). Report the median and 90th percentile, now and with a pool of 5 events. The bot's in-event choices (`BotRunDriver.cs:743`, first available) are irrelevant because petting is free. | nothing |
| P1 | implementer | Changes 1–2, minus the fight transport: `RawEventEntry.cs` (effect/requirement rows only), `EventEntryResolver.cs` (own sections), `EventRequirement.cs`, `RunOrchestrator.Event.cs`, `RunEncounter.cs`, `RunSnapshot.cs`, `SaveData.cs`, `ItemOfferRoll.cs` | dialogue-stage D1 commit |
| P2 | implementer (implementation may overlap P1) | Change 3: `RawRelicEntry.cs`, `RelicEffect.cs`, relic resolver, `RunOrchestrator.cs` `AvailableRelicOptions`, `FightSession.Ledger.cs`, `CombatMath.DamageResult`. **Must not edit `FightSession.Riders.cs`** except the spent-set. | nothing |
| P3 | implementer | Change 4 plus fight transport: `FightEncounterAdapter.cs`, `FightSession.Riders.cs` | P1 and P2 committed |
| P4 | implementer | `events.json` Zoo (M1 text), `relics.json` Kinship, docs, schema regen | P3 |
| Gate M1 | verifier | `run_tests_parallel.ps1 -Changed -BuildContent -BuildScenes`, once, on the combined tree | P4 |
| M2 | owner writes steps 2–10; orchestrator writes the 960×720 set-piece prompt (`Art/Events/petting_zoo/zoo.png`); implementer integrates | Runtime walkthrough capture of every page at the four aspects, while the owner is away | owner text and art |

## Tests (literal values)
- **Resolver on the real Zoo definition:** validator-legal, every `goTo` resolves, and every
  page has an unconditional row.
- **Graph:** from each healing route, `zoo_after_pet` is reached and has no sheep row. Pet
  twice in one visit is impossible. "Leave the sheep be" changes nothing except
  removing the sheep row. No path loops back to `zoo`.
- **Counter 0→10:** steps 1…10; Kinship granted on the 10th pet, not the 9th; the 11th pet →
  `post_arc` plus Kinship.
- **Owl present / benched:** pair text / solo text, and the counter moves in both cases.
  Shawn benched or downed → `petter`, counter unchanged. A downed ally's row is hidden.
  Heals never revive.
- **Reload:** after each intermediate choice (`zoo` → pet → step page → `zoo_after_pet`,
  and via `petter`), the reload lands on the same page with the counter and HP applied exactly
  once. Choose submitted twice on one page applies once. A save that throws leaves no
  half-applied state visible after reload (`RunOrchestrator.Event.cs:212`).
- **Peacock:** Charisma 19 greyed; 20/25/30/40 → 100/150/200/200.
- **Favor:** +10 through `CurrentSquadFavor`; an actual `ItemOfferRoll.Roll` and an actual shop
  stock generation read it; existing stock unchanged; it survives a party swap and a reload;
  gone next run.
- **Cold one:** the pool is full at control for Shawn/Bjorn/Odette. Runic ward matches the
  unbuffed value. Full after a poison tick. No refill on a Bloodlust extra action. Ends at
  `AdvanceLeg`. Survives a reload.
- **Kinship:**
  - First packet cancelled, a rider packet from the same attack lands, the next attack lands.
  - Resets next fight.
  - A lethal first packet → cancelled and the egg stays unhatched.
  - No Berserker's Vest proc, `Absorbed` is 0, and no ledger damage.
  - A DoT tick consumes it; a dodge and a full ward do not.
  - Bjorn with it gets nothing.
  - Never in the draft or shop offer.
  - The resolver refuses `bearer` plus modifiers.
- **Draft:** an event-granted relic does not alter `DraftHasAnotherRound`.

## Owner calls (defaults applied; the build proceeds on them)
- **D1** Is 10 pets acceptable? Decide the completion horizon (default: median ≤ 15 runs)
  before P0; if P0 misses it, pick between an event `weight` field and a shorter arc.
- **D3** Every damage source can consume Kinship, including a 1-point poison tick.
- **D4** Special pool = signature, else primary; Odette gets full mana every turn.
- **D5** Heals never revive; a downed Shawn counts as absent.
- **D6** +10 favor is 2.5× Shawn's authored 4.
- **D7** Kinship is per run.

## Coordination (live sessions, 2026-09-25)
- **Dialogue stage** owns `EventScreen`, `EventController`, `ScreenRegistry.WireEvent`,
  `NavigationInputModule`, the epithet field, and the page fields in `RawEventEntry.cs` /
  `EventEntryResolver.cs`. P1 rebases on its D1 commit and edits only the effect and
  requirement sections. Once the stage lands, step text moves into `lines` under
  outcomes that require `inParty sheep`. A KO'd member may speak; `alive` is not required
  for speaker checks.
- **Spell VFX** owns the `skills.json` vfx blocks for five spells and runs a `-BuildContent`
  gate. This plan does not touch `skills.json`. The M1 gate is announced first and sequenced
  around theirs.
