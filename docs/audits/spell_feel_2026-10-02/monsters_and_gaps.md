# Monster skills vs SPELL_FEEL.md, plus gaps (read-only review)

Paths: captures `docs/captures/spell_feel/<spell>_vs_<enemy>/{report.json,strip.png,stills/}`; in-context Bellwether frames `docs/captures/playtest-2026-10-02/lane6/kit_m5/`; raw frames `tools/screenshots/spell_feel/<spell>_vs_<enemy>/spell_<spell>_fNN.png` (60 fps, f = capture frame). Authored: `Assets/_Project/ContentData/skills.json`.

## How the Shawn-cast harness distorts monster skills

- Caster is Shawn (left, small), target is the treant/rat (right). Real enemy casts run right-to-left at a small party member. Direction of every `facing: auto` layer is mirrored from reality; target scale is inverted (a 620 px wave under a broad treant reads normal, under 150 px Shawn it reads huge).
- The recording starts at the first drawn frame, and monster beats author no caster pose in the capture, so the enemy's own wind-up, approach and stance are absent. Layers authored `at: hit` have no gather at all (frame 0 IS contact).
- Therefore: timing, residue and accent numbers are trustworthy; direction, scale and the telegraph/enemy-body relationship are not. Those come from the kit_m5 in-context frames where one exists (Bellwether only). Golem, treant, bog witch have no in-context capture anywhere in `docs/captures/`.

## Part A

### 1. bellwether_scratch (verb: rake; signature beat: red claw marks held on the target plus thrown blood)

Evidence: strip f0-f42, `stills/contact_f04.png`; in-context `bell_f_scratch_telegraph`, `bell_g_scratch_035`, `bell_h_after_scratch_bleed`.

- Contrast in time: capture has none (all layers `at: hit`; report gather/travel n/a). In context the Bellwether's lunge is the quiet part and the rake is the loud one; works. Not judgeable from the capture.
- Accent: white `flash` layer (glow 1.1, punch 0.25) lands with the hurt flash on one event. strip f1-f5: target is a solid white silhouette and the rake (white/lilac tips) is white-on-white; red claws only separate from f7 (0.12 s), held to f20 (0.28 s).
- Reaction: whole-body white hurt flash f1-f8, then fade to tint by f24. Starts on contact, about 6 frames. Good.
- Communication: telegraph badge is a red rake icon plus "35" (`bell_f_scratch_telegraph`); the hit shows red "-37" (Physical, `FightHudPalette.DamageTypePhysical`) beside the claws. Same hue family, consistent. The -37 sits next to the Bellwether's raised claw hand at `bell_g_scratch_035` but does not cover Shawn's face or badges. Bleed is carried by the red claws and the "Shawn is afflicted with Bleed!" line; the Bleed badge on the plate is outside this effect.
- Aftermath vs payoff: residue 0.43 s, clean 0.48 s after contact. Reasonable for a rake that leaves bleed.
- Action line: diagonal claws plus drops thrown along 215 degrees. One line. Good.
- Art meaning: reads as claw rake, not slash arc. Good.
- Value contrast: saturated red on dark forest; clear in context.
- Audio: `bellwether_scratch_impact.wav` at `hitCueSeconds` 0.12; no cast cue (fine for a lunge).
- Flashes/s: one white event. OK.
- Budget: most-seen move in the kit (every non-pair turn); 0.42 s rake + 0.3 s fade is the right size. Survives 1.5x by construction (unmeasured; 1.5x not captured).
- WARNs: none. "No release/contact detected" (rec None) is an ARTEFACT: the hit cue crosses before `module.HitCueCrossedForTest` is installed because the first drawn frame IS the hit (see Part B2 d).

Fixes:
1. Separate the claws from the flash: stagger the three claw strokes (draw-on over ~0.06 s) so strokes land as the white silhouette is at peak, rather than sitting invisible inside it until f7. Cost: low (use sprite frames 0-2 earlier or delay `flash` by 1-2 frames). Sharpens: scratch's signature is the drawn-on rake, not the generic flash.
2. Make Bleed legible after the cast ends: the claws fade at 0.28 s but the status continues for 3 turns; a faint red claw tint on the plate or a drip that persists. Cost: medium (status cue, not spell). Sharpens: "rake that keeps bleeding".
3. Check at 1.5x and 4:3 in context (not done). Cost: low.

