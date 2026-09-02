# GAP_AUDIT — Shop Screen v2

Audited against `README.md` at `5f1e7d4`, with the rows re-cut against the
revision of 2026-09-02 (`b7adb83` plus the external-review pass) — the relic
pager row is gone because the pager is, and rows were added for the detail
panel, the state machine, atomic persistence, `NO OFFER` and keyboard focus.
**Nothing in this handoff is built.**
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
| §3 Layout — recipient strip, 3 rows × 3 chips | 548×140 panel at x 1316, row count asserted against `EffectiveMaxSquadSize()` | No screen tree. The precedent is `ReckoningScreen.RowCount` (`Domain/UiKit/Screens/ReckoningScreen.cs:91-96`) pinned by `ReckoningTests.cs:159`; the real ceiling is 2 (`Data/SaveData.cs:53`, `:163-166`) | not built |
| §3 Layout — detail panel | 548×112 at 1316, 316; shows the focused card's slot/delta/affixes, relic effect, or spell effect+mana+cooldown+shape | No screen tree. `ItemDescription.Compare` (`Core/ItemDescription.cs:196-227`) is the delta source and already exists; it takes no modifiers and reports a weapon as `+0 attack` (`:229-248`), so the panel needs the damage line at `:145-168` for weapons | not built |
| §3 Layout — gear section, 4 cards | 420×204 at y 448 | No screen tree | not built |
| §3 Layout — relic section, 3 cards, no paging | 380×220 at y 708; `ShopStock.RelicCount = 3` and a section never shows more cards than its constant | No screen tree. The draft's paging (`Core/RelicDraftController.cs:76`, `:129-149`) exists and is deliberately NOT used here | not built |
| §3 Layout — worst-case string table | Every price field sized for 4 digits plus a suffix; sell meta for `T10 · +5 · 3 AFFIX` | `UiTextFitAudit` (`Editor/SceneBuilder/SceneBuilder.cs:105`) would check it; nothing to check | not built |
| §3 Layout — sell panel, scrollable bag list with price per row | 548×220 viewport, 548×44 rows | No screen tree | not built |
| §3 Layout — leave button | 320×64 at y 952 | No screen tree | not built |
| §3 Layout — survives the audit at four aspects | Clean at 1920×1080, 2580×1080, 1920×1440, 1920×1200 | `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) would check it; nothing to check | not built |
| §4 States — affordable / unaffordable with `NEED {n}` | Per-card opacity, border and price-text treatment | No screen tree | not built |
| §4 States — sold out holds its position | Card stays at index, row does not re-centre | No stock model | not built |
| §4 States — `NO OFFER` for an undersized pool | Leftover cards render in place, never hidden or duplicated | No stock model | not built |
| §4 States — text/shape cue on every accented state | Lock + `NEED n`, `✓` on the selected recipient, `REPLACING`, `REPLACE REQUIRED`, focus ring | No screen tree | not built |
| §4 States — keyboard focus ring and focus order | Sections → cards → rows → buttons; focus and hover feed the same detail panel | No screen tree, and no focus-order convention exists on any other screen to inherit | not built |
| §4 States — relic owned-already is filtered, not drawn | Held relics excluded from the roll | The rule exists in the draft (`Core/Bot/RunOrchestrator.cs:84-86`); no shop roll to apply it in | not built |
| §4 States — spell slot full / owned-by / would-fill / would-replace | Four chip states, green and red previews | No slot model | not built |
| §4 States — selected-for-sell | Row lift, `SELL FOR {n} G` label | No sell flow | not built |
| §4 States — rerolled | Whole-shop cross-fade, price doubles in place | No reroll | not built |
| §5.1 Interaction state machine | `Idle → Armed → RecipientSelected → ReplaceSlotPicking → Committed` plus `LeaveConfirm`, arming **per section**, every input defined per state | No controller. The deselect rule it generalises is in `RelicDraftController.Select` (`:150-161`) | not built |
| §5.1 / §5.6 Escape leaves transient states only | Disarm and close-picker; never leaves the shop | No controller | not built |
| §5.2 Every mutation is atomic | One orchestrator method: validate, apply (cannot fail), persist once | `SaveSystem.Save` already writes via a `.tmp` + `File.Replace` and swallows its own exceptions (`Core/SaveSystem.cs:123-175`), so the boundary that can half-fail is the in-memory apply on `SaveSlotManager`'s cached `SaveData` (`Core/SaveSlotManager.cs:35-46`), not the disk | not built |
| §5.2 Commit effects per section | Gear/consumable to bag, relic appended, book learned | `InventoryOps.Add` (`Domain/Inventory/InventoryOps.cs:43`) and `RunOrchestrator.TakeRelic` (`:112-123`) exist and are reusable; no caller | not built |
| §5.3 Recipients and the replace picker | Sole eligible recipient preselected with the confirm still on the card; owning rows inert; full rows carry `REPLACE REQUIRED`; replaced book destroyed | No spell-book system | not built |
| §5.4 Sell from the bag, worn gear excluded | List is `stockpiledItems`; no unequip and no dossier on this screen; the path is buy → leave → equip → sell next shop | `stockpiledItems` (`Data/SaveData.cs:146`) and `InventoryOps.TryRemoveAt` (`:113`) exist; `EquipMove.TryEquip` already displaces into the bag (`Domain/Equipment/EquipMove.cs:64`, `:84`); no sell flow | not built |
| §5.4 Selling a stack asks a quantity | `1` / `ALL` chips, so a stack is two presses | No sell flow | not built |
| §5.5 Reroll whole shop, price doubles, saturates | 25 / 50 / 100 / 200 … capped at 9999, `n` stored per node | No reroll | not built |
| §5.6 Leave returns to map and clears the room | Node marked cleared, no confirm prompt | `RunManager.ClearCurrentRoom` exists; `ArriveAt` currently clears non-fight rooms on *arrival* (`Core/Bot/RunOrchestrator.cs:189-192`), which is wrong for a shop | not built |
| §5.6 Leave confirms once | `LEAVE` → `LEAVE?`; Escape never leaves | No controller. Reverses the first draft's "nothing is confirmed on the way out" | not built |
| §5.6 Same stock after a quit and reload | Seeded per node from three coordinates | The property exists for treasure (`Core/RoomResolver.cs:43-45`); `RngStreams` has no shop stream and `Derive` takes only two position inputs (`Domain/Rng/RngStreams.cs:29-32`, `:44`) | not built |
| §7 Engine — price persisted at roll, plus `stockVersion` | A content or pricing change cannot rewrite an open shop | No stock model | not built |
| §7 Engine — no minimum-resolution policy needed | Settled project-wide by `ScaleWithScreenSize` + `Expand` at a 1920×1080 reference | `Editor/SceneBuilder/SceneBuilder.cs:203-210`; aspects checked are `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`) | match — nothing for this screen to add |
| §6 Design tokens | Palette and type taken from `FightHudPalette.cs` and the two prior handoffs | Palette constants exist (`Domain/UiKit/FightHudPalette.cs:18-81`); nothing consumes them here | not built |
| §7 Engine — everything is run gold | Chip reads `RunSnapshot.gold`, never `SaveData.Gold` | No reader | not built |
| §7 Engine — prices from the plan's formula, not `ItemDefinition.cost` | `Domain/Rewards/ShopPricing.cs` | Does not exist. `ItemDefinition.cost` (`Core/Content/ItemDefinition.cs:59-60`) is live v1 residue: 0 on all 33 weapons, median 505 at tier 10 | not built |
| §7 Engine — fixed card count emitted at build time, declared to the count audit | Five arrays declared through `ScreenDef.CountBindings` | No `ScreenDef` for this screen (`ScreenRegistry.cs:24-28` is the mechanism) | not built |
| §7 Engine — copy through `UiStrings` | Price labels as templates with numeric arguments | No strings added | not built |
| §8 Out-of-scope — Events, hub store, drop-tier rework | Deliberately excluded | Correctly excluded; `RoomType.Event` remains a placeholder (`RoomResolution.cs:102-103`) | match |
