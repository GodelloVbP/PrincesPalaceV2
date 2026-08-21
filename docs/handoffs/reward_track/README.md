# Art brief: the reward track

**Status: built first, briefed after — which is backwards.** `HANDOFF_TEMPLATE`
says a handoff is required for "any new screen", and this screen was built in a
single session without one. What follows is the implementation measured out of
the code, so a design pass starts from what is actually on screen rather than
from a screenshot, plus an honest list of what is provisional.

Everything under *What the design has to decide* is open. Everything under
*For the engine* is not.

## Overview

A battle pass: one horizontal rail with a node per level from 2 to 100, the
reward's name above each node and the level number below, scrolled behind a
fixed window that opens centred on where the player is.

It replaces nothing. Before it, the only view of the track was a single line on
the dossier saying what the *next* level gives — you could not see level 40, or
what you had already collected, or the shape of what was ahead.

## Where it lives

Inside the system menu's **Character & Inventory** pane, over the dossier,
opened by the dossier's third nav row ("Reward Track", beside Skills and The
Pack) and closed by its own CLOSE button.

It is deliberately **not** a sixth tab: `SystemMenuScreenTests` pins the
three-tab and five-tab rows to authored pixel positions from a design pass
(`40, 590, 1140`), so a sixth tab would mean inventing a layout somebody had
specified. If design wants it promoted to a tab, that is a tab-bar re-measure
and should be said explicitly.

## Layout, at 1920×1080

Panel is `SystemMenuLayout.PanelWidth` × `ContentHeight` = **1600 × 804**, with
its own fully opaque ground `#120A18`.

| | |
|---|---|
| Rail | one hairline, 2px, at the vertical centre |
| Nodes | 99, one per level 2–100 |
| Pitch | **190px** |
| Content width | **19,000px** (99 × 190 + 95 padding each end) |
| Visible at once | **~8.4 nodes** in the 1600 window |
| Scroll range | 17,400px end to end |

Per node, measured from the node's own centre:

| Element | Size | Offset | Font |
|---|---|---|---|
| Disc (`proc:solid_circle`) | 26 filler / **40 milestone** | 0, 0 | — |
| Reward mark | 15 filler / **22 milestone** | 0, 0 | — |
| Claimed tick (`proc:track_tick`) | 14 | +11, −11 | — |
| Caption | 170 × 64 | 0, +66 | 11 filler / **13 milestone** |
| Level number | 170 × 22 | 0, −44 | 12 filler / **16 milestone** |

Summary line top-centre (`LEVEL 47 · NEXT AT 48: A STAT POINT`), CLOSE top-right.

**190px pitch is set by the longest caption**, not by the dots: "YOUR SECOND
LIFE RETURNS AT EVERY BOSS" wraps to four lines and clips below about 170. The
dots would sit happily at 60.

## States

| State | Disc | Mark | Caption / number | Tick |
|---|---|---|---|---|
| To come | `#2E2244` | `#F2DB9E` | `#D6C8E852` | hidden |
| Reached | `#F2DB9E` | `#2E2244` | `#EDE6FF` | shown if claimed |
| You are here | `#EDE6FF` | `#2E2244` | `#F2DB9E` | shown if claimed |

Rail: `#C8B4DE29` unlit, `#F2DB9E6B` lit to the player's own node.

The mark is tinted **against** its disc — dark on a lit node, lit on a dark one
— because a single colour vanishes on one half of the rail.

**Reached and claimed are different questions.** Reached is `level`; claimed is
`claimedTrackLevel`, what the track has actually paid. They are normally in
lockstep and diverge for a character levelled by a migration or a debug grant.
The tick is the honest answer to "have I had this".

## The five marks

Four grant marks and one ring, not one icon per reward kind — every node
already carries its reward in words, so a twelfth bespoke shape would be doing
the caption's job. What the rail needs is to be scannable *without* reading.

