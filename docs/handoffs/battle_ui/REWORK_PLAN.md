> Companion to `README.md` and `GAP_AUDIT.md` in this folder — a v2 rework plan for
> the battle UI handoff, not a replacement for either. `GAP_AUDIT.md` stays as the
> permanent record of what the phase-6 build matched. This plan closes the original
> handoff's two open product-decision gaps (#7, #18), retires #11 outright, and specs
> the layer v1 never covered: combat-feel intent, enemy presentation, status
> iconography, and four domain systems with no UI surface today (break meter,
> transformation state, Brave banking, Second Life). No code, scenes, or content
> changes in this task — this is the brief that goes to the designer next.

# Battle UI v2 — rework plan

## 0. Response to the first critique (8 points)

| # | Critique point | Status |
|---|---|---|
| 1 | Group-target confirm is dead code | This plan's Gap #18 decision (§1) — routing fix in the README's for-the-engine notes |
| 2 | MP preview doesn't cover Wool | Fixed (§0a) |
| 3 | Break/Brave/Transformation/Second Life invisible | This plan's core addition (§3, §5) |
| 4 | Status tag line untested against real combinations | Fixed — cap/overflow rule (§0a) |
| 5 | Gamepad is an unowned shrug | Spec'd now, phase 7, owned decision (§1, §4) |
| 6 | Single-line bark was wrong, not 50/50 | Confirmed — keeps the rolling log as spec (§1, Gap #7) |
| 7 | Combat feel had zero design input | This plan's §6 |
| 8 | No boss/elite/summon presentation | This plan's §5 |

## 0a. Defect fixes from the first critique pass

**MP-only cost preview, on a Wool-built character.** Fixed: the preview model
generalizes to `{ costMp, costWool }` per skill; hovering any skill previews *both*
resources it touches. Wool has no bar to overlay a segment on (discrete pips, not a
fill), so its preview brightens the pips that would be spent instead of changing fill.

**No wrap/priority rule for status pills.** Fixed: cap visible pills at 3 per plate,
sorted by category priority (harm red → control blue → special amber → benefit
green), overflow beyond the cap collapses into one `+N` chip. Full list on
hover/inspect. (This rule itself had a bug — see §0b point 6.)

## 0b. Response to the second critique — a review of the built prototype

A second pass reviewed the actual `.dc.html` prototype and README this plan produced,
not just the plan text. Verified against the prototype source directly (line numbers
below refer to the pre-fix draft). Twelve points raised; ten are real and fixed here,
two are reframed, and one new defect the critique itself missed is added at the end.

| # | Point | Verdict | Resolution |
|---|---|---|---|
| 1 | Targeting inferred from `/ALL/.test(displayString)`; self/ally skills enter the same "enemy plates clickable" state | **Confirmed, critical.** `pick()` regexed the TARGET stat row for the bark line, and — worse than the critique states — nothing gated enemy-plate clickability on the skill's actual target type at all; a self-only skill (Woolgathering) reaching Target depth would light enemy reticles. | Typed target field on every skill/item (§4). Enemy plates only go live for enemy-type targets; ally plates (new) only go live for ally-type targets. |
| 2 | Unreachable melee targets still resolve; framed as contradicting the README | **Real gap, reframed.** The README already declared this a deliberate policy ("presentation only, domain rejects"), so it isn't self-contradictory — but the policy leaves an undefined case: what does the player see when a rejected click fires? That's the actual hole. | Strengthened, not overturned: unreachable targets become genuinely non-clickable in the UI (§4 rule 2), with domain-side rejection kept only as defense-in-depth, not the primary gate. |
| 3 | Group confirm reads as picking one enemy, not confirming the group | **Agreed.** | Group targeting now shows one linked bracket around every valid plate instead of N separate reticles, plus a `CONFIRM: ALL ENEMIES` control in the target prompt as the primary gesture (§4 rule 1). |
| 4 | Intent UI shows no ability name, no stable identity across duplicate enemies, no marker for a threatened ally | **Agreed, and partially already solved elsewhere.** `FightHudModel.DisplayNames` already numbers duplicate enemies ("Elite Bog Witch 1/2") stably across a fight — the mock just didn't use it. | Reuse that numbering on plate name, stage nameplate, and intent tooltip (one identity, three surfaces). Intent box gains the ability name (text, until glyphs land) alongside the damage number. An enemy intent aimed at an ally now marks that ally's plate the same way a target reticle marks an enemy's. |
| 5 | "Audited clean at four aspects" is unsupported; roster stack overlaps the hero sprite | **Confirmed by arithmetic on the mock's own declared coordinates**, not just "unsupported": hero placeholder spans x340–570 / y280–580; two roster mini-plates span x40–492 / y264–384 (or y306–426 transformed). Real, verifiable overlap on both axes at the mock's own numbers. | The "cannot violate" claim is withdrawn — it was wrong. Roster width capped to 296px (x40–336), clearing the hero box's x340 start. This closes the mock's own overlap; it does **not** stand in for a real audit. Re-verify against `FightStageAnchors.cs`'s actual coordinate system and final actor art before build sign-off — same discipline as AUDIT #45's "re-measure first," not a one-time geometry patch trusted forever. |
| 6 | Status rules conflict: BRK "added at the front" then sorted behind red/blue by category priority, so a broken enemy with 3+ ordinary statuses can lose its BRK badge to overflow | **Confirmed — real internal contradiction**, and it fights this plan's own earlier decision. §3 already specified a dedicated stage state-flag slot for BROKEN, separate from the plate pill row. The pill-row implementation re-added BRK as a pill and let it compete with ordinary statuses for the 3-pill cap. | Reconciled by removing BRK from the pill row entirely — it lives only in the dedicated flag badge already spec'd in §3/§5. The plate pill row shows ordinary statuses only, 3 visible + overflow, no BRK collision possible. |
| 7 | Status detail ships mouse-first (hover/inspect), gamepad last (phase 7 in the old ordering), contradicting this plan's own "nothing hover-only" rule | **Confirmed, real contradiction.** | The focus/inspect *abstraction* (what shows full detail, and when) is now built in phase 1 as an input-agnostic mechanism — mouse hover and keyboard focus both drive it from the start. Full gamepad *navigation* (moving the focus cursor with a pad) stays a later phase; what changes is that the display mechanism was never mouse-only to begin with. |
| 8 | Roster dots / Brave chevrons / Second Life dot rely on color alone | **Agreed.** | Category dots gain a shape cue in addition to color (not just hue). Brave pips gain a numeral, matching the verb-row badge's existing text treatment. Second Life becomes a filled-ember vs. snuffed-grey silhouette pair, not just a ring-color change. First appearance of Second Life, Transformation, and Brave (the three genuinely novel mechanics) gets a one-time bark-line callout — not a new tutorial system, reusing the log that already exists. Declined: a teaching prompt on every element: that's tutorial fatigue for things that are self-explanatory once labeled (HP/MP bars, status pills). |
| 9 | Wool shortage isn't shown — the preview formula only defines the pips that *would* be spent, which goes negative when the player can't afford the skill | **Confirmed by running the formula.** `[currentWool − costWool, currentWool)` at Wool 3 vs. cost 5 evaluates to `[-2, 3)` — undefined for the 2 missing pips. | Existing pips that would be spent keep the brighter glow. The deficit — pips beyond current Wool up to the cost — get a distinct red-outlined "missing" treatment instead of looking like ordinary empty pips. Cost text gains a `NEED 2` suffix when short, matching how an unaffordable MP cost already dims instead of just failing silently. |
| 10 | Too many simultaneous infinite-loop animations compete (break flash, reticle pulse, NOW pulse, low-HP pulse) | **Agreed, and it undercuts a pattern this plan already endorsed** (§7 cites Hades' restraint — "big effects rationed so they stay big" — without actually operationalizing it). | New house rule, applied to every state added in this pass: **transition, then settle.** A break flashes 2-3 times and rests in a static broken look (border tint, no more animation). Low HP kicks once, then holds a slow pulse only on the affected portrait/HP bar, not a screen-wide loop. Existing NOW-label and reticle pulses are unchanged (they're already single, load-bearing signals, not stacking with anything new). |
| 11 | Low-HP state doesn't identify *which* ally is endangered, and has no hysteresis (flicker risk right at the 25% line) | **Agreed.** | The endangered ally's own plate/portrait is the primary signal (desaturation + the settled pulse from point 10); the vignette is secondary reinforcement only. Threshold gets hysteresis: activates below 25%, clears above 30%. Multiple critical allies each show their own primary signal; a roster mini-plate in danger shows the same cue at dot scale. Second Life recovery clears the state with one brief positive flash rather than a silent cutoff. |
| 12 | Boss mode (persistent) and boss intro (transient) are conflated — the mock's single `bossMode` toggle drives the bark banner continuously | **Agreed.** REWORK_PLAN already separated these narratively (the intro was always "choreography, not a static state") but the interactive mock didn't demonstrate the separation, which is exactly how a builder ends up wiring the banner to every turn of a boss fight instead of just its opening 0.6s. | Two explicit, named states from here on: `encounterType: boss` (persistent — double-width plate, gold tag) and `bossIntroPhase: locked \| bannerIn \| landing \| bannerOut \| returned` (transient, plays once at encounter start). The bark banner style is keyed to the intro phase, never to `encounterType` alone. |

**New finding this pass, missed by both critiques: ally-targeted items have no
target picker at all.** Health Potion and Ether Draught both target `ONE ALLY`, but
nothing in the interaction model makes a party or roster plate clickable — only
enemy plates were ever wired as targets. This is the same class of gap as point 1
(targeting derived from too little information) but on the *ally* side, and it's a
real hole: an ally-target consumable currently has no way to resolve in this design
at all. **Fix:** generalize the same reticle/border-state machinery enemy plates use
to ally plates (party plate + roster mini-plates) whenever the typed target is
`SingleAlly`; `AllAllies` skills (Woolgathering-adjacent support spells, if any are
added later) confirm the same way group-enemy targets do — one linked bracket, no
individual selection.

**Declined from the second critique:**
- **The full acceptance matrix** (aspect ratios × content extremes × input devices as
  a formal test grid) is good QA practice but reads as a test protocol, not a design
  brief — this project's own `docs/HANDOFF_TEMPLATE.md` keeps that class of detail in
  `GAP_AUDIT.md`, written *after* the build, not in the handoff that precedes it. A
  condensed version lives in this document's Verification section instead of a
  standalone matrix.
- **Formal usability-testing requirements** (misclick rate, comprehension timing)
  before finalizing priority order — reasonable for a team with playtesting
  infrastructure; nothing in this project's tooling supports it today. Softened
  instead: value claims below are marked as ordering, not certainty.
- **A single mandated fix for the 4:3 roster problem** (the critique offered a
  horizontal-strip-inside-the-plate option and a top-edge-rail option) — both are
  legitimate; picking between them is a designer call, not a plan call. The
  requirement (a defined safe rectangle per aspect ratio, re-verified against real
  art) is adopted; the specific layout choice is left open in §3.

## 1. Decisions on the recorded open questions

One recommendation each; all reversible at the designer stage.

| # | Question | Decision | Why |
|---|---|---|---|
| AUDIT #45 | Detail column vs front enemy's feet | **Re-measure first** (the audit's numbers predate the column's move to bottom-right ≈(750,−366)); regardless, adopt the state rule: *transient panels leave the 48px ground band at Target depth* — detail column slides out (reverse `ColumnOpenAnimator`) when targeting begins | Geometry can't close per `FightStageAnchors.cs:80–85`; at Target depth the player needs the stage, and the detail info was consumed at selection time |
| Gap #7 | Bark: single state line vs 4-line rolling log | **Keep the rolling log; codify it as spec** | Breadcrumb + target prompt already do the "what do I do" job; the log is the only place variance/multi-packet/detonation math can be reconstructed. Dim older lines so the newest reads first |
| Gap #11 | Enemy status tag-line | **Replace with discrete pills** (§5) | Closes the last "partial"; shared component with party badges |
| Gap #18 | Group-target skills resolve instantly | **Route them through Target depth with a group-bracket confirm** (§0b point 3, §4) | An AoE with mana cost + variance is the most expensive misclick in the game |
| Gap #24 | Gamepad | **Spec the focus/inspect abstraction in phase 1 (input-agnostic); full pad navigation last** (§0b point 7, §4) | The display mechanism must never be mouse-only, even before pad input is wired |
| — | Party plate shows one character; stage holds 3 | **Roster mini-plates**, capped width so they clear the ally sprite footprint (§0b point 5) | Information, not interaction — no swap mechanic |
| — | Ally-targeted items have no target picker | **Ally plates (party + roster) go live using the same reticle machinery as enemy plates** (§0b new finding) | Otherwise Health Potion/Ether Draught cannot resolve in this design at all |
| AUDIT #57 | First boss kill must feel rewarding | **Ember ceremony** between victory beat and Reckoning (§6) | Uses only existing machinery |
| — | Blur (asked for in the original brief) | **No blur.** Vignette-weight pulses via the live post stack instead | No UI shader budget, frame-sequence VFX only |

## 2. Rough look (style intent)

Deep-violet translucent panels (`#1A1024E6` family) with gold accents (`#E7B25C`/
`#FFE0A8`), sharp square rows and bars (radius 0; radius 4 on panels only), 1px
hairlines, Chakra Petch throughout, uppercase tracked section labels, HP red
`#E07A62` / MP blue `#7EA8E6` / target amber `#FFC45A`. Gradients and glows are baked
into sprites — no CSS-style effects exist in engine. New colors: exactly **one** new
palette token (poison-violet, for detonation popups). All copy through
`UiStrings.cs`; all colors through `FightHudPalette.cs`.

## 3. Layout — the six regions, plus everything new

Canvas 1920×1080 centre-origin; must audit clean at 4 aspects (16:9, 21:9, 4:3,
16:10) — "must," not "does," until someone actually runs `UiAudit` against built
geometry (§0b point 5). No seventh region.

| Region | Stays | New tenants |
|---|---|---|
| **Initiative tracker** | yes | ghost-chip turn-order preview on push/pull skills; cracked-chip overlay for a broken enemy's skipped turn; chip collapse-and-slide on death; greyed (not removed) chip for a downed ally |
| **Bark strip** | yes — rolling log codified | dim-gradient on older lines; hosts the boss name banner during `bossIntroPhase` only, never for the whole `encounterType: boss` duration (§0b point 12) |
| **Enemy plates** | yes | break-shield tick row under the HP bar; status pill row (ordinary statuses only — BRK excluded, §0b point 6); boss plate spans both grid columns |
| **Party plate** | yes | transformation strip (docked on top); Brave pips with numeral (§0b point 8); Second Life ember/snuffed icon pair; **roster mini-plates capped to 296px width** so they clear the ally sprite's footprint (§0b point 5), now also **clickable as ally targets** (§0b new finding) |
| **Command columns** | yes, unchanged skeleton | detail column exits at Target depth |
| **Stage** | yes | intent box gains ability name + expected damage + stable per-slot numbering shared with the plate and tracker (§0b point 4); state-flag slot above the nameplate for BROKEN / MARKED (BRK lives *only* here, not in the pill row); elite gold foot-ring |

Ground-band rule (48px above each stage slot's feet stays clear of always-visible
panels) remains the law. At 4:3, the roster stack's specific treatment (compress into
a horizontal strip inside the party plate, vs. relocate to a top-edge rail) is left as
a designer choice — the requirement is a defined, non-overlapping safe rectangle at
that aspect, verified with final-size art, not a specific layout mandate from this
document.

