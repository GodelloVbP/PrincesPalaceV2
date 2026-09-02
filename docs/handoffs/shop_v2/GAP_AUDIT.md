# GAP_AUDIT — Shop Screen v2

Audited against `README.md` at `5f1e7d4`. **Nothing in this handoff is built.**
The room type generates and resolves as a placeholder
(`Domain/Dungeon/RoomResolution.cs:99-100`); there is no shop screen, no shop
controller, no pricing, and no spell-book system.

This file exists from the start rather than after a build, so the first audit
pass has a shape to fill in rather than one to invent. Strike a row or change
its verdict as each gap closes; nothing is deleted.

| Handoff section | Spec'd | Built | Verdict |
|---|---|---|---|
| §1 Overview — Shop is an in-run room spending run gold | A map room that opens a purchase panel over the map | `RoomType.Shop` generates at weight 6 (`Domain/Dungeon/DescentMap.cs:178`) and resolves to `Kind.ShopNotBuilt` (`RoomResolution.cs:99-100`); `RoomResolver.TryMessage` prints "not built" (`Core/RoomResolver.cs:92-94`) | missing |
| §1 Overview — run gold has a sink | Gold spent in-run | `RunManager.BankPayout` (`Core/RunManager.cs:396-402`) is the only writer and there is no spender; AUDIT #2 (`AUDIT.md:105`) | missing |
| §1 Overview — the automatic per-level spell is removed | No `PlayerKit.BasicSpell`; spells are learned books | `ResolvedSpellTier` still rides the kit (`Domain/Combat/Session/CombatantKit.cs:42-43`) and is appended to every skill submenu (`FightHudModel.cs:145-159`) | missing |
| §1 Overview — 3 spell slots per character per run | `RunSnapshot` carries learned book ids per character | `RunSnapshot` has no such field (`Data/RunSnapshot.cs`) | missing |
| §2 Files — designer prototype | `Shop Screen v2.dc.html` + `support.js` | Not produced | missing |
| §2 Files — live v2 reference renders | Screenshots of the hub store and relic screen | Not renderable: `ScreenRegistry.All` is five panels (`Editor/SceneBuilder/ScreenRegistry.cs:55-61`), no store panel exists, and the relic draft is nested inside HubPanel. Committed v1 PNGs used as anchors instead | deliberate-deviation — the screens asked for are not addressable by `tools/screenshot.ps1 -Panel`, whose list is derived from `ScreenRegistry` (`tools/screenshot.ps1:26-33`) |
| §3 Layout — header, gold chip, reroll button with price | Coordinates at 1920×1080 | No screen tree | not built |
| §3 Layout — spell section, 3 cards | 380×236 at y 156 | No screen tree | not built |
| §3 Layout — spell-slot strip, 3 chips per fielded character | 548×236 panel at x 1316 | No screen tree | not built |
| §3 Layout — gear section, 4 cards | 420×204 at y 448 | No screen tree | not built |
| §3 Layout — relic section, 3 cards with paging | 380×220 at y 708, prev/next/page label | No screen tree. The paging pattern exists on the draft (`Core/RelicDraftController.cs:76`, `:129-149`) and is reusable | not built |
| §3 Layout — sell panel, scrollable bag list with price per row | 548×220 viewport, 548×44 rows | No screen tree | not built |
| §3 Layout — leave button | 320×64 at y 952 | No screen tree | not built |
| §3 Layout — survives the audit at four aspects | Clean at 1920×1080, 2580×1080, 1920×1440, 1920×1200 | `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) would check it; nothing to check | not built |
| §4 States — affordable / unaffordable with `NEED {n}` | Per-card opacity, border and price-text treatment | No screen tree | not built |
| §4 States — sold out holds its position | Card stays at index, row does not re-centre | No stock model | not built |
| §4 States — relic owned-already is filtered, not drawn | Held relics excluded from the roll | The rule exists in the draft (`Core/Bot/RunOrchestrator.cs:84-86`); no shop roll to apply it in | not built |
| §4 States — spell slot full / owned-by / would-fill / would-replace | Four chip states, green and red previews | No slot model | not built |
| §4 States — selected-for-sell | Row lift, `SELL FOR {n} G` label | No sell flow | not built |
| §4 States — rerolled | Whole-shop cross-fade, price doubles in place | No reroll | not built |
| §5.1 Buy confirm on second press | Arm, then commit; one armed thing at a time; Esc disarms | No controller. The deselect rule it generalises is in `RelicDraftController.Select` (`:150-161`) | not built |
| §5.2 Commit effects per section | Gear/consumable to bag, relic appended, book learned | `InventoryOps.Add` (`Domain/Inventory/InventoryOps.cs:43`) and `RunOrchestrator.TakeRelic` (`:112-123`) exist and are reusable; no caller | not built |
| §5.3 Replace-slot picker | Character choice, then chip choice when full; replaced book is destroyed | No spell-book system | not built |
| §5.4 Sell from the bag, worn gear excluded | List is `stockpiledItems`; no unequip on this screen | `stockpiledItems` (`Data/SaveData.cs:146`) and `InventoryOps.TryRemoveAt` (`:113`) exist; no sell flow | not built |
| §5.5 Reroll whole shop, price doubles, resets on leave | 25 / 50 / 100 / 200 within a visit | No reroll | not built |
| §5.6 Leave returns to map and clears the room | Node marked cleared, no confirm prompt | `RunManager.ClearCurrentRoom` exists; `ArriveAt` currently clears non-fight rooms on *arrival* (`Core/Bot/RunOrchestrator.cs:189-192`), which is wrong for a shop | not built |
| §5.6 Same stock after a quit and reload | Seeded per node | The property exists for treasure (`Core/RoomResolver.cs:43-45`); `RngStreams` has no shop stream (`Domain/Rng/RngStreams.cs:29-32`) | not built |
| §6 Design tokens | Palette and type taken from `FightHudPalette.cs` and the two prior handoffs | Palette constants exist (`Domain/UiKit/FightHudPalette.cs:18-81`); nothing consumes them here | not built |
| §7 Engine — everything is run gold | Chip reads `RunSnapshot.gold`, never `SaveData.Gold` | No reader | not built |
| §7 Engine — prices from the plan's formula, not `ItemDefinition.cost` | `Domain/Rewards/ShopPricing.cs` | Does not exist. `ItemDefinition.cost` (`Core/Content/ItemDefinition.cs:59-60`) is live v1 residue: 0 on all 33 weapons, median 505 at tier 10 | not built |
| §7 Engine — fixed card count emitted at build time, declared to the count audit | Five arrays declared through `ScreenDef.CountBindings` | No `ScreenDef` for this screen (`ScreenRegistry.cs:24-28` is the mechanism) | not built |
| §7 Engine — copy through `UiStrings` | Price labels as templates with numeric arguments | No strings added | not built |
| §8 Out-of-scope — Events, hub store, drop-tier rework | Deliberately excluded | Correctly excluded; `RoomType.Event` remains a placeholder (`RoomResolution.cs:102-103`) | match |
