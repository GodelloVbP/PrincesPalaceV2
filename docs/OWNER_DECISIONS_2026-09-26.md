# Owner decisions digest, 2026-09-26

Every open item from `AUDIT.md` at `267b028e`, plus five working-tree items from today, has been checked
against the owner's recorded answers (memory files), the git log and the current code.

| Status | Count | Meaning |
|---|---:|---|
| **Answered** | 9 | You already decided it, or it is already built. No question, evidence in the table at the end. |
| **Moot** | 4 | The thing it was about no longer exists. |
| **Open** | 67 | 43 entries in 42 short questions (C, P, M, G, A, H1-H5; #124 is split across C7/M1, #129+#133 share A5, #173+#191 share P1), plus **24 code-housekeeping entries under one "approve all" (H6)**. |

Markers: **[PHONE]** answerable now. **[PAD]** needs a controller in hand. **[DESK]** needs a look at the running game.
Reply format: `C1 A, C2 B, ...` is enough. Anything you skip keeps today's behaviour.

---

## Combat & balance

**C1. Two elite treants take ~29 party actions to beat. The design target is 9-11.** [PHONE]
A) Make the treant less tanky in the next balance pass, measured with the bot. B) Accept long elite fights and widen the target. C) Raise basic attack damage again.
*Recommend A: a fight three times the target is a slog, and changing basic attack moves every fight.* [#201, `Tests/PlayMode/Combat/BalanceSheetTests.cs`]

**C2. The Forest Warden's summoned rats each pay full XP and gold, with no limit.** A player can stall and farm them. [PHONE]
A) Summons pay nothing. B) Each kind pays once per fight. C) They pay a fraction. D) Leave it.
*Recommend A: the kill relics (Essence Siphon, Inconspicuous Key) already ignore summons, so this matches.* [#97, `FightSession.Outcome.cs:93`]

**C3. A taunted monster can stay taunted all fight if it keeps using abilities instead of plain attacks.** You reported this ("stays taunted for ever"). [PHONE]
A) The taunt ends after the monster's next turn, including a turn lost to stun. B) It ends after the next turn the monster actually acts.
*Recommend B: the taunt then always redirects exactly one action.* [#105, `FightSession.Enemies.cs:1096`]

**C4. Velvet Shackles (9 mana) blocks physical actions only.** Most monsters lose the turn. The Bog Witch is unaffected, the boss roars instead, and the beetle heals itself instead. [PHONE]
A) Accept it: the card already says "only cast". B) Make it cheaper or longer. C) Also block heals and buffs (a new rule).
*Recommend A: choosing the right target is the skill.* [#194, `docs/PLAN_SPELL_EXPANSION.md` s.5]

**C5. The Bog Witch's Mud Burst only hits your 2nd and 3rd party slots.** With nobody there, she swings instead. [PHONE]
A) Keep it. B) Let it hit anyone again.
*Recommend A: it fits the fiction and makes formation matter.* [#83, `skills.json:1671`]

**C6. When the balance bot pulls hurt front-liners back with Move, it dies more often than when it plays plainly aggressive.** [PHONE]
A) Check whether the bot plays Move badly before changing anything. B) Make Move cheaper (for example, it no longer ends the turn).
*Recommend A: it costs nothing, and the bot's Move logic is crude.* [#82, `reports/bot/20260907-114015`]