## 4. How the buttons and systems work

**Unchanged core:** ATTACK skips to targeting; SKILL/ITEM open the scrollable
submenu (hover = select, drives detail panel + dual-resource cost preview; click =
confirm); HOLD BACK banks an action; BACK/Esc at every depth; breadcrumb names the
depth; parent columns stay visible.

**Typed target model (§0b point 1) — foundational, everything else depends on it.**
Every skill and item declares an explicit target type: `Self`, `SingleAlly`,
`AllAllies`, `SingleEnemy`, `AllEnemies`. Nothing about targeting behavior is ever
derived from a display string. The type decides: which plates go live (enemy plates
for the two enemy types, ally plates for the two ally types, nothing for `Self`),
whether entering Target depth needs a plate click at all (`Self` resolves the moment
it's picked, no Target depth), and the confirmation gesture (single types confirm on
one plate; the two `All*` types confirm via the group bracket, §0b point 3).

**Refinements:**
1. **Group-target confirm.** Entering Target depth for an `All*` skill draws one
   linked bracket around every valid plate (not N individual reticles) and shows a
   `CONFIRM: ALL ENEMIES` / `CONFIRM: ALL ALLIES` control in the target prompt as the
   primary gesture; clicking any bracketed plate also confirms. Esc backs out.
2. **Melee-reach: real rejection, not just dimming.** While a melee action is
   selected, unreachable enemy plates dim, grey their reticle, and **stop accepting
   clicks in the UI itself** — this is the primary gate now, not a hint waiting on
   domain-side rejection. Focusing (hover or keyboard focus) a dimmed plate shows a
   short reason ("Blocked by front rank") rather than silently doing nothing.
