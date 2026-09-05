# Handover — 2026-08-15

Working tree clean at `2f5e231` apart from one pre-existing modification to
`Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset` and a scatter of untracked
`*.log` files at the root, both of which predate this session.

Full gate green with `-BuildScenes`: **1420 EditMode, 273 PlayMode** (10 skipped
— the 9 long-standing `Assert.Ignore` content guards, plus `MapCaptureTests`,
which gates itself on a graphics device like every other pixel test).

This file replaces the 2026-08-12 handover, which stopped fifteen commits ago.
Everything it listed as outstanding is either closed below or restated under
**Still open**. Its reasoning survives in the git log and in `AUDIT.md`; nothing
was dropped silently.

---

## Read this before your first edit

**There are two Prince's Palace checkouts and only `-v2` is live.** `CLAUDE.md`
opens with the check and the tells. This is not a formality: on 2026-08-12 a
full session — diagnosis, layout fix, a new test class, a new tool, four commits
— went into v1 believing it was this project, and only the PNGs were salvageable.
Nothing inside v1 says it is stale.

The other two rules that matter: scenes and content are **generated, never
hand-edited**, and **never `git add -A`** — this tree is sometimes worked by two
sessions at once, and a sweeping add has already eaten another session's work.

---

## Still open, roughly in the order I would take them

### 1. The descent map's remaining polish

The map was rebuilt across `0d0e7ca`, `5a64a2c` and `2f5e231` and is now the
screen it was always meant to be. Three things were deliberately deferred rather
than forgotten:

- **The per-tile ground glow and the open-room beacon pulse.** v1 draws both
  under every tile — a radial glow tinted by room type, and a pulse on rooms you
  can actually walk to. Skipped because each is a *third and fourth*
  position-synced sibling pool (a child would paint on top of its own parent, so
  they cannot be children of the tile), which is a lot of wiring for polish. The
  state tint and the you-are-here marker carry the cue meanwhile. `BeaconPulse`
  already exists in Core, unused by the map.
- **Hover highlighting.** Never ported from v1.
- **The "you are here" marker is a plain white square.** It predates all of this
  and looks it.

### 2. Event, Shop and ItemSpawn rooms have nothing behind them

They generate, render, can be entered and clear — and that is all. `RoomType`'s
own header says so. v1 has `StoreController` and `DialogueController`; v2 has
neither, and `forest_event_1.png` / `forest_event_mysterious_woman.png` sit
unreferenced in `Art/Backgrounds`.

`MapController.Arrive()` is the single seam they wire into. It is what the click
used to do before the walk existed, so adding a screen means one case there and
no contact with the movement code. `ItemSpawn` additionally has **no generation
weight**, so nothing rolls it at all.

### 3. The talent tree: a backdrop, and 252 nodes on the old payload

Raised by the author 2026-08-15 and worth writing down, because two of the three
things it looked like are already done and the third is bigger than it sounds.

- **`TalentScreen.BackgroundKey` is still `Divine_principality_nebula.png`** —
  the same file `HubScreen` uses. The talent tree is wearing the hub's wallpaper.
  Unlike the map, there is no candidate already sitting in `Art/Backgrounds`;
  this one needs generating.
- **The talent CONTENT is not a migration problem.** `talents.json` and
  `skills.json` are byte-identical between the two trees (md5-checked), 294
  talents, fully wired through `TalentSkeleton` / `ConstellationLayout` /
  `TalentEffectType` with four test classes on it.
- **What is actually unfinished is the rework.** Dog, owl, fly and turtle are
  ~48 flat `statBonus` nodes each — 252 of the 294 — which is the exact "no build
  to make, only a stat total to accumulate" problem
  `docs/handoffs/archive/shawn_talent_rework/README.md` was written to kill. Only Shawn's
  42 use the `effects[]` vocabulary. And **Shawn's third path (the mage) does not
  exist**; `GAP_AUDIT_LAMB.md` records that its engine rule
  (`WoolPerStatusedEnemy`) is implemented and unit-tested, so the root is one
  JSON entry once its six strands are designed.

### 4. `AUDIT.md` findings still open

**#44** the Reckoning's gain label sits on the bar it annotates (wants a design
call, not a patch) · **#45** the detail column covers the front enemy's feet
while a submenu is open · **#37** the retreat-shaped economy · **#38** the
unenforced wipe forfeit · **#41** `CurrencyType.Embers` with no live reader. The
last three are recorded and deliberately unfixed.

### 5. Loose ends

