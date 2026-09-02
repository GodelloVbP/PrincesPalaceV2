# Plan — the in-run Shop, and the spell books it sells

Two systems, one commit stream. The shop is the smaller half: it is a room, a
roll and a screen. The spell books are the larger half, because "the standard
spell loadout disappears" is not a deletion — the thing being deleted is the
only path by which a caster's Intelligence multiplies anything at all.

Written against `5f1e7d4`. Every file:line below was read, not remembered.
Every gold number below was measured off `reports/bot/20260902-020048`
(12,000 runs, four shards, commit `32d31ba`), not estimated.

**Revised 2026-09-02 against an external review of `b7adb83`.** The structural
change is §4. The loadout change and the shop are no longer one commit stream:
spell books land **additively** first (Phase A, with the five spells keeping
their level unlocks), the shop, the screen and the bot batches follow, and the
flip to book-only is the **last** phase, gated on what Phase D measures rather
than on an argument made now. The author's decision that the standard loadout
disappears is unchanged — only the order of the risk is. Everything else in
this revision is smaller: a third RNG coordinate instead of a packed one (F9),
one method per shop mutation with one save at the end (§2f), a detail panel and
a keyboard focus order (§3d), and a recipient strip sized to the squad the save
can actually field (§3e).

---

## 0. Ten things the code says that change the build

### F1. There is no store in v2 to reuse, and no in-run gold sink of any kind

The brief says "`Core/StoreController.cs` (the hub store: what can be reused)".
That file does not exist. Nothing in v2 does.

- `Assets/_Project/Scripts/Domain/UiKit/Screens/` holds eighteen screens and
  none of them is a store.
- `ScreenRegistry.All` is five entries — MainMenu, Hub, Map, Talents, Fight
  (`Editor/SceneBuilder/ScreenRegistry.cs:55-61`).
- The hub's own Principality button is wired to
  `Debug.Log("Principality")` (`Core/HubController.cs:101`). That is the entire
  implementation.
- `docs/handoffs/shop/` is a **v1** prototype (`Shop Screen.dc.html`,
  `reference_screenshot.png`). It describes a permanent-Gold hub store with
  "UPGRADES" and "ITEMS" columns and an "Extra Recruit Slot (Owned)" row. None
  of it is live code in this repo.

