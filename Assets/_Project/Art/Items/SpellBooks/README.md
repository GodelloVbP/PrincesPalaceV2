# Spell Books

Each named spell folder contains a 512x512 assembled book, a 48x48 assembled book, and a separate full-resolution transparent glyph. Spell titles match the authored names: Mud Burst, Frost Flare, Cinderfault, Lightning Bolt.

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

## Thirteen expansion books: typed placeholders, no glyph

The thirteen `bookOnly` expansion skills from `docs/PLAN_SPELL_EXPANSION.md`
milestone A had no `iconPath` at all, so their books drew nothing in the
dossier, shop and fight submenu. None of the thirteen has a commissioned
glyph. Each now gets a folder of its own (`<Display Name>/<Display Name> -
Spell Book.png` at 512px, plus the `- 48x48.png` export `skills.json`
actually points at) built by `tools/build_spellbook_bases.py`, which reads
the SAME full-resolution masks and templates `output/spell-books/build.cjs`
delivered here (`Shared/Templates`, `Shared/Masks`) and reproduces its own
`tint()` step in Python/Pillow/numpy -- leather tinted to the spell's
damage type(s), glyph opening left genuinely transparent. `build.cjs` itself
was not run: it requires the npm `sharp` package, which is not installed
anywhere under this tree. These are placeholders awaiting a commissioned
glyph, the same graceful-degradation posture a gear card with no art
already takes (ItemIcons.Apply hides the slot's mark rather than drawing a
blank square).

A dual-type spell (one, so far) uses the jagged-split cover, region A/B per
`docs/PLAN_SPELL_BOOK_ART.md`. Type was read off the spell's own
`damageInstances`/`detonationSplit` field in `skills.json` where one exists;
where none exists, the type below is a placeholder choice (Arcane), not
something declared anywhere in the spell's data -- flagged in the "declared?"
column for the owner to override if a different element reads better on the
finished glyph.

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

Open owner call: Velvet Shackles, Censer of Embers and Thorn Tithe each have
an obvious thematic element (Nature/Earth, Fire, Nature respectively) via
their applied status, but nothing in `skills.json` types them that way, so
this pass left them on the Arcane fallback rather than authoring an element
the content file doesn't declare. Retinting any of the eight Arcane
placeholders is a `tools/build_spellbook_bases.py` re-run away.
