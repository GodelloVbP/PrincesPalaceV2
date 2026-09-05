# Cinderfault Asset Prompt Workflow

**Spell:** Cinderfault  
**Book tier:** 2  
**Mechanic:** Automatically hits every living enemy, up to three; 10 Fire + 10 Nature damage per enemy; 15 mana; unavailable for the caster's next two turns (`cooldownTurns: 3` in the current implementation); strong Intelligence scaling and slight Wisdom scaling; no status or persistent terrain.

## Purpose

This is the complete prompt workflow for producing Cinderfault's audiovisual package. It deliberately separates concept, runtime layers, icon, and audio. Do not ask one generation to solve all of them: each asset has a different composition, transparency, timing, and runtime responsibility.

The workflow produces:

1. one visual-development sheet;
2. one shared ground-fault animation sheet;
3. one target-local eruption animation sheet;
4. one optional caster-entry accent sheet;
5. one spellbook/icon asset when the UI supports spell icons;
6. one pressure/release audio cue;
7. one impact audio cue;
8. correction prompts driven by preview and in-game capture.

## References to attach to visual prompts

Attach the smallest useful reference set:

- a current 1920×1080 fight capture, preferably `tools/screenshots/FightPanel.png`, for stage scale and palette;
- one approved spell master such as `Assets/_Project/Art/Sheets/lightning_bolt.png` for sheet structure and rendering language;
- `Assets/_Project/Art/UI/typography_style_guide_revised_oomph.png` only as a colour/material-direction reference, never as a request to draw UI or text;
- the chosen Cinderfault concept image after Prompt 1.

Do not attach unrelated character sheets. Cinderfault is a global spell and should not inherit one character's anatomy or costume.

## Global visual constraints

Append these constraints to every runtime VFX prompt:

```text
Match the attached Prince's Palace combat art: dark theatrical fantasy,
painterly hand-illustrated forms, strong silhouettes, restrained detail at
gameplay scale, near-black basalt, iron red, molten orange, and tiny yellow-
white heat cores. The effect must remain readable over a dark battle stage.

No characters, creatures, hands, weapons, scenery, horizon, room, floor
texture, interface, lettering, numbers, symbols, captions, frame labels, grid
lines, cell borders, white boxes, drop shadows around the cell, or decorative
border. Do not move or rotate the camera. Do not crop active debris at a cell
edge. Keep identical scale, viewpoint, ground plane, and registration across
all cells. The sequence must progress chronologically from left to right on the
top row, then left to right on the bottom row. Every cell must be separable as
an animation frame.
```

For mixed opaque rock and luminous lava, request a true transparent background and authored alpha. If the chosen generator cannot reliably deliver alpha, request a flat pure `#00FF00` background with no green anywhere in the effect, key it before slicing, and record that preprocessing step. Do not use luminance keying on the basalt: it will erase the dark rock.

## Workflow overview

```text
1. Explore visual thesis
2. Select one concept and freeze the palette/shape language
3. Generate shared ground-fault master
4. Slice and preview the ground sequence
5. Generate target-eruption master against the approved ground concept
6. Slice and composite a rough in-game test
7. Generate optional caster accent only if the cast opening is unclear
8. Generate icon only when its UI destination is confirmed
9. Generate pressure and impact audio cues
10. Capture the complete cast in game
11. Use correction prompts on the specific failing layer
12. Lock sources, manifests, paths, and provenance
```

Do not advance merely because an isolated sheet looks attractive. Advance when the current layer works at combat scale.

## Prompt 1 — Visual-development sheet

### Goal

Choose the spell's silhouette, palette, crack language, and eruption material before commissioning animation frames.

This output is not shipped and does not need transparency-perfect edges.

### Prompt

