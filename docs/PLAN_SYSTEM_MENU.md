# System menu rebuild

The in-game menu opened by Esc / Start over the Hub, Map (with Shop and
Event) and Fight. Research, captures and the two review rounds behind this
plan: https://claude.ai/artifact/6CTjZvVkVxgRWawYHjbKdR.

Owner decisions (2026-09-29): one menu, not a pause card plus a game menu.
Spells and skills are one concept, and spell books are equipped beside the
gear. Glossary gets its own tab. In a fight everything is viewable and
nothing can be changed. The last tab is labelled EXIT.

Out of scope: a visual re-skin, a text-size option (the layout rules below
keep it possible, see C14), using consumables outside a fight, Start over
the Reckoning and Relic draft (AUDIT #191), and any save-format change.

## Behavioural contracts

### The frame

**C1 Tabs.** Built from one table, `SystemMenuTabs`. Each entry carries a
key, label, group, visibility by context (C2), hotkey (C8) and pane. A tab
with no built pane fails the build; placeholders are never shown to
players.

| Tab | Group | Camp | Descent | Fight | Hotkey |
|---|---|---|---|---|---|
| CHARACTER | game | ✓ | ✓ | ✓ | C (I opens it on the pack, C4) |
| PARTY | game | ✓ | ✓ | ✓ | P |
| RUN | game | – | ✓ | ✓ | R |
| GLOSSARY | game | ✓ | ✓ | ✓ | G |
| OPTIONS | system | ✓ | ✓ | ✓ | – |
| EXIT | system | ✓ | ✓ | ✓ | – |

The game group sits left; the system group sits right, set off by a wider
gap and a quieter label colour. The bar is laid out from measured label
widths only. The authored `LabelWidth` column goes, and the build measures
with TMP the same way `SystemMenuController.ApplyContext` does at runtime.
That keeps the bar correct under localisation and text scaling.

**C2 Context.** Every menu instance has one `MenuContext`: `Camp` (Hub),
`Descent` (Map, Shop, Event) or `Fight`. It replaces the `inDescent` flag on
`SystemMenuController` and the `lockedForFight` flags on the dossier and
Party controllers. Panes read `menu.Context`; no pane keeps its own flag.

**C3 Permissions, and fight is read-only.** Every action that changes game
state is a `MenuAction`, allowed or refused by one Domain table
`MenuPermissions.Allows(action, context)`. The table below is the whole
rule:

| Action | Camp | Descent | Fight |
|---|---|---|---|
| Equip / unequip gear | ✓ | ✓ | – |
| Place / replace / remove a spell book | –¹ | ✓ | – |
| Spend an attribute point | ✓ | ✓ | – |
| Refund an attribute point | ✓ | – | – |
| Swap / place / bench a party member | ✓ | – | – |
| Reposition party (seated ↔ seated) | ✓ | ✓ | – |
| Collect reward-track rewards | ✓ | ✓ | – |
| Cycle a character's title | ✓ | ✓ | – |
| Change options | ✓ | ✓ | ✓ |
| Exit actions | ✓ | ✓ | ✓ |

¹ Books exist only inside a descent (`RunSnapshot.unassignedSpellBooks`).

The same table decides both what a control does and how it draws, so the
two cannot disagree. A refused control keeps its normal look and stays
focusable and inspectable. Pressing it shows the notice (C11) with the
reason from the table, for example "Locked during a fight". The domain
operations (`RunOrchestrator.LearnSpell` / `ReplaceSpell`,
`EquipmentOps.Equip` / `Unequip`, `TalentOps.Invest` / `Refund`, the
reward-track claim, `PartyFormation`) take the context and refuse through
the same table. That guards every caller, not just the menu.

**C4 Open and close.**
- Esc or Start opens the menu from any scene root (Hub, Map, Shop, Event,
  Fight) and pauses (unchanged).
- Esc or Start while the menu is open closes it, unless the pane claims the
  press (C5).
- A hotkey opens the menu on its tab. From inside the menu it switches to
  that tab. Pressing the key of the tab already showing closes the menu.
- I opens CHARACTER › LOADOUT with focus in the pack.
- The pad View button opens RUN in a descent or fight, and CHARACTER at
  camp.
- Hub buildings open the menu through the same deep-link API (C9):
  CHARACTER SHEET opens CHARACTER, the glossary building opens GLOSSARY,
  and the corner Main Menu button opens EXIT. That last one removes its
  single-press run end (AUDIT #219).

**C5 Cancel / B** undoes the innermost pending thing: a picked-up item,
book or party member, then an open detail or compare card. With nothing
pending it closes the menu. Moving focus between the rail and content is
spatial (D-pad), not a level, so B never walks back through regions. This
keeps the owner's "Cancel steps back a level first" rule, and the levels
are now the pending states only.

**C6 Switching.**
- LT / RT step tabs and wrap over the visible tabs. Focus lands on the
  pane's entry control.
- LB / RB (Q / E) page within the tab: characters in CHARACTER, categories
  in GLOSSARY, relic pages in RUN. They do nothing in PARTY, OPTIONS and
  EXIT, and no prompt is shown there.
- Mouse: click a tab, click anything.

**C7 Reopen rule.** The menu reopens on the last game-group tab used in the
same context during this session. It is kept across scene loads in a
session-static field and never saved. If there is none yet: Camp →
CHARACTER, Descent → CHARACTER, Fight → RUN. System tabs are never
remembered, so a battle-speed change never makes the next fight open on
OPTIONS. Hotkeys and deep links override the rule.

**C8 Hotkeys** come from the tab table (C1), not scattered `GetKeyDown`
polls. One input handler owns them and forwards to C4.

**C9 Deep links.** `SystemMenuController.Open(tab, target)`, where `target`
is optional: a character id, a CHARACTER section, a glossary entry id, a
relic id. Every entry point uses it, including hotkeys, hub buildings,
`SheetPanel` and future "what's this?" links (AUDIT #227). `SheetPanel`
becomes a thin caller of it.

**C10 Lintel.** Realm title · context line (floor/room, or BETWEEN
DESCENTS) · gold · embers · a **mode pill** · close. The pill shows the C3
state for the whole menu: "AT CAMP", "IN A DESCENT", "IN A FIGHT — VIEW
ONLY". It replaces Party's own status pill.

**C11 One notice.** Refusals and confirmations in the menu go through one
notice line in a fixed place: text, optional timer, and the device-aware
prompt if any. It replaces the dossier's `_alreadyKnownRefusal` and
Party's toast inside the menu. This is the first user of AUDIT #259's
single notice component.

**C12 Prompt footer.** A fixed strip along the bottom of the panel lists
what the buttons do for the focused control, for example "A Equip · B Back
· LB/RB Character · LT/RT Tab". The keyboard shows key names; the pad shows
glyphs. It switches on `NavigationInputModule.LastInputWasPad`. Glyphs come
from a device → glyph table (Xbox first; adding PlayStation is one table).
No glyph art exists yet, so the first set is generated plates with the
button letter (C14 sizes).

**C13 One selection language.**
- Selected tab: bright label plus underline.
- Pad focus: the one `FocusMarker` arrow.
- Hover: a plate tint only, no scale. The remaining `.Hovers(...)` calls in
  menu screens go.
- A pending pick (item, book, character): gold ring on the source.

**C14 Type and space.**
- Every label in a menu pane uses a `Typography` role (`.Styled`). There
  are no unroled literal sizes.
- Nothing a player reads renders below 18px body height at 1080p (XAG
  101). Phase 1 fixes the per-font TMP minimum. Measured Chakra Petch is
  about 0.93 of font size, so the floor is about TMP 20.
- Decorative text is exempt and must be marked `AsDecor`.
- Every variable-length list pages or scrolls in one direction; no screen
  depends on a fixed row count fitting. This is what later makes a 125% /
  150% text option possible without a redesign.
- A build lint enforces the role rule and the floor for the menu screens.

### The tabs

**C15 CHARACTER.**
- **Portrait strip** across the top: the seated party in stage order (Rear,
  Middle, Front, left to right, matching the Party screen), then benched
  roster characters, set apart and dimmed. LB/RB and Q/E page through it.
  Each portrait carries an attention pip from `CharacterAttention` (C20).
- **Rail** on the left, always visible: portrait, name, level, XP, then the
  sections LOADOUT, ATTRIBUTES, SKILLS, REWARD TRACK, each with its pip.
- **Detail** on the right shows the selected section. Nothing ever covers
  the rail.
- Every section is readable in every context; C3 decides what can be
  changed.

**C16 LOADOUT (gear and spell books together).**
- **Slots:** the mannequin's gear slots from `EquipmentSlots.All`, plus a
  row of spell-book slots (`SpellBooks.MaxSpellSlots`). Both come from
  those sources, not from layout constants. A character whose pool refuses
  books (`SpellBooks.CanHold`, today Bjorn with Fury) shows one line, for
  example "Bjorn fights with Fury and carries no spell books", instead of
  the book row.
- **Pack**, beside the slots and never over them: gear, salves and the
  run's unassigned spell books in one grid, with filters All / Armour /
  Weapons / Salves / Books.
  - Books are shown in the pack view; they are not moved into
    `stockpiledItems`, so no save change.
  - Focusing a slot filters the pack to what that slot accepts; a book slot
    filters to Books.
  - Picking an entry and pressing a slot (or the reverse) equips or places
    it. The displaced gear or book returns to the pack.
  - One interaction model covers gear and books.
- **Compare**: while a pack entry is focused, the affected stats show
  `280 → 338 (+58)`, which fixes the reversed preview in AUDIT #243. For a
  book, the skill it teaches (cost, target, effect).
- **Empty pack**: a sentence ("Nothing carried. Gear from fights and shops
  lands here."), not a blank box.

**C17 ATTRIBUTES.** Six rows. Each row has the value, a − / + stepper beside
the number (AUDIT #243) and the effect line. Unspent points sit in the
header. Derived stats list below with a before → after preview while a
point is pending. Refund follows C3: at camp only, and in a descent the
row says "Refunds at camp".

**C18 SKILLS**, the one name for everything a character can do in a fight.
It matches the fight's SKILL verb.
- It lists `ContentDatabase.AvailableSkillsFor(character)`, grouped by
  source:
  - Innate (level)
  - From talents
  - From spell books (this descent)
  - From the reward track
- Each row shows the name, cost in the character's own pool name, cooldown,
  target and reach, and the description.
- This replaces the old Skills panel, which listed talent-granted skills
  only ("0 known" for Bjorn).
- Below it, read-only **TALENTS**: the character's kindled talents (name
  and description) and embers left. At camp it adds a link to the
  constellation (the Talents scene). Kindling stays in the hub.
- "Spell" survives only as "spell book", the item that teaches a skill for
  this descent. Content-facing renames are listed in C21.

**C19 REWARD TRACK.** The existing track design, hosted as a section instead
of a full-pane overlay. If Phase 1's space budget shows it cannot fit the
detail area at the type floor, it may take the full pane with the rail
collapsed to its identity header and an explicit Back. The spec decides
which, not the build. Collecting follows C3.

**C20 CharacterAttention** is a Domain model with no UI of its own. It
drives the strip pips, rail pips, and later hub and map badges. It
replaces `MapPendingBook`'s own count. A pip shows when:
- the character has unspent attribute points;
- the character has rewards waiting to collect;
- the character has an empty book slot, can hold books, and the run has
  unassigned books.

Empty gear slots never pip, because that would be noise.

**C21 Names.** One sweep across menu strings:

| Today | After |
|---|---|
| CHARACTER & INVENTORY | CHARACTER |
| MAIN MENU (tab) | EXIT |
| SPELLS (row), "Skills", "The Pack" | LOADOUT, SKILLS, pack inside LOADOUT |
| "Focus / turn" | "{signature} per turn", from content |
| "MANA" in fight detail for Fury users | the pool's own name |
| "{n} known" (a literal) | a `UiStrings` entry |
| Party subtitle "Some skills reach only from the rank they name" | "Melee skills can strike only the enemy front rank." (the real rule, `Reach`) |

The wider #236 glossary (HP/Health, Descent/Run, gold formats) stays its
own sweep.

**C22 PARTY.**
- Formation seats as today. Each seat adds the facts that decide
  placement: HP (current / max in a descent), physical and magical
  defence, role.
- Selecting a card previews the swap with those facts side by side (AUDIT
  #248).
- The roster section shows only when a character is unseated or a seat is
  closed. With everyone seated it hides instead of repeating the seats.
- Modes follow C3; the status pill moves to the lintel (C10).

**C23 RUN** (descent and fight only).
- **Relics**: icon, name, rarity and full description; paged; count in
  the header.
- **Run effects**: active event buffs (Prince's favor +N, special pools
  full this leg), the run's bonus damage percent, second lives left. None
  of these shows anywhere today.
  - Every `EventBuffs` kind needs a row definition, and a test fails on a
    kind without one.
- **Supplies**: consumables with count and effect, and gold.
- **This descent**: the Battle, Fold and Spoils figures from Run
  statistics, condensed. This tab replaces RUN STATISTICS.

**C24 Relic list, one presenter.** A Domain `RelicListModel` builds rows
from ids through one `ContentDatabase.GetRelic` lookup: name, rarity,
description, icon id, plus a row for an unknown id. A Core presenter paints
it with `Paging`. The Reckoning's RELICS tab, the RUN tab and the Relic
draft's new "held" strip (AUDIT #241) all use it. The three inline
`ContentDatabase.Relics.FirstOrDefault` lookups go.

**C25 GLOSSARY.**
- The existing catalog (seven categories, locked entries, paging), re-laid
  out for the menu pane: rail, list and detail.
- The standalone 1890×945 glossary modal and its own NavContext are
  deleted, so there is one glossary. That also resolves AUDIT #173 for the
  glossary.
- New category **Statuses**, from the definitions behind the fight's status
  box, because it is the one thing a player looks up mid-fight.
- LB/RB step categories.
- Deep link to an entry (C9).

**C26 OPTIONS.** The pane unchanged. It is shared with the main menu's
Settings modal, so any change is coordinated with that owner. Type floor
per C14.

**C27 EXIT.** Content unchanged: Return to title and Quit to desktop
(press twice), and Abandon descent (hold, in a descent only). Label per
C21.

## Growth rules

What must stay a one-place change, and the test that keeps it that way:

| When this grows | You change | Kept honest by |
|---|---|---|
| A new tab | one `SystemMenuTabs` entry plus its pane | the build refuses a tab without a pane; the bar lays out from measured widths |
| A 4th+ character or bench | nothing | the strip pages; roster visibility rule C22 |
| Gear or book slots | `EquipmentSlots` / `MaxSpellSlots` | slots are generated from them |
| Many relics (uncapped) | nothing | the relic presenter pages |
| A new event buff kind | a row definition | a test fails on a kind without a row |
| A new glossary category | `GlossaryCatalog` | existing catalog tests |
| A new pad family | one glyph table | the footer reads the table |
| Localisation / text size | strings only | measured bar, role-only type, one-direction scrolling (C14) |
| A new mutating action | one `MenuAction` row | the fight read-only test tries every action (Phase 0a) |

## Phases

Each phase is a green, committed checkpoint through
`tools/run_tests_parallel.ps1 -Changed`, plus `-BuildScenes` where a tree
changes. Phases touching the menu's shared files run one at a time.

**0a Fight read-only (C2, C3).** `MenuContext`, `MenuAction`,
`MenuPermissions`, and domain ops take the context.
- Every mutating handler goes through the table. This fixes the mid-fight
  spell-book change: `CharacterDossierController.cs:311-327` is wired above
  the `:364` lock guard, and `LearnSpell` has no context check.
- New PlayMode `MenuFightReadOnlyTests`: in the Fight scene, attempt every
  `MenuAction` through the UI; the save file's bytes are unchanged.
  EditMode tests pin the table.
- Route: implementer.

**0b Tab table cleanup (C1, part of C21).**
- Remove FloorMap: its enum and table entries, `DefaultFor` fallback,
  `Built` flag and placeholder. That is 11 lines in 5 files.
- Relabel to CHARACTER and EXIT.
- Delete the stale "three/five tabs" comments.
- Update the pins in `SystemMenuScreenTests` (positions), `SystemMenuTests`
  (counts at `:216`, `:238`, `:269`, `:278`), `SystemMenuPaneTests` and
  `SystemMenuLabelWidthTests`.
- Route: implementer.

**0c Hub exits (part of C4).** The corner Main Menu button opens the menu
on EXIT (AUDIT #219). Route: fixer.

**0d Drift.**
- Remove `.Hovers(...)` in the dossier (`CharacterDossierScreen.cs:616`,
  `:1098`).
- Casing.
- The empty-pack sentence.
- Route: fixer.

**1 Spec, type floor, baseline.** No game code. Owner sign-off ends the
phase.
1. Measure the body height of every `Typography` role and the default
   font at 1080p. Fix the TMP floor per font (C14).
2. Space budget: what fits in the 1600×804 pane at the floor for each
   pane in C15-C27.
3. The design spec (`docs/WORKFLOW.md:50-53`). A 1920×1080 mockup at real
   sizes of the frame, prompt footer, every CHARACTER section, RUN,
   GLOSSARY and PARTY, in every interactive state, including fight view-only
   and empty states.
4. A focus map per pane: entry control, every D-pad move, wrap, B at each
   pending state, and press counts for the test tasks.
5. Baseline test of today's menu with the owner plus 2–4 players, on
   keyboard and pad. Tasks and records per the Validation section.
6. The same tasks on the paper spec. Change the spec where people fail,
   before any build.

**2 Frame (C1, C4-C14).**
- Tab groups, context visibility, measured-only bar layout.
- Hotkey handler.
- The View button.
- Reopen rule.
- `Open(tab, target)`, with `SheetPanel` routed through it.
- Lintel mode pill.
- Notice line.
- Prompt footer and glyph table.
- Type roles and floor on the frame.
- The menu-screen type lint.
- Route: implementer. Escalation to senior only if the input ownership
  change breaks the dispatch order in `NavigationInputModule.Process`.

**3 RUN tab (C23, C24).**
- Extract `RelicListModel` and its presenter.
- Migrate the Reckoning's RELICS tab first. Its tests
  (`ReckoningTabTests`, `ReckoningGamepadNavigationTests`) must stay green
  with no pin changes beyond the row contents.
- Build RUN.
- Retire the RUN STATISTICS tab and move its geometry pins out of
  `SystemMenuPaneTests`.
- The Relic draft's held strip (AUDIT #241).
- Route: implementer.

**4 GLOSSARY tab (C25).**
- Pane layout.
- Hub building and hotkey open the tab.
- Delete the standalone modal.
- Statuses category.
- Rewrite `GlossaryTests`, `GlossaryScreenTests` and
  `GlossaryGamepadNavigationTests` for the pane.
- Route: implementer.

**5 CHARACTER (C15-C21).** The largest phase, in five commits:
- 5a Strip, rail and section shell. Move today's content into sections
  unchanged, and delete the overlays.
- 5b LOADOUT: book slots join the gear slots, unified pack with a Books
  filter, compare card. Removing a book to the pack is new behaviour and
  needs a `RunOrchestrator.UnlearnSpell` beside Learn/Replace, guarded by
  C3.
- 5c SKILLS with all sources plus read-only talents.
- 5d ATTRIBUTES inline.
- 5e `CharacterAttention` pips, which replace the map's book count.

Test impact:
- `DossierGamepadNavigationTests` (38) is rewritten against the Phase 1
  focus map, not patched.
- `DossierSpellSlotsTests`, `DossierSkillsPanelTests`, `DossierEquipTests`,
  `DossierRefundTests` and `CharacterDossierScreenTests` move to the new
  sections.

Route: implementer per commit. Senior only on a failed correction cycle.

**6 PARTY (C22).** Seat facts, swap preview, roster visibility rule,
subtitle fix. Update `PartyScreenTests` and `SystemMenuPartyTests`.
Route: implementer.

**7 Verify.**
- Rerun the Phase 1 tasks against the baseline (acceptance below).
- Captures at all four `UiFrames` aspects for every tab, in camp, descent
  and fight.
- A hardware pad pass (triggers and View are unverified on hardware, AUDIT
  #166).
- Full `tools/run_tests_parallel.ps1`.

## Validation

**Tasks** (Phase 1 baseline, Phase 1 paper, Phase 7 build), each on
keyboard and on pad:
1. Mid-fight: what does this relic do?
2. Mid-fight: what does this enemy status mean?
3. Mid-fight: how much health does Odette have?
4. Equip the better boots on Shawn.
5. Put a spell book on Odette.
6. Spend a stat point.
7. Put Odette in front.
8. Leave to the title screen.
9. Unprompted: "pause whenever you want to check something, and say why".

**Record** per task: completed, wrong turns (a tab or section opened that
did not hold the answer), presses or time, and where the player hesitated.

**Accept Phase 5 and 7** only if, against the baseline, on both inputs:
- no task that completes today fails;
- the build tasks (4–7) have fewer wrong turns;
- no build task needs more pad presses;
- nothing renders under the floor.

If the Phase 1 paper test misses these, the spec changes before any build.
Five sessions is a first diagnostic pass, not a guarantee.

## Risks

- **Parallel work.** Another session owns the main menu, which hosts the
  shared Options pane. Coordinate C26 and don't stage its files.
- **Test churn.** Phases 0b, 3, 4 and 5 rewrite pinned numbers and whole
  navigation suites. Pins are rewritten from the spec's numbers, never
  recomputed from the new code (CLAUDE.md gotcha 5).
- **Space at the floor.** Phase 1 may show a section cannot fit. The spec
  settles it (C19 is the known case), never the build.
- **Input ownership.** Hotkeys and View add inputs to
  `NavigationInputModule`. The dispatch order (Submit claim, base, system
  menu, Cancel, shoulders, triggers) is pinned by the gamepad suites. Add
  new inputs after the triggers and don't reorder.

## Assumed, say if wrong

- Collecting reward-track rewards and cycling titles count as changes, so
  they are blocked in a fight.
- Benched characters appear in the CHARACTER strip, after the seated party.
- The glossary building in the hub keeps its art, but its label changes
  from RELICS to GLOSSARY.
- Hotkeys C, I, P, R, G. None is bound anywhere today.

## Status

Planned 2026-09-29. Nothing built.
