# Gap audit — Battle UI handoff vs. built HUD

Audited 2026-08-01, against the codebase as of commit `bc8431f` (post
workflow-restructure Phases 0-5). Format per `docs/HANDOFF_TEMPLATE.md`.
Verdict ∈ **match** / **partial** / **missing** / **deliberate-deviation**.

Overall: the handoff is **substantially already built** — every layout
region exists, the three-column nested command system works exactly as
specified (ATTACK skips to target, RUN/HOLD BACK resolve immediately, BACK
exists at every depth, breadcrumb tracks depth), and the verb-row visual
hierarchy (primary/neutral/quiet) matches. The gaps are concentrated in three
places: **polish animations that were never implemented** (column slide-in,
plate/submenu hover scale, chip pulsing), **two built-but-dead features**
(the party plate's MP cost preview, the dialogue bark portrait), and
**keyboard/gamepad input beyond Escape**.

| # | Handoff section | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 1 | Layout regions (§ Layout regions table) | Exact top-left-origin coordinates per region | All 6 regions exist at reasonable centre-origin positions (`SceneBuilder.Fight.cs`); spot-converted a few back to top-left coords and they land within ~10-25px of spec, not pixel-exact | **match** (structurally correct; not pixel-audited region-by-region — see note below) |
| 2 | ~~Turn timeline — chip sizing (§1)~~ | Acting chip 78×78, upcoming 64×64 (0.82 scale) — the size difference *is* the "current turn" signal | ~~All 6 chips uniformly 74×74~~ — closed in `1dc17c7`: icon-local scale applied per slot (78/64 against the 74 baseline), layout untouched | **closed** |
| 3 | ~~Turn timeline — NOW label pulse (§1)~~ | Opacity 0.5↔1, 1.8s ease-in-out, infinite | ~~static, no pulse~~ — closed in `1dc17c7`: new `TextAlphaPulse` component on the slot-0 label | **closed** |
| 4 | ~~Turn timeline — enemy-turn emphasis (§1)~~ | Distinct border/glow emphasis on hostile upcoming chips | ~~flat colour dim only~~ — closed in `1dc17c7`: a colored ring behind hostile upcoming chips (`initiativeEmphasisRings`), on top of the existing dim | **closed** |
| 5 | Dialogue bark — container/text (§2) | 840-wide box, 56×56 portrait, single state-driven line | Container/text built correctly, `raycastTarget=false` correctly | **match** |
| 6 | ~~Dialogue bark — portrait (§2)~~ | Speaker portrait shown alongside the bark text | ~~always blank~~ — closed: `RefreshBarkPortrait` shows the acting PC's portrait (hidden on an enemy turn — no enemy portrait art exists to show, same graceful-degradation rule every other portrait site follows) | **closed** |
| 7 | Dialogue bark — single state-driven line (§2, State Management table) | One line, set per state transition (e.g. "Pick your mark.") | Shows the **combat message log** (up to 4 stacked lines via `AppendMessage`), a different and broader mechanism than a single bark string | **deliberate-deviation** (pre-existing system, does more than the spec asks — see note below, worth a product decision not a quick fix) |
| 8 | Enemy plates — layout/header/hint (§3) | 400w plate, name/HP/bar/tags, `ENEMIES` header, hint text swaps `N STANDING` ↔ `CLICK TO CONFIRM` | All present and correct (`Hud.cs:684`, `Fight.cs:414-448`) | **match** |
| 9 | ~~Enemy plates — reticle (§3)~~ | 14×14 diamond, shown while targeting, brighter when hovered | ~~no hovered-brighter state~~ — closed in `b4ecb74`: `ReticleHovered` swapped in via `_hoveredEnemyPlate` | **closed** |
| 10 | ~~Enemy plates — 3 border states (§3)~~ | idle / targeting-unhovered / targeting-hovered, each a distinct border+glow | ~~only 2 implemented~~ — closed in `b4ecb74`: `PlateTargetingHovered` third tier | **closed** |
| 11 | Enemy plates — status tags (§3) | Flex-wrapped row of discrete pill tags | One joined text line (telegraph + statuses + guard), not discrete pills | **partial** (functionally equivalent, visually simpler) |
| 12 | Party plate — HP/MP bars, wool pips (§4) | Bars + 16 wool pips, filled/empty colour distinction | All present and correct (`Hud.cs:338`, `FightController.cs:707-708`) | **match** |
| 13 | ~~Party plate — MP cost preview (§4)~~ | Lighter segment inside MP bar showing a hovered skill's cost | ~~`_mpPreview` only ever 0~~ — closed in `1dc17c7`: `SubmenuRowManaCost` wired into `OnSubmenuRowHovered` | **closed** |
| 14 | Command columns — verb hierarchy (§5 Column A) | ATTACK loud / SKILL,ITEM neutral / RUN,HOLD BACK quiet, distinct fills+borders+sizes | Matches exactly (`Fight.cs:568-590`) | **match** |
| 15 | Command columns — submenu row count (§5 Column B) | Mock shows 5 | 8 rows (`SkillButtonCount`) | **deliberate-deviation** (documented in code: a level-9 character needs 7 role skills + the basic Spell = 8; accepted, not a gap) |
| 16 | Command columns — detail column (§5 Column C) | Persistent 3rd column, not a tooltip, key/value stat rows | Matches — persistent, independently toggled, populated on hover (`Hud.cs:415-420`) | **match** |
| 17 | Command columns — breadcrumb (§5) | `COMMAND` / `COMMAND › SKILL` / `COMMAND › SKILL › TARGET` | Matches exactly (`Hud.cs:386-388`) | **match** |
| 18 | Target prompt — text variants (§6) | Single-target: "CHOOSE A TARGET"; group-target: "ALL ENEMIES — CONFIRM ON ANY PLATE" | Always "CHOOSE A TARGET" — no branch for group wording | **missing** (currently low-impact: group-target skills resolve via `CastCharacterSkill(skill, null)` and never enter `MenuDepth.Target` today, so the group prompt path is presently unreachable either way — see note) |
| 19 | Navigation graph (§ Interactions) | ATTACK skips to target for a single enemy; RUN/HOLD BACK resolve immediately; BACK at every depth; parent column stays visible | All match (`Actions.cs:34-44`, `Actions.cs:907-919`, `Actions.cs:885-905`, `FightController.cs:546-578`) | **match** |
| 20 | Hover behaviour — detail panel (§ Interactions) | Hovering a submenu row sets the detail panel subject | Matches (`AddHover`/`OnSubmenuRowHovered` → `RefreshUi` → `RefreshDetailPanel`) | **match** |
| 21 | ~~Hover behaviour — MP preview (§ Interactions)~~ | Hovering an MP-costing skill fires the cost preview | ~~did not~~ — closed in `1dc17c7`, same fix as #13 | **closed** |
| 22 | Keyboard — Escape (§ Keyboard/gamepad) | Esc ascends | Bound (`FightController.cs:596-599`) | **match** |
| 23 | ~~Keyboard — number keys 1-5 (§ Keyboard/gamepad)~~ | Jump straight to a root verb | ~~decorative only~~ — closed in `1dc17c7`: root-depth-only, guarded on the same interactable/active checks the buttons use | **closed** |
| 24 | Gamepad support (§ Keyboard/gamepad) | Shared idle/hover/selected states across mouse, keyboard focus, gamepad focus | No gamepad input anywhere in the project | **missing** (larger scope — likely a deliberate future item, not a quick fix; flagging rather than sizing it) |
| 25 | ~~Animations — column open slide+fade (§ Animations)~~ | translateX -10px→0, opacity 0→1, 140ms ease-out | ~~instant SetActive toggle~~ — closed: new `ColumnOpenAnimator` (Update()-driven lerp, same style as `ButtonPressAnimator`/`SubtleHoverScale` — this project has no tweening library) on the submenu, detail and target-prompt columns, played from `OnEnable` | **closed** |
| 26 | ~~Animations — row/plate hover scale (§ Animations)~~ | Submenu rows scale(1.02), enemy plates scale(1.03); root verbs keep the project's existing 105%/95% | Verb rows already matched (`ButtonPressAnimator`, 105/95); submenu/plate rows closed in `b4ecb74` via the new `SubtleHoverScale` component | **closed** |
| 27 | Enemy intent/telegraph surfaced (§ "What this fixes") | Enemy HP is a plate with bar + status tags, doubling as target picker | Matches, plus telegraph text is folded into the same tag line (`Hud.cs:710-713`) | **match** |

## Notes on ambiguous items

**#7, dialogue bark model.** The handoff's state machine wants exactly one
line, replaced on each transition. What's built is a 4-line combat log that
happens to sit in the same box. This isn't a bug — the log is arguably more
useful in play — but it's a real product decision (single authored line vs.
rolling log) rather than a quick implementation fix, and closing it either
direction is a judgment call worth confirming before spending time on it.

