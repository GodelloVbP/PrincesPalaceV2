# Plan — the in-run Shop, and the spell books it sells

Two systems, one commit stream. The shop is the smaller half: it is a room, a
roll and a screen. The spell books are the larger half, because "the standard
spell loadout disappears" is not a deletion — the thing being deleted is the
only path by which a caster's Intelligence multiplies anything at all.

Written against `5f1e7d4`. Every file:line below was read, not remembered.
Every gold number below was measured off `reports/bot/20260902-020048`
(12,000 runs, four shards, commit `32d31ba`), not estimated.

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

### F4. There is no "standard spell loadout". There is one auto-scaling spell per level, and deleting it kills Intelligence

This is the finding that decides the whole spell-book design, and the brief's
framing hides it.

`ContentData/spells.json` is **not a list of spells**. Its own `_readme` says:
"The Skill power/cost curve, one row per character level. Every character's
Skill action automatically uses whichever tier matches their current level —
there's no separate spell-picking UI." Nine rows, `Spark` (level 1, 10 MP,
×1.5) to `Ascendance` (level 9, 28 MP, ×4.2), each with a `scalesWith` line
like `intelligence S` / `wisdom C`.

It reaches a fight like this: `ContentDatabase.GetSpellTierForLevel`
(`Core/Content/ContentDatabase.cs:362`, and the score-aware overload at `:389`)
→ `FightEncounterAdapter.TierAtLevel` / `SpellTierFor`
(`Core/FightEncounterAdapter.cs:628-635`) → `PlayerKit.BasicSpell`
(`Domain/Combat/Session/CombatantKit.cs:42-58`) → an appended submenu row
(`Domain/Combat/Session/FightHudModel.cs:145-159`).

**Every reference that removing it touches:**

| File | Lines | What |
|---|---|---|
| `Core/FightEncounterAdapter.cs` | `:582`, `:621`, `:628-635` | both `KitFor` overloads, `TierAtLevel`, `SpellTierFor` |
| `Domain/Combat/Session/CombatantKit.cs` | `:42-43`, `:49`, `:58` | the field and the ctor parameter |
| `Domain/Combat/Session/FightSession.Skills.cs` | `:852`, `:854`, `:856-857`, `:866-869`, `:905`, `:907` | seven members: cost, name, afford, preview, multiplier |
| `Domain/Combat/Session/FightHudModel.cs` | `:35`, `:46`, `:55`, `:93-95`, `:145-159` | `IsBasicSpell`, `BasicSpellRow`, `SkillRowCount`, the appended row |
| `Core/FightController.Hud.cs` | `:894` | the detail panel's POWER stat |
| `Domain/Bot/FightAction.cs` | `:15`, `:76`, `:114-122`, `:166` | the `BasicSpell` action kind |
| `Domain/Bot/FightRunner.cs` | `:219`, `:228-229` | trace naming (`"Skill:" + BasicSpellNameFor`) |
| `Domain/Bot/GreedyAggressivePolicy.cs` | `:45`, `:81`, `:94-95` | scoring |
| `Domain/Bot/GreedyDefensivePolicy.cs` | `:149`, `:247-248` | scoring |
| `Domain/Bot/Lookahead2Policy.cs` | `:27`, `:154-156` | scoring |
| `Core/Content/ContentDatabase.Effective.cs` | `:569`, `:602`, `:658-675`, `:686` | **the scaling profile** — see below |
| `Core/Content/ContentDatabase.Validation.cs` | `:440-472` | duplicate-level and must-have-level-1 checks |
| `Core/Content/SpellTierDefinition.cs`, `Domain/Content/RawSpellTierEntry.cs`, `SpellTierEntryResolver.cs`, `ResolvedSpellTier.cs` | whole files | the content type |
| `Editor/ContentBuilder.cs` | `:22`, `:48`, `:59`, `:435-462` | `BuildSpellTiers` |
| `Data/Character.cs` | `:79` | a comment describing the mechanic |
| tests | `BotPolicyTests.cs:34-35,115-116,253-254,365-366`, `ChilledStatusTests.cs:233`, and every other `new PlayerKit(...)` site | the ctor's default arg absorbs most, the four `basicSpell:` sites do not |

