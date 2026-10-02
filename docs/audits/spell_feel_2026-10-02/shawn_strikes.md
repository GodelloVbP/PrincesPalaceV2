# Shawn strikes vs SPELL_FEEL.md

Basis: strips (treant for all six; cinderfault also rat), contact still for frost_flare, report.json for frost_flare, INDEX.md, skills.json vfx blocks, FightHudPalette. I did not open each stills/*.png separately: the strips carry the same frames, so frame numbers below are strip frames at 60 fps.

Shared facts (apply to all six, stated once):
- Every cast ends in the same target white-out (hurt flash): the treant is a solid white silhouette with a white puff, held ~6-8 frames then fading over ~8 more (mud f21-27, frost_flare f19-24, lightning f19-24, cinderfault f15-20, winters f29-35, crownfall f31-36). It is the loudest frame of every spell and it is identical, so it hides each spell's own accent and element colour. Large flashes per cast: 2 (layer starburst/impact + the white-out, ~0.03-0.1 s apart). Per second: 2 for one cast, over the 3/s guard if two casts land inside one second (e.g. frost_flare 0.52 s then lightning 0.52 s would be 4). Not a per-spell fault, but the white-out hurts element identity everywhere.
- No damage number or badge is visible in any contact still or strip crop I looked at (frost_flare contact_f18 shows none, the number spawns after). I could not verify number/effect overlap at the number's spawn frame from this evidence; check one live frame per spell. The target's head/body is covered by column-shaped effects (frost_flare, lightning, cinderfault) at contact, so overlap is likely for those three.
- Audio: report.json has no audio measurement. Sync below is authored sfx + hitCue vs rec contact frame only.
- Reaction: the white-out starts on the contact frame or one after (vis = rec+1), good. No flinch/knockback visible in the strips (treant does not move in any). Stage shake and hit-stop exist in code, not judged here (a strip cannot show it).

## mud_burst (Earth, 8 mana, tier 1, cd 2, Vulnerable)
Verb/beat: "hurl a gout" - a dark gold orb gathers on the caster (f0-11), then a horizontal gold-orange flame-stream fires (f12-17), burst on the treant (f20). Signature beat: none - reads as a fire-breath/flamethrower, not mud. The stream is orange-gold, the burst is white-out; no brown, no wet/mud read, no splatter on the ground. Earth token is C98A3A (tan-gold) so colour is defensible, but the shape and the fire-like glow are the problem.
- Contrast in time: good. 0.2 s hold, 0.1 s stream, then payoff (gather:travel 3.0 vs treant, tool shows 0.4 vs rat, artefact: the tool finds the gather/travel edge differently per target).
- Accent: f20-21 starburst on the body, single frame, ok.
- Reaction: white-out only; no weight (mud should be heavy: nothing slumps or drops).
- Communication: element ok-ish; Vulnerable status gets no distinct mark in the strip (cannot see the badge). Treant after f30 turns green-tinted, which is the shared hurt-flash fade, not a status.
- Aftermath vs payoff: orange sparks linger f24-33 (0.18 s residue, fine) but they read as embers, again fire.
- Action line: one horizontal line, clean. Value contrast: warm on dark forest, ok.
- Audio: sfx + castSfx authored; NO hitCueSeconds authored (auth ?), legacy impactFrame 13 vs recorded contact 20 - the data sync is unverifiable and the 7-frame gap between authored `impactFrame` and recorded contact is a real mismatch to fix in data (use layerFormat + hitCue).
- Budget: 0.63 s for an 8-mana cooldown-2 spell; neighbours winters_rebuke (8 mana, 1.03 s) and crownfall (6 mana, 1.07 s). Mid-short, fine.
- WARNs: none. Real.
Fixes:
1. Make the projectile mud: swap the stream for a lobbed arc (verb "lob/splat") with dark brown clumps; same 0.1 s flight. New frames or emitter retint (content tweak if emitter, new frame if painted). Serves signature beat + art meaning; sharpens "heavy, wet earth" vs fire.
2. Leave a mud splat on the ground at the feet through the residue (replaces ember sparks): content tweak with existing `place: target` layer. Serves aftermath-as-identity and Vulnerable (the target is coated).
3. Author hitCueSeconds and layers so contact = authored (0.333 s) and sfx lands with the splat. Content tweak.
4. Slight downward target squash at contact (code, only if a squash hook exists): carries mud weight. Skip if not.

## frost_flare (Fire+Ice, 7 mana, tier 2)
Verb/beat as it stands: none - reads as lightning. f6-18 a thin white-blue jagged column grows on the treant (it is literally the lightning_bolt silhouette), ground burst at f18 is white-purple; no flame anywhere in the frames. Verb intended: fire and ice colliding. The in-game sheet gives no fire and no ice shards.
- Contrast in time: weak. 0.1 s of nothing, then 0.2 s of a slowly growing column at the target, no gap before contact (the build is the attack). No quiet-then-loud.
- Accent: contact still f18 is a white-purple ground burst, good frame, but it arrives after 0.2 s of the same column so it does not pop; peak 11.2 is 3x typical, fine.
- Reaction: shared white-out only.
- Communication: FAIL. Fire = FF6A2A, Ice = 9FD8F5; frames show white/violet/blue-white. No orange anywhere; a Fire+Ice spell reads as lightning (and lightning_bolt in the same colours).
- Aftermath: tiny orange ember at the feet f24-31 is the only fire; 0.15 s residue, fine.
- Action line: vertical bolt on the target. Art meaning: wrong (storm). Value contrast: white on dark, ok.
- Audio: sfx authored, no hitCue (auth ?), legacy impactFrame 5 vs recorded contact 18 (13-frame gap in data, same legacy issue).
- Budget: 0.52 s total (clean 0.48 s), 7 mana tier 2: appropriate.
- Flashes: 1 starburst + 1 white-out; the column itself is bright for 0.2 s (f10-18 hot 0.4-0.6% rising): borderline, not a guard breach.
- WARNs: none. Real absence of problem flags is itself the tool artefact here: the tool has no check for element colour.
Fixes:
1. Wire the new sheet (see below): it already carries the fire/ice split. Content/art wiring.
2. Make the fire/ice split directional: fire half and ice half arrive from opposite sides and collide at the target, so the "collision" is the signature beat. Needs the new frames; layers via existing fields.
3. Put 0.15 s of dead air before the collision so contrast in time exists (shift the strike later or add a small two-coloured gather at the caster).
4. Author hitCue so sfx and the burst share the frame.

### New sheet (Assets/_Project/Art/Sheets/frost_flare.png, 3x2, black bg, not wired)
Read from the PNG: f0 a loose swirl of orange embers and ice shards (small, dim, fits a gather beat). f1 a spiral with a white-hot core, fire arms left, ice arms right (a good "two forces meeting" gather, centre bright). f2 a tall vertical spear, left half flame, right half crystal, tapering to a point at the bottom (travel/strike, pointing down - needs `orient: path` with artDegrees around -90; the 50/50 split will rotate with it, so the fire/ice halves may flip sides on the diagonal; check). f3 the spear embedded in a white burst, flame left, shards right (contact). f4 big payoff: flames left, long ice shards right, white core - the largest and brightest; it is the accent frame. f5 sparse settle: embers and shards, tiny orange flame left (residue).
Good: the verb "fire and ice shear together" is visible, colours match FightHudPalette. Concerns: black background means it needs the `#00FF00` key route or luminance additive; ART_PIPELINE says green key never luminance, so the dark interiors are fine as opaque only if re-done, and the glows/sparks here are black-keyed; f4's footprint (~500 px of 1536/3) is wide and will cover the target's number/head if fit to target; f2-f3 are tall and will read like a lightning bolt again if used as a vertical strike on the target, so use f2 as a diagonal flight from the sky point (the winters_rebuke/crownfall pattern) only if you accept the cousin relationship; better to keep f1->f4 as a collision at the target.

## cinderfault (Fire+Nature, 15 mana, tier 2, cd 3, DamageAll)
Verb/beat: "erupt" - a fire fissure ground-opens under the target (f5-12, orange flame rises), bursts at contact (f13), white-out f15. Signature: the fissure ground span (`fault` layer, align span across the formation) - it is distinct. Good: an ground-line verb unlike the others.
- Contrast in time: the weakest spot. Total 0.38 s clean (rat 0.37 s). The most expensive spell of the six (15 mana, cd 3, AoE) is the shortest. There is no quiet part; flame ramps from f5, hits f13.
- Accent: tool WARN "no accent (peak 7.1 vs 1.5x typical 6.4)" on treant: REAL. In the strip f13 (impact) is nearly identical to f12 - a ramping fire column that never changes at contact; the white-out is what the tool measures.
- Reaction: shared white-out.
- Communication: Fire only. Nature (5F A2 4A green) appears nowhere; first green is the treant's own recolour after f21. Fire+Nature reads as fire. For rats (f10-12) the flame is small and reads as a campfire under the rat, hot peak 0.89%.
- Aftermath: 0.12 s residue, clean. Fine. Action line: vertical rise, ok. Art: a campfire/bonfire silhouette (the flame is a tapered orange bonfire shape at the trunk base), not a fissure: no crack in the floor visible in the strip, the `cinderfault_ground` sprite seems not to read at this scale.
- Audio: eruption_impact + startup authored, hitCue 0.2167 = frame 13 = rec (rec 13, rat 14), in sync. Good.
- Budget: 0.38 s for 15 mana/cd 3: too short for its tier; the fortieth cast is fine, the first isn't.
- Flashes: eruption flare is small, 1 large white-out only. Good.
- WARNs: accent (real). Rat: none.
Fixes:
1. Add a held ground crack before the eruption (0.2 s of glowing fissure widening, silent) so the burst has a quiet part: content tweak (fault layer earlier, `erupt` delayed) - serves contrast+accent, sharpens "ground opens" identity. Keeps AoE total <0.7 s.
2. Make the eruption a different shape/size from the ramp: a bursting column wider than the pre-flame, with a one-frame white-orange core (punch up); the accent peak. Content tweak (punch/glow) or a new frame.
3. Show Nature: green-black ash/spore motes rising from the crack in the residue, or a green vein line in the fissure: emitter retint, content tweak. Serves communication.
4. Check number overlap: a bonfire at the body centre sits under the number.

## lightning_bolt (Lightning, 11 mana, tier 3)
Verb/beat: "strike from nowhere" - small sparks at the treant's head (f0-5), a jagged bolt grows down the body (f6-17), bright ground flash (f18), white-out. Signature: the bolt itself (jagged branching) - works, reads as lightning (Lightning token F5F06A yellow-white: frames are blue-white, near enough).
- Contrast in time: the sparks f0-5 are a tell but the bolt then takes 0.2 s to grow with visible jagging, which weakens the "instant" quality of lightning: the real thing should be: dim, then one frame, then hit. Currently it ramps linearly into contact like frost_flare.
- Accent: peak 11.3 at f19, 3x typical, good, hot peak 3.46%.
- Reaction: shared white-out; lightning is the one spell where a brief flinch/jitter on the target would be natural.
- Communication: element ok (blue-white); bolt overlays the head and trunk: covers the number's spawn zone likely.
- Aftermath: tiny orange fire at feet f24-31, clean by 0.48 s; the orange ember is a fire cue, slightly wrong for Lightning but also the same ember as frost_flare (shared prop).
- Audio: lightning_strike sfx; no hitCue authored (auth ?), legacy impactFrame 5 vs rec 18: unverified. Thunder should land on the one flash frame.
- Budget: 0.52 s, 11 mana tier 3: shortest relative to cost after cinderfault; rat gather 0.28/travel 0.02 = tool artefact (the bolt is instantaneous on short targets).
- Flashes: bolt itself brightens f17-20 (hot 3.5%) + white-out: 2.
- WARNs: none. Real "none".
Fixes:
1. Compress the build: sparks 0.2 s, then the bolt appears at full length in 1-2 frames with a single bright frame, then jag/flicker 3 frames. Content tweak (frame timing). Sharpens "instant".
2. Add a one-frame sky flash/darkening of the stage before the strike (code or a full-field layer): reaction to a storm; careful with the flash guard (counts as a large flash, so remove the extra bolt brightening).
3. Replace the orange ember with a scorch mark/arc residue, or cut it: tweak.
4. Author hitCue to the strike frame.

## winters_rebuke (Ice, 8 mana, tier 2, cd 2, Chilled)
Verb/beat: "fire a shard from the sky" - a blue swirl hangs above the caster/target (f0-17, a long idle), turns into a crystal spear (f18-27), lands in a starburst (f28), frost collapses and settles as a ring at the feet (f38-48). Signature: the frost ring at the feet + chill that lingers (ring f38-48): a real, readable identity and the best-defined Chilled statement of the group.
- Contrast in time: good. 0.27 s gather hold, 0.2 s flight, a big impact.
- Accent: f28 ice starburst is crisp, white-blue, on the body: good. peak 12.4.
- Reaction: white-out (f29-35) then a long green-tinted hurt fade with ice crystals still in front of the body f36-42.
- Communication: Ice 9FD8F5 matches. Chilled gets the ring (works).
- Aftermath vs payoff: 0.38 s residue (0.43 clean after contact); the ring is a deliberate hold and ends ok.
- Action line: diagonal descent from the sky point, one line. Art: shard-in-body reads as a spear/arrow into the tree (the design note says tip buries itself): acceptable. Value contrast: pale blue on dark: ok.
- Audio: cast + impact sfx, hitCue 0.46 = frame 28 = rec 28, synced. Good.
- Budget: 1.03 s (62 frames) for an 8-mana cd-2 tier-2 spell: long; crownfall (6 mana, tier 1) is 1.07 s. The fortieth cast of a cd-2 spell: a 0.27 s hang above the caster that does not do anything (a ~4-pixel swirl at f0-11) is the dead part; the swirl is small and far from both actors (f0-5 shows a tiny blue dot above the caster: weak).
- Flashes: 2 (impact starburst, white-out): the starburst plus white-out are adjacent frames: effectively one long ~0.15 s flash. Hot peak 3.2%.
- WARNs: none. Real.
Fixes:
1. Shorten the sky swirl and cut the pre-start dead time (`amass` 0.28 s at 7 fps; start the spear earlier) so the cast is ~0.75 s. Content tweak. Serves budget; keeps identity.
2. Make the amass bigger/brighter so the hang reads as "frost forming" (the dot at f0-10 is under-sized for a stage this big): size 260 -> larger or add cold breath at the caster.
3. Keep ring but let it freeze the ground (stay as a still for 0.1 s more) only if it replaces the crystal shards still covering the body at f36-42.
4. Shard exit: the spear is also a diagonal from the sky point, same as crownfall - see caster section.

## crownfall (Arcane, 6 mana, tier 1)
Verb/beat: "drop a crown on the target" - a violet crown assembles in the air (f0-17, ring -> crown -> crystal), turns into a spike (f18-29, diagonal), shatters on the treant (f30), shards fall. Signature: the crown that assembles (readable art, purple matches Arcane C69AF1). Nice.
- Contrast in time: gather 0.36 s then flight 0.16 s then payoff: ok, but the crown is held at f12-17 in the same sprite pose for 6 frames (dead).
- Accent: f30 shatter burst, then white-out. Peak 11.3. Fine.
- Reaction: shared white-out; purple shards remain through f42-50 over a green-tinted body.
- Communication: Arcane purple, good. Marked consumption (12 vs 7 damage): no visible difference in either capture (strip not captured Marked) - the bigger-damage branch has no visual escalation.
- Aftermath: 0.38 s residue ok; shards linger 0.3 s on the body.
- Action line: diagonal from sky point like winters_rebuke. Art: crown becomes a javelin: reads "spear" more than "fall": the verb "crownfall" implies falling, the delivery is a diagonal throw.
- Audio: cast + impact, hitCue 0.5 = f30 = rec 30. Good.
- Budget: 1.07 s total for the CHEAPEST spell (6 mana, tier 1): this is the one that fails the fortieth-cast test. It is the longest of the six and the lowest tier.
- Flashes: 2.
- WARNs: INDEX shows "gather measured 0.13 s, authored 0.34 s; travel measured 0.37 s, authored 0.16 s" on the treant: tool artefact (it splits at the first big pixel change, caused by the crown becoming brighter earlier than the release); the rat capture agrees with authored (gather 0.35, travel 0.15). Not a problem.
Fixes:
1. Cut the cast to ~0.7 s: crown assembles in 0.2 s, not 0.36 (fps 14 -> ~20) and shorten the 0.5 s hang. Content tweak. Serves budget; keeps the assembling crown.
2. Make it a drop: spike falls vertically from above the target instead of from the sky point diagonal (`place: target`, orient down). Content tweak; sharpens verb ("falls") and separates it from winters_rebuke.
3. Marked branch: a bigger, gold-edged crown for the consumed hit (needs a variant frame or a code branch on damage branch): new frame/code. Serves communication of the 12-damage payoff.
4. Trim the shard hang over the body f36-50.

## blackglass_spear (reference)
Works as defined in SPELL_FEEL.md. Only note: 12 mana tier 3 cd 3, 0.6 s gather, 1.1 s clean - the same weight as winters_rebuke (8 mana) and crownfall (6), so this is NOT longer than the cheap spells; the budget relation (expensive = longer) is not expressed across the roster: crownfall and winters are as long as the 12-mana spell, cinderfault (15) is a third as long. Treat as roster-level, not the reference's fault.

## Caster-level: spells that read alike
1. frost_flare + lightning_bolt (worst): both are a vertical bright column on the treant, grown over 0.2 s, ground flash at f18, same ember at the feet, same total 0.52 s, same timing (rec 18 for both), near-identical clean frames (f24-31 in both strips). Shared trait: vertical stroke, same rhythm, same colour family. One change: frost_flare becomes a two-sided collision with the new sheet (fire from one side, ice from the other, meeting at the target), lightning_bolt stays a vertical instant bolt but loses the build-up.
2. winters_rebuke + crownfall: both are a sky-held hang -> diagonal spear from the sky point -> burst on the body -> shard settle, ~1.0 s, rec 28 vs 30, hitCue 0.46 vs 0.5. Shared trait: identical beat sequence and staging. One change: crownfall drops vertically onto the target (a fall), winters_rebuke stays a diagonal (a thrown shard) and keeps its ground ring.
3. cinderfault + mud_burst: both are warm orange/brown on the ground/body with a bonfire-like flame and an ember tail. Shared trait: orange flame shapes and ember residue. One change: mud_burst becomes brown splatter, no flame (see its fix 1).
4. Shared across all: the identical treant white-out and white puff reads as one effect for every element. Single change for the roster: tint the hurt flash toward the spell's element or shorten it to 3 frames (code, FightBeat hurt flash), because it hides each spell's colour.

## Ranked worst problems
1. frost_flare reads as lightning: no orange/ice split anywhere in the frames; same rhythm and column as lightning_bolt. New sheet fixes it if used as a collision, not a vertical strike.
2. Budget inverted: crownfall (6 mana) 1.07 s and winters_rebuke (8) 1.03 s outlast cinderfault (15 mana AoE, 0.38 s) and match blackglass (12 mana).
3. cinderfault has no accent (real WARN, f13 looks like f12) and no Nature colour.
4. The shared treant white-out is the loudest frame for every spell and swallows element colour; 2 large flashes per cast.
5. mud_burst reads as a fire stream (no mud), and frost_flare/lightning/mud use legacy impactFrame with no hitCue (authored 5/5/13 vs recorded 18/18/20): audio sync is unverified for these three.
