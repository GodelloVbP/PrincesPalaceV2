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

### The second pass, against the prototype rather than the document

The first build was made from `DESIGN.md`, which is the handoff written out as
tables. The **prototype** — `Reward Track.dc.html`, in the same folder of the
Claude Design project — is the same design as running code, and reading it
against the built screen turned up nine differences the tables do not carry,
because a table can give a colour but not a gradient and a position but not an
alignment. All nine are now built.

| Was | Is | Why it mattered |
|---|---|---|
| Rail band a flat `#2E224499` block | A vertical wash peaking at `2E`, fading to nothing at both edges, hairlines inset 44 and fading at their ends | Three times too dark and with a hard edge each end. It read as a grey stripe laid across the panel — and it is what made the empty strip below it read as a hole, because a solid block turns everything that is not the block into a gap. |
| Here-node halo a flat 106px | `disc + 16`, so 42 on filler and 60 on a milestone | 106 is four times a filler disc. The first capture shows the player's own node as a white smear with its mark invisible: the one node the eye is meant to land on was the one whose reward could not be read. |
| Lit rail a flat tint | A ramp, 32% at the start of the run to 100% at the player's node, with a 15px bloom under it | A flat bar says how much is done. A ramp says which END of it is now, which on a rail whose lit half can be 8,000px long is the difference between a progress bar and a path. |
| Summary one 18px sentence | Five pieces: LEVEL, the figure at 34px, a rule, NEXT AT n, and the reward's name | The figure is the thing the row exists to say and it was set at the same size as the word in front of it. |
| Card four centred lines | A plate: gradient ground, gold rim, framed art well with corner ticks, a header row (kicker — rule — LVL — figure), the name, a divider, and a state dot with its words | Centred is not a smaller version of the design's card, it is a different one. |
| NEXT a 26x14 chevron at the top of the band | The word NEXT over a 14px hairline that fades toward the node | The chevron lived in the only gap a centred caption left, 105px above the rail, and the first capture shows it reading as dust on the band's edge. |
| Node captions centred in their box | Hung from a fixed bottom edge at 50, text bottom-aligned | A centred block moves its last line by half its own height depending on how long the reward's name happens to be. Hung, every caption on the rail ends the same distance from its node. Hanging them is also what opened the strip the NEXT mark now lives in. |
| Ribbon: dots raised off the line, numbers below, ticks only where a level is owed | Everything on the line, numbers above, and a tick for every one of the 87 filler levels | 99 levels drawn as 12 marks says nothing about how far apart they are. The waiting weight — twice as wide, twice as tall, in gold — now reads against a comb instead of being the only thing on the line. |
| Panel a flat rect | A radial wash at its middle and four corner brackets | Both are the difference between a ground and a fill, on the screen with more flat ground showing than any other. |

**Added on top of the design, after seeing it built:** a milestone now carries
two ambient cues — an aura that breathes behind the disc on a 5.2s loop, phase-
offset per landmark so the twelve do not swell in unison, and a plate ring
broken into eight arcs that turns once every 26 seconds. Section 6's milestone
treatment is size, a plate ring, a bigger caption and a bigger number, and all
four are static: twelve landmarks holding perfectly still on a rail that
shimmers, breathes and pulses everywhere else read as printed rather than as
important. Neither cue carries information — state is still said by colour and
by the pulse.

The turning ring **is** the plate ring rather than a second circle outside it.
A second circle was the first build, at 60 against the plate's 58, and two
hairlines two pixels apart are one hairline: the solid ring filled the turning
one's gaps exactly. Moving it outward is not available either — 66 is where the
level number starts. So the design's ring keeps its radius and its weight and
loses its continuity, which is the trade a rotating ring demands: a solid circle
looks identical at every angle.

Two smaller ones in the same pass: the **collect and CLOSE buttons** are
hairline boxes rather than the shared gold plate — two filled gold rectangles at
the top of a screen whose entire palette is gold-as-stroke — and the **seal
pip** is now a dark disc with the check drawn ON it in gold rather than knocked
OUT of it.

**The seal pip is where the first pass got the design wrong twice, in opposite
directions**, and it is worth writing down because both mistakes looked correct.
Section 2 gives the pip as 15px at `+(d/2 - 8)`. That offset is the pip's LEFT
EDGE — the prototype positions it by its top-left corner with no centring
transform, unlike the halo and the pulse ring beside it, which both carry
`translate(-50%,-50%)`. Read as a centre, a 15px pip lands its near edge half a
pixel past the middle of the mark it sits beside, which is exactly what the
first build measured and reported: 87 of 99 nodes with their one scannable
feature covered. The fix taken then was to shrink the pip to 9px on filler
nodes; the fix the design intended is to place its centre on the rim, where a
15px pip clears the mark by ten pixels. The 9px pip was also drawn as a knockout,
which at that size is a one-pixel crack — hence the smudge in the first capture.

