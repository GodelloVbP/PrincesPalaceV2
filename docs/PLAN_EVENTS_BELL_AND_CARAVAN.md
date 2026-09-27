# Plan: Bell in the Fog and the Rat Caravan (rev 2)

Rev 2, 2026-09-27 (owner's review answers in). **(assumed)** = gap-fill, repeated in section 6 with the code conflicts.
Ships in two stages: **A, the Bell**, then **B, the Caravan**. Companions: PLAN_PETTING_ZOO, PLAN_DIALOGUE_STAGE.

## 1. Behavioural contracts

### 1.1 Shared: an event that may return
- **Trigger** an event opens. **Condition** not `mayReturn` (every existing event). **Outcome** its id goes into
  `eventsSeen` at once, as today: once per run.
- **Trigger** a `mayReturn` event opens. **Outcome** nothing goes into `eventsSeen`; it stays eligible for every later
  Event node, any number of times, with no cap and no spacing rule (owner, 2026-09-27).
- **Trigger** a pick or a fight result applies `finish`. **Outcome** its id goes into `eventsSeen`; it never rolls
  again. The caravan's "run flag after robbing" **is** this: Rob's win carries `finish`. No second flag.
- Floor gating stays generic (`floors`); a floor-type gate joins it in `EventRoll` when types exist, so both events pin
  to forest as content.

### 1.2 Shared: an event that starts a fight
- **Trigger** a pick whose outcome carries a `fight` effect. **Condition** the fight's party (override or normal
  fieldable) has a standing member. **Outcome** a pending encounter request is persisted, the event stays open, and the
  Fight screen builds *that* fight instead of a room roll.
- **Trigger** the fight ends. **Outcome** the session reports `Defeated` (every enemy down), `Survived` (round limit
  passed, someone standing) or `Fell`; the event's authored `onDefeated` / `onSurvived` / `onFell` outcome applies
  through the choice effect code; one save.
- **Loss policy:** `endRun` (today: `EndRun`, defeat screen) or `wake` (run continues, fielded members at 0 HP go to 1
  HP). **Pays:** `true` = payout, spell drop, Reckoning; `false` = none, Continue.
- An event fight **never clears its room or advances the leg**; the event's Leave does, as today. Back from the fight
  the Map reopens the event and plays the result, then the page (dialogue contract 3).

### 1.3 Shared: event-local speakers
- **Trigger** a line's speaker is one of the event's own `speakers`. **Outcome** the stage shows it exactly like a party
  bust: bottom-anchored, name plate (name + epithet), side by `cast` or first appearance, mirrored on the right.
- An event speaker is always present at its own event: the presence dataflow treats it as guaranteed on every page. Its
  id may not collide with a character id.
