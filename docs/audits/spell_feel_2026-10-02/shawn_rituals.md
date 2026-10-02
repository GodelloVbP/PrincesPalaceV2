# Shawn ritual+settle family vs docs/SPELL_FEEL.md

Read-only audit. Evidence: report.json + strip.png per captured spell (treant and rat), my own montages of strip frames, the 12-cell sheets in Resources/Spells/<id>/f0..f11.png, skills.json vfx blocks, Resources/Audio/Sfx wavs (peak positions approximate, 10 ms steps).

## The suspected contact desync is a tool artefact, not a real desync

- The ritual layer starts at beat start (capture f0), not at the hit cue. `at: release` for a no-gather cast = beat start. Evidence: censer swing already visible at f6-f16, viper fangs at f6, velvet ribbons at f6, in every strip.
- Sheets are 12 cells at 0.06 s (palace_passage: 11 cells = 0.66 s). hitCueSeconds 0.3 = cell index 5 = f18-f21. Cell 5 is the payoff cell in all ten sheets (viper starburst, gale full crescent, velvet cuff, aegis radiant burst, ashen fire-rock explosion, borrowed teal flash + clock hands, palace doors open on the purple field). The recipes' notes say the same ("Impact is authored ... against source cell 6").
- Frames: vipers f18-f19 starburst (IMPACT marker f19); gale crescent f18-f21; censer flame pillar f18-f19; thorn white spark f19-f21; court ring + floor disc f18-f21; velvet cuff f18-f20; palace doors-open f18-f19. Recorded hit_frame is 18 or 19 in all 14 reports, so number + sound fire with the visual payoff. Hard rule holds for all seven.
- Why the tool warns: `release_frame` is inferred as ~18 (it is 7, 14, 3 in other runs, meaningless here) and "biggest rise after release" then picks later ribbon/smoke/ally-reappearance activity: f21 thorn, f25 velvet, f28 censer/court, f34 palace (treant: that is Shawn reappearing from behind the doors at f33, not an effect). All six WARNs (palace/treant, velvet x2, censer/treant, thorn x2, court x2, vipers/rat) are artefacts. Recommended hitCueSeconds per spell: **0.30 s (f18) for all seven, unchanged.** Cell 5 spans 0.30-0.36, so 0.30-0.32 is the window; do not move later.
- Audio check: cast cues are 0.30 s risers peaking ~0.25 s (viper 0.26, censer 0.27, court 0.26, thorn 0.22, palace 0.19, gale 0.18 of a 0.33 s clip), so the peak lands on the cue. Two impact clips peak late: thorn_tithe_impact peaks ~0.21 s into the clip, borrowed_moment_impact ~0.23 s. Their transient is 12-14 frames after the visual payoff. velvet_shackles has no castSfxPath (no riser), only an impact; gilded_aegis has no sfx at all (falls back to generic status_ward cues).
- Tool-level follow-up: spell_feel.py should take the ritual start as capture f0 when the layer is `at: release` with no gather, else this family will WARN forever.

## Family trait (sameness risk)

All ten share: a painted 12-cell overlay on the target (or caster) centre, no travel, 0.06 s per cell, quiet build 0.30 s, one bright cell at 0.30-0.36, 0.3-0.4 s decay, same 0.14 s fade, then a 10-18 particle settle burst with identical physics (drag 1.8, fadeFrom 0.55, endScale 0.72, spin +-180, size 0.1-0.3). Seeds are shared (1067: vipers, gale, thorn; 1455: velvet, borrowed, ashen). Rhythm is therefore one rhythm; only art differs. Afflict spells (censer, thorn, velvet, court) are the worst: "painted aura around the target, flash at 0.3 s, fade", and swirling curved shapes recur in gale, velvet, court, palace. Per the doc, rhythm is an identity axis, so the signature beat has to differ in WHERE and WHEN, not only in painting. See "separating beats" at the end.

---

