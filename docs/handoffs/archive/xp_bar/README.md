# Art brief: the experience bar

**Status: brief seeded from the current implementation, not yet a design.** The
sections below are what the engine does today, measured out of the code so the
design pass starts from facts rather than from a screenshot. Everything under
*What the design has to decide* is genuinely open.

## Overview

There are **two** experience bars in the game and they do not look like each
other. One is painted; one is two flat rectangles. That mismatch is the reason
for this brief.

This is the same observation `dossier_containers` opens with — *"the contents
are painted and the things holding them are not"* — arriving in a second place.

## The two bars

### 1. The Reckoning's bar — painted, per character, post-fight

`ReckoningScreen`, one per squad row. Built from three baked sprites:

| Key | Baked by | What it is |
|---|---|---|
| `proc:bar_track` | `ProceduralSpriteBaker.BakeBar` | the recessed channel |
| `proc:bar_fill` | same | the light that fills it |
| `proc:bar_bloom` | same | the glow the earned segment throws onto the panel |

It carries three states in one bar — where the character *was*
(`BarFillBefore01`), where they are now (`BarFill01`), and the segment just
earned between them, which is tinted separately (`BarBeforeTint`).

`BakeBar`'s own comment records a constraint worth keeping: everything about
that art is **uniform along x**, because the fill is stretched to a fraction and
anything that varies horizontally — a rounded cap, a highlight at one end —
distorts as the bar grows, and distorts *differently every frame while it
animates*. All the shaping is vertical.

### 2. The dossier's bar — unpainted

`CharacterDossierScreen`, in column A under the character's name. Two
`Ui.Solid` rectangles and two labels. No art at all.

Exact geometry, at the project's 1920×1080 reference, measured from column A's
centre (`DossierLayout`):

| Element | Node | Size | Position |
|---|---|---|---|
| "XP" label | `DossierXpLabel` | 30 × 16, 10px | x −142 (left edge + 15) |
| Track | `DossierXpTrack` | **204 × 3** | left edge at x −117 |
| Fill | `DossierXpFill` | 204 × 3, left-pivoted | left edge at x −117 |
| "N left" | `DossierXpRemaining` | 70 × 16, 11px | x +122 |

Colours: track `#C8B4DE24`, fill `#B9A2D6` (`CharacterDossierScreen.Accent`),
both labels `#D6C8E86B`.

The row sits at `DossierLayout.XpRowCentreY`, and directly under it is the
reward-track nav row, which now carries "LEVEL 6: +3% EXPERIENCE".

## For the engine

Three things the design must not break, each of which has already cost
something once:

1. **The fill is driven by WIDTH, never by anchors.** Anchors are relative to
   the parent, and the parent is column A rather than the 204px track — a bar
   sized by anchors crossed that fraction of the whole column and ran out past
   its own caption into the loadout stage. `DossierXpBarTests` pins it.
2. **The fill clamps to 0..1.** `CombatReward.BarFill01` and the dossier both
   do. A character can hold more experience than their current level requires
   (the level curve was retuned and is cheaper than the old one between roughly
   levels 2 and 44), and an unclamped bar would overfill.
3. **`XpTrackWidth` is derived**, not authored: `ContentAWidth − XpLabelWidth −
   XpLabelGap − XpRemainingWidth`. Widening either label narrows the track
   automatically. Do not hardcode 204.

## What the design has to decide

- **Does the dossier bar adopt the Reckoning's art, or get its own?** The baked
  channel exists and is already uniform-along-x, so reusing it is close to free.
  At 3px tall it may simply be too small a bar for that art to read, in which
  case the row's height is the real question.
- **Is 3px right at all?** It is the thinnest thing on the screen and reads as a
  hairline rather than as a gauge.
- **Does the dossier bar want the Reckoning's three-state treatment** — was /
  now / just-earned — or is that a post-fight idea that does not belong on a
  sheet the player opens between runs?
- **What does a full bar look like?** Nothing currently marks the moment before
  a level-up.

## Out of scope

- The reward track's own rail (`RewardTrackScreen`), which is a different
  readout of the same progression and has just been built. If the two should
  share a visual language that is worth saying, but it is not this brief.
- The level curve and what a level costs. That is balance, not art.
- The Reckoning's animation timing.

## Where the numbers live

- `Assets/_Project/Scripts/Domain/UiKit/DossierLayout.cs` — the XP row's geometry
- `Assets/_Project/Scripts/Domain/UiKit/Screens/CharacterDossierScreen.cs` — the dossier bar
- `Assets/_Project/Scripts/Domain/UiKit/Screens/ReckoningScreen.cs` — the painted bar
- `Assets/_Project/Scripts/Editor/ProceduralSpriteBaker.cs` — `BakeBar`
- `Assets/_Project/Scripts/Tests/PlayMode/DossierXpBarTests.cs` — what must stay true
