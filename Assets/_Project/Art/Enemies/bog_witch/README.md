# Bog Witch — stance stills

Delivered id: `bog_witch`. Stances: idle, attack, cast, taunt, hurt, defeated.

> **The source sheet does NOT live in this folder.** It is
> `Art/Enemies/bog_witch_sheet.png`. This file is here because
> `docs/STANCE_SHEET_SPEC.md` puts an actor's record at
> `Art/Enemies/<actor>/README.md`.

## Provenance

**Protected legacy — see `Art/Sheets/hand_assembled.json`'s `actors` block.**
No `recipe.json`, and re-slicing the committed sheet would not produce this
art: `bog_witch_sheet.png` carries **baked captions, a background and floor
shadows**, all three now prohibited by `docs/STANCE_SHEET_SPEC.md` §1, and
whatever removed them was not this pipeline. The spec names this sheet
explicitly as one never to use as a style reference for the same reason.

Which cell became which stance was not recorded. `HandAssembledArtTests` pins
all six stills by content hash.

`groundLine` is `10.5` against a measured 8 — a 2.5px difference, inside the
8px band, and a fractional number because a real measurement can land between
two pixels.