**The load-bearing one is `ContentDatabase.Effective.cs:658-675`.** It reads the
tier's `scaling` and puts it into `ScalingSet.SpellTier`
(`Domain/Stats/ScalingSet.cs:21`), which is summed with the two weapon profiles
at `:54` and `:60`. That is the *only* place a caster's Intelligence and Wisdom
multiply a cast. Delete the tier without replacing that input and INT becomes a
dead stat — quietly, with nothing failing.

**Therefore: a spell book must carry its own `ScalingProfile`.** This is not
optional polish; it is the condition under which the deletion is safe. The
existing `scalesWith` grades in `spells.json` are the migration source: fold
them into the authored books.

### F5. A learned spell should be a `ResolvedSkill`, which removes the "spell submenu" from the build entirely

`ResolvedSkill` (`Domain/Content/ResolvedSkill.cs:15-80`) already carries every
field a spell needs: `Effect`, `Targeting`, `ManaCost`, `ResourceCost`,
`Power`, `FlatAmount`, `IgnoresDefense`, `DamageInstances`, `Vfx`,
`AppliesStatus`, `Requirements`, `ScalingAxis`, `QueuePushSlots`,
`CooldownTurns`, `PlayerSelectable`.

`PlayerKit.Skills` (`CombatantKit.cs:35`) is already the list the fight's Skill
submenu is built from — `FightHudModel.SkillRows`
(`FightHudModel.cs:118-160`) iterates `session.SkillOptionsFor(actor)`,
handles affordability, cooldown labels and ability-requirement filtering, and
then appends the basic-spell row at the end.

So the whole of the fight-side work is: **put learned books into
`PlayerKit.Skills`, and stop appending the extra row.** No new submenu, no new
dispatch, no new targeting. `FightHudModel.BasicSpellRow`/`SkillRowCount`
(`:93-95`) exist precisely so the row index and the dispatcher cannot disagree
about which row is the basic spell; both disappear with it.

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
(`RngStreams.cs:44`), and the shop needs three: step, node, reroll index. Pack
rather than extend — `b = nodeId * 64 + rerollIndex` — or add a three-input
overload. Packing is smaller and keeps `Derive`'s signature a serialized format
too; 64 rerolls in one visit is not reachable at a price that doubles.

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

### 1a. Content: a new type, not an item kind

**Decision: a new content type `spellbooks.json` → `SpellBookDefinition`.** Not
an `ItemKind`, and not a reuse of `SkillDefinition`.

Reasons, in order of weight:

1. **A book is not a possession.** It is consumed on learning and never enters
   the bag. `ItemKind` (`Core/Content/ItemEffect.cs`) values are Consumable /
   Weapon / Equipment, and every one of them is a thing that sits in
   `stockpiledItems` with a count. A book with `count` is a lie the sell panel
   would have to be taught to skip.
2. **A book is not a character's authored kit.** `skills.json` entries carry a
   `characterId` and an `unlockLevel` (`ResolvedSkill.cs:20-21`) — they are
   *whose* and *when*. A book is neither: any character can learn any book, at
   any level, and only within one run.
3. But a book **resolves into** a `ResolvedSkill` (F5), so the definition is
   `ResolvedSkill`'s fields minus `CharacterId`/`UnlockLevel`, plus a price
   tier and a `ScalingProfile` (F4).

**Ordering, per CLAUDE.md gotcha 4.** `ContentDatabase.LoadOrdered<T>` is
constrained on `IOrderedContent` and a lint refuses `Resources.LoadAll`
anywhere else. `SpellBookDefinition : ScriptableObject, IOrderedContent` with
`sortOrder` assigned from authored file order, exactly as
`SpellTierDefinition.cs:11` and `ItemSetEntryResolver` already do. It does not
build otherwise.

Seed the file by migrating the nine `spells.json` tiers into nine books —
`Spark` through `Ascendance`, keeping each row's `manaCost`,
`powerMultiplier` and `scalesWith` — so the first version of the system ships
with content that is already balanced, and `spells.json` is deleted in the same
commit rather than left as a second source of truth.

### 1b. Data: three slots per character, on the run

```
RunSnapshot:
    public List<LearnedSpellEntry> learnedSpells = new List<LearnedSpellEntry>();

[Serializable] class LearnedSpellEntry { public string characterId; public string bookId; public int slot; }
```

A flat list of `{characterId, bookId, slot}`, not a dictionary — same reason
`RunHealthEntry` is a list entry (`Data/RunSnapshot.cs:9-14`): `JsonUtility`
serializes fields and `Dictionary` is not one of the shapes it can write.

