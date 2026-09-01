# Handoff: Battle UI — v2 rework (break meter, statuses, roster, ceremony)

## 1. Overview

v2 rework of the nested command HUD. The nested verb → choice → target system,
initiative tracker, party plate, and detail column are **unchanged and already
built** (see `GAP_AUDIT.md` — 22/27 items closed against v1). This pass adds the
layer v1 never covered: enemy break/status presentation, three domain systems with no
prior UI surface (transformation, Brave banking, Second Life), a turn-order preview,
melee-reach teaching, and the group-target confirm flow — plus a typed target model
that everything else depends on. Decisions and the two rounds of critique behind each
change are recorded in `REWORK_PLAN.md` — read that first if a "why" here seems
unmotivated, especially §0b, which corrects several defects an earlier draft of this
pass shipped with.

**Not required to re-implement:** anything already `match`/`closed` in
`GAP_AUDIT.md`. This handoff only specs *changed* and *new* elements; unlisted
elements (root verb column, submenu, breadcrumb, wool pips, etc.) stay exactly as
built.

## 2. Files

| File | What it is |
|---|---|
| `Battle UI v2.dc.html` | The updated design prototype. Open in a browser; fully interactive. Authoritative for layout and behavior of everything changed/added in this pass — **with the limitations noted in §7**, since the prototype is a design reference, not production code. |
| `support.js` | Runtime the prototype needs. Must sit beside the HTML. Not part of the design. |
| `README.md` (this file) | Spec for build. |
| `REWORK_PLAN.md` | The decision record — why each change was made, including the two critique passes that shaped it, phasing, patterns borrowed. |
| `GAP_AUDIT.md` | v1's permanent build-vs-spec record. Untouched; a fresh audit gets written after this pass builds. |
| `Battle UI.dc.html` | v1 prototype, kept for before/after diffing. No longer authoritative. |

## 3. Layout — exact coordinates (1920×1080 reference, unchanged regions omitted)

All v1 regions and coordinates stand (turn timeline top-left x40/y34; bark top-centre
y34 max-width 840; enemy plates right x40/y150 width 400; party plate left x40/bottom
40 width 452; command columns left524/right472/bottom40 height300; target prompt
bottom364). New geometry only:

| Element | Position / size |
|---|---|
| Break-tick row | Inside each elite/boss enemy plate, directly under the HP bar. Row height 6px, gap 3px, max 8 segments, full row width = plate content width (400 − 32 padding = 368px, ÷ segment count). |
| Status pill row | Replaces the old tag row, same position (9px below the HP bar / break row). **Ordinary statuses only — BRK is never a pill** (§4, was a real conflict in the previous draft, see `REWORK_PLAN.md` §0b point 6). Cap 3 visible + one `+N` overflow chip; never wraps the plate taller. |
| BROKEN state-flag badge | A distinct element from the pill row: inline before the enemy name on the plate, and on the stage above the nameplate (see "Stage intent chips" below). This is the *only* place BRK appears. |
| Elite/boss badge | Inline before the enemy name, same baseline as the BROKEN badge (mutually exclusive in this mock's demo data, but the component must support both existing at once). |
| Boss plate (`encounterType: boss`) | Same right/top anchor, width **800px** (spans what would be two plate-widths) instead of 400px. Persistent for the whole fight — do not key this to the intro banner (§5 rule 5). |
| Intent chips (stage) | One per enemy slot: a 46×46 icon+damage box, the ability name in small text below the icon, an optional state-flag chip above it (BROKEN, or MARKED — the two can be shown as a short stacked list, not forced to choose one), and the nameplate label. A stable per-slot number (or letter) appears wherever two enemies share a display name, matching the same identity shown on that enemy's plate — reuse `FightHudModel.DisplayNames`, do not invent a second numbering scheme. `⚔` prefix for single-target intent, `⤨` for group. Damage text always prefixed `~` (variance is ±20%, "about" is honest). When an intent targets an ally rather than an enemy, that ally's plate (party plate or roster mini-plate) shows the same marker treatment a target reticle would give an enemy plate. |
| Transformation strip | `left:40, bottom:256` (directly on top of the party plate's top edge), width 452, height 34. Border matches party plate but `border-bottom:none` so it reads as one fused frame. Only rendered while `isTransformed`. |
| Brave pips | Inline in the party plate header row, right of the class line, gap 4px, 2 glyphs **plus a `1/2`-style numeral** — color/fill alone is not sufficient (§4). |
| Second Life indicator | Small icon beside the party portrait — **an ember silhouette when available, a snuffed/grey silhouette when spent** (a shape change, not only a color/glow change — §4). |
| Roster mini-plates | `left:40`, **width 296px** (not 452 — narrower than the party plate on purpose), each 56px tall, gap 8px, stacked bottom-up starting at `bottom:264` (party plate top + 8px gap), or `bottom:306` when the transformation strip is showing. **Now clickable as ally targets** when a `SingleAlly`-typed skill/item is selected (§5 rule 1) — same reticle/border-state machinery as an enemy plate. |
| HOLD BACK bank badge | Text-appended to the verb label itself: `HOLD BACK · BANK {n}/2` — no separate element. |
| Low-HP indicator | Primary signal is the affected ally's own plate (desaturated portrait + a settled slow pulse, not a loop — §4). A full-canvas radial vignette is secondary reinforcement only, `inset:0`, `pointer-events:none`, sitting above the background layers, below all HUD panels. |

**Roster width correction.** An earlier draft of this pass sized the roster stack at
452px (matching the party plate) and asserted it "cannot violate" the stage
ground-band because it grows upward, away from the stage. That claim was checked
against the mock's own declared coordinates and was wrong: at 452px width, the roster
stack overlaps the hero sprite placeholder on both axes. **296px is the corrected
width**, chosen so the roster's right edge (x=336) clears the hero placeholder's left
edge (x=340) in the mock's own coordinate system. This closes the mock's
self-inconsistency; it is **not** a substitute for a real audit. Before build
sign-off, re-verify against `FightStageAnchors.cs`'s actual coordinate system and
final actor art — the mock's placeholder box is a rough stand-in, not the real
silhouette. At 4:3, the roster's specific treatment (compress to a horizontal strip
inside the party plate, or relocate to a top-edge rail) is a designer choice, not
mandated here; the requirement is a defined, non-overlapping safe rectangle at that
aspect, verified with final-size art.

## 4. Typed target model (foundational — read before the state tables below)

Every skill and item declares an explicit target type — never inferred from a
display string:

```
Self | SingleAlly | AllAllies | SingleEnemy | AllEnemies
```

This decides three things, uniformly, for every ability in the game:
- **Which plates go live** at Target depth: `SingleEnemy`/`AllEnemies` → enemy
  plates; `SingleAlly`/`AllAllies` → party plate + roster mini-plates; `Self` → no
  plates, resolves immediately without entering Target depth at all.
- **The confirmation gesture**: single types confirm on one plate click (or Enter on
  the focused plate); the two `All*` types show one linked bracket around every valid
  plate and confirm via a `CONFIRM: ALL ENEMIES` / `CONFIRM: ALL ALLIES` control in
  the target prompt, or a click on any bracketed plate.
- **Unreachable-target rejection**: for a melee-flagged `SingleEnemy` skill, a plate
  whose domain-side `frontRow` is false is not just dimmed — it stops accepting
  clicks in the UI. Focusing it (mouse hover or keyboard focus) shows a short reason
  ("Blocked by front rank") instead of doing nothing. This is the primary gate now;
  domain-side rejection is kept only as defense-in-depth, not the mechanism a player
  is expected to hit.

An earlier draft of this pass derived group-vs-single behavior from regex-matching
the displayed `TARGET` stat text (`/ALL/.test(...)`), and gated every enemy plate's
clickability on `depth === "target"` alone — meaning a **self-only** skill like
Woolgathering could leave enemy plates clickable. Both are fixed by this model; do
not reintroduce string-based target detection anywhere in the real implementation.

## 5. Interaction / logic rules

1. **Group-target confirm.** Entering Target depth for an `AllEnemies` or
   `AllAllies` skill draws one linked bracket around every valid plate (not N
   individual reticles — a stack of separate diamonds reads as "pick one," not
   "confirm all"). Any bracketed plate click, or Enter on gamepad/keyboard, confirms
   the whole group. Esc backs out to the submenu it came from, same as single-target.
2. **Melee-reach rejection.** While the currently hovered submenu row *or* the
   confirmed `sel` at Target depth has `melee: true`, every enemy plate whose
   domain-side `frontRow` flag is false dims to 50% opacity, its reticle greys out,
   does not scale up on hover, and **does not accept a click** — this is UI-enforced,
   not merely a hint. A focused-but-blocked plate shows a one-line reason. The
   domain must still reject the action if it somehow fires (defense in depth), but
   the UI is expected to be the actual gate players hit.
3. **Ally targeting.** `SingleAlly` skills/items (Health Potion, Ether Draught) make
   the party plate and roster mini-plates live using the same border-state and
   reticle machinery enemy plates use. This closes a gap the previous draft had
   entirely: nothing made an ally clickable, so an ally-target consumable had no way
   to resolve.
4. **Detail column exits at Target depth.** The persistent third column
   (`ColumnOpenAnimator`, reversed) slides out the moment `depth` becomes `"target"`,
   regardless of branch, and stays hidden until the player backs out to `sub` or
   picks a new root verb.
5. **Break meter.** Ticks are purely presentational against a domain-side
   `breakCurrent / breakMax`. When `breakCurrent >= breakMax`: play the amber
   hit-flash variant **2-3 times, then settle** into a static broken look (border
   tint, no more looping animation — see the transition-then-settle rule below), show
   the dedicated `BROKEN` flag badge (never a pill), crack the initiative chip, swap
   that enemy's intent icon to a stun-dash glyph, and fire one full-weight hit-stop
   regardless of the triggering hit's damage (the one documented exemption to the
   damage-weighted HitStop curve).