- Each event speaker declares its own expression list (the merchant: neutral, grinning, hostile); a line naming an
  undeclared expression is refused at build. Party characters keep the stage's expression enum, which gains `entranced`
  (Shawn's bust for the bell page).
- Missing art degrades as the stage already does (requested, neutral, no bust). The Petting Zoo is unchanged.

### 1.4 Bell in the Fog
**Appearance.** `mayReturn`; event-level require `inParty sheep alive`; `floors: []` (see 6.3).

**Page graph**
| Page | Choices → destination |
|---|---|
| `bell` (opens; Shawn `entranced`) | Touch the bell [inParty sheep alive] → fight `bellwether` · Ask the others → ask pages · Walk away → Leave, no effect, no `finish` |
| `ask_both` / `ask_bjorn` / `ask_odette` / `ask_none` | "Listen again" → `bell` |

- "Ask the others" outcomes, first match wins: {bear alive, owl alive} → `ask_both`; {bear alive} → `ask_bjorn`; {owl
  alive} → `ask_odette`; {} → `ask_none` (narration). Requirements are AND-only, hence outcomes, not a hidden choice.
  They hear nothing; no effect.

**Fight `bellwether`.** Party override `[sheep]`; enemies `[bellwether]`; normal class; `surviveRounds: 10`, round label
"Toll"; loss policy `wake`; `pays: false`. Bjorn and Odette sit out: their HP, pools and XP are untouched.
- **Trigger** a round starts (rounds 1..10). **Outcome** the bell tolls: the top-centre counter shows the toll number;
  the Bellwether gains one stack of attack for the rest of the fight (`rallyPerRound`, start **8% per toll**, cap 10
  stacks, tuned in M8); the muffled toll plays off the beat; the flock overlay steps one frame closer, with footsteps.
- **Trigger** round 11 would start with Shawn standing. **Outcome** `Survived` (Endure). No 11th toll.
- **Trigger** the Bellwether dies before that. **Outcome** `Defeated` (Break), in any round, toll 10's included
  **(assumed reading of "before toll 10")**.
- **Trigger** Shawn falls. **Outcome** `Fell`.
- Black Ram Mode has no interaction with the tolls. No music; the plate reads "The Bellwether", steady.

**Endings** (each applies `finish`)
| Result | Effects | Then |
|---|---|---|
| Endure (`onSurvived`) | relic `toll_of_the_flock`; exp to Shawn | page `endure`: the flock turned, Shawn's face; "Leave" |
| Break (`onDefeated`) | relic `toll_of_the_flock`; relic `bellwethers_bell`; the same exp to Shawn | page `break`; "Leave" |
| Fall (`onFell`) | none (wake policy put Shawn at 1 HP) | Leave with result text |

**Tuning contract.** An untransformed, floor-appropriate Shawn survives 10 tolls narrowly. Measured by the bot (M8):
with transforms disabled, Endure rate 60-85% per floor and median Shawn HP at Endure ≤ 30% of max **(assumed
thresholds)**.

**Relic `bellwethers_bell`.** Bearer `sheep`, `draftable: false`. Every Transform the bearer enters lasts +1 turn. Read
at the one line that computes transform length (M3), so it covers any Transform effect, not only Black Ram.

**Relic `toll_of_the_flock` (the Endure relic, owner design 2026-09-27).** Bearer `sheep`, `draftable: false`.
- **Trigger** one of Shawn's turns opens. **Condition** it is his 3rd, 6th, 9th... turn this fight. **Outcome** a ghost
  flock charges the enemy line: one packet to every living enemy of `TollOfTheFlockAttackPercent`% of Shawn's current
  attack, buffs included (tunable, start **15%**), times `TollOfTheFlockTransformMultiplier` (tunable, start **1.5**) if
  he is transformed at that instant. Black Ram's own +50% attack (`skills.json:1331`) already feeds the base, so a
  transformed charge is ~1.5 × 1.5 = **2.25x** an untransformed one. Damage type: his attack type, through the normal
  defences.
- **Tuning contract (owner):** a small, slightly gimmicky bonus, never build-defining. The flock's share of Shawn's
  damage dealt stays **under ~5% untransformed** and **under ~10% for a Black Ram build**. M8a measures it; the two
  tunables move until it holds.
- Counting is per fight, on turns that *open* (`OpenTurnFor`). Extra actions (`ReopenTurnFor`) do not count (AUDIT
  #113's rule). Speed synergy is intended: a faster Shawn charges more often.
- Attribution: the packets are Shawn's damage dealt, with `KillCredit.Nobody`, so a flock kill never arms
  Trample/Bloodlust (the stale-flag hazard, `FightSession.Enemies.cs:1051-1060`); kill-keyed relics do not fire
  **(assumed)**.
- Presentation: one beat with the relic's `vfx` (ghost sheep crossing) and `sfxPath` (toll plus hoofbeats); log "The
  lost flock charges." Works in every fight of the run, bosses included.

### 1.5 Rat Caravan
**Appearance.** `mayReturn`, any floor, no event-level requires. The merchant is an event speaker (neutral, grinning,
hostile), on the right.

**Page graph**
| Page | Choices → destination |
|---|---|
| `caravan` (opens) | Browse → shelf, then `after_browse` · Browse with Odette [inParty owl alive, hidden] → shelf with fakes marked, then `after_browse` · Rob him → fight `rat_pack` · Walk on → Leave, no `finish` |
| `after_browse` | Walk on → Leave, no `finish` · Look again → shelf (same reveal state), then `after_browse` · Rob him → fight `rat_pack` |

`after_browse` lists Walk on first so a first-available bot never loops (the Petting Zoo rule).

**Shelf `caravan`.**
- **Trigger** the first Browse or Rob of the run. **Outcome** stock is rolled once, keyed to that (step, node) on new
  streams, and persisted **with the event**, not the node: the shop's gear roll (4 cards, same candidates, tier band and
  affixes as the room shop) plus 2 consumable cards **(assumed; see 6.1)**. Price = 70% of the shop price, rounded away
  from zero, min 1.
- **Walk on keeps the stock.** Every return shows the same cards, minus what was bought, with the same reveal state: no
  free reroll. The stock ends only on `finish` or with the run.
- Fakes: `max(1, round(n/3))` of the n real cards, picked on their own stream, stored on the stock entry and never shown
  unless revealed. With 6 cards, 2 are fake.
- **Trigger** Browse with Odette. **Outcome** fake cards carry a "Fake" mark, persisted with the stock. The mark does
  not follow the item into the bag **(assumed)**. No rerolls **(assumed)**.
- **No Stolen tag, no sell or upgrade rule** (buy-back is 30%, below the caravan's 70%; see 3.3). A fake sells at the
  genuine price, which reveals nothing.

**Fake gear.** **Trigger** a fight completes (not a run loss). **Condition** worn by a fielded member **(assumed)**.
**Outcome** its counter drops from 3; at 0 it is removed in that settlement's save and the Reckoning (or Continue
fallback) shows "<item> falls apart." and "No refunds." **Fake consumable.** **Trigger** used in a fight. **Outcome**
consumed, turn spent, no effect; log: "This is of such poor quality... it's a fake."

**Rob: fight `rat_pack`.** Enemies `[rat, rat, rat]`, class elite, normal fieldable party, no round limit, loss policy
`endRun`, `pays: true`.
- **Defeated** → Reckoning, then `onDefeated`: `takeShelf` (unsold cards granted, fakes stay fake, one lost to the
  scuffle on a seeded stream) + `finish`; the result names the lost piece.
- **Fell** → normal run loss (the build refuses `onFell` on `endRun`).

### 1.6 Presentation, both events
- Event art is 16:9 everywhere: 1280x720 on the dialogue stage (same top pin, `EventScreen.cs:360`, clear of the 256
  box), 960x540 in the legacy layout, commissioned at 1920x1080; `docs/EVENTS.md` sizing is rewritten. Both events use
  the stage (opaque `backdrop`, keyed set pieces, busts). The fight HUD gains a top-centre round counter, shown only
  when the fight has a round limit.

## 2. Precedence and edge cases

1. **Round start order:** (a) past the limit → `Survived`, nothing below fires; (b) enemy round effects (the rally); (c)
   the next actor's `OpenTurnFor`. The round hook carries nothing else.
2. **Several rounds between two turns** (time-based rounds, `TurnOrder.cs:597-606`) each fire in order. **Kill vs
   limit:** Survived is only checked at a round start, so a kill wins; mutual death keeps `PlayerWon`'s rule.
4. **The flock inside Shawn's turn start:** it fires as the last step of `OpenTurnFor`, after regen, statuses (a poison
   tick that drops him skips it), the transform tick (a Black Ram that expires this turn gives no multiplier) and the
   Cold One fill. A flock kill of the last enemy ends the fight `Defeated` before he acts. A turn that opens and is then
   skipped by a stun still counts **(assumed)**.