### 2. death_knell (verb: toll / execute by seat; signature beat: bell cracks, a void wave rolls under the target)

Evidence: strip f0-f44, `stills/{gather_f11,release_f24,contact_f30,residue_f42}.png`; in-context `bell_c_knell_telegraph_front`, `bell_d_knell_telegraph_middle`, `bell_e_knell_050/070`.

- Contrast in time: bell swings 0.38 s (f6-f23, ring arcs from f21), wave erupts 0.5 s. Good quiet-then-loud. gather:travel 3.3.
- Accent: wave appears at full size on the contact frame f30, tree flashes white f31-f37. In context `bell_e_knell_050` the wave crest plus white Shawn silhouette plus bell glow arrive together. One moment. Good.
- Reaction: full white silhouette ~6 frames (f31-f37), shake 0.8 authored. Starts on contact. Good.
- Communication: strongest in the roster. Telegraph names "Death Knell!" with a bell icon and per-seat numbers (5200 front, 800 middle, SAFE rear) at `bell_c_knell_telegraph_front` and `bell_d_...middle`, so the party sees who is threatened before the hit. Hit popup is magenta -800 (Void, `DamageTypeVoid #C22FB0`), wave and bell are lilac/violet, matching. Gap: the telegraph badge is tinted by intent kind (`EnemyIntentIcons.TintFor`, Knell `#D9C8F2`, or lethal red `#FF3B3B` when lethal), not by element, so the badge number is red while the result is magenta. Not wrong, but the badge and the hit are two colour languages. The popup never overlaps the wave (-800 floats above Shawn).
- Aftermath vs payoff: residue 0.15 s, clean 0.2 s after contact. The audio is 1.4 s (`death_knell_impact.wav`) while the picture is gone in 0.2 s. For the move that deals 130% max HP at the front this is the shortest aftermath in the kit. The principle allows holding a heavy spell's aftermath on purpose.
- Action line: bell drops from the sky, wave runs along the floor. Two lines but sequential, read as one toll then its shock. Fine.
- Art meaning: cracked bell plus violet shards reads as "toll / doom", not as a prop. Strong (`bell_e_knell_070`: cracked bell outline).
- Value contrast: lilac outline on dark canopy; good in context.
- Audio: cast riser (0.5 s clip, peak window 2.20-2.70 s of source) lands on the 0.5 s hit cue by design. Payoff sound, number and accent share frame 30. OK.
- Flashes/s: bell glow plus white silhouette plus wave glow inside ~0.12 s; one large flash event. OK.
- Budget: rare boss pair move; 0.78 s bell is affordable. 1.4 s audio does not scale at 1.5x (known).
- WARNs: none. (`vis` 31 vs `auth` 30 is one frame; fine.)
- Capture caveat: bell is `place: sky`; in context (`bell_e_knell_050`) it hangs at x~790 between the Bellwether and Shawn, 490 px from the target. The bell does not point at the victim; the wave under him and the seat labels do that.

Fixes:
1. Hold the aftermath on purpose: let the wave's last shards or a ring-down of the bell linger ~0.3-0.4 s past the hit, outside the white silhouette frames, so a lethal outcome registers. Cost: low (`seconds`/`fade` on wave, optional extra tail layer from existing shockwave frames). Sharpens: a toll rings out; nothing else in the roster rings.
2. Aim the bell: place it over the victim's seat (or tilt it toward him) rather than mid-field. Cost: low if `sky` accepts a target offset, else medium. Sharpens: "the bell tolls for him".
3. Unify the colour language: tint the knell badge number/icon by `DamageType.Void` via `FightHudPalette.ForDamageType` when not lethal, keep red only for the lethal state. Cost: low. Sharpens: the move's void identity shows before it lands.
4. Capture a 3-PC in-context cast at 1.5x and 4:3 (nothing exists). Cost: low, evidence only.

