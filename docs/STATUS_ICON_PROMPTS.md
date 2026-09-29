# Image prompts: status-effect glyphs

Based on the revised [status-effect UI plan](PLAN_STATUS_EFFECT_UI.md), sections 3–6. Seventeen complete standalone prompts: twelve statuses and two presentations of signed speed effects (prompts 1-14), then Fortified, Silenced and Disarmed for Bjorn's constellations (15-17; Silenced and Disarmed are engine windows on `CombatantState.Suppression`, shown as badges by `StatusHud.SilenceRow`/`DisarmRow`). No frames or Broken badge in this batch. These are prompts, not generated assets.

## Production contract

Copy the entire fenced block for one image. Generate the six-icon pilot first: Protect, Vulnerable, Shielded, Rooted, Speed Up and Speed Down. Compare at the actual 24px glyph size inside a 36px badge before committing to the remaining set.

The game supplies backing, deterministic polarity frame, counter, labels and tooltips. Do not bake them into these glyphs. Glyphs are untinted. Runtime polarity distinguishes a benefit from a detriment; speed_down is an asset for existing speed penalties, not a new status.

Green keying matches a range of green-dominant colours, not just #00FF00. These prompts avoid green subject colours, green leaves and cyan with excessive green. Reject generated deviations before processing. Keep Shielded opaque: its painted highlight conveys a ward without unreliable semi-transparency.

Keep raw 1024-square masters in proposed `Assets/_Project/Art/UI/Status/Raw/`. Key the master first, inspect its alpha, then resize to 256 square using alpha-aware filtering. The existing keyer has no Status kit; the later art task must use a scoped invocation or add a route. Do not assume its default command imports these files.

## Standalone prompts

### 1. Poison — `poison.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: A single broad teardrop of toxic liquid, pointed at the top, with a heavy rounded base. Sulfur yellow #D6C45A with ochre #927A35 shadow and one ivory highlight. No skull, face, bubbles or green pigmentation. The solid droplet outline carries recognition.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 2. Regen — `regen.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One plump heart with two broad lobes and a deep top notch. Rose #DC829D face, plum #87415F shadow, one pale pink highlight. No leaf, stem, cross, droplet or face. Preserve the unmistakable heart silhouette.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 3. Protect — `protect.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One whole kite shield viewed straight on, broad shoulders and a pointed bottom, with one broad central bevel. Steel lavender #A6A2B8 with slate #635F78 shadow and an ivory highlight. No emblem, boss, cracks or decorative rim. A solid uninterrupted shield silhouette, designed to pair with a split version.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 4. Vulnerable — `vulnerable.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One kite shield viewed straight on, split completely into two large halves by a wide jagged vertical gap. Offset one half slightly downward; keep both halves broad and readable. Rust #D1846D face, burgundy #844C54 shadow, pale peach highlight. Match the broad shoulders and pointed bottom of a simple whole kite shield. The background must be visible through the gap. No tiny cracks or emblem.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 5. Stun — `stun.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: A single compact dizziness symbol made of exactly two solid chunky five-point stars: one large star upper-left and one smaller star lower-right, separated by a clear gap. Gold #EDC663 with ochre #A47838 shadow and pale cream highlight. No orbit line, spiral, extra stars, sparkles or face. This deliberate two-shape symbol is the complete subject.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 6. Shielded — `shielded.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One round magical ward rendered as a SOLID opaque pale blue disc with one broad off-centre crescent highlight. Periwinkle #B4B5ED face, blue-violet #7179BE shadow, ivory highlight. The subject's circular silhouette is essential; it is not a badge frame. No actual translucency, nested rings, snowflake, shield, glow or fine reflections. Its centre must remain solid after background removal.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 7. Provoked — `provoked.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One stylised frontal wild-boar mask with a wide blunt snout and exactly two large ivory tusks rising at the sides. Muted crimson #CB7484 face and burgundy #783F57 shadow. Use broad angular masses and two simple dark eye slits, no fur texture or bristles. The tusks must dominate the silhouette. No skull, weapon, shield or extra ornaments.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 8. Empowered — `empowered.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One broad straight sword upright with its point at the top, a short thick horizontal crossguard and a simple thick grip. Amber #E9AC55 blade with ochre #9C6838 shadow and ivory highlight. The blade is wider than a realistic sword to remain readable when tiny. No floating energy strokes, flames, jewels or ornate hilt.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 9. Chilled — `chilled.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One bold six-armed snowflake, six equally spaced thick arms with one pair of short broad branches per arm. Ice lavender #BBC8F3 face, blue-violet #777EC2 shadow, ivory highlight. Keep generous open gaps between arms and no hairline detail. No surrounding disc, shield or extra flakes. The six-arm silhouette carries recognition.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 10. Rooted — `rooted.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One simple side-view boot pointing right, gripped by two very broad roots that curve around its ankle and spread downward. Muted tan #BD937C boot, russet #96634F roots, cream highlight. Roots visibly clamp the boot and are the main silhouette feature. No green moss, leaves, fine root tendrils, wing or arrow. Treat boot and roots as one compact composite symbol.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 11. Marked — `marked.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One target symbol built from exactly four thick inward-facing right-angle brackets around one large solid centre dot. Coral #E39B9E with burgundy #954E68 shadow and pale pink highlight. Keep all five parts clearly separated by background gaps. No enclosing circle, second ring, arrows, shield or star. This five-part symbol is the complete subject.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 12. Feared — `feared.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One tall screaming ghost mask with a rounded forehead and two broad tapered points along the bottom. Pale lilac #D6B9ED face, violet #9366B0 shadow and ivory highlight. Exactly two large dark eye holes and one very large vertical dark oval mouth; the mouth is the dominant internal feature. No teeth, skull nose, stars, spiral or wispy fine trails.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 13. Speed Up — `speed.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One simple side-view boot pointing right with one large stylised wing sweeping upward and backward from the heel. Pale blue #B5C1EF boot, blue-violet #8280C9 wing, ivory highlight. Use exactly three broad joined feather lobes; the wing is larger than the boot. No detached speed lines, arrow, roots or ankle bindings. Treat boot and wing as one compact composite symbol.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 14. Speed Down — `speed_down.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: One simple side-view boot pointing right beneath one broad downward-pointing arrow. The arrow's head sits just above the ankle with a clear gap; it must not obscure the boot. Muted mauve #C397BA boot, lilac #ABA0D4 arrow, plum #795779 shadow. No wing, roots, snowflake, detached motion lines or fine details. Boot and arrow form one compact composite symbol; this must differ visibly from the rooted boot.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 15. Fortified — `fortified.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: The top of a stone battlement: a short, thick wall section with three square merlons and two open square gaps between them, sitting on a solid block base. Slate blue-grey #7A8594 with deep slate #4B5361 shadow on the right faces and one ivory highlight along the top edge. No shield, no flag, no bricks drawn as individual lines - at most two broad horizontal mortar bands. The crenellated silhouette carries recognition.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 16. Silenced — `silenced.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: A closed spellbook seen at a slight angle, bound shut by one thick iron band with a heavy padlock at the front edge. Deep violet cover #5B3A7A with dark plum #3A2450 shadow, iron band and lock in steel grey #8A8FA0, one ivory highlight on the cover corner. No runes, letters, glow or sparks. The locked-shut book silhouette carries recognition.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

