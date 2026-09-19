# Plan: Odette's Prismatic Orb, and the element-choice mechanic

**Status:** implementation brief, 2026-09-08. Branch `prismatic-orb`.
**Owner's ask, verbatim in spirit:** a low-to-medium damage, single-target
skill for Odette (`owl`). After picking it she chooses the element (Earth,
Water, Fire or Wind), then the target. She gets it at level 1 and loses her
three `placeholder_caster_*` skills. Afterwards, authoring a new spell should
be easy, and every place where it was not is to be recorded.

This is a **skill**, not a global spell, under `docs/SPELL_DESIGN_STANDARD.md`'s
own distinction: it is Odette's, unlocked by her level, never a book. It reuses
spell-presentation technology, which the standard explicitly allows.

## Assumptions made without the owner (say the word and any of them flips)

1. **Numbers.** `manaCost 8`, one packet of `16` on the x10 scale. The
   placeholders it replaces were Arcane 18 @ 8 and Fire 20 @ 9; Lightning Bolt
   (a tier-3 book) is 20 @ 11. Fixed packets ride the spell-tier multiplier
   (1.5 at level 1) and INT scaling, same as the placeholders did, so 16 sits
   just under them: "low-to-medium" with the upside that the player can
   always pick the weakness.
2. **The mechanic is general, not Odette-only.** Any skill may author an
   `elements` list. Nothing in the fight, the bot or the HUD names the orb.
3. **Element choice requires fixed packets.** A skill with `elements` must
   also author `damageInstances`; the chosen element retypes every packet.
   Attack-scaled skills (Power/FlatAmount) keep riding the caster's
   `attackType` and may not author `elements` -- the resolver refuses it.
   This keeps the mechanic to one retype and no new state on the pipeline.
4. **No VFX yet.** `Art/Sheets/Spells/prismatic_bolt/` holds a visual
   development sheet (four projectiles, four impacts), not a frame sheet the
   slicer can cut. The orb ships with no `vfx` block, as the placeholders
   did; the content shape allows a per-element `vfx` so the art lands
   without another chain change.
5. **Only Fire currently matters.** No enemy in `enemies.json` is weak or
   resistant to Earth, Water or Wind. Three of the four choices are neutral
   against every monster in the game today. That is an owner's call on enemy
   authoring, recorded in the report, not fixed here.

## Behavioural contracts

### C1. Content

- `skills.json` gains `prismatic_orb`: `characterId: owl`, `unlockLevel: 1`,
  `effect: DamageSingle`, `manaCost: 8`, `damageInstances: [{type: Earth,
  amount: 16}]`, `elements: [Earth, Water, Fire, Wind]` (see shape below).
  Display name "Prismatic Orb". Description in the house voice, one or two
  sentences, no "placeholder".
- `placeholder_caster_bolt`, `placeholder_caster_firebolt`,
  `placeholder_caster_mend` are deleted from `skills.json`. The regenerated
  `Resources/Content/` no longer contains their assets; the stamp is
  regenerated and committed with the change.
- `RawSkillEntry.elements`: an array of `{ "type": "<DamageType>", "vfx":
  {SpellPresentation, optional} }`. Resolver rules, each refused with a
  message naming the skill id and the field:
  - every `type` parses as a `DamageType` (same parser `damageInstances`
    uses, so 'Frost' is accepted for Ice);
  - no duplicate type;
  - one element alone is refused (a choice of one is not a choice; author a
    typed packet instead);
  - `elements` non-empty requires `damageInstances` non-empty;
  - every authored packet's `type` is one of the listed elements (so the
    JSON reads as what happens when the first element is picked).
- `ResolvedSkill` carries `ElementChoice[] Elements` (`[Serializable]`,
  `{ DamageType Type; SpellPresentation Vfx; }`), `HasElementChoice`, and
  `ResolvedSkill AsElement(DamageType)` which returns a copy with every
  packet retyped and, when that element authored a `vfx` with a path, that
  presentation in place of the skill's own. Id, cooldown, cost, reach and
  everything else are unchanged on the copy. Implement the copy with
  `MemberwiseClone` plus a fresh packet array -- NOT a hand-written
  field-by-field copy, which is the bug class AUDIT #60 records.
- `docs/CONTENT_SCHEMA.md` is regenerated (`tools/content_schema.ps1`) and
  the new fields carry `[ContentDoc]` text like their neighbours.

### C2. Session

- `FightSession.CastSkill(int index, CombatantState target, DamageType?
  element)` is the new seam. The existing two-argument overload calls it
  with `null`.
