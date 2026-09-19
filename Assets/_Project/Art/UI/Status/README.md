# Status glyph set — 6 September 2026

Generated with the built-in image_gen tool from docs/STATUS_ICON_PROMPTS.md. Fourteen approved transparent 256 × 256 PNGs are in Processed/. All fourteen approved PNGs have also been copied to Assets/_Project/Resources/Status/ with the specified filenames. Chilled and Rooted images were replaced while preserving their existing .meta files. Runtime UI wiring and Unity import/render validation remain separate work.

## Files

- Raw/: fourteen selected original 1254 × 1254 masters (the tool returned this size despite the 1024-square request).
- Processed/: fourteen matching 256-square RGBA glyphs. No frames, text or counters are baked in.
- Review/16, 24, 32, 48/: downsized samples produced from the actual 256-square exports.
- Review/contact-sheet.png: actual-size comparison, greyscale, light background and illustrative badge frames.
- Review/fight-backdrop-check.png: 24px glyphs on the project's Fight background. This is an art QA composite, not an engine capture.
- Review/generation-manifest.json: exact prompts and original generation paths; selected masters are also saved locally.
- Review/verification.json: source mode, size, processing choice and export bounds.
- Review/Rejected/empowered-narrow.png: first sword rejected for insufficient visual weight.
- Review/export_icons.py: reproducible keying, normalization, alpha-aware resizing and QA sheets. Run from the project with Pillow installed.
- status-icons-256.zip: just the fourteen production PNGs.

## Small-size decisions and verification

Inspected the full set at 16, 24 and 32px, plus 24px greyscale and dark/light/backdrop compositions. The target is a 24px image region inside a 36px badge. At 16px, Rooted's fine root branches and the boar's facial detail soften; recognition relies on silhouette and learned association. Prefer 24px or larger rather than promising detail readability at arbitrary tiny sizes.

Empowered was regenerated with a shorter, broader blade and crossguard. Transparent edges are resized in premultiplied alpha. Tight content bounds are normalized to 88% of the output square's long axis, providing consistent optical scale and a modest transparent safety margin; this intentionally replaces the prompt's looser 76% master framing.

Thirteen selected masters have native alpha, which is preserved. Rooted uses a green background and was keyed at master resolution using tools/key_green_screen.py's key_out_green function before resizing. The original art and keyer are not modified. All output PNGs have nonempty opaque subject pixels and transparent corners.

## Integration notes

Use Processed/<slug>.png for the planned Status/<slug> resources. Preserve existing chilled/rooted .meta GUIDs when replacement is performed as part of UI integration. Render glyphs untinted; existing runtime tinting was designed for white silhouettes and needs the planned UI change. Supply the opaque backing and polarity frame in UI. Recheck bilinear sampling, compression, canvas scaling and counters in Unity; offline previews do not prove those settings.

The previous script-drawn Chilled and Rooted runtime PNGs have been replaced. Their generator and historical credits remain intact. This README records provenance of this new generated set.