### 3. boulder_slam (verb: erupt/crush from the ground; signature beat: jagged rock bursts out of the floor under the target)

Evidence: strip f0-f28, `stills/{release_f04,contact_f15,residue_f27}.png`; art `Resources/Spells/golem_boulder/f0..f5`. No in-context golem capture. Legacy `vfx` block (`seconds` 0.48, `impactFrame` 3, no layers).

- Contrast in time: art f0 is blank, f1 is the full eruption already. The rock pops fully formed at 0.08 s (capture f4) with no growth frames, then holds standing 0.17 s, then crumbles (sheet f4-f5, capture f19-f26). No quiet part before it; the pop is the loudest visual and it is 11 frames BEFORE the hurt flash.
- Accent: pop at f4 (0.08 s) and crumble at f20 (0.33 s), contact at f15 (impact frame 3 = 0.24 s). The art's accent is not on contact. At f16-f19 the treant flashes white behind rocks that are still standing.
- Reaction: white flash f16-f19, then tint fade to f27. Starts one frame after rec contact (rec 15, vis 16). Fine.
- Communication: rock tan/ochre; popup is Physical red (no `damageType` authored; `physicalMove true`). Earth art over a Physical number is acceptable for a physical slam. Telegraph relationship unverifiable (no in-context golem frame). The rocks cover the target's whole lower half f4-f19 (strip), so an enemy cast would cover the struck party member's body and, on small bodies, their status badges and number spawn.
- Aftermath vs payoff: residue 0.15 s, clean 0.18 s after contact. Good.
- Action line: vertical eruption. Single line.
- Art meaning: reads as stalagmite spikes, not a boulder and not a slam. The skill is named Boulder Slam and the golem has no wind-up (no `approach`, `windupStance`, `stance` authored); the golem never visibly slams anything.
- Value contrast: tan on dark ground; fine, but low saturation against the brown treant (capture) may blend; unknown against party members.
- Audio: `boulder_slam_impact.wav` (0.39 s, 0.105 s lead-in) at the impact frame 3. Payoff sound on frame 15. OK.
- Flashes/s: one. OK.
- Budget: the golem's ONLY ability (`attackWeight` 0, one `boulder_slam` entry in `enemies.json`), so this plays every golem turn. 0.48 s total is right; the repetition risk is the identical eruption each time, not length.
- WARNs: none.

Fixes:
1. Put the accent on contact: set `impactFrame` to 1 (pop = hit) or add 0.15 s of anticipation (floor crack/dust under the party member, from existing `impact_burst` or particles) so the quiet part precedes the pop. Cost: trivial (one field) to low. Sharpens: the slam lands when the ground opens, not 11 frames earlier.
2. Stop burying the target: the eruption sorts over the body. Move to ground/behind sorting or shrink it to the feet. Cost: medium (legacy path has no `sort`; converting to `layerFormat: 1` layers is the clean route). Sharpens: rock bursts around the victim instead of hiding him.
3. Give the golem a wind-up so "slam" is earned: `windupStance: attack` exists (golem has `attack.png`); the tell then is the golem's raised fist before the ground erupts. Cost: low (content only, art exists); verify the pose reads at that scale. Sharpens: the verb is the golem's, not just the ground's.
4. Vary the eruption per cast (rotate member frames, random flip) since it fires every golem turn. Cost: low if `SpellVfxPlayer` has a flip; else medium.

### 4. spore_cloud (verb: drift and settle; signature beat: a toxic cloud hangs over the formation and sinks)

Evidence: `spore_cloud_vs_treant` strip (too large to read at strip scale), `stills/contact_f21.png`, `stills/residue_f33.png`, crops of raw frames f10/24/40/60/80/100 from `tools/screenshots/spell_feel/spore_cloud_vs_treant/`.

