# Authoring event rooms

An event room is one entry in `Assets/_Project/ContentData/events.json` plus,
optionally, one picture per page. No code. Field-by-field defaults are in
`docs/CONTENT_SCHEMA.md` (generated); this page is the walkthrough.

## The shape

```
event    { id, floors[], requires[], pages[], mayReturn, speakers[], fights[] }
page     { id, artPath, title, body, choices[] }       first page opens the event
choice   { text, requires[], hiddenUntilMet, effects[], outcomes[] }   1-4 per page
outcome  { requires[], effects[], result, goTo }       first match wins
```

- **`goTo`** is another page's `id` in the same event (the same page is fine:
  that is how "do it again" loops) or the literal `Leave`.
- **`result`** replaces the body after the choice, with an effects line under
  it ("-5 gold · +50 XP", counters are never shown). A `Leave` outcome with a
  result shows it and waits on a single Leave button; a `Leave` with no result
  and no visible effect closes at once.
- **Order inside a choice:** the choice's own `effects` apply first, then the
  outcomes are tested against the updated state, then the winning outcome's
  effects apply. One save at the end.

## The demo, annotated

`demo_wishing_well` is a placeholder that exercises every feature. Replace it.

| Choice | What it shows |
|---|---|
| Leave | the always-open exit every page needs |
| Toss a coin (5 gold) | a gold spend (its `gold >= 5` gate is implied), `counter +1`, and two outcomes: `counter min 10 max 10` goes to page `wish_granted`, the unconditional last one loops back to `well` |
| Call on Shawn's luck | `inParty sheep`: open while Shawn is in the squad, greyed with "Requires Shawn" otherwise |
| Recite a well-worn prayer | `memberLevel min 15`: greyed with "Requires a level 15 party member" |
| Whisper one more charm (page 2) | `ability charisma min 20` with `hiddenUntilMet`: takes no row at all until it passes |

## The Petting Zoo

`petting_zoo` is the first real event (`docs/PLAN_PETTING_ZOO.md`, page graph
there). Things it does that a new event may want to copy:

- **One choice per visit** (owner, 2026-09-25). Every pick plays its scene and
  ends the event. The step pages' single row, "Say goodbye", is a silent Leave,
  so it closes at once. The petter rows leave with a short result. The one
  edge back to `zoo` is "Leave the sheep be", which picks nothing. A
  first-available bot could loop zoo <-> petter only with nobody standing,
  and `PettingZooEventTests` walks every squad to prove it.
- **A counter arc in one choice.** "Pet the sheep" has no choice-level effects.
  Each outcome gates on the pre-increment `zoo_sheep` and bumps it itself; the
  unconditional last outcome (`petter`) bumps nothing.
- **All text on the dialogue skeleton.** Every page plays `lines` and has an
  empty `body`. Outcomes into a scene page leave `result` empty, so the scene
  opens on its own first line. Odette speaks only on `step1_pair`, whose
  outcome requires `inParty owl`. Shawn speaks only on the step-1 pages, whose
  outcomes require `inParty sheep`. The fallback page (`petter`) is narration.
- **Results on a staged event take the 200 cap.** The peacock, fawns, cold one
  and petter results all play on the stage, because `zoo` and `petter` have lines.
- Pinned by `PettingZooEventTests` (content, fast host) and `PettingZooRunTests`
  (through the run, needs built content).

## Requirements

One flat row, `{ kind, character, alive, ability, min, max, counter, reason }`. A list is AND.
The same rows gate an event (`event.requires`), a choice and an outcome.

| kind | fields | passes when |
|---|---|---|
| `inParty` | `character`, `alive?` | that character is in the active squad; with `alive: true`, also standing (run HP above 0). `alive` on any other kind is refused |
| `memberLevel` | `min`, `character?` | any squad member (or the named one) is at least level `min` |
| `ability` | `ability`, `min`, `character?` | any squad member's **effective** score (gear and talents included) is at least `min` |
| `counter` | `counter`, `min?`, `max?` | the counter's value is inside `[min, max]` |
| `gold` | `min` | run gold is at least `min` |

A choice that fails shows greyed with the first failing reason and cannot be
picked; `hiddenUntilMet: true` hides it instead. Never author a gold gate for
a gold spend: the spend adds its own.

### Lock reasons

Every row has a generated caption, and none of them shows an internal id:

| kind | generated caption |
|---|---|
| `inParty` | `Requires Shawn` (the character's display name); with `alive`, `Requires Shawn standing` |
| `memberLevel` | `Requires a level 15 party member`, or `Requires Shawn at level 15` |
| `ability` | `Requires 20 CHA`, or `Requires Shawn with 20 CHA` |
| `counter` | `Not yet` while below `min`, `No longer` once past `max` |
| `gold` | `Requires 5 gold` |

A counter cannot say what it counts, so give any counter gate on a choice its
own `reason` (`"reason": "The well has not heard you enough"`). `reason` works
on every kind and replaces the generated caption outright. Only choice rows
show a caption; `reason` on an event-level or outcome row is ignored.

## Effects

`{ kind, amount, item, counter, character, relic }`, applied in order.

| kind | amount | effects line |
|---|---|---|
| `gold` | positive gains (counts as earned), negative spends (does not) | `+25 gold` / `-5 gold` |
| `healPercent` | 1-100 of each member's max HP. The party heal stands a downed member back up | `Party healed 30%` |
| `healPercent` + `character` | 1-100 of that one member's max HP. **Never revives**: a member at 0 stays at 0, and one not in the squad is untouched (no line either way). `character` on any other kind is refused | `Shawn healed 30%`, or `Shawn fully healed` at 100 |
| `damagePercent` | 1-100; floors at 1 HP, never kills | `Party hurt 10%` |
| `exp` | split across the party as after a fight | `+50 XP` |
| `exp` + `character` | all of it to that one member, not split (half if they are down, as after a fight; none if they are not in the squad). An unknown id is refused | `+50 XP` |
| `item` | how many of `item` go to the stockpile | `+2 Health Potion` |
| `counter` | added to `counter` | none |
| `relic` | ignored. Adds `relic` to the run's relics; already held does nothing (no line). The build refuses an id not in `relics.json` | `Relic: Kinship` |
| `princesFavor` | > 0. Added to the squad's Prince's favor for the rest of the **run**, after the squad's best member (item offers and shop stock rolled from now on; a shelf already rolled is not rerolled) | `+10 Prince's favor` |
| `fillSpecialPool` | exactly 1. For the rest of this **leg**, each character's special pool (signature, else primary) is full when their turn opens | `Special pools full each turn this leg` |
| `fight` + `fight` | none (refused). Starts the event's own fight by id; see **Fights** | none |
| `finish` | none (refused). Marks a `mayReturn` event seen; see **Returning events** | none |

`princesFavor` and `fillSpecialPool` are run buffs (`RunSnapshot.eventBuffs`).
A run buff ends with the run; a leg buff also ends when the party takes the
exit to the next leg. Both survive a quit and reload.
The fight reads `fillSpecialPool` at the end of each turn opening (`FightSession.OpenTurnFor`);
an extra action does not refill.

## Counters

A counter is a named number on the **profile save**. It survives the end of a
run and is shared by every event, so event A can read what event B counts.

- `min 10 max 10` fires **once**, on the pick that makes it 10.
- `min 10` alone fires on every pick from the tenth on.
- The build refuses a `counter` requirement on a counter no effect increments
  (typo guard). Renaming a counter id loses what it was tracking.

## Which event appears

On arriving at an Event node on floor F, one event is picked at random from
those whose `floors` contains F (empty means every floor), that have not been
seen this run, and whose event-level `requires` pass. An event shows at most
once per run, unless it is `mayReturn` (next section). An empty pool falls
back to the old "nothing here" line. Never rename an event id once a save
exists.

## Returning events

`"mayReturn": true` keeps an event in the pool after it opens. Walk away and
it can come back at the next Event node, any number of times, with no cap and
no spacing rule. The event is marked seen only when a `finish` effect applies,
on a choice, an outcome or a fight result. After that it never rolls again
this run.

- An event without `mayReturn` is marked seen the moment it opens, as before.
  `finish` there would do nothing, so the build refuses it.
- The ways out that should not end the event ("Walk away", "Walk on") simply
  carry no `finish`.
- The run honours both: opening a `mayReturn` event leaves `eventsSeen`
  alone, and `finish` adds the event to it wherever it applies.

## Fights

An event can start a fight and then continue from its result. Each fight is a
row in the event's `fights`, started by an outcome whose effects include
`{ "kind": "fight", "fight": "<id>" }`.

```
fight  { id, enemies[], elite, party[], surviveRounds, roundLabel, onLoss, pays,
         backdrop, roundSfx, ambience, roundOverlay { path, fromScale, toScale },
         onDefeated, onSurvived, onFell }
```

- **Starting one:** the `fight` effect goes in an **outcome's** effects, one
  per outcome, and that outcome's `goTo` stays **empty**. The fight's result
  says where the event goes next. The build refuses a `fight` effect in a
  choice's own effects, two in one outcome, or one beside a `goTo` (`Leave`
  included). A fight result cannot start another fight. A fight that no
  outcome starts is refused.
- **Results** are ordinary outcome rows (`effects`, `result`, `goTo`). They
  take no `requires`; the build refuses them.
  - `onDefeated`: every enemy is down. Always required.
  - `onSurvived`: the round limit passed with someone standing. Required when
    `surviveRounds > 0` and refused when it is 0.
  - `onFell`: the fighters fell. Required on `onLoss: wake` and refused on
    `endRun`, where the run is over.
- **`enemies`**: active ids from `enemies.json`, in stage order, repeats
  allowed (`["rat", "rat", "rat"]`). Their `slotSpan`s may add up to at most 3,
  the stage's slots. An enemy meant only for its event sets `"rollable": false`
  in `enemies.json` so room fights never roll it.
- **`party`**: character ids who fight instead of the normal squad. Empty
  means the normal squad. The others sit out untouched. The choice that starts
  it should require one of them `alive`.
- **`surviveRounds`**: 0 means no limit. With a limit, `roundLabel` (the
  counter's word, at most 12 characters, `MaxRoundLabelLength`), `roundSfx`
  and `roundOverlay` apply. Without one they are refused, because nothing
  would read them.
- **`onLoss`**: `endRun` (empty; the run ends, as in a room) or `wake` (the run
  goes on and the fallen stand at 1 HP). **`pays`** (default true): a payout,
  spell drop and Reckoning like a room fight. `false` pays nothing.
  **`elite`**: the elite class, for payout and default backdrop.
- **Art and sound:** `backdrop` and `roundOverlay.path` are Assets-relative
  and baked like page art, filed in the event's own folder (`backdrop` may
  also use `Art/Backgrounds/`). `roundSfx` and `ambience` are
  Resources-relative with no extension.
- **Presence:** each result is an edge from the page that started the fight,
  carrying whatever that choice and outcome guaranteed. A result page's
  speakers must be guaranteed on every launch that reaches it.
- **Result text** plays on the stage when the starting page or the result's
  `goTo` page has lines, and then takes the 200 cap.
- **At runtime:** the pick that starts a fight saves a pending request
  (`RunSnapshot.pendingFight`) and the event stays on that page; the Fight
  screen opens on it, and a quit mid-fight relaunches the same enemies on the
  same stream. When the fight ends, its result outcome applies (effects,
  result text, `goTo`) in one save, the request clears, and leaving the fight
  returns to the Map, which reopens the event on the result and then the page.
  The room stays uncleared and the leg does not advance; the event's Leave
  clears it. A pick whose fight has nobody standing in its party is refused
  (`NoFighters`). A `wake` fight fields no second lives. `pays: false` and a
  `wake` loss end on Continue, with no Reckoning and no defeat screen.
- **On the Fight screen** (`FightRoundPresentation`, off
  `EncounterRequest.Presentation`): `backdrop` replaces the class backdrop;
  with a round limit a top-centre counter reads `<roundLabel> N` ("Toll 3";
  "Round N" when `roundLabel` is empty) and steps as each round's beat plays,
  never past the limit; `roundSfx` plays at each round start; the overlay is
  drawn full-frame behind the figures and scales from `fromScale` at round 1
  to `toScale` at the last round in equal steps; `ambience` loops from the
  fight's start until it is left. A room fight shows none of it. A missing
  file hides its layer (overlay), keeps the class backdrop, or stays silent
  (sounds) -- never an error. Backdrop and overlay files are baked into the
  Fight scene, so a new file needs `-BuildScenes`. Paint the overlay as a
  1920x1080 frame with transparency; at scales above 1 its edges leave the
  screen, so keep what matters near the centre.

## Art

- **Where:** one folder per event, named by its id:
  `Assets/_Project/Art/Events/<event_id>/<page_id>.png`, written into the page
  as `"artPath": "Assets/_Project/Art/Events/<event_id>/<page_id>.png"`
  (Assets-relative, with extension). Example: the demo's `well` page is
  `Assets/_Project/Art/Events/demo_wishing_well/well.png`. The build refuses
  a Resources-style path, and refuses art under `Art/Events/` that is not
  one folder deep in its own event's folder (`EventEntryResolver`). The file
  name is by convention the page id; pages of one event may share a file.
- **Size:** commission at **1920 x 1080** (16:9). The one file is drawn at
  **1280 x 720** as the dialogue stage's set piece (hung 64 below the top,
  clear of the dialogue box) and at **960 x 540** in the line-less layout's
  frame (`EventScreen.StageArtWidth`/`ArtWidth`). Off-ratio art is
  letterboxed, not stretched. (4:3 until 2026-09-28; older 960 x 720 art
  still shows, pillarboxed.)
- **Missing art** (empty `artPath`, or a file not there yet) shows the empty
  frame. Art is baked into the Map scene, so a new file needs a scene rebuild
  (`tools/run_tests_parallel.ps1 -BuildScenes`).

## Limits the build enforces

At most 4 choices per page; at least one choice per page with no `requires`
and not `hiddenUntilMet`; the last outcome of a choice has no `requires`;
every character, item, relic, enemy, ability, page, counter, fight and speaker
named must exist. Fights and event speakers have their own rules, in their
sections.

Text length, in characters. Each cap is the length of the sample the panel's
box is audited against at every scene build (the samples in `UiStrings` are
built from these constants), so anything the content build lets through fits:

| field | cap | constant in `EventEntryResolver` |
|---|---|---|
| page `title` | 28 | `MaxTitleLength` |
| page `body` | 600 | `MaxBodyLength` |
| outcome `result` (shown in the body's place) | 600 | `MaxBodyLength` |
| choice `text` | 50 | `MaxChoiceTextLength` |
| lock reason on a choice row, authored or generated | 46 | `MaxLockReasonLength` |

The lock-reason cap covers generated captions too, so a long display name in
`Requires <name> with 20 CHA` is refused at build rather than clipped on
screen. Raising a cap is a layout change: the box has to fit the longer
sample, and a scene build (`-BuildScenes`) re-measures it.

Errors name the event, page, choice where there is one, and the field.

## Dialogue lines

A page can play dialogue before its choices (`docs/PLAN_DIALOGUE_STAGE.md`).
A page with no `lines` works exactly as it did before: body and choices shown at once.

- **`lines`**: `[{speaker, expression, text}]`. `speaker` is a character id,
  one of the event's own `speakers` (see **Event speakers**), or `narration`
  (no bust, no name plate). A character's `expression` is one of neutral,
  happy, annoyed, nervous, sad, surprised or entranced. Empty means neutral,
  and narration takes none. A missing bust file warns at build and never
  refuses. The line falls back to neutral, then to no bust.
- **Caps**: 12 lines per page (`MaxLinesPerPage`), 200 characters per line
  (`MaxLineLength`). Tags count toward the 200.
- **Markup**: `<i>...</i>` only, lowercase, balanced, not nested. Any other
  `<` is refused, `<b>` included.
- **`cast`**: `[{character, side}]` pins a speaker to `left` or `right`.
  Unlisted speakers alternate by first appearance: the first distinct
  speaker goes left, the second right, and so on. A listed character that
  never speaks on the page is refused.
- **`backdrop`**: event-level (default `Art/Backgrounds/Dungeon.png`), and a
  page can override it. Filed under `Art/Backgrounds/` or the event's own
  `Art/Events/<event_id>/`.
- **Presence**: a speaker must be *guaranteed* in the squad on their page.
  Only a named `inParty`, `memberLevel` or `ability` row guarantees a
  character. On the start page that means the event's own `requires`. On
  any other page, it takes a row on every choice or outcome route that
  leads in. The build refuses a speaker that one route leaves out.
  Knocked-out members still count, so presence means squad membership only.
- **The last outcome is unconditional**, so it is also the fallback variant.
  Point it at a page that is narration-only or uses guaranteed speakers.
  Put character variants (`inParty sheep` -> a Shawn page) in the
  conditional outcomes above it.
- **Counter timing**: choice-level effects apply *before* outcomes are
  evaluated, and outcome-level effects apply *after*. Either works, but an
  event states which one it uses. The Petting Zoo convention bumps the
  counter in the outcome's effects and gates each outcome on the
  pre-increment value.

## Event speakers

A person who belongs to the event rather than the party (the caravan's
merchant) is a row in the event's `speakers`:

```
speaker  { id, name, epithet, bustPath, expressions[] }
```

- Lines and `cast` entries name it by `id`. It stands on the stage like a
  party bust: bottom-anchored, name plate (`name` and `epithet`), and a side by
  `cast` or first appearance, mirrored on the right.
- **Always present** at its own event. It needs no `requires`, on any page.
- **`id`** may not be a character id (a line naming it would be ambiguous),
  `narration`, or a duplicate. `name` is required. `epithet` is capped like a
  character's (32).
- **`expressions`**: the faces it has, lowercase letters, digits and
  underscores, since they are bust file names. A line naming one it did not
  declare is refused. A line naming none takes the first. An empty list
  declares just `neutral`. A character's faces stay the fixed list above.
- **`bustPath`**: a Resources-relative folder holding `<expression>.png` per
  face, like a character's `dialogueBustPath`. Empty, or a missing file, warns
  at build and shows the name plate and text.
- The stage draws an event speaker exactly as it draws a party bust, from
  the line's own face name: the requested face, then `neutral`, then no bust
  (the plate and text still show). A party character's `entranced` loads the
  same way, from `<dialogueBustPath>/entranced.png`.

## Testing a new event

Debug menu (F1) > Tools > open event by id, where the party stands. It also
sets counters, so a tenth-time branch is one click away.