- **Unused painted art**, keyed and committed but referenced by nothing: the
  painted `rarity_burst` (beaten by the baked one in a side-by-side), `exp_track`,
  `exp_fill`. The author was asked whether to delete them and has not said.
- **Mana potion art never arrived** — only `health_potion` was generated, so the
  satchel has no mana consumable.
- **`RelicModifierType` has ten values and no content uses any of them.**
- **Tier 0 items exist** — `RarityTable.FloorTier(0)` returns 0 and the Reckoning
  showed "TIER 0" at depth 0. No item in `items.json` has tier 0, so this is the
  roll floor rather than content. Never chased down.
- **`avoidsFrontSlot` is authored, resolved, and read by nobody.**

> The "zero of 701 items require anything" entry that used to live here is
> **closed** — `3663f81` gates 605 of 701 on the scores their own material
> grants, so the requirement cascade fires in play now.

---

## What landed since the last handover

### Today, 2026-08-15 — the descent map (2 commits)

`5a64a2c` **the wood, and a window onto it.** Three things that looked like three
jobs and were one: the seven painted room icons had been sitting in
`Art/Backgrounds/Processed` referenced by nothing, the backdrop was being
stretched flat, and nine columns were crushed into a fixed 1500px track. The last
is why the first two could not land alone — `forest_map_background.png` is a
canopy with clearings *punched through it* at measured positions, so a room only
stands in a clearing if the camera puts it there. Now: masked viewport over a
~8800-wide scrolling content rect, background tiled every two columns, current
room pinned to the left clearing at x 520 and the column being chosen into on the
right at 1383.

Two things in there are worth carrying forward:

- **Rows no longer centre**, which reverses this project's own previous rule and
  the PlayMode test that pinned it. "Centring is what makes a fork read as a
  fork" was correct against a stretched backdrop with no clearings to miss.
  Against a tiled one, `MaxColumnWidth` is exactly 3 and there are exactly 3
  clearing rows, so slot **is** the clearing. A short column leaves its unused
  clearing empty, middle one included.
- **v1's fog covered its own last column.** It measured the fade from the content
  edge while columns measure from the first clearing, so a 520-wide opaque panel
  started at `span+50` against a boss at `span+520`. Re-derived here off the last
  column's right edge, and asserted for every leg length rather than the one that
  happens to generate. This is the third time v1's constants have not survived
  contact; assume they will not.

`2f5e231` **the party walks it.** A click starts a walk along the trail already
drawn between the two rooms, and the room's content fires on arrival. The figure
resolves through the squad leader → `CharacterDefinition.battleSpritePath` →
`StanceAnimationLibrary`, which is the fight stage's own actor pipeline, so a
second character is a content entry rather than a code change.

- **There is no walk cycle in this project's art for anybody.** Confirmed
  acceptable by the author for now. The idle travels the bezier with a
  deliberately small two-bounce bob — big enough to read as a gait on a
  walk-cycle sprite reads as a hop on a static one.
- **Where he stands was measured, and it answered a question the brief left
  open.** The ask was "next to the room icon"; `tools/measure_clearing.py` (new)
  scans the painted clearings at 155 x 283 content units against a 100 x 130
  tile, so there is no "beside" that also fits inside the clearing. He stands at
  its right edge and overhangs the canopy, which is consistent — the trails are
  drawn over canopy too. That same scan independently confirms
  `ClearingColumnX`/`ClearingRowY` to within 10 units, the first check those
  numbers have ever had.
- **The camera holds still while he walks**, which is the opposite of the first
  attempt. The scroll is pinned to a room offset, so following the figure pins
  the *figure* to a fixed screen x and slides the entire wood past him:
  everything moves except the thing that is moving. It pans one column
  afterwards, smoothstepped.
- The trail's jitter seed is **looked up rather than counted twice**. It was a
  per-link counter the walk would have had to reproduce exactly — including that
  a node with no position skips its links *without* advancing the count. Two
  loops agreeing by coincidence is not agreement.

### 2026-08-13 — gear, enemies, the fight stage, the map's trails (11 commits)

Grouped by what they were actually about:

**The gear economy grew two orders of magnitude, and three things had to follow
it.** `b77e952` rebalanced the five ability derivations, which were linear with
constants sized for a set granting 11 points against one now granting 187 —
scaling the constants was the obvious move and is wrong, because anything big
enough to matter at +187 is catastrophic at −3, which is where Shawn's Strength
sits. `7777bde` found that **`DifficultyCurve` had never been called** — the only
live caller in the project was `VictoryRewards`, so every fight at every depth
used authored `baseStats` verbatim and the corridor of trivial fights the file
exists to prevent is what shipped. Wired, and retuned onto **two rates** (enemy
health tracks player damage at ×1.81/tier, enemy attack tracks player health at
×1.50/tier), both measured off `AbilityDerivation` rather than chosen, with fight
length held constant as the pinned property. `3663f81` gave 605 of 701 items a
requirement gated on the scores their own material grants.

