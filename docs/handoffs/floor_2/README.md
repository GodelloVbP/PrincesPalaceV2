# Floor 2 — The Bellows

Design doc, not yet built. Steampunk, as the first biome to occupy floor 2's
slot.

**A floor is a slot, not a place.** Each floor holds an array of candidate
biomes and a run picks one. Floor 1's array is `[outer_wood]`, floor 2's is
`[bellows]`, and both grow. That is the shape from day one even while each
array holds exactly one entry, because retrofitting it later means touching
every consumer. Section 3 is the mechanism; the rest of this doc designs
**The Bellows**, which is one biome, not the floor.

---

## 0. The finding that has to come first: there is no floor 1

`floor` is an `int` on `RunSnapshot` (`Data/RunSnapshot.cs:56`). It is written
exactly once, `floor = 1` in `RunManager.StartRun` (`Core/RunManager.cs:98`),
and **never incremented anywhere in the project**. Two labels read it —
`HubController.cs:64` and `MapController.cs:186` — and nothing else in the game
branches on it.

Everything that reads as "the forest floor" is a hardcoded constant, not a
floor and not a biome:

| What | Where |
|---|---|
| Map background, path, 7 room icons | `Domain/UiKit/Screens/MapScreen.cs:37-49`, `const string` |
| Fight backdrop, elite backdrop, boss backdrop | `Domain/UiKit/Screens/FightScreen.cs:31-33`, `const string` |
| Enemy pool | `Core/RunEncounter.cs:91` — every active enemy, unfiltered |
| Music | `Resources/Audio/music_layers.json`, one `floors` entry |

So this is two jobs, and the second is worthless without the first: **build the
floor-slot / biome structure**, then **author steampunk into it**. Authored
steampunk on today's codebase would be a background nobody ever sees, because
nothing ever sets `floor` to 2 and nothing would look at it if it did.

### Two live bugs found while reading, both in scope

1. **`bossEnemyId` is never written.** It is read at `RunEncounter.cs:71` and
   `FightBootstrap.cs:312`, declared at `RunSnapshot.cs:57`, and assigned
   nowhere. Every boss room therefore falls through to a random roll from the
   three `isBoss` monsters, and `FightBootstrap.BossIdOf`'s "content gap"
   fallback is the only path that ever runs. Section 3 closes this by
   construction — a biome declares its boss.

2. **`first_forest_boss` can never unlock.** `achievements.json` gates it on
   `DefeatSpecificBoss` / `forest_warden`; `enemies.json` declares that monster
   as `warden`. No code remaps between them. So the achievement never fires and
   `forest_wardens_tooth` is permanently unobtainable. See section 6.

---

## 1. Pushback on the theme, and the version I would rather build

Forest to Victorian steampunk is a hard tonal cut, and the game is called
Prince's Palace. Brass goggles and airships would read as a different game's
second level bolted on.

The version that survives the cut is **the palace's own machinery**: descend
out of the overgrown outer ward and into the works that keep the place
standing. Boilers, bellows, pressure regulators, brass and soot and heat.
Steampunk in silhouette and palette, feudal in provenance — nobody invented
this to be modern, a prince built it to heat a palace and it has been running
unattended for a long time.

That keeps the painterly Hades direction intact, gives the descent a reason
(you are going *into* something, not switching worlds), and every visual
steampunk beat is still available. Everything below assumes it. Say the word
and it re-skins to straight Victorian without any structure changing.

**Name: The Bellows.** Floor 1's existing biome is retro-named **The Outer
Wood** so the pair reads as a set — and so it has an id to sit in an array
next to its future siblings.

---

## 2. What a floor *is*

**A floor is 16 steps: two legs, ending on its boss.**

This falls out of the cadence that already exists — `DescentMapGenerator`
forces an elite every 8 steps and a boss every 16 (`StepsPerElite`,
`StepsPerBoss`). Leg 1 ends on an elite, leg 2 on a boss. A floor is exactly
that pair, and no generator change is needed.

```
floor 1 = steps  0..16   (elite at 8,  boss at 16)
floor 2 = steps 17..32   (elite at 24, boss at 32)
floor N = steps (N-1)*16+1 .. N*16
```