**C7. A kill by poison pays no per-kill relic.** For example, Bounty Hunter Contract does not pay. [PHONE]
A) The Contract pays on poison kills too. B) Leave it.
*Recommend A: the Contract is written "per body". The other three per-kill relics cannot pay without an attacker anyway.* [#124(c), `FightSession.Ledger.cs:425`]

**C8. A stunned (or helplessly rooted) turn still counts as "taking an action".** Nothing shipped changes visibly today. It would matter for a future resource that decays while you are idle. [PHONE]
A) A skipped turn is not an action. This matches your ruling that a transform running out is not one. B) Keep it.
*Recommend A: consistency, and a one-line change.* [#210, `FightSession.Enemies.cs` ForfeitTurn]

**C9. The 4x-slower hit recoil you asked for.** The next blow waits only when it hits a body that is still recoiling. A third monster can still be easing back while the next blow lands elsewhere. Waiting for everyone adds about 4.4 s per six-blow round. [PHONE]
A) Keep the narrow wait. B) Wait for everyone.
*Recommend A: B more than doubles round length.* [#184, `FightBeatPlayer.cs:1384`]

## Controls / gamepad

**P1. Start opens the menu from Hub, Map, Fight, Shop and Events.** While a pop-up is open (glossary, relic draft, end-of-fight Reckoning) the press is ignored. [PAD]
A) Keep ignoring it while a pop-up waits for a choice. B) Open over pop-ups (real lifecycle work). C) Only the Reckoning and relic draft open it (one line each).
*Recommend A.* [#173 + #191, `NavContext.cs:144`]

**P2. Holding the stick steps once and does not repeat.** You asked for this because the old repeat was random. Long lists like the reward track now take many flicks. [PAD]
A) Keep it. B) Build a clean hold-to-repeat that starts after a short delay.
*Recommend B if flicking along the reward track annoys you.* [#167, `NavigationInputModule.cs:132`]

**P3. From the Hub gate, Left now goes to Talents first, then Principality (screen order).** Your 2026-09-18 wording was the reverse. Your 09-19 report ("left twice you are left middle") matched screen order. [PAD]
A) Keep screen order. B) Your original wording.
*Recommend A.* [#180, `HubController.cs:259`]

**P4. When picking an ally, Right moves to whoever stands to the right on screen.** Because your party is mirrored, that is the nearer ally. [PAD]
A) Keep screen direction. B) Use list order regardless of the mirror.
*Recommend A.* [#190, `FightController.Input.cs`]

**P5. While inspecting monsters with the pad, Up/Down step through the monsters.** Changing your action means pressing Left or B first (3 presses instead of 2). [PAD]
A) Keep it: B always returns you to the action you were on. B) Up/Down jump straight back to the action list.
*Recommend A.* [#176, `FightController.Input.cs:1317`]

**P6. Start while carrying a character on the Party screen.** The first press puts the character back down, and the second closes the menu. [PHONE]
A) Keep it. B) Close at once and drop the carry.
*Recommend A: no half-finished carry.* [#174, `SystemMenuController.cs:245`]

**P7. The focus arrow sits left of wide buttons and above everything else.** On Talents it covers the orb's name. On the Reckoning it sits under "CHOOSE ONE". [DESK]
A) Keep one rule. B) Add a per-button override for those two.
*Recommend A unless it bothers you.* [#172, `FocusMarkerPlacement.cs:58`]

## Menus & screens

**M1. The end-of-run stats "Shielded" row only counts wool soaking damage.** Wool soaking is switched off, so the row always reads 0, even though shields now eat real damage. [PHONE]
A) Count shield absorption in it. B) Rename it. C) Remove the row.
*Recommend A: the name already promises that.* [#124(b), `CombatLedger.cs:33`]

**M2. On the save screen, every filled slot wears the gold "recommended" ring.** That can be up to three at once. [PHONE]
A) Gold ring only on the most recently played slot. B) No gold ring, and a plain "has a save" look. C) Keep it.
*Recommend A.* [#175, `SaveSlotController.cs:108`]

**M3. "You already have this spell prepared" is printed across the first spell slot's name.** [PHONE]
A) Show it on the slot you pressed. B) Give it its own line under the slots (a layout change).
*Recommend A: cheapest, and it is where you are looking.* [#146, `CharacterDossierController.cs:1412`]

**M4. The Talents screen no longer says whose tree it is or which constellation is open.** You cut those labels. Only Shawn has talents today. [PHONE]
A) Leave it. B) Add a small constellation indicator back.
*Recommend A until a second character gets talents.* [#178, `TalentScreen.cs:285`]

**M5. The shop's "SHOPKEEPER" portrait was a placeholder with no art.** It was removed to make room for the item comparison panel. [PHONE]
A) Accept it. B) Bring back a small header once portrait art exists.
*Recommend A.* [#197, `ShopScreen.cs`]

**M6. On the attributes panel, the WIS line shows the actual regen number.** DEX and CHA show what every 2 or 4 points buy, because a one-point step shows "+0" half the time. [PHONE]
A) Keep the mix. B) Show all three as per-point fractions (+0.25 and so on).
*Recommend A: whole numbers read better.* [#199, `AbilityEffectDescriptions.cs`]

**M7. Playtime keeps counting while the pause menu is open.** [PHONE]
A) Keep it ("time with the game open", as Steam counts it). B) Pause the clock in menus.
*Recommend A.* [#142, `GlobalStateLifecycleTests.cs:237`]

**M8. On the end-of-fight XP screen, the "+N XP" text sits on the right end of the XP bar.** [DESK]
A) Move it onto the top line beside the level. B) Make the rows taller. C) Style the overlap as intentional.
*Recommend A.* [#44, `ReckoningScreen.cs:705`]

**M9. Next to the Legs slot, the dossier item tooltip covers the mannequin drawing.** [DESK]
A) Accept it. B) Anchor the tooltip so it clears the art.
*Recommend B.* [#202, `ItemComparisonPanel`]

**M10. The monster intent icon now sits higher on some monsters (a measurement bug fix from today).** Nobody has looked at it in-game yet. [DESK]
A) It looks right. B) Too high. Say which monster.
*Recommend: glance at a rat fight.* [#211, `6e8c71d5`]

## Content & progression

**G1. If you quit mid-run, the run is gone when you come back.** It is settled as if it had ended. "Continue" opens your save, not the run. [PHONE]
A) Keep it: a run lives in one sitting. B) Build save-and-quit resume (a real feature, several days).
*Recommend A now, and B later if playtesters lose runs to closed windows.* [#123, `RunManager.cs:65`]

**G2. You said you wanted "banks" during a run where some gold can be stored safely.** Gold is lost on death today. The run-map rebuild is being planned right now. [PHONE]
A) Reserve a bank stop in the run-map plan. B) Drop the idea: gold is at risk until the run ends. C) Decide later.
*Recommend A: it is cheap to reserve while the plan is open.* [#37, `CurrencyType.cs:9`]

**G3. The cheapest floor-1 gear went from 20g to 35g (about 1.75x). You asked for about 1.5x (about 30g).** [PHONE]
A) Bring it down to 1.5x as asked. B) Keep 1.75x.
*Recommend A.* [#196, `ShopPricing.GearBase`]

**G4. Spells' level-based power stops rising at level 9. Levels now run to 40, and 30 is completion.** From level 9 on, spells only grow through the reward track and gear. [PHONE]
A) Level 9 is the ceiling. B) Extend the curve to level 30 in the next balance pass.
*Recommend B: 31 flat levels makes a caster like Odette feel dead.* [#135, `spells.json`, 9 tiers]

**G5. A first-time boss kill pays one ember, but only as a line on the end-of-run screen.** You asked that it "feel very rewarding". [PHONE]
A) Add a moment right after the boss fight (the ember drops, with a card). B) The end-of-run line is enough.
*Recommend A.* [#57, `DefeatController.cs:143`]

**G6. Three status spells declare no element, so their book covers use the grey Arcane fallback:** Velvet Shackles, Censer of Embers, Thorn Tithe. Their glyphs landed today. [PHONE]
A) Censer = Fire, Thorn Tithe = Nature, Shackles stays Arcane. B) All stay Arcane.
*Recommend A. Check first whether Burn's damage type follows the spell's element.* [#198, `879a408f`]

## Art

**A1. Cinderfault and Spore Cloud now stretch across the monsters' drawn bodies, not their picture boxes.** A lone rat's fault line is much shorter. [DESK]
A) Good. B) The old look was better.
*Look at Cinderfault on one rat and on a full rank.* [#206]

**A2. Against a tall treant, Winter's Rebuke, Blackglass Spear and Crownfall start under the combat-log text.** [DESK]
A) Lower the sky height cap. B) Accept the overlap on tall monsters.
*Recommend A.* [#205, `SpellFlight.SkyCeiling`]

**A3. The soft "bloom" glow never worked on menus or the hub.** The lantern and palace never glowed. Only Cinderfault glows now. [DESK]
A) Give a glow to the specific art that should shine (lantern, palace). B) Lower the threshold (the whole skyline glows). C) Raise the strength.
*Recommend A.* [#181, `PipelineBuilder.cs:161`]

**A4. Gilded Aegis and Borrowed Moment target an ally, but their effect is drawn on the caster.** [PHONE]
A) Draw it on the ally. B) Keep it on the caster.
*Recommend A.* [#208]

**A5. Spare art nothing plays:** (1) the old 15-frame water orb, replaced by the five-layer version; (2) four monster poses (golem guard, rat guard, rat extra, bog witch taunt). [PHONE]
A) Delete both. B) Keep both. C) Delete the old orb (git history keeps it) and keep the poses for future monster skills.
*Recommend C.* [#129, #133, #145 B10(a)]

## Tooling / repo housekeeping (all [PHONE])

**H1. `AGENTS.md` has an uncommitted rewrite.** It drops the pinned Codex model names (GPT-6 Astra / GPT-5.6) and the Codex "orchestrator never edits" rule. Roles stay, but the models are no longer pinned.
A) Commit it. B) Discard it.
*Recommend A if you made it on purpose. Note that it loosens Codex's orchestrator-only rule, which Claude's CLAUDE.md keeps.*