5. **Second lives** are 0 in a `wake` fight **(assumed)**. An **empty override party** cannot start: the choice carries
   the `inParty alive` require, and an empty request is refused and logged, never an empty stage.
7. **Quit mid event-fight:** the pending request relaunches the same fight on the same (step, node) stream. **Quit on a
   result page:** effects are already saved; a reload replays text only.
8. **Returns:** Walk away / Walk on never touch `eventsSeen`; a `mayReturn` event can come back on the very next Event
   node and any number of times.
9. **Rob after buying:** only unsold cards are granted; one left is the one lost; none left, the text says so. Odette's
   marks do not steer the loss.
10. **Caravan items never merge.** Every item bought or taken from the caravan carries its own lot id, genuine ones
    included, so a fake is never revealed as "the one stack that did not merge" **(my call; the owner's minimum was
    fakes only)**.
11. **Fake breaking in a no-pay fight:** the "No refunds." line shows in the end log above Continue. The bot's local
    satchel carries the fake flag, so it pays the turn too.
12. **Bellwether's Bell** adds to Wrath's `TransformDurationBonus`. Existing events do not change.

## 3. The shared engine model

### 3.1 The encounter request (the seam for "event starts a fight")
Every fight becomes one **EncounterRequest**: party, enemies, class, round limit, loss policy, pays, presentation
(backdrop, round label and sfx, overlay, ambience). A room's request is derived from the node as `RunEncounter.For` does
today; an event's is read from its `fights` block while `run.pendingFight` names it.