6. **Boss/elite plates.** Elite = gold `ELITE` tag + gold name, normal-width plate.
   Boss = gold `BOSS` tag + gold name + double-width plate (800px), persistent for
   the whole encounter (`encounterType: boss`). **This is a separate state from the
   intro banner.** The boss intro is its own transient sequence —
   `bossIntroPhase: locked → bannerIn → landing → bannerOut → returned` — that plays
   once at encounter start (0.6s input lock, name banner in the bark region,
   hit-stop on the landing frame, max rack-shake) and never re-triggers mid-fight. An
   earlier draft's demo toggle conflated these (flipping "boss mode" drove the banner
   continuously); the bark banner style must be keyed to `bossIntroPhase`, never to
   `encounterType` alone.
7. **Turn-order ghost.** Purely a hover-driven preview: no state is committed, nothing
   in the actual queue moves until the skill is confirmed. Only skills with a
   domain-side push/pull property get this treatment. (Sequenced early because it's
   cheap relative to its plausible payoff — this project has no usability-testing
   infrastructure to confirm it's the highest-value addition, so treat that ordering
   as a reasonable guess, not a proven result.)
8. **Transformation / Brave / Second Life.** All three are pure display of existing
   domain state — no new interaction model, not clickable. On each mechanic's first
   appearance in a fight, the bark strip gets a one-time callout line (reusing the
   existing log, no new tutorial system) — these three are the only additions that
   get this treatment; the rest (statuses, break meter) are legible enough from
   their visual design alone.
9. **Dual-resource cost preview (party plate).** Cost model is `{ costMp, costWool }`
   per skill/item; hovering previews both resources it touches. MP: lighter segment
   overlaid on the filled end of the bar, `width = costMp / mpMax`. Wool: pips have
   no bar to overlay, so the pips that *would* be spent get a brighter glow overlay
   on their normal fill. **When the player can't afford the cost, the pips beyond
   current Wool up to the cost get a distinct red-outlined "missing" treatment** —
   not the same look as an ordinary unfilled pip — and the cost text gains a
   `NEED {n}` suffix. (A previous draft's formula only defined the pips that would be
   spent, which goes negative and undefined the moment the player can't afford the
   skill — exactly the case that most needs a clear "you're short" signal.) Same rule
   applies to MP.
10. **Low-HP state.** Triggers per-ally, not globally: the endangered ally's own
    plate/portrait is the primary signal (desaturated, with one settled pulse — see
    below), the screen vignette is secondary reinforcement. Hysteresis: activates
    below 25% HP, clears above 30% (not the same threshold both ways — prevents
    flicker from healing ticks hovering near the line). Multiple critical allies each
    show their own primary signal independently; a roster mini-plate in danger shows
    the same cue at dot scale. Second Life recovery clears the state with one brief
    positive flash, not a silent cutoff.
11. **Transition, then settle — a house rule for every new looping/persistent
    visual in this pass.** A break flashes 2-3 times, then rests in a static broken
    look. Low HP kicks once on crossing the threshold, then holds a slow single
    pulse only on the affected element, never a screen-wide infinite loop. This
    applies to every new animated state added in this pass; it does not change the
    existing NOW-label pulse or targeting reticle pulse, which are each a single,
    load-bearing signal, not additions stacking on top of something else.

