# Authoring event rooms

An event room is one entry in `Assets/_Project/ContentData/events.json` plus,
optionally, one picture per page. No code. Field-by-field defaults are in
`docs/CONTENT_SCHEMA.md` (generated); this page is the walkthrough.

## The shape

```
event    { id, floors[], requires[], pages[] }
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

## Requirements

One flat row, `{ kind, character, ability, min, max, counter }`. A list is AND.
The same rows gate an event (`event.requires`), a choice and an outcome.

| kind | fields | passes when |
|---|---|---|
| `inParty` | `character` | that character is in the active squad |
| `memberLevel` | `min`, `character?` | any squad member (or the named one) is at least level `min` |
| `ability` | `ability`, `min`, `character?` | any squad member's **effective** score (gear and talents included) is at least `min` |
| `counter` | `counter`, `min?`, `max?` | the counter's value is inside `[min, max]` |
| `gold` | `min` | run gold is at least `min` |

A choice that fails shows greyed with the first failing reason and cannot be
picked; `hiddenUntilMet: true` hides it instead. Never author a gold gate for
a gold spend: the spend adds its own.

## Effects

`{ kind, amount, item, counter }`, applied in order.

| kind | amount |
|---|---|
| `gold` | positive gains (counts as earned), negative spends (does not) |
| `healPercent` | 1-100 of each member's max HP |
| `damagePercent` | 1-100; floors at 1 HP, never kills |
| `exp` | split across the party as after a fight |
| `item` | how many of `item` go to the stockpile |
| `counter` | added to `counter` |

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
once per run. An empty pool falls back to the old "nothing here" line. Never
rename an event id once a save exists.

## Art

- **Where:** `Assets/_Project/Art/Events/<event>_<page>.png`, written into the
  page as `"artPath": "Assets/_Project/Art/Events/<event>_<page>.png"`
  (Assets-relative, with extension; the build refuses a Resources-style path).
- **Size:** the frame is **960 x 720** at 1080p (4:3 landscape). Deliver at
  that size or an exact multiple (1920 x 1440). Off-ratio art is letterboxed,
  not stretched.
- **Missing art** (empty `artPath`, or a file not there yet) shows the empty
  frame. Art is baked into the Map scene, so a new file needs a scene rebuild
  (`tools/run_tests_parallel.ps1 -BuildScenes`).

## Limits the build enforces

At most 4 choices per page; at least one choice per page with no `requires`
and not `hiddenUntilMet`; the last outcome of a choice has no `requires`;
`body` at most `EventEntryResolver.MaxBodyLength` characters (600, measured to
fit the panel); every character, item, ability, page and counter named must
exist. Errors name the event and page.

## Testing a new event

Debug menu (F1) > Tools > open event by id, where the party stands. It also
sets counters, so a tenth-time branch is one click away.