**The one seam: `RunOrchestrator.CurrentEncounterRequest()`.** `BuildFight` builds from it (from the node today,
`RunOrchestrator.cs:282-284`), `FightBootstrap` takes class and backdrop from it (`FightBootstrap.cs:516-526`), and
`SettleFight` branches on it once. Nothing else learns event fights exist; the bot gets them for free.

**Why it fits both uses.** Constant: adapter build, carried HP, ledger fold, write-back, seeded stream, combat. Varying,
and materially different between the two:
| | Bellwether | Rat pack |
|---|---|---|
| Party | override `[sheep]` | normal fieldable |
| Win | kill or survive 10 rounds | kill |
| Loss | wake at 1 HP, run continues | run ends |
| Pays | no | yes, elite |
| Result | three authored outcomes | one authored outcome |

**The result is an event outcome.** `onDefeated` / `onSurvived` / `onFell` are `RawEventOutcome` rows: effects, result
text, goTo and the one save are reused. A fight is a choice whose outcome combat decides. **Next requirement** (an
ambush node, a duel for Bjorn alone): another request, content only.

### 3.2 The round-start hook (the toll and the round limit)
`TurnOrder` counts rounds (`TurnOrder.cs:33-36`); nothing reads them. The session gains `OnRoundStarted(round)`, fired
from `GrantTurnStart` when the round moved, in section 2's order. Its users are the round limit and enemy round effects;
the rally is the first enemy effect, and any future "grows each round" monster is the second, as content.

**The toll's buff.** Statuses refresh, never stack (`StatusEffect.cs`). `FallingOffStacks` holds keyed stacks per
combatant: a fight-long stack is the storage. Missing is a reader: an enemy's `BonusAttackPercent` is never written
(`RefreshAttackBonus` is called only on player paths, `FightSession.cs:518`, `FightSession.Skills.cs:243`). Smallest
general mechanism: enemy field `rallyPerRound {attackPercentPerStack, maxStacks}`, a stack per round start,
`AttackBonusFor` summing rally stacks for any actor, and the enemy path calling `RefreshAttackBonus` before
`CombatMath.ComputeAttackDamage` (`FightSession.Enemies.cs:964`).

**Triggered relics, generically.** A `RelicEffect` member called from one combat-event hook, with `bearer` and
`draftable` (`RawRelicEntry.cs:70,77`). Existing hooks: combat begin (`FightSession.RelicMechanics.cs:40`), swing/cast
(`FightSession.Relics.cs:50-114`), kill (`:550`), the damage funnel (Kinship). New: **the bearer's turn opening**, end
of `OpenTurnFor` (`FightSession.Riders.cs:371`), with a per-fight opened-turn count. Presentation reuses
`SpellPresentation` as an optional relic `vfx`, via `BeginBeat`/`RecordSpellPresentation`/`CommitBeat`
(`FightSession.Beats.cs:20,81,316`).

### 3.3 Item instances (fakes)
**Sell-back finding:** `ShopPricing.SellFraction = 0.30` of shop price (`Domain/Rewards/ShopPricing.cs:130`); the
caravan charges 70%, so resale loses 40 points. Per owner rule 9 the Stolen tag, sell refusal and upgrade rule are
**dropped**; only the per-instance fake state remains. Identity is four positional values threaded through ~15 files; a
fifth is the patch §10 forbids, and still wrong: `TryRemoveAt` removes the *first* match, so a genuine and a fake potion
would be spent interchangeably.