3. **Turn-order ghost preview.** Hovering a push/pull skill ghosts the projected
   reorder in the initiative tracker (`TurnOrder.Project()` already computes this).
   Likely valuable — unconfirmed by any usability test, since none exists on this
   project yet; sequence it early because it's cheap relative to its plausible payoff,
   not because its value is proven.
4. **Focus/inspect abstraction (§0b point 7).** One mechanism decides what shows full
   detail (a status pill's tooltip, an intent's full sentence, a plate's reason-when-
   blocked) — driven by mouse hover **and** keyboard focus from phase 1. Full gamepad
   cursor navigation is a later phase; the display mechanism itself is never
   mouse-only, from the start. Standing constraint: never add an interaction reachable
   only by mouse hover.

## 5. Enemy presentation and status effects

**Intents — perfect information, Slay-the-Spire lineage.** Each intent box shows the
ability name, expected damage (`~14` — "about," because of ±20% variance), a scope
glyph, and — new — a stable per-slot number shared by the plate, the stage nameplate,
and the intent tooltip whenever duplicate enemy names exist (reusing
`FightHudModel.DisplayNames`, not inventing a new marker system). An intent aimed at
an ally marks that ally's plate the same way an enemy reticle marks a target.

**Break meter.** Ticks on the plate. On break: amber hit-flash variant, a dedicated
`BROKEN` stage flag badge (never a pill — §0b point 6), cracked initiative chip,
intent icon swaps to a stun-dash glyph, one full-weight hit-stop regardless of the
triggering hit's damage (a documented exemption from the weight curve — a break is a
state change, not a damage event), then the break's *own* flash settles per the
transition-then-settle rule (§0b point 10) rather than looping indefinitely.