| Mark | Shape | Means |
|---|---|---|
| `proc:track_stat` | `^` chevron | a stat point |
| `proc:track_exp` | `^^` double chevron | +N% experience |
| `proc:track_health` | `+` cross | +N max health |
| `proc:track_favor` | `×` | Prince's Favor |
| `proc:ring_outline` | `○` | any milestone capability |

All baked by `ProceduralSpriteBaker.BakeTrackMarks` as **strokes**, and that is
load-bearing twice over:

- A glyph from the font would rasterise into the dynamic atlas, which is tracked
  in git (AUDIT #47) — any run that drew it would dirty a committed asset.
- Solid shapes do not read at this size. Favor took three attempts: a four-point
  star fades its arms to nothing and rendered as a dot; a filled diamond is
  crisp but a solid shape nine pixels across is *also* a dot. Only strokes read
  at 15px.

## For the engine

1. **`NodeX` is measured from the content's LEFT EDGE; `Place.At` measures from
   a parent's CENTRE.** `NodeOffsetX` is the conversion. Placing `NodeX` directly
   put every node half a content-width too far right and `UiAudit` refused the
   build with "TrackDot100 escapes its parent: 9,330px past the right".
2. **Every node is emitted at its real position.** Level 40 sits at the same x
   in every save, so nothing is placed at runtime — only the content rect slides,
   which is one `anchoredPosition` against a hundred.
3. **The lit rail is driven by WIDTH, left-pivoted, never by anchors.** The
   parent is a 19,000px rect; the dossier's XP bar records what that mistake
   looks like when it ships.
4. **Milestone is asked of `RewardTrack`, never guessed from the reward kind.**
   Level 10 is Prince's Favor +5 and a filler node is +2 Prince's Favor — same
   kind, different status. Guessing drew level 10 as an ordinary node.
5. The panel ground must stay **fully opaque**. At `#120A18FA` the dossier's
   portrait and ability scores read through a hundred captions.

## What the design has to decide

- **The screen is airy.** The rail sits on a band of empty space above and
  below, roughly the top and bottom thirds. Icons and ticks were the first
  answer to that and it is still sparse. This is the main open question.
- **The marks are procedural strokes in a painterly game.** Everything else —
  portraits, items, relics, the talent sky — is painted. These are geometry.
  Whether that reads as deliberate UI vocabulary or as placeholder is a call
  nobody has made.
- **Milestones are distinguished only by size and a ring.** Twelve of the
  hundred nodes are the ones that matter, and the relic line (25 → 45 → 60 → 70)
  is the track's one real chain. Neither is drawn as a chain.
- **Nothing on the rail marks the NEXT reward.** Only the summary line names it.
- **The tick is small and pale** at 14px in a corner.
- **~8 nodes visible of 99.** A player at level 47 cannot see level 50's
  milestone without scrolling. Tiers, a minimap, or milestone-only jump points
  are all unexplored.

## Out of scope

- The dossier's XP bar — its own brief, `docs/handoffs/xp_bar/`.
- What each level gives and what a level costs. That is balance; the table is
  `RewardTrack.Milestones` and `FillerMix`, and the reasoning is in
  `docs/HANDOVER_PROGRESSION_TRACK.md`.
- The talent tree, which is the other meta-progression screen and has its own
  established look.

## Where the numbers live

- `Assets/_Project/Scripts/Domain/UiKit/RewardTrackLayout.cs` — every coordinate
- `Assets/_Project/Scripts/Domain/UiKit/Screens/RewardTrackScreen.cs` — the tree and the tokens
- `Assets/_Project/Scripts/Core/RewardTrackController.cs` — states and scrolling
- `Assets/_Project/Scripts/Editor/ProceduralSpriteBaker.cs` — `BakeTrackMarks`
- `Assets/_Project/Scripts/Tests/EditMode/RewardTrackLayoutTests.cs` — what must stay true
- `tools/screenshots/runtime/SystemMenu_reward_track.png` — current state, via
  `tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests`
