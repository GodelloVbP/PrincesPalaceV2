# System menu rebuild

The in-game menu opened by Esc / Start over the Hub, Map (with Shop and
Event) and Fight. Research, captures and review rounds:
https://claude.ai/artifact/6CTjZvVkVxgRWawYHjbKdR. Layout numbers,
states and focus maps live in the design spec, `docs/SPEC_SYSTEM_MENU.md`
(Phase 1). This plan owns the rules, the spec owns the pixels.

Owner decisions (2026-09-29): one menu. Spells and skills are one concept,
and spell books are equipped beside the gear. Glossary is its own tab. In a
fight everything is viewable and nothing is changeable. The last tab is
EXIT. The owner asked for the whole plan to be built. Player tests
therefore run after the build, not before Phase 5 (see Validation).

Out of scope: a visual re-skin, a text-size option (C14 keeps it
possible), using consumables outside a fight, Start over the Reckoning and
Relic draft (AUDIT #191), and any save-format change.

## Behavioural contracts

### Model

**C1 Tabs.** Built from one table, `SystemMenuTabs`. Each entry carries a
key, full label, short label, group, visibility by context, hotkey and
pane. The build refuses a tab whose pane is missing, so a placeholder can
never reach a player.

| Tab | Group | Camp | Descent | Fight | Hotkey | Lands in |
|---|---|---|---|---|---|---|
| CHARACTER | game | ✓ | ✓ | ✓ | C | Phase 2 (content Phase 5) |
| PARTY | game | ✓ | ✓ | ✓ | P | Phase 2 |
| RUN | game | – | ✓ | ✓ | R | Phase 3, replacing RUN STATISTICS |
| GLOSSARY | game | ✓ | ✓ | ✓ | G | Phase 4 |
| OPTIONS | system | ✓ | ✓ | ✓ | – | as now |
| EXIT | system | ✓ | ✓ | ✓ | – | Phase 0b |

The game group sits left; the system group sits right, after a wider gap,
with a quieter label colour. I is a second key for CHARACTER with a target
(C4).

**Bar layout and overflow.** Label widths are measured with TMP, both at
build time and on each open. The authored `LabelWidth` column goes. If the
full labels do not fit at the tab role's minimum size, the bar uses every
tab's short label. If the short labels do not fit either, the build fails
for that language, which is an authoring error. A future text-size option
never scales the bar past fit. Measuring only detects overflow; the short
labels and the build failure are the policy.

**C2 Context.** Each menu instance has one `MenuContext`: `Camp` (Hub),
`Descent` (Map, Shop, Event) or `Fight`. It is a Domain enum.
`SystemMenuController` carries it as a serialized field set by
`ScreenRegistry.WireSystemMenu`. It replaces the menu's `inDescent` flag
and the `lockedForFight` flags on `CharacterDossierController` and
`PartyController`. Panes read `menu.Context` and keep no flag of their
own.

**C3 Permissions: a fight is read-only.** Every state change the menu can
make is a `MenuAction`. It is allowed or refused by one Domain table,
`MenuPermissions.Allows(action, context)`, which also returns the refusal
reason.

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

Enforcement happens in two places, reading the same table:

- **Controls.** A refused control keeps its normal look and stays
  focusable and inspectable. Pressing it shows the notice (C11) with the
  table's reason.
- **Operations.** The domain operations take the context and refuse
  through the table too, so every caller is guarded, not just the menu.
  - Core: `RunOrchestrator.LearnSpell` / `ReplaceSpell` / the new
    `UnlearnSpell`, `EquipmentOps.Equip` / `Unequip`, `TalentOps.Invest` /
    `Refund`, and the reward-track claim.
  - Domain: `PartyFormation` (via its existing mode).
  - The balance bot passes `Descent`.

**C4 Data sources.** Values in the menu come from exactly one of three
places.

| Kind | Examples | Source in Camp / Descent | Source in Fight |
|---|---|---|---|
| Permanent | level, XP, attributes, talents, gear worn, titles, reward track | `SaveData` character | same |
| Run | relics, event buffs, bonus damage, books, pack, gold, carried HP between fights | `RunManager.Run` | same, **except HP** |
| Fight | current HP, pool, signature, statuses, temporary stat modifiers, cooldowns | – | `IFightMenuSource` |

- **The fight source.** `IFightMenuSource` is implemented by the Fight
  scene, which registers it with the menu at wiring. In a fight, the
  current HP, pool and effective stats come from it, never from
  `RunSnapshot.currentHealth`. That snapshot is written only when the fight
  ends (`RunEncounter.WriteBackHealth`, called from the fight-end rule at
  `RunOrchestrator.cs:530`), so mid-fight it is stale.
- **As built (5e).** `IFightMenuSource` is a Core interface over the Domain
  shape `FightMemberFacts` (health, pool, signature, temporary stat
  modifiers). `FightController.Bind` hands itself to
  `SystemMenuController.FightSource`; the controller records what it painted
  where it paints (`RefreshPcPlate`, `PaintVitals`), and `FightModifiers` lists
  the speed buffs, form grants and armour shred the status badges show.
- **The displayed moment.** The menu shows what the fight HUD is showing
  when the menu opens, not the simulation's lead. The session records beats
  before the beat player paints them. Reading the session could therefore
  reveal a hit or a death the player hasn't seen yet. The source reads the
  HUD's painted values.
- **Stat columns in a fight** show base → effective when a temporary
  modifier applies, for example "Speed 10 → 13 (Stampede)".

### Input and state

**C5 Pending states.** There are three, all in panes. They are the only
"levels" Cancel steps back through.

| Pending | Created by | Lives in |
|---|---|---|
| `SlotArmed(slot)` | A on an empty gear or book slot: focus moves into the pack, filtered to that slot | CHARACTER › LOADOUT |
| `BookPicked(id)` | A on a pack book while every book slot is full: focus moves to the book slots to choose which to replace | CHARACTER › LOADOUT |
| `Carry(character)` | picking up a seat or roster card | PARTY |

Attribute points have no pending state. "+" and "−" commit on press, as
today. While "+" or "−" is focused, the before → after preview shows.

**C6 Transitions.** Read top to bottom. The first row whose condition holds
wins. "Cancel pending" means the pending state is dropped with no change to
game state and focus returns to where it was created.

| Event | Condition | Outcome |
|---|---|---|
| B / Esc-as-Cancel | a pending exists | cancel pending |
| B / Esc-as-Cancel | nothing pending | close menu |
| Start / Esc-as-SystemMenu | a pending exists | cancel pending (menu stays open) |
| Start / Esc-as-SystemMenu | nothing pending | close menu |
| Hotkey or deep link with a **target** (I → LOADOUT pack; hub building; glossary entry) | anywhere | cancel pending, then open or switch to the tab and go to the target. Never closes, except a hotkey whose target is already showing (I with the pack up): the keyboard needs a key that closes after I. |
| Plain tab hotkey (C, P, R, G) | menu closed | open on that tab (C7 section rule) |
| Plain tab hotkey | menu open, another tab showing | cancel pending, switch tab |
| Plain tab hotkey | menu open, that tab showing | cancel pending, close menu |
| LT / RT | – | cancel pending, step tab |
| LB / RB (Q / E) | – | cancel pending, page within the tab (C8) |
| Rail section change, click elsewhere in the tab | a pending exists | cancel pending, then act |
| Any close (X, hotkey, Esc) | – | cancel pending first; a pending never commits on close |
| A on a refused control (C3) | – | notice with the reason; no pending created |

Esc drives both SystemMenu and Cancel. The dispatcher already spends an Esc
press on SystemMenu only (`NavigationInputModule.cs:260-266`). The two Esc
rows therefore resolve through the SystemMenu path with the same outcomes.

**C7 Reopen rule.** The menu reopens on the last game-group tab and
CHARACTER section used in the same context, this session. That memory is
session-static, survives scene loads, and is never saved. With no memory:
Camp and Descent → CHARACTER › LOADOUT; Fight → RUN. System tabs are never
remembered. Hotkeys and deep links override the rule, and they update the
memory.

**C8 Switching.** LT / RT step tabs, wrapping over visible tabs, with
focus on the pane's entry control. LB / RB (Q / E) page within a tab:

| Tab | What pages |
|---|---|
| CHARACTER | characters |
| GLOSSARY | categories |
| RUN | relic pages |
| PARTY, OPTIONS, EXIT | nothing, and no prompt is shown |

The mouse clicks anything.

**C9 Entry points.** `SystemMenuController.Open(tab, target)` is the only
way in; `target` is optional (a character id, a CHARACTER section, a
glossary entry id, a relic id).

- **Callers:**
  - root Esc/Start handlers
  - the hotkey owner
  - the pad View button (RUN in a descent or fight, CHARACTER at camp)
  - hub buildings: CHARACTER SHEET → CHARACTER, the glossary building →
    GLOSSARY, the corner Main Menu button → EXIT (AUDIT #219)
  - `SheetPanel`, which becomes a thin caller
- **One hotkey owner.** Hotkeys are read by one handler that owns every
  menu hotkey. The three existing C/I owners are removed:
  - `HubController.cs:381-388`
  - `MapController.cs:213-214`
  - `FightController.Input.cs:1564-1565`
- **When hotkeys fire.** Only when a scene root or the menu is top of the
  nav stack, which is the same rule as Start today.

**C10 Lintel.** Realm title, context line, gold, embers, a **mode pill**
and close. The pill states C3 for the whole menu: "AT CAMP", "IN A
DESCENT", "IN A FIGHT — VIEW ONLY". Party's own status pill goes.

**C11 One notice.** Refusals and confirmations inside the menu go through
one notice line in a fixed place in the frame: text, an optional timer, and
the device-aware prompt. It replaces the dossier's `_alreadyKnownRefusal`
and the in-menu `PartyToast`. It is the first user of AUDIT #259's single
notice component.

**C12 Prompt footer.** A fixed strip at the bottom of the panel lists what
the buttons do for the focused control and the current pending state.

- The keyboard shows key names; the pad shows glyphs. It switches on
  `NavigationInputModule.LastInputWasPad`.
- Glyphs come from a device → glyph table. Xbox comes first, and
  PlayStation is one more table. The first glyph set is generated plates
  with the button letter; no glyph art exists yet.

**C13 One selection language.**
- Selected tab: bright label plus underline.
- Pad focus: the one `FocusMarker` arrow.
- Hover: a plate tint, no scale. The remaining `.Hovers(...)` calls in menu
  screens go.
- A pending source: a gold ring.

**C14 Type and space.**
- Every label in a menu screen takes a `Typography` role.
- Nothing a player reads renders below 18px body height at 1080p (XAG
  101). Phase 1 fixes the per-font TMP floor. Measured Chakra Petch is
  about 0.93 of font size, so the working floor is TMP 20.
- The lint checks **every** label and button in a migrated menu screen,
  whatever its `AsDecor`. `AsDecor` is a raycast and audit flag, and
  readable headings and values carry it today.
- Text that carries no information (an ornament numeral, a watermark) opts
  out with its own `.AllowSmallText("reason")`, following the
  `AllowOverlap` pattern. It gets a test that the text really is
  non-essential.
- Every variable-length list pages or scrolls in one direction. No screen
  depends on a fixed row count fitting.

### The tabs

**C15 CHARACTER.**
- **Portrait strip** on top: the seated party in stage order (Rear,
  Middle, Front, left to right), then benched roster characters, set apart
  and dimmed. LB/RB and Q/E page through it. Each portrait carries an
  attention pip (C20).
- **Rail** on the left, always visible: portrait, name, level and XP, then
  the sections LOADOUT, ATTRIBUTES, SKILLS and REWARD TRACK, each with its
  pip. The detail area shows the selected section, and nothing covers the
  rail.
- Every section is readable in every context; C3 decides what changes.

**C16 LOADOUT.**
- **Slots:** the mannequin's gear slots from `EquipmentSlots.All`, plus a
  row of book slots (`SpellBooks.MaxSpellSlots`). Both are generated from
  those sources. A character whose pool refuses books (`SpellBooks.CanHold`)
  shows one line instead of the book row, for example "Bjorn fights with
  Fury and carries no spell books".
- **Pack**, beside the slots: gear, salves and the run's unassigned books
  in one grid, with filters All / Armour / Weapons / Salves / Books.
  - Books are shown in the pack view only; they stay in
    `unassignedSpellBooks`, so there is no save change.
- **Flows** (C5, C6):

  | Press | Result |
  |---|---|
  | A on a pack gear entry | equip to its slot (`EquipmentOps`, as today) |
  | A on a pack book | place it in the lowest free book slot; all full → `BookPicked` |
  | A on a worn slot | unequip, or remove the book to the pack (`UnlearnSpell`) |
  | A on an empty slot | `SlotArmed` |

- **Compare:** while a pack entry is focused, affected stats read
  `280 → 338 (+58)` (fixes AUDIT #243's reversed preview). A book shows the
  skill it teaches.
- **Empty pack:** a sentence, not a blank box.

**C17 ATTRIBUTES.** Six rows. Each row has its value, a − / + stepper beside
the number, and the effect line. Unspent points sit in the header. The
derived stats show below with before → after while a stepper is focused.
Refund follows C3; in a descent the row says "Refunds at camp".

**C18 SKILLS**, the one name for everything a character can do in a fight.
It matches the fight's SKILL verb.
- **List:** `ContentDatabase.AvailableSkillsFor(character)`, grouped by
  source: innate (level), talents, spell books (this descent) and reward
  track.
- **Each row:** name, cost in the character's own pool name, cooldown,
  target and reach, and description.
- **Talents:** below the list, read-only, the kindled talents and the
  embers left. At camp only, a link to the constellation.
- **"Spell"** survives only in "spell book".

**C19 REWARD TRACK.** The existing track, hosted inside the detail area as a
section. It is re-laid out for that width in the spec; there is no
full-pane mode. Collecting follows C3.

**C20 CharacterAttention.** A Domain model fed plain values by Core. It
drives the strip and rail pips, and replaces `MapPendingBook`'s own count.
A pip shows when:
- the character has unspent attribute points, or
- the character has rewards waiting to collect, or
- the character has an empty book slot, can hold books, and the run has
  unassigned books.

Empty gear slots never pip. Built as `CharacterAttention` (Domain) over
`CharacterAttentionFeed` (Core). The map's hint reads the same feed and names
what waits, only the reasons that hold, in the pips' order: "Points to spend ·
Rewards waiting · Books to place — press C" (`AttentionHint`; the key is
`PromptGlyphs`' `Character` button, so a pad says Start and the line follows
the device in hand). It replaced `ATTENTION NEEDED: {n}`, which had lost the
reason.

**C21 Names.**

| Today | After |
|---|---|
| CHARACTER & INVENTORY | CHARACTER |
| MAIN MENU (tab) | EXIT |
| SPELLS / Skills / The Pack rows | LOADOUT, SKILLS; the pack sits inside LOADOUT |
| "Focus / turn" | "{signature} per turn", from content |
| "MANA" in the fight detail for a Fury user | the pool's own name |
| "{n} known" (a literal) | `UiStrings` |
| Party subtitle "Some skills reach only from the rank they name" | "The front rank takes the enemies' blows. Melee skills can strike only the enemy front rank." (the real `Reach` rule) |
| Hub building RELICS (opens the glossary) | GLOSSARY |

**C22 PARTY.** The formation seats, with the facts that decide placement:
- HP from C4: carried between fights, live in a fight;
- physical and magical defence;
- role.

Selecting a card previews the swap with those facts side by side (AUDIT
#248). The roster shows only when a character is unseated or a seat is
closed (`PartyFormation.RosterVisible`). Modes follow C3. The status pill moved
to the lintel. A character the fight is not showing (benched, or carried at
zero and never fielded) reads the run's carried health, which is not stale for
them.

**C23 RUN** (Descent and Fight):
- **Relics:** icon, name, rarity and description; paged; count in the
  header.
- **Run effects:** active event buffs, bonus damage percent and second
  lives left. Each `EventBuffs` kind has a row definition, and a test fails
  on a kind without one.
- **Supplies:** consumables with counts and effects, and gold.
- **This descent:** the Battle / Fold / Spoils figures, condensed.

**C24 One relic presenter.** Assembly direction is Domain ← Core; Domain
references nothing. So:
- Domain `RelicListModel` takes plain `RelicRow` records (id, name, rarity
  text, description, icon id, known) and owns ordering and paging.
- Core `RelicRows.From(ids)` resolves them through one
  `ContentDatabase.GetRelic` lookup, including the unknown-id row.
- A Core presenter paints them.

The Reckoning's RELICS tab, RUN, and the Relic draft's new held strip
(AUDIT #241) all use it. The three inline `ContentDatabase.Relics.FirstOrDefault`
lookups go.

**C25 GLOSSARY.**
- The existing catalog (seven categories, locked entries, paging), re-laid
  out for the menu pane.
- The standalone 1890×945 glossary modal and its NavContext are deleted.
- New category **Statuses**, from the definitions behind the fight's
  status box.
- LB/RB step categories. Deep link to an entry (C9).

**C26 OPTIONS.** The layout is unchanged; every label reaches the floor. It
is shared with the main menu's Settings modal, so the same tree, resized in
place, serves both (Phase 6).

**C27 EXIT.** Content unchanged. Label per C21.

## Growth rules

| When this grows | You change | Kept honest by |
|---|---|---|
| A new tab | one `SystemMenuTabs` entry plus its pane | the build refuses a paneless tab; the overflow policy (C1) |
| A 4th+ character or a bench | nothing | the strip pages; roster rule (C22) |
| Gear or book slots | `EquipmentSlots` / `MaxSpellSlots` | slots are generated |
| Many relics | nothing | the presenter pages |
| A new event buff kind | a row definition | a test fails on a kind without one |
| A new glossary category | `GlossaryCatalog` | the catalog tests |
| A new pad family | one glyph table | the footer reads it |
| Localisation / text size | strings, plus short labels | C1 overflow policy, role-only type, one-direction scrolling |
| A new state change | one `MenuAction` row | the fight read-only tests (Phase 0a) |

## Phases

Each phase is a green, committed checkpoint through
`tools/run_tests_parallel.ps1 -Changed`, with `-BuildScenes` whenever a
tree or a `[SerializeField]` changes. Phases run one at a time.

**0a Fight read-only (C2, C3).**
- Add `MenuContext`, `MenuAction` and `MenuPermissions`.
- Serialized context replaces `inDescent` / `lockedForFight`. This changes
  `ScreenRegistry.WireSystemMenu`, so regenerate the scenes in this phase.
- Every mutating handler and domain op goes through the table.
- New `UnlearnSpell`, guarded but not yet wired to UI.
- **Tests:**
  - EditMode pins the whole table.
  - PlayMode `MenuFightReadOnlyTests`:
    1. In the Fight scene, invoke every refused action through its UI
       control and through its domain op. Assert unchanged in-memory state
       (character equipment, attributes and titles, the run's books and
       learned spells, pack, formation, claimed track level, and the
       FightSession's actors) and unchanged save bytes.
    2. Separately, in a sandboxed save, options and exits stay allowed in
       a fight. Assert the option changes, and that an exit reaches its
       `EndRun` path.

  This closes the bug that lets spell books change mid-fight
  (`CharacterDossierController.cs:311-327`, wired above `:364`).

**0b Tabs cleanup.**
- Remove FloorMap: the enum, table entries, `DefaultFor`, the `Built` flag
  and the placeholder.
- Relabel to CHARACTER and EXIT, add short labels, and build the overflow
  policy (C1).
- Delete the stale "three/five tabs" comments.
- **Tests re-pinned:**
  - `SystemMenuScreenTests` (positions, from the new measured widths)
  - `SystemMenuTests` (`BetweenRunsTheBarHasFourTabs`,
    `InADescentTheBarGainsTheTwoRunTabs`, `TheHubNeverShowsTheDescentTabs`)
  - `SystemMenuPaneTests`
  - `SystemMenuLabelWidthTests`
- **Intermediate bar:** CHARACTER, PARTY, RUN STATISTICS (descent/fight),
  OPTIONS, EXIT.

**0c Hub exit button → `Open(EXIT)`** (AUDIT #219). Uses today's
`SystemMenuController.Select`, since `Open(tab, target)` lands in Phase 2.

**0d Drift** is folded into Phase 5. The dossier's hover scales, its row
casing and its empty pack are all rebuilt there, so fixing them first
would be thrown away.

**1 Design spec.** No game code. The output is `docs/SPEC_SYSTEM_MENU.md`:
1. The type floor per font, measured.
2. The space budget for the 1600×804 pane minus the footer.
3. The layout of every pane at 1920×1080, with node sizes.
4. Every interactive state, including view-only and empty states.
5. A focus map per pane: the entry control, every D-pad move, wrap,
   Cancel at each pending state, and press counts for the validation tasks
   from their fixed starting conditions.

The owner reviews the spec when they return; building proceeds on it,
since the owner asked for the whole build.

**2 Frame (C1, C6-C14).**
- Groups, visibility, measured bar and overflow policy.
- `Open(tab, target)`, and `SheetPanel` routed through it.
- The one hotkey owner, with the three old C/I owners removed.
- The View button, the reopen rule, the lintel pill, the notice line, and
  the prompt footer with its glyph table.
- Type roles on the frame.
- The type lint, **scoped to a migrated-screens list** that starts with the
  frame and grows each phase.
- **Intermediate state:** hotkeys C, I, P; tabs as after 0b. Targets exist
  only for panes that exist.
- Escalate to senior (`cross-layer`) only if the input-ownership change
  breaks `NavigationInputModule.Process` dispatch order.

**3 RUN (C23, C24).**
- `RelicListModel`, `RelicRows` and the presenter.
- Migrate the Reckoning RELICS tab first. `ReckoningTabTests` and
  `ReckoningGamepadNavigationTests` stay green with only row-content pins
  changed.
- Build RUN, and retire RUN STATISTICS (its pins move).
- Add the Relic draft held strip (AUDIT #241).
- Hotkey R and the relic deep link. RUN joins the lint list.

**4 GLOSSARY (C25).**
- The pane layout; the hub building and G open it.
- Delete the standalone modal.
- Add the Statuses category.
- Rewrite `GlossaryTests`, `GlossaryScreenTests` and
  `GlossaryGamepadNavigationTests`. GLOSSARY joins the lint list.

**5 CHARACTER (C4, C15-C21).** Five commits:
- 5a Strip, rail and section shell; today's content moves into sections;
  overlays deleted.
- 5b LOADOUT: unified slots and pack, `SlotArmed` / `BookPicked`, compare,
  `UnlearnSpell` wired.
- 5c SKILLS with all sources, plus read-only talents.
- 5d ATTRIBUTES inline.
- 5e `CharacterAttention` pips, plus `IFightMenuSource` for fight values.

`DossierGamepadNavigationTests` is rewritten against the spec's focus map,
not patched. `DossierSpellSlotsTests`, `DossierSkillsPanelTests`,
`DossierEquipTests`, `DossierRefundTests` and `CharacterDossierScreenTests`
move to the sections. CHARACTER joins the lint list.

**6 PARTY, OPTIONS, EXIT (C22, C26, C27).**
- PARTY: seat facts from C4, swap preview, roster rule, subtitle, pill
  removed.
- OPTIONS and EXIT: type floor only. Exit's separator gaps close from 56 to
  40 so the taller notes still fit the stack.
- The lint walks the whole menu tree; the scope list is deleted, and only the
  rows borrowed from other screens keep a name prefix.

**7 Verify.**
- Captures at all four `UiFrames` aspects, for every tab, in camp, descent
  and fight.
- An audit of the build against the spec, section by section.
- The full `tools/run_tests_parallel.ps1`.
- Owner-run: the hardware pad pass (AUDIT #166) and the player tasks below.

## Validation (owner-run, after the build)

**Fixed starting conditions** for every task and both inputs:
- the same save fixture (Shawn, Odette and Bjorn seated; two relics; a
  known pack; one unassigned book);
- menu memory cleared, so the reopen rule starts from its default;
- the stated scene: a Fight at turn 1, or the Map.

**Tasks:**
1. Mid-fight: what does this relic do?
2. Mid-fight: what does this enemy status mean?
3. Mid-fight: how much health does Odette have?
4. Equip the better boots on Shawn.
5. Put the spell book on Odette.
6. Spend a stat point.
7. Put Odette in front.
8. Leave to the title screen.
9. Unprompted: "pause whenever you want to check something, and say why".

**Record** per task: completed, wrong turns, presses or time, and
hesitations.

**Accept** when:
- every task completes on both inputs, including tasks 1–3, which are
  impossible or partial today;
- any task at zero wrong turns stays at zero;
- a task with problems in the baseline at least halves its wrong turns;
- no build task needs more pad presses than the spec's focus map counts;
- nothing renders under the floor.

If the build fails these, the spec changes first, then the build. The old
dossier is recoverable from git.

## Risks

- **Parallel sessions.** Another session owns the main menu, which shares
  the Options pane. Stage explicit paths only; don't reformat
  `OptionsScreen`.
- **Test churn.** Pins are rewritten from the spec's numbers, never
  recomputed from new code (CLAUDE.md gotcha 5).
- **Input dispatch.** Hotkeys and View are added after the triggers in
  `NavigationInputModule.Process`. Never reorder the existing dispatch.

## Assumed, say if wrong

- Reward collection and title cycling count as changes, so they are
  blocked in a fight.
- Benched characters appear in the CHARACTER strip, after the seated
  party.
- The hub's RELICS building is relabelled GLOSSARY.
- Hotkeys C, I, P, R, G. C and I move from their three current owners to
  the one owner.

## Status

Revised 2026-09-30. Phases 0a-6 landed (see the git log); Phase 7 is built and
audited below. What is left is the owner's: the captures to look at
(`tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests`, files
`SystemMenu_aspect_<tab>_<w>x<h>.png` for loadout, skills, party, run,
glossary, options and exit at the four `UiFrames` aspects, and
`fight_character` for the Fight context), the hardware pad pass (AUDIT #166)
and the player tasks under Validation.

## Audit (Phase 7)

The build against the spec and the contracts, one line each. "Matches" means
read against the code and pinned by the named test; "fixed" and "amended" are
this phase's own changes, with the reason.

**Contracts**

- **C1 Tabs**: matches. `SystemMenuTabs.All` carries key, both labels, group,
  hotkey, run-only and the pager label; every entry builds a pane; the bar
  measures on each open and falls back to short labels
  (`SystemMenuLabelWidthTests`, `SystemMenuBarGroupsTests`, `SystemMenuPaneTests`).
- **C2 Context**: matches. `MenuContext` is a Domain enum set by
  `ScreenRegistry.WireSystemMenu`; no `inDescent` or `lockedForFight` remains.
- **C3 Permissions**: matches. One table, `MenuPermissions`; controls and the
  Core operations both read it, and the bot passes `Descent`
  (`MenuPermissionsTests`, `MenuFightReadOnlyTests`).
- **C4 Data sources**: matches. The Fight scene registers `IFightMenuSource`;
  the rail, PARTY and the compare column read the HUD's painted values, never
  the snapshot (`FightMenuSourceTests`, `PartyMemberFactsTests`).
- **C5 Pending states**: matches. `SlotArmed`, `BookPicked` and `Carry` live in
  the panes; attribute points commit on press (`DossierLoadoutTests`,
  `MenuHotkeyTests`).
- **C6 Transitions**: matches. Cancel steps back a level before it closes, and
  every close, tab change and hotkey drops the pending first
  (`StartOpensSystemMenuTests`, `MenuHotkeyRuleTests`).
- **C7 Reopen rule**: matches. Per context, session-static, system tabs never
  remembered, hotkeys update it (`MenuMemoryTests`, `MenuHotkeyTests`).
- **C8 Switching**: matches. LT/RT step tabs and wrap; LB/RB page characters,
  categories and relics, and PARTY, OPTIONS and EXIT show no pager prompt
  (`MenuPromptsTests`).
- **C9 Entry points**: matches. `Open(tab, target)` is the way in; the three
  old C/I owners are gone; the hub's glossary building and corner button call
  it. AUDIT #219 keeps its other two callers (map abandon, main-menu exit), as
  its entry says.
- **C10 Lintel**: matches. The mode pill reads AT CAMP, IN A DESCENT and IN A
  FIGHT — VIEW ONLY (`MenuPromptsTests.ThePillNamesEachContext`).
- **C11 One notice**: matches. Refusals and confirmations go to the footer's
  notice; the dossier's refusal field and the party toast are gone.
- **C12 Prompt footer**: matches. Keyboard names or glyph plates by
  `LastInputWasPad`; the pair set follows the focus and the pending state
  (`MenuFooterTests`, `MenuPromptsTests`).
- **C13 One selection language**: fixed. The arrow (the one `FocusMarker`)
  drew on top of the Party subtitle and ribbon: a word was dropped as a
  neighbour whenever a raycast-blocking backdrop sat behind it. Draw order now
  decides, so an unrelated hit hides a word only if it is drawn after it
  (`PartyGamepadNavigationTests.TheFocusMarkerStandsOnNoWords_OnAnySeat`).
- **C14 Type and space**: matches. The lint walks every label and button in the
  menu tree, decor included, with the floors the spec states
  (`MenuTypeLintTests`); every list pages or scrolls one way.
- **C15 CHARACTER**: matches. Strip in stage order with the bench after it, a
  rail that nothing covers, pips from one model (`DossierRailTests`).
- **C16 LOADOUT**: matches. Slots and books generated from
  `EquipmentSlots.All` and `SpellBooks.MaxSpellSlots`, one pack with filters,
  compare as `280 → 338 (+58)` (`DossierLoadoutTests`).
- **C17 ATTRIBUTES**: matches. Steppers beside the value, unspent in the
  header, `before → after` while a stepper is focused, "Refunds at camp" in a
  descent (`DossierAttributesTests`, `DossierDerivedRowsTests`).
- **C18 SKILLS**: fixed. Descriptions were cut to one line and an ellipsis (the
  box was two nominal lines, 54, and a Body 20 line with its spacing is about
  27.6). A row now grows to the lines its words need, measured on the label; no
  text in the list may end in an ellipsis, and a test reads every authored
  skill and talent on all three characters
  (`DossierRailTests.EveryDescriptionInTheListIsShownWholeNeverCutToAnEllipsis`).
  Spec 3.3 amended: the row is 100 tall at two lines, 29 a line after that.
- **C19 REWARD TRACK**: matches. Hosted in the detail at 1196 × 660 with no
  frame of its own; collecting follows C3 (`RewardTrackScreenTests`).
- **C20 CharacterAttention**: matches. One Domain model over one Core feed
  drives the pips and the map hint, which names only reasons that hold
  (`CharacterAttentionTests`, `DossierAttentionTests`, `MapAttentionHintTests`).
- **C21 Names**: matches. CHARACTER, EXIT, LOADOUT and SKILLS, "{signature} per
  turn", the real Reach subtitle, the glossary's Skills category and the hub's
  GLOSSARY building.
- **C22 PARTY**: fixed. Seat facts, swap preview, roster rule and lintel pill
  matched. Three defects from 1080p captures: the carry badge was translucent
  over the sprite (now opaque, sprite dimmed while it shows; spec 6 amended),
  the ribbon used ASCII "->" (now "→", and a test says the font has it), and the
  pane opened on the front seat instead of the rear (now `INavPaneEntry`; that
  is what made task 7 two presses over). The HP bar's salmon is `HpBright`, the
  same token as the fight's party plates (pinned equal in `PartyScreenTests`);
  a full bar reading as damage is the game's own health colour, not a Party
  defect, and is the owner's call.
- **C23 RUN**: matches. Relics paged, run effects with a row definition per
  `EventBuffs` kind (a test fails on a missing one), supplies, and the
  descent's figures (`RunPaneScreenTests`, `SystemMenuRunTests`,
  `RunEffectRowTests`).
- **C24 One relic presenter**: matches. `RelicListModel` (Domain),
  `RelicRows.From` and `RelicListPresenter` (Core) serve the Reckoning, RUN and
  the draft's held strip; the one remaining `Relics.FirstOrDefault` builds
  fight effects and is not a presenter.
- **C25 GLOSSARY**: matches. Eight categories with Statuses, locked entries,
  LB/RB categories, deep link; the standalone modal and its context are gone
  (`GlossaryGamepadNavigationTests`).
- **C26 OPTIONS**: matches. Same tree as the main menu's Settings, resized in
  place, at the floor (`OptionsPaneTests`).
- **C27 EXIT**: matches. Content unchanged, labels per C21, separator gaps 40
  (`SystemMenuExitsTests`).

**Spec sections**

- **1 Type floor**: matches (`MenuTypeLintTests.TheFloorsAreThoseTheSpecStates`).
- **2 Frame**: matches. Lintel 52, panel 1600 × 900, bar 96, footer 48 with a
  4 px gap, pill 36, tab gap 48 and group gap at least 96 (`SystemMenuLayout`,
  `MenuFooterLayout`).
- **3 CHARACTER, 3.1-3.4**: matches, and 3.3 amended (above). Strip chips
  240 × 64, rail 300, detail 1196 × 660, columns 404 / 408 / 352.
- **4 RUN, 5 GLOSSARY**: matches, at 1520 × 756 (`RunLayout`, `GlossaryLayout`).
- **6 PARTY**: amended: opaque badge and dimmed sprite, "→" ribbon, the arrow
  standing below the seat, and the health colour stated. The pane is 1488 × 716
  as the section already says.
- **7 OPTIONS and EXIT**: matches.
- **8 States and prompts**: matches (`MenuPromptsTests`, `MenuFooterTests`).
- **9 Focus maps**: matches, with one fix: PARTY's entry is now the rear seat
  (`PartyController.NavEntry`).
- **10 Press counts**: matches. Journey tests hold every pad count from the
  fixed start: 1 (plus one per relic down), 4 + n, 2, 9, 8, 4, 6 and 4
  (`JourneyCharacterLoadoutTests`, `JourneySystemMenuPressCountTests`).

**Captures**: `SystemMenuCaptureTests.CaptureEveryTabAtEveryAspect` and
`CaptureTheFightsCharacterViewAtEveryAspect` write one PNG per tab per aspect;
their walks also run headless (`TheAspectWalkReachesEveryTabInADescent`,
`TheFightsCharacterViewOpensViewOnly`), so only the pictures wait for the
owner's screenshot pass.
