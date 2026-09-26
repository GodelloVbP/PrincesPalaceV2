# Spell Books

Each named spell folder contains a 512x512 assembled book, a 48x48 assembled book, and a separate full-resolution transparent glyph. The four original books are Mud Burst, Frost Flare, Cinderfault and Lightning Bolt; thirteen expansion books use the same shared cover kit.

`Shared/Templates` contains matching solid/split neutral books with genuinely transparent center openings. `Shared/Masks` contains leather, both color regions, and opening masks. `Shared/Palette` contains all eleven damage-color variants and palette/slot coordinates. `Previews` contains comparison sheets with opaque backgrounds. `Source` holds generated originals and prompts.

The assembled books have opaque glyph backings inside their center; the reusable templates and palette bases leave that opening transparent. Exterior backgrounds are transparent in asset PNGs. Near-opaque generated interior alpha was normalized to 255; partially transparent silhouette edges remain antialiased.

Rebuild processing is in `output/spell-books/build.cjs`; the working package remains there. No game/UI references or texture import settings were changed. Unity will create importer metadata when these new assets are imported.

## Repointed: the four delivered books now use the 48x48 export

`mud_burst`, `frost_flare`, `cinderfault` and `lightning_bolt` authored
`iconPath` at the 512px master. Menus draw the book at 44px (dossier) or
smaller (36px in the fight submenu's `CharacterSkill{i}Mark`) and Unity's
minification of a 512px source goes soft at that size (2026-09-07 UI-kit
finding: the softness is minification, not import settings). Each folder
already had a `- 48x48.png` sibling that nothing referenced; `skills.json`'s
four `iconPath` lines now point at it instead.

## Thirteen expansion books: distinct glyphs

The thirteen `bookOnly` expansion skills from `docs/PLAN_SPELL_EXPANSION.md`
milestone A each have a distinct generated glyph. Each spell folder holds the
original generated glyph, a 512px assembled book, and the 48px export that
`skills.json` points at. `tools/build_spellbook_bases.py` reads the shared
full-resolution templates and masks, tints the leather to the spell's damage
type, and places the glyph over a dark backing in the center opening. The
shared templates retain their genuine transparent opening. The reproducible
build uses Python/Pillow/numpy; `output/spell-books/build.cjs` remains the
original four-book package.

A dual-type spell (one, so far) uses the jagged-split cover, region A/B per
`docs/PLAN_SPELL_BOOK_ART.md`. Type was read off the spell's own
`damageInstances`/`detonationSplit` field in `skills.json` where one exists;
where none exists, the cover uses Arcane as a visual fallback, not as a
declared damage type. The glyphs follow each spell's name and effect rather
than implying a new gameplay element. Compare all thirteen at the actual
48px size in `output/spell-books/preview-expansion-48.png`.

| Spell | Type(s) | Declared? |
| --- | --- | --- |
| Gilded Aegis | Arcane | No damage/element field on this skill; Arcane is the fallback, not a themed pick |
| Winter's Rebuke | Ice | `damageInstances` type `Frost` (schema alias for `Ice`) |
| Viper's Bite | Poison | `damageInstances` type `Poison` |
| Crownfall | Arcane | `damageInstances` type `Arcane` |
| Ashen Reckoning | Poison + Fire (split) | `detonationSplit` `["Poison","Fire"]` |
| Blackglass Spear | Void | `damageInstances` type `Void` |
| Borrowed Moment | Arcane | No damage/element field; fallback |
| Gale Scythe | Wind | `damageInstances` type `Wind` |
| Palace Passage | Arcane | No damage/element field; fallback |
| Velvet Shackles | Arcane | No damage/element field; `appliesStatus: Rooted` reads Nature/Earth thematically but isn't a typed field, so left at the fallback rather than invented |
| Censer of Embers | Arcane | No damage/element field; `appliesStatus: Burn` reads Fire thematically but isn't a typed field, so left at the fallback rather than invented |
| Thorn Tithe | Arcane | No damage/element field; `appliesStatus: Thorned` reads Nature thematically but isn't a typed field, so left at the fallback rather than invented |
| Court of Whispers | Arcane | No damage/element field; fallback |

Velvet Shackles, Censer of Embers and Thorn Tithe retain Arcane covers because
their skill rows do not declare a damage type. Their glyphs show shackles,
embers and thorns respectively. Changing a cover palette is a one-row update
in `tools/build_spellbook_bases.py` followed by a rebuild.