```text
Create a visual-development sheet for a fantasy combat spell named
"Cinderfault." The spell combines tectonic earth movement and lava. It targets
an entire enemy formation of up to three positions simultaneously.

Show six distinct visual ideas on one clean concept sheet:
1. a thin fault line branching toward three occupied ground positions;
2. black basalt plates beginning to lift;
3. orange lava visible through widening cracks;
4. the instant all three vents rupture simultaneously;
5. heavy rocks falling while smaller embers remain suspended;
6. the fault cooling back to black with no persistent damaging pool.

The emotional progression is pressure, warning, rupture, weight, and cooling.
This is tectonic magic, not three fireballs and not a generic explosion.
Earth must dominate the silhouette; fire is the dangerous interior revealed by
the fracture. Use near-black basalt, dark iron red, molten orange, and only
small yellow-white heat cores at the hottest points. Keep the surrounding page
neutral and unobtrusive.

Match the attached Prince's Palace references: dark theatrical fantasy,
painterly hand-illustrated shapes, strong silhouettes, restrained detail, and
effects designed to remain legible at gameplay scale. No characters, UI,
lettering, captions, spell name, ornate frame, or finished battlefield scene.
This is a shape-and-material exploration sheet, not a splash illustration.
```

### Selection test

Choose a direction only if:

- the effect reads as earth plus lava in grayscale;
- one fault clearly creates all three impacts;
- the peak is not visually indistinguishable from a fire explosion;
- the dark rock remains visible against the fight background;
- the concept leaves negative space for enemy silhouettes and damage numbers;
- the cooling frame does not imply damage-over-time terrain.

Record the chosen concept image as the primary reference for every later prompt.

## Prompt 2 — Shared ground-fault animation master

### Runtime job

One wide effect behind the actors. It communicates that Cinderfault is a single formation-wide spell. It contains the advancing fault, branching cracks, lifted ground plates, and cooling ground—but not the large foreground eruption.

This layer requires a shared formation-width renderer. It should not be cloned once per enemy.

### Recommended sheet

- 2×3 grid
- six chronological drawings
- wide landscape cell inside each grid position
- transparent background/authored alpha
- fixed camera and ground registration
- impact drawing: frame 4, counting from 1, as the plates break open

### Prompt

```text
Using the approved Cinderfault concept as the exact material and palette
reference, create a six-frame VFX animation sprite sheet in a precise 2-row by
3-column grid. Each cell is a wide landscape effect for the ground beneath an
enemy formation of three positions. The six cells form one chronological
sequence, read left to right across the top row and then left to right across
the bottom row.

Frame 1: a nearly invisible hairline of dull red pressure enters along the
ground, mostly dark and restrained.
Frame 2: the line races forward and branches cleanly toward three evenly spaced
impact positions.
Frame 3: black basalt plates at all three positions lift slightly; the branching
cracks brighten from red to molten orange; no eruption yet.
Frame 4: the ground fractures simultaneously at all three positions; plates
snap open and the lava cores reach peak brightness, but leave the large upward
eruption to a separate foreground effect.
Frame 5: the lifted plates fall inward; glow recedes; a few small ground-level
embers remain.
Frame 6: the cracks cool to dark iron red and black, visibly closing with no
persistent lava pool.

The effect must read as one connected tectonic fault, not three separate magic
circles. Keep the space above the ground mostly transparent so three combatant
silhouettes remain visible. Earth and black rock dominate; lava is visible only
inside the fractures. Use true transparent background with clean authored
alpha.

Match the attached Prince's Palace combat art: dark theatrical fantasy,
painterly hand-illustrated forms, strong silhouettes, restrained detail at
gameplay scale, near-black basalt, iron red, molten orange, and tiny yellow-
white heat cores. The effect must remain readable over a dark battle stage.

No characters, creatures, hands, weapons, scenery, horizon, room, floor
texture, interface, lettering, numbers, symbols, captions, frame labels, grid
lines, cell borders, white boxes, drop shadows around the cell, or decorative
border. Do not move or rotate the camera. Do not crop active debris at a cell
edge. Keep identical scale, viewpoint, ground plane, and registration across
all cells. The sequence must progress chronologically from left to right on the
top row, then left to right on the bottom row. Every cell must be separable as
an animation frame.
```

