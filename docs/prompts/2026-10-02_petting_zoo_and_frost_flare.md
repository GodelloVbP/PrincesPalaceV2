# Petting Zoo and Frost Flare prompts

Designer prompts, 2026-10-02. Same shape as `output/event_art/PROMPTS.md`:
composition and invariants only. Stills only, no rigs or frame animation.

## Petting Zoo set piece

Target: `Assets/_Project/Art/Events/petting_zoo/zoo.png`, the `artPath` of the
`zoo` and `petter` pages of `petting_zoo` (`ContentData/events.json`). It sits
over the stage default backdrop, so it carries no sky, floor plane or scenery
edge to edge.

Giant_rat_sheet.png is the rendering reference ONLY: do not copy its subject or
palette. Uniform bold dark outlines, exactly 2-3 flat tones per colour area,
small palette, minimal internal texture. No painterly gradients, rim lighting,
glow or micro-texture.

1920x1080, solid #00FF00 background (keyed with `tools/key_green_screen.py`;
no green anywhere in the subject). No UI, text, frames, signage lettering or
watermark.

- One small wooden petting-zoo pen, low split-rail fence, gate at front right.
  Whole pen inside the central 65% of the canvas, broad margins.
- Inside: one ordinary woolly sheep chewing at the rail (not Shawn: no vest,
  strap or tan muzzle), one peacock with its tail fanned, two fawns nosing at
  an empty wooden trough.
- By the gate: a small cooler, beads of water on it, a few bottle necks showing.
- Animals side-on or three-quarter, readable silhouettes, no overlap that hides
  a head. Daylight, cheerful, no creature looking at camera.
- A blank wooden sign hung on the fence, no lettering: the text names it
  PETTING ZOO, and generated lettering is unreadable and cannot be localised.
- No characters, crowd or people. The fence crowd lives in the text.

Owner: confirm the flat-cel style. Every other event set piece (bell, caravan)
is painterly per `output/event_art/PROMPTS.md`; this one follows the lane brief.

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
- Cold one (`cold_one.png`): cooler lid thrown open, a few bottles taken out,
  two empty bottles tipped over on the grass.

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