`MaxSpellSlots = 3`, a constant in Domain beside the entry type. Purely
additive, so `SaveData.CurrentVersion` does not move — an older save's
in-flight run has no such field, `JsonUtility` leaves the list empty, and
"empty" is exactly "has learned nothing", which is the correct reading.

Nothing carries over: `StartRun` replaces the whole snapshot
(`Core/RunManager.cs:135`), which is the same mechanism that already resets
`relicIds` and `offerRerollsUsed` (`RunSnapshot.cs:112`, `:129-131`).

`SaveData.Reconcile` must drop entries whose `bookId` no longer resolves, the
same tolerant treatment `stockpiledItems` gets at `Data/SaveData.cs:405` — but
**dropping, not deleting silently into a void**: a learned book that vanished
because content was edited under a live run should free its slot, which is
what dropping the entry does.

### 1c. How a learned book reaches the kit

One insertion point, in the place the adapter already builds the list:

`FightEncounterAdapter.KitFor(Character, ...)`
(`Core/FightEncounterAdapter.cs:610-621`) builds `skills` and passes it to
`new PlayerKit(...)`. Append the run's learned books for that character id,
resolved to `ResolvedSkill`, after the authored skills. The submenu order then
reads "your kit, then what you learned this run", which is the same ordering
the basic spell had (`FightHudModel.BasicSpellRow` put it last).

The `basicSpell` argument at `:621` becomes `null` and then goes away with the
parameter.

The definition-only overload at `:549-582` is the no-save path (a direct scene
load, a test); it learns nothing and passes an empty list. That is the
graceful-degradation case and it already behaves correctly.

### 1d. Learn / replace

```
LearnBook(characterId, bookId):
    entries = learnedSpells where characterId matches
    if entries.Count < 3     -> append at the lowest free slot index
    else                     -> caller must supply a slot to overwrite
```

Two calls, not one: `CanLearn(characterId)` returns the free slot or -1, and
`LearnBook(characterId, bookId, slot)` writes. The screen asks first, shows the
replace picker when the answer is -1, and calls with the chosen slot. The bot
policy answers the same question without a screen.

Refuse a duplicate: learning a book already in one of that character's three
slots is a no-op that returns false, and the shop card reads OWNED for that
character. Not a hard error — content can change under a run — but not a silent
success either.

### 1e. Drops

Books drop from won fights, alongside the existing consumable roll.

`VictoryRewards.RollConsumableDrops` (`Domain/Combat/Session/VictoryRewards.cs:76`)
is the model: one roll per enemy, in roster order, returning `(enemy, itemId)`
pairs rather than mutating anything, "which is what makes a seeded run
reproduce its own loot". Add `RollSpellBookDrop` beside it with the same shape
— pure, injected randomness, returns a book id or null.

Rate: **not** `ItemDropChance = 0.3f` (`VictoryRewards.cs:30`), which is
per-enemy and would hand out several books a leg. A book is a third of a
character's whole loadout for the run. Start at one roll per *fight*, not per
enemy, at `0.10` normal / `0.20` elite / `0.35` boss — roughly one book per leg,
which fills three slots by the end of leg 3 and leaves the shop as the way to
get them sooner or better. Put it in `VictoryRewards` as named constants so the
next balance batch can move them without hunting.

Do **not** put books into `ItemOfferRoll` (`Core/ItemOfferRoll.cs`). Its
`Candidates()` is documented as equippables-only for a reason worth keeping:
"a 'choose one of three' that can offer a health potion is not a choice, it is
a tax on the one player who reads carefully" (`:30-35`). A book in the gear
offer is that same tax with a different noun. A dropped book is an event of its
own, shown after the offer.

### 1f. A character with no spells

House style is graceful degradation, and the honest version here is that the
Skill verb has nothing behind it for a level-1 character who has found nothing.

`FightHudModel.SkillRows` returns a list; today it can never be empty because
the basic row is always appended (`FightHudModel.cs:145-159`). After the change
it can be. Two things must hold:

1. The Skill verb is **shown and disabled**, not hidden, with the reason on it
   ("NO SPELLS LEARNED"). The submenu's own rule already says unaffordable rows
   are included and dimmed rather than removed, "the player should learn what
   they have rather than watch the list change length"
   (`FightHudModel.cs:100-103`) — a verb that vanishes is the same defect one
   level up.