- Refused outright, spending nothing and not ending the turn, with a
  message: an element skill cast with no element; an element skill cast
  with an element it does not list; a non-element skill cast with an
  element. Refusal ordering: after the reach check, before the cost check
  (a choice the menu should never have offered must not read as "cannot
  afford").
- A valid cast resolves `skill.AsElement(element)` through the existing
  `CastSkill(ResolvedSkill, target)`; no change to `ResolveDamageSingle`,
  `ResolveDamageInstances`, cooldowns, the ledger or the beat. Pin: against
  a target weak to Fire, a Fire-chosen cast lands +50% and an Earth-chosen
  cast lands neutral, with literal expected numbers.
- `PreviewSkillPower` and the detail card work on the retyped copy when an
  element is selected, so the POWER row and the damage-type row describe
  what the click will do.

### C3. Menu and HUD

- `MenuDepth` gains `Element`, between `Sub` and `Target`. Only a skill with
  `HasElementChoice` enters it; every other skill's path is byte-for-byte
  what it is today (Root -> Sub -> Target, or instant resolve for Self/Party).
- Pressing an element skill's row opens Element depth: one `SubmenuRow` per
  element, named by the element, affordable, no cost text. Hovering selects
  for the detail card, which is `DetailForSkill` on the retyped copy.
  Pressing an element row records it on `FightMenuState.ChosenElement` and
  enters Target depth. The enemy click passes the chosen element to C2.
- `Back`: Target -> Element (element skill) or Sub (any other); Element ->
  Sub, clearing the chosen element; Sub -> Root. `Reset` clears the element.
- `Breadcrumb` reads `C O M M A N D  ›  S K I L L  ›  E L E M E N T` at
  Element depth and `...  ›  E L E M E N T  ›  T A R G E T` at Target depth
  for an element skill. Unchanged for everything else.
- `DamageTypeLabel` for an element skill with no element chosen lists the
  choices joined by `/` (the same join the multi-packet label already uses).
- The submenu column already holds at least four rows (Shawn's kit needs
  more); confirm rather than assume, and say so in the report.

### C4. Bot

- `FightAction` carries `DamageType? Element`. `LegalActions` emits one
  action per (element x eligible target) for an element skill and exactly
  what it emits today for every other skill. `Apply` passes the element
  through C2. `ToString` shows it. `FightRunner`'s `"Skill:<id>"` naming is
  unchanged.
- Policy scoring: if a policy already scores a damaging cast through the
  session's preview or affinity, hand it the retyped copy so Fire against a
  Fire-weak target scores higher. Add no new heuristic. Every policy must
  still pick a legal action on Odette's turn: `FightInvariants` is the
  check, `BotFightRunnerTests` the harness.

### C5. Verification gates, in order

1. `dotnet build tools/domain-tests` green after every Domain edit.
2. `tools/test.ps1 content` and `tools/test.ps1 combat` green (dotnet path
   for the `[D]` classes; the combat area includes PlayMode, ~110s).
3. `tools/build_content.ps1` builds; `git status` shows the three
   placeholder assets deleted and `prismatic_orb.asset` plus the stamp added.
4. `tools/preview.ps1 -Spell prismatic_orb` and `-Character owl` write
   pictures; look at them. The cast will show no spell art (assumption 4);
   the damage number and the log line must show the chosen element.
5. `tools/run_tests_parallel.ps1 -BuildContent` green before the commit.
   Add `-BuildScenes` only if a screen tree under `Domain/UiKit/Screens/`
   or a `[SerializeField]` changed; it should not need to.
6. Commit on `prismatic-orb` by explicit path. The tree carries another
   session's uncommitted font and art changes: never stage anything you did
   not touch for this plan.

## Draw policy

Assumption 4 above: no picture yet. `ElementChoice.Vfx` is the seat the art
will take. When the four frame sheets exist, `slice_spell_sheet.py --new
prismatic_orb_fire --sheet ...` four times and four `vfx` blocks in the
`elements` list is the whole delivery -- no C#.

## What this deliberately does not do

- No per-element status rider (Water chills, Earth roots, ...). A
  four-way rider table is a second mechanic; the owner did not ask for it.
- No enemy re-authoring for Earth/Water/Wind (assumption 5).
- No talent-tree node for the orb; level 1 is the ladder the owner named.
- No VFX.

## Friction register (filled in by the implementer, then triaged into AUDIT.md)

Every file the orb itself touched, every file the mechanic touched, with a
one-line reason each, and for each: was the touch about the orb, about the
mechanic, or about the chain restating a field (AUDIT #60's class)? Counts
in a table at the end. This is the deliverable the owner asked for beside
the skill.