**Derived, not stored.** `FloorOf(step) => step / StepsPerBoss + 1`, as a pure
static in `Domain/Dungeon/` next to `DifficultyCurve`. The save field stays and
gets written through from `step` for display and back-compat, but nothing
computes from it — a stored floor and a stored step are two representations of
one fact and will disagree eventually, which is the same argument
`RunManager.Map` already makes for regenerating the map from its seed rather
than serialising it.

*Considered and rejected:* floor = one leg (8 steps). Too fast to establish an
identity, and a leg that ends on an elite gives nothing to gate the transition
on.

**The descent stays endless.** Floors 1-5 cover steps 0-80, which is exactly
the gear ladder (`GearScaling` tier 10 = step 80). Past floor 5 the slots run
out and section 3's endless rule takes over. A floor is a themed band of an
endless corridor, not a level with an end.

---

## 3. Floors are slots; biomes fill them

Two content types, and the split is the whole design.

### `BiomeDefinition` — everything a place is

`biomes.json`, generated by `ContentBuilder` like everything else. One entry
per themed place. Six fields, each replacing a hardcode named in section 0:

| Field | Replaces | Notes |
|---|---|---|
| `id`, `displayName` | — | `bellows`, "The Bellows". Goes in saves — never rename. |
| `enemyTag` | `RunEncounter.Pool()`'s unfiltered list | New `tags` field on `RawEnemyEntry`; the pool filters to the active biome's tag, falling back to the whole active roster when a tag matches nothing. |
| `bossId` | nothing — the gap in section 0 | Written to `run.bossEnemyId` on biome entry. Fixes bug 1. |
| `mapArt` | `MapScreen.cs:37-49` | Background + path + 7 room icons. |
| `fightArt` | `FightScreen.cs:31-33` | Ordinary / elite / boss backdrops. |
| `musicSetId` | `music_layers.json` `floors` entry | That map becomes floor-to-set keyed by *biome* instead. |

A biome knows nothing about which floor it is on. That is what lets one appear
in two floors' arrays later.

### `floors.json` — the slots

```json
{
  "floors": [
    { "floor": 1, "biomes": ["outer_wood"] },
    { "floor": 2, "biomes": ["bellows"] }
  ]
}
```

Each entry is a floor number and its candidate list. Adding a variant is one
string. Overlap is free and needs no schema change — a biome id may appear in
any number of arrays.

### How a run picks

Deterministic from the run seed, never stored:

```
BiomeFor(runSeed, floor) =
    candidates = floors[floor].biomes  minus  every biome already used
                 by floors 1..floor-1 in this run
    if candidates is empty, fall back to the full unfiltered list
    pick one with RngStreams.Open(runSeed, RngStreams.Biome, floor)
```

Three properties fall out, and each is deliberate:

- **Reproducible and resumable.** Same seed, same floor, same biome, forever —
  the same reason `RunManager.Map` regenerates from the seed rather than
  serialising. No new save field, and no migration when the arrays change.
- **No repeats within a run.** Once arrays overlap, floor 3 could otherwise
  hand you the biome floor 1 just gave you. The exclusion is computable because
  earlier floors are themselves derivable — it needs no state. Falling back to
  the unfiltered list when exclusion empties the pool is the house's graceful
  degradation: a short array is a content state, not an error.
- **`RngStreams.Biome = 5`.** A new stream number, not a reuse. The header on
  that file is explicit that these are a serialized format and that streams are
  kept disjoint so a content change can only affect the thing it is about —
  sharing `Boss` here would mean adding a biome reshuffles every boss pick.

### Past the last authored floor

The descent does not end and the slot list does. Floors beyond the last entry
reuse the **last authored floor's array**, with the same no-repeat exclusion,
which degrades to "keep re-rolling the deepest biomes" once they run out. An
explicit `endless` slot can be added later if the deep game wants its own pool;
nothing here needs to change for that.

### Where the transition fires

`FightBootstrap.cs:293` already calls `RunManager.AdvanceLeg()` when the leg is
over. Crossing a 16-step boundary there is the transition: resolve the new
floor's biome, write its `bossEnemyId`, swap the art and the music set. No new
hook.