2. Every bot policy that scores a `BasicSpell` action must tolerate an actor
   with zero skills. `FightAction.cs:114-122` already guards on
   `KitFor(actor)?.BasicSpell != null` before offering the action, so removing
   the kind removes the guard's subject too — but `GreedyAggressivePolicy.cs:45`
   and `GreedyDefensivePolicy.cs:149` filter action lists that could now be
   attack-only. Pin it with a test: a kit with no skills produces a legal
   action list and a policy picks from it.

---

## 2. The shop

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

So "a normal fight's payout at that depth buys about one common item" and
"higher tier costs more" cannot both hold across the whole run — if price is
linear in tier and income is flat, purchasing power must fall. It should fall:
gold mattering less as a run goes on is correct for the genre, and the shop
stays a real decision early where the player is poorest. The rule that
survives intact is the *local* one: **within a shop, the tier-matched item
costs about one normal fight, and stepping up a tier costs visibly more.**

### 2b. Pricing formulas

```
BaseFor(tier)          = 20 + 4 * tier                     // gear
PlusFactor(plus)       = 1.00 + 0.35 * plus                // 0..5
RiftFactor(riftTier)   = 1.00 + 0.50 * (int)riftTier       // 0..3 affix slots
GearPrice              = round( BaseFor(tier) * PlusFactor * RiftFactor )

ConsumablePrice        = ItemDefinition.cost               // authored, 15 today

SpellBookPrice(tier)   = 45 + 25 * tier                    // book tier 1..5

RelicPrice(rarity)     = { Common 60, Uncommon 110, Rare 190, UltraRare 300, Mythic 460 }

SellPrice(x)           = max(1, round(0.30 * price(x)))

RerollPrice(n)         = 25 * 2^n                          // n = rerolls this visit
```

Every constant lives in one Domain class (`Domain/Rewards/ShopPricing.cs`),
pure and engine-free like the rest of `Domain/Rewards/`, so the whole table is
unit-testable without a scene — and **pinned with literal expected values**,
never recomputed from the formula, per CLAUDE.md gotcha 5.

Relic prices are **flat, not depth-scaled**, deliberately: a relic bought at
step 8 has thirty-two steps left to pay off and one bought at step 40 has
nothing, so a flat price makes the early buy the good buy without a second
curve to tune.

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
| Spell book, tier 1 | 70 | 70 | 70 |
| Spell book, tier 3 | 120 | 120 | 120 |
| Spell book, tier 5 | 170 | 170 | 170 |
| Relic, Common | 60 | 60 | 60 |
| Relic, Rare | 190 | 190 | 190 |
| Relic, Mythic | 460 | 460 | 460 |
| Reroll 1 / 2 / 3 | 25 / 50 / 100 | 25 / 50 / 100 | 25 / 50 / 100 |

Reading the anchors: at step 8 a tier-matched common is **24** against a
16-19 fight and a **61** rare against a 68-76 boss — both land. At step 40 the
common is 40 against a 24 fight, so purchasing power has fallen to ~0.6 fights
per item, which is the taper §2a argues for. A Mythic relic at 460 is about
2.5 legs of saving: a run-defining purchase, reachable but never casual.

Sell prices at 30%: a tier-matched common sells for 7 / 10 / 12; a +3
two-affix piece for 25 / 33 / 41. Selling the whole bag never funds a relic,
which is the "not supposed to make you rich" instruction holding.

**Sell is bag-only. Worn gear cannot be sold.** Recommended and adopted:
`EquipmentOps.Unequip` (`Core/EquipmentOps.cs:77`) already exists and the
character sheet already calls it (`Core/CharacterDossierController.cs:245`
`UnequipSlot`), so the flow is "unequip through the dossier, then sell" with no
new code. The alternative — selling off the body — means the shop needs the
whole equip/displace rule set (`Domain/Equipment/EquipMove.cs`), and
`EquipMove`'s own convention is that "every method does both halves of the move
or neither"; a sell that half-performs an unequip is exactly the quiet
item-destroying bug that rule exists to prevent.

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
| Spell books | 3 | weighted over `ContentDatabase.SpellBooks`, excluding books already in every fielded character's slots |
| Items | 4 | `ItemOfferRoll.Candidates()` through `ItemOfferTable.Choose(candidates, FloorTier(step), MaxTier, next, 4)`, then `RarityTable.RollPlus` + `ModifierTable.RollRiftTier`/`PickModifiers` per item — the identical three-axis roll `ItemOfferRoll.Roll` does at `Core/ItemOfferRoll.cs:154-168`, with `count` 4 |
| Relics | 3 | `RelicPool.DraftWeighted(available, next, 3)` with `available` = `RelicPool.Available` minus `run.relicIds`, exactly as `RunOrchestrator.RelicDraftOffer:84-86` |
| Sell | — | not rolled; it is `save.stockpiledItems` |