### Preview gate

Before generating the eruption layer:

1. key the background if necessary;
2. run `tools/slice_spell_sheet.py --new cinderfault_ground --sheet <source>` with the measured grid configuration;
3. add and verify a reproducible `VFX` manifest entry;
4. render the GIF preview;
5. place the sequence behind three stand-in enemy silhouettes at real stage scale.

Reject the sheet if the cracks float, cross enemy bodies, look like three unrelated circles, or become invisible against the actual stage.

## Prompt 3 — Target-local eruption animation master

### Runtime job

One compact eruption played at each occupied enemy ground point on the shared impact frame. The same source may be mirrored and varied slightly in runtime scale or particle seed, but all eruptions begin simultaneously.

This layer supplies contact and foreground material. It must not redraw the shared branching fault.

### Recommended sheet

- 2×3 grid
- six square cells
- transparent background/authored alpha
- impact drawing: frame 3 or 4, selected after preview
- central ground contact kept fixed across all cells

### Prompt

```text
Using the approved Cinderfault concept and ground-fault animation as references,
create a six-frame target-local eruption VFX sprite sheet in a precise 2-row by
3-column grid. Each square cell shows one compact lava-and-basalt rupture from
the same fixed ground contact point. The sequence reads left to right across
the top row, then left to right across the bottom row.

Frame 1: compressed dark stones tremble around a narrow orange crack; very
small silhouette.
Frame 2: two or three basalt plates kick upward as the crack flashes brighter;
the eruption is beginning.
Frame 3: peak contact—one forceful vertical rupture of molten lava, sharp black
rock slabs, and a compact dust crown; strong upward silhouette with a bright
core at the ground contact.
Frame 4: the peak widens slightly and begins breaking apart; heavy rocks arc
outward while lava folds back downward.
Frame 5: the lava column collapses; large rocks fall quickly; sparse embers and
dust remain.
Frame 6: only a few falling fragments, dim embers, and a small fading heat haze;
no glowing pool and no suggestion of ongoing damage.

This is a tectonic rupture, not a bomb, fireball, mushroom cloud, geyser of
water, or magical circle. Give the rock fragments weight and gravity. Keep the
bright area compact so an enemy silhouette and its damage number remain
readable. The eruption should cover the lower portion of a target but never
hide the full body for more than the peak frame. Use true transparent
background with clean authored alpha.

Match the attached Prince's Palace combat art: dark theatrical fantasy,
painterly hand-illustrated forms, strong silhouettes, restrained detail at
gameplay scale, near-black basalt, iron red, molten orange, and tiny yellow-
white heat cores. The effect must remain readable over a dark battle stage.

No characters, creatures, hands, weapons, scenery, horizon, room, floor
texture, interface, lettering, numbers, symbols, captions, frame labels, grid
lines, cell borders, white boxes, drop shadows around the cell, or decorative
border. Do not move or rotate the camera. Do not crop active debris at a cell
edge. Keep identical scale, viewpoint, ground plane, and registration across
all cells. The sequence must progress chronologically from left to right on the
top row, then left to right across the bottom row. Every cell must be separable
as an animation frame.
```

### Composite gate

Slice this as `cinderfault_eruption`, preview it, and composite three instances over the shared ground layer with enemy silhouettes present.

All three start on one frame. Vary only secondary debris—not contact timing. Reject if:

- it reads as three unrelated fire attacks;
- bright material hides enemies or damage numbers;
- debris appears weightless;
- the base of the eruption drifts between cells;
- the last frame implies a persistent hazard.

## Prompt 4 — Optional caster-entry accent

### Use only if needed

Do not generate this automatically. First capture the cast using the character's existing cast stance plus the ground effect. Add this asset only if players cannot tell that the caster caused the fault before it appears under the enemies.

### Prompt

