# Playtest batch 2026-10-02: lanes and briefs (rev 2)

20 owner items, 9 lanes, 4 waves. Each brief pastes as is. Owner words are quoted. **Assumed** marks a gap I filled; every one is listed at the end. File:line pointers were verified against the tree on 2026-10-02.

## Rules for every lane

- One shared tree. Stage explicit paths only.
- The tree already carries uncommitted scene, font and `CLAUDE.md` changes from before this batch. Don't revert them and don't stage them.
- Run gates as one foreground call with a long timeout. Never background a gate and wait for it (memory `subagents-must-block-on-gates-in-foreground`).
- Pick gates per `docs/TESTING.md`: iterate with the dotnet `[D]` loop or `tools/test.ps1 <area>`, then commit through `tools/run_tests_parallel.ps1 -Changed`.
  - Add `-BuildScenes` only if the lane is marked **scenes** below.
  - Add `-BuildContent` only if it is marked **content**.
- Captures use the default hidden desktop. No `-Runtime` captures while the owner is at the desk.
- Bugs get fixed without asking. A real design fork goes into the report with your recommendation, not into a blocking question.
- Commit each test-passing checkpoint yourself with explicit paths. Report: commit sha, gate command plus its pass line, and every assumption made.

## Waves

Within a wave, at most one lane runs `-BuildScenes` and at most one runs `-BuildContent`. Lanes inside a wave don't share files.

| Wave | Lanes | Scenes | Content |
|---|---|---|---|
| 1 | 1 Fight HUD, 3 Combat rules, 4 Audio, 9 Prompts | 1 | 4 |
| 2 | 2 Menus, 5 Bell content | 2 | 5 |
| 3 | 6 Bell presentation, 7 Blackglass | 6 | 7 |
| 4 | 8 Attack motion + Odette ring | 8 | 8 |

Why this order:
- Lane 6 edits `FightScreen.cs` after lane 1 has finished with it.
- Lanes 4, 7 and 8 each edit `skills.json`, so they sit in separate waves.

Reuse: start fresh `implementer`s with these briefs. Resuming the old sessions (`Navigation feedback and controls`, `Character animation and positioning issues`) re-reads about 200K of uncached context per turn, and I didn't confirm they belong to this project.

---

## Lane 1: Fight HUD readability (`implementer`, scenes)

1. **Spell detail card** (owner screenshot of Prismatic Orb, bottom right). Only the MP row has text. The shield, arrow and dot rows show an icon and no value. The rest of the card is empty space.
   - `FightHudModel.cs:703-724` always adds TARGET, EFFECT and SCALES with values. The blank rows are therefore a paint bug in `FightController.Hud.cs` `RefreshDetail` / `FillDetailIcons`, not missing data. Find why the values don't reach the row labels.
   - Then size the card to its rows.
   - Check five skills of different shapes (two-use rule) and capture before and after.
2. **Buff/debuff tooltip**: "still way too small and illegible." Sizing goes through `Core/TooltipFit.cs`. Bring its body text up to the reading size the fight HUD uses for intent badges (`FightScreen.cs` near 775, "AT READING SIZE"). Capture before and after.
3. **Low-HP warning**: "should pulse, and should only cover the outer 10% of the edges. Right now it's really too much."
   - It exists: `FightScreen.cs:1448` `BuildLowHpVignette` is a full-stretch `proc:vignette`, toggled by `FightController.Hud.cs` `RefreshLowHpVignette` (about line 67).
   - Confine it to a band of about 10% of the screen at the edges, with a slow alpha pulse.
   - Keep the existing anti-flicker hysteresis (the comment at `Hud.cs:77`). It never blocks input.
4. **Ward feedback**: "ward does not show application right after cast." The ward status or bar appears late. Find the HUD refresh that misses the cast beat and fix it for every ward source: skills, plus the Magical Shield and Sparring Buckler relics, which raise a ward on cast (`FightSession.RelicMechanics.cs` near 395).

## Lane 2: Menus and flow (`implementer`, scenes)

1. **Navigation**: "can't click down from Party, Glossary, Options and Exit to the stat block."
   - Those four are the system menu's tabs (`SystemMenuScreen.cs:217-241`).
   - Fix: Down from the tab strip enters the active pane's first selectable element, which is the stat block on Party. Apply this to all four panes as one rule in the screen tree, not as per-button links.
   - Context: memories `gamepad-navigation-2026-09-17` and `gamepad-owner-feedback-2026-09-18`, in particular "Cancel steps back a level first".
2. **Reckoning**: "the Continue button completely blocks the rest." `ReckoningScreen.cs`: lay Continue out so it covers nothing at 16:9 or 4:3. Capture both.
3. **Bug**: equipping Blackglass Shard on Shawn jumps to the reward track. The spell does equip.
   - Section switching lives in `CharacterDossierController.cs`. See lines 170 (`trackScreen.ExitTo = SectionRow(RewardTrack)`), 266 and 460-473.
   - Suspect: an equip or rebuild path that re-enters `ShowSection` with a stale `_section` or `ExitTo`.
   - Fix the class (any spell, any character) and add a regression test.