Extract the per-item three-axis roll out of `ItemOfferRoll.Roll` into a
`RollOne(offer, encounter, favor, next)` helper so the shop and the reward
screen cannot drift on what "one rolled copy" means. That is one seam with two
callers, the same shape F2 of `PLAN_BALANCE_BOT.md` argues for.

Encounter class for the roll: `EncounterClass.Normal`. A shop is not a fight;
using Elite or Boss would make browsing better than winning.

### 2e. Persistence

Stock state on `RunSnapshot`, purely additive:

```
public List<ShopStockEntry> shopStock = new List<ShopStockEntry>();   // itemId/bookId/relicId, kind, plus, riftTier, modifierIds, sold
public int shopRerollsUsed;                                           // this visit; cleared on leave
public int shopNodeId = -1;                                           // which node the stock belongs to
```

`shopNodeId` is the guard, not an optimisation: without it a stale list from a
previous shop paints the next one. On arrival, if `shopNodeId != currentNodeId`,
re-roll and reset `shopRerollsUsed` to 0.

Sold-out is a flag on the entry, not a removal — a removed entry renumbers the
card indices and the screen binds cards by index, which is the class of bug
`ScreenDef.CountBindings` exists to catch (`ScreenRegistry.cs:24-28`).

**The run persists on arrival, before the shop resolves.** This is not a new
rule: `RunOrchestrator`'s own header states it — "the run persisted on arrival
BEFORE the room resolves" (`RunOrchestrator.cs:35`) — and it is why quitting
inside a shop cannot reroll it. `RunManager.MoveTo` already persists; the stock
roll must happen after that call and be persisted itself.

### 2f. The `RunOrchestrator` seam

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

returning one of buy-item(i) / buy-book(i, replaceSlot) / buy-relic(i) /
sell(bagIndex) / reroll / leave, called in a loop until it says leave or the
purse refuses. `RunView` already carries `Gold` (`Domain/Bot/RunView.cs:17`),
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
  cannot widen a tree. This is F3 of `PLAN_PROGRESSION_TRACK.md` restated, and
  it means the relic page arrows exist because the *maximum* is paged, not
  because today's 18 relics need it.
- Every such array is declared to `UiCountAudit` through
  `ScreenDef.CountBindings` (`ScreenRegistry.cs:24-28`) — five arrays here
  (spell cards, item cards, relic cards, sell rows, slot chips), and the audit
  comment names the shipped Store bug that pairing exists to prevent.
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

---

## 4. Build order

Estimates are working days for one person, and assume the test suite is run
before each commit (~130s, `tools/run_tests_parallel.ps1`).

### Phase 0 — pin the basic spell before removing it (0.5d)

- **Changes:** nothing in production. Characterization tests for the current
  behaviour: a level-5 character's Skill row name/cost/power; the
  `ScalingSet.SpellTier` contribution to a cast at INT 20 vs INT 10; a bot
  policy's `BasicSpell` scoring.
- **Must not change:** the tests must fail if the behaviour changes, which is
  the point — write them so they pin literals, not recomputed formulas.
- **How we know:** the three tests pass now, and each is proved non-vacuous by
  breaking the production value by hand and watching that one test fail.

### Phase 1 — spell books replace the tier curve (2.5-3d)

- **Changes:** `spellbooks.json` + `SpellBookDefinition` (`IOrderedContent`) +
  resolver + `ContentBuilder.BuildSpellBooks`; the nine `spells.json` tiers
  migrated in with their `scalesWith` intact; `RunSnapshot.learnedSpells` +
  `MaxSpellSlots`; `RunOrchestrator.LearnBook`/`CanLearn`; the adapter appends
  learned books to `PlayerKit.Skills`; every reference in F4's table removed;
  `spells.json`, `SpellTierDefinition` and the four `Domain/Content` spell-tier
  files deleted.
- **Must not change:** `ScalingSet`'s three-profile shape, and the fact that
  Intelligence multiplies a cast. `ContentDatabase.Effective.cs:658-675` must
  keep filling `ScalingSet.SpellTier` — from the learned book now, `None` when
  nothing is learned.