This needed one new capability in the UI kit: `UiTextAlign`, because every label
in the project was centred in both axes and nothing could say otherwise. The
card's header row and the node captions are the two layouts that made the
absence load-bearing.

### Where the build still departs from the handoff

- **The face stays Chakra Petch**, and this is the one gap that is not a
  judgement call: section 6 sets Cormorant Garamond and Lora, the project has
  one font asset, and adding two means two typeface files this repository does
  not have. Every size and letterspacing from section 6 is used as written,
  which is what section 6 itself prescribes if the mono stays. Section 11 lists
  the face as open; it still is.
- **The card caption sets at 22px, not 32.** At 32 the longest reward the track
  can name runs about 700px against the card's 474, wraps to three lines and
  clips. 32 is right for milestone names and wrong for filler ones.
- **The card's art-slot note is not drawn.** The prototype's card carries "ART
  SLOT · 15 × 15 MARK" along its footer. That is instrumentation for the art
  commission — it names the slot's delivery size — and a player reading it would
  be reading a build note.
- **The plate's rim does not change with state.** The design runs it gold at 99
  when the reward is reachable and 52 when it is not; that is four rim edges to
  find and repaint for 18% on a one-pixel line, and the mat inside already
  carries the state across 86x86.
- **The scroll viewport is centred on the rail**, not at y 296. Section 1's own
  numbers disagree by 6px — a viewport at 296 of height 200 has its middle at
  396, and the rail is specified at 402. The rail landing on the panel's exact
  vertical centre is the invariant that was kept.
- **No panel hatch.** The prototype lays a 1px-per-3px repeating grid over the
  panel at 2% opacity. uGUI needs a tiled sprite for that; the radial wash and
  the corners carry the same job.
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

- **The 16 reward sprites** of section 8 — now down to the 15 SMALL ones. The
  card's 86px slot is filled: it draws the talent tree's painted medallions,
  mapped by reward kind in `RewardTrackLayout.CardArtFor`, which is twelve
  assignments made from a set drawn for another screen and should be overruled
  by anyone with an opinion. The rail's marks are still strokes and the reason
  is size rather than taste — a painted medallion at 15px is a gold dot, which
  is the same argument section 8 makes for choosing strokes. A node's disc is
  also tinted by state, and painted art tinted violet reads as a painting
  behind glass.
- **The face.** Section 6 sets Cormorant Garamond and Lora against the shipped
  condensed mono, and section 11 lists it as a decision still to make. It is
  also the only item on this list that needs an ASSET as well as an answer:
  `Assets/_Project/Fonts/` holds one font, and two more would be two typeface
  files this repository does not have plus the TMP assets built from them.
- **The relic chain**, 25 → 45 → 60 → 70, still reads as four unrelated
  milestones. The handoff's own "not resolved, deliberately".
- **Gamepad focus.** The prototype is pointer-and-keyboard; whether focus and
  hover are the same state needs deciding before stick navigation is built.
- **Season end.** Section 11 flags that there is no state for a closed track.
  Nothing has asked for one.
- **The strip at y 502-648.** Still empty, and it is now clear that the design
  leaves it empty too — the prototype has the same gap between its rail band
  and its ribbon, and its own band table skips it. What changed is why the gap
  reads as it does: the band above it was a flat block three times too dark, so
  everything that was not the block read as a hole. With the wash right, this is
  a quiet third of a panel rather than a dead one. Still worth a decision, but
  it is a design question and not a build gap.

~~**Milestone choices.** Levels 35 and 50 grant a choice with no UI to make
it.~~ — **not a gap.** Carried from sections 4 and 11, which were written
against an older reward table. There is no level-35 milestone in
`RewardTrack.Milestones`; 50 is `WiderOffer`, consumed by
`OfferRowLayout.cs:76`; and 70's `ChosenStartingRelics` has had its chooser
since `d01d49b` (`RelicDraftController.cs:152`).

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
- `Assets/_Project/Scripts/Editor/ProceduralSpriteBaker.cs` — `BakeTrackMarks` for
  the five marks and the seal, `BakeTrackGrounds` for the band wash, the fading
  hairline, the lit rail's ramp and bloom, the NEXT mark's drop and the card's
  ground
- `Assets/_Project/Scripts/Domain/UiKit/UiTextAlign.cs` — the one UI-kit
  capability this screen needed that did not exist
- `Assets/_Project/Scripts/Tests/EditMode/RewardTrackLayoutTests.cs` — what must stay true
- `Assets/_Project/Scripts/Tests/EditMode/RewardTrackStateTests.cs` — the four states
- `Assets/_Project/Scripts/Tests/PlayMode/RewardTrackClaimTests.cs` — collecting, through the panel
- `tools/screenshots/runtime/SystemMenu_reward_track.png` and `..._waiting.png`
  — current state in both halves, via
  `tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests`