**H2. The Chakra Petch font asset has its glyph tables and atlas cleared (1024 → 1x1).** This is dynamic-atlas churn: `ClearDynamicDataOnBuild: 1`, and the last intended regeneration was `3573b566`.
A) Discard. B) Commit.
*Recommend A.*

**H3. `Art/Events/` is untracked and holds only an empty `demo_wishing_well/` folder.** The wishing well's page has no art (`artPath: ""`). Separately, the Petting Zoo pages point at `Art/Events/petting_zoo/zoo.png`, which does not exist in the tree.
A) Delete the empty folder and let `Events/` land with the first real event art. B) Commit it as is.
*Recommend A. The zoo art is still owed.*

**H4. Three leftover worktrees:**
- `agent-a97ff…`: 16 files from 2026-09-05. It is a per-ability "scaling" block on skills. Main took a different design (`scalingAxis`), and this copy is 3 weeks stale.
- `agent-aab5…`: `BattleSpeed.cs` + tests. These are identical to main except one comment word; battle speed has already shipped.
- `dreamy-thompson` (clean, detached): a rat-rig commit `9ab61137` that is not in main. Rigs were retired by the stills-only policy.

A) Discard all three. B) Keep one (name it).
*Recommend A: nothing in them is missing from main that main still wants.*