## Lane 3: Combat rules (`implementer`, no build: Domain C# only)

1. **Bjorn's Fury**: "does not get any Fury through poison damage (DoTs I presume) and not through wards either."
   - Bjorn has no flat gains (`pools.json:25`). His Fury comes from one of three root engines (`Domain/Combat/FuryEngine.cs`, `FightSession.FuryEngines.cs`):
     - Sentinel: pays per hit taken.
     - Einherjar: pays per damaging action.
     - Juggernaut: pays per turn.
   - Trigger → condition → outcome. **Assumed:**
     - **Sentinel**: a DoT tick on Bjorn, or a hit his ward absorbs fully or partly, counts as a hit taken. It pays on the raw incoming amount, the same way `PlantedShield.FuryForAbsorbedHit` already does.
     - **Einherjar**: a DoT that Bjorn applied pays Fury when it ticks, as damage from his action.
     - **Juggernaut**: unchanged, since it is per turn by design. Say so in the report.
   - Pin expected values as literals (gotcha 5). Bot policy code reading Fury must stay consistent.
2. **Magical Shield** (a relic): its ward "should dissipate like normal wards. This is too strong."
   - Today it lasts `FightTuning.MagicalShieldDurationTurns = 99` (`FightTuning.cs:127`).
   - **That constant is shared with Sparring Buckler (`RelicMechanics.cs:398`) and the third relic ward.** Change Magical Shield only: it takes `DefaultWardTurns` (1).
   - Rename the constant so it no longer names Magical Shield, and fix the comments at `FightTuning.cs:105-127` that state the old design.
   - The other relic wards stay whole-fight. Flag this in the report as an owner call.

## Lane 4: Audio (`implementer`, content)

Context: memory `sound-pass-2026-09-29` and `Resources/Audio/README.md`.

1. **Prismatic Orb, Earth**: "sound strikes too late." Align `prismatic_orb_earth_impact` with the contact beat, the way the other three elements are aligned.
2. **Buff/ward sound**: there is none today. The source pack is `C:\Games\Sounds\Prince's Palace\Spells\Holy Spell Pack\96000kHz` (40 wavs; licence in `Contact.txt`).
   - Build one seam: an application cue keyed by status type, not per spell.
   - Ward: start from `Holy_spell_protect_cast_01..03`. Heal and regen: `Holy_spell_heal_cast_bright_*`.
   - Import only the wired clips, matching how earlier packs were imported (check `git log -- Assets/_Project/Resources/Audio/Sfx`). Credit the pack in `CREDITS.md`.
3. **"General normalize over all sound vfx and some reverb filter, maybe a stereo enhancer."**
   - **Normalization already exists.** `tools/measure_audio_levels.py` writes `audio_levels.json` and applies playback gain (SFX target -18 dB RMS, ceiling -1 dB). Don't build a second system.
   - Instead:
     - re-run the tool and confirm every SFX in use has an entry (today 83 entries);
     - judge whether RMS mis-levels short transient hits against long risers. If so, propose switching the SFX metric to LUFS-S or peak-weighted and show the before/after gain table.
   - **The project has no `AudioMixer`.** Reverb means adding one: an SFX group with a reverb send, routed through the existing audio player. Unity has no built-in stereo enhancer. List the options and don't build one.
   - The owner judges by ear, so leave the reverb wet level as one named tuning value.

## Lane 5: Bell in the Fog, content and text (`implementer`, content)

Context: `docs/PLAN_EVENTS_BELL_AND_CARAVAN.md`, `docs/PLAN_BELLWETHER_KIT.md`, `docs/PLAN_DIALOGUE_STAGE.md`.

1. **Remove the Wishing Well event** (`demo_wishing_well`, `events.json:6`). It is the demo fixture for about 11 test files and probably the `docs/EVENTS.md` walkthrough.
   - Move the fixtures and the walkthrough onto another event; don't drop the coverage.
   - Event ids persist in a save's `eventsSeen`, and the `wishing_well_tosses` counter is a profile value. Prove that a save holding either still loads.
   - Delete the untracked `Art/Events/demo_wishing_well.meta`.
2. **Death Knell**: "Don't hand-hold the player."
   - The callout text comes from `FightSession.Enemies.cs:597-610` ("Death Knell! Step back", "Dark Chains! Death Knell next") and `FightHudModel.cs:1645-1705` (" Step back.").
   - **Assumed:** drop both callouts for the Knell sequence. Keep the badge, its number and the domain flag `StepBackIsSafer`, which the bot's `TelegraphAnswer` reads.