### 17. Disarmed — `disarmed.png`

```text
Create one square 1024 x 1024 fantasy combat HUD glyph. Flat cel-shaded graphic compatible with a painterly fantasy game, but with NO brush texture or fine painted detail. Design for a 24-pixel glyph inside a 36-pixel badge. Centre the subject in a consistent 76% safe box with clear margins. Use two broad subject tones and at most one broad highlight, plus a thick dark-plum #302036 silhouette outline. Important strokes and open gaps should survive at roughly two pixels at final glyph size. No gradients, glow, cast shadows, text, letters, numbers, UI container, decorative border or enclosing badge frame. Circular shapes are allowed only when the subject calls for them.

SUBJECT: A short broad sword snapped in two: the hilt and lower blade upright on the left, the broken upper blade tumbling away to the upper right with a clear open gap between the pieces. Steel grey #9AA0AE blade with slate #5E6472 shadow, brown #7A4E2E grip and crossguard, one ivory highlight on the blade. No blood, motion lines or sparks. The visible break and gap carry recognition.

Place the subject on a perfectly flat pure green #00FF00 field extending to all four edges, including all intended open gaps. Green is BACKGROUND ONLY. Keep every subject colour free of green dominance: green channel must not exceed the larger of red and blue. Do not add green reflections or colour spill. Paint the subject fully opaque; no translucency. Return exactly one image, not a contact sheet, mockup or alternate views.
```

## Delivery manifest

| File | Proposed runtime resource |
|---|---|
| poison.png | Status/poison |
| regen.png | Status/regen |
| protect.png | Status/protect |
| vulnerable.png | Status/vulnerable |
| stun.png | Status/stun |
| shielded.png | Status/shielded |
| provoked.png | Status/provoked |
| empowered.png | Status/empowered |
| chilled.png | Status/chilled |
| rooted.png | Status/rooted |
| marked.png | Status/marked |
| feared.png | Status/feared |
| speed.png | Status/speed |
| speed_down.png | Status/speed_down |
| fortified.png | Status/fortified (StatusEffectType.Fortified) |
| silenced.png | Status/silenced (StatusHud.SilenceSlug, a `Suppression` window, not a status) |
| disarmed.png | Status/disarmed (StatusHud.DisarmSlug, a `Suppression` window, not a status) |

Approved outputs go to `Assets/_Project/Resources/Status/`. Resource wiring, including speed variants and ordinary status IconKind fields, belongs to implementation; a file alone does not establish it.

Preserve existing chilled/rooted .meta GUIDs. The Status importer already handles Sprite/Single, pivot, transparency and mipmaps. Inspect filtering/compression and verify every resource loads as a Sprite. Preserve previous credits; add provenance for the new art rather than deleting history.

## Acceptance before delivery

- Fourteen files, consistent scale and margins, no text, frames or accidental extra objects.
- Inspect keyed masters and downsampled exports on dark and light grounds: no green fringe, erased subject interiors, opaque green gaps or rectangular background.
- Build a labelled contact sheet at 24px glyph / 36px composite badge, plus the actual compact-party variant. Include counters 1, 2 and a multi-digit value and both polarity frames.
- Compare Protect/Vulnerable/Shielded; Rooted/Speed Up/Speed Down; Stun/Chilled; Poison/Feared/Regen; Marked/Shielded. Confirm distinctions in greyscale and over fight backgrounds after brief legend training.
- If a pair fails, revise silhouette or spacing before increasing ornament or saturation. If the compact variant fails, adjust UI sizing instead of pretending full-size artwork proves readability.
- Record generation provenance and approved master/export paths. Do not retire the old generator until its consumers have been checked.

