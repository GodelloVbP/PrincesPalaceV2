# Stone Golem — stance stills

Delivered id: `golem`. Content id and art folder agree.

> **The golem's source sheets do NOT live in this folder.** They sit directly
> in `Art/Enemies/` — `golem_sheet.png`, `golem_sheet_attack.png`,
> `golem_sheet_attack_rock.png`. This file is here because
> `docs/STANCE_SHEET_SPEC.md` puts an actor's record at
> `Art/Enemies/<actor>/README.md`, the same arrangement the rat has.

## Provenance

**Protected legacy — see `Art/Sheets/hand_assembled.json`'s `actors` block.**
There is no `recipe.json` here and there will not be one: the six stills were
picked out of frame sheets by hand in `48131f4` (attack and cast are both `f2`
of the attack sheet — the same slam, deliberately) and no recorded invocation
reproduces that choice. `HandAssembledArtTests` pins every one of them by
content hash, so a tool writing into `Resources/Enemies/golem/` fails the
suite whatever caused it.

## groundLine 69, and why it is the roster's one large override

`Resources/StanceManifest.json` authors `groundLine: 69` for this actor while
the art measures 64 — inside the 8px band, so it needs no exemption. The
number is worth explaining anyway, because it is the reason the manifest
exists at all: **the slam erupts an earth spike roughly 50px below the golem's
own feet**, so `attack.png` and `cast.png` measure a ground line of 20 against
the other four stances' 64. A scan for "the lowest opaque pixel" reads the
spike as the floor and hoists the whole figure off the ground.
`StanceManifestValidationTests` takes the MEDIAN across an actor's stances for
exactly this reason, which is what lets it measure the golem at all.

`groundLineSource` stays `authored`: this is a judgement about the art, and no
tool can reproduce the number because no tool produced the art.
