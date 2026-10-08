# QA capture pass, 2026-09-26

Runtime captures for AUDIT #203's nine systems, the #204 focus proof, intent badges after `6e8c71d5` (#79/#211), the expansion spell-book icons from `879a408f`, and the owner-digest questions #45, A1, A2, A3, M8, M9 (`docs/OWNER_DECISIONS_2026-09-26.md`). No game code, content, scenes or AUDIT.md changed; nothing committed. Every PNG was opened and looked at. Pixel positions are on the 1920x1080 capture. Tools: `tools/screenshot.ps1 -Runtime -RuntimeFilter <class>`, `tools/preview.ps1 -Spell/-Enemy`, `tools/focus_check.ps1 -Mode Runtime`.

## Summary

| System | Verdict | One line |
|---|---|---|
| #203.1 Attributes panel | defect | The six rows show no text (no score name, value or effect); only the `+` buttons and the pad marker draw. The dirty font asset is not ruled out as the cause. |
| #203.2 Fight detail card | defect | POWER has no damage-type tag ("SKILL · 7 POWER"), and three rows are bare icons with no label. |
| #203.3 Five-row skills list | defect | Five rows then a scrollbar, as specified, but the pad marker sits on the ITEM command button rather than in the open list. |
| #203.4 Shop comparison + LB/RB | defect | The panel lists only the offered item (no equipped side or delta), and the highlighted card and the confirming card are different items. |
| #203.5 Enemy plates | defect | Head-zone icons are 8-15px slivers or missing; a yellow bar hangs below the plate frame. Ward segment and recoil tiers not captured. |
| #203.6 Blackglass / Winter's Rebuke spears | defect | Both spears draw but cross the combat-log text (Blackglass reaches the top edge); both impact tails hide the whole treant in white. |
| #203.7 Thorn Tithe `fit: target` | looks right | The thorn ring scales to the body on both the treant and the rat. |
| #203.8 Treant slam and spores | could not capture | Preview stills are taken after each turn, so no frame shows the windup, ground burst or spore cloud. |
| #204 Focus proof | could not capture | Baseline: Unity took the foreground for 359 ms and the guard gave it back. The hidden-desktop fix is not implemented, so it could not be tested. |
| Intent badges (#79 / #211, M10) | looks right | Rat and beetle badges sit ~50-60px above the drawn body, the same gap on both; the treant's gap is larger (~85px). |
| Expansion spell-book icons | defect | At the ~28px the shop draws them the glyphs cannot be read; eight of thirteen covers are the same purple; Blackglass Spear's black cover nearly disappears on the dark card. |
| #45 Detail card vs far enemy's feet | looks right | With a submenu open the card's top edge is ~75px below the rightmost enemy's feet. |
| A1 Cinderfault on one rat / a rank | looks right | One rat gets a compact fault the width of its body; a rank gets one fault per rat. The rank's log line runs under the enemy plates. |
| A2 Sky spells vs the treant | defect | All three spells start in the combat-log band; Crownfall also hangs over empty ground left of the treant. |
| A3 Bloom on menus / hub | could not capture | Main-menu lantern halo looks painted into the art; palace windows show no glow spill; no fixture shows the hub without a menu over it. |
| M8 XP text on the XP bar | could not capture | No capture fixture reaches the XP rows; ReckoningCaptureTests only photographs the item choice. |
| M9 Tooltip vs mannequin | defect | A pack tooltip beside the loadout covers the mannequin's head and shoulder and the HEAD/NECKLACE slots; the exact Legs-slot hover was not captured. |

## What blocked or limits the captures

- **Font asset.** `Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset` is modified and uncommitted in the working tree: empty glyph table, 1x1 atlas. Every capture was synced from this tree. A re-capture of the attributes panel with the committed font (TestRunner copy only) was denied by the permission classifier, so the font is not ruled out as the cause of #203.1's blank rows.
- **FightMenuCaptureTests failed at step 3** with "the actor has no castable skill to open targeting with": Shawn's only skill, Shear, costs 3 Wool and he is at 0/10. Frames 3-5 (targeting, intent hover) were never written. The fixture no longer matches the content. Log: `fight-menu/run.log`.
- **Hand-built fixtures never show intent badges.** FightMenu and StatusRow build their fight by hand; only IdentityPlateCaptureTests (a real run fight) shows intents.
- **`preview.ps1` exits 1 on every run** even though its tests pass and it writes its frames. Not investigated.
- **Turn-order ribbon falls back to letters** ("S", "O", "B") for the party in several frames while enemy portraits in the same ribbon draw; the same party has portraits in `enemy-plates/status_rows_rest.png`.
- **Stale preview files** in `tools/screenshots/preview/` from older runs were initially picked up; they were moved out, so only this session's files remain in `spear-layers/`.
- The red placeholder boxes where the sheep should stand in RuntimeScreenshotTests frames and the treant preview are how those fixtures build their actors, not a runtime bug.

## 1. AUDIT #203: nine landed systems

### 1.1 Attributes panel
[attributes/Dossier_attributes.png](attributes/Dossier_attributes.png)
- Shows: "3 POINTS TO SPEND", CLOSE, the pad marker on row 0, six `+` buttons.
- **Defect:** the six rows show no text at all (no "STR 12", no effect line).
- Ruled out: `PaintAttributesPanel` fills the name and effect labels, and Hub.unity wires all twelve (`DossierAttrRowName0`... ChakraPetch, white, size 16). The labels exist and are filled but nothing is visible.
- Cause not diagnosed; see the font caveat. The panel is mostly empty space. Left/Right/Submit cannot be judged from a still.

### 1.2 Fight detail card
[fight-menu/FightMenu_2_skill_submenu.png](fight-menu/FightMenu_2_skill_submenu.png), [skills-list/FightSubmenu_SixSkills.png](skills-list/FightSubmenu_SixSkills.png), [skills-list/FightSubmenu_ThreeSkills.png](skills-list/FightSubmenu_ThreeSkills.png)
- Card is 260 wide at the right edge, element icon beside the title, cost row ("3 WOOL", "3 MP", "8 MP", "2T").
- **Defect:** no damage-type tag on POWER ("SKILL · 7 POWER" for Shear, "SKILL · 20 POWER" for Ember Lance). Commit `98a4e062` specified the POWER row with its damage type and a following type tag; the element only shows as the icon.
- **Defect:** three rows are bare icons (shield, arrow, dot) with no text. Commit `10589669` calls this "by design", but the owner's original complaint was "three icon rows with no values", and they still read as blank rows.
- ThreeSkills was captured mid-fade (list and card half transparent).

### 1.3 Five-row skills list
[skills-list/FightSubmenu_SixSkills.png](skills-list/FightSubmenu_SixSkills.png), [fight-menu/FightMenu_6_long_list_scrolled.png](fight-menu/FightMenu_6_long_list_scrolled.png)
- Correct shape: six skills show as five rows (Ember Lance to Mend), scrollbar, BACK; unaffordable Iron Cleave dimmed.
- **Defect (SixSkills):** the pad marker is on the ITEM command button (~808, 866) while the breadcrumb reads COMMAND › SKILL; no row looks selected, yet the card shows Rally.
- **Defect (ThreeSkills):** the marker is on MOVE.
- **Defect (scrolled item list):** the marker floats at (800, 555) in empty stage space next to no row; the card shows "Potion 1" while the list is scrolled to Potions 3-7.

### 1.4 Shop comparison panel and LB/RB picker
[shop/Shop.png](shop/Shop.png), [shop/Shop_gear_selected.png](shop/Shop_gear_selected.png), [shop/Shop_pack.png](shop/Shop_pack.png)
- **Defect:** the panel shows only the offered item (Bound Staff: DMG 15, +16 ATK, scaling, affix) with no equipped item, difference or "vs equipped" section, although the dossier tooltip has one. Shawn may have nothing in that slot; the capture cannot tell.
- **Defect:** two cards look selected: the white frame and pad marker are on Worn Leather Torso, but "CONFIRM · 46 G" and the panel are for Bound Staff.
- Picker reads `<  Shawn  >` with no LB/RB glyphs; stepping was not exercised.
- With nothing selected the shopkeeper column is one large empty panel; even with the comparison open ~60% of it is empty.
- All relics read COMMON (#196's "no bare commons" may apply to gear only; flagged, not judged). The pack sell list is clean.

### 1.5 Enemy plates
[enemy-plates/status_rows_rest.png](enemy-plates/status_rows_rest.png), [enemy-plates/status_rows_hover.png](enemy-plates/status_rows_hover.png), [intent-badges/Fight_identity_plates.png](intent-badges/Fight_identity_plates.png), [fight-menu/FightMenu_1_resting.png](fight-menu/FightMenu_1_resting.png)
- **Defect:** head-zone icons unreadable or missing: Beetle none; Giant Rat an ~8x4px grey sliver; Treant a ~15px green blob from its canopy; Golem and Bog Witch ~12px specks.
- **Defect:** a yellow bar sits below each plate, outside its red frame (y~218 under a frame ending at y~210); the beetle's is dark in the status fixture.
- A tiny "BRK" label (~8px) right of the beetle's health bar; neither bar nor label is explained on screen.
- The "ENEMIES" header is faint against the canopy and partly hidden behind the plate tops. Plates are ~60% empty vertical space.
- Ward segment not captured (no fixture wards an enemy). Recoil tiers not captured (motion, no frame-series fixture).
- Status badges: the treant's two badges sit at (1600, 725), left of and below its feet ring at (1680, 660), so they read as belonging to empty ground.

### 1.6 Blackglass Spear / Winter's Rebuke spears
`spear-layers/spell_winters_rebuke_vs_treant_{before,flight,impact,after,tail}.png`, `spear-layers/spell_blackglass_spear_vs_treant_*`
- Both draw as a single diagonal still pointing down-right at the treant.
- Winter's Rebuke (impact): upper end at y~85 across log lines 2-4; tip stops ~(1050, 350) in open air left of the canopy.
- Blackglass Spear (impact): upper end runs off the top of the screen across both log lines; tip ~(1150, 500) in front of the treant's face.
- Both tails: the whole treant disappears under a solid white-blue (or white-violet) burst; on Winter's Rebuke the "-4" is green on white and barely readable.
- Separately: the log line "...It's not very effective." runs into the ENEMIES header at x~1390, and white log text over the pale canopy is hard to read.

### 1.7 Thorn Tithe `fit: target`
[thorn-tithe/spell_thorn_tithe_vs_treant_impact.png](thorn-tithe/spell_thorn_tithe_vs_treant_impact.png), [thorn-tithe/spell_thorn_tithe_vs_rat_impact.png](thorn-tithe/spell_thorn_tithe_vs_rat_impact.png)
- On the treant the ring spans canopy to roots (~450px); on the rat it shrinks to the body (~280px); both centred. No defect.

### 1.8 Treant slam and spores
`treant/treant_turn1_trunk_slam.png`, `treant/treant_turn2_spore_cloud.png`, `treant/treant_turn3_attack.png`, `treant/treant_stances.png`
- `preview.ps1 -Enemy treant` takes stills at the turn boundary, after the action: turn1 shows only the result ("-10", a Trunk Slam log line); turn2's log does not yet show Spore Cloud.
- The stance sheet does contain an arms-raised windup pose.
- Seeing the effects needs a frame-series capture of an enemy ability, which no fixture provides.
- The treant's intent badge (green spiral) sits at y~190, ~85px above the canopy, close to the plate column.

## 2. AUDIT #204: focus proof
[focus/focus_check_runtime_baseline.log](focus/focus_check_runtime_baseline.log)
- `tools/focus_check.ps1 -Mode Runtime -RuntimeFilter DossierAttributesCaptureTests`: exit 0, "FOCUS CHECK: ok".
- Focus was taken briefly: at 19:03:10.871 the foreground moved to the launched Unity.exe; 359ms later the focus guard handed it back to explorer.exe. A second "Unity" window appeared but never took focus.
- The fix is not implemented: `Start-UnityQuiet` sends only `-nographics` launches to the hidden desktop `WinSta0\PPHeadless`. Testing it needs a tooling change first. Open question: does Unity get a working graphics device on an unwatched desktop?
- Suggested next step: launch one `-Runtime` capture through `DesktopLauncher.Launch` on PPHeadless, watched by `focus_check.ps1 -Mode Command`.

## 3. Intent badges after 6e8c71d5 (#79, #211, M10)
[intent-badges/Fight_identity_plates.png](intent-badges/Fight_identity_plates.png) (real run fight: Ironback Beetle and Giant Rat), [treant/treant_turn1_trunk_slam.png](treant/treant_turn1_trunk_slam.png)
- Giant Rat (trimmed idle): badge bottom y~447, ear tips y~498, gap ~51px; badge centred over the body at x~1620, ~40px right of the head.
- Ironback Beetle: badge bottom y~527, antenna tops y~585, gap ~58px.
- Treant: ~85px above the canopy.
- Rat and beetle gaps match, so the old bug (the rat's badge low by its crop offset) is gone. No badge sits on a body or shoulder. The gap is a full badge-width of air ("above" rather than "just above"). The treant's larger gap puts its badge near the plate column.
- For M10: looks right for the rat; glance at the treant before deciding it is too high.

## 4. Expansion spell books (879a408f)
[shop/Shop.png](shop/Shop.png), [spellbooks/book_icons_contact_sheet.png](spellbooks/book_icons_contact_sheet.png) (composite of all 13 48x48 icons at 48px, 28px and 2x)
- In the shop the icon draws at ~28px; the glyph is a 3-5px dot and only the cover colour tells books apart.
- Eight of thirteen covers are the same purple (Borrowed Moment, Censer of Embers, Court of Whispers, Crownfall, Gilded Aegis, Palace Passage, Thorn Tithe, Velvet Shackles) and cannot be told apart at game size (ties into owner call G6).
- Blackglass Spear's near-black cover almost disappears on the near-black card.
- At 2x the glyphs are distinct and on theme: the problem is size, not the art.
- Not captured: dossier spell slots, fight skill-row marks, reward screen (no fixture renders an expansion book there).

## 5. Owner-digest items
- **#45 detail card vs the far enemy's feet** ([fight-menu/FightMenu_2_skill_submenu.png](fight-menu/FightMenu_2_skill_submenu.png)): with the skill submenu open and three enemies, the card (x 1620-1880, y 742-967) starts ~75px below the rightmost enemy's (Bog Witch) feet ring and covers no enemy. The one-enemy SixSkills frame also clears. Not tested: a treant in that slot (its status badges already sit at y~725).
- **A1 Cinderfault on one rat / a rank** (`cinderfault/spell_cinderfault_vs_rat_impact.png`, `cinderfault/spell_cinderfault_vs_rat-rat-rat_impact.png`, `cinderfault/*_ground.png`): one rat gets a compact burst of spikes and embers at its feet about its body width; a rank of three gets one burst per rat joined by a thin ember crack, reading as three hits rather than one fault line. The rank's log line runs from x 410 past x 1650 and slides under the enemy plates, hiding its end.
- **A2 sky spells vs the tall treant**: all three overlap the log text. Winter's Rebuke starts at y~85 across log lines 2-4; Blackglass Spear starts off-screen across both log lines; Crownfall's crown top at y~95 over log line 2, and it hangs above empty ground at x~990, left of the treant at x~1320.
- **A3 bloom on menus/hub** ([bloom/MainMenu_t7.0s.png](bloom/MainMenu_t7.0s.png), plus `_t1.5s`, `_t15.0s`): the lantern halo looks painted into the art, not bloom; lit palace windows have crisp edges, no glow spill, matching "bloom never worked on menus". The hub was not captured (every hub fixture opens a menu over it). The capture camera does enable post-processing (`CanvasCapture.cs:115`), so bloom would show if active.
- **M8 "+N XP" text on the XP bar**: not captured; no fixture reaches the end-of-fight XP rows. In [reckoning-xp/Reckoning_choice.png](reckoning-xp/Reckoning_choice.png) the per-character tooltip is already open over the middle coif while the pad marker is on the left coif.
- **M9 dossier tooltip vs mannequin** (`dossier-tooltip/Dossier_tooltip_topright.png`, `dossier-tooltip/Dossier_tooltip_bottomleft.png`): hovering the Sovereign Leather Coif opens a 420x420 tooltip over the mannequin's head and left shoulder and the HEAD, NECKLACE and GLOVES slots. The Torso tooltip clears the mannequin but covers NECKLACE, GLOVES, LEGS and the pack's Carried count. The fixture hovers pack cells, not the Legs slot itself; a Legs-slot tooltip would sit closer to the mannequin. Both tooltips are about half empty below their text. In the bottom-left frame the pad marker (▼) is drawn over the word "Gloves".

## Files

| Folder | Contents |
|---|---|
| `attributes\` | 1 PNG |
| `fight-menu\` | 3 PNGs + `run.log` |
| `skills-list\` | 2 PNGs |
| `shop\` | 3 PNGs + `run.log` |
| `enemy-plates\` | 2 PNGs |
| `intent-badges\` | 1 PNG |
| `spear-layers\` | 15 PNGs (3 spells × 5 frames, vs treant) |
| `thorn-tithe\` | 10 PNGs |
| `cinderfault\` | 12 PNGs |
| `treant\` | 4 PNGs |
| `dossier-tooltip\` | 4 PNGs |
| `reckoning-xp\` | 2 PNGs |
| `bloom\` | 3 PNGs |
| `spellbooks\` | `book_icons_contact_sheet.png` |
| `focus\` | `focus_check_runtime_baseline.log` |

## Open questions from the capture pass
1. The dirty ChakraPetch font asset could be the cause of the blank attribute rows; confirming needs either the font change discarded (owner digest H2) or a TestRunner-only experiment.
2. #204 needs a tooling change to route graphics launches through PPHeadless before its proof can run.
3. M8, the ward segment, recoil tiers, the treant ability VFX, and the book icons in the dossier, fight rows and rewards all need capture fixtures that do not exist yet.