- Contrast in time: authored hit cue 0.35 s (`hitCueSeconds`), cast riser `spore_cloud_cast.wav` 0.35 s. Release is frame 0 so there is no gather; the emitter starts at release.
- Accent: NONE in the picture. At `contact_f21` and `residue_f33` there is no visible cloud; the only emitter output is a few ~20 px droplets near the trunk (11/s, size 0.35-0.7 of a 316 px sprite; thorn_tithe droplet f3 is a green bubble). The cloud does not exist visually; the only accent is the engine hurt flash (white f22-f30) and the green poison tint f36-f47. The spell as drawn is a hurt flash with a status tint.
- Reaction: white flash then green tint on target (`strip` rat f22-f47, treant `f24` and `f40` crops). Reads as poison via the tint, so the status is communicated, but by the status system, not this cast.
- Communication: telegraph "your whole party" scope exists (ART_PIPELINE telegraph rule). Droplets are acid green (`DamageTypePoison #A8E63C` family) and tint is green: consistent. Number: direct damage is Physical (no `damageType`), red by the palette; the "-7" in `residue_f33` is dim and greenish, hard to read against the trunk. It does not cover faces.
- Aftermath: authored length is long (`window` 0.9 s + `lifeMax` 1.4 s, so about 2.3 s cleared). Nothing visible to clear, so the tail is theoretical, but the SIM keeps the cast live.
- Action line: drift upward (aimDegrees 270, gravity -40), 360 spread. No readable line because nothing is drawn.
- Art meaning: droplets are thorn-tithe art reused (comment says no dedicated spore art exists); green bubbles read as "drops of poison", not spores or a cloud.
- Value contrast: pale green specks on a mid-brown trunk: low.
- Audio: cast riser, then impact `spore_cloud_impact.wav` (1.00 s gas puff) at 0.35 s. OK.
- Flashes/s: contact flash 0.15 s; the poison tick at f97/f100 is a second white flash (see WARN note). Two events ~1.5 s apart. OK.
- Budget: 2.3 s authored for something not visible; a sub-0.5 s visible payoff would fit the cost-0 tier better.
- WARNs: RESIDUE TAIL 1.55 s and DESYNC (vis f97/f114 vs auth f21) are ARTEFACTS of the recording window, with one real finding inside them. The window ran to the emitter's authored 2.1-2.3 s; the biggest activity in it is the Poison tick landing at the next turn (white full-body flash with "-3" at f100 in the treant crop; flash at f114-f118 in the rat strip). That tick is a different event, so "vis" and "residue" measure the DoT, not the cast. The real finding is that the authored cast is 2.3 s long and almost invisible.

Fixes:
1. Make the cloud exist: raise `rate` (11 to ~40), `sizeMin/Max` (0.35-0.7 to ~0.9-1.6) and weight toward f3 (green bubble); or add one soft haze sprite layer over the formation. Cost: low for emitter fields, medium if a haze sprite is commissioned. Sharpens: spore cloud's identity is a mass hanging over the party, which is the opposite of a point strike.
2. Give it a payoff beat: a one-time `burst` of droplets at the hit (0.35 s) from each struck body on the existing emitter system; sound and number already on that frame. Cost: low. Sharpens: the cloud "lands" on the party rather than the status silently arriving.
3. Cut the authored tail to about 1.2 s once visible (`window` 0.9 to ~0.5, `lifeMax` 1.4 to ~0.8). Cost: trivial. Sharpens: fortieth-cast budget for a 0-mana move; also stops the harness window overlapping the next poison tick.
4. Owner call, not mine: direct damage type is Physical (red popup) while the move is a poison cloud; Poison/Nature would colour the number on-theme but changes affinity maths. Cost: content, affects balance.

### 5. dark_chains (not captured; harness refuses Reposition) — verb: bind and drag; signature beat: chains snap tight around the target

From authored block plus kit_m5 frames: `bell_a_chains_telegraph`, `bell_b_chains_030/060/080/095/110`.