```text
Create a compact six-frame magical ground-entry accent for the global spell
Cinderfault, arranged as a precise 2-row by 3-column sprite sheet. The accent
appears at any caster's feet and communicates pressure being forced downward
into the battlefield.

Frame 1 begins as a faint dark-red ring; frames 2 and 3 contract inward while a
few tiny stones lift; frame 4 compresses into one bright orange fracture point;
frame 5 sends a short line of energy out along the ground; frame 6 disappears
almost completely. The motion is inward and downward, never a large aura or an
upward explosion. Keep it small enough to work beneath characters of different
sizes and silhouettes.

Use black basalt fragments, dark iron red, restrained molten orange, and a tiny
yellow-white core. True transparent background with clean authored alpha.
No character, body parts, clothing, weapon, text, symbol, rune alphabet, UI,
scenery, frame labels, borders, or grid lines. Fixed camera, identical ground
contact, scale, and registration in every cell.
```

## Prompt 5 — Spellbook/icon art

### Dependency

The current skill content does not expose a dedicated spell icon path. Generate this only after its actual UI destination, crop, and resolution are specified. Otherwise it becomes attractive unused art.

### Prompt

```text
Create one square ability icon for the global spell Cinderfault. Show a single
jagged black fault splitting open from lower left to upper right, with molten
orange lava visible inside and three small basalt plates lifting from the
fracture. The silhouette must remain recognizable at very small UI size.

Dark theatrical fantasy, painterly hand-illustrated texture, bold simple shape,
high local contrast, near-black basalt, iron red, molten orange, and one tiny
yellow-white heat core. Earth dominates the icon; fire is contained within the
crack. Center the symbol with generous safe margin.

No character, hand, staff, landscape, full eruption, book mockup, lettering,
spell name, numbers, interface frame, circular badge, decorative border, cast
shadow, or multiple icon variants. Place the isolated icon artwork on a flat
pure #00FF00 background with no green anywhere in the icon itself for clean
keying.
```

## Prompt 6 — Pressure/release audio

### Runtime job

A short cue beginning with the cast and ending at or just before impact. It creates pressure without stealing the impact transient.

### Prompt

```text
Create a short fantasy game spell-casting sound for Cinderfault, approximately
0.45 to 0.65 seconds. Begin with a restrained subterranean stone grind and a
few close pebble movements. Build into a tightening low tectonic rumble as
pressure travels through the ground. End with a brief narrowing or suction of
energy that leaves space for a separate impact sound immediately afterward.

The material is heavy basalt under stress with molten heat beneath it. Dark,
tactile, and threatening, but quieter than the impact. No explosion, no impact
crash, no thunderclap, no musical melody, no voice, no chanting, no stock magic
sparkle, no long reverb, no persistent loop, and no sub-bass tail that masks the
next cue. Clean stereo game-ready effect with a dry, precise ending.
```

## Prompt 7 — Rupture/impact audio

### Runtime job

One shared impact cue fired exactly when all targets receive damage.

### Prompt

```text
Create a short fantasy combat impact sound for the spell Cinderfault,
approximately 0.35 to 0.55 seconds. The first instant must be a sharp, readable
fracture of thick stone, immediately reinforced by one heavy low-frequency
tectonic body. Add a compact burst of molten pressure and several heavy basalt
fragments falling back to ground. Finish with a very short restrained lava hiss
and dust tail.

It should sound like the battlefield splitting upward beneath three enemies at
the same moment: one unified rupture, not three sequential explosions. Heavy
rather than merely loud. No thunder, firearm, bomb, metallic sword hit, glass
shatter, voice, chant, melody, huge cinematic boom, long earthquake rumble, or
reverb tail that masks the next combat action. Put the strongest transient at
the very beginning so it can align exactly with the gameplay impact event.
```

## Correction-prompt workflow

Do not regenerate the entire package when one layer fails. Attach the latest source and request a tightly scoped edit.

### If the effect looks like fire rather than earth