So there is no `StoreController` to lift parts from, and the Shop is not a
second store — it is **the first one**. It is also the first sink for run gold
in the game's history: `RunManager.BankPayout` (`Core/RunManager.cs:396-402`)
is the only writer of `run.gold` outside `EndRun`, and there is no reader that
spends it. `AUDIT.md:105` (#2, "the whole economy is incoherent by ~2 orders of
magnitude") and `AUDIT.md:813` (#37, "the economy is built around a
voluntary-retreat flow the design does not have") are both symptoms of that
absence.

**Consequence for the build:** budget nothing for reuse. Budget for a first
screen of its kind, and expect the pricing to be the contentious part rather
than the plumbing.

### F2. Shop already generates, already renders, already resolves — as an apology

`RoomType.Shop` is listed among the placeholders that "generate, they render,
they can be entered and cleared" (`Domain/Dungeon/RoomType.cs:27-33`). It has a
real generation weight — 6 out of 90 in `MiddleRooms`
(`Domain/Dungeon/DescentMap.cs:173-180`: Fight 52, Event 14, Treasure 12,
Shop 6, Rest 6). `RoomResolution.Resolve` returns `Kind.ShopNotBuilt`
(`Domain/Dungeon/RoomResolution.cs:99-100`) and `RoomResolver.TryMessage`
prints `UiStrings.MapRoomShop` (`Core/RoomResolver.cs:92-94`).

**How often a player sees one.** A leg is 8 steps
(`DescentMap.cs:124 DefaultLegLength`), of which two are forced — elite at
offset 4, boss at 8 (`DescentMap.cs:139-140`). Six columns roll freely, at up
to `MaxColumnWidth = 3` nodes each (`DescentMap.cs:144`). At 6/90 per node and
~2.3 nodes a column, a column offers a shop about 15% of the time, so roughly
**0.9 shops offered per leg** — and the player only takes one node per column,
so actually visiting one is a choice against a fight. This is arithmetic off
the weights, not a measurement; the bot's `runs.jsonl` records `roomType` only
for fights, so a real count needs a batch that traces every room.

Nothing about this changes. The build replaces one enum case's meaning, not the
map.

### F3. The arrival seam is one enum with three values, and it needs a fourth

`RunOrchestrator.Arrival` is `Refused | Fight | Resolved`
(`Core/Bot/RunOrchestrator.cs:150-162`), and `ArriveAt`
(`RunOrchestrator.cs:169-193`) is the single body both callers go through:

| Caller | Line |
|---|---|
| the screen | `Core/MapController.Walk.cs:172` |
| the bot | `Core/Bot/BotRunDriver.cs:364` |

`ArriveAt` currently branches once — fight or not — and everything not a fight
is resolved on the spot with `RoomResolver.Resolve` + `ClearCurrentRoom`
(`:189-192`). A shop is the second room that leads to a screen and therefore
must **not** clear itself on arrival. Adding `Arrival.Shop` costs one case in
each caller and nothing else; `MapController.Walk.Arrive`'s `default:` arm
already falls through to `Refresh()`, so a new value that is not handled would
silently clear-and-redraw, which is the wrong failure. Handle it explicitly in
both.

### F4. There are two different things called "spell", and only one of them is what the author means

**Revised 2026-09-02 against author decision 1.** The original text below
treated `spells.json`'s auto-scaling ladder as *the* spell system and planned
to delete it. Reading the author's own example — "the mud blast and frost
flare etc." — against `Assets/_Project/ContentData/skills.json` shows that is
the wrong target. `mud_burst`, `static_fleece`, `frost_flare`, `lightning_bolt`
and `golden_fleece` are ordinary `characterId: "sheep"` entries in
**`skills.json`**, each with its own `unlockLevel` (3/4/5/7/8) and its own
`manaCost` (8/6/7/11/12). They are not rows of `spells.json`. `spells.json` is
a *different* system: a level-keyed curve (`Spark`…`Ascendance`) that drives
the automatic, nameless "Skill" action every character gets regardless of what
it has learned (`FightController.SpellTierFor` in the old readme's own words).

Two systems, one word, easy to conflate:

| | `spells.json` → `PlayerKit.BasicSpell` | `skills.json` entries with `manaCost > 0` |
|---|---|---|
| What it is | An automatic, nameless "Skill" action every character has from level 1 | Named, authored abilities (Mud Burst, Frost Flare, …) |
| Gated by | Character level only, always on | `unlockLevel` today |
| Reached via | `FightEncounterAdapter.TierAtLevel`/`SpellTierFor` → `PlayerKit.BasicSpell` → an appended submenu row | `ContentDatabase.AvailableSkillsFor` → `PlayerKit.Skills`, same as `shear`/`headbutt`/etc. |
| What the author means by "the mud blast and frost flare" | Not this | **This** |

**Decision 1 removes the first row's gating and the appended-row plumbing —
never the second row's underlying scaling — and it does not delete
`spells.json`.** Here is why that distinction has teeth.

`mud_burst` and the other four use `scalingAxis: "Auto"` (or leave it
unwritten, which is the same thing), which `ScalingAxes.For` resolves to the
**Spell** axis for a Nature-typed caster (`Domain/Combat/SkillResolution.cs:106-110`).
A Spell-axis skill's damage rides `actor.SkillScaling`
(`SkillResolution.cs:110`), which is built once per fight from
`ContentDatabase.Effective.cs`'s `EffectiveSkillScaling(character)`
(`:640-675`): `tier.scaling` — the `intelligence`/`wisdom` grade off the
**same `spells.json` ladder**, looked up by `character.level` — combined with
whatever `spellScaling` the equipped weapons carry. **This is the only place a
caster's Intelligence and Wisdom multiply any Spell-axis cast, named skill
included**, and it is keyed to level, not to which named skill is being cast.
Deleting `spells.json` — the original text's plan — would silence Intelligence
for `mud_burst` too, which is exactly the failure mode the old F4 warned about,
just aimed at the wrong file.

**So `spells.json` / `SpellTierDefinition` / `ContentDatabase.GetSpellTierForLevel`
/ `EffectiveSkillScaling` all stay, untouched, as the shared INT/WIS-by-level
scaling curve every Spell-axis skill already rides.** What *does* go is the
narrower BasicSpell layer built on top of it — the automatic, nameless action
and its appended row:

| File | Lines | What | Disposition |
|---|---|---|---|
| `Core/FightEncounterAdapter.cs` | `:582`, `:621`, `:628-635` | both `KitFor` overloads' basic-spell lookup, `TierAtLevel`, `SpellTierFor` | removed |
| `Domain/Combat/Session/CombatantKit.cs` | `:42-43`, `:49`, `:58` | the `BasicSpell` field and ctor parameter | removed |
| `Domain/Combat/Session/FightSession.Skills.cs` | `:852`, `:854`, `:856-857`, `:866-869`, `:905`, `:907` | seven members: cost, name, afford, preview, multiplier | removed |
| `Domain/Combat/Session/FightHudModel.cs` | `:35`, `:46`, `:55`, `:93-95`, `:145-159` | `IsBasicSpell`, `BasicSpellRow`, `SkillRowCount`, the appended row | removed |
| `Core/FightController.Hud.cs` | `:894` | the detail panel's POWER stat | removed |
| `Domain/Bot/FightAction.cs` | `:15`, `:76`, `:114-122`, `:166` | the `BasicSpell` action kind | removed |
| `Domain/Bot/FightRunner.cs` | `:219`, `:228-229` | trace naming (`"Skill:" + BasicSpellNameFor`) | removed |
| `Domain/Bot/GreedyAggressivePolicy.cs` | `:45`, `:81`, `:94-95` | scoring | removed |
| `Domain/Bot/GreedyDefensivePolicy.cs` | `:149`, `:247-248` | scoring | removed |
| `Domain/Bot/Lookahead2Policy.cs` | `:27`, `:154-156` | scoring | removed |
| `Data/Character.cs` | `:79` | a comment describing the mechanic | rewritten |
| tests | `BotPolicyTests.cs:34-35,115-116,253-254,365-366`, `ChilledStatusTests.cs:233`, and every other `new PlayerKit(...)` site | the ctor's default arg absorbs most, the four `basicSpell:` sites do not | updated |
| `Core/Content/ContentDatabase.Effective.cs` | `:569`, `:602`, `:640-675`, `:686` | `EffectiveSkillScaling`, `EffectiveWeaponScaling`, `EffectiveSkillDisplayName`, `GetSpellTierForLevel` | **kept, unmodified** — this is the scaling curve, not the appended row |
| `Core/Content/ContentDatabase.Validation.cs` | `:440-472` | duplicate-level and must-have-level-1 checks on `spells.json` | **kept** — the ladder is still authored content that needs the same checks |
| `Core/Content/SpellTierDefinition.cs`, `Domain/Content/RawSpellTierEntry.cs`, `SpellTierEntryResolver.cs`, `ResolvedSpellTier.cs` | whole files | the content type | **kept** — still resolves `spells.json` for `EffectiveSkillScaling` to read |
| `Editor/ContentBuilder.cs` | `:22`, `:48`, `:59`, `:435-462` | `BuildSpellTiers` | **kept** |

No `ScalingProfile` needs inventing for a book. A book teaches an existing
`SkillDefinition`, and that definition's own `scalingAxis` already routes its
damage through the untouched ladder above — the same as `shear` or `headbutt`
do today. §1a spells out the consequence for content shape.

### F5. A learned spell is already a `SkillDefinition` — resolving it is not new work, it is the existing skill pipeline with one more unlock route

Under decision 1 (F4) a "spell" that gets learned is one of the five existing
`skills.json` entries. `ResolvedSkill` (`Domain/Content/ResolvedSkill.cs:15-80`)
already carries every field it needs, and `FightEncounterAdapter.Resolve`
(`:651-670`) already converts a `SkillDefinition` to one — there is nothing to
build here, only a fourth unlock route to add.

`PlayerKit.Skills` (`CombatantKit.cs:35`) is already the list the fight's Skill
submenu is built from — `FightHudModel.SkillRows`
(`FightHudModel.cs:118-160`) iterates `session.SkillOptionsFor(actor)`,
handles affordability, cooldown labels and ability-requirement filtering, and
(today) then appends the basic-spell row at the end.

So the whole of the fight-side work is: **teach `ContentDatabase.AvailableSkillsFor`
a fourth "how is this unlocked" route (§1a) so a learned skill flows into
`PlayerKit.Skills` exactly the way `unlockLevel`, `unlockedSkillIds` and talent
grants already do, and stop appending the basic-spell row (F4).** No new
submenu, no new dispatch, no new targeting. `FightHudModel.BasicSpellRow`/
`SkillRowCount` (`:93-95`) exist precisely so the row index and the dispatcher
cannot disagree about which row is the basic spell; both disappear with it.

The brief's "the fight's spell submenu" is therefore already built. Do not
build a second one.

### F6. Items already carry an authored gold price, and it is about 5× too expensive for run gold

`ItemDefinition.cost` exists, with the tooltip "Permanent Save.Gold cost to buy
one in the Divine Principality store" (`Core/Content/ItemDefinition.cs:59-60`).
`itemsets.json` authors it per set as `cost` + `costPerTier`, expanded by
`ItemSetEntryResolver.cs:157-158` and written at `:282` as
`cost + costPerTier * tier`.

Measured across the 646 built assets in `Resources/Content/Items/`:

| Kind | n | tier | cost min / median / max |
|---|---|---|---|
| Consumable | 2 | 0 | 15 / 15 / 15 |
| Weapon | 33 | 0-10, 3 per tier | **0 / 0 / 0** |
| Equipment | 611 | 0-10, ~55 per tier | tier 0: 0/55/80 · tier 5: 130/280/330 · tier 10: 230/505/580 |

Against measured run income of ~22 gold a normal fight and ~185 gold a leg
(§2), a median tier-5 torso at 280 is a leg and a half of saving for one piece
— and every weapon is free, because `weapons.json` has no `cost` field at all
and its `_readme` says outright "weapons are never sold in the Store — they
only drop as loot".

**Do not price the shop off `ItemDefinition.cost`.** Take its *shape* (linear
in tier) and nothing else. Leave the field alone rather than rewriting 646
assets: it is v1 residue with no live reader, and repurposing it would make one
number answer two questions.

The one exception worth keeping: consumables. `health_potion` at 15 is already
about two-thirds of a normal fight's payout, which is a sensible potion price.
Read consumable prices from `cost`; compute everything else.

### F7. The bag is `SaveData.stockpiledItems`, not `RunSnapshot.inventory`

The sell section needs to know what "the bag" is, and the two candidates are
not equally alive.

- `SaveData.stockpiledItems` (`Data/SaveData.cs:146`) is what the character
  sheet paints (`Core/CharacterDossierController.cs:431`), what a taken offer
  is written into (`Core/Bot/RunOrchestrator.cs:481`), what displaced gear falls
  back to (`Domain/Equipment/EquipMove.cs:64`, `:84`), and what the debug menu
  grants into (`Core/DebugMenuController.cs:152`).
- `RunSnapshot.inventory` is **vestigial** — `RunManager.cs:222` says so in as
  many words, and the only reader is a stats readout
  (`Core/RunStatsController.cs:135-138`).

`stockpiledItems` is cleared by `EndRun` (`Data/SaveData.cs:324`; the move out
of `StartRun` is AUDIT #8, `AUDIT.md:176`), so despite living on the profile it
is already run-scoped in practice. Sell from it.

`InventoryOps` (`Domain/Inventory/InventoryOps.cs:29-113`) already has
everything the sell flow needs, including the stacking key that matters here:
stacks are keyed on `(itemId, plus, modifierIds, riftTier)`, so two copies at
different plus are different objects and must be priced separately.
`TryRemoveAt` (`:113`) removes exactly one identified copy — that is the sell
call.

### F8. Relics have no slot cap, the pool is already weighted, and the draft screen already pages

Four things the relic section does not have to invent:

1. `RunSnapshot.relicIds` is a list and its comment states the design:
   "infinite slots per run — mid-run relic rewards from elites or bosses drop
   straight in here with no shape change" (`Data/RunSnapshot.cs:108-112`).
   AUDIT #50 (`AUDIT.md:1242`) and #51 (`:1273`) both confirm there is no cap
   anywhere to lift. So "buying a relic appends to `relicIds`" is not an
   assumption that needs defending; it is the only shape available.
2. `RunOrchestrator.RelicDraftOffer` (`RunOrchestrator.cs:68-107`) already
   filters out already-held relics (`:84-86`) and already draws weighted.
3. `RelicPool.WeightOf` (`Domain/Relics/RelicPool.cs:95-108`) is the rarity
   curve: Common 100, Uncommon 45, Rare 18, UltraRare 6, Mythic 2. Content
   today is 18 relics — 9 common, 3 uncommon, 1 rare, 4 ultrarare, 1 godlike.
4. `RelicDraftController` already **pages** (`:76`, `:129-149`), built for the
   level-70 track reward that shows the whole pool. The "many many relics
   later" scaling requirement is solved by machinery that exists.

The relic section is `DraftWeighted` with a price attached and a purse check.

### F9. Stock must be keyed to the node, and `RngStreams` needs a new number

`RoomResolver.cs:43-45` opens `RngStreams.Treasure` against
`(run.runSeed, run.step, run.currentNodeId)`, and its comment says exactly why:
"re-entering the same treasure room after a quit finds the same stash rather
than rerolling for a better one". A shop wants the identical property and for
the identical reason — otherwise quitting is a free reroll.

`RngStreams` reserves `Leg=1, Boss=2, Fight=3, Treasure=4`
(`Domain/Rng/RngStreams.cs:29-32`) and warns: "Changing one of these renumbers
every run that was ever seeded — treat them as a serialized format. New streams
take a new number." **Shop = 5.**

`Derive(runSeed, stream, a, b)` takes only two position inputs
(`RngStreams.cs:44`), and the shop needs three: step, node, reroll index.
**Add the third input. Do not pack it.**

Packing (`b = nodeId * 64 + rerollIndex`) was this plan's original
recommendation and it is the worse of the two. It makes two independent
coordinates share one field, so the day either one grows a bound — more than 64
node ids in a step, or a reroll counter that stops being per-node — the packing
silently aliases two different positions onto one seed. An aliased seed is
invisible: nothing throws, nothing logs, and the only symptom is a player
seeing the same stock twice and having no way to say so.

The extension is free, and *bit-for-bit* free, which is what makes it safe
against the file's own "treat them as a serialized format" warning:

```
public static ulong Derive(ulong runSeed, uint stream, int a, int b = 0, int c = 0)
{
    ulong z = runSeed;
    z ^= stream * 0x9E3779B97F4A7C15UL;
    z ^= unchecked((ulong)(long)a) * 0xBF58476D1CE4E5B9UL;
    z ^= unchecked((ulong)(long)b) * 0x94D049BB133111EBUL;
    z ^= unchecked((ulong)(long)c) * 0xD6E8FEB86659FD93UL;   // new line
    ...
}
```

`c` defaults to 0, `0 * K` is 0, and `z ^ 0` is `z`. Every existing caller
therefore derives the identical seed it derives today — Leg, Boss, Fight and
Treasure are all unmoved, and no run that was ever seeded is renumbered, which
is precisely what the header demands. `Open` takes the same default. Pin the
property rather than reasoning about it: a test asserting
`Derive(s, Fight, 3, 7) == Derive(s, Fight, 3, 7, 0)` across a spread of seeds
fails the moment someone "tidies" the new line into the mix instead of leaving
it as an XOR.

The shop's three coordinates are `(run.step, run.currentNodeId,
run.shopRerollsUsed)`.

### F10. The Shop is a nested screen inside Map, not a new scene — and that means the screenshot tool cannot see it

`ScreenRegistry.All` is five screens, one scene each
(`ScreenRegistry.cs:42-61`). Everything else is a sub-tree: `RelicDraftScreen`
is `HubScreen.Draft` (`Domain/UiKit/Screens/HubScreen.cs:53`, built at `:150`),
`SystemMenuScreen` and `GlossaryScreen` likewise.

The shop is entered from the map, so `ShopScreen` nests in `MapScreen` exactly
as the draft nests in the hub. No new `ScenePath`, no new entry in
`ScreenRegistry.All`, no sixth scene.

The cost: `tools/screenshot.ps1 -Panel <name>` derives its panel list from
`ScreenRegistry` (`tools/screenshot.ps1:26-33`, and the whole point of AUDIT
#43 at `AUDIT.md:951`), so a nested screen cannot be captured by name. The
relic draft has the same gap today. Not worth a scene to fix; worth knowing
before someone asks for a screenshot.

Layout is still audited: `UiFrames.All` re-solves every screen at 1920×1080,
2580×1080 (21:9), 1920×1440 (4:3) and 1920×1200 (16:10)
(`Domain/UiKit/UiFrames.cs:16-22`), and a nested tree is part of the tree it
nests in.

---

## 1. The spell-book system

**Revised 2026-09-02 against author decision 1** — "spells can be learned from
the books, so the mud blast and frost flare etc." A book teaches an *existing*
authored spell: one of the five `characterId: "sheep"` entries in
`skills.json` that cost mana — `mud_burst`, `static_fleece`, `frost_flare`,
`lightning_bolt`, `golden_fleece` — and any future entry authored the same way.
This whole section is rewritten from the original text, which read "the
standard loadout disappears" as pointing at `spells.json`'s auto-scaling
ladder (F4); it does not. `spells.json` stays exactly as it is.

### 1a. Content: option (a) — no new content type, a flag on the existing skill

Two shapes were on the table:

**(a) No new content type.** A book is a reference to a skill id. Drop/sell it
as `{skillId}`. Add `bookOnly: true` to the skill entry, replacing its
`unlockLevel` as the gate: `bookOnly` skills are never reached by levelling,
only by being learned. **Chosen.**

**(b) A thin `spellbooks.json`** with `{id, skillId, tier, price}` rows,
carrying pricing/rarity metadata a skill entry shouldn't have to.

**(a), for three reasons:**

1. **There is nothing left for a second content type to hold.** F4 (revised)
   already established that the only per-cast field a "book" needs beyond what
   `SkillDefinition` has is a price band, and F9/§2b already solve "price by
   tier" generically for every other sold thing (gear, relics) as a pure
   function of a tier number — a book needs the same one number, not a whole
   asset. Reason 3 of the original (b)-shaped decision — "a book resolves into
   a `ResolvedSkill` … plus a price tier and a `ScalingProfile`" — no longer
   holds: F4 (revised) shows the scaling comes for free from the skill's own
   `scalingAxis`, so there is no `ScalingProfile` to carry and nothing left
   that only a new type could hold.
2. **A skill entry already carries the "granted rather than earned" idiom.**
   `unlockLevel: 999` plus a route into `AvailableSkillsFor` other than the
   level check is the existing convention for Provoke, Headbutt, Black Ram
   Mode and friends (`ContentDatabase.cs:230-240`, `TalentGrantedSkillsFor`).
   `bookOnly: true` is the same idiom with a fourth route, not a new one.
3. **(b)'s own case for a new type — "a book is not a possession, not a
   character's authored kit" — is still true, but it is an argument against
   putting a book in `stockpiledItems` or gating it by `characterId`+
   `unlockLevel`, not an argument for a second ScriptableObject asset per
   spell.** `bookOnly: true` on the existing `SkillDefinition` satisfies both
   halves: it is excluded from the level ladder (not "a character's authored
   kit" in the levelling sense) and it never touches the bag (the shop and the
   drop roll reference it by `skillId`, never write it to `stockpiledItems`).

**The five `skills.json` entries that change, exactly.** `bookOnly` is the
sentinel and it is authoritative; `unlockLevel` is **absent** on a book-only
skill, and the content validator refuses an entry carrying both. The book tier
lives in content beside it as `bookTier`, not in a switch statement in
`ShopPricing` — it is a per-skill authored number, and the one place a per-skill
authored number belongs is the skill entry.

| id | today | after Phase E |
|---|---|---|
| `mud_burst` | `unlockLevel: 3` | `unlockLevel` removed, `bookOnly: true`, `bookTier: 1` |
| `static_fleece` | `unlockLevel: 4` | `unlockLevel` removed, `bookOnly: true`, `bookTier: 2` |
| `frost_flare` | `unlockLevel: 5` | `unlockLevel` removed, `bookOnly: true`, `bookTier: 2` |
| `lightning_bolt` | `unlockLevel: 7` | `unlockLevel` removed, `bookOnly: true`, `bookTier: 3` |
| `golden_fleece` | `unlockLevel: 8` | `unlockLevel` removed, `bookOnly: true`, `bookTier: 4` |

In Phase A these five gain `bookTier` and keep their `unlockLevel`; only
Phase E strikes the level out (§4). `bookTier` is authored from the start
precisely so Phase E moves no price.

**The 999 sentinel this replaces was load-bearing in three places, and dropping
it costs one change in each.** This is the part the original text got wrong by
assuming `999` and `bookOnly` were interchangeable:

1. **The resolver defaults an absent `unlockLevel` to 1, not to "none".**
   `RawSkillEntry.unlockLevel` initialises to `-1`
   (`Domain/Content/RawSkillEntry.cs:24`) and `SkillEntryResolver.cs:104` reads
   `raw.unlockLevel >= 0 ? raw.unlockLevel : DefaultUnlockLevel`, with
   `DefaultUnlockLevel = 1` (`:22`). So simply deleting the field from the JSON
   makes a book-only spell available to a **level-1 character by the level
   route** — the exact opposite of the intent, silently. The resolver must
   branch on `bookOnly` before that default: a `bookOnly` entry with
   `raw.unlockLevel >= 0` is the content error the decision asks for, and a
   `bookOnly` entry without one resolves to `int.MaxValue` rather than to 1.
2. **`AvailableSkillsFor` sorts on `unlockLevel`.** `ContentDatabase.cs:249`
   is `.OrderBy(s => s.unlockLevel).ThenBy(s => s.sortOrder)`, and "your kit,
   then what you learned this run" only falls out of that sort while a book
   spell's level is *high*. Resolving to `int.MaxValue` (point 1) keeps the
   ordering for free; resolving to 0 would put learned spells first.
3. **Do not lean on `KitFor(CharacterDefinition, …)`'s level filter to exclude
   book spells.** This plan previously claimed "its existing
   `s.unlockLevel <= level` filter already rejects `999`". At `b7adb83` that
   filter did not exist: the definition-only overload was
   `.Where(s => s.characterId == definition.id).OrderBy(s => s.sortOrder)` and
   nothing else, so it handed a level-1 kit **every** skill the character
   owned, 999s included. A change in the tree at the time of writing (another
   session, `Core/FightEncounterAdapter.cs:561-565`) adds
   `s.playerSelectable && s.characterId == definition.id && s.unlockLevel <= level`,
   which fixes that on its own terms. Either way the conclusion for this plan
   is the same and it is the reason point 1 matters: a `bookOnly` skill whose
   `unlockLevel` is merely *absent* resolves to **1**, sails through that
   filter, and lands in a level-1 kit. What excludes it is the resolver
   answering `int.MaxValue` for a book-only skill, not the filter. Belt and
   braces: add `&& !s.bookOnly` here too, because a filter that reads as
   "levelled skills only" should say so rather than depend on a sentinel two
   files away.

`ContentDatabase.Validation.cs:404-407`'s "unlocks at level N; characters start
at level 1" check gains the same `bookOnly` carve-out, for the reason its own
neighbouring comment gives: content is checked at authoring time **and** at
load time, and both copies of a rule move together or one of them starts
lying.

**`ContentDatabase.AvailableSkillsFor` (`Core/Content/ContentDatabase.cs:213-252`)
gains a fourth route**, beside `unlockLevel <= character.level`,
`unlockedSkillIds.Contains`, and talent-granted:

```
|| (s.bookOnly && run.learnedSpells.Any(e => e.characterId == character.definitionId && e.skillId == s.id))
```

`FightEncounterAdapter.KitFor(CharacterDefinition, level)` — the no-run,
definition-only overload used by direct scene loads and tests (`:549-568`) —
has no `run` to ask, and it does **not** exclude book spells by construction.
It needs an explicit `&& !s.bookOnly`; §1a point 3 has the detail, including
why its level filter (whether or not one is present when this is built) cannot
be relied on to do the job.

`headbutt`, `shear`, `battering_ram`, `fleece_ward` (Ward) and `woolgathering`
(Woolgathering) are ordinary skills today and stay ordinary skills — **none of
the five is `bookOnly` and none is touched by this section.** Enemy skills
(`boulder_slam`, `roar`, `grapple`, `shell_up`, `barrel_roll`, `trunk_slam`,
`spore_cloud`, `bog_mud_burst`) are `playerSelectable: false` already and are
likewise unaffected.

### 1b. Data: three slots per character, on the run

```
RunSnapshot:
    public List<LearnedSpellEntry> learnedSpells = new List<LearnedSpellEntry>();

[Serializable] class LearnedSpellEntry { public string characterId; public string skillId; public int slot; }
```

`skillId`, not `bookId` — under decision 1 there is no separate book
identity, only the id of the `bookOnly` skill being learned. A flat list of
`{characterId, skillId, slot}`, not a dictionary — same reason `RunHealthEntry`
is a list entry (`Data/RunSnapshot.cs:9-14`): `JsonUtility` serializes fields
and `Dictionary` is not one of the shapes it can write.

`MaxSpellSlots = 3`, a constant in Domain beside the entry type. Purely
additive, so `SaveData.CurrentVersion` does not move — an older save's
in-flight run has no such field, `JsonUtility` leaves the list empty, and
"empty" is exactly "has learned nothing", which is the correct reading.

Nothing carries over: `StartRun` replaces the whole snapshot
(`Core/RunManager.cs:135`), which is the same mechanism that already resets
`relicIds` and `offerRerollsUsed` (`RunSnapshot.cs:112`, `:129-131`).

`SaveData.Reconcile` must drop entries whose `skillId` no longer resolves to a
`bookOnly` skill, the same tolerant treatment `stockpiledItems` gets at
`Data/SaveData.cs:405` — but **dropping, not deleting silently into a void**: a
learned spell that vanished because content was edited under a live run should
free its slot, which is what dropping the entry does.

### 1c. How a learned spell reaches the kit

No separate insertion point is needed. §1a's fourth `AvailableSkillsFor` route
*is* the mechanism — `FightEncounterAdapter.KitFor(Character, ...)`
(`Core/FightEncounterAdapter.cs:610-621`) already builds `skills` from
`ContentDatabase.AvailableSkillsFor(character)` and passes it straight to
`new PlayerKit(...)`; nothing at that call site changes. This is smaller than
the original plan, which had the adapter append a separately-resolved book
list after the authored skills — that extra step is gone because a learned
spell is not a separate kind of thing to resolve, it is a `SkillDefinition`
that has become reachable.

The `basicSpell` argument at `:621` still becomes `null` and then goes away
with the parameter — that part is F4 (revised), unrelated to how a learned
spell reaches the kit.

### 1d. Learn / replace

```
LearnSpell(characterId, skillId):
    entries = learnedSpells where characterId matches
    if entries.Count < 3     -> append at the lowest free slot index
    else                     -> caller must supply a slot to overwrite
```

Two calls, not one: `CanLearn(characterId)` returns the free slot or -1, and
`LearnSpell(characterId, skillId, slot)` writes. The screen asks first, shows
the replace picker when the answer is -1, and calls with the chosen slot. The
bot policy answers the same question without a screen.

Refuse a duplicate: learning a spell already in one of that character's three
slots is a no-op that returns false, and the shop card reads OWNED for that
character. Not a hard error — content can change under a run — but not a silent
success either.

### 1e. Drops

Spells drop from won fights, alongside the existing consumable roll.

`VictoryRewards.RollConsumableDrops` (`Domain/Combat/Session/VictoryRewards.cs:76`)
is the model: one roll per enemy, in roster order, returning `(enemy, itemId)`
pairs rather than mutating anything, "which is what makes a seeded run
reproduce its own loot". Add `RollSpellDrop` beside it with the same shape —
pure, injected randomness, drawing from `ContentDatabase.Skills` filtered to
`bookOnly`, returning a skill id or null.

Rate: **not** `ItemDropChance = 0.3f` (`VictoryRewards.cs:30`), which is
per-enemy and would hand out several spells a leg. A spell is a third of a
character's whole loadout for the run. Start at one roll per *fight*, not per
enemy, at `0.10` normal / `0.20` elite / `0.35` boss — roughly one spell per
leg, which fills three slots by the end of leg 3 and leaves the shop as the way
to get them sooner or better. Put it in `VictoryRewards` as named constants so
the next balance batch can move them without hunting.

Do **not** put spells into `ItemOfferRoll` (`Core/ItemOfferRoll.cs`). Its
`Candidates()` is documented as equippables-only for a reason worth keeping:
"a 'choose one of three' that can offer a health potion is not a choice, it is
a tax on the one player who reads carefully" (`:30-35`). A spell in the gear
offer is that same tax with a different noun. A dropped spell is an event of
its own, shown after the offer.

### 1f. A character with no learned spells

House style is graceful degradation. Under F4 (revised), the honest version of
this section is smaller than it was: the sheep is **never** without a Skill
verb, because `shear` (`unlockLevel: 1`), `woolgathering` (`unlockLevel: 2`)
and `battering_ram` (`unlockLevel: 6`) are ordinary, non-`bookOnly` skills that
still unlock by level. A fresh, book-less sheep still has `shear` from turn
one. **What actually disappears at run start is the automatic, nameless
BasicSpell action** (F4) — "no free spell at all" means no free *named*
spell beyond the authored kit above, not an empty Skill submenu.

The general rule from the original plan still holds, for the case the sheep
itself mostly avoids and any future character might not:

1. If a character's Skill submenu were ever genuinely empty, the Skill verb
   must be **shown and disabled**, not hidden, with the reason on it ("NO
   SPELLS LEARNED"). The submenu's own rule already says unaffordable rows are
   included and dimmed rather than removed, "the player should learn what they
   have rather than watch the list change length" (`FightHudModel.cs:100-103`)
   — a verb that vanishes is the same defect one level up.
2. Every bot policy that scored a `BasicSpell` action must tolerate an actor
   whose `Skills` list happens to be empty. `FightAction.cs:114-122` already
   guards on `KitFor(actor)?.BasicSpell != null` before offering that action;
   removing the `BasicSpell` kind (F4) removes the guard's subject too — but
   `GreedyAggressivePolicy.cs:45` and `GreedyDefensivePolicy.cs:149` filter
   action lists that could now be attack-only for a kitless actor in principle.
   Pin it with a test: a kit with no skills produces a legal action list and a
   policy picks from it.

**What the character sheet shows:** the 3-slot strip per character (`docs/handoffs/shop_v2/README.md`
§3, "Row A") reads `learnedSpells` directly — `EMPTY` for an unfilled slot,
the spell's display name for a filled one. It is unaffected by whether the
character's ordinary Skill submenu happens to be empty; the two are different
lists (`AvailableSkillsFor`'s full result vs. the run's `learnedSpells`).

---

## 2. The shop

**Per stock roll: 3 spell cards, 4 item cards, 3 relic cards** (§2d). The
designer README's card counts (`docs/handoffs/shop_v2/README.md` §3, Row A/B/C)
match: 3 spell cards, 4 item cards, 3 relic cards.

### 2a. What a normal fight is actually worth — the price anchor

Measured off `reports/bot/20260902-020048` (12,000 runs; `PayoutGold` per fight
from `traces.jsonl`, medians over **won** fights only, since a lost fight pays
zero and would drag the Fresh medians to 0 at every boss):

| Profile | Room | steps 1-8 | 9-16 | 17-24 | 25-40 |
|---|---|---|---|---|---|
| Fresh | Fight | 16 | 22 | 20 | 14 |
| Fresh | Elite | 31 | 37 | 53 | 44 |
| Fresh | Boss | 68 | 60 | 76 | 60 |
| Mid | Fight | 16 | 22 | 22 | 22 |
| Mid | Elite | 31 | 41 | 44 | 44 |
| Mid | Boss | 68 | 60 | 76 | 76 |
| Late | Fight | 19 | 24 | 24 | 24 |
| Late | Elite | 33 | 44 | 50 | 50 |
| Late | Boss | 76 | 76 | 82 | 82 |

Gold earned per step descended (run total won-gold ÷ deepest step):

| Profile | n | median | p25 / p75 | median run total | median depth |
|---|---|---|---|---|---|
| Fresh | 4000 | **10.12** | 7.62 / 13.25 | 73 | 8 |
| Mid | 4000 | **18.94** | 15.53 / 22.65 | 531 | 28 |
| Late | 4000 | **21.19** | 17.72 / 26.41 | 740 | 40 |

Median cumulative won-gold at depth, among runs that reached it:

| Profile | S4 | S8 | S12 | S16 | S24 | S32 | S40 |
|---|---|---|---|---|---|---|---|
| Fresh | 51 | 93 | 197 | 267 | 435 | 660 | 869 |
| Mid | 57 | 147 | 224 | 318 | 515 | 704 | 892 |
| Late | 61 | 153 | 239 | 335 | 524 | 732 | 944 |

**The finding that shapes the pricing: income is flat with depth, and tiers are
not.** A normal fight pays 16 in leg 1 and 22-24 forever after; a leg is worth
about 150 gold in leg 1 and about 185 in every leg after. Meanwhile
`RarityTable.StepsPerTier = 8` (`Domain/Rewards/RarityTable.cs:40`) means the
expected item tier climbs by one *every leg*.

**Revised 2026-09-02 against author decision 2** — "price should be a
calculation based on tier and the + mod, regardless of floor." The formula in
§2b already is: `BaseFor(tier)`, `PlusFactor(plus)` and `RiftFactor(riftTier)`
take no depth or floor argument, and neither does `SpellBookPrice(tier)` or
`RelicPrice(rarity)`. Price was never actually a function of depth in this
plan — only **stock** is, through `RarityTable.FloorTier(step)` picking which
tiers get offered. Those are two different rules and the original prose above
blurred them by arguing from "purchasing power" as if it were a pricing
decision still open to debate. It is not a decision: it is the plain
arithmetic consequence of a tier-priced formula meeting flat-with-depth income
(§2a's own numbers), stated here as an observation rather than a pushback
paragraph —

**A tier-matched item costs about one normal fight's payout in leg 1 and
progressively more of one in later legs, purely because income per fight stops
climbing around step 8-16 while the tier on offer keeps climbing every leg.**
Nothing about the pricing formula needs to change to make that true, and
nothing about it should change to make it false — a depth term in the price
would double-count what `FloorTier` already does on the stock side. **Price is
tier-and-plus-and-rift, full stop; stock-tier-by-depth is a separate, stock-only
rule (§2c/§2d).**

### 2b. Pricing formulas

```
BaseFor(tier)          = 20 + 4 * tier                     // gear
PlusFactor(plus)       = 1.00 + 0.35 * plus                // 0..5
RiftFactor(riftTier)   = 1.00 + 0.50 * (int)riftTier       // 0..3 affix slots
GearPrice              = round( BaseFor(tier) * PlusFactor * RiftFactor )

ConsumablePrice        = ItemDefinition.cost               // authored, 15 today

SpellBookPrice(skill)  = { 1: 70, 2: 95, 3: 120, 4: 145 }[skill.bookTier]

RelicPrice(rarity)     = { Common 60, Uncommon 110, Rare 190, UltraRare 300, Mythic 460 }

SellPrice(x)           = max(1, round(0.30 * price(x)))

RerollPrice(n)         = min(25 * 2^n, RerollCeiling)      // n = run.shopRerollsUsed
RerollCeiling          = 9999
```

Every constant lives in one Domain class (`Domain/Rewards/ShopPricing.cs`),
pure and engine-free like the rest of `Domain/Rewards/`, so the whole table is
unit-testable without a scene — and **pinned with literal expected values**,
never recomputed from the formula, per CLAUDE.md gotcha 5.

Relic prices are **flat, not depth-scaled**, deliberately: a relic bought at
step 8 has thirty-two steps left to pay off and one bought at step 40 has
nothing, so a flat price makes the early buy the good buy without a second
curve to tune.

**Spell price band, derived from today's `unlockLevel`.** `skills.json` has no
`tier` field for a skill, so `SpellBookPrice` needs one manufactured from the
one number that already exists per spell — its (former, pre-`bookOnly`)
`unlockLevel`. This is a first guess for the balance batch to move, not a
measured curve:

| skillId | unlockLevel (pre-decision-1) | book tier | price |
|---|---|---|---|
| `mud_burst` | 3 | 1 | 70 |
| `static_fleece` | 4 | 2 | 95 |
| `frost_flare` | 5 | 2 | 95 |
| `lightning_bolt` | 7 | 3 | 120 |
| `golden_fleece` | 8 | 4 | 145 |

Roughly two unlock-levels per book tier, `golden_fleece` pinned at the top
band since it is the sheep's ultimate (`spendsAllResource: true`,
`ignoresDefense: true`, a 5-turn cooldown). The tier-to-price mapping lives as
a four-entry lookup, not a formula, in `ShopPricing.cs` — four bands is not
enough data to fit a curve to, and a lookup is honest about that. **Which tier
a spell is in lives in content as `bookTier`** (§1a), not in a switch here:
`ShopPricing` should not have to be edited to author a sixth spell, and a
switch over skill ids in Domain is a content table wearing a compiler.

**The reroll ceiling is a display fact before it is an economy fact.**
`25 · 2^n` passes 9999 at n = 9 (12,800) and overflows a signed int at n = 27.
Nothing in the run stops a player pressing the button, so the price has to stop
growing somewhere the label can still print it — `UiTextFitAudit`
(`SceneBuilder.cs:105`) sizes the reroll button for four digits plus a suffix,
and a five-digit price fails that audit rather than merely looking wrong.
Saturating at 9999 costs nothing in play: it is roughly eleven legs of income
at the measured ~185 a leg (§2a), which is more than a 40-step run earns in
total. **Past the ceiling the button stays visible and unaffordable** rather
than disappearing, for the same reason the skill submenu keeps unaffordable
rows — "the player should learn what they have rather than watch the list
change length" (`FightHudModel.cs:100-103`).

`n` is `RunSnapshot.shopRerollsUsed`, and it is **per node** rather than per
visit. Those describe the same quantity today (a shop clears on leave and
cannot be re-entered, §5.6), but naming it after the node is what makes the
price and the RNG's third coordinate (F9) read the same number instead of two
numbers that agree by coincidence.

**Canonicalise modifier ids — sorted, ordinal — before pricing and before
comparing.** The bag half of this is already done and needs nothing:
`InventoryOps.ModifiersMatch` compares `modifierIds` as an unordered SET
(`Domain/Inventory/InventoryOps.cs:33-38`), so two stacks whose affixes rolled
in different orders already merge correctly. Pricing is the half that is not
done. A price or a card identity keyed off a `string.Join` of whatever order
`ModifierTable.PickModifiers` returned would give two identical items two
answers — and `ShopStockEntry.modifiers` is **persisted** (§2e), so that
incidental order would be frozen into the save and outlive the roll that
produced it. Sort ordinally when the stock entry is built. One line, and it
makes the entry's identity canonical rather than accidental.

**`sell(x) < buy(x)` for every rollable item, pinned by a test over the whole
table.** At 30% the inequality is uninteresting above about 3 gold, and the
`max(1, …)` floor is exactly where it stops being uninteresting: an item priced
at 1 would sell for 1, and buy-then-sell would be free rather than lossy. That
is not an exploit — it gains nothing — but it is the *shape* of one, and the
shape is what a test should refuse before a constant moves and makes it real.
Sweep `ContentDatabase.Offerable` × plus 0..5 × riftTier 0..3 and assert
`SellPrice(x) < GearPrice(x)` in every cell, plus the consumable row, the four
book tiers and the five relic rarities. This does not violate CLAUDE.md gotcha
5: it asserts an **invariant** (`<`) rather than recomputing the formula to
check the formula's own output. The arithmetic itself stays pinned by §2c's
literal table.

### 2c. The price table, against measured income

Three depths. Shop stock is tier-banded to the depth via
`RarityTable.FloorTier(step) = step / 8` (`RarityTable.cs:40-47`), so the
"tier-matched" row is the common case and the "+1 tier" row is the stretch.

| | **step 8** (tier 1) | **step 24** (tier 3) | **step 40** (tier 5) |
|---|---|---|---|
| *normal fight pays* | 16-19 | 22-24 | 22-24 |
| *elite pays* | 31-33 | 44-50 | 44-50 |
| *boss pays* | 68-76 | 76-82 | 76-82 |
| *a leg is worth* | ~150 | ~190 | ~190 |
| Common gear, tier-matched, +0, no affix | **24** | **32** | **40** |
| Same, +1 | 32 | 43 | 54 |
| Same, +2 | 41 | 54 | 68 |
| One tier up, +0 | 28 | 36 | 44 |
| Rare: tier-matched, +2, 1 affix | **61** | **81** | **102** |
| Very rare: +3, 2 affixes | 82 | 109 | 136 |
| Health potion | 15 | 15 | 15 |
| Spell, `mud_burst` (T1) | 70 | 70 | 70 |
| Spell, `static_fleece`/`frost_flare` (T2) | 95 | 95 | 95 |
| Spell, `lightning_bolt` (T3) | 120 | 120 | 120 |
| Spell, `golden_fleece` (T4) | 145 | 145 | 145 |
| Relic, Common | 60 | 60 | 60 |
| Relic, Rare | 190 | 190 | 190 |
| Relic, Mythic | 460 | 460 | 460 |
| Reroll 1 / 2 / 3 | 25 / 50 / 100 | 25 / 50 / 100 | 25 / 50 / 100 |

Every row here is priced by tier/plus/rift alone (§2a, revised) — the three
columns differ only because a deeper shop *stocks* a higher tier, never
because depth enters the formula. Reading the anchors: at step 8 a
tier-matched common is **24** against a 16-19 fight and a **61** rare against a
68-76 boss — both land. At step 40 the tier-matched common is 40 against a
22-24 fight, so it costs somewhat more than one fight's payout — an observed
consequence of flat income meeting a climbing tier band, not a lever this plan
pulls. A Mythic relic at 460 is about 2.5 legs of saving: a run-defining
purchase, reachable but never casual. Spell prices are flat across all three
columns by construction (§2b) — a spell is worth the same whenever it is
bought.

Sell prices at 30%: a tier-matched common sells for 7 / 10 / 12; a +3
two-affix piece for 25 / 33 / 41. Selling the whole bag never funds a relic,
which is the "not supposed to make you rich" instruction holding.

**Sell is bag-only. Worn gear cannot be sold — an intentional restriction,
not an omission.** The review read §5.4's "unequip on the character sheet
first" as contradicting §8's "no equipping from the shop", and it would, except
that the sheet is reached from the **map** and not from inside the shop. This
plan never said so. Saying it, as the path a player actually walks:

1. Buying a gear card puts the item in the bag. It is **not** equipped, and the
   shop offers no way to equip it.
2. `LEAVE` returns to the map. The character dossier is reachable from there,
   as it is from anywhere on the map.
3. Equipping the new piece through the dossier displaces whatever it replaces
   **into the bag**, atomically, carrying its own plus and affixes —
   `EquipMove.TryEquip` already does exactly that
   (`Domain/Equipment/EquipMove.cs:64`, `:84`), and `EquipmentOps.Equip`
   (`Core/EquipmentOps.cs:43-69`) is the save-side half that rescales carried
   health around the swap.
4. The displaced piece is now bag stock and sells at the **next** shop.

So nothing is lost, it is banked one shop later. That delay is the entire cost
of the restriction, and it is small against what the alternative costs: a shop
that sells off the body needs the whole equip/displace rule set inside it, and
`EquipMove`'s own convention is that "every method does both halves of the move
or neither" — a sell that half-performs an unequip is precisely the quiet
item-destroying bug that rule exists to prevent.

**No dossier access from inside the shop in v1.** The shop is a nested panel
over the map (F10); so is the dossier. Stacking one over the other means an
overlay stack, a second focus owner, and a bag that can change underneath the
sell list while the sell list is on screen. All three are real work, none of it
is about shopping, and the map is two presses away. Recorded in §6 as the
obvious convenience to add second.

**A shop purchase does not auto-equip, and that is a deliberate divergence from
the reward path rather than a restatement of it.**
`RunOrchestrator.TakeOffer` **does** auto-equip a taken offer into an *empty*
slot (`Core/Bot/RunOrchestrator.cs:474-499`), on the stated reasoning that a
piece filling a hole in the loadout carries no decision. A purchase is a
different act: the player already decided, with money, against three other
cards, and having the game then place the item is a second decision taken on
their behalf at the moment they are most likely to be mid-plan. So a shop buy
calls `InventoryOps.Add` and stops. Worth stating outright because `TakeOffer`
looks like the obvious method to reuse and is the wrong one.

### 2d. Stock rolling

```
stream = RngStreams.Shop (= 5)
rng    = RngStreams.Open(run.runSeed, RngStreams.Shop, run.step, run.currentNodeId * 64 + rerollIndex)
```

Per F9. Same node, same stock, forever — quitting mid-visit and returning finds
the shop as it was, and re-entering a cleared shop is impossible because the
room clears on leave.

What it rolls, in one pass:

| Section | Count | Source |
|---|---|---|
| Spells | 3 | weighted over `ContentDatabase.Skills` filtered to `bookOnly`, excluding a spell already learned by every fielded character (§1a/§4 "Spell card — Owned by everyone") |
| Items | 4 | `ItemOfferRoll.Candidates()` through `ItemOfferTable.Choose(candidates, FloorTier(step), MaxTier, next, 4)`, then `RarityTable.RollPlus` + `ModifierTable.RollRiftTier`/`PickModifiers` per item — the identical three-axis roll `ItemOfferRoll.Roll` does at `Core/ItemOfferRoll.cs:154-168`, with `count` 4 |
| Relics | 3 | `RelicPool.DraftWeighted(available, next, 3)` with `available` = `RelicPool.Available` minus `run.relicIds`, exactly as `RunOrchestrator.RelicDraftOffer:84-86` |
| Sell | — | not rolled; it is `save.stockpiledItems` |

Extract the per-item three-axis roll out of `ItemOfferRoll.Roll` into a
`RollOne(offer, encounter, favor, next)` helper so the shop and the reward
screen cannot drift on what "one rolled copy" means. That is one seam with two
callers, the same shape F2 of `PLAN_BALANCE_BOT.md` argues for.

Encounter class for the roll: `EncounterClass.Normal`. A shop is not a fight;
using Elite or Boss would make browsing better than winning.

**The counts are constants, not layout.** `ShopStock.SpellCount = 3`,
`ShopStock.ItemCount = 4`, `ShopStock.RelicCount = 3`, in the same Domain class
as the roll. The screen emits exactly that many cards per section at build time
(§3a) and `ScreenDef.CountBindings` pairs each array with its constant so the
two cannot drift (`ScreenRegistry.cs:24-28`).

**There is no relic pager.** The designer brief carried a `1 / 6` pager, copied
from `RelicDraftController`'s paging — which exists *there* because the
level-70 track reward shows the whole relic pool, and a pool is unbounded.
A shop section shows `RelicCount` cards drawn from a pool it never displays.
Paging becomes a question the day a count constant exceeds the number of cards
the layout can show, and not one day sooner; at that point it is the same
`Domain/UiKit/Paging.cs` helper the draft already uses, added then. The pixels
the pager occupied go back into the layout (README §3).

**No duplicate `contentId` within one roll.** Each section draws without
replacement. `RelicPool.DraftWeighted` already does this — it takes an
`available` list and removes as it draws — and the item and spell draws must
match it. The same card twice at the same price is not variety; it reads as a
broken roll, and it is one.

**An undersized pool renders `NO OFFER`; it never shrinks the row.** After
excluding held relics, spells every fielded character already knows, and spells
with no eligible recipient at all (below), a pool can hold fewer candidates
than its section has cards — the relic pool is 18 today and a deep run holds
several of them. The remaining cards render a `NO OFFER` placeholder **in
place**: never hidden, never duplicated to pad the row, and the row does not
re-centre. That is the same rule sold-out cards already follow (§2e) and it has
the same reason — the screen binds cards by index, and a row that re-centres
moves every card the player was already looking at.

**Books with no eligible fielded recipient are filtered out at roll time.** A
book that every fielded character has already learned cannot be bought by
anyone, so offering it is a dead card taking a live card's slot; the roll drops
it, exactly as the relic draft drops a held relic
(`RunOrchestrator.cs:84-86`). "Fielded" is `SaveData.ActiveSquad`, capped at
`EffectiveMaxSquadSize()` (`Data/SaveData.cs:163-166`) — **two** today, see
§3e. A book known by *some* fielded character is still offered, carrying the
ownership label of §4 / README §4.

### 2e. Persistence

Stock state on `RunSnapshot`, purely additive:

```
public List<ShopStockEntry> shopStock = new List<ShopStockEntry>();
public int shopRerollsUsed;         // at shopNodeId. The reroll price's n AND the RNG's third coordinate
public int shopNodeId = -1;         // which node this stock belongs to
public int shopStockVersion;        // the generator revision that produced it
```

```
public enum ShopEntryKind { Gear, Spell, Relic }

[Serializable]
public class ShopStockEntry
{
    public ShopEntryKind kind;       // exactly one; the other kinds' fields are unread
    public string contentId;         // itemId | skillId | relicId, per kind
    public int plus;                 // Gear only
    public List<string> modifiers;   // Gear only, ORDINAL-SORTED (§2b)
    public int riftTier;             // Gear only
    public int price;                // resolved at roll time and PERSISTED
    public bool sold;
    public int section;              // 0 spells, 1 items, 2 relics
    public int index;                // position within the section
}
```

**One `kind` per entry, and the fields for the other kinds are simply not
read.** A discriminated union is not a shape `JsonUtility` can serialise —
same constraint that made `learnedSpells` a flat list rather than a dictionary
(§1b) — so `kind` is the discriminator and the rule is enforced by the
constructor and a validator, not by the type system.

**`price` is resolved at roll time and stored.** Prices are a pure function of
tier, plus and rift (§2a), which makes recomputing them on load *look* free —
and it is free right up until a content patch, a `ShopPricing` constant, or a
`RarityTable` change lands under an in-flight run. Then a card the player is
looking at silently changes price, or worse, becomes affordable after they
decided it was not. An open shop is a quoted price. Store it.

**`stockVersion`** is the same argument one level up. It records which
generator produced this list. If a later build changes the roll's shape — a
new section, a different count, a different candidate filter — an open shop
from an older version is **not** rewritten; it is left exactly as it was rolled
and the new generator applies at the next node. A shop that reshuffles itself
because the game updated is a free reroll granted by a patch note.

**Reconciliation on load, per entry, tolerant.** `SaveData.Reconcile` already
takes this posture for `stockpiledItems` (`Data/SaveData.cs:405`) and for the
relic loadout (`:520-526`), and the shop gets the same treatment:

| What is broken | What happens |
|---|---|
| `contentId` no longer resolves (item, skill or relic deleted) | The card renders `NO OFFER` **in place**. Not removed: removal renumbers indices, and the screen binds by index |
| A learned spell's `skillId` no longer resolves, or is no longer `bookOnly` | The entry is dropped, freeing its slot, and the character sheet shows a one-time notice saying a learned spell was lost to a content change. Dropping silently is how a player concludes the save is corrupt |
| `shopNodeId != run.currentNodeId` | Stale stock from a previous shop. Re-roll, reset `shopRerollsUsed` to 0, set `shopNodeId` and `shopStockVersion` |
| `sold` is true on an entry whose content vanished | Stays sold. The gold was spent; the card is not a refund |

`shopNodeId` is the guard, not an optimisation: without it a stale list from a
previous shop paints the next one.

Sold-out is a flag on the entry, not a removal — a removed entry renumbers the
card indices and the screen binds cards by index, which is the class of bug
`ScreenDef.CountBindings` exists to catch (`ScreenRegistry.cs:24-28`).

**The run persists on arrival, before the shop resolves.** This is not a new
rule: `RunOrchestrator`'s own header states it — "the run persisted on arrival
BEFORE the room resolves" (`RunOrchestrator.cs:35`) — and it is why quitting
inside a shop cannot reroll it. `RunManager.MoveTo` already persists; the stock
roll must happen after that call and be persisted itself.

### 2f. Every mutation is one method, one order, one save

Six things a shop can do to a save — buy gear, buy a relic, buy a book, learn
into a free slot, replace a learned spell, sell, reroll. Each is **one**
`RunOrchestrator` method, and each has the same three-part shape:

```
1. VALIDATE, touching nothing.       resolve every id, check the purse, check the slot
2. APPLY, unable to fail.            mutate RunSnapshot then SaveData, in the order below
3. PERSIST, exactly once.            SaveSlotManager.SaveCurrent()
```

Ordering, and why each is that way round:

| Mutation | Order |
|---|---|
| Buy gear / consumable | deduct `run.gold` → mark the entry `sold` → `InventoryOps.Add` into `save.stockpiledItems` → persist |
| Buy relic | deduct `run.gold` → mark `sold` → append to `run.relicIds` → persist |
| Buy book (free slot) | deduct `run.gold` → mark `sold` → append `LearnedSpellEntry` → persist |
| Buy book (replacing) | deduct `run.gold` → mark `sold` → overwrite the chosen slot's `skillId` → persist |
| Sell | `InventoryOps.TryRemoveAt` → **only if it returned true**, credit `run.gold` → persist |
| Reroll | deduct `run.gold` → increment `shopRerollsUsed` → re-roll `shopStock` at the new third coordinate → persist |

**Gold leaves first on a buy and arrives last on a sell**, so the arithmetic
failure mode in both directions is the player being briefly poorer than they
should be rather than briefly richer. And on a sell the removal is the *only*
step that can legitimately answer "no" — `TryRemoveAt` returns false when the
exact `(itemId, plus, modifierIds, riftTier)` copy is not there
(`Domain/Inventory/InventoryOps.cs:113-121`) — so it goes first and its answer
gates the credit. A sell that credited first and then failed to remove is a
gold printer.

**One `SaveCurrent()` at the end, never one per step.** This is
`EquipmentOps`' rule, followed rather than re-argued: it "DELIBERATELY DOES NOT
PERSIST" and lets each caller decide when to write, because the sheet, the
reward path and the bot all mean different things by "done"
(`Core/EquipmentOps.cs:27-33`). A shop mutation's "done" is the whole mutation.

#### What atomicity means here, precisely — and what it does not

The review asked for a test per mutation that injects a failure at each
boundary and asserts the pre-state or the post-state, never a half state. That
is the right test, but two of the code facts it assumes are wrong and the
tests have to be built against the code that exists:

- **`SaveSystem.Save` does not throw.** It writes to a sibling `.tmp` and
  `File.Replace`s it — one filesystem operation, so a torn write cannot happen
  (AUDIT #10) — and it catches every exception, logs
  `"…could not be written… The previous save on disk, if any, is untouched."`,
  and returns (`Core/SaveSystem.cs:123-175`). So there is no throw at the
  persistence boundary to catch, and pointing `SaveSystem.RootOverride` at an
  unwritable path produces a *logged* failure, not an exception.
- **`RootOverride` is a path redirect, not a fault injector.** Its own comment
  says what it is for: a test-only escape hatch so the PlayMode suite stops
  deleting the developer's real save (`SaveSystem.cs:11-19`).

So the boundary that can actually leave a half state is **not** the disk write.
It is step 2, in memory: `SaveSlotManager.CurrentSave` hands back a cached,
live `SaveData` (`Core/SaveSlotManager.cs:35-46`), every mutation edits that
object in place, and an exception thrown midway through step 2 leaves the
cached object half-mutated whether or not anything reached the disk. Two
consequences, and they are the spec:

1. **Step 2 must be incapable of failing.** Every id is resolved, every list is
   materialised, and every refusal is decided in step 1. Step 2 is then
   assignments and `Add`/`TryRemoveAt` calls on lists that are known to exist.
   This is not a style preference; it is the only way to get atomicity out of
   a mutable cached object with no transaction around it.
2. **The tests inject at the two boundaries that exist.** A test-only
   `Func<bool>` seam on the orchestrator (defaulted to null, set and cleared in
   the test's `finally`, the same posture `RootOverride` and
   `SaveSystem.InMemory` already take) that refuses at the end of step 1
   asserts the **pre-state**: gold unchanged, entry not sold, bag unchanged,
   nothing persisted. An unwritable `RootOverride` asserts the other end: the
   in-memory state is fully mutated, the disk still holds the previous save,
   and the divergence is *logged* rather than silent. Both are one PlayMode
   test per mutation, six of each.

The honest summary for the handoff: a shop mutation is atomic against its own
sequence, and against the disk it is a single-write commit that either lands or
leaves the previous save intact. It is **not** transactional across a process
kill between step 2 and step 3 — a purchase made in the last few milliseconds
before a crash is lost along with everything else since the last write, which
is the same guarantee every other purchase, talent click and fight victory in
this game already has.

### 2g. The `RunOrchestrator` seam

```
public enum Arrival { Refused, Fight, Shop, Resolved }
```

`ArriveAt` (`RunOrchestrator.cs:169-193`) gains one branch beside the fight
branch: roll-or-restore the stock, persist, return `Arrival.Shop`, and — like
the fight branch — **do not** call `ClearCurrentRoom`. The room is cleared when
the player leaves, through a new `RunOrchestrator.LeaveShop()` that clears the
stock, resets `shopRerollsUsed`, and calls `RunManager.ClearCurrentRoom()`.

`MapController.Walk.Arrive` (`Core/MapController.Walk.cs:172`) gains
`case Arrival.Shop: OpenShop(); return;` — a nested panel, so no
`Navigation.Go`. Its `default:` arm must stop being the catch-all that swallows
a new value.

`BotRunDriver` (`Core/Bot/BotRunDriver.cs:360-382`) currently treats "not a
fight" as "continue". It gains a shop branch that calls the policy, and the
`isFight` flag at `:358` is no longer a two-way split.

Buy/sell/reroll bodies go in `RunOrchestrator` beside `TakeRelic`
(`:112-123`), not in the controller — same reason the relic commit moved there:
the bot is the second caller and a second copy is a second rulebook.

`IRunPolicy` (`Domain/Bot/IRunPolicy.cs:17-48`) gains:

```
ShopChoice ChooseShop(ShopView shop, RunView view, SeededRandom rng);
```

returning one of buy-item(i) / buy-spell(i, characterId, replaceSlot) /
buy-relic(i) / sell(bagIndex) / reroll / leave, called in a loop until it says
leave or the purse refuses. `RunView` already carries `Gold` (`Domain/Bot/RunView.cs:17`),
so the policy can already see what it can afford. Cap the loop — a policy that
never says leave is a hang, and `BotRunDriver` has no timeout for it.

---

## 3. UI

### 3a. The tree

`Domain/UiKit/Screens/ShopScreen.cs`, built by `ShopScreen.Build()`, nested
inside `MapScreen.Build()` as `screen.Shop` — the pattern
`HubScreen.cs:53`/`:150` uses for `RelicDraftScreen`.

Sections and coordinates are the designer brief's job
(`docs/handoffs/shop_v2/README.md`), not this document's. What the plan fixes:

- Every card, row and slot chip is a **fixed-count array** emitted at build
  time and hidden when unused. Scenes are generated once; a runtime count
  cannot widen a tree. This is F3 of `PLAN_PROGRESSION_TRACK.md` restated.
  The counts are `ShopStock`'s three constants (§2d), and because a section's
  cards can never outnumber its own constant there is **nothing to page** —
  the relic pager is removed (README §3).
- Every such array is declared to `UiCountAudit` through
  `ScreenDef.CountBindings` (`ScreenRegistry.cs:24-28`) — five arrays here
  (spell cards ×3, item cards ×4, relic cards ×3, sell rows, and slot chips at
  `RecipientRowCount` × `MaxSpellSlots`) — and the audit comment names the
  shipped Store bug that pairing exists to prevent.
- All copy through `UiStrings.cs`. `UiKitLintTests.LabelTextIsOnlyAssignedThroughUiText`
  fails the build on a direct `.text` assignment (`Core/RoomResolver.cs:68-74`
  describes the lint), so prices go through a template with the number as an
  argument, not a formatted string.

### 3b. Wiring

`ScreenRegistry.Map()` (`ScreenRegistry.cs:574+`) already builds `MapScreen`
and binds `MapController`. The shop's controller attaches in the same `Wire`
block and binds `screen.Shop`'s refs, exactly as the hub binds `screen.Draft`.
No new `ScreenDef`.

### 3c. Audits

`UiFrames.All` — 1920×1080, 2580×1080, 1920×1440, 1920×1200
(`Domain/UiKit/UiFrames.cs:16-22`) — re-solves the whole map tree with the shop
in it. The 4:3 frame is the one that bites: the brief's layout uses the full
1080 of vertical space, and at 1920×1440 the panel is fixed-size and centred,
so the risk is the map content behind it, not the shop. Expect one
`AllowOverlap("the shop panel covers the map it is entered from")` on the
panel, with that reason.

Text fit is checked by `UiTextFitAudit` (`SceneBuilder.cs:105`), which is why
price labels get a width sized for four digits and a suffix, not for today's
two.

### 3d. The detail panel, and keyboard focus

A card is 380×236 and carries an icon, a name, a sub-line and a price. That is
enough to tell two swords apart by name and not enough to choose between them.
Every other screen in the suite that asks the same question answers it the same
way, and there are two established shapes to copy from:

- **A persistent panel in reserved space** — `TalentScreen`'s
  `DetailName`/`DetailBody` plus its kicker, price and refusal lines
  (`Domain/UiKit/Screens/TalentScreen.cs:98-99`, `:308-350`).
- **A floating tooltip** — the dossier's and the reckoning screen's
  (`CharacterDossierScreen.cs:124-126`, `:795-814`;
  `ReckoningScreen.cs:158-159`, `:757-785`).

**Take the Talent screen's shape.** A tooltip is authored at `Place.At(0, 0)`
and moved at runtime specifically so the containment audit can still solve it —
`ReckoningScreen`'s own header at `:705-720` explains that, and the price is an
`AllowOverlap`, a placement helper, and an authored half-height that cannot
exceed `ContentTop` (a constraint that already cost that screen a 600→590
retreat by 1.6px). A persistent panel in reserved space needs none of it, and
the shop has the space: §3e shrinks the recipient strip from five rows to
three, and the block underneath it is where the panel goes. Coordinates in
README §3.

What it shows, by the kind of the focused or hovered card:

| Kind | Contents |
|---|---|
| Gear | Equip slot; the derived-stat delta against the **currently selected recipient**; the affix names |
| Consumable | What it does, one line, off the item's own description |
| Relic | Effect text and rarity |
| Spell | Effect, mana cost, cooldown, target shape — every one a field `ResolvedSkill` already carries (`Domain/Content/ResolvedSkill.cs:15-80`) |

The delta comes from `ItemDescription.Compare`
(`Core/ItemDescription.cs:196-227`), which simulates the swap through the same
`ResolveTargetSlot` a real equip uses and therefore reports the *cascade* — a
piece that goes dormant because its requirement was being met by what got
displaced — for free, with no hand-written dependency map. Two things it does
not do, which the panel must not imply it does:

- **It takes no modifiers and no rift tier.** The signature is
  `Compare(Character, ItemDefinition, int candidatePlus)` and `SimulateEquip`
  sets only `(id, plus)` on the clone (`ItemDescription.cs:250-263`). So the
  delta is the base contribution at that plus, and the affixes are listed **by
  name** beside it rather than folded into the numbers. Do not add them by
  hand: that is a second implementation of what an affix does, and the two will
  disagree.
- **A weapon's contribution does not appear in the delta at all.** Gear no
  longer grants flat Attack — `ContentDatabase.EffectiveStats` zeroes
  `bonus.attack` — so a sword's whole contribution arrives at the
  `FightEncounterAdapter` seam as WeaponPower and reads as `+0 attack`
  (`ItemDescription.cs:229-248` states this outright). For a weapon card the
  panel shows the damage line `ItemDescription` already builds for the dossier
  (`:145-168`), not a stat delta that would be honestly zero and read as
  useless.

**Focus order** is sections in layout order, and cards within a section in
index order: spell cards → recipient rows → item cards → relic cards → sell
rows → reroll → leave. Tab advances, Shift+Tab reverses, arrows move within a
section, Enter presses. **Focus and hover feed the same panel**, focus winning
when both are present, so a keyboard user reads exactly what a mouse user reads
by hovering. Focus is drawn as an outline (README §6's `Focus outline` token) —
a shape cue rather than a colour change, so it stays legible on a card that is
already armed and already amber.

### 3e. The recipient strip is sized to the squad the save can field

The designer brief sized the spell-slot strip for five characters. The save
cannot field five. `SaveData.EffectiveMaxSquadSize()` is
`BaseMaxSquadSize + (extra_recruit_slot ? 1 : 0)` (`Data/SaveData.cs:163-166`)
and `BaseMaxSquadSize` is **1** (`SaveData.cs:53`) — the comment there records
that the squad was cut to solo deliberately, that Fly/Dog/Turtle/Owl still
exist and come back the moment it returns to 3, and that buying
`extra_recruit_slot` still reaches **two** today. Two is the real ceiling.

Emit **three** rows. Not two, and not five. That is `ReckoningScreen`'s
precedent taken verbatim: `RowCount = 3`, with the comment "Fixed at build
time, so it must cover the largest party the save can field. Base squad is 1
today and the design's stated target is 3; four leaves headroom without costing
anything, since unused rows are hidden"
(`Domain/UiKit/Screens/ReckoningScreen.cs:91-96`), pinned by
`ReckoningTests.cs:159` asserting `RowCount >= Save.EffectiveMaxSquadSize()`.
Pin the shop's the same way, against the same expression — a test that wrote
the literal `3` would be the drift it exists to catch. Unused rows are hidden,
as `ReckoningScreen`'s are.

The two rows this removes are 96px of vertical (`2 × 40 + 2 × 8`), and that is
where §3d's detail panel goes.

---

## 4. Build order

Estimates are working days for one person, and assume the test suite is run
before each commit (~130s, `tools/run_tests_parallel.ps1`).

**The phases were restructured 2026-09-02.** The original order built the
loadout change and the shop as one stream, which meant the riskiest change in
the plan — removing the only spell every character starts with — landed first
and could not be measured until the last day. The phases below stage the risk
instead: books are **additive** until everything that measures them exists,
and the flip is last and reversible in one commit. The author's decision that
the standard loadout disappears is unchanged; what changed is when the game
finds out.

### Phase 0 — pin the basic spell before removing it (0.5d)

Unchanged, and it stays first even though the removal now happens in Phase E.
The characterisation tests are what make E a one-commit decision instead of an
archaeology exercise, and writing them before anything moves costs nothing.

- **Changes:** nothing in production. Characterisation tests for the current
  behaviour: a level-5 character's Skill row name/cost/power for the automatic
  BasicSpell action; the `ScalingSet.SpellTier` contribution to a cast at INT
  20 vs INT 10 (both for BasicSpell and for `mud_burst`, since both ride the
  same `EffectiveSkillScaling` curve — F4 revised); a bot policy's `BasicSpell`
  scoring.
- **Must not change:** the tests must fail if the behaviour changes, which is
  the point — write them so they pin literals, not recomputed formulas.
- **How we know:** the tests pass now, and each is proved non-vacuous by
  breaking the production value by hand and watching that one test fail.

### Phase A — spell books, ADDITIVELY (2d)

The five spells **keep their `unlockLevel`**. `bookOnly` ships as a field
authored `false` everywhere, `bookTier` ships authored on the five, and a book
teaches a spell through a new route into `AvailableSkillsFor` that happens to
duplicate an unlock the character may already have — which is a no-op, not a
conflict. `PlayerKit.BasicSpell` is untouched.

At the end of Phase A the game is strictly larger than it was and nothing a
player could do before has stopped working. That is the whole point of the
staging: every later phase can be reverted to here.

- **Changes:** `bookOnly` and `bookTier` on
  `SkillDefinition`/`RawSkillEntry`/`ResolvedSkill`, with the resolver and
  validator rules of §1a (including the `!s.bookOnly` filter on
  `FightEncounterAdapter.KitFor(CharacterDefinition, …)`, which today has no
  level filter at all); `RunSnapshot.learnedSpells` + `MaxSpellSlots = 3`;
  `RunOrchestrator.CanLearn`/`LearnSpell`; `AvailableSkillsFor`'s new route;
  `SaveData.Reconcile` dropping unresolvable learned entries with the one-time
  notice (§2e); `VictoryRewards.RollSpellDrop` and its rate constants — the old
  Phase 2, folded in, because a book with no way to obtain one cannot be
  measured.
- **Must not change:** `spells.json`, `SpellTierDefinition`,
  `EffectiveSkillScaling`/`GetSpellTierForLevel` (F4 revised — the shared
  scaling curve every Spell-axis skill rides, `mud_burst` included);
  `PlayerKit.BasicSpell`; the five entries' `unlockLevel`;
  `ItemOfferRoll.Candidates()`, which stays equippables-only
  (`Core/ItemOfferRoll.cs:30-42`).
- **How we know:** a level-3 sheep still has `mud_burst` by level; a level-1
  sheep that learned `mud_burst` from a book has it once, not twice; a
  definition-only `KitFor` no longer hands out `bookOnly` skills; a seeded run
  drops the same books twice; `ContentDatabaseTests`' canary gains a `bookOnly`
  skill and a `learnedSpells` entry that unlocks it (AUDIT #20,
  `AUDIT.md:363`, is the precedent for that being required). Full suite green
  with scenes built.

### Phase B — the shop room (2d)

- **Changes:** `RngStreams.Shop = 5` and `Derive`/`Open`'s third coordinate
  (F9); `Domain/Rewards/ShopPricing.cs`; `Domain/Rewards/ShopStock.cs` with the
  three count constants and the roll (§2d); `RunSnapshot.shopStock` /
  `shopRerollsUsed` / `shopNodeId` / `shopStockVersion` and their
  reconciliation (§2e); `Arrival.Shop`, the `ArriveAt` branch and `LeaveShop`;
  the six mutation methods of §2f; `RoomResolution.Kind.ShopNotBuilt` retired.
- **Must not change:** the treasure stream's key or any existing stream's
  numbers (F9's bit-for-bit test is the proof); `BankPayout`'s gold-only
  contract (`RunManager.cs:393-402`); the "persist on arrival before resolving"
  ordering (`RunOrchestrator.cs:35`).
- **How we know:** every price in §2c pinned as a literal; the
  `sell(x) < buy(x)` sweep (§2b); a run that quits inside a shop and reloads
  finds identical stock at the same reroll price with the same cards sold; a
  sold-out entry stays at its index; the twelve atomicity tests of §2f;
  `MapController.Walk.Arrive`'s `default:` no longer swallows a new arrival
  (prove it by adding a fifth value and watching a test fail).

### Phase C — the screen (2.5d)

Half a day more than the original estimate, and it buys the detail panel and
the keyboard focus order (§3d), which the original plan did not have.

- **Changes:** `Domain/UiKit/Screens/ShopScreen.cs`, nested into `MapScreen`;
  `ShopController` in Core; the interaction state machine (README §5) as an
  explicit state field rather than a set of booleans; the detail panel;
  `CountBindings` for the five arrays; strings in `UiStrings`.
- **Must not change:** `ScreenRegistry.All` stays five entries; no new scene.
- **How we know:** the layout audit passes at all four `UiFrames`; the count
  audit passes; `UiTextFitAudit` passes against README §3's worst-case string
  table; the recipient strip's row count is asserted against
  `EffectiveMaxSquadSize()` rather than a literal (§3e); a PlayMode test walks
  map → shop → buy → leave and finds the room cleared and the gold spent; a
  second walks the state machine's whole input table and asserts no input in
  any state reaches an unlisted state.

### Phase D — bot policy, and the batches that gate Phase E (1.5d)

- **Changes:** `IRunPolicy.ChooseShop` (`Domain/Bot/IRunPolicy.cs:17-48`) and
  implementations for the four archetypes; `ShopView`; `BotRunDriver`'s shop
  branch, replacing the two-way `isFight` split at `:358`; the trace records
  purchases; `roomType` added to `runs.jsonl`'s `rooms[]` rows.
- **Must not change:** the run trace's existing fields, so old reports stay
  comparable. The additions above are additive — `bot_merge.py` reads the keys
  it knows and a new one breaks no previous batch.
- **How we know:** a 12,000-run batch at the same shard/profile shape as
  `reports/bot/20260902-020048`, reporting the two lists below.

**Spell acquisition — these are the numbers that gate Phase E:**

1. P(a fielded character has learned a first spell) by step 8, 16, 24, 32 —
   i.e. by each boss.
2. Expected spell slots filled per leg, per profile.
3. Median depth for runs holding **zero** books entering leg 2, against runs
   holding at least one. Phase E makes the bookless start universal, so this is
   the measurement that says whether it is playable.
4. Share of shop visits that showed a book the party could use and could not
   afford.

**The shop's economy:**

5. **Route comparison**: median depth and median gold when the policy takes a
   shop node instead of a fight node in the same column. This is the one that
   needs `roomType` on the `rooms[]` row — today only `fights[]` carries it
   (`docs/BOT_SUMMARY_SCHEMA.md`, `runs.jsonl` block), so a shop visit is
   currently indistinguishable from any other non-fight room in the merged
   data. `RoomTrace` already carries `RoomType` in `traces.jsonl`, so this is a
   writer change, not a new measurement.
6. Purchases per visit, and the share of visits with **zero** purchases.
7. Share of shown cards affordable on arrival.
8. Rerolls followed by no purchase.
9. Gold left on leaving, and gold at death — which should fall sharply from the
   measured 531/740 medians, or the sink is not biting.
10. Spend share per section.

**The gate.** Phase E ships only when, at the tuned rates and prices: (a) the
median run has a usable book before the first boss; (b) runs that reach leg 2
bookless do not lose materially more depth than a rate change could give back;
and (c) taking a shop over a fight does not cost depth. If any of the three
fails, the fix is a drop rate or a price, measured again — not shipping E and
finding out from a player. The bot's numbers are a floor, not a verdict, and
the call on whether a gap is acceptable is the author's.

### Phase E — the five spells become book-only (1d, gated on D)

The phase that can break the game, which is why it is last and why it is the
only one with an entry condition. It is also small, because everything it needs
shipped in A-D.

- **Changes:** `bookOnly: true` and `unlockLevel` **removed** on the five
  entries (§1a's table); the free BasicSpell row and every reference in F4
  (revised)'s "removed" column taken out; `bookTier` becomes the only route to
  a book's price.
- **Must not change:** everything Phase A must not change, minus
  `PlayerKit.BasicSpell` — plus §2c's price table, which reads `bookTier` and
  never `unlockLevel` precisely so this phase moves no price.
- **How we know:** Phase 0's `mud_burst` scaling test at INT 20 vs INT 10 still
  produces the same number, unchanged, because nothing it reads moved; a fresh
  sheep has `shear` from turn one and a playable Skill verb with no spells at
  all (§1f); content validation refuses an entry carrying both `bookOnly` and
  `unlockLevel`; the Phase D batch re-run against the same baseline.
- **Revert:** one commit. That is the entire argument for this ordering. At the
  end of D the shop has shipped and the books are a bonus on top of the
  existing loadout; if E measures badly it comes out and the shop stays.

**Total: 9.5 days**, against the previous plan's 7.5-8. The staging costs
1.5-2 days and buys three things the old order could not: the five spells reach
players through books before they stop reaching them by level, the flip is
gated on measurements rather than on an argument, and the shop ships whether or
not the loadout change survives contact. If that trade is not worth 2 days,
collapse A and E back into one phase — but do it as a decision, not by
accident.

---

## 5. Assumptions

Stated because the author gave no answer and the code did not force one. Each
is cheap to reverse.

1. **Reroll rerolls the whole shop** — all three sections and the four items at
   once, not per section. Price doubles per reroll (25 / 50 / 100 / 200 …),
   saturating at 9999 (§2b), and `n` is `shopRerollsUsed` **at that node**.
   There is no reset, because a shop clears on leave and is never re-entered.
   Rerolls are unlimited while the player can pay; the doubling is the limit,
   and the ceiling exists so the label can still be printed.
2. **Prices are a pure function of tier, plus and rift — never of depth or
   floor** (author decision 2, §2a/§2b revised). Depth affects only which
   tier a shop *stocks* (`RarityTable.FloorTier`, §2c/§2d); that a tier-matched
   item costs closer to two fights by leg 5 than one is an observed
   consequence of flat income meeting a climbing stock tier, not a knob this
   formula turns.
3. **Spell prices are banded by a book tier derived from the spell's
   (pre-decision-1) `unlockLevel`** (§2b), flat with depth for the same reason
   every other price is. A spell is worth the same whenever it is bought
   because it lasts the whole run either way.
4. **Buying a relic appends to `run.relicIds`** with no cap, per F8. There is no
   slot to fill and none to invent.
5. **Sell is bag-only** (`save.stockpiledItems`); worn gear cannot be sold, as
   an intentional restriction. The path is buy → leave → equip through the
   dossier on the map (which displaces the old piece into the bag) → sell it at
   the next shop. §2c walks it, and states why no dossier is reachable from
   inside the shop in v1.
6. **Stock is tier-banded to the depth** through `RarityTable.FloorTier`, so the
   shop at step 8 sells tier-0..2 gear and the shop at step 40 sells tier-4..6.
   Without this the price table's "one tier up" row has no meaning.
7. **The shop's roll uses `EncounterClass.Normal`.** Browsing must not beat
   fighting.
8. **Book drop rate is per fight, not per enemy** — 0.10 / 0.20 / 0.35 by room
   class. A first guess, placed in named constants for the balance batch to
   move.
9. **A relic already held is not offered**, matching the draft
   (`RunOrchestrator.cs:84-86`). A book already in one of that character's slots
   shows OWNED rather than being filtered out of the roll, because ownership is
   per character and the shop is shared — but a book **every** fielded
   character already knows is filtered out at roll time (§2d), since no press
   on it could do anything.

### To validate, not to assume

The two below are stated as assumptions because the plan needs them to be true
and nothing in the code makes them true yet. Both are cheap to add and both are
measured by Phase D before Phase E is allowed to ship.

10. **The first boss of a run guarantees one book drop.** At the proposed rates
    (0.10 normal / 0.20 elite / 0.35 boss, per fight) a run can plausibly reach
    leg 2 with nothing learned — which is exactly the state Phase E makes the
    *starting* state, and exactly what Phase D metric 3 measures. If bookless
    runs lose real depth, a guaranteed first-boss book is the cheapest fix
    available and it goes in before E. Recorded here rather than built now
    because the measurement may say it is unnecessary, and a guarantee added
    "to be safe" is a balance change nobody measured.

11. **Every roll offers at least one item card priced at or below a normal
    fight's payout at that depth** — 16-24 gold, by §2a's table. Without it a
    shop can present four cards a player has no chance of affording, which is a
    room that cost them a fight and returned nothing but a reroll button.
    Implemented as a constraint on the item draw (re-draw the cheapest slot,
    bounded, until it lands under the anchor) rather than as a discount, so the
    price formula stays a pure function of tier/plus/rift (§2a). Phase D metric
    7 is what says whether the constraint is doing anything.

12. **No dossier or equipping from inside the shop in v1** (§2c). Bought gear
    is worn from the map, and a shop purchase never auto-equips — deliberately
    unlike `RunOrchestrator.TakeOffer`, which does.

---

## 6. Later — recorded, not built

- **Tier drops and plus rolls need reworking.** The author's read is that tiers
  climb too fast and pluses are too rare. The code agrees on both counts:
  `RarityTable.StepsPerTier = 8` (`RarityTable.cs:40`) gives a tier a leg, so a
  40-step run passes through five tiers of gear; and `LootLadder`'s geometric
  climb means the top rung is 5% at the hard cap of `MaxStep = 0.55`
  (`LootLadder.cs:19-52`), with `NormalStep` at 0.22 — so a +3 from an ordinary
  fight is about 1%. Both are single constants with documented reasoning, so the
  rework is a tuning pass plus a bot batch, not a redesign. **Out of scope
  here** because moving `StepsPerTier` moves every price in §2c.
- **The Events screen.** `RoomType.Event` generates at weight 14 — more than
  twice the shop's — and resolves as `Kind.EventNotBuilt`
  (`RoomResolution.cs:102-103`). `RoomResolution`'s own comment calls it "a
  REGRESSION against v1 knowingly left standing: v1 ran a wandering-mage event
  that taught an event-gated spell. Porting it needs the spell-teaching path"
  (`:95-101`). **Phase 1 of this plan builds that spell-teaching path**, so the
  event room's blocker is removed as a side effect and the Events screen becomes
  the obvious next piece of work.
- **A relic slot cap**, if the design ever wants one. AUDIT #50/#51
  (`AUDIT.md:1242`, `:1273`) are the register entries; today there is nothing to
  lift.
- **Reaching the character dossier from inside the shop.** The obvious
  convenience, deliberately second (§2c): it needs an overlay stack, a second
  focus owner, and a rule for a bag that changes underneath an open sell list.
  None of that is about shopping, and the map is two presses away.
- **Relic paging**, the day `ShopStock.RelicCount` exceeds the cards a row can
  show (§2d). `Domain/UiKit/Paging.cs` and `RelicDraftController`'s use of it
  are the pattern; adding it before there is anything to page was the original
  brief's mistake.
- **Localization.** Every string goes through `UiStrings` already, which is the
  half that matters structurally. README §3's worst-case table is English only
  and `UiTextFitAudit` checks English only; a second language is a pass over
  every screen in the suite, not a shop feature.
- **`ItemDefinition.cost` and `startingStock`** are v1 store residue with no
  live reader for the equipment case (F6). Once the shop ships and consumables
  read `cost` for real, the field is half-live — worth an `AUDIT.md` entry
  saying which half.