**H5. The spell-book README points at `output/spell-books/preview-expansion-48.png`.** That file exists on this disk only, because `output/` is untracked.
A) Move the PNG into `docs/captures/` and repoint the link. B) Drop the sentence.
*Recommend A.*

**H6. Approve all 24 code-housekeeping recommendations below in one go?** Each one is invisible to the player or trivially small.
A) Approve all. B) Approve all except the numbers you name.
*Recommend A.*

| # | What it is | Recommended |
|---|---|---|
| 84 | Move row says "NO ROOM" when the real reason is "rooted" | Say "ROOTED" |
| 86 | A track reward with no amount validates and grants nothing | Add a per-reward amount rule |
| 87 | Unused "declared boss" fields | Keep until the run-map rebuild decides bosses, then wire or delete |
| 89 | Party size 3 is typed in three places | One constant |
| 91 | Menu tab-fit check cannot fail with 3 tabs or fewer | Check each label against its own box |
| 92 | Duplicate sprite path mixes two actors' hover data | Refuse duplicates at build |
| 94 | `preview.ps1`/`build_content.ps1` treat an unreadable Unity process as a lock (the test gates are already fixed) | Same fix as the gates |
| 95 | `.meta` edits never trigger tests | Leave it (noise); gotcha 3 is covered by diffing `Art/` |
| 96 | UI-kit splicer reports "gave up" as a 50% inset | Report "unmeasured" and fail the splice |
| 98 | `measure_stage.py` is not in the gate | Wire it in (Pillow is already a tools dependency) |
| 102 | Bot's in-memory save keeps a live object | Copy on save: accuracy over speed |
| 103 | Staging hook scans commit-message text (recurred today) | Stop scanning message text; also refuse a bare commit chained after a refused add |
| 111 | "Absorbs damage" switch on resource pools does nothing | Remove it: shields are how absorbing works now |
| 112 | `shopStockVersion` is written, never read | Keep it as a record |
| 120 | Two null-handling styles in one method family | Make all nine tolerant (house style) |
| 122 | Shop sell headroom 12 is labelled provisional | Keep 12, remove the label |
| 41 | Old shared Embers wallet field | Delete at the next save-version bump |
| 63 | A monster's kills are not recorded | Leave until a screen shows monster kills |
| 124(a) | Hit effects can fire on a body that already fell | No effects on an already-dead target |
| 137 | A crashed bot shard undercounts the batch silently | Warn and self-correct the count |
| 143 | A test-scenario doc row contradicts shipped VFX design | Fix the doc row |
| 145 | Leftover resolver forks: B8 unique plate colour; B9 `restoredByManaEffects`; B10(b); B11 | B8: add the check (6 colours, 3 characters). B9: no, today's reading is right. B10(b), B11: close, no rule stated |
| 179 | Four one-way pad links (debug menu, glossary, system-menu tab, talent pager) | Make debug and glossary two-way, keep the system-menu tab as is, return the talent pager to its arrow |
| 200 | Test damage literal 8 was never traced by hand | Trace it once |
| 207 | Spell preview captures are labelled one step early | Fix the capture anchor |