**The fight stage.** `2aced95` turned the depth line outward: it used to recede
*inward* toward a shared vanishing point, so back ranks stood closer to the enemy
than front ranks — and the real fault was occlusion, with the middle enemy 45%
visible behind a golem. Four formations were *rendered* before one was picked,
and the author's-and-my favourite measured worst of the four at 28%. `e8e0bef`
and `a0a1ba7` are one argument in two halves: stance timing now derives from
`impactFrame` so a six-frame attack runs 108/90/35/38/95/114ms instead of a flat
80 — then the snap frames turned out to be *too* fast to read at 60Hz, so a 55ms
floor took time back from the long frames. The two are in direct tension and
`TheCurveActuallyDoesSomething` now says so in its bound.

**The character overlay.** `5e2b563` ported v1's item comparison. The valuable
half is the cascade — requirements are met from everything else worn, so a swap
can read as +4 CON while quietly switching off the boots — and it works by
simulating the equip through the real resolver rather than subtracting two stat
blocks. Split differently from v1 so the formatting half is Domain-testable; 13
EditMode tests exist that could not have existed in v1.

**The map's trails**, `0d0e7ca` — see above; the scrolling half of that commit's
own "not ported" list is what today closed.

`bec9e76` retired `FindFirstObjectByType`, and notably **two of its three call
sites wanted the root Canvas**, where the obvious swap to `FindAnyObjectByType`
would have been a latent bug rather than a fix. `d9c079b`, `e9fce5d`, `fababaf`,
`8759358`, `94bca54` are art polish: the Continue arrow's heat, the ember's fade,
the exp bar, and armour onto one formula with eight more styles.

---

## Traps, still all true

- **Compositing beats reasoning for anything visual, every time it has been
  tested.** The armour-stand anchors were eyeballed wrong three times and fixed
  in one pass by measuring. Four stage formations were rendered because the one
  that sounded best measured worst. Today the walker's standing offset came from
  a pixel scan rather than from the brief, and the camera behaviour was only
  obviously wrong once it was on screen. There is a `tools/measure_*.py` for
  each of these; add to them rather than eyeballing.
- **The audit cannot see paint.** It knows a node's box, not that the box has a
  gold border drawn on it.
- **`UiAudit` measures DECLARED rects**, so a mask is not a licence to overflow
  and a runtime resize is invisible to it. The map declares every tile at the
  widest a tile can be for exactly this reason.
- **My patch scripts printed success unconditionally**, once, anchored on a line
  removed two commits earlier. Scratchpad scripts `sys.exit` on a missed anchor
  now. Keep that.
- **A button child named `<button>Label` collides** with the emitter's generated
  caption. Name them `Caption` (or, on the map, `Name`).
- **Check candidate class names against the tree before writing one.**
  `FrameHoldCurve` was named on the third attempt: `FrameTiming` collides with
  `UnityEngine.FrameTiming`, and a grep for `class StanceTiming` missed a struct
  four lines away.
- **`run_tests_parallel.ps1` refuses to run while a test class matches no area
  in `tools/test_areas.ps1`.** That is the guard working, not a fault — add the
  pattern. It has fired three times in three sessions.
- **`tools/test.ps1` does not rebuild scenes.** A change to a screen tree or its
  wiring will fail PlayMode with a `NullReferenceException` on a serialized field
  that is null only in the stale test copy. Run
  `run_tests_parallel.ps1 -BuildScenes` before believing that failure.
- **Art keeps landing in v1.** Check there before believing a file is missing.

---

## Verification, in brief

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Changed
```

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1 -BuildScenes
```

A full scene rebuild reassigns every `fileID`, so a five-scene diff in both
directions is normal and not a sign something broke.

Screens render headless for visual QA. `-Panel` renders a scene **at rest**,
which for anything pooled or runtime-populated is a black rectangle — the map's
whole content is runtime, so it has a PlayMode capture instead:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/screenshot.ps1 -Runtime -RuntimeFilter MapCaptureTests
```

`-RuntimeFilter` is new. The per-screen capture classes already existed and
nothing could reach them: the filter was one hardcoded class name, so a capture
test written for one screen could only be run by hand-assembling the Unity
command line.
