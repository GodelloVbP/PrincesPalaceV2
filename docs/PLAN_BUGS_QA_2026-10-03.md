# Plan: bugs and QA, 2026-10-03

Rev 4, for independent review. Rev 4 answers the second review (findings 5-6)
and the owner's Cinderfault answer; rev 3 answered the first review (findings 1-5); rev 2 folded in my own attack on rev 1 and the
owner's answers of 2026-10-03 (relic chips everywhere a relic is received;
sounds from `C:\Games\Sounds`; the owner judges the Toll art before any rework). Sources: `docs/audits/code_review_2026-10-03.md`
(findings R1-R7) and the owner's QA notes of 2026-10-03 (Q1-Q5). Every repo claim
below was read in the tree on 2026-10-03; file:line refers to that tree.

Companion plans: `PLAN_GIF_CAPTURE.md` (the recording tool Q3 and the visual
checks lean on), `PLAN_UNITY_6_6.md` (runs after this one lands).

## 1. Decisions taken

Each is a gap-fill; the owner can overturn any of them by number.

1. **"Skip to the reckoning screen" on a loss means:** every fight that ends while
   a run continues opens the Reckoning. Today an event fight lost under the
   `wake` policy shows only the fight's own Continue button.
2. **Reckoning without a payout** (a `wake` loss, a `pays: false` event fight) opens
   in an outcome form. It shows no gold, XP or offers, but keeps the title, the
   gained relics, the tally and the way out.