- Contrast in time: rift opens at the Bellwether (0.42 s), chain flies 0.2-0.52 s, bind wraps at arrival 0.52 s for 0.3 s, then the stage drags the target. In `bell_b_chains_030` a dense chain bundle plus starburst sits at the Bellwether's forward hand; the flight itself is not in any sampled frame.
- Accent: bind wrap with `punch` 0.12, glow 0.5 on the arrival frame, `dark_chains_impact.wav` (0.66 s) at `hitCueSeconds` 0.52. `bell_b_chains_060/080` show the chains wrapped around Shawn: reads clearly.
- Reaction/aftermath: no damage, so no hurt flash. The bind layer ends at 0.82 s; `bell_b_chains_095` shows no chains and Shawn still at the left edge; `bell_b_chains_110` shows him already dragged to x~520. The authored comment confirms why: `follow` is "authored-only, read by nothing", so the drag itself has no tether and no accent. The actual payoff (the pull) is the least-drawn part.
- Communication: telegraph badge "Dark Chains!" with a lavender chain-and-arrow icon above the Bellwether (`bell_a_chains_telegraph`), colour `IntentPull #A88BD8`, and chains/rift are the same violet. Cannot tell WHO is targeted from this frame (only one PC in the capture): needs a 3-PC frame to confirm the telegraph shows the victim ("must show who is targeted before the hit"). Unverified.
- Value contrast: saturated violet chains on dark floor and over Shawn's green cloak: good (`bell_b_chains_060`).
- Audio: impact at contact, fine; no cue for the drag.
- Flashes: none large. Budget: 1-in-5 acting turns; 0.82 s total, acceptable.
- WARN/capture: nothing measured.

Fixes:
1. Tether the drag: make the bind (or a taut chain from the Bellwether) follow the target through the displacement, or hold it until the drag ends and add a ground-skid cue. Cost: medium (the `follow` field is dead; either implement it generally or lengthen the bind to PullHoldSeconds plus the drag duration). Sharpens: the chain's point is being pulled, so the pull needs to be seen.
2. Put the impact sound and an accent on the moment the pull starts (end of hold) not only at arrival. Cost: low. Sharpens: two payoffs (snap, drag) each with a beat.
3. Verify the telegraph names the victim with 3 PCs and at 1.5x. Cost: evidence only.

### 6. bog_mud_burst (never cast) — verb: fling and splat; signature beat: the mud shot leaves a gold-brown splash and Vulnerable

Authored vfx is identical to the player's book spell `mud_burst` (`Spells/mud_burst`, 0.65 s, impactFrame 13, departFrame 9, travel-centre, same sfx), which WAS captured with no WARNs. Judged via that capture: gather 0.25 s (treant)/0.10 s (rat), travel 0.08/0.23 s, contact f20 (rec) vs f21 (vis), residue 0.18 s, clean 0.25 s after contact (`mud_burst_vs_treant/report.json`). See `Resources/Spells/mud_burst/f0..f25` (swirl 0-7, streak 8-11, splat 12-25).

- Accent: splat frame at impact 13 with spray; reads. Aftermath short. Fine.
- Communication: art is gold/ochre (Earth, `DamageTypeEarth #C98A3A`). The bog witch's version has no `damageType` (Physical), so the number is red while the player's version is `damageInstances: Earth`. Mismatch between art colour and popup/telegraph colour for the monster version only. No in-context frame; direction of the left-to-right streak on a right-to-left cast is unverified (legacy `vfx`, no `facing` authored; `SpellVfxPlayer.SetFacing` exists).
- Reach: `reachSlots [2,3]` (middle or rear). Threat aimed at back-rank targets, so the telegraph must show the seat; unverified.
- Identity: shares art with a player spell on purpose; fine. Different caster, same effect.

Fixes:
1. Set `damageType`/`damageInstances` Earth like the player's version so number, hit flash and art agree (owner's call: changes affinity). Cost: content.
2. One in-context capture of the witch casting at a rear PC at 16:9 and 4:3 to confirm facing and seat telegraph. Cost: evidence (needs harness change B2 c).
3. No timing changes needed on the evidence.

### 7. trunk_slam (not captured: no drawn frames) — verb: smash with the trunk; signature beat: the ground bursts where the trunk lands

Authored block (single layer `slam`: `Vfx/impact_burst`, 0.28 s at hit, size 340, glow 0.9, `hitCueSeconds` 0.14; stance `trunk_slam`, `windupStance: attack`, `approach: close`). Art `Resources/Vfx/impact_burst/f0..f5`: a small white six-point star shrinking to a grey disc with eight thin rays, fading (viewed). Treant art has `trunk_slam.png` stance pose.

