# Petting Zoo and Frost Flare prompts

Designer prompts, 2026-10-02. Same shape as `output/event_art/PROMPTS.md`:
composition and invariants only. Stills only, no rigs or frame animation.

## Petting Zoo set piece

Target: `Assets/_Project/Art/Events/petting_zoo/zoo.png`, the `artPath` of the
`zoo` and `petter` pages of `petting_zoo` (`ContentData/events.json`). The
stage draws it at 1280x720 over the dark `Art/Backgrounds/Dungeon.png`, between
the speaker portraits and above the dialogue box.

Shared event style from `output/event_art/PROMPTS.md`: Hades-inspired painterly
fantasy, bold sculpted brush planes, warm subdued gold and cool teal shadows,
clean silhouettes. Not flat clip art, not a mobile-game diorama.

1920x1080, solid #00FF00 background, keyed with `tools/key_green_screen.py`.
No green or yellow-green anywhere in the subject, ground and shadows included.
No UI, text, frames, signage lettering or watermark.

- Camera at eye level, front-on, looking very slightly down: the fence runs
  left to right across the frame like a stage. Not isometric, not top-down.
- The pen stands on its own opaque patch of trampled dirt and loose straw,
  drawn edge to edge under the fence with soft-edged ends, so the keyed image
  has a floor. Ground shadows are dark brown on that dirt, never green.
- Whole group inside x 420..1500, y 220..960. Nothing touches the canvas edge.
- Front rail: a blank weathered wooden sign hanging from it, no lettering.
- Behind the rail, left to right: a peacock, tail fanned, body blue, crest and
  eyespots blue, bronze and gold with no green; two spotted fawns nosing at an
  empty wooden trough; an ordinary woolly sheep with a dark face chewing at the
  rail (not Shawn: no vest, strap or tan muzzle).
- Front right, by a closed gate: a dented red cooler (the text says "a cooler of
  cold ones"), lid propped open, three brown glass beer bottles with crown caps
  in ice, beaded with cold. Not water bottles.
- Light: one warm lantern hung on a fence post, cool shadows falling away, as
  if the pen were set down inside a dark dungeon. Not daylight.
- No characters, crowd or people. The fence crowd lives in the text.

## Petting Zoo follow-on pieces

Same style, canvas and keying as the set piece. Each one preserves `zoo.png`'s
pen, fence, gate, sign and light, and changes only what is listed. Shawn and
the party never appear: the dialogue stage shows them as portraits.

- Sheep close-up (`step1.png`, both `step1_pair` and `step1_solo`): the same
  zoo sheep, large and centred, leaning its head over the rail toward camera,
  curious and calm. Pen reduced to the rail and a post or two behind it; the
  other animals out of frame.
- Peacock (`peacock.png`): the peacock front and centre, tail fanned to full
  width, chest out, smug. A scatter of small gold coins on the ground by the fence.
- Fawns (`fawns.png`): gate open, both fawns standing just outside it looking
  off the right edge of the canvas, as if following someone; trough emptied.
- Cold one (`cold_one.png`): the cooler half empty, two empty beer bottles
  tipped over on the dirt.

Wiring: a result shows on the choosing page, so it keeps `zoo.png`. These three
need each outcome to `goTo` its own one-line page with that `artPath`, which is
a content change in `events.json`, plus a scene rebuild. Steps 2-10 and
`post_arc` get prompts once their text exists.

## Frost Flare spell sheet

Replaces `Assets/_Project/Art/Sheets/frost_flare.png` (1536x1024, 3x2 cells).
The recipe `Art/Sheets/recipes/frost_flare.json` maps six drawings f0..f5 into
nine played frames; keep the cell count and order so the recipe still fits.

Intent: one bolt, half fire and half frost, matching the spell's 4 fire + 4 frost
damage. A strike, not a swirl: gathering, a bolt coming down, the ground taking
it, embers and frost shards.

1536x1024 solid-black atlas, uniform 3x2 cells with margins, no labels or
dividers. Bright core and luminous falloff for luminance alpha keying, no
scenery or floor.

- f0: small gathering of orange sparks and pale-blue frost motes high in the cell.
- f1: the gathering brighter and tighter, sparks and frost spiralling into one point.
- f2: a bolt dropping from the top edge, orange core wrapped in an ice-blue sheath.
- f3: contact: the bolt fully down, a hard white-hot flash where it lands (impact
  sits low-centre, around 18% up from the cell bottom).
- f4: burst outward at the impact point, flame tongues and ice shards splitting apart.
- f5: fading embers and drifting frost crystals, mostly dark.

Owner: what to change from the current sheet: ______

## Frost Flare sound

Replaces `Assets/_Project/Resources/Audio/Sfx/frost_flare.mp3` (1.10 s today).
No sound-prompt precedent exists, so this is a brief, not a generator prompt.

Beat timing comes from the skill's `vfx` (`seconds` 0.52, nine frames,
`impactFrame` 5): one frame is about 58 ms, contact lands about 0.29 s after
the VFX starts.

- Duration: 1.0-1.3 s total, tail included.
- Layers: a rising crackle of fire with a glassy, icy shimmer on top (startup);
  a short downward whoosh (travel); a heavy impact with a hiss of steam and an ice
  crack (contact); a soft sizzle and tinkling shards (tail).
- Sync points from the first sample: startup 0-0.17 s, travel 0.17-0.29 s,
  contact transient at 0.29 s (the loudest point, sharp onset), tail to the end.
- Mono or narrow stereo, peaks at or under -1 dBFS; playback gain is set by
  `tools/measure_audio_levels.py`, so do not pre-compress it flat.

Owner: what to change from the current sound: ______

## Blackglass Spear rift frame

Replaces the stand-in `blackglass_spear_rift` (a mirror-splinter cell from the
spear atlas, which reads as a second spear lying in the target). The `rift`
layer in `skills.json` stretches this one frame from launch point to impact,
along the flight (`align: flight`, docs/ART_PIPELINE.md §5b), so draw it
horizontal and let the engine turn it.

The spear flies through its target and off-screen (`through: true`), so the
rift spans launch point to screen edge: draw it long and thin.

Painterly spell style matching the Blackglass Spear atlas. 1536x1024, solid
#00FF00 background, keyed with `tools/key_green_screen.py`, not luminance: the
rift's dark interior must stay opaque. No green anywhere in the subject.

- One horizontal tear across the full canvas width, centred vertically, about
  180 px tall at its widest, tapering to hairline points at the left and right
  edges; nothing touches the top or bottom edge.
- Inside: a near-black void with a faint deep-violet depth, no stars or scenery.
- Edges: ragged, torn like cloth or cracked glass, lit by a thin hot violet-white
  rim fading outward into soft purple glow.
- A few small black glass splinters drifting off the edges.
- Reads as a wound in the air, not as a crystal, blade or solid object.

Then re-slice with the existing `Art/Sheets/recipes/blackglass_spear_rift.json`
(drop its `turn` step, since this frame is already horizontal).