**Boss/elite.** Elite: gold foot-ring + gold name. Boss: double-width plate,
`encounterType: boss` persistent; the intro banner is its own transient
`bossIntroPhase` sequence (lock → banner-in → landing → banner-out → control
returned), never re-triggered mid-fight, never conflated with the persistent boss
styling (§0b point 12).

**Summons.** Fade in with a soft-red flash variant, plate row slides into the grid, a
chip slides into the initiative tracker — the beat that matters, since summons take
turns.

**Status pills.** 20px sharp-cornered pills, category-colored border (red = harm,
green = benefit, blue = control, amber = special — SPD/MARKED only; BRK excluded,
§0b point 6). Cap 3 visible + one `+N` overflow chip, full list on hover/inspect.
Icon art exists for 2/12 today — ship first with 2-letter text codes in the same
pill container so nothing reflows when glyphs land.

## 6. Combat-feel intent

**Hit frames stay the spine.** Every attack, stance, and spell already declares its
impact frame; damage number, silhouette flash, freeze, shake, recoil, and voice all
fire on it.

**Freeze / shake: don't touch the shared curve.** New events map into it (break and
boss-landing at full weight; detonation weighted by its damage). Shake stays on the
stage racks, never the camera or HUD.

**Damage numbers.** Keep the semantic triad (red damage / green heal / grey Miss); a
small element glyph beside the number carries type, scale and motion carry
effectiveness (super-effective 1.3× harder punch-in + gold chevron + bloom; resisted
0.8×, no bloom, down chevron). Multi-packet casts stagger 90ms with x-jitter, one
glyph each. Poison detonation gets its own oversized popup in the new poison-violet
token. Popup pool (6) can't cover 3 packets × 3 enemies — raise to ~12 or coalesce
beyond 2 packets/target (flag for implementation).