3. **"How long was I gone"**: "not done in our correct dialogue way."
   - That line is already dialogue (`events.json:1009`). The broken part is what follows: the `Leave` outcomes (about 1014-1030) put Bjorn's and Odette's replies in a prose `result` string with inline quotes.
   - Turn them into speaker lines with expressions, the way the `lines` arrays do it. If outcomes can't carry `lines` (`docs/CONTENT_SCHEMA.md`), route each outcome to a small dialogue page via `goTo` rather than adding a field.
   - Sweep `events.json` for other quoted-speech `result` strings and report the count.

## Lane 6: Bell in the Fog, presentation (`implementer`, scenes; after lane 1)

1. **Darkness**: "should start off very dark at the edges and the field and gradually creep back per turn up to a minimum of like 15%."
   - **Assumed:** a darkness overlay over the field and the edges starts at about 90% opacity and loses a fixed step each round down to a floor of 15%.
   - Put the start, step and floor in one tuning row and show it only in the Bellwether fight.
   - It has to read under the HUD, not over it.
2. **End of fight**: "it should just give a standard Reckoning screen like you won. The Bellwether should fade into the darkness."
   - Use the normal victory Reckoning, with no event-specific ending screen.
   - The Bellwether's death is a fade-out into the overlay, not the standard death.
   - Find the current ending path through `RunEncounter.cs` and the event-fight handling in `EventFightResolverTests`.

Gate: `BellInTheFogCaptureTests` and `BellwetherKitCaptureTests` captures attached.

## Lane 7: Blackglass Spear VFX (`implementer`, content)

Area: `Art/Sheets/recipes/blackglass_spear*.json`, `vfx.layers` in `skills.json`, and the travel/anchor code. Loop: `docs/ART_PIPELINE.md` §5b. Context: memories `spell-presentation-is-layered-not-per-spell` and `spell-vfx-tails-2026-09-25`.

- Owner: "It doesn't really strongly accumulate in the air and then fire a spear that really flashes and leaves a tear in reality behind it."
- Beats wanted: a strong build-up in the air, then the spear fires with a hard flash, then a lingering tear along the path.
- Trajectory bug: against the Elder Treant it aims at the feet. The rat looks better, but its angle is also off. Aim at the target's body-centre anchor for every enemy size. Check every other projectile spell for the same anchor fault.
- Gate: fight captures against the treant and the rat.

## Lane 8: Attack motion and Odette's ring (`implementer`, scenes + content)

1. **Attack motion**: "We have to redo most attack animations. The lunge in one go is weird. The jump forward and then animate looks way better."
   - The model exists: skill `approach` = Hold | Lunge | Close | Charge (`docs/CONTENT_SCHEMA.md:262`), played by `Core/FightBeatPlayer.cs`.
   - Only 3 skills author `lunge` explicitly, yet the owner says "most". So first, inventory the *effective* motion of every attacking skill and enemy move, including defaults and enemy schedules. Find which ones play move-and-strike as one motion.
   - **Assumed:** "jump forward then animate" is `Close`: travel completes, then the attack pose plays at the target. Convert every one-motion attack to it. Keep a skill on Lunge only where a test or authored comment says why, and list those.
   - Not new art: art policy is stills only.
2. **Odette's green ring**: "it flickers up to her, and then back to the ground all the time." The ring's anchor alternates between her body and her floor seat. Not located yet: start in `FightController.StageVisuals.cs` and whatever owner-side status or aura draws a green ring on `owl`. Pin it to one anchor. Check whether other characters' rings share the cause.

## Lane 9: Designer prompts (main session; docs only)

Format: match `output/event_art/PROMPTS.md`.

1. **Petting Zoo**: an art prompt for the designer, from `docs/PLAN_PETTING_ZOO.md` and memory `petting-zoo-event-2026-09-25`. Style reference: the Giant Rat flat cel sheet (memory `enemy-sprite-style-is-the-giant-rat-sheet`). Stills only.
2. **Frost Flare**: a new art prompt and a sound prompt. Current assets are `Art/Sheets/recipes/frost_flare.json` and `Resources/Audio/Sfx/frost_flare.mp3`.
   - The owner didn't say what's wrong with them. Don't invent a reason; state the prompt's intent and leave a one-line "owner: what to change" slot.
   - If `PROMPTS.md` has no sound-prompt precedent, write the sound brief as: duration, layers, and sync points to the spell's beats (startup, travel, contact).

---

## Assumptions to correct before sending

1. Lane 2: Down from any system-menu tab enters that pane, not only Party.
2. Lane 3: Sentinel counts DoT ticks and ward-absorbed hits as hits taken, Einherjar is paid for its own DoT ticks, and Juggernaut is unchanged.
3. Lane 3: only Magical Shield loses its whole-fight ward. Sparring Buckler and the third relic ward keep theirs.
4. Lane 5: "no hand-holding" removes both Knell callouts ("Step back" and "Death Knell next"). The badge and number stay.
5. Lane 6: darkness starts at about 90%, steps down each round and stops at 15%. Bellwether fight only.
6. Lane 8: every one-motion attack becomes `Close`, including enemy moves.
7. Lane 9: Frost Flare is a full replacement.