```text
Revise this exact sprite sheet while preserving its grid, timing, registration,
and transparent background. Increase the visual mass and silhouette of black
basalt plates and heavy rock fragments. Confine the molten orange and yellow
light inside cracks and beneath the stones. Reduce free-floating flame shapes
and eliminate fireball/explosion language. The result must read first as earth
fracturing and second as lava revealed within it.
```

### If three targets look like three separate casts

```text
Revise the shared ground-fault sheet while preserving the approved palette,
grid, frame count, and registration. Make one continuous entering fault branch
organically into all three impact positions before they rupture. The branches
must visibly share one origin and one connected crack network. Remove isolated
circles, separate sigils, and disconnected pools beneath individual targets.
```

### If the impact hides combatants

```text
Revise the eruption sheet while preserving timing and ground contact. Narrow
the bright lava column, lower the dust opacity, and reduce the number of large
foreground fragments. Keep the peak forceful in the lower half of the frame,
but preserve clear negative space around the target's torso, head, and damage
number. Do not reduce the sharpness of the contact frame.
```

### If the sequence boils or changes design between frames

```text
Revise this six-frame animation for strict temporal consistency. Preserve one
fixed camera, one ground contact, one scale, and the same underlying fault and
rock shapes across all cells. Each frame must advance the previous state rather
than redraw it. Keep basalt plate identities and crack branches spatially
consistent as they lift, break, fall, and cool. Remove newly appearing unrelated
rocks and eliminate camera or perspective drift.
```

### If the aftermath implies ongoing damage

```text
Revise only the final two cells. Make the lava collapse and darken rapidly;
close the fault into black and iron-red seams; remove the pool, sustained flame,
and active vent. Leave only a few falling stones, dim embers, dust, and a brief
heat haze. The battlefield must clearly return to a safe neutral state.
```

### If the impact sound is too cinematic or fatiguing

```text
Revise this exact impact sound while preserving the immediate first-frame stone
fracture. Shorten the low-frequency decay, reduce the cinematic boom and reverb,
bring forward the texture of heavy rock, and end the lava hiss sooner. It must
remain satisfying when played repeatedly in normal combat and leave space for
music, voice, and the next action.
```

## Integration and QA order

1. Save raw visual sources under `Assets/_Project/Art/Sheets/` with stable names.
2. Preserve authored alpha or record the exact green-key preprocessing step.
3. Add each reproducible sheet to `tools/slice_spell_sheet.py`'s `VFX` manifest.
4. Slice frames into `Assets/_Project/Resources/Spells/<effect-id>/f0..fN.png`.
5. Run the slicer's preview and inspect the contact sheet.
6. Verify no blank mid-sequence frames, drawn borders, grid contamination, or cropped debris.
7. Ensure every runtime PNG imports as a Sprite and commit its `.meta` with it.
8. Composite the shared ground effect behind actors and target eruptions at occupied ground points.
9. Synchronize damage, per-target numbers, impact visual, impact audio, target deformation, and stage impulse.
10. Measure and normalize both audio files through the project's existing audio-level workflow.
11. Capture one-, two-, and three-enemy casts; tall and wide enemies; player and enemy-side mirroring if enemies may cast it.
12. Repeat Cinderfault several times in a full fight to test fatigue and pacing.
13. Test interruption/scene exit and verify every effect returns to its pool.
14. Record the final prompt, source, preprocessing, slicer manifest, timing, and content block in the spell's provenance note.

## Final acceptance questions

- Does one connected fault visibly cause all impacts?
- Do all occupied targets appear to be struck simultaneously?
- Can the player see each enemy and each result?
- Does the effect read as tectonic earth containing lava, not generic fire?
- Does the impact sound occur at the exact perceived rupture?
- Does the spell cool away without promising a status it does not apply?
- Does it remain satisfying rather than exhausting across repeated use?
- Does reduced shake or brightness still leave the impact readable?
- Are the raw sheets reproducible through the committed pipeline?

If any answer is no, use the correction prompt for the failing layer before adding more particles, brightness, shake, or audio volume.