## 6. States

### Enemy plate
| State | Border | Extra |
|---|---|---|
| Idle | `rgba(224,120,110,.26)` | — |
| Targeting, not hovered | `rgba(255,196,90,.5)` | glow, reticle amber @ .5 opacity |
| Targeting, hovered | `#ffc45a` | glow, `scale(1.03)`, reticle @ full opacity |
| Targeting, unreachable (melee-blocked) | `rgba(140,120,150,.2)` | plate opacity 0.5, reticle greyed, no scale-up, **no click accepted**, reason shown on focus |
| Broken | static tint (post-settle) | `BROKEN` flag badge shown (never a pill), independent of targeting state |

### Break meter (elite/boss only — normal mobs have no break meter)
Empty segment: `border:1px solid rgba(231,178,92,.4)`, fill `rgba(20,12,10,.8)`.
Filled segment: same border, fill `linear-gradient(#ffe0a8,#e7b25c)`. At
`filled === max`, the plate enters Broken — a state transition, not a gradual fade.

### Status pill (shared component — enemy plate, party plate mini-dots, roster dots)
| Category | Meaning | Border / text |
|---|---|---|
| red | harm (Poison, Vulnerable, Stun) | `#e07a62` / `#f3b7a8` |
| green | benefit (Regen, Protect, Empowered, Shielded) | `#8fbf6a` / `#c9e6ae` |
| blue | control (Chilled, Rooted, Provoked) | `#7ea8e6` / `#c7dcf5` |
| amber | special (SPD, MARKED) | `#e7b25c` / `#ffe0a8` |

**BRK is not a category member — it never appears in this component.** It lives only
as the dedicated flag badge (§3, §6 "Enemy plate" table).

Full pill (enemy/party plates): `padding:3px 7px`, fill `rgba(20,10,16,.7)`, content =
2-letter code + `·` + turns-remaining. Dot variant (roster mini-plates): 8×8, border
in the category color, fill = that color at 27% alpha, **plus a shape distinction per
category** (not color alone — a previous draft relied entirely on hue, which fails
color-vision-safe design).

**Cap and priority.** Max 3 pills visible per plate, sorted red → blue → amber →
green (threat reads first). Anything beyond the cap collapses into one `+N` overflow
chip (neutral violet border, no category color). Full list on hover/inspect.

**Ship order:** code art doesn't exist yet for 10 of 12 statuses. Ship first with
2-letter text codes; when the glyph sheet lands, swap the text node for an icon
inside the same pill container so nothing reflows.

### Party plate additions
| Element | Off / inactive | On / active |
|---|---|---|
| Transformation strip | not rendered | rendered, fused to plate top, gold uppercase text `"{FORM NAME} — {N} TURNS"`; bark callout on first appearance this fight |
| Brave pips (×2) | empty glyph + numeral `0/2` | filled glyph + numeral reflecting banked count, 0–2; bark callout on first bank |
| Second Life | snuffed/grey silhouette, no glow | ember silhouette, gold glow; bark callout on first appearance this fight |

### Ally plate targeting (new — party plate and roster mini-plates)
Same three targeting states as an enemy plate (idle / targeting-not-hovered /
targeting-hovered), active only when the selected skill/item's typed target is
`SingleAlly` or `AllAllies`. No unreachable state exists for allies — reach rules
apply to enemy targeting only.

### Initiative tracker additions
| Trigger | Effect |
|---|---|
| Hovering a push/pull skill | The chip at the projected destination gets a dashed amber outline plus a small `PUSHED →` label. Clears on mouse-leave / selection change. |
| Any tracked enemy is Broken | The corresponding hostile chip gets a diagonal crack overlay. |

### Stage intent chips
Each enemy slot shows: an optional state-flag chip (`BROKEN`, `MARKED` — can appear
together, not mutually exclusive), the ability name, the intent icon+damage box
(`~14`, scope glyph), a stable per-slot identity number where duplicate names exist,
and the nameplate label. An intent aimed at an ally marks that ally's plate.

## 7. Design tokens (new only — v1's token table in the old README stands for everything else)