**The seam: an `ItemInstance` value** (the four values plus `Provenance {lot, fake, fightsLeft}`) and **one merge
predicate `ItemInstance.SameStack(a, b)`**: equal as today for ordinary items; an instance with a `lot` equals only the
same lot. `InventoryEntry` = instance + count, `EquipmentSlotEntry` = slot + instance. On disk the fields stay flat plus
one nested `provenance` (additive for JsonUtility).

**Save versions.** No data conversion is ever needed (additive fields, the house posture). Each stage still bumps, with
an empty step, so an older build refuses a save it would mishandle: **7 in Stage A** (a pending event fight would settle
as a room fight and skip its ending), **8 in Stage B** (fakes would load as genuine). `Reconcile` gains: a pending fight
whose event is not open is dropped (A); a fake at `fightsLeft ≤ 0` is removed (B).

### 3.4 The merchant shelf (the caravan)
The caravan's stock outlives its node, so it cannot live in the room shop's single slot (`run.shopStock` / `shopNodeId`,
overwritten by the next shop). It gets its own run list, `run.shelves` (owner = event + shelf id, entries, revealed).
Rolling, buying and the panel stay the shop's, with the stock list passed in; the owner decides the recipe (`shelves`),
that Leave returns to the event, and that the stock ends on `finish` or with the run. Seam: `EnsureShopStock(owner)`;
`BuyGear` stamps the instance from the stock entry.

### 3.5 Content shape (events.json, one pipeline)
- Event: `mayReturn`; `speakers[]` {id, name, epithet, bustPath, expressions[]}; `fights[]` {id, enemies[], elite,
  party[], surviveRounds, roundLabel, onLoss `endRun|wake`, pays, backdrop, roundSfx, ambience, roundOverlay {path,
  fromScale, toScale}, onDefeated, onSurvived, onFell}; `shelves[]` (Stage B) {id, priceFactorPercent, fakeShare,
  sections[], consumableCount}.
- Effects: `fight` {fight}, `finish`; `exp` gains `character`. Stage B: `shelf` {shelf, reveal}, `takeShelf` {shelf,
  amount = cards lost}.
- Enemy: `rollable` (default true; false keeps it out of `RunEncounter.Pool()`, `RunEncounter.cs:97-101`),
  `rallyPerRound`. Relic: optional `vfx`.
- The build refuses: `finish` on a non-returning event; a `fight` effect outside outcome effects, more than one per
  outcome, or with a non-empty goTo; requires on a fight outcome; `onFell` on `endRun`; a missing `onSurvived` when
  `surviveRounds > 0`; enemies over the stage's 3 slots by `slotSpan`; a speaker id that is a character id; an
  undeclared expression; unknown ids. The presence dataflow treats each fight outcome as an edge from the launching
  page.

## 4. Milestones

One owner each; iterate with the `[D]` loop and `tools/test.ps1 <area>`; commit only on the gate (`docs/TESTING.md`:
`tools/run_tests_parallel.ps1 -Changed`, plus `-BuildContent`/`-BuildScenes` where named), announced to other live
sessions first.

### Stage A: Bell in the Fog (M1, M2, M3, M6, M7a, M8a, M9a)

**M1: Content model and validation (implementer).** Stage A rows of 3.5, speakers included; `docs/CONTENT_SCHEMA.md`
regen; `docs/EVENTS.md` sections for returning events, fights, speakers.
- `Domain/Content/RawEventEntry.cs:33-47` (effect row), `:125-152` (line, event); `EventEntryResolver.cs` own sections;
  `EventCastPresence.cs` (fight edges, event speakers guaranteed); `RawEnemyEntry.cs`; `RawRelicEntry.cs`.
- Gate: `tools/test.ps1 content`, then `run_tests_parallel.ps1 -Changed -BuildContent`. Done when every refusal in 3.5
  has a test naming it, and existing events build unchanged.

**M2: Encounter request, returning events, save 7 (senior).** `Escalation: architecture` (a fight stops always clearing
its room or ending the run on loss).
- Returning: `OpenEventHere` (`RunOrchestrator.Event.cs:177-178`) skips `eventsSeen` for a `mayReturn` event; `finish`
  in `ApplyEffect` (`:316-411`). `EventRoll.Pick` (`EventRoll.cs:34`) is unchanged.