## vipers_bite (DamageSingle, Poison 3, tier 1, 7 mana, cd 2)
- Verb/signature: bite. Signature: jaws hold open ~0.2 s (cells 2-4 near-identical, f6-f17) then snap with a starburst at f18. Reads as bite, not generic poison. Good.
- Contrast in time: yes, 0.3 s held jaws then a 1-frame snap. Best in family.
- Accent: starburst f18-f19 is the lock point. But a white hurt-flash silhouette follows at f20-f25 (~0.1 s) and is larger/brighter than the bite (hot peak 1.4%); two bright events ~2-3 frames apart, the second one hides the fangs. Flash count 2 per cast, inside the guard.
- Reaction: flash starts f20, a frame or two after the cue frame; target ends green-tinted (f30-f44), that reads Poison landed.
- Communication: green on Poison #A8E63C: correct. Number/badges not visible at this scale; check the frame at number spawn (the 350 box covers the head f12-f24).
- Aftermath: gel drainage f30-f46, residue 0.38-0.40 s, clean 0.77 s. Material-specific, does not compete.
- Action line/art: vertical close of two jaws; reads as jaws. Value contrast: pale ivory fangs on green, good.
- Audio: cast riser + impact peak ~0.07 s, on cue.
- Budget: 0.72+0.14 on a 7-mana cd-2 cast is the longest of the cheap spells; fortieth-cast risk is moderate (0.3 s of held jaws is the cost). Fine at 1.5x (~0.58 s).
- WARNs: desync = artefact; rat "no accent 4.4 vs 3.1" is artefact (starburst clearly lands in the rat montage, f18-f21).
- Fixes: (1) the spell's defining rule, detonating existing Poison, has no visual; capture targets have no Poison. Add a conditional tell when Poison is consumed (code, small, new layer condition); sharpens the "set off what is already there" identity. (2) Shorten the white hurt flash overlap by letting the starburst cell hold one frame longer (cell re-time, content tweak) so bite and flash read as two beats not one smear. (3) Mid-priority: retarget the particle seed (1067 shared) (content). (4) None beyond.

## gale_scythe (DamageAll, Wind 5, tier 2, 11 mana, cd 3, pushes queue)
- Verb/signature: sweep. As shipped it reads as a leaf-and-dust whirlwind/tornado more than a scythe; the crescent arcs at cell 5 (f18-f21) are the scythe-like beat.
- Contrast in time: weak. The whirl is large and bright from f16 to f32 (cells 3-7 all big); no quiet before the loud.
- Accent: white crescent f18-f21 is clear, then hurt-flash white silhouette f22-f26 overlapping the whirl. Fine.
- Communication: teal-white = Wind #A8F0E0 matches; brown dust/orange leaves outside the palette but secondary. A 380 box over the whole treant f16-f32 hides the body and head; number spawn needs checking. Push-down-the-order has no tell (a displaced dust-wake toward the order column would carry it).
- Capture covers one target only; AoE (all enemies simultaneously, per-enemy box) and flash stacking at 3 enemies unverified. Three simultaneous big white whirls plus three hurt flashes land on the same frame: counts as one instant but is the biggest flash load in the family.
- Aftermath: leaves/dust f28-f36, residue 0.28-0.42 s, clean 0.72 s. OK.
- Audio: cast 0.33 s riser peak 0.18, impact peak 0.08. Fine.
- Budget: 11 mana cd 3, 0.72 s is proportionate.
- WARNs: none raised; consistent with the frames.
- Fixes: (1) add the quiet before: cells 0-2 shrink or thin the whirl (content: opacity/size ramp on cells, or re-pick stand-in cells) so the crescent f18 is the only large event; sharpens "one cut". (2) A hard single diagonal cut cell or `punch` raise on cell 5 instead of whirl (new frame if needed). (3) Order-push tell (code or HUD). (4) Verify 3-enemy capture.

## palace_passage (SwapAllies, free action, tier 2, 7 mana, cd 3)
- Verb/signature: step through a door. Pair of gold-framed doors with red curtains and a purple starfield opening at f18-f19, closing f29-f32. Strongest "means the right thing" art of the family, and nothing else looks like it.
- Contrast in time: doors fade in f0-f6, then hold nearly constant f6-f29 (peak activity 3.9 vs floor and typical both low); the 0.3 s cue falls mid-hold with no punch change (punch 0.12). The accent is only the purple-field opening and ribbon, soft.
- The payoff for a swap is a position change. In the treant capture the actors do not change seats (Shawn appears at the same left position at f33), so the swap itself is not evidenced. Doors hide Shawn from f3 to f32 (0.5 s) with no payoff visible; verify in live play that the move happens at the 0.3 s cue (f18) behind the doors, not after f33.
- Effect appears on one anchor (`target-centre`); a swap touches two seats. The second seat gets nothing.
- Communication: no damage, no element; gold/red is Shawn's palette, fine. Covers the actor's whole body 0.5 s (covers face/badges region of that unit).
- Aftermath: tail cells 8-10 shrink and vanish (f26-f32), particles aim down. Residue 0.38-0.42 s. Fine.
- Audio: cast peak 0.19, impact 1.0 s clip peak 0.03. OK.
- Budget: free action, tier 2, cd 3; 0.66 s plus the swap is fair, but a free action should be quick: 0.5 s of doors before payoff is the long end.
- WARN: treant desync 34 = Shawn reappearing; artefact. Rat run clean.
- Fixes: (1) confirm and align the seat change to f18 (code/timing check); (2) put a door on each seat (second layer, `target-centre` on the destination or `formation`, code if absent); (3) shorten the hold f6-f17 (re-time cells) so open-door at 0.3 s is a step change; (4) punch up the starfield cell (content).