- It is the house default contact burst (ART_PIPELINE 5c: generated, "burst only, no arc" for blunt). Nothing in the effect says wood, bark or ground. For the Elder Treant's signature blow, identity is the generic blunt hit.
- Contrast in time: the quiet part is the body wind-up (`windupStance`), good in concept (evidence only from authored data).
- Accent: bright star at contact, glow 0.9. Not capture-verifiable. No `shake` authored (knell uses 0.8, roar 0.85), so stage shake depends on damage-scaled hit-stop only.
- Reaction: unknown. Communication: Physical red popup; burst is white, element-neutral; telegraph from the effect.
- Audio: `trunk_slam_impact.wav` 0.20 s `wood_03`, no lead-in. Short; sits on the hit cue.
- Budget: 0.28 s layer, the cheapest in the set.

Fixes:
1. Author `shake` (0.5-0.8) so a slam has the stage react; field exists. Cost: trivial. Sharpens: crush shows in the stage, not in the sprite.
2. Add a splinter-and-dust layer from existing `thorn_tithe_particles` (f0/f2 are thorn shapes) as a ground-level `burst` emitter at hit, plus a short dust ring on the `ground` band. Cost: low (no new art). Sharpens: the trunk's signature is wood and soil, not a star.
3. Scale the burst to the treant (size 340 on a 95 HP boss reads small) once captured. Cost: trivial after capture.
4. First get a capture: see B2 b.

## Part B1 — Odette and Bjorn

Correction to the premise: Odette's `prismatic_orb` HAS layered vfx (one `vfx` block per element in `elements[]`: Earth, Water, Fire, Wind; layers charge/core/wake/shed/splash/spray). The gaps are Mend and Prism Ward. Bjorn's ids are `placeholder_brawler_*` for Slam/Bellow/Brace.

Odette (owl, magic, plain mana)

| Skill | Effect | Presented today |
|---|---|---|
| prismatic_orb | DamageSingle, element picked | layered vfx per element (Water pilot) |
| mend | HealSingle (20 + spell attack, 6 mana) | generic cast pose, heal number in `HealColor` green; no vfx, no stance authored; no status so no StatusCues sound |
| prism_ward | Ward (1 turn, 20 + spell attack) | Shielded chime on apply (`StatusCues.ByType[Shielded]`); status badge; no spell visual |

Bjorn (bear, Fury pool; all but Slam unlock via talents, `unlockLevel` 999)

| Skill | Effect | Presented today |
|---|---|---|
| placeholder_brawler_slam | DamageSingle, Fury tiers x1/x2/x4 | rush/overhead/slam stances, default contact burst (5c), tier shake 0.6/1.0, tier hit-stop 0.18 s at x4; same burst at every tier |
| rampage | DamageAll, same tiers | same as Slam, one burst per target |
| hack | DamageSingle, two hits | rush + attack stance, default contact burst per hit; no shake authored |
| headsplitter | DamageSingle (Fury-scaled) | rush/overhead/slam stances, default burst |
| gorge | DamageSingle + self-heal 30% | rush/overhead/attack stances, default burst; heal number only |
| shield_bash | DamageSingle + Stun | rush/overhead/attack stances, burst, shake 0.5; Stun silent in StatusCues |
| placeholder_brawler_provoke (Bellow) | Provoke | nothing: no vfx, no stance; Provoked is not in `StatusCues` (only Shielded, Protect, Regen are) |
| placeholder_brawler_ward (Brace) | Ward | Shielded chime only |
| plant_the_shield | PlantShield | victory pose, shake 0.3, stage shield prop (`Core/FightController.ShieldProps.cs`, `PlantedShieldStage`), break shards |
| hold_the_line | BuffParty (Fortified) | victory pose, shake 0.4; Fortified has no cue (silent); no vfx |
| berserk | Transform | sprite swap to `bear_berserk` at the impact, victory pose, shake 0.6; no cast layers |
| unbroken | Unbroken (regen + CC immunity) | victory pose, shake 0.4; any Regen chime if the status is Regen (unverified); no vfx |
| bjorn_cursed_blood | CursedBlood (heal becomes Void damage) | victory pose, shake 0.6; no vfx, no persistent cue for a rule that reroutes healing |
| second_wind | HealSelf (Fury to HP) | heal number only |