- Request: `run.pendingFight`; `CurrentEncounterRequest()`; `BuildFight` (`RunOrchestrator.cs:274-338`) and
  `RunEncounter.For` (`RunEncounter.cs:58-88`) take the request; party override intersected with
  `EncounterRoll.FieldableParty` (`EncounterRoll.cs:263-298`); `SettleFight` (`RunOrchestrator.cs:415-549`) branches
  once: event fights skip `ClearCurrentRoom` (`:536`) and `AdvanceLeg` (`:546`), apply `wake`, apply the result outcome,
  save once. `FightBootstrap.EncounterFor` (`:516-526`) reads the request. `exp.character`. `SaveData.CurrentVersion` 7
  (`SaveData.cs:64`), empty step in `Migrate` (`:469-543`).
- Screens: `EventController` launches the Fight; `LeaveFight` (`FightController.Input.cs:959-962`) returns to the Map,
  which reopens the event (`MapController.cs:151-155`); no reward falls back to Continue (`:1025-1035`).
- Bot: `VisitEvent` (`BotRunDriver.cs:740-800`) runs its fight routine on a pending request. Gate: `tools/test.ps1 run`,
  then `run_tests_parallel.ps1 -Changed`.
- Done when tests show: a returning event rolls again after Walk away (three times in a row included) and never after
  `finish`; benched HP is untouched by an override fight; `wake` puts the fallen at 1 HP and the run continues; `endRun`
  ends it; a won event fight leaves the room uncleared and the event on its result; a reload mid-fight relaunches the
  same enemies; room fights settle as before (`FightSettlementTests` untouched).

**M3: Rounds, rally, transform relic, Toll of the Flock (implementer).**
- `CombatEncounter` exposes `Round` (`_turnOrder` is private, `CombatEncounter.cs:14`); `OnRoundStarted` in
  `GrantTurnStart` (`FightSession.Riders.cs:361-366`), round 1 via `Begin` (`FightSession.cs:461`); round limit ends the
  encounter `Survived` and calls `ResolveOutcome` (`FightSession.Outcome.cs:46`); an `EndReason` on the session.
- Rally: `FallingOffStacks` key, `AttackBonusFor` (`FightSession.Talents.cs:288-310`), enemy `RefreshAttackBonus` before
  `FightSession.Enemies.cs:964`.
- `RelicEffect.BellwethersBell` at `FightSession.Talents.cs:948`. `RelicEffect.TollOfTheFlock` at the end of
  `OpenTurnFor` (`FightSession.Riders.cs:371`), not in `ReopenTurnFor`; packets via `DealDamage(..., KillCredit.Nobody)`
  (precedent `FightSession.RelicMechanics.cs:454-470`); both tunables in `FightTuning`.
- Gate: `tools/test.ps1 combat`, then `run_tests_parallel.ps1 -Changed`.
- Done when literal-value tests pin: round order, catch-up, Survived at 11 not 10, a round-10 kill is Defeated, rally at
  0/1/10 stacks (8% each) and the cap; the Bell's +1 with and without Wrath; the flock on opened turns 3/6/9 not 2/4,
  not on a Bloodlust action, twice as often at double speed, 15% base, 1.5x transformed on the same attack and 2.25x
  with Black Ram's bonus, skipped when poison drops him, no Bloodlust from its kill; a Survived fight with an enemy
  standing tears down cleanly.

**M6: 16:9 frame, speakers on the stage, fight HUD (implementer).**
- `EventScreen.cs:69-70` (and the stage set piece `:428-434`) to 1280x720 / 960x540; the stage loads event-speaker busts
  beside party busts (`EventLineView`, the bust loader); `entranced` in the expression enum; round counter and overlay
  layer in the fight screen tree; `ScreenRegistry` wiring; request-driven backdrop, round sfx, overlay steps; one
  looping ambience channel in `SoundController` (it plays one-shots only today).
- Gate: `run_tests_parallel.ps1 -Changed -BuildScenes`; `tools/screenshot.ps1 -Panel Event` and the fight panel, looked
  at.