## velvet_shackles (Afflict Rooted 2 turns, tier 2, 9 mana, cd 3)
- Verb/signature: bind. Ribbons coil (cells 0-4) then a spiked, chain-linked collar/cuff snaps shut at cell 5 (f18-f20), then loose ribbons keep swirling f21-f32. Signature = the cuff. Reads bind.
- Contrast in time: the cuff is held only ~3 frames, the swirl before and after is the same energy; no silent beat. Tool "no accent" (4.7 vs 3.6 on treant, 2.9 vs 2.4 on rat) is real here: swirling ribbons give one flat plateau.
- Accent: cuff f18-f20, low; no flash, punch 0.14, glow 1.05. The hurt flash does not apply (no damage), so nothing else punctuates.
- Communication: crimson/gold, no element; Rooted has no persistent mark on the target after f34 visible in the montage (check badge). The ribbons cover the treant f12-f32.
- Aftermath: cells 7-11 keep wrapping and fall as petals f22-f38, 0.2 s longer than the lock; competes with the payoff. Residue 0.38-0.40 s.
- Audio: impact only (peak 0.12 in 0.39 s clip), no cast riser, so the 0.3 s build is silent; the payoff peak is ~7 frames after f18.
- Budget: 9 mana, cd 3, fine.
- WARNs: desync artefact; accent real.
- Fixes: (1) cut cells 6-9 (shorten the swirl-after to 1-2 cells, then fade) so the cuff stays on screen as the last, held image; content re-slice, no new art. Sharpens "locked". (2) Hold the cuff 0.12 s (cells 5 repeated) with cell 4 slowed before it: contrast. (3) Add castSfxPath riser (audio) and move the impact transient to 0.30. (4) Persistent shackle mark on the Rooted unit (code/HUD).

## censer_of_embers (Afflict Burn 4/turn x3, tier 2, 8 mana, cd 2)
- Verb/signature: ignite. Signature: orange censer swing/arc at the target's base (f12-f16) then a flame pillar at f18-f19 with embers. Reads fire, not generic.
- Contrast in time: yes. Quiet embers and swing 0-0.25 s, flame at f18. Strong in the family.
- Accent: flame pillar f18-f22, clear on both targets. Peak 6.7 on treant at f18.
- Communication: Fire #FF6A2A matches; flame covers the treant torso and head f18-f24 so the number spawn area is covered. No damage on cast (status): the Burn tick is the damage payoff, and it is not part of this vfx; check the tick beat separately.
- Aftermath: smoke column f24-f36 then ash; clean at 0.88 s on treant (0.57 s after contact), longest in the family. For a DoT that is defensible as "still burning" but it overlaps the next UI beat; tool residue 0.33 s is fine.
- Audio: cast 0.3 riser peak 0.27, impact peak 0.11. Good.
- Budget: 8 mana cd 2, frequent. Treant clean 0.88 s on a cd-2 cast is the longest tail; fortieth-cast risk.
- WARNs: treant desync 28 = smoke rise; artefact. Rat clean.
- Fixes: (1) trim smoke cells so clean lands ~0.75 s (content re-slice); (2) keep one ember/char mark on the target while Burn lasts (code: persistent layer, sharpens "set alight, still burning"); (3) swing arc is the signature, give it a hit-spark at the end of the swing (new frame optional); (4) none.

