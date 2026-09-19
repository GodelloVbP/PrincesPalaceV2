# Archive — plan and handoff docs whose work landed

Per `docs/WORKFLOW.md` §11: a `PLAN_*`/`HANDOVER_*` doc moves here once its last
phase lands; a handoff's whole directory moves to `docs/handoffs/archive/<slug>/`
once its feature lands. Nothing here is current design — read the live doc or
the code it points at instead. One row per file, newest archival pass first
within each batch.

## Archived 2026-09-19 (this pass)

| File | What it was | When it landed | Where the live behaviour is now |
|---|---|---|---|
| `PLAN_STATUS_EFFECT_UI.md` | Design for the per-combatant status-badge HUD row: codes, colours, overflow, the frozen art contract | All three phases shipped; last commit `b517ee97` "Status HUD phase 3: polish (plan section 9)" | `StatusIconImportPostprocessor.cs`, `StatusBadgeIconTests.cs`, the fight HUD's status row; art contract still lives at `docs/STATUS_ICON_PROMPTS.md` (kept live, frozen) |
| `PLAN_BATTLE_SPEED.md` | Battle speed as an in-fight preset table, revision 3 | Shipped; last commit `773b9d21` "Battle speed: G6, regenerated scenes and docs" | The Options pane's speed row, `GameSettings`, `FightBootstrap`'s pace source |
| `PLAN_PRISMATIC_ORB.md` | Odette's Prismatic Orb skill and the element-choice mechanic | Shipped; last commit `bcc52c1a` | `PrismaticOrbTests.cs`, `ElementChoice`, Odette's skill row in `skills.json` |
| `PLAN_REWARD_TRACKS.md` | Per-character reward tracks replacing squad-wide milestone modifiers | Shipped; last commit `05d1f930` "Delete the SignatureAbsorbs alias..." | `RewardTrackController.cs`, `reward_tracks.json`, the Reward Track screen |
| `PLAN_SPELL_LAYERS.md` | Layered spell presentation (revision 4): ordered authored layers replacing fixed flight/trail/contact/particle blocks | Shipped, M0-M9, pilot Water + Cinderfault validation; last commit `bcc52c1a` | `SpellVfxRecipeDriftTests.cs`, `SpellEmitter`/`SpellEmitterSim`, `docs/ART_PIPELINE.md` §5b's authoring loop |
| `HANDOFF_SPELL_LAYERS_REVISION.md` | Owner's revised brief that `PLAN_SPELL_LAYERS.md` was written against | Landed with the plan above | Superseded by the shipped layered-spell system; historical context only |
| `BUG_HUNT_2026-09-08.md` | Overnight six-hunter bug hunt record (combat, run/dungeon, UiKit, Core/Domain seam, tools, speed/Reach follow-up) | Completed hunt, gated into main by `0184be2e` | Findings folded into `AUDIT.md`/`docs/AUDIT_STRUCK_ARCHIVE.md`; this doc is the hunt's own method/result record |
| `BUG_HUNT_2026-09-10.md` | Four-hunter bug hunt (combat/pool model, run-outside-combat, fight stage, verification tooling) | Completed hunt, gated into `prismatic-orb` | Same as above |
| `BUG_HUNT_2026-09-11.md` | Stages 1-2 of the total bug hunt: obligation manifest, runner audit, four mechanical stage-2 passes | Completed; `docs/hunt/MANIFEST.md` is the durable artifact | `tools/hunt_manifest.py`, `docs/hunt/MANIFEST.md`; findings in `AUDIT.md`/archive |

**Handoff directories moved to `docs/handoffs/archive/<slug>/` this pass:**

| Slug | What it was | Evidence it landed |
|---|---|---|
| `party_screen` | Party roster/formation screen design (seats, benching, drag-and-drop) | `GAP_AUDIT.md`: 24 of 25 audited rows "match"; `PartyScreen.cs`/`PartyController.cs` built and wired in `ScreenRegistry.WireParty` |
| `relic_screen` | Relic/equipment screen redesign (shared pool, per-hero equip) | `RelicDraftScreen.cs` built and wired via `ScreenRegistry.WireRelicDraft`; substantial commit history under the handoff path |
| `system_menu` | System menu shell, tabs, and per-tab panes | `SystemMenuScreen.cs`/`SystemMenuController.cs`/`SystemMenuTabs.cs` built and wired; five+ landed commits against the handoff path |
| `spell_layers` | Owner's brief answering the layered-spell-presentation plan (duplicate of `HANDOFF_SPELL_LAYERS_REVISION.md`, kept alongside the plan it answers) | Landed with `PLAN_SPELL_LAYERS.md` above |