- Done when UiAudit passes at four aspects, the counter hides in room fights, captures show 16:9, and a fixture event
  speaker shows on either side with the missing-art fallback.

**M7a: Bell content (implementer).** `events.json` Bell (placeholder text marked), `enemies.json` `bellwether`
(`rollable: false`, `rallyPerRound`), `relics.json` both relics (`bearer: sheep`, `draftable: false`), docs. Gate:
`run_tests_parallel.ps1 -Changed -BuildContent -BuildScenes`. Done when a debug-opened run walks every page and every
fight result, and the bot's first-available path terminates with the room cleared.

**M8a: Bell tuning (implementer adds flags; verifier runs; orchestrator judges).** Bot flags on `tools/bot.ps1` (params
`:1-33`): `-ForceEvent <id> -ForceEventFloor <n>`, `-EventChoice <text>`, `-NoTransform`, `-GrantRelic <id>`; trace
fields for event fights (result, rounds, Shawn HP %, transformed) and the flock's damage share.
`docs/BOT_SUMMARY_SCHEMA.md` updated. Runs: Bell per floor with and without `-NoTransform`; `-GrantRelic
toll_of_the_flock` vs baseline. Done when the tuning contract (1.4) holds on every floor at a rally near 8%, and the
flock's share of party damage, transformed and not, meets the flock contract in 1.4 (under ~5% / under ~10%).

**M9a: Bell art (owner art; implementer integrates).** Slice via `tools/slice_actor_sheet.py --actor
Enemies/bellwether`; Shawn's `entranced` bust through `tools/normalize_dialogue_busts.py`; runtime captures of every
page and result at four aspects while the owner is away.

### Stage B: Rat Caravan (M4, M5, M7b, M8b, M9b)

**M4: Fake item instances, save 8 (senior).** `Escalation: cross-layer` (Domain inventory/equipment, Core satchel, Data
save version). Smaller than rev 1: no Stolen tag, no sell refusal, no upgrade rule.
- Step A, no behaviour change: `ItemInstance` + `SameStack` through `InventoryOps` (`:43-60`, `:113`), `EquipMove`
  (`:36,64,84`), `EquipmentOps` (`:51`), `BagView` (`:39`), `TakeOffer` (`RunOrchestrator.cs:612`), shop buy/sell.
  Commit on a green gate before Step B.
- Step B: `Provenance`; save 8, `Reconcile` rule of 3.3; `SatchelStack` (`FightHudModel.cs:71`) carries the instance so
  `OnItemUsed` (`FightBootstrap.cs:568`) and `SpendConsumable` (`RunOrchestrator.cs:378`) spend that exact entry; the
  fake path in `UseConsumable` (`FightSession.Items.cs:39`) for both callers (`FightRunner.cs:86,276`); wear countdown
  and removal in `SettleFight`, notice on the settlement.
- Gate: `tools/test.ps1 hub` and `run`, then `run_tests_parallel.ps1 -Changed`.
- Done when: existing inventory/equip tests pass unmodified after Step A; lot items never merge and a fake is never
  spent for a genuine one; equip/unequip keeps provenance; v7 loads as v8 unchanged and 8 is refused below 8; a worn
  fake breaks on its 3rd completed fight, not its 2nd; a bagged fake does not count.

**M5: Merchant shelf and caravan rules (implementer).** Stage B rows of 3.5; `run.shelves`; `EnsureShopStock(owner)`
(`RunOrchestrator.Shop.cs:136-154`) and the buy path take a stock list; shelf Leave returns to the event (`:163-174`
stays the room shop's); 70% price, fake pick and reveal, consumable cards (non-`Offerable` consumables,
`ContentDatabase.cs:242-250`), `takeShelf` with the seeded loss; new `RngStreams` after `Event = 9`
(`RngStreams.cs:58`). Map opens the shelf from the event and returns to it (`MapController.cs:274,292`). Bot: the shop
buying loop shared with `VisitEvent`.
- Gate: `tools/test.ps1 run` and `ui`, then `run_tests_parallel.ps1 -Changed -BuildContent`.
- Done when: fakes are 1/1/2/2 for 3/4/5/6 cards; prices pinned literally; the same stock and fakes after a reload **and
  after Walk on and a return at another node**; a room shop in between leaves the caravan stock alone; reveal survives;
  Rob grants unsold minus one; room shop unchanged.