## thorn_tithe (Afflict Thorned 5 Nature, tier 3, 10 mana, cd 3)
- Verb/signature: impale/ensnare in thorns: brambles ring up around the target, white spark at the centre f18-f21 and thorns sink f30-f36. "Thorned" retaliation (5 on its own move) is not represented. As authored it reads "thorny aura".
- Contrast in time: weak; thorns ring is there from f6.
- Accent: a small white spark at f19-f21, weak; rat "no accent" is real (peak 3.0 vs typical 2.2 on a tiny box).
- Communication: Nature is #5FA24A green; the sheet is brown/orange thorns, so the element colour language is not used (brown thorns on a brown treant also give low value contrast, f12-f32 the ring blends into the treant). Only the status tint carries it.
- Aftermath: thorns sink into the ground f30-f36 (particles aim down), residue 0.38 s. Matches the verb well.
- Audio: impact clip peaks ~0.21 s in (12 frames after the visual), cast riser peak 0.22. The impact transient lags.
- Budget: tier 3, 10 mana, cd 3: 0.72 s is appropriate for the rarest of the Afflicts.
- WARNs: desync artefact; rat accent real.
- Fixes: (1) re-grade thorns toward the Nature green / pale thorns for value contrast on bark (content: tint in recipe, or new frame); (2) make the payoff a thorns-erupt beat at f18 (thorns lunge inward, spike cell), not a white spark: new frame, sharpens "the thorns bite"; (3) retime the impact clip so the transient sits at 0.30; (4) represent retaliation: a small thorn flick whenever the Thorned unit moves (code, DoT beat).

## court_of_whispers (Enthrall, AllEnemies, tier 3, 14 mana, cd 4, caster Vulnerable)
- Verb/signature: unnerve; purple wisps rise round the target, a lit disc at its feet, wisps stream up f6-f32. Reads "arcane aura/curse circle", the verb "whisper/fear" is not in the art. No signature beyond a generic cursed field.
- Contrast in time: weak; wisps present f6-f32, nearly flat. Best accent in family by tool (13.9 on treant) comes from the big ring brightening at f18.
- Accent: ring + centre disc f18-f21. Acceptable.
- Communication: Arcane #C69AF1 purple matches. Effect is only on enemies; the caster's own price (Vulnerable +20%) has no visual on Shawn. Fear has no tell on the target (flinch/cower). Box 400 covers the full target f12-f34.
- Aftermath: wisps drift f30-f44, residue 0.33-0.38 s, clean 0.73 s. Ok.
- Audio: cast peak 0.26, impact 1.2 s clip peak 0.06; long tail (1.2 s) beyond the 0.86 s visual, acceptable.
- Budget: 14 mana cd 4 is the most expensive/rarest in the family but is the same 0.72 s as the 7-mana ones; a rare cast can afford a longer or heavier gather. Not escalated.
- WARNs: desync artefact (28 = ring settling).
- Fixes: (1) a signature that says whisper: hooded courtier silhouettes leaning in at 0.3 s (new frame); sharpens "court"; (2) show the price: a short purple pulse on Shawn at the same cue (second layer on caster, content); (3) lengthen contrast: dim cells 0-3 and spend the extra time on a 0.9-1.0 s rare-cast gather (content timing); (4) a recoil flinch on each target (reaction, code).

## gilded_aegis (Ward, SingleAlly, tier 1, 7 mana, cd 2) - authored data only
- Verb/signature: assemble/ward: four gold shield shards drift (cells 0-3, 0-0.24 s), lock into one crowned shield at cell 4, radial burst with spikes at cell 5 (0.30 s), shield holds cells 6-7, fades. Signature = shards snapping into a shield. Reads ward.
- Doc's buff/ward rule: accent and number on the status-landing frame. Cell 5 = 0.30 = hitCueSeconds: correct. Contrast: drifting shards then lock-in: good.
- Anchor problem: `place: caster-centre` with `targeting: SingleAlly`. caster-centre resolves to the caster's own body (FightController.SpellVfx.cs ~l.489), so a ward cast on another ally shows the shield on Shawn, not on the shielded ally. The eye lands on the wrong body. Real, from data; it is only right for a self-cast. Same on borrowed_moment.
- Communication: gold, no element; no sfxPath/castSfxPath authored, so the cast has no riser and the impact falls back to generic status_ward_1..3. Weakest audio in the family. Persistent shield mark on the ally after f11 not in the vfx (status badge only).
- Flashes: one radial burst, ok. Budget: 7 mana cd 2, 0.72 s, matches vipers (tier 1).
- Capture harness refused (Ward unsupported in preview); not a content issue.
- Fixes: (1) target-centre (the shielded ally): content, sharpens "this one is warded"; (2) add cast riser + impact (audio); (3) leave a faint gold rim on the ally for the ward's 2 turns (code: persistent layer, ward identity); (4) none.

