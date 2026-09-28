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

## The Bell in the Fog

`bell_in_the_fog` (`docs/PLAN_EVENTS_BELL_AND_CARAVAN.md` 1.4) is the first
returning event and the first event fight.

> **DRAFT COPY (M7a, 2026-09-28).** Every line, result and title in the Bell
> is a first draft written to the brief (quiet dread, never gory; the bell is
> the only clean thing in the forest; a clue about the mind's owner, never an
> explanation, no human names). The owner rewrites it. The tests pin page
> ids, choice texts, speakers and effects, never the prose, so a rewrite
> breaks nothing as long as it keeps under the caps.

- **Graph.** `bell` (Touch the bell / Ask the others / Walk away) opens with
  Shawn `entranced`. Ask routes by who is standing, first match wins:
  `ask_both`, `ask_bjorn`, `ask_odette`, `ask_none`; each has one row, "Listen
  again", back to `bell`. Walk away leaves with a result and no `finish`, so
  the Bell comes back at a later Event node.
- **Fight `bellwether`.** Shawn alone, 10 tolls, `wake`, `pays: false`.
  `onSurvived` (Endure) -> `endure`, `onDefeated` (Break) -> `break`,
  `onFell` (Fall) leaves on its result. All three `finish`. Endure grants
  Toll of the Flock, Break both relics, each with **80 exp to Shawn**: a
  literal, since event exp does not scale with depth. 80 is about a
  floor 1-2 elite room's payout per member (two enemies of ~25 raw exp x the
  1.56 elite multiplier); it underpays deeper. A depth-scaled `exp` is an
  engine change, not content.
- **Endure's Leave** has four outcomes on who is standing, so the "only a
  second passed" line comes from whoever is actually there (Bjorn, Odette,
  both, or nobody). It pays nothing; the fight result already did.
- **The Bellwether** (`enemies.json`, `rollable: false`) stands on its own
  seven stills, `Resources/Enemies/bellwether/` (M9a, 2026-09-28), sliced by
  `Art/Enemies/bellwether/recipe.json` onto one 758x593 canvas, ground line 8,
  head box in `StanceManifest.json`. Redesigned 2026-09-28 as an upright,
  gaunt black ram (idle 559px tall against Shawn's 350). Tuned in M8a and again
  with the round-limited attack rate (2026-09-28): HP 150, attack 33, speed
  5, defenses 20/20, rally 8% x 10. Speed 5 rather than 8 is on purpose:
  fewer, heavier swings spread the floor-1 result, where the bot's Shawn
  barely varies, so one attack point moves floor-1 Endure by ~6 points
  instead of ~20. The per-floor table is in that commit's message.
- **The Bellwether's kit** (docs/PLAN_BELLWETHER_KIT.md, M5, 2026-09-28).
  No plain swing (`attackWeight` 0). Every acting turn is **Scratch**
  (`bellwether_scratch`: a physical lunge in its `attack` pose that leaves
  Bleed 8 x 3) except its 1st/2nd and 5th/6th, which are the scheduled pair
  (`schedule`, counted on its own acting turns, never split): **Dark Chains**
  (`dark_chains`, `extra` pose, Reposition to the front seat, no damage) and
  then **Death Knell** (`death_knell`, `cast` pose, Void, 110/35/0% of the
  chained target's max HP at the front/middle/rear seat when it lands,
  `ignoresDefense` so armour cannot soak the front). Each round's rally plays
  its `cast` pose with a void ripple (`rallyPerRound.stance`/`vfx`) and its
  status row shows the stacks as `x3` on `Status/rally.png`. While the knell
  is committed, its callout sits on a dark plate and each party seat shows the
  knell's figure on the floor (red lethal, lilac survivable, SAFE). Numbers
  are M5 start values; M7 tunes them. Art: seven recipes under
  `Art/Sheets/recipes/` (`bellwether_scratch`, `_drops`,
  `bellwether_toll_ripple`, `dark_chains_origin`/`_travel`/`_bind`,
  `death_knell_bell`/`_shockwave`), each recipe's `_notes` saying how the
  delivered grid differed from the ask. **Sound pending**: no kit sfx is
  delivered, so every `sfxPath` is empty and the kit is silent -- still owed:
  the claw rake (dry, wet tail), the bleed tick, the chains (rattle + void
  whoosh, ~0.8s), the knell (cracked toll, sub-bass, ~2s), the lethal-badge
  sting, and a toll sfx for the rally ripple (the round's own `toll` still
  plays in the Bell fight). Captures: `docs/captures/kit-m5/`.
- **Art and sound** delivered (M9a, 2026-09-28): `fog_clearing.png` (event
  backdrop and fight backdrop), `stump_bell.png` (bell and ask pages),
  `endure.png`, `bell_broken.png`, `flock.png` (round overlay, 0.35 -> 0.6,
  its feet -- `pivotY` 0.15 -- on the fog line half-way up the frame, tinted
  `#C8D0DAB4` so the fog shows through; redesigned art 2026-09-28),
  `Audio/Sfx/Events/bell_in_the_fog/toll` (round sfx),
  `Audio/Music/Events/bell_in_the_fog/wind` (ambience), and Shawn's
  `entranced` bust (`normalize_dialogue_busts.py`, recorded in
  `Art/Portraits/dialogue_recipe.json`).
- **Relics.** Both icons are in `Art/Items/Relics/Processed/`
  (`bellwethers_bell.png`, `toll_of_the_flock.png`); a relic `iconPath`
  naming a missing file stops the scene build, so the build is what proves
  they resolve. Toll of the Flock's `vfx` is one travelling layer (a ghost
  flock galloping from Shawn to each struck enemy) cut from
  `Art/Sheets/recipes/toll_of_the_flock.json` into `Spells/toll_of_the_flock`,
  with `flock_charge` as its sfx.