| Token | Hex | Use |
|---|---|---|
| Poison-violet | `#b168e8` (text `#e3b6ff`) | Poison detonation damage popup — the only new palette color in this pass, through `FightHudPalette.cs` |
| Category red | `#e07a62` / text `#f3b7a8` | Harm-category status pill |
| Category green | `#8fbf6a` / text `#c9e6ae` | Benefit-category status pill |
| Category blue | `#7ea8e6` / text `#c7dcf5` | Control-category status pill |
| Category amber | `#e7b25c` / text `#ffe0a8` | Special-category status pill (SPD, MARKED) and the BROKEN flag badge |

Geometry: break-tick segments 1px border, 0 radius. Pills: 0 radius, 1px border.

## 8. For-the-engine notes

- **Break meter, statuses, transformation, Brave, Second Life** all read from
  existing or planned domain fields — nothing here requires new save data beyond
  what combat already tracks.
- **Group-target routing** is a real code change: `FightController.cs:546-578`'s
  target-depth flow needs to fire for group skills instead of resolving through
  `CastCharacterSkill(skill, null)` immediately (`GAP_AUDIT.md` #18).
- **Typed target field** is a new piece of skill/item data (`Self | SingleAlly |
  AllAllies | SingleEnemy | AllEnemies`) — confirm whether `SkillTargeting` (already
  in the domain, per `SkillEffect.cs`) already covers this exactly before adding a
  parallel enum; if it does, the UI should read it directly rather than re-deriving
  target type from anything display-related.
- **Ally-plate targeting** needs the same click/hover wiring the enemy plates already
  have, generalized to the party plate and roster mini-plates — check whether the
  existing `OnEnemyPressed`-style handler can be parameterized rather than
  duplicated. The prototype only wires this onto the roster mini-plates; the acting
  character's own party plate (bottom-left) needs the identical treatment for a
  `SingleAlly` item used on oneself (Health Potion's stated case) — not modeled in
  the mock, but the same targeting rule applies to it.
- **Stable enemy identity** should reuse `FightHudModel.DisplayNames` — do not invent
  a second numbering scheme for the intent box.
- **Melee-reach rejection** needs a `CanMeleeReach` check exposed per-plate at render
  time — check whether this already exists for domain-side move validation and reuse
  it rather than re-deriving reach in UI code.
- **Boss double-width plate**: confirm `PlateH`/`PlatePitch` coupling in
  `FightStageAnchors.cs:80-85` before implementing — a width change on one enemy
  plate must not silently shift the others' pitch.
- **Roster/hero geometry**: the 296px roster width closes the *mock's* overlap; it
  has not been checked against `FightStageAnchors.cs`'s real coordinates or final
  actor art. Re-verify before considering this closed, the same way AUDIT #45 needs
  a re-measure rather than trusting stale numbers.
- Sprite budget: zero new panel sprites. Break ticks, pills, and badges are
  border-tint + text-color treatments on the existing `panel_red` sprite family.
- All new copy goes through `UiStrings.cs`.

## 9. Explicit out-of-scope (this pass)

- **Gamepad cursor navigation.** The focus/inspect *abstraction* (what shows detail,
  driven by mouse + keyboard) is in scope for phase 1 — see `REWORK_PLAN.md` §0b
  point 7. Moving that focus with a d-pad is phase 7, not built here.
- **Real damage-number/effectiveness/detonation animation.** The "Combat feel" card
  in the prototype is a static reference swatch, not an interactive demo of the
  popup system, stagger timing, or vignette kick — those are motion specs in
  `REWORK_PLAN.md` §6 for the juice phase, not layout/state specs here.
- **Boss intro cinematic choreography** (the 0.6s lock, slide-in, banner timing) is
  described narratively in `REWORK_PLAN.md`, not modeled as an animated sequence in
  the prototype — which shows only the bark-region banner's resting visual, keyed
  correctly to `bossIntroPhase` per §5 rule 6, but not its transitions.
- **Status icon glyph art.** Shipping with 2-letter text codes is the explicit
  interim state — icon art is a separate asset task.
- **Summon arrival** and **first-boss Ember ceremony** — one-shot animation
  sequences, described in `REWORK_PLAN.md` §6, not modeled as static states here.
- **A formal usability-testing protocol** (misclick rate, comprehension timing,
  decision-time measurement) for validating priority claims like the turn-order
  ghost's value — this project has no playtesting infrastructure; value claims in
  this handoff are stated as a reasonable ordering, not a proven result.
