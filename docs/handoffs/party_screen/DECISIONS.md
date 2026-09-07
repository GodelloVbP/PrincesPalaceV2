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