Ranked by benefit under SPELL_FEEL (frequency and how invisible the effect is), one idea each, not specs:

1. Slam / Rampage (Bjorn). Highest frequency and the Fury tier is the whole fantasy, yet every tier plays the same generic burst. Verb: crush. Signature: the Fury tier written in the ground (x1 dust puff, x2 cracked floor, x4 crater with a held dust ring and the 0.18 s stop). Sharpens the contrast between tiers.
2. Mend (Odette). Her role, cast most, shows only a green number. Verb: stitch. Signature: a single owl-feather bloom that drifts down onto the ally and knits shut, one soft accent on the heal frame, no spike (heal principle: "a gentle swell, a single bloom").
3. Bellow (Bjorn). Core tank loop, invisible, and its effect (enemies must hit him) is exactly what the player needs to see before the enemy telegraph. Verb: dare. Signature: a chest-thump roar ring that sweeps across the field and brands the provoked enemy with a lingering mark that the telegraph then points to.
4. Prism Ward (Odette). Verb: refract. Signature: a prism lattice settles on the ally and splits into the four element colours (the palette tokens Earth/Water/Fire/Wind) as a one-turn halo that dims as the ward is spent; owl-literal "seeing through" the hit.
5. Cursed Blood (Bjorn). The one mechanic that changes what a heal does, with only a victory pose. Needs a persistent Void-tinted cue for the two turns it lasts (communication principle for statuses); verb: curdle. Signature: his healing visibly turns black and flies out at the enemies.
6. Hold the Line (Bjorn). Party-wide buff with zero cue (Fortified silent). Verb: brace. Signature: a line of shield glints sweeping across the whole party, one chime on the status frame (needs a `StatusCues` entry).
7. Brace, Unbroken, Second Wind, Gorge (heal moment), Berserk (cast layers). Lower: Berserk and Plant the Shield already have a form swap and a prop.

## Part B2 — Harness gaps

All four rely on `tools/spell_feel.ps1` calling `SpellRuntimeCaptureTests.TheWaterPilotRecordsAsAFrameSeries` (`Assets/_Project/Scripts/Tests/PlayMode/Art/SpellRuntimeCaptureTests.cs`), which stands the fight up through `PreviewFight.ForSpell` and presses the skill via `ForceFirstAction`.

a. Ward, Reclaim, Hasten (and Reposition) not castable.
- Cause: `PreviewStage.SupportedEffects` (`Assets/_Project/Scripts/Domain/Preview/PreviewStage.cs:35-61`) is a whitelist. Ward is absent (comment `:29-33`: needs a talent tree; its strength lives on the TALENT, `SkillEffect.cs` ~`:41-52`, so an un-talented Ward does nothing), Reclaim needs a status on the target, Hasten needs a turn schedule, Reposition is simply not listed. `PreviewStage.For` returns `Refusal` at `:105-113`, and `PreviewFight.ForSpell` (`Assets/_Project/Scripts/Core/PreviewFight.cs:229-233`) stops there, which the harness prints as "REFUSED".
- Smallest fix (diagnosis only): for effects the stage refuses, bypass the session and play the vfx through the module directly, as the overlap tests already do (`module.PlaySpellVfxForTest`, used in `TwoConsecutiveCastsOverlap...`). That records the presentation without fabricating a talent/status/schedule. The cost is no real damage number or status cue. Reposition alone could be added without faking anything: add it to `SupportedEffects` and see c below for the target pick.