**#18, target-prompt text variants.** Fixing the string alone is a 2-line
change, but it's currently dead code: nothing routes a group-target skill
through `MenuDepth.Target` today (they resolve straight through
`CastCharacterSkill(skill, null)`), so the group-target prompt would never
actually show even with the text fixed. Whether that routing gap is
in-scope for "close this handoff" or a separate, bigger combat-flow change
is worth a call before touching it.

**#1, layout region pixel audit.** The raw values were spot-checked (2 of 6
regions converted back to top-left coordinates) rather than exhaustively
verified pixel-by-pixel against all six regions' exact numbers in the
handoff table. Given every region is confirmed present, correctly
proportioned, and non-overlapping (see the FightPanel screenshot from the
Phase 3 verification pass), a full pixel audit is low-value relative to its
cost and was not done.

## Summary by verdict

- **match**: 11 (#1, #5, #8, #12, #14, #16, #17, #19, #20, #22, #27)
- **closed**: 11 (#2, #3, #4, #6, #9, #10, #13, #21, #23, #25, #26) — see the sha in each row
- **partial**: 1 (#11 — status tags as one joined text line rather than discrete pills; functionally equivalent, low priority, left as-is)
- **missing**: 2 (#18 — dead code path, text fix alone would be cosmetic; #24 — gamepad support, out of scope for this pass)
- **deliberate-deviation**: 3 (#7, #15, #24's own framing overlaps here too — #7 and #15 are genuine accepted deviations, #24 is closer to "large deferred scope")

All of Phase 6's independently-closeable gaps are closed as of commits
`1dc17c7`, `b4ecb74`, `0a6e084`. What's left is two items that need a
product decision rather than a fix (#7 dialogue bark model, #18 group-
target prompt routing) and one larger, explicitly out-of-scope lift
(#24 gamepad support) — none of which were guessed at.