## Left in place (checked this pass, not archived)

| File or directory | Reason left in place |
|---|---|
| `docs/PLAN_SHOP.md` | Gate 4 ("the loadout flip"), the plan's own last phase, is unstarted; Gate 2/3 human checklists are explicitly recorded as not run |
| `docs/handoffs/shop/` | Tied to `PLAN_SHOP.md` above — its own gates are still open |
| `docs/handoffs/shop_v2/` | Tied to `PLAN_SHOP.md` above — its GAP_AUDIT still has real "not built" rows (keyboard nav, Escape handling, human checklists) |
| `docs/PLAN_SPELL_BOOK_ART.md` | Reference/spec doc, not phase-gated — describes an ongoing art production convention rather than a plan with a last phase |
| `docs/PLAN_SPELL_FEEL.md` | Reference/foundational design doc, not phase-gated; `PLAN_SPELL_LAYERS.md` explicitly "extends, does not contradict" it |
| `docs/PLAN_BALANCE_BOT.md` | Living reference for a tool that exists — kept live on purpose |
| `docs/GAMEPAD_NAVIGATION_PLAN.md` | Active |
| `docs/GAMEPAD_NAVIGATION_RESEARCH.md` | Active |
| `docs/handoffs/progression_v2/` | Its own status line says phases 4-6 are "held until" three owner decisions land — not landed |
| `docs/handoffs/talent_tree/` (v1, vine layout) | `talent_tree_v2` (already archived) says it "replaces the hero-switcher vine tree" — v1 was superseded before build rather than landed; the archival rule as written covers "feature landed," not "superseded pre-build," so this is the owner's call |

## Archived earlier (best-effort description, not re-verified this pass)

| File | What it was |
|---|---|
| `2026-08-01_BATTLE_ART_PROMPTS.md` | Image-gen prompts for Floor 1 battlefield backdrops — marked "delivered" at archival |
| `2026-08-01_HANDOFF.md` | A point-in-time session handoff — marked "superseded" at archival |
| `2026-08-01_HUB_ART_PROMPTS.md` | Image-gen prompts for the Divine Principality hub art — marked "delivered", all six pieces wired in |
| `2026-08-01_HUB_DESIGN_BRIEF.md` | Design brief for the main hub — marked "delivered" |
| `2026-08-01_RUN_MAP_ART_PROMPTS.md` | Image-gen prompts for the forest-floor run map nodes — marked "delivered" |
| `2026-08-01_SYSTEMS_PLAN.md` | Plan for rarity, rewards, the infinite descent, and a dynamic Reckoning — marked "partially superseded" |
| `ARCHITECTURE_AUDIT_FIXED_FINDINGS.md` | Companion archive to `architecture_audit.md` Part III, same one-line-stub/full-write-up scheme this pass used for `AUDIT.md` |
| `CINDERFAULT_ASSET_PROMPT_WORKFLOW.md` | Cinderfault spell's asset/prompt production workflow |
| `HANDOVER_PROGRESSION_TRACK.md` | Design record for the level reward track (predecessor to `PLAN_REWARD_TRACKS.md`'s per-character redesign) |
| `PLAN_PROGRESSION_TRACK.md` | Build-order plan companion to the handover above |
| `STATIC_COMBAT_ART_DEEP_DIVE.md` | Production recommendation for static combat art techniques; superseded by `docs/ART_PIPELINE.md` |

**Handoff directories archived earlier:**

| Slug | What it was |
|---|---|
| `adaptive_music` | Systems handoff: vertical-layered adaptive music per floor |
| `battle_ui` | Battle UI v2 rework: break meter, statuses, roster, ceremony |
| `cinderfault` | Implementation record for the Cinderfault spell, against its own `HANDOVER.md` |
| `dossier_containers` | Art brief: painted containers for the Character/Inventory dossier |
| `floor_2` | Design doc for Floor 2 ("The Bellows"), steampunk biome — not yet built as of archival |
| `reward_track` | Art brief for the reward track (written after the screen was already built) |
| `run_map` | Handoff: forest-floor run map, image-based nodes |
| `shawn_talent_rework` | Mechanics handoff: Shawn's talent rework, Wool as a spend resource |
| `talent_tree_v2` | Talent tree redesign: 3x3 root grid with double convergence, replacing the vine-tree layout in `docs/handoffs/talent_tree/` (left in place — see report) |
| `xp_bar` | Art brief for the experience bar (seeded from the existing implementation) |