Also part of H6, not a decision: the `characters.json` readme still cites a level-60 wool-absorb reward, and the track ends at 40. The text will be corrected (see #121 below).

---

## Answered: closing, no question needed

| # | Closing as answered | Evidence |
|---|---|---|
| 54 | Elites got their stat boost (HP x1.40, ATK/DEF x1.15) | `353c3175`; `Core/FightEncounterAdapter.cs:62-89` |
| 58 | Shawn's second "level ladder" is gone: only Shear unlocks at level 1, and everything else is granted by the tree or track (unlockLevel 1 or 999 only) | progression v2, memory `party-pass-2026-09-15.md:67`; `skills.json` sheep rows |
| 65 | A poison death now counts as "went down" | `f096823e` |
| 85 | Affix jackpot odds (`MaxStep`): no gear balance work until the items redesign, your call | memory `owner-decisions-2026-09-11.md:19-20` |
| 104 | Dossier spell-book list: books now have icons (17/17) and rows draw icon + selection | `CharacterDossierScreen.cs:107-108`, `879a408f`; hands-on QA still pending |
| 121 | Wool absorb stays off, your call. Automatic absorb was also rejected by the bot, and Tuck In (a shield you press) replaced it | `owner-decisions-2026-09-11.md:17-18`; `party-pass-2026-09-15.md:64`; `reward_tracks.json:21`. Residue: stale readme text, fixed under H6 |
| 125 | Item sets: "leave for now, redesign later" | `owner-decisions-2026-09-11.md:19-20` |
| 128 (+ #145 B7) | `three_bosses`: "leave" (game heavy in development). So no build check that would fail it either | `owner-decisions-2026-09-11.md:25` |
| 134 | Bjorn's track can now pay into Fury (`FuryGainOnAttack`, `FuryStartOfFight`) | progression v2 phase 4; `RewardTrackContentPinTests.cs:229`. Odette's zero talents is authoring still to do, not a decision |

## Moot: the subject no longer exists

| # | Why moot | Evidence |
|---|---|---|
| 45 | The 340x300 detail panel over the front enemy is gone. The card is now 260 wide, anchored to the screen's right edge | `FightScreen.cs:2411-2414`. Not re-checked on screen against the far enemy slot |
| 51 | "Extra relic slot" track rewards no longer exist; the tracks were re-authored level by level | `reward_tracks.json` (no relic-slot reward) |
| 88 | `UnlockedAmount` now has four callers | `ContentDatabase.Effective.cs:317,835,838,889` |
| 150 | Shawn's level 30 is now the placeholder capstone ability you chose in progression v2, not a static_fleece stand-in | `reward_tracks.json:36`. What the capstone does is still yours to design, and is not an AUDIT item |