- **How we know:** Phase 0's scaling test, rewritten to learn a book with the
  same grades, produces the same number. `ContentDatabaseTests`' canary gains a
  spell-book entry (AUDIT #20, `AUDIT.md:363`, is the precedent for that being
  required). A character with zero books produces a legal action list and a
  playable Skill verb. Full suite green with scenes built.

### Phase 2 — books drop (0.5d)

- **Changes:** `VictoryRewards.RollSpellBookDrop` + rate constants; the
  fight-settlement path offers the drop and the learn/replace answer routes
  through `RunOrchestrator.LearnBook`.
- **Must not change:** `ItemOfferRoll.Candidates()` stays equippables-only
  (`Core/ItemOfferRoll.cs:30-42`).
- **How we know:** a seeded run drops the same books twice; a bot batch reports
  median books learned per run, which should sit near 1 per leg.

### Phase 3 — the shop room (2d)

- **Changes:** `RngStreams.Shop = 5`; `Domain/Rewards/ShopPricing.cs`;
  `RunSnapshot.shopStock`/`shopRerollsUsed`/`shopNodeId`;
  `Arrival.Shop` + the `ArriveAt` branch + `LeaveShop`; buy/sell/reroll on
  `RunOrchestrator`; `RoomResolution.Kind.ShopNotBuilt` retired.
- **Must not change:** the treasure stream's key, `BankPayout`'s
  gold-only contract (`RunManager.cs:393-402`), and the "persist on arrival
  before resolving" ordering (`RunOrchestrator.cs:35`).
- **How we know:** every price in §2c pinned as a literal; a run that quits
  inside a shop and reloads finds identical stock; a sold-out entry stays at its
  index; `MapController.Walk.Arrive`'s `default:` no longer swallows a new
  arrival (prove it by adding a fifth value and watching a test fail).

### Phase 4 — the screen (2d)

- **Changes:** `Domain/UiKit/Screens/ShopScreen.cs`; nested into `MapScreen`;
  `ShopController` in Core; `CountBindings` for the five arrays; strings in
  `UiStrings`.
- **Must not change:** `ScreenRegistry.All` stays five entries; no new scene.
- **How we know:** the layout audit passes at all four `UiFrames`; the count
  audit passes; a PlayMode test walks map → shop → buy → leave and finds the
  room cleared and the gold spent.

### Phase 5 — bot policy and a batch (1d)

- **Changes:** `IRunPolicy.ChooseShop` + implementations for the four
  archetypes; `ShopView`; `BotRunDriver`'s shop branch; the trace records
  purchases.
- **Must not change:** the run trace's existing fields, so old reports stay
  comparable.
- **How we know:** a 12,000-run batch at the same shard/profile shape as
  `20260902-020048`, reporting: gold at death (should fall sharply from the
  measured 531/740 medians — if it does not, the sink is not biting), items
  bought per run, books learned per run, and median depth (should rise for Mid
  and Late, or the shop is priced too high to matter).

**Total: 8.5-9 days.** The spell books are 4 of them and they are the risk;
the shop screen is the part most likely to need a second design round.

---

## 5. Assumptions

Stated because the author gave no answer and the code did not force one. Each
is cheap to reverse.

1. **Reroll rerolls the whole shop** — all three sections and the four items at
   once, not per section. Price doubles per reroll within a visit (25 / 50 /
   100 / 200) and resets to 25 on leaving. Rerolls are unlimited while the
   player can pay, because the doubling is the limit.
2. **Prices are anchored on the depth's own income**, which §2a shows cannot
   hold uniformly — the tier-matched item costs about one normal fight in leg 1
   and about 1.6 by leg 5. Taken deliberately; see §2a for the argument.
3. **Spell books are priced by book tier**, flat with depth. A book is worth the
   same whenever it is bought because it lasts the whole run either way.
4. **Buying a relic appends to `run.relicIds`** with no cap, per F8. There is no
   slot to fill and none to invent.
5. **Sell is bag-only** (`save.stockpiledItems`); worn gear must be unequipped
   through the character sheet first. §2c gives the reason.
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
   shows OWNED rather than being filtered out, because ownership is per
   character and the shop is shared.

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
- **`ItemDefinition.cost` and `startingStock`** are v1 store residue with no
  live reader for the equipment case (F6). Once the shop ships and consumables
  read `cost` for real, the field is half-live — worth an `AUDIT.md` entry
  saying which half.