**Screen effects.** No blur. A 0.15s vignette kick on full-weight hits; the low-HP
state (§0b point 11) is per-ally-primary, vignette-secondary, hysteresis-gated
(activate <25%, clear >30%), one settle per the transition-then-settle rule — not a
sustained global loop.

**Deaths and endings.** Enemy death: fade + plate greys-and-slides-out + tracker chip
collapse. Ally down: full-weight stop, grayscale portrait, chip greyed not removed.
Victory: hold a 0.8s beat on stage before Reckoning mounts. Defeat: 1.2s desaturation
ramp, then the defeat screen. **First-boss Ember (#57):** scrim deepens, an ember
sprite rises from the boss's position (~1.5s), one bark line, warm gold vignette,
then Reckoning opens with the Ember as a pre-claimed glowing top row.

## 7. Patterns borrowed

- **Slay the Spire / Into the Breach** — public enemy math, finished by naming the
  ability and linking stable identity on the intent box.
- **Darkest Dungeon** — dim-and-reject the unreachable, don't just dim it.
- **FFX CTB** — action-reorder preview in the turn list (marked as a hypothesis, not
  a proven win — §0b declined point).
- **Hades** — restraint, now actually operationalized as transition-then-settle
  (§0b point 10), not just cited.
- **Persona 5** — the responsiveness, not the diagonal chaos: imitate the feel,
  refuse the look (this project's sharp-square, four-aspect-audited house style would
  fail P5's skewed layouts on its own tests).

## 8. Deliverable format and phasing

**Deliverable:** `docs/handoffs/battle_ui/` — `README.md` (build spec), this file
(decision record), `Battle UI v2.dc.html` + `support.js` (interactive prototype,
authoritative for layout/behavior of everything changed or added in this pass),
`Battle UI.dc.html` (v1, kept for before/after diffing). `GAP_AUDIT.md` stays
untouched as the permanent v1 record; a fresh audit is written after this pass
builds.

**Phasing** (revised — interaction/identity foundation now comes first, since §0b
showed visual features built on top of an untyped, unstable model is exactly how the
`/ALL/`-regex and duplicate-name bugs happened):

1. **Interaction foundation.** Typed target model, valid/invalid target state with a
   shown reason, stable per-combatant identity reused everywhere, the input-agnostic
   focus/inspect abstraction (mouse + keyboard).
2. **Information surfacing.** Break meter, status pills (BRK excluded, text-code
   interim), roster stack (geometry-safe), Brave/Transformation/Second Life
   (shape+text redundant, one-time bark callout on first appearance), intent ability
   name + damage + identity + ally marker, dual-resource preview including the
   deficit state.
3. **Flow.** Group-target bracket confirm, ally-target plates, real melee-reach
   rejection, turn-order ghost preview.
4. **Responsive layout.** Re-verify all four aspects with final-size actor art and
   the longest real skill/item lists; confirm the prototype's submenu reflects the
   already-built 8-row/24-pooled masked scroll rather than free-flowing rows.
5. **Combat feedback.** Multi-packet stagger, effectiveness scaling, detonation
   moment, vignette pulses, hysteresis-gated low-HP state, death/chip animations,
   victory beat — everything built to the transition-then-settle rule.
6. **Ceremony.** Boss intro as its own phase sequence (never conflated with
   persistent boss styling), elite dressing, summon arrival, Ember moment, glyph art
   lands.
7. **Gamepad.** Full pad cursor navigation, wired into the phase-1 focus abstraction
   rather than retrofitted onto it.

## Critical files

- `Assets/_Project/Scripts/Domain/UiKit/Screens/FightScreen.cs` — the screen tree
- `Assets/_Project/Scripts/Domain/UiKit/FightHudPalette.cs` — one new poison token only
- `Assets/_Project/Scripts/Domain/Stage/FightStageAnchors.cs` — coupled stage constants; also the real coordinate system the mock's roster/hero geometry must be re-verified against (§0b point 5)
- `Assets/_Project/Scripts/Domain/Combat/Session/FightHudSpec.cs` — capacities (popup pool, pill counts, mini-plates)
- `Assets/_Project/Scripts/Domain/Combat/Session/FightHudModel.cs` — `DisplayNames` numbering to reuse for stable identity (§0b point 4)
- `Core/FightController.*.cs`, `Core/FightBeatPlayer.cs` — painting/playback hookups
- `docs/handoffs/battle_ui/{README.md,GAP_AUDIT.md}`, `docs/HANDOFF_TEMPLATE.md`

## Verification

- This task: brief lands as `docs/handoffs/battle_ui/{REWORK_PLAN.md,README.md}`; no
  build needed.
- Downstream: every phase runs `tools/run_tests_parallel.ps1 -BuildScenes`; `UiAudit`
  clean at all four aspects (not asserted here — actually run); ground-band test
  (`NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet`) still green; legibility tests
  extended with the low-HP grade state; `tools/screenshot.ps1 -Panel Fight` (and
  `-Runtime` for anything that moves) eyeballed per WORKFLOW §8.
- Condensed acceptance checklist for whoever builds phase 1-4 (not a substitute for
  `GAP_AUDIT.md`, which is written after the real build): every target type
  including self/ally/ally-group/enemy/enemy-group resolves through the typed model;
  an unreachable melee target is non-clickable via mouse and keyboard; a plate with
  zero, three, and 4+ statuses all render without wrapping the plate taller; a broken
  plate's BRK badge never disappears regardless of ordinary status count; two enemies
  sharing a name are distinguishable on plate, stage, and intent tooltip; an
  unaffordable Wool, MP, and dual-cost skill each show a legible shortage, not a
  silent non-preview.
