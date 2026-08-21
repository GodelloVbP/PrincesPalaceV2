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

## What the design decided

**Answered.** The design handoff (`Reward Track - Handoff.dc.html`, in the
Claude Design project) took all six of the questions below and returned a
resolution for each. Everything in this section is now BUILT; the *Layout* and
*States* tables above describe the screen as it was measured before that pass,
and are kept as the record of what the design was drawn against.

| Was open | Resolved as |
|---|---|
| The screen is airy | Two new bands: a **focus card** at y 118-268 and a full-width **ascent ribbon** at y 648-768. The rail keeps its own band, now with a soft `#2E2244` wash and hairline edges at 290 and 501. |
| Nothing marks the next reward | Marked twice — a bobbing caret above node `level+1`, and the focus card resting on that reward whenever nothing is hovered. |
| ~8 nodes visible of 99 | The ribbon shows all 99 at 14.5px pitch: milestones raised and numbered, waiting levels as tall gold ticks, the visible window drawn as a draggable box. |
| The tick is small and pale | Replaced by a **seal pip** — a dark disc with the check knocked *out* of it, so the gold disc beneath shows through as the mark. |
| Marks are procedural strokes | Every disc and the card become **art slots**. The stroke survives as the silhouette brief, and is what each slot draws until painted art lands. |
| Milestones read only by size | Plus a gold hairline plate ring at inset −7, a larger heading caption, and a raised numbered dot on the ribbon. |

**Collection became manual**, which is the largest consequence and the only one
that is a gameplay change rather than a presentation one. `RewardApplier` no
longer calls `ClaimTrackRewards`, so stat points, Favor, max health and
experience-find arrive when the player opens this screen and collects, not at
the end of a fight. That gap between `level` and `claimedTrackLevel` is what
the four-state model draws — see `RewardTrack.StateOf`. To revert it, put the
call back: the screen works either way and would simply have nothing to collect.

### Where the build departs from the handoff

Each is a deliberate call, with the reason recorded at the code site that makes
it rather than only here.

- **The face stays Chakra Petch.** Section 6 changes to Cormorant Garamond and
  Lora; section 11 lists the face as still open. Every size and letterspacing
  from section 6 is used as written, which is what section 6 itself prescribes
  if the mono stays.
- **The card caption sets at 22px, not 32.** Measured: at 32 the longest reward
  the track can name runs about 700px against the card's 484, wraps to three
  lines and clips. 32 is right for milestone names and wrong for filler ones.
- **The seal pip scales with its disc**, 15px on a milestone and 9 on filler.
  Section 2 gives one size for both; at 15 on a 26px disc it covered the very
  mark it sits beside, on 87 of 99 nodes. The ratio carries across, not the
  pixel count. The first runtime capture is what caught it.
- **The lit rail is `#F2DB9EBF`, not the measured `6B`.** That measurement was
  taken against the flat ground this screen used to have; the new band wash
  lifts what sits behind the line, and at 42% the lit half read pale lilac.
- **The scroll viewport is centred on the rail**, not at y 296. Section 1's own
  numbers disagree by 6px — a viewport at 296 of height 200 has its middle at
  396, and the rail is specified at 402. The rail landing on the panel's exact
  vertical centre is the invariant that was kept.
- **The NEXT caret is 26 x 14, not square.** Its height is pinned by the 14px
  gap above the caption; drawn square in that gap it read as a speck a hundred
  pixels from the node it points at.
- **Reduce-motion is not implemented.** Section 7 asks for the ambient cues to
  drop and the meaningful ones to stay. There is no accessibility setting in
  `GameSettings` to read, and inventing one for a single screen would put the
  switch where no other screen could find it. The split is written so it is a
  one-line guard once that setting exists: `Animate()` is every cue that would
  go, and nothing else is in it.
- **No parallax and no dust.** Section 7's last row wants a background layer at
  0.06x scroll and motes at 0.14x. This panel's ground is a single opaque solid
  — there is no background to move. It needs art before it needs code.
- **No sound on claim.** `Sound` is an enum of meanings whose every value is
  asserted to resolve to a file that exists, so a collect sound is a new value,
  a new case in `SoundLibrary.PathOf` and a clip nobody has recorded. The
  handoff specifies no audio.

### Still open

- **The 16 reward sprites** of section 8. Every slot is built and draws its
  ghost glyph; this is the one thing standing between the screen and the
  painterly read.
- **The relic chain**, 25 → 45 → 60 → 70, still reads as four unrelated
  milestones. The handoff's own "not resolved, deliberately".
- **Milestone choices.** Levels 35 and 50 grant a choice with no UI to make it.
- **Gamepad focus.** The prototype is pointer-and-keyboard; whether focus and
  hover are the same state needs deciding before stick navigation is built.
- **The dead strip at y 502-648.** It falls out of the handoff's own band table,
  and it is the one place the screen still reads as airy.

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
- `Assets/_Project/Scripts/Core/RewardTrackController.cs` — states and painting
- `Assets/_Project/Scripts/Core/RewardTrackController.Input.cs` — claiming, scrolling, hover
- `Assets/_Project/Scripts/Core/RewardTrackController.Motion.cs` — every cue in section 7
- `Assets/_Project/Scripts/Core/RailScroll.cs` — wheel and drag over the rail
- `Assets/_Project/Scripts/Domain/Progression/RewardTrack.cs` — `StateOf`, `IsWaiting`, `UnclaimedCount`
- `Assets/_Project/Scripts/Editor/ProceduralSpriteBaker.cs` — `BakeTrackMarks`
- `Assets/_Project/Scripts/Tests/EditMode/RewardTrackLayoutTests.cs` — what must stay true
- `Assets/_Project/Scripts/Tests/EditMode/RewardTrackStateTests.cs` — the four states
- `Assets/_Project/Scripts/Tests/PlayMode/RewardTrackClaimTests.cs` — collecting, through the panel
- `tools/screenshots/runtime/SystemMenu_reward_track.png` and `..._waiting.png`
  — current state in both halves, via
  `tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests`