3. **A granted relic is always shown as a chip where the player receives it**
   (owner, 2026-10-03):
   - on the Reckoning for a fight outcome;
   - on the event page for a choice outcome (Petting Zoo's Kinship).

   Pointer hover or pad focus on a chip shows a detail tooltip: name, rarity,
   description.
4. **The effects text never lists relics.** Each fact is shown once, where it is
   received. Other effects (exp, heal, "finish") keep their text line.
5. **The multi-packet accent replay is removed, not re-authored** (R2). An
   authored short accent is a later art/audio task.
6. **Court of Whispers' tail is baked into its impact clip offline.** No per-spell
   runtime reverb system: the project has one world source with one global
   `AudioReverbFilter` and no mixer (`Core/SoundController.cs:26-42`).
7. **"Heavy GUI tricks" on Cinderfault means stage VFX plus the stage shake**,
   not HUD effects. The HUD frame stays still (`Core/StageShake.cs` header).
8. **Harpoons are new, thrown, electrically charged and exploding** (owner,
   2026-10-03, after Rumble's ult). See C10.
9. **Toll of the Flock: only the hoofbeat layer goes from the sound.** The bell
   voice and the noise rush stay. The art rework scope is set after the owner
   and I watch its GIF (C11).

## 2. Behavioural contracts

### C1. Where a fight ends

**Routing reads the settlement, never live run state.**
- `FightEnded` is raised first (`Core/FightController.Input.cs:1437-1440`). Its
  handler settles, and settlement can call `RunManager.EndRun`, which replaces
  the run with `hasRun = false` (`Core/RunManager.cs:288`).
- Only after that does the controller pick a screen (`:1451`). So by the time it
  routes, `RunManager.HasRun` is false after a fatal loss. Rev 2 routed on it and
  would have sent that loss to the Continue button.

Two facts are captured:
- **At fight start:** whether this fight belongs to a run. Fixed for the fight's
  life.
- **At fight end:** the settlement.

Precedence, first match wins:

| # | Condition | Screen | Input owner |
|---|---|---|---|
| 1 | Settlement says the run ended (`RunEnded != null`) | Defeat screen | DefeatController's NavContext (unchanged) |
| 2 | The fight belonged to a run (captured at start) | Reckoning; outcome form when the settlement has no reward | ReckoningController's NavContext |
| 3 | Otherwise (preview, test harness) | Fight's Continue button | Fight NavContext; Submit presses Continue |

- Rule 2 replaces `OpenReckoning`'s "no reward, so fall back to Continue" branch
  (`FightController.Input.cs:1169-1179`). Inside a run there is no Continue
  fallback.
- **Rule 3 fixes the class of the reported bug.** Today
  `NoNavigation(continueButton)` (`:151-152`) and the fight's Submit route
  (`ConfirmFocus`, `:1686`) leave a pad player with no way forward. Contract:
  **whenever a control is the only way forward, the active NavContext can press
  it.**
- **The routing is total over `FightEndReason`**
  (`Domain/Combat/Session/FightSession.Rounds.cs:12`):
  - None: the fight is not over.
  - Defeated and Survived: rule 2.
  - Party down: rule 1 when the settlement ended the run, otherwise rule 2.

  A test enumerates the enum and fails on a reason with no row.

### C2. Relic receipts: one pending list, shown once

**Model.**
- The run keeps a persisted list of **pending relic receipts**: relic ids applied
  but not yet acknowledged by the player.
- Every grant path appends what it actually applied:
  - `SettleEventFight`'s `applied` list, filtered to kind Relic
    (`Core/Bot/RunOrchestrator.Event.cs:440-447`);
  - an event choice's applied effects.
- A relic the apply step skipped (a duplicate) is never appended.
- A surface that displays receipts shows them as chips and remembers which ids
  it displayed. **Leaving that surface acknowledges and clears exactly those
  ids**, in the same save as the leaving. Receipts it did not display stay
  pending.
- "Leaving" covers:
  - Reckoning dismissal;
  - on the event screen, moving to another page, the event's Leave, and handing
    off to a fight or the shelf.
- **Pending receipts count as something to present.** Today a Leave outcome with
  no result text and no effects line closes the event at once
  (`Core/Bot/RunOrchestrator.Event.cs:337-343`, `nothingToSay`). Once relic text
  is removed, a relic-only Leave would close before its receipt is seen. So
  `nothingToSay` also requires that no receipts are pending.

This replaces rev 2's "fight outcome" flag, which suppressed the event-page
chips even when a crash between settlement and the Reckoning meant the player
never saw them. Settlement saves before the Reckoning opens
(`RunOrchestrator.Event.cs:449`), so that gap is real.

| Interrupted at | Reload lands on | Receipt shown on |
|---|---|---|
| After settlement, before the Reckoning opens | Map, event reopens (pending fight cleared) | event page |
| Reckoning open, not dismissed | same | event page |
| After Reckoning dismissal | same | nowhere: acknowledged |

**One component, two surfaces.** A "Gained" chip strip, used on the Reckoning
and on the event page.
- It is a new widget set on the existing relic presenter seam
  (`Core/RelicListPresenter.cs:56`, `Core/RelicRows.cs:16-45`;
  `docs/CODE_MAP.md:74`).
- No second relic formatter.

**Layout.**
- One chip per relic, in grant order: icon plus name.
- Hidden when empty. At most 4 chips; no authored outcome grants more than 2.
- On the Reckoning: directly above Continue (`ReckoningScreen.cs:164`), on every
  tab.
- The vertical budget there is tight (`ReckoningScreen.cs:263-266`), so placement
  is proved by a capture and `UiBindingAudit`.

**Detail.**
- Pointer enter or pad focus shows one tooltip beside the chip: name, rarity,
  description. It goes through the existing `TooltipFocusRouter`
  (`Core/ReckoningController.cs:204, 233-263`).
- Leaving the chip hides it.
- Submit on a chip moves focus to the surface's primary action: Continue on the
  Reckoning, the first choice row on the event page.

**Reckoning navigation graph** (after an offer is chosen, or with no offers;
the offer-choosing phase is unchanged).
- Today the vertical column is tabs, then the relic pager when shown, then
  Continue (`ReckoningController.cs:308-330`). Rev 3 inserts the chip rail
  between the pager and Continue, matching its screen position.

| From | Up | Down | Left/Right | Submit |
|---|---|---|---|---|
| Tabs rail | none (top) | pager if shown, else chips if any, else Continue | next/previous tab, clamp | today's |
| Pager (relics tab, >1 page) | tabs | chips if any, else Continue | prev/next button | today's |
| Chip rail | pager if shown, else tabs | Continue | next/previous chip, clamp | focus to Continue |
| Continue (entry) | chips if any, else pager if shown, else tabs | none | none | dismiss |

- Every element in the column is reachable from Continue on every tab, with and
  without chips. Test T7 walks the graph on each tab.
- The links are rebuilt whenever the tab, the pager's visibility or the chip
  count changes, through the existing wiring's rebuild.

**Event page navigation.**
- Today the event screen's rail holds only the pickable choice rows
  (`Core/EventController.cs:812-830`), and the screen claims Submit while
  dialogue plays (`submitClaim`, `:795`).
- **Chips join navigation when the choice rows do** (`_rowsShown`): never during
  dialogue playback, so Submit keeps advancing the dialogue as today. They are
  visible but not focusable during playback.
- The chips sit above the choice rows and form one Rail.
  - Down from the chip rail goes to the first choice.
  - Up from the first choice goes to the chip rail.
  - Left/Right on the chips clamp.
  - Submit on a chip focuses the first choice (C2 detail).
- Entry focus is unchanged: the first choice. The chips are reached by Up.

**The tree change goes through the generators:**
`Domain/UiKit/Screens/ReckoningScreen.cs`, the event screen tree,
`Editor/SceneBuilder/ScreenRegistry.cs`, then a scene build.

### C3. Reckoning outcome form, and the event page

- **Outcome form** (C1 rule 2 without a reward):
  - Hidden: the gold label, the XP rows and the offer cards.
  - Shown: a title naming the ending (Victory, Endured, Fell; new `UiStrings`
    entries), the chips, the tabs and Continue.
- **Dismissing** goes through `LeaveFight` to the Map, which reopens the event
  and plays its result (`Core/EventController.cs:259-271`), as today.
- **Event page.**
  - `EventEffectSummary` (`Domain/Events/EventEffectSummary.cs:111-113`) stops
    emitting relic text. `event.effect_relic` (`UiStrings.cs:382-383`) is deleted
    once nothing reads it.
  - The page shows the pending receipts as chips (C2) on both the legacy page
    (`EventController.cs:328`) and a staged page (`:391`).
  - After a normal Reckoning dismissal the list is already empty, so no chips
    appear: one fact, shown once.

### C4. Status cues play on the beat that applies the status (R1)

- `StatusCues.Observe` (`Core/StatusCues.cs:55-75`) diffs each combatant's
  statuses as recorded in the beat being played, not the live
  `combatant.Statuses`.
- The baseline is the fight's state at `Session.Begin`, recorded once. Statuses
  present then (relic wards at fight start) never chime.
  - Rev 1 said "first beat a combatant is seen". That silenced a status applied
    by the very first action of the fight.
- Depends on C6: it reads the raw types C6 records.

### C5. A split hit plays its impact clip once (R2)

The `PacketAccent` coroutine and its replay at half volume
(`Core/FightBeatPlayer.cs:1203-1224`) are removed. The staggered numbers and
hit flashes per packet stay.

### C6. Beat snapshots record raw statuses (R3)

- `Vitals` (`Domain/Combat/Session/Vitals.cs:61`) carries, as domain data,
  **everything `FightHudModel.StatusRowsFor` reads at snapshot time**:
  - the status list (type, magnitude, duration);
  - the session facts the rows depend on. `session.IsMarked` is one, per the
    comment at `FightHudModel.cs:1420`. The full list comes from reading
    `StatusRowsFor`, and it is reported in the package.
- `SnapshotVitals` (`FightSession.Beats.cs:514-528`) stops calling `StatusRowsFor`.
- The HUD maps a snapshot to rows when it paints, **from the snapshot only**,
  never the live session. Reading live state at paint time is R1's bug class,
  and moving the formatter must not reintroduce it.
- Rows painted per beat are identical before and after (test T9).
- The motive given in the review (bot and balance runs pay for HUD formatting)
  is measured: `tools/bot.ps1` run time before and after, one seed. If there is
  no measurable gain, the layering fix still stands on its own and the report
  says so.

### C7. The over-art backing writes only when its result changes (R4)

- `FitBacking` (`Core/OverArtEdge.cs:100-119`) maps the label's text bounds into
  the backing parent's space through both transforms (`:108-110`).
- So the backing depends on the text, the alpha, the parent rect, and the
  label's transform relative to that parent, including any animated move or
  scale. Rev 2 listed the first three and would have left the backing behind a
  moving label.
- Rather than enumerating inputs again, the fit is **computed** every
  `LateUpdate`, which is cheap, and **written** (`sizeDelta`,
  `anchoredPosition`, `color`, `SetActive`) only when the result differs from the
  last write by more than 0.01 units or 0.001 alpha.
- The canvas rebuilds that R4 reports come from those writes, not from the
  arithmetic.
- `OnEnable` forces one write, so a re-enabled label never shows a stale plate.

### C8. `place: above` keeps the authored size on tall bodies (R5)

- Today `SpellFlight.AbovePoint` clamps the start under `SkyCeiling = 240`
  (`Domain/Stage/SpellFlight.cs:31, 56-60`). Then `KeepUnderTheLog`
  (`FightController.SpellVfx.cs:683-699`, floor `MinFitScale = 0.25`) shrinks
  the art box so it fits. On the treant, Crownfall lands at about a third of its
  size.
- Contract: a **travelling** layer on `above` is never scaled by the ceiling.
  Precedence, in order:
  1. Authored size.
  2. A fall of at least 0.5 body heights. A start point lowered to the body's
     middle would leave no fall at all on a tall body, and a drop with no fall is
     not the spell.
  3. Staying under the ceiling.

  So the start point lowers until the art fits under the ceiling, but never below
  middle + 0.5 body heights. Whatever still overhangs, overhangs for the
  `travelSeconds` of the fall (0.14 s on Crownfall).
- Stationary layers keep today's `KeepUnderTheLog` behaviour.
- The draw order of the effects band against the battle log decides whether that
  overhang is drawn over or under the log. The package reports which, with a
  capture on the treant.

### C9. Court of Whispers: a long, echoing, whispering tail

- **When it plays.** `sfxPath` plays at the impact moment
  (`Core/FightBeatPlayer.cs:1096`), and `make_impact_cues.py` puts the transient
  at the clip's start. So `hitCueSeconds` (0.3, `skills.json:1637`) is not
  affected by the clip's contents.
  - Rev 1 tied the two together; that was wrong.
- **The clip's head** (transient and body) stays as today. A tail is mixed in
  under and after it and decays to -60 dB by the clip's end.
- **Starting values,** judged by ear, not tuned to numbers:
  - Reverb: RT60 about 1.8 s, pre-delay about 40 ms, two echo taps at about
    120 ms and 240 ms.
  - Detune: two copies of the body at about ±12 cents, mixed under the dry signal.
  - Whispers: a bed that enters about 0.15 s in and decays with the reverb.
  - Total length about 2.0-2.5 s.
- **Whisper source.** `C:\Games\Sounds` has no whisper recordings: no file name
  among its 6,472 matches whisper, murmur, breath, ghost, spirit or chant
  (searched 2026-10-03). Plan:
  - "Whisperize" spoken lines: noise-excited LPC resynthesis, which keeps the
    formants and removes the voicing.
  - Candidate sources: `Super Dialogue Audio Pack v1` (has a license PDF) and
    `RPG Voice Starter Pack` (readme names no licence).
  - Super Dialogue Audio Pack v1: licence confirmed by the owner (2026-10-03).
    The RPG Voice Starter Pack is not used.
  - Several lines are layered, offset, and partly reversed so no word is
    intelligible.
- **Generator.** A script in `tools/` builds the clip from a committed dry source
  and the chosen lines. Running it twice gives byte-identical output.
  `tools/measure_audio_levels.py` is re-run so `audio_levels.json` gains match.
- **Overlap.** The tail outlives the visual (`settle` emitter, life ≤ 0.8 s);
  `PlayOneShot` does not hold the beat. At the 1.5x battle-speed preset a 2.5 s
  tail runs under 2-3 later beats. The owner judges it in game at both speeds. If
  it muddies them, the fix is a shorter tail, not ducking (no mixer exists).

### C10. Cinderfault: charged harpoons, fire circles, then the rift

**Owner's reference (2026-10-03):** Rumble's ult in League of Legends. Electrically
charged harpoons are thrown, land and explode. Circles of fire mark where they
land and hide the rift forming underneath.

**Today.** Release at t=0, the row at `skills.json:196-299`:
- `fault`: the ground crack along the rank.
- `erupt`: per-target black spikes.
- `ash`.
- `hitCueSeconds` 0.2167, no `shake`.

The capture (`docs/captures/spell_feel/cinderfault_vs_rat/strip.png`) shows the
crack visibly forming for f6-f12 before anything happens.

**Identity** (`docs/SPELL_FEEL.md:149-167`). Verb: *pin, then rupture.*
Signature beat: charged harpoons nail each enemy's ground, the ground ignites
around them, and the earth tears open between them. A vertical drop is
Crownfall's signature, so the harpoons arrive on a steep diagonal from the
caster's side.

| Beat | Time (s, from release) | What the eye sees | Notes |
|---|---|---|---|
| Throw | 0 to about 0.18 | One harpoon per struck enemy, crackling with electric arcs, flies in on a steep arc from the caster's side, oriented along its path | new art; travelling layer, `orient: path` |
| Landing | staggered about 0.05 apart, in rank order, front to back | Each harpoon strikes the ground at its enemy's foot line and bursts: an electric flash plus sparks | new burst art; emitter for sparks |
| Fire circle | from each landing | A ring of fire blooms around the impact point, about 1.2× body width, full in about 0.08 s | new art, ground band |
| Hidden crack | under the rings | The `fault` crack-widening frames play beneath the rings, so the rift is never seen forming | rings drawn above `fault` |
| Rupture (hit cue) | last landing + about 0.10 | The rift rips open along the rank, existing spikes erupt from the rings, strong stage shake | `shake` (`CONTENT_SCHEMA.md:263`); hit cue moves later, to about 0.40 for three targets |
| Burn-down | about 0.35 | Rings, arcs and rift cool; residue under 0.5 s | |

**Layer-system capabilities** this needs and that are not established:
- **Landing a travelling layer on the foot line.** `above` lands on the middle
  (`Domain/Stage/SpellFlight.cs:56-60`).
- **An arc path.**
- **A per-struck-index stagger.**

Each missing one is added as **one field on the layer model** (default = today's
behaviour, documented in `CONTENT_SCHEMA.md`, with a second plausible user named
in the commit), never a per-spell offset. The package reports which were needed.

**Sound.**
- The hit cue moves, so the cast cue `cinderfault_startup` (the owner's recording)
  is re-cut by `tools/make_spell_cast_cues.py` (`:19-20`).
- An electric crack layer is added to the impact.
- Both come from `C:\Games\Sounds` (the Dark and Fire spell packs,
  `Prince's Palace/Spells/...`).
- The owner hears both before commit.

**Art.** Harpoon (a still, charged), landing burst, fire ring (bloom, loop,
burn-down). The owner generates them from prompts in
`docs/prompts/2026-10-03_cinderfault.md`.
- The mechanics land first with stand-ins: Blackglass's spear as the harpoon,
  and the eruption's ember frames as the ring.

**Element mismatch (open call O5).** Cinderfault deals Fire 5 + Nature 5
(`skills.json:207-216`). `Lightning` exists as a damage type
(`Domain/Stats/DamageType.cs:26`). Electric arcs will read as Lightning to a
player who knows the element colours.
- Keep the damage and treat the charge as flavour, or change the damage?
- The plan proceeds visually and leaves the numbers untouched.

### C11. Toll of the Flock

**Sound.** `flock_charge.wav` is regenerated without the hoof loop:
- The hoof loop is at `output/event_art/generate_bell_event_audio.py:108-119`.
- The bell voice (strike at 0.56 s) and the rush stay.
- `hitCueSeconds` 0.55 (`relics.json`, the toll row) stays aligned with the strike.
- The generator lives under the git-untracked `output/`. It moves into `tools/`,
  keeping only the flock function's inputs, so the clip has a committed source.

**Art, step 1: record and look.**
- A GIF of the sheet animation, from the existing `slice_spell_sheet.py` GIF path
  (`tools/slice_spell_sheet.py:1092`).
- A GIF in a fight once `PLAN_GIF_CAPTURE.md` phase A lands.
- Defects I already read off `Art/Sheets/Spells/toll_of_the_flock/sheet.png`:
  - The flock count flickers between 3 sheep (f0, f1, f3) and 2 (f2, f4, f5).
  - In f2 and f4 two bodies merge into one elongated sheep.
  - The six frames are separate poses rather than a gallop cycle.

**Art, step 2.** The owner decides the rework. I write the prompt (constant flock
count, a looping stride, a dissolve end frame); the owner generates; then it is
sliced and recorded again.

### C12. spell_feel follow-ups (R7)

Corrected against `docs/captures/spell_feel/INDEX.md`:
- **Spore Cloud on the treant:** no WARN (row 53). The finding is visual (no
  cloud around the target) and is judged on the GIF, not the report.
- **Boulder Slam:** recorded hit cue f9 against authored f14 (rows 49-50). The
  authored value moves to the visible contact unless the GIF shows the art
  itself is late.
- **Residue tails:**
  - Censer of Embers: 0.73 s treant, 0.72 s rat (rows 35-36).
  - Dark Chains: 0.73 s rat only (row 44). The treant row has no residue warn.
  - Bring each under 0.5 s by ending the lingering layer, not by fading it
    faster across the board.

### C13. History comments (R6)

Rewritten to say why the code is this way, in present tense. Locations:
- `Domain/UiKit/Screens/FightScreen.cs:2950-2951`
- `Domain/Combat/Session/FightHudModel.cs:756-762`
- `Tests/EditMode/Combat/MeleeSoundOverrideTests.cs:346-347`
- `Tests/PlayMode/Combat/FightSkillDetailElementIconTests.cs:138-140`
- `Tests/PlayMode/Ui/DossierLoadoutTests.cs:447-450`

## 3. Test matrix

Behaviour first; each row names the contract it proves. Pad rows drive the
production `NavigationInputModule` with an injected input source, not direct
method calls. The expected values are literals, never recomputed from the
production formula (CLAUDE.md gotcha 5).

| # | Scenario | Expected | Contract |
|---|---|---|---|
| T0 | Every `FightEndReason` value | Each has exactly one routing row; an unlisted value fails the test | C1 |
| T1 | Room fight won | Reckoning, full form | C1 r2 |
| T2 | Room fight lost, run ends, driven through the real `FightEnded` -> settlement -> routing flow (not a stubbed settlement) | Defeat screen, although `RunManager.HasRun` is already false when routing runs | C1 r1 |
| T3 | Bellwether: Survived (10 tolls) | Reckoning; chip = Toll of the Flock; after dismissal the event page shows no chips and its text has the exp line and no relic | C1, C2, C3 |
| T4 | Bellwether: Defeated | Reckoning (`pays: true`, `events.json:1233`); Gained = Toll of the Flock, then Bellwether's Bell | C2 |
| T5 | Bellwether: Fell (`wake`) | Reckoning outcome form, title Fell, no chips; pad Submit on Continue reaches the Map; the event plays the Fell text | C1, C3 |
| T6 | Preview fight, no run, ends | Continue shown; pad Submit presses it | C1 r3 |
| T7 | Pad on the Reckoning, each tab in turn, with 0 and 2 chips, relics tab with and without a second page | Every column element (tabs, pager, chips, Continue) reachable from Continue and back by the C2 graph; chip focus shows the tooltip and it follows Left/Right; Submit on a chip returns to Continue | C2 |
| T8 | A relic already held is granted again | The strip shows only what was applied | C2 |
| T9 | Petting Zoo choice granting Kinship, driven by pad from the page's normal entry | During dialogue the chip is visible, not focusable, and Submit advances the dialogue; after it, Up from the first choice focuses the chip and its tooltip shows; Submit on the chip returns to the first choice; the text line has no "Relic:" entry | C2, C3 |
| T9b | An outcome that is Leave with only a relic grant and no result text | The event does not close at once; the chip is shown; Leave then closes it and the receipt is acknowledged | C2 |
| T9c | A grant on page 1 whose receipts are displayed, then the player moves to page 2 | Page 1's receipts are acknowledged on leaving page 1; a grant applied on page 2 is shown there | C2 |
| T10 | Bellwether Defeated, then reload at each of: before the Reckoning opens; Reckoning open; after dismissal | Event page shows both chips; shows both chips; shows none | C2 receipts |
| T11 | Same fight, beat by beat | HUD status rows per beat identical before and after C6 (recorded literals), including a Marked target | C6 |
| T12 | Ward applied on beat 3 of a batch; a status applied by the fight's first action; a relic ward present at `Begin` | Chime on beat 3; chime on the first action's beat; no chime for the relic ward | C4 |
| T13 | Frost Flare (split hit) | Impact clip requested once per beat | C5 |
| T14 | Over-art label: static for 60 frames; text change; label moved and scaled relative to the parent without a text change; disabled and re-enabled | No write while static; a write on the frame of each change; plate correct after re-enable | C7 |
| T15 | Crownfall on treant and on rat | Spike box at authored fit size on both; fall distance ≥ 0.5 body heights on both (literals) | C8 |
| T16 | Court of Whispers clip | Head matches the dry source within tolerance; length and decay within the C9 ranges; generator byte-deterministic | C9 |
| T17 | Cinderfault vs rat, treant, 3-rat formation | Harpoons land on each foot line in rank order; rings bloom at each landing; no crack pixels outside the rings before the hit cue; hit cue after the last landing; residue < 0.5 s | C10 |

## 4. Packages

Ordered. Packages that share a file are serialized. Agent routing per CLAUDE.md.

| Pkg | Contracts | Files (main) | Agent | Verifier gate (one run per snapshot) |
|---|---|---|---|---|
| P1 | C6, then C4, then C5 | `Vitals.cs`, `FightSession.Beats.cs`, `FightHudModel.cs`, HUD paint path, `StatusCues.cs`, `FightBeatPlayer.cs` | implementer | `tools/test.ps1` combat areas; `run_tests_parallel.ps1 -Changed`; bot timing (C6) |
| P2 | C7, then C13 | `OverArtEdge.cs`, the five comment sites | fixer | `-Changed` |
| P3 | C8 | `SpellFlight.cs`, `FightController.SpellVfx.cs` | implementer | `-Changed`; `spell_feel.ps1 -Spell crownfall` compare |
| P4 | C1, C2, C3 | `FightController.Input.cs`, `FightBootstrap.cs`, `RunOrchestrator.Event.cs`, `FightSettlement`, run snapshot pending-receipts field (save version bump if the save code requires one), `ReckoningScreen.cs`, the event screen tree, `ScreenRegistry.cs`, `ReckoningController.cs`, `EventController.cs`, `EventEffectSummary.cs`, `RelicListPresenter` widget set, `UiStrings` | senior. `Escalation: cross-layer` (Domain tree + Core + scene generation with new serialized fields and a save field, landing atomically) | `-Changed -BuildScenes`; **new named capture cases are deliverables of P4** (they become `PLAN_GIF_CAPTURE.md` `still` scenarios): Reckoning full form with offers, outcome form (Fell), 0 chips, 2 chips, chip tooltip focused by pad, event legacy page with a receipt, event staged page with a receipt |
| P5 | C9, C11 sound | `tools/` generators, two wavs, `audio_levels.json` | implementer | Python tests; `-Changed`; owner listens before commit |
| P6 | C10 | `skills.json` cinderfault row, layer system if the stagger field is needed (after P3), prompt doc | implementer | `-Changed -BuildContent`; `spell_feel.ps1 -Spell cinderfault -Versus rat,treant` compare |
| P7 | C12 | `skills.json` other rows (after P6) | implementer | `-BuildContent`; `spell_feel.ps1` per spell |
| P8 | C11 art | sheet, recipe, sliced frames | owner, then slicer | GIFs |

**Parallelism.** P1, P2, P4 and P5 have disjoint file sets. P1's `FightHudModel.cs`
and P2's comment edit in the same file are the one overlap: P2 does C13 only after
P1 lands.

**Verification follows `docs/WORKFLOW.md` "Verification runs":**
- Each implementation owner iterates with focused tests only (`tools/test.ps1 <area>`).
- When a package is ready, I record a snapshot: HEAD sha plus the package's
  uncommitted paths.
- **One verifier** runs the commit gate (`run_tests_parallel.ps1 -Changed`, with
  `-BuildScenes` / `-BuildContent` where listed) once against that snapshot.
- Gates are serialized: one verifier at a time across all packages. A result is
  void if any gate-relevant file changed after its snapshot.
- Each owner stages only its own paths. P3 runs before P6, and P6 before P7.

**Docs in the same commit as the change:**
- `docs/CONTENT_SCHEMA.md` for a new layer field (P6);
- `docs/CODE_MAP.md` for the chip widget set (P4);
- `AUDIT.md` rows closed for R1-R7.

## 5. Who looks at pictures

- **Me (orchestrator), before any package is reported done.** I look at the
  frames or GIF of every visual change myself. Agents' visual claims have been
  wrong before.
- **Owner:**
  - Cinderfault before/after GIFs.
  - The Toll of the Flock GIFs: the owner looks first, and the rework is
    whatever the owner decides after looking.
  - Reckoning and event-page captures with chips and tooltip.
  - Court of Whispers and the Toll sound in game, by ear, at 1x and 1.5x.
- **Runtime captures:** OK this session (owner, 2026-10-03). They run on the
  hidden desktop.

## 6. Open owner calls

- **O2.** Whisper source: whisperized dialogue lines (C9), pending the licence
  check. Answered in part: sounds come from `C:\Games\Sounds`, which has no
  whispers as such.
- **O3.** Toll of the Flock art direction, after the owner sees the GIFs.
- **O5.** Cinderfault's electric look against its Fire + Nature damage (C10).

Answered 2026-10-03: O1 (relic chips wherever a relic is received) is now C2/C3;
O4 (harpoons are new, thrown, charged; C10); the Super Dialogue pack licence is
confirmed for C9.