- Pinned by `BellInTheFogEventTests` (content, fast host) and
  `BellInTheFogRunTests` (every page, Endure/Break/Fall through the run, the
  bot's walk, never rolled). Captures: `BellInTheFogCaptureTests`,
  `docs/captures/events-art/`.

## The Rat Caravan

`rat_caravan` (`docs/PLAN_EVENTS_BELL_AND_CARAVAN.md` 1.5) is the first
merchant shelf and the first event speaker in shipped content.

> **DRAFT COPY (M7b, 2026-09-28).** Every line, result and title, the
> merchant's name ("Mister Pockets") and his epithet ("Dealer in Nearly
> Everything") are a first draft written to the brief: sly, comic, slightly
> shady, and he never pretends the goods are genuine. The owner rewrites
> them. The tests pin page ids, choice texts, speakers, faces and effects,
> never the prose, so a rewrite breaks nothing inside the caps.

- **Graph.** `caravan` (Browse / Browse with Odette / Rob him / Walk on)
  opens with the merchant on the right, grinning through the pitch and
  neutral when he admits some of it is fake. Browse opens the shelf and
  lands on `after_browse` (neutral, "no refunds"); Browse with Odette
  (`inParty owl alive`, hidden until met) opens it with the fakes marked and
  lands on `after_odette`, where Odette names the fakes and the merchant
  grins about it. Both after-pages offer Walk on / Look again / Rob him --
  **Walk on first**, so a first-available bot never loops. Look again is a
  plain `shelf` to `after_browse`; the marks stay (they are the stock's).
  Every Walk on leaves on the merchant's parting shot with no `finish`, so
  the caravan comes back at a later Event node with the same stock.
- **One deviation from the plan's graph:** Browse with Odette goes to its
  own `after_odette` page rather than `after_browse`, because Odette's
  spotting of the fakes needs a page to be said on (a result beat is
  narration only). Its rows are `after_browse`'s.
- **Shelf `caravan`**: 70%, a third fake, the gear roll plus two
  consumables, titled "The Rat Caravan", kept by `merchant`.
- **Fight `rat_pack`**: three `rat`s (slot span 1 each, the stage's three
  slots), elite, the normal squad, no round limit, `endRun`, pays. The
  Reckoning plays, then `onDefeated`: `takeShelf` 1 + `finish`, a result
  line (the effects line under it names every card taken and the one "Lost
  in the scuffle"), then `robbed`, where the merchant is hostile and
  leaves. Its Leave is silent. The merchant's hostile face is on `robbed`
  only: a pick that starts a fight goes straight to the fight, so there is
  no page before it to be hostile on.
- **Tuning (M8b, 2026-09-28):** see that commit's message for the per-floor
  rob-vs-elite-room win rates.
- **Art** delivered (M9b, 2026-09-28): `road.png` (event and fight
  backdrop), `caravan.png` (the three browsing pages), `robbed.png`, and the
  merchant's busts `Resources/Portraits/Dialogue/rat_merchant/{neutral,
  grinning,hostile}.png` (one 1408x1402 canvas, Shawn's bust format; no
  source or recipe was delivered for them, so they are not reproducible).
- Pinned by `RatCaravanEventTests` (content, fast host) and
  `RatCaravanRunTests` (every page and the rob through the run, the bot's
  walk, the `-EventChoice "Rob him"` probe). Captures:
  `RatCaravanCaptureTests`, `docs/captures/events-m7b/` (before the art) and
  `docs/captures/events-art/` (with it).

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

`{ kind, amount, item, counter, character, relic, fight, shelf, reveal }`, applied in order.

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
| `finish` | none (refused). Marks a `mayReturn` event seen, and ends every shelf stock it owns; see **Returning events** | none |
| `shelf` + `shelf` (+ `reveal`) | none (refused). Opens the event's own shelf by id; see **Merchant shelves** | none |
| `takeShelf` + `shelf` | cards lost first, 0 or more. Hands the party the shelf's unsold cards; see **Merchant shelves** | `+1 <item>` per card, then `Lost in the scuffle: <item>` or `Nothing left to lose in the scuffle` |

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
         backdrop, roundSfx, ambience,
         roundOverlay { path, fromScale, toScale, pivotX, pivotY, anchorX, anchorY, tint },
         onDefeated, onSurvived, onFell }
```

- **Starting one:** the `fight` effect goes in an **outcome's** effects, one
  per outcome, and that outcome's `goTo` stays **empty**. The fight's result
  says where the event goes next. The build refuses a `fight` effect in a
  choice's own effects, two in one outcome, or one beside a `goTo` (`Leave`
  included). A fight result cannot start another fight. A fight that no
  outcome starts is refused. A choice with effects of its own may not have
  an outcome that starts a fight with a `party`: whether that party has
  anyone standing is checked before the choice's effects apply, and they can
  change which outcome wins.
- **Nobody to send:** a pick whose fight would field nobody is refused. A
  request that fields nobody by the time it is built (or loaded) is dropped,
  and the event stays on the page that launched it.
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
- **Depth in a round-limited fight.** Enemies scale with depth as in a room
  (health 7.5% a step, break shields likewise, defenses not at all), except
  **attack, which rides 5.3% a step instead of 3.8%** whenever
  `surviveRounds > 0` (`DifficultyCurve.ScaleEnemyAttack`, applied where the
  fight is built from its request). A room fight's danger grows with its
  length; a fight that ends after N rounds has no length to grow, so on the
  room rate it gets easier every floor (M8a: the Bell's median Endure HP
  8% -> 58% over floors 1-5). The health rate overshot (Bell Endure
  92% -> 10%). Author a round-limited enemy's `attack` for floor 1; the rate
  carries it down. Summons called in during the fight follow the same rule.
  Room fights and `surviveRounds: 0` event fights are unchanged.
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
  fight's start until it is left. A room fight shows none of it. **Where the
  overlay stands:** it scales about `pivot`, a point of its own image
  (fractions from the image's bottom-left), held on `anchor`, a point of the
  frame (fractions from the frame's bottom-left; the backdrop fills the frame,
  so this is a point on the painting). Both default to 0.5, the centre-scaled
  full frame. `tint` ('#RRGGBB' or '#RRGGBBAA', refused without a path)
  multiplies the image; its alpha lets the backdrop show through. The build
  refuses a pivot or anchor outside 0-1 and a tint that is not a colour
  token (`FightRoundPresentation.PlaceOverlay` is the formula). A missing
  file hides its layer (overlay), keeps the class backdrop, or stays silent
  (sounds) -- never an error. Backdrop and overlay files are baked into the
  Fight scene, so a new file needs `-BuildScenes`. Paint the overlay as a
  1920x1080 frame with transparency; at scales above 1 its edges leave the
  screen, so keep what matters near the centre.

## Merchant shelves

An event can keep a merchant's stock: the room shop's roll and buying, on a
stock that belongs to the **event**, not the node. Each is a row in the
event's `shelves`, opened by a `shelf` effect and robbed by `takeShelf`.

```
shelf  { id, priceFactorPercent, fakeShare, sections[], consumableCount, title, keeper }
effect { "kind": "shelf", "shelf": "<id>", "reveal": false }
effect { "kind": "takeShelf", "shelf": "<id>", "amount": 1 }
```

- **The stock** is rolled the first time a `shelf` or `takeShelf` effect of
  the run needs it, at that (step, node), on the shelf's own streams
  (`RngStreams.ShelfGear`/`ShelfConsumables`/`ShelfFakes`, 10-12, keyed by
  the event and shelf too, so two shelves at one node roll apart), and kept on
  the run (`RunSnapshot.shelves`): across a reload, Walk on, a room shop in
  between and a return at any later node. Every visit shows the same cards
  minus what sold. There is no reroll. It ends only on the event's `finish`
  or with the run.
- **`sections`**: `["gear"]` stocks the room shop's gear roll -- 4 cards, same
  candidates, tier band, affixes and floors. Books and relics are refused:
  they carry no item instance, so a fake could not apply. **`consumableCount`**
  (0-3) adds that many consumables from `items.json`, drawn without repeats
  (the room shop sells none). A shelf must stock something.
- **`priceFactorPercent`** (1-100): each card costs that percent of the room
  shop's price for it, rounded half away from zero, never below 1 (a 15-gold
  potion at 70 is 11). Selling back is unchanged: 30% of the room shop's
  price, fake or not.
- **`fakeShare`**: `max(1, round(cards / fakeShare))` of the real cards are
  fake, picked on their own stream -- 3 means a third (1, 1, 2, 2 of 3, 4, 5,
  6 cards); 0 means none. A fake looks, prices and sells like the genuine
  card. Fake gear falls apart after 3 fights worn by a fielded member ("No
  refunds."); a fake consumable spends the turn and does nothing.
- **Every copy from a shelf is its own lot** (`<event>:<shelf>:<step>:<node>:<card>`),
  genuine ones included, so it never stacks with anything -- not even an
  ordinary potion -- and a fake is never given away as the one that did not
  stack.
- **`shelf`** puts the shelf on screen after the pick; leaving it returns to
  the event, on the page the pick's `goTo` named. It never clears the room:
  the event's own Leave does. `"reveal": true` marks the fakes ("FAKE" on the
  card's meta line) for this and every later visit and reload; a plain
  `shelf` after it keeps the mark. The mark is the shelf's: it does not
  follow a copy into the bag. The build refuses a `shelf` in a pick that
  leaves the event (`goTo` Leave), starts a fight, finishes, or opens a
  second shelf, and in a fight's result.
- **`takeShelf`** hands over every unsold card as the copy a purchase would
  have given (fakes stay fake), after the scuffle loses `amount` of them,
  picked on `RngStreams.ShelfScuffle` at the robbery's (step, node); Odette's
  marks play no part. None left: nothing is lost, and the line says so. Put
  `finish` after it -- the build refuses a `shelf`/`takeShelf` after a
  `finish`, which would roll a fresh stock nobody sees.
- **The shelf screen** is the shop's: gear in the gear panel, consumables in
  the book panel under CONSUMABLES, and no relics, rerolls or pack.
  **`title`** (capped like a page title, 28) replaces SHOP; empty, the
  keeper's name does (so the build refuses an untitled shelf whose keeper's
  name is over 28), then SHOP. **`keeper`** names one of the event's own
  `speakers`: the relic panel's slot shows that speaker's bust (first
  declared face, then neutral, else PORTRAIT PENDING), name and epithet, and
  -- when `fakeShare` is above 0 -- "Some of these are fakes..." or, once
  revealed, "The fakes are marked FAKE." The build refuses a keeper that is
  not a speaker of the event.
- **At runtime** the pick saves `RunSnapshot.pendingShelf` (so a quit on the
  shelf comes back to it); while it is set the event refuses picks
  (`ShelfOpen`). The bot runs the shop's buying loop on it and then leaves.

A worked example, the caravan's shape:

```json
"shelves": [
  { "id": "caravan", "priceFactorPercent": 70, "fakeShare": 3,
    "sections": ["gear"], "consumableCount": 2 }
],
...
{ "text": "Browse", "outcomes": [
  { "effects": [{ "kind": "shelf", "shelf": "caravan" }], "goTo": "after_browse" } ] },
{ "text": "Browse with Odette", "hiddenUntilMet": true,
  "requires": [{ "kind": "inParty", "character": "owl", "alive": true }],
  "outcomes": [
  { "effects": [{ "kind": "shelf", "shelf": "caravan", "reveal": true }], "goTo": "after_browse" } ] },
...
"fights": [
  { "id": "rat_pack", "enemies": ["rat", "rat", "rat"], "elite": true,
    "onDefeated": { "result": "...",
      "effects": [{ "kind": "takeShelf", "shelf": "caravan", "amount": 1 }, { "kind": "finish" }],
      "goTo": "Leave" } }
]
```

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
every character, item, relic, enemy, ability, page, counter, fight, shelf and
speaker named must exist. Fights, merchant shelves and event speakers have
their own rules, in their sections.

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
