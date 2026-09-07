# Party screen: decisions that override the handoff

Recorded verbatim-in-substance from the P2 briefing, because the handoff
(`README.md`, `party-screen-reference/`) predates every one of these and
disagrees with several of them outright. Where the two conflict, this file
wins.

## Seats are mechanically different

The front-rank rule landed 2026-09-07: enemy melee hits the party's front
living rank, skills can be rank-limited, and squad index 0 is the front. The
handoff's own escape clause ("If the real game DOES want row-based combat
roles, rename the labels and re-add role-specific copy") applies, so:

- Labels are **FRONT / MIDDLE / REAR** (seat index 0/1/2), not "Position 1/2/3".
- Drawn **left to right as REAR, MIDDLE, FRONT**, so the front seat sits
  nearest the "Facing the enemy →" ribbon on the right — matching the fight
  stage's own party-left/enemy-right orientation. Seat index 0 (front) is
  therefore the RIGHTMOST column, not the first one drawn.
- The subtitle replaces "Order only — every position fights the same." with:
  **"The front rank takes the enemies' blows. Some skills reach only from the
  rank they name."**

## Three modes, not two

Camp (hub scene — swap freely), Run (map scene — reposition only), and
ViewOnly (fight scene — the live encounter owns its own order, so the pane
changes nothing). The handoff only names `camp`/`run`. Status pill copy:

- `AT CAMP — Swap freely.`
- `IN A RUN — Reposition only.`
- `IN A FIGHT — Formation fixed.`

## No page title inside the pane

The system menu tab already says PARTY, and the lintel sits above the panel.
The same rule `RunStatRows` applied to its own header applies here: no
redundant "Party" heading inside the content.

## The prototype-controls strip is not built

The handoff's own README says as much (`"this strip is a demo-only affordance
and should not ship"`) — recorded here so it stays visible from the design
side, not only in a commit message.

## Locked seats are a rendered state with no system behind them yet

A locked seat is a predicate the tree can show (scrim + caption), but nothing
in production locks a seat today — no run-modifier system exists. Wire it to
whichever system authors run modifiers, if one is ever built.

## Closed is a fourth seat state

A CLOSED seat (seat index ≥ `EffectiveMaxSquadSize`, which ranges 1..3) gets
the same scrim treatment as a locked seat, captioned **"No seat yet"** instead
of "Locked this run". The handoff has no such state — it always shows 3 filled
positions.

## Seat count is fixed at 3

The fight stage has exactly 3 party slots. Unlike Roster, Formation has no
build-time guard, because there is nothing for a designer to outgrow.

## Roster card count is fixed at build time

`PartyScreen.Build(PartyInputs)` takes `RosterCardCount`, the way
`MainMenuScreen.Build(new MainMenuInputs(SaveSystem.SlotCount))` reads its own
count from outside Domain. The build-time guard throws if that many cards
cannot fit one row. `characters.json` authors 3 characters today (`sheep`,
`placeholder_brawler`, `owl`); `SystemMenuScreen.Build`'s own default keeps
that number rather than the handoff's five-character demo roster (Turtle, Fly,
Dog are explicitly not real content per the handoff's own Assets section).

## Art loads from Resources/Characters/<id>, never from the handoff's sprites

The handoff's `party-screen-reference/sprites/` are copies of
`Resources/Characters/sheep/idle.png` and `.../owl/idle.png`, made because the
designer had no access to the real UI kit. They were **not** copied into
`docs/handoffs/party_screen/` — the handoff README says as much, and copying
them would have invited exactly the mistake it warns against. The tree
declares an Image node per art slot with no baked sprite; the runtime (a later
package) loads `Resources/Characters/<id>/idle.png` directly, the same way
`FightScreen`/`StanceAnimationLibrary` already do. A monogram standee (a plate
plus one runtime letter) is the declared fallback, shown by default since no
build has any art loaded yet.

## Drag-and-drop is a later package

The tree needs nothing extra for it — every state drag-and-drop would toggle
(a ring, a badge, a scrim) is already declared for the click-to-select-then-
place path the handoff also documents as the primary interaction.

## What this package (P2) does not do

No controller, no domain model, no `ScreenRegistry` wiring. Those are later
packages' jobs. This package only adds the declared tree
(`PartyScreen.cs`/`PartyLayout.cs`), the `SystemMenuTab.Party` entry, its
strings, and the tests that pin the tree's geometry and state coverage.