b. trunk_slam empty recording.
- Cause: not an empty draw. The recording loop ends when `module.HasReached(handle, targetCursor)` is true; `SpellPerformancePlayer.HasReached` (`Core/SpellPerformancePlayer.cs:363`) returns true as soon as the handle is not live. trunk_slam has one 0.28 s layer at hit (hit cue 0.14 s), so the cast frees about 0.42 s after release and the loop writes only ~17 frames. The test then fails `Assert.Greater(lit, 20, ...)` (`SpellRuntimeCaptureTests.cs:380`), because every frame was lit but there are fewer than 21. spell_feel.ps1 treats the failed test as "capture failed" and copies nothing, so the INDEX note ("no drawn frames over 17 frames") misdescribes it.
- Smallest fix: relax the floor to scale with the series, e.g. `Assert.Greater(lit, Mathf.Min(20, frames / 2))` or `lit >= frames - 2`. One line at `:380`. Also an unrelated residue: the 0.25 s `FrameEndMargin` never applies when the handle frees early, so short casts get no tail frames; consider continuing `FrameEndMargin` seconds after `!IsLive`.

c. bog_mud_burst never cast.
- Cause: the skill has `reachSlots [2,3]` (middle or rear). With `PP_RUNTIME_VERSUS` set the test fields exactly one body (`SpellRuntimeCaptureTests.cs:238-240`, `new List<string> { versus.Trim() }`), which stands at slot 1. The forced press then confirms `FirstLivingEnemyIndex()` (`Core/FightController.Input.cs:418, 437-446`, first living enemy = slot 1), and `ConfirmTarget` refuses a target that fails `CanReachEnemy` (`Domain/Combat/Session/FightSession.cs:162`). No cast is begun, `fight.IsBusy` never turns true and the test asserts "was never cast".
- Smallest fix: when the skill's `Reach` excludes slot 1, field 3 enemies (VERSUS repeated or filled) and make the forced press pick the first enemy `CanReachEnemy` accepts instead of index 0. Two touch points: the `fielded` list at `SpellRuntimeCaptureTests.cs:238` and `FirstLivingEnemyIndex` at `FightController.Input.cs:437`. The same two edits (last/rear enemy for Reposition, plus `Reposition` in `PreviewStage.SupportedEffects`) would unblock dark_chains.
- Separate, larger gap: the harness only ever casts as the player (`ChooseCaster` falls back to the roster lead, `PreviewFight.cs:325-381`), so no capture can show an enemy cast aimed at the party (SPELL_FEEL workflow step 8). Needs an enemy-side forced ability hook in `FightSession.Enemies.cs` (ability pool is chosen at `:71` and `:303`); that is a harness feature, flagged here and not sized.

d. bellwether_scratch "no release/contact detected" (and any `at: hit` spell).
- Cause: `HitCueCrossedForTest` is installed at `SpellRuntimeCaptureTests.cs:340`, after the wait loop for the first drawn frame at `:299`. A layer timed `at: hit` first draws on the hit-cue frame itself, so the cue has already crossed and `hitFrame` stays -1 (report `rec None`). Smallest fix: after taking the handle, if the module's cursor is already >= `hitCueSeconds`, set `hitFrame = 0`.

e. 1.5x not exposed.
- Cause: `SpellRuntimeCaptureTests` resets `FightBeatPlayer.BeatSpeedMultiplier = 1f` in `StandTheFightUp` (`:175`) and in `[TearDown] Restore` (`:196`); the only env vars read are `PP_RUNTIME_SPELL` and `PP_RUNTIME_VERSUS` (`:122-123`). `FightBeatPlayer.BeatSpeedMultiplier` (`Core/FightBeatPlayer.cs:118`) feeds `Pace` (`:161`).
- Smallest fix: add `PP_RUNTIME_SPEED`, parse as float defaulting 1, assign it at `:175` instead of 1f (leave the TearDown reset), and pass it through `tools/screenshot.ps1` (`$env:PP_RUNTIME_SPELL = $Spell`, near `:201`) and a `-Speed` parameter on `tools/spell_feel.ps1`. Note `Pace` also includes `PlayerSpeedMultiplier` (`:161`), so confirm it is 1 in the fixture. `CaptureFps` time stays fixed at 60, which is correct: the beat waits scale, the clock does not. Check `report.json` timings are compared against `authored` seconds divided by the speed.