## ashen_reckoning (Reclaim, Poison+Fire 150%, tier 3, 10 mana, cd 2) - authored data only
- Verb/signature: reclaim/convert: green poison motes converge into a spiral (cells 0-3), compress into a glowing lava rock (cell 4), explode orange with green streaks (cell 5, 0.30 s), smoke and cinders to cell 11. Signature = poison swirling into one rock then blowing up. Most narratively coherent art of the family and nothing else uses converge-then-burst.
- Contrast in time: yes (inward motion, 0.24 s, then a 1-cell burst). Cell 5 = 0.30 s = hitCueSeconds, correct.
- Communication: green (Poison) into orange (Fire) matches both element tokens and the two damage packets. Two packets means two numbers (Poison, Fire): per the hard rule each needs its own frame/sound; one sound and one accent are authored, so stagger them 2-3 frames or accept one combined number. Explosion box 360 covers the target f18-f24; number spawn area covered.
- Vulnerable (+20% damage for 1 turn) has no tell.
- Aftermath: smoke column cells 7-9 is thick (0.36-0.54 s); tier-3 so allowed to hold, but it should stop competing with the second packet's number.
- Audio: impact clip 0.9 s, peak at 0.02 (transient on the cue), cast peak 0.24. Good.
- Fixes: (1) split the two packets: poison-green pop at f18, fire pop at f21 (code/timing in the hit sequence); (2) pull smoke cells one cell shorter (content); (3) a Vulnerable mark (HUD/status); (4) none.

## borrowed_moment (Hasten, SingleAlly, tier 2, 8 mana, cd 2) - authored data only
- Verb/signature: hasten/borrow time: hourglass inside a brass ring, sand running upward (rising sand cells 2-4), teal flash with clock hands at cell 5 (0.30 s), hourglass shatters into sand and brass arcs. Signature = hourglass shatter. Reads time; distinct.
- Contrast: lit but gentle build then flash, adequate. Cell 5 = hitCueSeconds, correct.
- Anchor: `caster-centre` on a SingleAlly spell, same issue as gilded_aegis: the hourglass is on Shawn, not the ally who moves earlier. The turn-order change (the actual payoff) is in the HUD column; the vfx and that shift need to share the 0.3 s frame (not verifiable here).
- Communication: no damage; teal/gold, no conflict. Impact clip peaks ~0.23 s into a 0.6 s clip: transient is ~14 frames after the visual.
- Aftermath: sand drifts cells 7-11 (0.42-0.72 s) in empty space, 0.4 s. Fine.
- Fixes: (1) place on the hasted ally (content); (2) retime the impact so its transient is at 0.30 (audio); (3) sand streams toward the turn-order column's slot (code, makes the payoff land where the eye reads turn order); (4) none.

---

## Family: which read alike and the separating beat

Read alike (one beat shape, "aura blooms, flash at 0.3 s, fade"): **thorn_tithe, court_of_whispers, velvet_shackles, censer_of_embers** (all Afflict, status only, target-centre, ring or swirl around the target). Second cluster by swirling-curve shapes: **gale_scythe, velvet_shackles, court_of_whispers, palace_passage**. **vipers_bite, ashen_reckoning, gilded_aegis, borrowed_moment** are already distinct (jaws snap, converge-and-burst, shards lock, hourglass shatter).

Shared traits to break: identical 0.06 s cell rate, cue at cell 5, 0.14 fade, settle emitter physics, centre-of-target placement, no motion across the field.

Per-spell separating beats (grounded in its verb/material, each different in where or when):
- thorn_tithe: thorns burst UP from the floor under the target at 0.3 s, one hard vertical spike frame, then sink (ground-origin, not a ring around the body). Retaliation later shown as a thorn flick on movement.
- court_of_whispers: no bloom at all; a ring of hooded silhouettes at the target's rear that lean in and the target recoils at 0.3 s (a flinch, a screen-space still). Longer, quieter gather since it is the rarest/most expensive.
- velvet_shackles: end held, not swirled: ribbons whip tight and the collar stays as the last image for ~0.15 s (a freeze rather than an aura).
- censer_of_embers: keep the swing (arc then ignite); give the swing travel (arc across the floor from Shawn's side) so the origin is the thurible, with smoke trail, and a lingering coal glow while Burn lasts.
- gale_scythe: single hard diagonal cut across all enemies at 0.3 s, the wind and leaves arrive after the cut as wake; the order push shown as a dust shove.
- palace_passage: doors on both seats, actors cross while hidden (swap shown as a position change at 0.3 s).
- vipers_bite: keep the held-jaw snap; add the consumed-Poison detonation as its second beat.
- gilded_aegis / borrowed_moment: move the anchor to the ally; shield rim and sand-to-order-column as persistent/payoff tells.
