# Spell Books

Each named spell folder contains a 512x512 assembled book, a 48x48 assembled book, and a separate full-resolution transparent glyph. Spell titles match the authored names: Mud Burst, Frost Flare, Cinderfault, Lightning Bolt.

`Shared/Templates` contains matching solid/split neutral books with genuinely transparent center openings. `Shared/Masks` contains leather, both color regions, and opening masks. `Shared/Palette` contains all eleven damage-color variants and palette/slot coordinates. `Previews` contains comparison sheets with opaque backgrounds. `Source` holds generated originals and prompts.

The assembled books have opaque glyph backings inside their center; the reusable templates and palette bases leave that opening transparent. Exterior backgrounds are transparent in asset PNGs. Near-opaque generated interior alpha was normalized to 255; partially transparent silhouette edges remain antialiased.

Rebuild processing is in `output/spell-books/build.cjs`; the working package remains there. No game/UI references or texture import settings were changed. Unity will create importer metadata when these new assets are imported.