## P3 (integration): decisions made while wiring the controller

Recorded because the brief left them open, not because any of them override
something already decided above.

- **Role display text.** Nothing in the codebase turns `CharacterRole` into
  words — the fight HUD's own class row was cut before this package
  (`PartyScreen`'s `RoleText`/`PartyClassText` comment). `PartyController.
  RoleDisplayName` is a small local switch (`CrowdControl` → "Crowd Control",
  the rest unchanged) rather than `role.ToString()`. If a real role-display
  convention shows up elsewhere later, this should move to wherever that
  lives instead of staying a Party-local switch.
- **RESOLVED (P4).** The toast now fades: a `CanvasGroup` (added via
  `[RequireComponent]` on the new `PartyToast`, attached at wire time in
  `ScreenRegistry.WireParty`) holds full alpha for 2.0s then fades to 0 over
  0.4s before deactivating, matching the design's "~2.4s" copy. `PartyToast`
  is a small local component rather than `BeaconPulse`/`StageDeathFade` --
  see its own header for why neither reuse-first candidate fit (an unbounded
  loop vs. a fight-beat-scaled, three-image fade tied to a combat slot).
  ~~The toast is a hard show/hide, not a fade.~~ The design's own copy says
  "fades after ~2.4s", but the toast node carries no `CanvasGroup` and
  nothing in `CODE_STANDARDS.md` SS2's reuse-first registry is a drop-in
  "fade this out" primitive that doesn't already assume a battle-stage
  component (`BeaconPulse`, `StageDeathFade`). Scoped down to show/hide on a
  timer; a real fade is a small follow-up if the design still wants one.
- **RESOLVED (P4).** `PartyController` now reads the same manifest the fight
  stage does: `AlignArtSlots` still pins each slot's own canvas-bottom to
  the slot floor once, at wire time, but every repaint (`GroundArt`) shifts
  that baseline down by the occupant's `StanceManifest.GroundLineFor` entry,
  scaled from the sprite's own source pixels to however large
  `preserveAspect` actually draws it inside the slot's fixed box (the slot
  never shows a sprite at native size, unlike the fight stage's own
  anchor-stretched figure). A missing manifest entry still falls back to
  plain canvas-bottom (`GroundLineFor` already returns 0 on a miss). Two
  actors with different headroom under their feet now share one foot line
  here the same way they do on the fight stage.
  ~~Seat/card art is bottom-aligned to the SLOT's floor, not to each actor's
  FEET.~~ `FightController.StageVisuals.cs` grounds a stage figure against a
  per-actor manifest offset (`StanceManifestLoader.Manifest.GroundLineFor`)
  because delivered art does not put its own feet on its own canvas edge —
  the golem's is 52px off, Shawn's 43px per `Resources/StanceManifest.json`.
  `PartyController.AlignArtSlots` used to not read that manifest; it pinned
  each sprite's own canvas bottom to the slot's floor once, at wire time,
  which was a real gap: with more than sheep/owl in play, two actors whose
  canvases carry different amounts of headroom under their feet would NOT
  have shown their feet on the same line here, even though they do on the
  fight stage.
- **`placeholder_brawler` resolves real art today.** `characters.json` points
  its `battleSpritePath` at `Characters/sheep` (the same reuse-Shawn's-face
  move already made for `portraitPath`), so with today's content every
  roster card and every seat shows a sprite — the monogram fallback path is
  real code (`PartyRosterEntry.HasArt`, `PartyController.ArtFor`/
  `MonogramFor`) but currently unreachable through the game itself. Flagged
  rather than special-cased: the moment any future character ships with no
  `battleSpritePath`, the fallback is already live.
- **Glow colours.** `PartyScreen`'s own `GlowNeutral` is `private`; the
  occupied (green) and selected/valid-destination (gold) tokens the design
  calls for aren't declared anywhere accessible to a controller, so
  `PartyController` declares its own three hex constants read off the
  handoff's own "Ground-glow accents" token table rather than adding public
  surface to `PartyScreen` for two colours.
- **No genuinely benched roster card exists to test today.** Seats and the
  roster are both sized at 3, and `characters.json` authors exactly 3
  characters, so every character is always seated in both Camp and Run —
  `SystemMenuPartyTests` says this plainly rather than skipping the
  "benched card is not selectable" case quietly; that state is covered at
  the model level by `PartyFormationTests` instead, against a formation
  built directly rather than through a save.