### The one awkward piece: art is baked at build time

`ScreenRegistry.cs:87-88` resolves the elite and boss backdrops through
`SceneBuilder.LoadSpriteByKey` and bakes them into serialized fields. That
works for three sprites; with biomes it is *every biome's ten sprites* resident
whether or not the run will ever reach them — and unlike floors, the count of
biomes is meant to grow indefinitely.

**Recommendation:** biome art lives at `Resources/Biomes/<id>/` and loads at
runtime, the way `Intent/<slug>` already serves both loaders from one file
(`FightScreen.cs:36-39`). The currently-baked floor-1 sprites stay exactly as
they are and become the fallback for a biome whose folder is missing — so the
Outer Wood is unchanged, degradation is free, and adding a biome is a folder
plus two JSON lines.

This is the only part of the design with real code cost. Everything else is
content plus one pool filter.

### What the player is told

A variant nobody can name is a variant nobody notices. The hub's descent
caption and the map header should name the **biome**, not "Floor 2" — the
floor number is scaffolding, the place is the thing. `UiStrings.HubResumeFloor`
and `MapDepth` both change wording.

**Default decision, cheap to veto:** the *current* biome is named, the next
one is not revealed until its boss dies. Everything is derivable from the seed
the moment a run starts, so showing the whole itinerary up front is one line —
but knowing floor 3 is the Bellows before picking a squad turns squad choice
into a lookup table. Floor 1's biome *is* shown before the squad is picked,
which is the one case where prep-versus-surprise lands on prep.

---

## 4. The Bellows' mechanical thesis: plating, and pressure

A biome that is only a repaint is a biome the player notices for ten seconds.
The Bellows gets one pressure that its whole roster expresses, and it is chosen
to be the *inverse* of the Outer Wood's.

**The Outer Wood favours the organic damage types.** Bog Witch is weak to
Poison, Stone Golem weak to Nature — Fly and Shawn are the answer.

**The Bellows favours the elemental and arcane ones.** Machines do not bleed
and cannot be poisoned; they resist Physical, and they crack under thermal
shock. Turtle (Ice) and Owl (Arcane) are the answer; Dog (Physical) and Fly
(Poison) struggle. That is a real "bring the right squad" pressure, using only
the weakness/resistance system that already ships — and it is what makes the
biome roll worth surfacing before a run starts.

Paired with that, this is where the **stagger meter** finally matters. Plating
means fat `breakShieldPoints` and high `defense`, which means fights that are
long unless you break them. The Outer Wood's roster mostly ignores that system;
the Bellows is the tutorial for it that never existed.

**And one monster breaks the rule on purpose.** The Bellows Hound has no
plating — soft and hot, resistant to Ice, weak to Nature. An all-Ice squad that
learned the biome's lesson too well hits a wall on it. One exception per biome,
so the roster has to be read rather than pattern-matched.

### Author every biome to the same step-0 band. This is now a hard rule.

`DifficultyCurve` already scales every authored number by absolute step
(`FightEncounterAdapter.ToCombatant`). At step 24 that is **x5.9 health and
x3.4 attack** before anything biome-specific.

Under the old floor-is-a-place design this was a preference. **Under the slot
design it is a requirement**, because the moment arrays overlap the same biome
can be a player's floor 1 or their floor 4, and a biome authored "for floor 2"
would be a wall in one position and a pushover in the other. Difficulty belongs
entirely to the step curve; a biome contributes **shape only** — resistances,
speeds, shields, statuses, encounter composition.

That the curve is step-based rather than floor-based is precisely what makes
floors-as-pools possible at all. It is the load-bearing property of this
design; a future retune that makes difficulty depend on floor identity breaks
it.

---

## 5. Roster — The Bellows

Every monster below fits the existing authoring envelope in `RawEnemyEntry` —
stats, one weakness, one resistance, `breakShieldPoints`, one optional skill
(`skillName`/`skillPower`/`skillChance`), one optional `appliesStatus` on hit,
`avoidsFrontSlot`, art paths. Nothing here needs new combat code. Statuses used
are all live members of `StatusEffectType`. All six carry `tags: ["bellows"]`.