**M7b / M8b / M9b.** Caravan content with the merchant speaker (same gate as M7a); tuning with `-EventChoice "Rob him"`,
done when a rob is won at the bot's elite-room rate for that depth within 10 points; caravan art and captures.

**Assets** (owner generates; 1920x1080 unless stated; opaque backdrops, the rest keyed alpha):
- A: `Art/Events/bell_in_the_fog/` `fog_clearing.png` (event and fight backdrop), `stump_bell.png`, `flock.png` (fight
  overlay), `endure.png` (flock turned, Shawn's face), `bell_broken.png`. Shawn `entranced` bust (the stage's bust
  format). Bellwether key-pose sheet, Giant Rat flat-cel, green screen, the rat's stances (idle, attack, cast, guard,
  hurt, defeated, extra). Toll of the Flock vfx (ghost sheep crossing) as §5b layers of `docs/ART_PIPELINE.md`. Both
  relic icons at the existing relic icon size.
- B: `Art/Events/rat_caravan/` `road.png`, `caravan.png` (wagon, no merchant), `robbed.png`. The rat merchant's bust in
  three expressions, neutral / grinning / hostile, same format and canvas.

**SFX (A):** muffled toll landing off the beat; seamless wind loop ≥ 20 s; flock footsteps per toll; bell-touch sting;
bell shatter (Break); flock charge (hoofbeats + toll). No music.

## 5. Risks

- **R1 Lifecycle (M2).** `FightSettlementTests` pins room settlement; one branch point, room path untouched. Shared
  files (`EventScreen`, `ScreenRegistry`, `events.json`, `relics.json`): gates announced.
- **R2 Identity refactor (M4) is wide** (~15 files). Step A lands with zero behaviour change first.
- **R3 A win with an enemy standing.** Code may assume a win means every enemy dead (stances, tallies,
  `VictoryRewards.For`). M3 tests the Survived teardown; `pays: false` keeps it out of payouts.
- **R4 Returning events crowd the pool.** With no cap, two `mayReturn` events can recur often. Today every floor is
  forest, which exaggerates it; once floor types exist a deep run visits the same type about three times at most, so
  crowding should shrink. M8 reports appearances per event per run; a weight is the owner's call if it still shows.
- **R5 Toll presentation between turns.** The toll's sfx and overlay step fire outside any actor's turn, new to the
  fight screen. M3/M6 check the next turn still opens normally.
- **R6 Single user / bot bias:** `roundOverlay` and the round label serve only the Bell (plain fields); the
  first-available bot touches the bell every run, moving batch death rates (M8a names it).
- **R7 Flock scaling with speed** is intended, but a speed-stacked Shawn (Slippers, Anklet) charges more often; the ~10%
  ceiling must hold there too. M8a reports the share by speed band.

## 6. Open owner calls

**Where the brief meets the code**
1. **The room shop sells no consumables**: its gear shelf draws from `ContentDatabase.Offerable` (generated gear only,
   `ContentDatabase.cs:242-250`). Default: the caravan adds two consumable cards of its own. Should the room shop get
   them too?
2. **The shop mix includes books and relics**, which have no item instance, so a fake cannot apply. Default: the caravan
   stocks gear and consumables only.
3. **No biome exists**; floors are numbers and every floor draws forest. Bell uses `floors: []`.
4. **The wishing-well demo has no art** (empty `artPath`, empty folder); `petting_zoo/zoo.png` is undelivered. "Refit" =
   the frame and `docs/EVENTS.md` go 16:9; the zoo brief moves to 1920x1080.
5. **No music plays anywhere** (`Domain/Audio` layers are unwired): "no music" needs no code now.

**Gap-fills to confirm**
6. A round-10 kill is Break (Endure only at round 11). 7. Fake gear counts worn fights, not bag time.
8. No second lives in a `wake` fight; flock kills credit nobody; a stunned turn counts for the flock.
9. Every caravan item gets a lot id (no merge), not just fakes (2.10).
