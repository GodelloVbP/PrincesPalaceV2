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

**Revised 2026-09-03 against the shop's interactive prototype**
(`docs/handoffs/shop_v2/Shop Screen v2.dc.html`, `README.md` §0). The
recipient strip and replace picker did not survive design review inside the
shop — buying a book is now a plain purchase, and assignment moves to the
character dossier as a new, related deliverable (§1g, Phase C.5). The detail
panel becomes a hover/focus tooltip and drops its per-recipient gear
comparison (§3d). Nothing about the pricing, the roll, the spell content or
the Phase A-E staging changes; this revision touches where the recipient
interaction lives and how it's shown, not what it decides. Total build time
moves from 9.5 to 11 days (§4).

**Revised 2026-09-03 (b) against a product review of the plan above.** The
review's verdict was "technically diligent but product-risky": the plan
proves implementation correctness before proving the shop creates decisions.
Most of it is accepted and it changes the build order, the offer counts, the
interaction model and the reroll. What is accepted, what is not, and why, is
§7 at the end of this document; the gate structure that replaces §4's phases
is §7.4. **Where §7 and an earlier section disagree, §7 wins** — earlier
sections are left standing as the record of how each number was derived, not
rewritten, and each superseded paragraph is marked inline.

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
spends it. `docs/AUDIT_V1_ARCHIVE.md:129` (#2, "the whole economy is incoherent by ~2 orders of
magnitude") and `AUDIT.md:22` (#37, "the economy is built around a
voluntary-retreat flow the design does not have") are both symptoms of that
absence.

**Consequence for the build:** budget nothing for reuse. Budget for a first
screen of its kind, and expect the pricing to be the contentious part rather
than the plumbing.

### F2. Shop already generates, already renders, already resolves — as an apology

`RoomType.Shop` is listed among the placeholders that "generate, they render,
they can be entered and cleared" (`Domain/Dungeon/RoomType.cs:27-33`). It has a
real generation weight — **revised, table rebalanced since this was
written**: `MiddleRooms` was Fight 52 / Event 14 / Treasure 12 / Shop 6 /
Rest 6 (90 total) when this section's arithmetic below was done; it is now
Fight 250 / Event 7 / Treasure 6 / **Shop 3** / Rest 3 (269 total)
(`Domain/Dungeon/DescentMap.cs:187-194`), rebalanced to correct Fight's
measured share against `DescentRoadVariationTests` — the table's own comment
records the old table measuring 44.2% Fight against a 57.8% raw weight, and
this one measuring 54.6% against a 92.9% raw weight, so raw table weight and
actual room frequency are two different numbers on this table, more so now
than before. **The 7:6:3:3 ratio among Event/Treasure/Shop/Rest is
unchanged** (same 14:12:6:6 ratio, halved), so Shop's *share of non-Fight
rooms* is unmoved; working it through the two measured Fight shares above
puts Shop's share of *all* rooms at roughly 8.8% before this rebalance and
roughly 7.2% after — a real but moderate drop, not the 6-fold one the raw
weights alone would suggest. `RoomResolution.Resolve` returns
`Kind.ShopNotBuilt` (`Domain/Dungeon/RoomResolution.cs:99-100`) and
`RoomResolver.TryMessage` prints `UiStrings.MapRoomShop`
(`Core/RoomResolver.cs:92-94`).

**How often a player sees one.** A leg is 8 steps
(`DescentMap.cs:124 DefaultLegLength`), of which two are forced — elite at
offset 4, boss at 8 (`DescentMap.cs:139-140`). Six columns roll freely, at up
to `MaxColumnWidth = 3` nodes each (`DescentMap.cs:144`). The **0.9 shops
offered per leg** below was derived from the *raw* 6/90 weight, which this
revision's own paragraph above shows is a materially worse proxy for the
current table than it was for the old one — treat this figure as stale
rather than rescaling it by hand, and get it from a real trace instead:
~~At 6/90 per node and ~2.3 nodes a column, a column offers a shop about 15%
of the time, so roughly 0.9 shops offered per leg~~ — and the player only
takes one node per column, so actually visiting one is a choice against a
fight. This was arithmetic off the weights, not a measurement, and the
weights it was arithmetic off no longer apply; the bot's `runs.jsonl` records
`roomType` only for fights, so a real count needs a batch that traces every
room — more so now than when this paragraph was first written, since the
raw-weight shortcut it took has gotten less reliable, not more.

**Now traced, not estimated.** A real 2000-seed x 5-legs-per-seed trace
(`GenerateLeg` output, every node counted, not the table weight) put Shop at
6.1% of all nodes and found **31% of legs had no Shop node on any branch at
all** — a player could clear a whole leg, boss included, without a shop
option ever appearing. Decision: one Shop guaranteed per leg, not a bigger
weight (a weight bump only shrinks the gap, it cannot close it, and every
point taken from Fight to feed Shop erodes the 53-57% Fight-share band
`DescentRoadVariationTests` pins). `DescentMapGenerator.EnsureLegHasShop`
(`Domain/Dungeon/DescentMap.cs`, run after `EnforceEveryRoadHasVariety`)
retypes exactly one non-forced, non-entry node to Shop on any leg that rolled
none; a leg that already has one is untouched. Tested in
`DescentRoadVariationTests`.

Nothing about this changes. The build replaces one enum case's meaning, not the
map.

### F3. The arrival seam is one enum with three values, and it needs a fourth

`RunOrchestrator.Arrival` is `Refused | Fight | Resolved`
(`Core/Bot/RunOrchestrator.cs:171-183`), and `ArriveAt`
(`RunOrchestrator.cs:190-214`) is the single body both callers go through:

| Caller | Line |
|---|---|
| the screen | `Core/MapController.Walk.cs:172` |
| the bot | `Core/Bot/BotRunDriver.cs:365` |

`ArriveAt` currently branches once — fight or not — and everything not a fight
is resolved on the spot with `RoomResolver.Resolve` + `ClearCurrentRoom`
(`:206-213`). A shop is the second room that leads to a screen and therefore
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
**Spell** axis for a Nature-typed caster (`Domain/Combat/SkillResolution.cs:161`
— not `:106-110`, which is an unrelated `GiftMana`/`GiftFury`/`GiftHaste`
`case` block).
A Spell-axis skill's damage rides `actor.SkillScaling`
(`SkillResolution.cs:165`), which is built once per fight from
`ContentDatabase.Effective.cs`'s `EffectiveSkillScaling(character)`
(`:653-676`): `tier.scaling` — the `intelligence`/`wisdom` grade off the
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
| `Core/FightEncounterAdapter.cs` | both `KitFor` overloads (`:569-604` definition-only, `:630-643` `Character`), the tier lookup inside each (`:598`, `:641`), `SpellTierFor` (`:654-655`) — all three of the original citations (`:582`, `:621`, `:628-635`) pointed at other code by the time this was checked | both `KitFor` overloads' basic-spell lookup, `TierAtLevel`, `SpellTierFor` | removed |
| `Domain/Combat/Session/CombatantKit.cs` | `:48`, `:54`, `:63` | the `BasicSpell` field and ctor parameter | removed |
| `Domain/Combat/Session/FightSession.Skills.cs` | `:870` (cost), `:872` (name), `:874-875` (afford), `:884-885` (preview), `:923` (multiplier) — the whole cluster had drifted from the original `:852`-`:907` citations, which now sit inside an unrelated `ActorAttackType` comment | seven members: cost, name, afford, preview, multiplier | removed |
| `Domain/Combat/Session/FightHudModel.cs` | `:35`, `:46`, `:55`, `:102`, `:104`, `:155-167` | `IsBasicSpell`, `BasicSpellRow`, `SkillRowCount`, the appended row | removed |
| `Core/FightController.Hud.cs` | `:917` (was `:894`, now a comment) | the detail panel's POWER stat | removed |
| `Domain/Bot/FightAction.cs` | `:15`, `:76`, `:114-122`, `:166` | the `BasicSpell` action kind | removed |
| `Domain/Bot/FightRunner.cs` | `:228-229` (the `:219` citation is now a comment two lines above the actual case) | trace naming (`"Skill:" + BasicSpellNameFor`) | removed |
| `Domain/Bot/GreedyAggressivePolicy.cs` | `:45`, `:81`, `:94-95` | scoring | removed |
| `Domain/Bot/GreedyDefensivePolicy.cs` | `:149`, `:247-248` | scoring | removed |
| `Domain/Bot/Lookahead2Policy.cs` | `:27`, `:154-156` | scoring | removed |
| `Data/Character.cs` | `:79` | a comment describing the mechanic | rewritten |
| tests | `BotPolicyTests.cs:34-35,115-116,253-254,365-366`, `ChilledStatusTests.cs:233`, and every other `new PlayerKit(...)` site | the ctor's default arg absorbs most, the four `basicSpell:` sites do not | updated |
| `Core/Content/ContentDatabase.Effective.cs` | `EffectiveSkillScaling` at `:653-676`, `EffectiveWeaponScaling` at `:620-636`, `EffectiveSkillDisplayName` at `:682-688`, `GetSpellTierForLevel` called from all three (e.g. `:686`) — **the original four citations (`:569`, `:602`, `:640-675`, `:686`) were paired with the wrong method names**, not just drifted: `:569` sat inside `EffectiveSkillManaCost` and `:602` inside `EffectiveSkillPowerMultiplier`, neither of which this row names | `EffectiveSkillScaling`, `EffectiveWeaponScaling`, `EffectiveSkillDisplayName`, `GetSpellTierForLevel` | **kept, unmodified** — this is the scaling curve, not the appended row |
| `Core/Content/ContentDatabase.Validation.cs` | `:440-475` (was `:440-472`, cut off before the second check's error message) | duplicate-level and must-have-level-1 checks on `spells.json` | **kept** — the ladder is still authored content that needs the same checks |
| `Core/Content/SpellTierDefinition.cs`, `Domain/Content/RawSpellTierEntry.cs`, `SpellTierEntryResolver.cs`, `ResolvedSpellTier.cs` | whole files | the content type | **kept** — still resolves `spells.json` for `EffectiveSkillScaling` to read |
| `Editor/ContentBuilder.cs` | `:22`, `:48`, `:59`, `:436-465` (method body, was `:435-462`) | `BuildSpellTiers` | **kept** |

No `ScalingProfile` needs inventing for a book. A book teaches an existing
`SkillDefinition`, and that definition's own `scalingAxis` already routes its
damage through the untouched ladder above — the same as `shear` or `headbutt`
do today. §1a spells out the consequence for content shape.

### F5. A learned spell is already a `SkillDefinition` — resolving it is not new work, it is the existing skill pipeline with one more unlock route

Under decision 1 (F4) a "spell" that gets learned is one of the five existing
`skills.json` entries. `ResolvedSkill` (`Domain/Content/ResolvedSkill.cs:15-161`,
the whole struct — `:15-80` only reached partway through the field list)
already carries every field it needs, and `FightEncounterAdapter.Resolve`
(`:671-690`, not `:651-670`) already converts a `SkillDefinition` to one —
there is nothing to build here, only a fourth unlock route to add.

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

- `SaveData.stockpiledItems` (`Data/SaveData.cs:163`) is what the character
  sheet paints (`Core/CharacterDossierController.cs:431`), what a taken offer
  is written into (`Core/Bot/RunOrchestrator.cs:530`), what displaced gear falls
  back to (`Domain/Equipment/EquipMove.cs:64` in `TryEquip`, `:84` in the
  sibling `TryUnequip`), and what the debug menu
  grants into (`Core/DebugMenuController.cs:149`).
- `RunSnapshot.inventory` is **vestigial** — `RunManager.cs:223` says so in as
  many words, and the only reader is a stats readout
  (`Core/RunStatsController.cs:135-138`).

`stockpiledItems` is cleared by `EndRun` (`Core/RunManager.cs:228`, not
`Data/SaveData.cs:324` — that line is inside `Migrate()`'s version-bump block,
a different file and a different concern; the move out
of `StartRun` is AUDIT #8, `docs/AUDIT_V1_ARCHIVE.md:200`), so despite living on the profile it
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
   AUDIT #50 (`AUDIT.md:449`) and #51 (`:480`) both confirm there is no cap
   anywhere to lift. So "buying a relic appends to `relicIds`" is not an
   assumption that needs defending; it is the only shape available.
2. `RunOrchestrator.RelicDraftOffer` (`RunOrchestrator.cs:68-107`) already
   filters out already-held relics (`:84-86`) and already draws weighted.
3. `RelicPool.WeightOf` (`Domain/Relics/RelicPool.cs:107-118`, values at
   `:111-115`; was `:95-108`, which trails off before the values) is the rarity
   curve: Common 100, Uncommon 45, Rare 18, UltraRare 6, Mythic 2. Content
   today is 18 relics — 9 common, 3 uncommon, 1 rare, 4 ultrarare, 1 godlike.
4. `RelicDraftController` already **pages** (`:77-79`, `:130-151`; `:76` on
   its own is a blank line), built for the
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
#43 at `AUDIT.md:158`), so a nested screen cannot be captured by name. The
relic draft has the same gap today. Not worth a scene to fix; worth knowing
before someone asks for a screenshot.

Layout is still audited: `UiFrames.All` re-solves every screen at 1920×1080,
2580×1080 (21:9), 1920×1440 (4:3) and 1920×1200 (16:10)
(`Domain/UiKit/UiFrames.cs:16-22`), and a nested tree is part of the tree it
nests in.

---

## 1. The spell-book system

**Revised 2026-09-03 against the shop prototype.** §1a-§1f describe learning a
spell as one act: buy it, name a recipient, done. The shipped prototype
(`docs/handoffs/shop_v2/Shop Screen v2.dc.html`) splits that into two acts on
two screens — the shop sells the book, the dossier assigns it — because the
recipient strip and replace picker did not survive the layout review. §1a-§1f
are unchanged; a `bookOnly` skill and how it reaches a kit once learned are the
same regardless of which screen does the learning. §1d's `LearnSpell` and
`CanLearn` are unchanged too — they are still exactly what the dossier calls.
What changes is §1b (a book now has a stop-over between "bought" and
"learned") and where §1d is called from (§1g, new).

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
2. **`AvailableSkillsFor` sorts on `unlockLevel`.** `ContentDatabase.cs:249-250`
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

**`ContentDatabase.AvailableSkillsFor` (`Core/Content/ContentDatabase.cs:207-252`)
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
    public List<string> unassignedSpellBooks = new List<string>();   // skillIds, added 2026-09-03

[Serializable] class LearnedSpellEntry { public string characterId; public string skillId; public int slot; }
```

**`unassignedSpellBooks` is new, per the revision at the top of §1.** A shop
purchase no longer names a character, so a bought book needs a home between
`SOLD` and `LearnSpell` — a flat `List<string>` of skill ids, one entry per
copy owned and not yet placed. Not a set: buying `lightning_bolt` twice to
teach it to two different characters is legal (nothing prevents two characters
from knowing the same spell — §1c/§1d have no such rule), so two purchases are
two list entries, and the dossier's assignment panel removes one entry per
`LearnSpell` call. A duplicate sitting unassigned does **not** count as "every
fielded character already knows it" for §2d's roll-exclusion rule — it teaches
no one until it is placed, so the roll must keep offering it (or others of the
same tier) exactly as if the player owned nothing.

Purely additive, same reasoning as `learnedSpells` itself: `SaveData.CurrentVersion`
does not move, an older in-flight run's `JsonUtility` deserialize leaves the
list empty, and empty is the correct reading — nothing bought, nothing
unplaced. It is reconciled by the same rule as `learnedSpells`, below.

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
`Data/SaveData.cs:436-438` — but **dropping, not deleting silently into a void**: a
learned spell that vanished because content was edited under a live run should
free its slot, which is what dropping the entry does. `unassignedSpellBooks`
gets the identical treatment: an entry whose `skillId` no longer resolves is
dropped, which reads as "the book was never bought" rather than as an error.

### 1c. How a learned spell reaches the kit

No separate insertion point is needed. §1a's fourth `AvailableSkillsFor` route
*is* the mechanism — `FightEncounterAdapter.KitFor(Character, ...)`
(`Core/FightEncounterAdapter.cs:630-643`, not `:610-621`, which is now
comment text) already builds `skills` from
`ContentDatabase.AvailableSkillsFor(character)` and passes it straight to
`new PlayerKit(...)`; nothing at that call site changes. This is smaller than
the original plan, which had the adapter append a separately-resolved book
list after the authored skills — that extra step is gone because a learned
spell is not a separate kind of thing to resolve, it is a `SkillDefinition`
that has become reachable.

The `basicSpell` argument at `:641` (not `:621`) still becomes `null` and then goes away
with the parameter — that part is F4 (revised), unrelated to how a learned
spell reaches the kit.

### 1d. Learn / replace

```
LearnSpell(characterId, skillId):
    entries = learnedSpells where characterId matches
    if entries.Count < 3     -> append at the lowest free slot index
    else                     -> caller must supply a slot to overwrite;
                                 the displaced slot's skillId returns to
                                 run.unassignedSpellBooks (§7.1 point 5) —
                                 it is not destroyed
```

Two calls, not one: `CanLearn(characterId)` returns the free slot or -1, and
`LearnSpell(characterId, skillId, slot)` writes. **Per §1g, the caller is now
the dossier's assignment panel, not the shop** — it asks first, shows the
replace picker when the answer is -1, and calls with the chosen slot. The bot
policy answers the same question without a screen, via `ChooseSpellAssignment`
(§1g, §2g). **Replacing a learned spell returns the book it displaces to the
pool** (§7.1 point 5): the slot's previous `skillId` is appended to
`run.unassignedSpellBooks` before the new one overwrites it, so reassignment
is free and reversible rather than a destroy path.

Refuse a duplicate: learning a spell already in one of that character's three
slots is a no-op that returns false, and the assignment panel reads OWNED for
that character (moved from "the shop card reads OWNED", since the shop no
longer knows who owns what — §1g). Not a hard error — content can change under
a run — but not a silent success either.

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
   have rather than watch the list change length" (`FightHudModel.cs:108-111`)
   — a verb that vanishes is the same defect one level up.
2. Every bot policy that scored a `BasicSpell` action must tolerate an actor
   whose `Skills` list happens to be empty. `FightAction.cs:114-122` already
   guards on `KitFor(actor)?.BasicSpell != null` before offering that action;
   removing the `BasicSpell` kind (F4) removes the guard's subject too — but
   `GreedyAggressivePolicy.cs:45` and `GreedyDefensivePolicy.cs:149` filter
   action lists that could now be attack-only for a kitless actor in principle.
   Pin it with a test: a kit with no skills produces a legal action list and a
   policy picks from it.

**What the character sheet shows:** the 3-slot strip per character — designed
at `docs/handoffs/shop_v2/README.md` §3 "Row A" for the shop, relocated to the
dossier by §1g below — reads `learnedSpells` directly — `EMPTY` for an
unfilled slot, the spell's display name for a filled one. It is unaffected by
whether the character's ordinary Skill submenu happens to be empty; the two
are different lists (`AvailableSkillsFor`'s full result vs. the run's
`learnedSpells`).

### 1g. Where assignment happens now

**New 2026-09-03**, against the shop prototype dropping the recipient strip
(top of §1). Buying a book (§2, §2f) only ever appends a `skillId` to
`run.unassignedSpellBooks` — it never calls `LearnSpell`. Something still has
to call it, on a screen that has somewhere to show three characters' three
slots each, which the shop's five-panel grid does not have room for and the
shop prototype does not attempt.

**That screen is the character dossier**
(`Domain/UiKit/Screens/CharacterDossierScreen.cs`), not a new one. It already
has the layout precedent this needs — a three-column sheet with an existing
"pack" section (`DossierLayout.PackSortRowHeight`/`PackSortCentreY`/
`PackIconCentreY`/`PackNameCentreY` — not `PackSortRow`, which isn't an actual
symbol in this file — `CharacterDossierScreen.cs:358-450`) whose *controller*,
`CharacterDossierController.cs`, browses `stockpiledItems` for it (the screen
file itself only lays the row out) the same
way the shop's Pack modal does, so a spell-books panel following the same
row-list convention is additive to a screen that already speaks this
component's language, not a new interaction vocabulary. **This is not
optional follow-up work** — README §8 says so outright now — a book that can
be bought but never assigned is not a shipped feature, only a shipped half of
one.

**The panel reuses README §4/§5.3 verbatim, rehosted.** Those sections were
marked superseded-by-relocation, not wrong: the recipient-eligible/inert row
states, the `OWNED BY {NAME}`/`OWNED BY {n}` badges, the green
would-fill/red would-replace slot chip previews, `REPLACE REQUIRED`, the
`ReplaceSlotPicking` sub-state and its `REPLACING` label all transfer as
written, because the recipient row and its three slot chips are the same
component regardless of which screen hosts them. What changes in the move:

- **Source list.** The strip no longer reads "the shop's rolled spell cards";
  it reads `run.unassignedSpellBooks`, deduplicated for display (badge:
  `LEARN ({n} OWNED)` when more than one copy of the same skill is unplaced)
  but each copy is consumed independently — placing one copy of a duplicate
  entry leaves the rest in the pool for another character.
- **No purchase context.** README §5.3's "arming a spell card" step does not
  exist here; a book in this list is already owned. Pressing it directly does
  what arming + confirming a recipient row used to do: `CanLearn` /
  `LearnSpell`, per §1d.
- **Row count is still three, pinned the same way (§3e below)** — the dossier
  shows one character at a time already (`DossierLayout`'s column structure),
  so "the recipient strip" here is really "this character's own three slots",
  simpler than the shop version which had to show three characters' slots at
  once. `ReckoningTests.cs:183`'s pattern (assert against
  `EffectiveMaxSquadSize()`, never a literal) still applies to the count of
  *character columns/pages* the dossier itself already handles, not to a new
  count this panel introduces.
- **Bot side:** `IRunPolicy.ChooseShop`'s old `buy-spell(i, characterId,
  replaceSlot)` choice becomes two things — §2g details it — a plain
  `buy-spell(i)` and a new `ChooseSpellAssignment` call, both still made by the
  bot, just no longer required to happen in the same orchestrator call. The
  bot has no UI to walk to, so nothing stops it from resolving both in the same
  turn; the human player's version of "both" can span visits to two different
  screens, possibly two different rooms.

**Layout, states table and worst-case strings for this panel are not specced
here** — README §8 lists it as required but out of scope for *this* handoff's
coordinates, and it needs its own pass against `DossierLayout`'s existing
geometry before Phase C.5 (§4) starts. What is settled is the shape (a
relocation of README §4/§5.3, not a redesign) and the data it reads
(`unassignedSpellBooks`), so that pass has a spec to extend rather than a
blank page.

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
not — though this has been rebalanced once since it was measured.** A normal
fight pays 16 in leg 1 and 22-24 forever after; a leg is worth about 150 gold
in leg 1 and about 185 in every leg after. `RarityTable.StepsPerTier` was 8
when this was written, meaning the expected item tier climbed by one *every
leg*; it has since been **doubled to 16**
(`Domain/Rewards/RarityTable.cs:47`), with the file's own comment recording
why — the old value pushed the floor tier ahead of what the design called for
at a given depth. Tier now climbs every **two** legs, not every one. This is
the tier-climb rework §6 files as "out of scope here" — it has, in part,
already happened; §6 should be revisited rather than quoted as still-open.

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
somewhat more of one in later legs** — softer than "progressively more"
originally put it, now that `StepsPerTier` has doubled (§2c): the tier on
offer climbs every *two* legs, not every one, so income per fight stopping
its own climb around step 8-16 outpaces a slower-moving tier by less than
this section first found. §2c's reworked table has the actual numbers: the
gap between a leg-1 common and a leg-5 common shrank from a 67% price
increase to 40% once the tiers those steps actually stock were corrected.
Nothing about the pricing formula needs to change to make the shape true, and
nothing about it should change to make it false — a depth term in the price
would double-count what `FloorTier` already does on the stock side. **Price is
tier-and-plus-and-rift, full stop; stock-tier-by-depth is a separate, stock-only
rule (§2c/§2d).**

### 2b. Pricing formulas

**Superseded 2026-09-03 (b) — see §7.1 point 7.** `RerollPrice` below is
whole-shop, at `25 * 2^n`. The product review moved reroll to per-section,
at `15 * 2^n` per section, three independent counters.

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

**Superseded 2026-09-03 (b) — see §7.1 point 7.** The ceiling logic below
still holds at the new `15 * 2^n` per-section rate; only the base and the
whole-shop framing are stale.

**The reroll ceiling is a display fact before it is an economy fact.**
`25 · 2^n` passes 9999 at n = 9 (12,800) and overflows a signed int at n = 27.
Nothing in the run stops a player pressing the button, so the price has to stop
growing somewhere the label can still print it — `UiTextFitAudit`
(`SceneBuilder.cs:115`) sizes the reroll button for four digits plus a suffix,
and a five-digit price fails that audit rather than merely looking wrong.
Saturating at 9999 costs nothing in play: it is roughly eleven legs of income
at the measured ~185 a leg (§2a), which is more than a 40-step run earns in
total. **Past the ceiling the button stays visible and unaffordable** rather
than disappearing, for the same reason the skill submenu keeps unaffordable
rows — "the player should learn what they have rather than watch the list
change length" (`FightHudModel.cs:108-111`).

**Superseded 2026-09-03 (b) — see §7.1 point 7.** `shopRerollsUsed` becomes
`int[]`, one counter per section, not the scalar below.

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
`RarityTable.FloorTier(step) = step / 16` (`RarityTable.cs:51-54`). **Revised
against the current divisor** — this table originally kept the same three
reference steps (8/24/40) under the old `/8` divisor, where they landed on
tier 1/3/5; against the current `/16` they land on **tier 0/1/2**
(`FloorTier(8)=0`, `FloorTier(24)=1`, `FloorTier(40)=2`). The reference steps
are kept as-is rather than rescaled, because they're anchored to §2a's
measured income data (the same S8/S24/S40 columns), not to the tier ladder —
only the tier each one stocks, and every price that follows from it, has
moved. Recomputed below from §2b's formulas; the "+1 tier" row is now the
stretch a shop can offer, same as before, just numbered two lower.

**One more correction while rebuilding this:** the old "Very rare: +3, 2
affixes" row's values (82/109/136) were never actually `+3`/2-affix numbers —
they were `BaseFor(tier) × 3.40`, which is `+2`/2-affix
(`PlusFactor(2) × RiftFactor(2)`), not `+3`/2-affix
(`PlusFactor(3) × RiftFactor(2) = 4.10`). A pre-existing label/value mismatch,
independent of the tier issue — the label is the more specific, informative
half, so recomputed to match the label rather than the old numbers.

| | **step 8** (tier 0) | **step 24** (tier 1) | **step 40** (tier 2) |
|---|---|---|---|
| *normal fight pays* | 16-19 | 22-24 | 22-24 |
| *elite pays* | 31-33 | 44-50 | 44-50 |
| *boss pays* | 68-76 | 76-82 | 76-82 |
| *a leg is worth* | ~150 | ~190 | ~190 |
| Common gear, tier-matched, +0, no affix | **20** | **24** | **28** |
| Same, +1 | 27 | 32 | 38 |
| Same, +2 | 34 | 41 | 48 |
| One tier up, +0 | 24 | 28 | 32 |
| Rare: tier-matched, +2, 1 affix | **51** | **61** | **71** |
| Very rare: +3, 2 affixes | 82 | 98 | 115 |
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
because depth enters the formula. Spell, relic and reroll prices don't move
with any of this — they're not gear, so `FloorTier` never touches them.

**Reading the anchors, corrected:** at step 8 a tier-matched common is
**20** against a 16-19 fight — about one fight's payout, not comfortably
under it the way the old (tier-1) numbers read — and a **51** rare against a
68-76 boss, which lands with more room than before (61 against the same
boss). At step 40 the tier-matched common is **28** against a 22-24 fight,
roughly 1.2× a fight's payout. **The climb from step 8 to step 40 is now
much gentler than this section originally found**: the common-gear price
rose from 24 to 40 (a 67% increase) under the old tier assignment, against
fight income that only moved from 16-19 to 22-24 over the same span — the
"costs progressively more of a fight's payout" finding this section built
its narrative around. Under the
current tiers it rises from 20 to 28 (a 40% increase) over the same span,
against the same income — still a real climb, but a noticeably smaller one.
Doubling `StepsPerTier` didn't just relabel the columns; it softened the
exact economic pressure this section was written to describe. A Mythic
relic at 460 is about 2.5 legs of saving: a run-defining purchase, reachable
but never casual — unaffected by any of this, since relics don't scale by
tier. Spell prices are flat across all three columns by construction (§2b) —
a spell is worth the same whenever it is bought.

Sell prices at 30%: a tier-matched common sells for 6 / 7 / 8; a +3
two-affix piece for 25 / 29 / 35 (the last is an exact rounding-mode
midpoint, 34.5 — confirm which way `ShopPricing`'s actual `Math.Round` call
breaks before pinning it as a literal test value, per §2b). Selling the
whole bag never funds a relic, which is the "not supposed to make you rich"
instruction holding.

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
   (`Domain/Equipment/EquipMove.cs:64`; `:84` is the mirror displacement in
   the sibling `TryUnequip`, not this method), and `EquipmentOps.Equip`
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
slot (`Core/Bot/RunOrchestrator.cs:521-544`), on the stated reasoning that a
piece filling a hole in the loadout carries no decision. A purchase is a
different act: the player already decided, with money, against three other
cards, and having the game then place the item is a second decision taken on
their behalf at the moment they are most likely to be mid-plan. So a shop buy
calls `InventoryOps.Add` and stops. Worth stating outright because `TakeOffer`
looks like the obvious method to reuse and is the wrong one.

**Revised 2026-09-03: a spell book purchase carries the same non-decision
one step further than this paragraph originally argued — it does not even
name a character.** Under the shop prototype (§1, top) a book purchase
appends to `run.unassignedSpellBooks` and stops; `LearnSpell` is a separate
act on a separate screen (§1g). The reasoning above still holds for *why* a
purchase should not auto-place its own outcome; it now applies one step
earlier than when this paragraph was written.

### 2d. Stock rolling

```
stream = RngStreams.ShopGear (= 5) | ShopBooks (= 6) | ShopRelics (= 7), one per section
rng    = RngStreams.Open(run.runSeed, RngStreams.ShopGear, run.step, run.currentNodeId, run.shopRerollsUsed[section])   // corrected 2026-09-03 (b): three coordinates, one stream per section (§7.1 point 7)
```

Per F9. Same node, same stock, forever — quitting mid-visit and returning finds
the shop as it was, and re-entering a cleared shop is impossible because the
room clears on leave.

**Superseded 2026-09-03 (b) — see §7.1 point 3.** The counts below are
3 spells / 4 items / 3 relics. Gate 1 re-decided them as **Gear 3 / Relics 2
/ Books 1**.

What it rolls, in one pass:

| Section | Count | Source |
|---|---|---|
| Spells | 3 | weighted over `ContentDatabase.Skills` filtered to `bookOnly`, excluding a spell already learned by every fielded character (§1a/§4 "Spell card — Owned by everyone") |
| Items | 4 | `ItemOfferRoll.Candidates()` through `ItemOfferTable.Choose(candidates, FloorTier(step), MaxTier, next, 4)`, then `RarityTable.RollPlus` + `ModifierTable.RollRiftTier`/`PickModifiers` per item — the identical three-axis roll `ItemOfferRoll.Roll` does inside its `foreach` at `Core/ItemOfferRoll.cs:194-211` (the method itself spans `:164-214`; `:154-168`, cited before, is mostly `Roll`'s doc comment), with `count` 4 |
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
two cannot drift (`ScreenRegistry.cs:26-30`).

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

**Superseded 2026-09-03 (b) — see §7.1 point 4.** The "no ownership badge"
sentence below is reversed: the book card now shows `KNOWN BY …`,
`ELIGIBLE k/m`, `ALL SLOTS FULL` and `1 UNASSIGNED COPY` at purchase time.

**Books with no eligible fielded recipient are filtered out at roll time.** A
book that every fielded character has already learned cannot be bought by
anyone, so offering it is a dead card taking a live card's slot; the roll drops
it, exactly as the relic draft drops a held relic
(`RunOrchestrator.cs:100-107`). "Fielded" is `SaveData.ActiveSquad`, capped at
`EffectiveMaxSquadSize()` (`Data/SaveData.cs:193-199`) — **two** today, see
§3e. A book known by *some* fielded character is still offered — **revised
2026-09-03**: the shop card carries no ownership badge for it (that
information moved to the dossier with the recipient strip, §1g); a player who
buys a second copy for a character who already knows it only finds out at
assignment time, via §1d's duplicate refusal. README §4's `OWNED BY {NAME}`/
`OWNED BY {n}` badge rows are relocated, not built into the shop card.

### 2e. Persistence

**Superseded 2026-09-03 (b) — see §7.1 point 7.** `shopRerollsUsed` below is
a scalar. It becomes `int[] shopRerollsUsed`, length `ShopStock.SectionCount`,
one counter per section.

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
takes this posture for `stockpiledItems` (`Data/SaveData.cs:436-438`) and for the
relic loadout (`:557-559`, not `:520-526` — that range is now the
displaced-equipment-into-bag block, a different reconciliation), and the shop gets the same treatment:

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
`ScreenDef.CountBindings` exists to catch (`ScreenRegistry.cs:26-30`).

**The run persists on arrival, before the shop resolves.** This is not a new
rule: `RunOrchestrator`'s own header states it — "the run persisted on arrival
BEFORE the room resolves" (`RunOrchestrator.cs:35`) — and it is why quitting
inside a shop cannot reroll it. `RunManager.MoveTo` already persists; the stock
roll must happen after that call and be persisted itself.

### 2f. Every mutation is one method, one order, one save

**Revised 2026-09-03: five shop mutations, not six.** "Learn into a free
slot" and "replace a learned spell" are no longer shop mutations — §1g moves
them to the dossier, where they are still exactly this pattern (validate,
apply, persist once), just called `AssignSpell`/`ReplaceSpell` and owned by
`CharacterDossierController` rather than `RunOrchestrator`'s shop half. Buy
gear, buy a relic, buy a book, sell, reroll — each is **one** `RunOrchestrator`
method, and each has the same three-part shape:

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
| Buy book | deduct `run.gold` → mark `sold` → append `skillId` to `run.unassignedSpellBooks` → persist |
| Sell | `InventoryOps.TryRemoveAt` → **only if it returned true**, credit `run.gold` → persist |
| Reroll | deduct `run.gold` → increment `shopRerollsUsed` → re-roll `shopStock` at the new third coordinate → persist |

The dossier's two mutations follow the identical shape, on a different list:

**Superseded 2026-09-03 (b) — see §7.1 point 5.** The "Replace a learned
spell" row below destroys the displaced book — it only removes the incoming
`skillId` from the pool and overwrites the slot, with nothing returned for
what was there before. Corrected order: remove one matching incoming
`skillId` from `run.unassignedSpellBooks` → append the *displaced* slot's
`skillId` to `run.unassignedSpellBooks` → overwrite the slot → persist.

| Mutation | Order |
|---|---|
| Assign to a free slot | remove one matching `skillId` from `run.unassignedSpellBooks` → append `LearnedSpellEntry` → persist |
| Replace a learned spell | remove one matching `skillId` from `run.unassignedSpellBooks` → overwrite the chosen slot's `skillId` → persist |

Both refuse in step 1 (§1d's duplicate rule) rather than partially removing
from one list and failing to write the other — same "step 2 cannot fail"
discipline as below, on a smaller state.

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

`ArriveAt` (`RunOrchestrator.cs:190-214`) gains one branch beside the fight
branch: roll-or-restore the stock, persist, return `Arrival.Shop`, and — like
the fight branch — **do not** call `ClearCurrentRoom`. The room is cleared when
the player leaves, through a new `RunOrchestrator.LeaveShop()` that clears the
stock, resets `shopRerollsUsed`, and calls `RunManager.ClearCurrentRoom()`.

`MapController.Walk.Arrive` (`Core/MapController.Walk.cs:172`) gains
`case Arrival.Shop: OpenShop(); return;` — a nested panel, so no
`Navigation.Go`. Its `default:` arm must stop being the catch-all that swallows
a new value.

`BotRunDriver` (`Core/Bot/BotRunDriver.cs:360-385`) currently treats "not a
fight" as "continue". It gains a shop branch that calls the policy, and the
`isFight` flag at `:360` (not `:358`, which is the prior statement's closing
`};`) is no longer a two-way split.

Buy/sell/reroll bodies go in `RunOrchestrator` beside `TakeRelic`
(`:112-123`), not in the controller — same reason the relic commit moved there:
the bot is the second caller and a second copy is a second rulebook.

`IRunPolicy` (`Domain/Bot/IRunPolicy.cs:17-48`) gains:

```
ShopChoice ChooseShop(ShopView shop, RunView view, SeededRandom rng);
```

returning one of buy-item(i) / buy-spell(i) / buy-relic(i) / sell(bagIndex) /
reroll / leave, called in a loop until it says leave or the purse refuses.
`RunView` already carries `Gold` (`Domain/Bot/RunView.cs:17`), so the policy
can already see what it can afford. Cap the loop — a policy that never says
leave is a hang, and `BotRunDriver` has no timeout for it.

**Revised 2026-09-03: `buy-spell` dropped its `characterId`/`replaceSlot`
arguments**, per §1g — the shop mutation no longer takes them. A second method
covers what they used to do:

```
SpellAssignmentChoice ChooseSpellAssignment(RunView view, string skillId, SeededRandom rng);
```

returning assign(characterId, slot) or skip. `BotRunDriver`'s shop branch
calls it once per unassigned book still in `run.unassignedSpellBooks` after
the `ChooseShop` loop exits (a purchase from this visit or a leftover from an
earlier one — the bot doesn't distinguish, and neither does the dossier). The
bot has no dossier to walk to, so it resolves every pending assignment in the
same turn it left the shop; a human player's two acts can be separated by any
number of rooms. That asymmetry is real and worth a comment at the call site,
not a bug to chase — the bot's `runs.jsonl` should therefore be read as "spells
assigned as soon as possible," not as a measurement of how promptly a player
would actually visit the dossier.

---

## 3. UI

### 3a. The tree

`Domain/UiKit/Screens/ShopScreen.cs`, built by `ShopScreen.Build()`, nested
inside `MapScreen.Build()` as `screen.Shop` — the pattern
`HubScreen.cs:53`/`:156-157` uses for `RelicDraftScreen`.

Sections and coordinates are the designer brief's job
(`docs/handoffs/shop_v2/README.md`), not this document's. What the plan fixes:

- Every card, row and slot chip is a **fixed-count array** emitted at build
  time and hidden when unused. Scenes are generated once; a runtime count
  cannot widen a tree. This is F3 of `PLAN_PROGRESSION_TRACK.md` restated.
  The counts are `ShopStock`'s three constants (§2d), and because a section's
  cards can never outnumber its own constant there is **nothing to page** —
  the relic pager is removed (README §3).
- Every such array is declared to `UiCountAudit` through
  `ScreenDef.CountBindings` (`ScreenRegistry.cs:26-30`) — **revised 2026-09-03**
  to four arrays, not five: spell cards ×3, item cards ×4, relic cards ×3, and
  sell rows (now inside the `PACK` modal rather than an inline viewport, still
  a fixed count against a capped view of `stockpiledItems`). The fifth array
  the original plan counted — slot chips at `RecipientRowCount` ×
  `MaxSpellSlots` — does not exist on this screen; it moves to the dossier's
  assignment panel (§1g), which needs its own `CountBindings` entry when that
  panel is built. The audit comment names the shipped Store bug that pairing
  exists to prevent.
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

Text fit is checked by `UiTextFitAudit` (`SceneBuilder.cs:115`), which is why
price labels get a width sized for four digits and a suffix, not for today's
two.

### 3f. Layout in pixels, and the two ambiguities the prototype's code resolved without deciding them

**New 2026-09-03.** `Shop Screen v2.dc.html` lays out in CSS `fr`/`minmax`
columns that carry no pixel budget — nothing in it tells `UiTextFitAudit`
what width a price chip gets. This section is that budget: a 1920×1080
reference layout for the five-panel grid (README §0.6), built from the
prototype's own column ratio (`1fr 1fr 1.3fr`, two rows) so the proportions
match what was reviewed, with the price/meta field widths taken **verbatim**
from README §3's original worst-case-string table wherever the field is the
same field, since that analysis didn't stop being correct when the panel
around it changed shape.

**The five panels**, 40px outer margin, 24px gutter:

| Panel | (x, y, w, h) |
|---|---|
| Title `SHOP` | 40, 40, 1840, 48 (centred) |
| Relics | 40, 112, 543, 452 |
| Spell Books | 607, 112, 543, 452 |
| Shopkeeper art | 1174, 112, 706, 452 |
| Gear | 40, 588, 1110, 452 |
| Shop Actions | 1174, 588, 706, 452 |

(543+24+543+24+706 = 1840; 452+24+452 = 928; both close the 1840×928 content
box inside the 40px margin.)

**Relics / Spell Books panel** (543×452, 3 stacked cards): header at +16,+16;
cards at +16,+46, each 511×123 with a 10px gap between. Card internals: icon
+12,+16,26,26; name +46,+16,453,26; meta/sub-line +12,+54,300,20; **price chip
+320,+50,180,28**, worst case `CONFIRM · 9999 G` / `🔒 NEED 9999` — the same
180px budget and the same two strings README §3 already established, unmoved
because the chip itself didn't change, only its container did.

**Gear panel** (1110×452, 2×2): header at +16,+16; cards at +16,+46, each
533×189 with a 12px gap both ways. Icon +12,+14,64,64; name +88,+16,433,26;
meta +12,+92,300,20 (worst case `TIER 3 · +2 · 1 AFFIX`, per README §3);
**price chip +341,+88,180,28**, same budget and strings as above.

**Shopkeeper panel** (706×452): header +16,+16; portrait `image-slot`
+16,+46,674,390.

**Shop Actions panel** (706×452): header +16,+16; four stacked controls at
674px wide (gold display, reroll, pack, leave), each with generous headroom
against README §3's worst-case strings (`9,999 G`, `REROLL · 9999 G`) since
674 is well past the 240/260px this content needed in the original narrower
header layout — this panel is not the tight fit anywhere in the screen.

**Pack modal**, centred overlay: 640×700 at (640, 190). Header 640×64 (title +
close). Row list at +24,+80, rows 592×56 with an 8px gap: name +12,+16,220,24;
meta +244,+19,220,18 (worst case `T10 · +5 · 3 AFFIX`, needs 148 per README
§3 — 220 is headroom, not a new number); price +476,+16,116,24, right-aligned
(worst case `SELL · 9999 G`, needs 148 — **this one is tight**: 116 is
*less* than README's own 148 finding for the identical string. Widen the
price field to at least 148 before implementation; this is flagged, not
silently narrowed). Armed, stack of 2+: the meta+price region (336px) splits
into two quantity chips at 163px each with an 8px gap, worst case
`ALL 99 · 9999 G` (needs 148 per README §3 — 163 clears it).

**Tooltip**: fixed 320×auto (not the prototype's 280 — 320 gives the
longest spell effect line room without wrapping past 3 lines), positioned
per §3d, no single-line truncation on the body (title and kicker still
truncate at 288px inner width, matching the card name fields they mirror).

**Superseded 2026-09-03 (b) — see §7.1 point 6.** Both ambiguities below are
moot: the per-section "arming" model they argue about is gone. Single global
selection means there is at most one selected card to clear — click outside
or Escape simply clears it, no per-section disarm and no priority order to
pick between.

**The two ambiguities:**

1. **Click outside disarms the section that's armed**, on the main screen as
   well as inside the Pack modal — README's state table already specs this
   for every section; the prototype only wires it for the modal's own
   backdrop. Implement a single screen-level "pointer down outside any armed
   card or the Pack modal" handler that disarms whichever section(s) are
   armed, rather than one handler per section.
2. **Escape disarms every currently-armed section at once**, not the first
   one found in a fixed priority order. Sections arm independently (README
   §5.1's "arming is per section"), so a player can legitimately have a relic
   and a spell both armed at the same moment; a single Escape press should
   return the whole screen to Idle rather than requiring one press per armed
   section. This is a decision the prototype's code made implicitly (`handleEscape`
   returns after the first match) and gets no vote from the README either —
   settled here as the more predictable of the two readings, not inherited
   from either source.

### 3d. The detail panel, and keyboard focus

**Revised 2026-09-03: the prototype took the other shape.** The two
established options below were both real precedent when this section was
first written, and the prototype (`docs/handoffs/shop_v2/Shop Screen v2.dc.html`)
picked the second one, not the first. This section now specs the tooltip, and
records why the containment argument for the panel no longer decides
anything.

A card is 380×236 and carries an icon, a name, a sub-line and a price. That is
enough to tell two swords apart by name and not enough to choose between them.
Every other screen in the suite that asks the same question answers it the same
way, and there are two established shapes to copy from:

- **A persistent panel in reserved space** — `TalentScreen`'s
  `DetailName`/`DetailBody` plus its kicker, price and refusal lines
  (`Domain/UiKit/Screens/TalentScreen.cs:98-99`, `:308-350`). Rejected: the
  shop's five-panel grid (README §0) has no reserved block left for it — the
  space this would have used is the recipient strip's, and the strip is gone,
  not shrunk.
- **A floating tooltip** — the dossier's and the reckoning screen's
  (`CharacterDossierScreen.cs:124-126`, `:795-814`;
  `ReckoningScreen.cs:163-164` for the tooltip fields — not `:158-159`, which
  is `RowBarBefores`/`RowGains`). **Chosen**, matching the prototype. Authored
  at `Place.At(0, 0)` and moved at runtime so the containment audit can still
  solve it, exactly as `ReckoningScreen`'s own header explains at `:746-751`
  and again at `:806-811` (`:705-720`, cited before, is `RowNames`/`RowLevels`
  plus the start of an unrelated const comment — not this explanation).

**Positioning has no mouse to anchor to, unlike the web prototype.** The
prototype places its tooltip at `evt.clientX + 16, evt.clientY + 12` — a
runtime mouse coordinate that does not exist for a keyboard-focused card, and
the prototype does not attempt keyboard focus at all (`Prototype Notes.md`
never mentions it; it is absent from the mock, not deferred). The engine
version cannot copy this and does not need to: every card's `RectTransform`
is already known at build time, mouse or not. Anchor the tooltip to
**whichever card currently has focus-or-hover** (focus wins when both are
set, same rule as before) at a fixed offset from that card's own rect —
below-right, clamped to stay inside the panel. **The "`ReckoningScreen`'s
`AllowOverlap` price tooltip already clamps this way" claim does not check
out** — `:757-785` is header comment for `BuildOfferTooltip` (a
squad-comparison tooltip, not a price one) with no `AllowOverlap` call in it,
and neither of the file's two actual `AllowOverlap` calls (`:384` on the
continue button, `:483` on the frame glow) is a tooltip clamp. There may be no
existing precedent for edge-clamping a tooltip in this codebase to copy —
whoever builds this should verify that before assuming the pattern exists
somewhere to lift. A mouse hover and a keyboard Tab therefore both resolve to
"the tooltip follows
the focused-or-hovered card," they just differ in *what sets focus-or-hover*,
not in how the tooltip is placed once it is.

**Focus order** is sections in layout order, cards within a section in index
order: relic cards → spell cards → item cards → reroll → pack → leave — the
prototype's own left-to-right, top-to-bottom panel order (README §0.6), not
the original spell→recipient→item→relic order, since there is no recipient
row on this screen any more. Tab advances, Shift+Tab reverses, arrows move
within a section, Enter presses. Focus is drawn as an outline (README §6's
`Focus outline` token) — a shape cue rather than a colour change, so it stays
legible on a card that is already armed and already amber. **This is not
optional polish** — Prototype Notes.md files it under "lower-priority", but a
keyboard-only pass through this screen with no focus feedback shows the player
nothing about what they are about to buy, which is a functional gap, not a
finish pass.

What it shows, by the kind of the focused or hovered card:

| Kind | Contents |
|---|---|
| Gear | Equip slot; the item's own stats; the affix names — **no delta**, revised below |
| Consumable | What it does, one line, off the item's own description |
| Relic | Effect text and rarity |
| Spell | Effect, mana cost, cooldown, target shape — every one a field `ResolvedSkill` already carries (`Domain/Content/ResolvedSkill.cs:15-161`) |

**Revised 2026-09-03: no comparison delta, for any card, anywhere on this
screen.** The original design read the gear delta against "the currently
selected recipient" — which meant the spell-slot recipient strip's selection,
piggybacked as a de facto character picker for gear too (`ItemDescription.Compare`,
`Core/ItemDescription.cs:196-227`). With the strip gone (§1, top) there is no
recipient selected anywhere in the shop, spell purchase or not, so there is
nothing left to compare against. This is a real simplification, not only a
loss: `ItemDescription.Compare`, `ResolveTargetSlot`, and the cascade/no-affix/
no-rift-tier caveats the original text spent a full paragraph on are simply
not called from this screen at all. The full compare-against-currently-worn
view is not gone from the game — it stays exactly where it already lives, in
the dossier, where a recipient is selected by the plain fact of being the
column on screen.

**A weapon's tooltip is still the damage line, not a flat stat, and this is
the one place the prototype's own demo data is wrong to copy.** Gear grants no
flat Attack — `ContentDatabase.EffectiveStats` zeroes `bonus.attack` — so a
sword's whole contribution arrives at the `FightEncounterAdapter` seam as
WeaponPower (`ItemDescription.cs:229-248` states this outright). The
prototype's placeholder data (`Shop Screen v2.dc.html`'s `iron_crook:
'+6 ATK / +2 REACH'`) shows a flat stat because it is fake data standing in
for real content, not because the rule changed. For a weapon card the tooltip
shows the damage line `ItemDescription` already builds for the dossier
(`:145-168`); for every other gear card it shows the item's own stats and
affix names, by name, exactly as an affix already prints on the dossier —
never folded into a number by hand, which would be a second implementation of
what an affix does.

### 3e. The recipient strip is sized to the squad the save can field

**Revised 2026-09-03: this section describes the dossier's assignment panel
now, not the shop's — the shop has no recipient strip (§1g).** Kept because
the reasoning is unchanged and the dossier panel needs it, only rehosted.

The designer brief sized the spell-slot strip for five characters. The save
cannot field five. `SaveData.EffectiveMaxSquadSize()` is
`BaseMaxSquadSize + (extra_recruit_slot ? 1 : 0)` (`Data/SaveData.cs:193-199`)
and `BaseMaxSquadSize` is **1** (`SaveData.cs:50`) — the comment there records
that the squad was cut to solo deliberately, that Fly/Dog/Turtle/Owl still
exist and come back the moment it returns to 3, and that buying
`extra_recruit_slot` still reaches **two** today. Two is the real ceiling.

Emit **three** rows [of whatever the dossier's assignment panel turns out to
be — a per-character strip if it shows every character at once, or three
slot-chip rows if it shows one character at a time, per §1g]. Not two, and not
five. That is `ReckoningScreen`'s precedent taken verbatim: `RowCount = 3`
(`Domain/UiKit/Screens/ReckoningScreen.cs:94-101`), pinned by
`ReckoningTests.cs:183` asserting `RowCount >= Save.EffectiveMaxSquadSize()`.
**The comment quoted here has since been rewritten and no longer says "four
leaves headroom"** — worth flagging on its own, since that quote already
contradicted the `RowCount = 3` it was offered as precedent for (it described
a build where the constant was 4). The current comment argues from
`SquadOfThreeReady`'s eventual base of 3 rather than "1 today, target 3," and
`RowCount` itself is confirmed still 3 — the number this section wants — but
the comment records something worth a second look before copying its
reasoning: it says a save that both reaches squad-of-three *and* buys
`extra_recruit_slot` "outgrows this by one row," which reads as 4 needed
against a `RowCount` of 3. `EffectiveMaxSquadSize()` caps its return at
`FightHudSpec.StageSlotsPerSide` (3) precisely where that combination would
otherwise return 4, and `ReckoningTests.cs:183` passes today — so there is no
live bug, but the comment is describing the pre-cap arithmetic rather than
the capped, actually-returned value, which is confusing enough that it is
worth being precise about here rather than quoting it as if it still matches
line for line.
Pin the dossier panel's the same way, against the same expression — a test
that wrote the literal `3` would be the drift it exists to catch. Unused rows
are hidden, as `ReckoningScreen`'s are.

The shop itself needs none of this — with the strip gone, its own layout has
no row-count-against-squad-size question left to answer (§3d's tooltip is a
single floating overlay, not an array).

---

## 4. Build order

**Superseded 2026-09-03 (b) — see §7.3.** The four gates in §7.3 replace the
phase list below and its day estimates; §7.3's exit criteria are what a build
is checked against now, not the "How we know" bullets under each phase here.

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
  `docs/AUDIT_V1_ARCHIVE.md:387`, is the precedent for that being required). Full suite green
  with scenes built.

### Phase B — the shop room (2d)

- **Changes:** `RngStreams.Shop = 5` and `Derive`/`Open`'s third coordinate
  (F9); `Domain/Rewards/ShopPricing.cs`; `Domain/Rewards/ShopStock.cs` with the
  three count constants and the roll (§2d); `RunSnapshot.shopStock` /
  `shopRerollsUsed` / `shopNodeId` / `shopStockVersion` and their
  reconciliation (§2e); `Arrival.Shop`, the `ArriveAt` branch and `LeaveShop`;
  the five shop mutation methods of §2f (revised 2026-09-03 — was six);
  `RoomResolution.Kind.ShopNotBuilt` retired.
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

### Phase C — the screen (2d, revised 2026-09-03 — was 2.5d)

**Revised down half a day.** The original half-day premium bought the detail
panel and keyboard focus order against a *reserved-panel* design; the
tooltip (§3d) is cheaper to build than a panel that has to be laid out and
containment-audited in reserved space, even though the panel dropped the
recipient-delta comparison (also a wash — that logic isn't called from this
screen at all now). Net: roughly the original estimate, not the discount the
smaller feature set might suggest, because keyboard focus support (Tab order,
arrow nav, focus-driven tooltip placement) is still full scope here — it was
never in the prototype to begin with (§3d).

- **Changes:** `Domain/UiKit/Screens/ShopScreen.cs`, nested into `MapScreen`;
  `ShopController` in Core; the interaction state machine (README §5, minus
  §5.3's recipient/replace states — §0/§1g) as an explicit state field rather
  than a set of booleans; the tooltip and its focus-or-hover anchoring (§3d);
  `CountBindings` for the four arrays (§3a); strings in `UiStrings`; the
  `PACK` modal (README §0.4) as its own focus scope.
- **Must not change:** `ScreenRegistry.All` stays five entries; no new scene.
- **How we know:** the layout audit passes at all four `UiFrames`; the count
  audit passes; `UiTextFitAudit` passes against the worst-case string table in
  §3 (revision block, replacing README §3's now-superseded one); a PlayMode
  test walks map → shop → buy → leave and finds the room cleared, the gold
  spent, and a `skillId` appended to `unassignedSpellBooks` for a book buy; a
  second walks the state machine's whole input table and asserts no input in
  any state reaches an unlisted state, including the click-outside and
  multi-section-Escape resolutions in §3f.

### Phase C.5 — dossier spell assignment (1.5d, new 2026-09-03)

**Added because §1g exists.** Not optional follow-up: a book that can be
bought and never learned is half a feature, and README §8 says so. Smaller
than Phase C because the interaction design is not new work — it is README
§4/§5.3 relocated (§1g) onto a screen, `CharacterDossierScreen.cs`, that
already has the column and row conventions this panel reuses.

- **Changes:** a spell-books panel on `CharacterDossierScreen.cs`, following
  its existing "pack" row-list convention (`DossierLayout.PackSortRowHeight`/
  `PackSortCentreY` region, `:358-450`); `CharacterDossierController`'s `AssignSpell`/`ReplaceSpell`
  calls (§2f); `CountBindings` for the panel's slot-chip array; its own
  layout pass at the four `UiFrames`, since it has coordinates of its own to
  settle that this document does not specify (§1g).
- **Must not change:** the shop's own tree or mutations (Phase B/C); the
  dossier's existing pack/stats panels.
- **How we know:** a PlayMode test buys a book in the shop, leaves, opens the
  dossier, assigns it, and finds it in `learnedSpells` and gone from
  `unassignedSpellBooks`; a second test exercises the full-slots → replace
  picker → `REPLACING` → commit path from README §5.3, now against the
  dossier's controller; the row count is pinned against
  `EffectiveMaxSquadSize()` per §3e, not a literal.

### Phase D — bot policy, and the batches that gate Phase E (2d, revised 2026-09-03 — was 1.5d)

**Half a day more**, for the second policy call §1g/§2g added
(`ChooseSpellAssignment`) — a real decision (who, which slot) that used to be
folded into `ChooseShop`'s `buy-spell` and is now a separate call the driver
makes once per pending book after the shop loop exits.

- **Changes:** `IRunPolicy.ChooseShop` (`Domain/Bot/IRunPolicy.cs:17-48`),
  now `buy-spell(i)` with no recipient (§2g), and the new
  `ChooseSpellAssignment`, both implemented for the four archetypes; `ShopView`;
  `BotRunDriver`'s shop branch, replacing the two-way `isFight` split at
  `:358`, and its post-shop assignment loop; the trace records purchases and
  assignments; `roomType` added to `runs.jsonl`'s `rooms[]` rows.
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
   shop node instead of a fight node in the same column. **This no longer
   needs a writer change — `rooms[]` already carries `roomType`.** This
   section originally said only `fights[]` did; `docs/BOT_SUMMARY_SCHEMA.md`
   now documents `roomType` on both `fights[]` and `rooms[]`, so a shop visit
   is already distinguishable in the merged data. Less work than this section
   assumed, not more — worth confirming against a live batch before relying on
   it, since this correction is from reading the schema doc, not from running
   one.
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

**Total: 11 days**, revised 2026-09-03 from 9.5 — Phase C's tooltip saves 0.5d
against its original reserved-panel estimate, Phase C.5 (dossier assignment)
adds 1.5d as a genuinely new deliverable the shop prototype's scope cut made
necessary, and Phase D's second policy call adds 0.5d, net +1.5d. Against the
original pre-staging plan's 7.5-8 days the total spread is now 3-3.5 days, and
it buys four things the old order could not: the five spells reach players
through books before they stop reaching them by level, the flip is gated on
measurements rather than on an argument, the shop ships whether or not the
loadout change survives contact, and — new as of this revision — assignment is
a real screen instead of an inline step, which is what the prototype's design
review actually asked for. If the Phase C.5 trade is not worth 1.5 days,
folding assignment back into the shop (i.e. rejecting the prototype's
deviation and returning to README §5.3 as originally specced) is the
alternative — but that is a call for whoever owns the design review, not a
default to fall back to silently.

---

## 5. Assumptions

Stated because the author gave no answer and the code did not force one. Each
is cheap to reverse.

1. **Superseded 2026-09-03 (b) — see §7.1 point 7.** Reroll is per section,
   `15 * 2^n` each, not the whole-shop rule below.
   **Reroll rerolls the whole shop** — all three sections and the four items at
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
9. **Superseded 2026-09-03 (b) — see §7.1 point 4.** The "no OWNED badge"
   reading below is reversed: the book card carries `KNOWN BY …`,
   `ELIGIBLE k/m`, `ALL SLOTS FULL` and `1 UNASSIGNED COPY` facts.
   **A relic already held is not offered**, matching the draft
   (`RunOrchestrator.cs:100-107`). A book already in one of that character's
   slots is still offered — ownership is per character and the shop is
   shared — but **revised 2026-09-03**: the shop card no longer shows OWNED
   for it (§2d, §1g), since the shop has no per-character context to show it
   against any more; the OWNED read happens at assignment time, on the
   dossier. A book **every** fielded character already knows is filtered out
   at roll time (§2d), since no press on it could do anything for anyone.

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

- **Tier drops need reworking less than this section assumed — half of it
  already happened.** This originally read "`RarityTable.StepsPerTier = 8`
  gives a tier a leg, so a 40-step run passes through five tiers of gear."
  `StepsPerTier` has since been **doubled to 16**
  (`Domain/Rewards/RarityTable.cs:47`), with the file's own comment recording
  the exact complaint this bullet made — the old value pushed the floor tier
  ahead of the design's stated target — and fixing it the same way this
  bullet would have asked for. A 40-step run now passes through roughly two
  and a half tiers, not five. What's left: **plus rolls are still rare** —
  `LootLadder`'s geometric climb means the top rung is 5% at the hard cap of
  `MaxStep = 0.55` (`LootLadder.cs:19-53`), with `NormalStep` at 0.22 — so a
  +3 from an ordinary fight is about 1%, unchanged. Still out of scope here,
  because moving either constant moves every price in §2c — but §2c's own
  table needs a pass anyway now that `FloorTier`'s divisor changed (flagged
  there directly), so this is no longer purely hypothetical future work.
- **The Events screen.** `RoomType.Event` generates at weight **7**, not 14
  (`Domain/Dungeon/DescentMap.cs:187-194` — rebalanced, same note as §0/F2) —
  still more than twice the shop's — and resolves as `Kind.EventNotBuilt`
  (`RoomResolution.cs:102-103`). `RoomResolution`'s own comment calls it "a
  REGRESSION against v1 knowingly left standing: v1 ran a wandering-mage event
  that taught an event-gated spell. Porting it needs the spell-teaching path"
  (`:94-98`). **Phase 1 of this plan builds that spell-teaching path**, so the
  event room's blocker is removed as a side effect and the Events screen becomes
  the obvious next piece of work.
- **A relic slot cap**, if the design ever wants one. AUDIT #50/#51
  (`AUDIT.md:449`, `:480`) are the register entries; today there is nothing to
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

---

## 7. Product review, 2026-09-03 (b) — what changes and what does not

Eleven product points and six technical ones, weighed one at a time against
the code rather than accepted as a block. The build order in §7.3 replaces
§4's phases. Everything below is stated as a decision so it can be reversed
by editing one line here, not by re-arguing it.

### 7.1 Accepted — and what each one changes in the build

1. **The economy baseline is biased (review #1). Accepted.** §2a's medians
   are over *won* fights and lifetime cumulative gold; neither is what a
   player holds when the shop door opens. The shop is priced against
   **gold on arrival**, measured by the bot with the shop room live in the
   simulation, not off an old batch. `RoomTrace` gains `GoldOnArrival`,
   `GoldSpent`, `GoldOnLeave`, per-section purchase counts and
   `RerollsUsed[]`, and the gate-1 report prints p10 / p25 / median arrival
   gold at shop nodes per profile, the share of visits arriving with less
   than the cheapest card, and **gold forgone** — the median won-fight payout
   at that step, which is what a shop node costs to take. The "taking a shop
   does not cost depth" gate is replaced by a matched-seed comparison over
   the following leg: survival to the next boss, power gained, and depth
   variance, shop-taking policy against fight-only policy on the same seeds.
2. **The shop has no gameplay identity (review #2). Accepted as a written
   target, not as a redesign.** The room is for **converting run gold into a
   build decision the player can explain**. Three target decisions, and the
   telemetry that says whether each one is happening:
   - *Save or spend*: a modest tier-matched piece now, or hold for a
     run-defining relic. Measured: share of visits leaving with gold at or
     above the cheapest unsold relic, and whether that gold is spent on a
     relic at a later shop in the same run.
   - *Patch or reinforce*: a potion or armour against a weakness the last
     leg exposed, or a weapon/relic that stacks what already works. Measured:
     purchase kind against damage-taken share over the previous leg.
   - *Reroll or walk*: pay to refresh one poor section, or leave with the
     gold. Measured: rerolls per section per visit, and rerolls followed by
     no purchase in that section.
   Stock generation, prices and layout are judged against these three, and
   a GAP_AUDIT row that cannot name one of them is machinery, not product.
3. **Ten offers is too many (review #3) and five books cannot fill three
   cards (review #5). Accepted together.** `ShopStock` constants become
   **Gear 3, Relics 2, Books 1** — six primary offers, pack and sell
   secondary. The featured book is one card because the whole pool is five
   entries and the fielded squad is one character with three slots
   (`SaveData.BaseMaxSquadSize = 1`): three cards would show 60% of the
   pool at every shop and be exhausted in a single run. The counts are
   constants, the screen binds to them, and **gate 1 re-decides them from
   measured affordability** before the screen is laid out — that is the
   whole reason the economy is simulated before the screen exists. §2d's
   3/4/3 and the prototype's 3/4/3 grid are superseded; the five-panel
   shape stays, the Relics and Spell Books panels hold fewer cards.
4. **Buying an unassigned book hides the facts the buyer needs (review #4).
   Accepted.** The book card and its tooltip show, from run state the shop
   already has: `KNOWN BY SHAWN` / `KNOWN BY {n}`, `ELIGIBLE {k}/{m}`,
   `ALL SLOTS FULL` when every eligible character has three, and
   `1 UNASSIGNED COPY` when one is already in `unassignedSpellBooks`. The
   §2d rule "the card carries no ownership badge" is reversed. The map
   screen gets a pending-book indicator while `unassignedSpellBooks` is
   non-empty, and the dossier's panel header carries the count. No forced
   assignment on leaving; the indicator is the nudge.
5. **Replacement destroys value (review #6). Accepted, in the cheapest
   form.** Replacing a learned spell returns the displaced book to
   `unassignedSpellBooks`. Reassignment outside combat is free. There is no
   destroy path, so README §5.3's `REPLACING` / struck-through-chip warning
   language is dropped with it; the picker still exists, it just moves a
   book rather than deleting one. §1d and §2f's "Replace" row are amended:
   *remove one matching `skillId` from the pool → append the displaced
   `skillId` to the pool → overwrite the slot → persist*.
6. **The interaction grammar is too large (review #8). Accepted.** README
   §5.1's per-section arming is withdrawn (for the second time; the first
   draft had it right). One selection, globally, is the whole model:
   - A press selects a card and shows its detail. Unaffordable cards are
     selectable for inspection; their chip still reads `NEED {n}`.
   - The selected card's chip reads `CONFIRM · {price} G`; a second press
     on it, or the `BUY` button in the Shop Actions panel, commits. `BUY`
     exists so keyboard and a future gamepad have one commit control.
   - Selecting another card moves the selection. Click outside clears it.
   - Escape: closes the Pack modal if open; else clears the selection;
     else nothing.
   - The Pack modal owns selection while open; the main screen's selection
     is cleared when it opens.
   - **`LEAVE` keeps its one confirm** (`LEAVE` → `LEAVE?`). The review
     asked for one click; leaving discards the visit permanently and the
     button sits beside `REROLL` and `BUY` in the same panel, so a misclick
     is cheap to make and impossible to undo. One extra press on a
     once-per-room action is the smaller cost. Any other input reverts it.
   The canonical state table is README §5.1 (rewritten, no strike-throughs
   to apply mentally).
7. **Whole-shop reroll is too coarse (review #9). Accepted.** Reroll is
   **per section**. Each of the three sections has its own reroll button in
   its panel header, its own counter and its own price:
   `RerollPrice(n) = min(15 * 2^n, 9999)`, `n` per section. The RNG follows:
   three streams instead of one — `RngStreams.ShopGear = 5`,
   `ShopBooks = 6`, `ShopRelics = 7` — each opened on
   `(run.step, run.currentNodeId, rerollsForThatSection)`. F9's third
   `Derive` coordinate is still needed and still unpacked; the section is
   carried by the stream number, not by arithmetic on the coordinate.
   `RunSnapshot.shopRerollsUsed` becomes `int[] shopRerollsUsed` of length
   `ShopStock.SectionCount`, which also answers the review's "scalar on the
   run" objection: it is keyed by section, and `shopNodeId` remains the
   per-node guard (a run holds exactly one uncleared shop, because a shop
   clears on leave and the map allows one current node).
8. **Accessibility scope (review #10). Accepted in the form that costs
   nothing now and a lot later.** Keyboard navigation is built as a
   navigation graph over the six cards and the action buttons — up/down/
   left/right neighbours declared per card, `Enter` commits, `Escape` per
   point 6 — so a gamepad's d-pad/A/B maps onto it without a second model.
   Gamepad *input* is still not wired; the model it would drive is.
   Every animation on the screen reads one shared multiplier,
   `UiMotion.DurationScale` (Domain, default 1), so reduced motion is one
   constant, not a per-screen retrofit.
9. **A human usability gate before the flip (review #11). Accepted; it is
   the author's to run, not the bot's.** Gate 2 and gate 3 each end with
   five to eight fresh-player sessions and a short checklist, §7.3. The bot
   validates balance; the checklist validates whether the shop is legible.
10. **The technical corrections (review, "Technical and planning
    problems"). All accepted.** §2d's RNG example is corrected in place.
    README §5.1 is one canonical table. `shopRerollsUsed` is per section
    (point 7). `SaveSystem.Save` returns `bool` and every shop mutation
    returns a `ShopResult` (`Ok`, `Refused` with a reason,
    `AppliedNotPersisted`) so a swallowed write is visible to the caller and
    the screen can say so. Day estimates are dropped in favour of gates with
    exit criteria.

### 7.2 Not accepted, or deferred — with the reason

- **Post-purchase equip / sell-the-displaced-piece inside the shop
  (review #7). Deferred to after gate 2's human sessions.** The story is
  real: a bought upgrade leaves the old piece stranded in the bag until a
  shop that may never come. But the fix puts an equip flow inside the shop,
  which §2c argued against for a reason that has not changed — `EquipMove`'s
  "both halves or neither" rule and a bag that changes under an open sell
  list. Gate 2's checklist asks specifically whether testers hit this. If
  they do, the compact `EQUIP` / `TO PACK` pair after a gear purchase is the
  first gate-2 follow-up; the atomic operation it needs, `EquipmentOps.Equip`,
  already exists.
- **Five to six offers as a fixed number.** Six is the *starting* constant
  (point 3); gate 1 measures affordability per section and may move it.
- **One-click leave.** Kept at two, point 6.
- **Expanding the book pool.** The review is right that five books is thin,
  and nothing here fixes it: authoring a sixth spell is content, and content
  is the author's. The one-card section is sized to the pool that exists.
  Recorded in §6.

### 7.3 Build order — four gates, replacing §4's phases

The review's re-cut is adopted with one change: the economy is simulated
first and the books come *third*, after the screen, not first as §4 had
them. §4's argument for books-first was that the flip needed measuring
before it shipped; that still holds and gate 4 still measures it. What
changed is that the shop no longer depends on books existing — a shop
selling gear and relics is already a complete room — so the riskiest
content change stops being on the critical path of the screen.

Each gate ends with a written exit check in `docs/handoffs/shop_v2/GAP_AUDIT.md`,
and no gate starts until the previous one's check is recorded. Phase 0's
characterisation tests (§4) move to the start of gate 3, where the code they
pin is first touched.

**Gate 1 — economy prototype, no screen.**
- Build: F9's third coordinate and the three shop streams (point 7);
  `Domain/Rewards/ShopPricing.cs` (§2b, with per-section reroll);
  `Domain/Rewards/ShopStock.cs` (roll, counts 3/2/1, `NO OFFER`, assumption
  11's affordability floor); `RunSnapshot` stock fields (§2e, `int[]`
  rerolls); `Arrival.Shop`, `ArriveAt`'s branch, `LeaveShop`; the buy-gear /
  buy-relic / sell / reroll-section mutations (§2f) returning `ShopResult`;
  `SaveSystem.Save` returning `bool`; `IRunPolicy.ChooseShop` + `ShopView`
  for the four archetypes; `BotRunDriver`'s shop branch; the `RoomTrace`
  fields of point 1; `bot_merge.py`/report rows for the gate-1 numbers.
  Books are **not** in this gate: the book section rolls `NO OFFER` until
  gate 3 exists, and the constant is already 1.
- Must not change: any existing stream's seeds (F9's bit-for-bit test);
  `BankPayout`; `ItemOfferRoll.Candidates()`; the trace's existing fields.
- Exit: a batch at the `20260902-020048` shape reports arrival-gold p10 /
  p25 / median per profile, forgone gold, affordability per section, share
  of zero-purchase visits, rerolls-then-nothing per section, and the
  matched-seed leg comparison of point 1. The counts and prices are then
  **re-decided in writing** here before gate 2 starts.

**Gate 2 — the small playable shop.**
- Build: `ShopScreen` nested in `MapScreen` (§3a/§3b), against the
  five-panel grid with gate-1's counts; the single-selection model and
  `BUY` (point 6); per-section `REROLL` buttons; the tooltip (§3d); the
  Pack modal; the navigation graph and `UiMotion.DurationScale` (point 8);
  `CountBindings`; strings; a PlayMode walk map → shop → buy → leave.
- Exit: layout, count and text-fit audits green at all four frames; the
  human checklist — time to first purchase, can the tester say why the
  offer they bought was good, did anyone try to sell a worn piece, did
  anyone misread `NEED n` as the price — recorded in GAP_AUDIT.

**Gate 3 — the spell acquisition loop.**
- Build: Phase 0's characterisation tests; `bookOnly`/`bookTier` (§1a,
  additive, `unlockLevel` kept); `learnedSpells` + `unassignedSpellBooks`
  (§1b); `CanLearn`/`LearnSpell` with return-to-pool replacement (point 5);
  `RollSpellDrop` (§1e); the book card's purchase-time facts (point 4); the
  map indicator; the dossier assignment panel (§1g, own layout pass);
  `ChooseSpellAssignment` for the bot; gate-1's report extended with §4
  Phase D's spell-acquisition numbers 1-4.
- Exit: the four spell numbers reported; the human checklist — did the
  tester find the unassigned book, buy a duplicate by accident, or
  regret a replacement.

**Gate 4 — the loadout flip (old Phase E).**
- Unchanged from §4 Phase E in content: `bookOnly: true`, `unlockLevel`
  removed on the five, `PlayerKit.BasicSpell` and its rows taken out.
- Entry: §4 Phase D's gate (a)/(b) on the gate-3 batch, plus gate 3's human
  checklist showing no tester ended a run with an unused book they did not
  know about. Revert is one commit, as before.

### 7.4 What the audit now tracks

Every GAP_AUDIT row gains a fifth column, **Decision served**, naming one of
the three decisions in point 2 or `machinery`. Rows marked `machinery` are
allowed — a save field is machinery — but a gate cannot exit on machinery
rows alone.

### 7.5 Gate 1 build notes — where the code corrected the plan (commit 4bc66f7)

- **Sell-price midpoints round to even.** `Math.Round` takes 34.5 down to 34
  and the potion's 4.5 down to 4; §2c predicted 35 and 5. Pinned as the code
  behaves, in `ShopPricingTests`.
- **The affordability anchor (assumption 11) is 24, not 16-19.** The cheapest
  gear that can exist is tier-0 / +0 / no-affix at 20, so a 16 anchor is a
  guarantee that never fires. `ShopPricing.NormalFightPayoutAnchor = 24`.
- **`RelicRarity.Godlike` is priced at 700.** §2b's table stopped at Mythic;
  the enum has six values and `RelicPool.WeightOf` already treats Godlike as
  rarer than Mythic. Without a case the rarest relic would price as Common.
- **Section order is gear = 0, books = 1, relics = 2** (`ShopStock`), which
  reverses §2e's older `0 spells, 1 items, 2 relics`. The constants are the
  truth; the screen binds to them.
- **`ShopStockEntry.noOffer` is an explicit flag**, not an empty `contentId`,
  so a reconciled placeholder still says which content vanished.
- **`RoomResolution.Kind.ShopNotBuilt` survives gate 1** for the map's
  interim shim in `MapController.Walk.Arrive`; gate 2 deletes both together.
- **F9's single `Shop = 5` stream prose is historical**; the three per-section
  streams of §7.1 point 7 are what shipped (`RngStreams.ShopGear/ShopBooks/
  ShopRelics` = 5/6/7). The gear shelf's affordability re-draw consumes extra
  draws from `ShopGear`, so `ShopStock.AffordabilityRedraws` and the anchor
  are part of the shelf's seed and moving either renumbers every shop.