| id | Name | HP | Spd | Atk | Def | Shield | Weak | Resist | Role |
|---|---|---|---|---|---|---|---|---|---|
| `sootling` | Sootling | 70 | 14 | 4 | 0 | 0 | Ice | Fire | chaff |
| `rust_knight` | Rust Knight | 300 | 6 | 7 | 6 | high | Arcane | Physical | wall |
| `pressure_vessel` | Pressure Vessel | 260 | 4 | 5 | 7 | highest | Ice | Physical | anvil |
| `bellows_hound` | Bellows Hound | 190 | 13 | 8 | 1 | low | Nature | Ice | the exception |
| `cog_widow` | Cog Widow | 140 | 11 | 6 | 1 | low | Physical | Arcane | backline |
| `governor` | **The Governor** | 700 | 7 | 10 | 7 | high | Ice | Physical | boss |

**Sootling** — a cloud of live ash. `breakShieldPoints: 0`, which the resolver
already treats as "has no stagger meter and cannot be broken" (distinct from
omitted). Fast enough to act twice while the party is busy on something
plated. No skill. The reason to bring an AoE.

**Rust Knight** — **already authored** in `enemies.json` at exactly these
numbers, sitting `active: false` awaiting art. It is a perfect steampunk
monster that costs zero design. Tag it `bellows`, give it a shield value, flip
`active` when the sheet lands.

**Pressure Vessel** — the biome's statement. Slowest thing in the game after
the Golem, the biggest break shield, and it scalds: skill "Scald",
`skillPower: 1.5`, `skillChance: 0.35`, `appliesStatus: Vulnerable`,
`statusMagnitude: 25`, `statusDuration: 2`. Vulnerable is steam-scald in
fiction and an incoming-damage multiplier in fact, so it makes the *other*
monsters in the room the threat — which is what makes a slow monster worth
killing first.

**Bellows Hound** — see section 4. Skill "Backdraft", `skillPower: 1.7`,
`skillChance: 0.4`. No status.

**Cog Widow** — `avoidsFrontSlot: true`, so she hides behind the plating.
Skill "Magnetise", `skillPower: 1.2`, `skillChance: 0.45`,
`appliesStatus: Provoked`, `statusDuration: 2` — the character she hits can
only swing at *her*, which drags your damage off the thing you were breaking.
A pressure the game already supports and has never used offensively.

**The Governor** — a steam governor is the real mechanism that stops a boiler
running away with itself, and this one has been doing that job alone for a
very long time. Skill "Overpressure", `skillPower: 2.2`, `skillChance: 0.5`,
`appliesStatus: Vulnerable`, `statusMagnitude: 30`, `statusDuration: 2`. High
defense and a real break shield, so the fight is "stagger it or lose the
race".

*Wanted but deliberately not specified:* a monster that ramps itself each turn
(over-pressure building). `appliesStatus` applies to **whoever it hits**, not
to the caster — an enemy cannot buff itself from content. That needs code, so
it is out of scope here rather than written as though it would work. Noted in
section 8.

---

## 6. Boss wiring, done right this time

Bug 2 in section 0 exists because an achievement's `parameter` and an enemy's
`id` were typed independently and never compared. The Bellows avoids it by
having one place the id is written:

- `enemies.json` declares `governor`.
- `biomes.json` names `bossId: "governor"`.
- `achievements.json` gates `first_bellows_boss` on `DefeatSpecificBoss` /
  `governor`.
- `relics.json` adds `governors_weight`, `unlockedBy: first_bellows_boss` —
  mirroring the existing `forest_wardens_tooth` shape.

**Achievements are per BIOME, never per floor.** Once arrays hold more than one
entry, a run can skip a biome entirely, so "clear floor 2" is not a thing a
player can be asked to do — but "put down the Governor" always is.
`DefeatSpecificBoss` on an enemy id is already the right shape; the naming just
has to stop implying position. The existing `first_forest_boss` is fine on that
count, `The Wood Yields` even more so.

**Mechanise it.** A `ContentDatabase.Validation` rule that every
`DefeatSpecificBoss` parameter names a real `isBoss` enemy would have caught
bug 2 at build time and stops the next biome repeating it. Worth two more rules
in the same pass, both cheap and both preventing a silent dead biome:

- every biome id in `floors.json` names a real biome;
- every biome's `enemyTag` matches at least one active enemy, and its `bossId`
  names an `isBoss` enemy carrying that tag.

That is the house preference — fix the class, not the instance. Fixing
`forest_warden` to `warden` in the same pass is a one-word edit, but it does
resurrect an achievement for anyone who already killed the Warden, which is the
right outcome and worth stating out loud rather than doing quietly.

---

## 7. Art required

This is the real cost of the array shape: **the bill below repeats per biome**,
not per floor. Ten sprites and six enemy sheets each. That argues for growing
the arrays slowly — one biome per floor now, the second variant only when its
art is actually wanted — while the structure that makes adding one cheap goes
in immediately.

Delivered per `docs/ART_PIPELINE.md` conventions. Backgrounds full-frame opaque
at **1672x941** (match the existing files, not the 1920x1080 reference
resolution); icons keyed on flat `#00FF00`; enemy sheets through
`slice_actor_sheet.py`.

**Backgrounds (3)** — `Resources/Biomes/bellows/fight.png`, `elite.png`,
`boss.png`. A boiler hall; a collapsed gantry over a furnace mouth for the
elite; the Governor's regulator chamber for the boss.

**Map (2 + 7 icons)** — background and path, plus the seven room icons
(`room`, `mob`, `mob_elite`, `mob_boss`, `rest`, `event`, `chest`) redrawn in
brass and soot. Same silhouettes as the forest set at the same sizes, so
`MapLayout`'s measured clearing positions still hold — those numbers were
measured off `forest_map_background.png` (`Domain/UiKit/MapLayout.cs:25`) and a
differently-composed background invalidates them. **This constraint now binds
every future biome**, which is worth writing into `ART_PIPELINE.md`: a biome's
map background is a repaint of a fixed composition, not a free canvas.

**Enemy sheets (6)** — one per monster above. Note `facing` in each entry; both
existing sheets face right.

**Music (1 set)** — `Resources/Audio/Music/Bellows/`, nine stems on the
`floor_1` pilot spec's tier split, plus one row in `music_layers.json`. The
manifest's own readme already documents this as "a folder and three lines,
never code"; the only change is that the row keys on a biome rather than a
floor number. The Outer Wood's stems have not been recorded either, so this is
not blocking.

### Suggested delivery order

Art is the long pole, so this is built to be shippable before all of it lands —
every art path in this project degrades gracefully already.

- **Phase A (minimum playable floor 2):** the structure in sections 2-3, the
  three fight backgrounds, `rust_knight` + `pressure_vessel` + `governor`. The
  map keeps forest art. Playable, themed where it matters most, three sheets.
- **Phase B:** `sootling`, `bellows_hound`, `cog_widow`, the map kit.
- **Phase C:** music.

---

## 8. Explicitly out of scope

- **A second biome in either array.** The arrays ship holding one entry each.
  The point of doing the slot structure now is that the second entry is content
  plus art, with no consumer to change.
- **Self-buffing / ramping enemies.** Wanted for the over-pressure fantasy;
  needs combat code (section 5).
- **Biome-specific events and shops.** `RoomType.Event` and `.Shop` are still
  placeholders with no bespoke behaviour (`Domain/Dungeon/RoomType.cs:28-34`).
  This does not fix that; `BiomeDefinition` leaves room for an event-pool field
  later, and that field is a strong candidate for what makes two biomes in one
  slot feel different beyond their monsters.
- **Weighted biome selection.** The pick is uniform over the candidate list.
  Weights are one field on the slot entry when a reason to want them appears;
  adding them later does not change the selection's shape.
- **A steampunk item set or weapon family.** Tempting — `itemsets.json` would
  take a brass/clockwork material in one block — but item drops are not
  biome-gated today and making them so is a separate design.
- **Retuning `DifficultyCurve`.** The Bellows lands inside the existing curve
  deliberately, and section 4 explains why that is now structural. If steps
  17-32 play badly, that is a curve finding, not a Bellows one.
- **Floors 3+.** The structure makes them two JSON lines and an art bill, but
  nothing below floor 2 is designed here.
