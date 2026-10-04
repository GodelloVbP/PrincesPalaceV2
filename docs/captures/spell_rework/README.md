# Spell rework capture coverage (PLAN_SPELL_REWORK S5)

Every section-3 capture scenario of `docs/PLAN_SPELL_REWORK.md`, per spell: the
"before" GIF (S0, `before/`), the "after" GIF (`after/baseline/`) and whether it was
recorded. Both sets are untracked (about 1.1 GB for after); regenerate with
`tools/gif.ps1 -Manifest spell_rework_before` and `-Manifest spell_rework_after`.

After was recorded 2026-10-04 in one boot: 43 of 46 scenarios passed. The censer rat
series and the two ally series failed because the squad killed the target before the
last cast, so they were re-run in two small follow-up boots at 4, 5 and 3 casts; the
manifest holds those final values. Each GIF was checked on a frame sampled at about
30% of its length; Burn's end and Viper's detonation were checked frame by frame.

Per-spell notes (what the cast does, beats, verdicts): the NOTES link under each heading.
The before-set notes: [before/NOTES.md](before/NOTES.md).

### 1. Court of Whispers

[NOTES](after/court_of_whispers/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| full 3-enemy rack, rats | `court_of_whispers_rat_full_1x.gif` | `court_of_whispers_rat_1x.gif` | recorded; Shawn's cost pulse is in this clip (verdict in NOTES) |
| full rack, treants | `court_of_whispers_treant_full_1x.gif` | `court_of_whispers_treant_1x.gif` | recorded |
| crowded mixed rack (rat, treant, beetle) | `court_of_whispers_rat-treant-beetle_full_1x.gif` | `court_of_whispers_rat-treant-beetle_1x.gif` | recorded |
| inside Bell in the Fog, round 1 (darkness 0.9) | `court_of_whispers_rat_full_dark0.9_1x.gif` | `court_of_whispers_rat_dark0.9_1x.gif` | recorded as `-Darkness 0.9`: near-black stage and the Round 1 counter only, not Bell's fog overlay or toll (see below) |
| five casts at 1.5x | `court_of_whispers_rat_full_x5_1.5x.gif` | `court_of_whispers_rat_x5_1.5x.gif` | recorded |
| 4:3 layout | none | `court_of_whispers_rat_4x3_1x.gif` | recorded; no before (the option postdates it) |

### 2. Thorn Tithe

[NOTES](after/thorn_tithe/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| rat | `thorn_tithe_rat_1x.gif` | `thorn_tithe_rat_1x.gif` | recorded |
| treant | `thorn_tithe_treant_1x.gif` | `thorn_tithe_treant_1x.gif` | recorded |
| five casts at 1.5x | none | `thorn_tithe_treant_x5_1.5x.gif` | recorded; no before |

### 3. Mud Burst

[NOTES](after/mud_burst/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| rat | `mud_burst_rat_1x.gif` | `mud_burst_rat_1x.gif` | recorded |
| treant | `mud_burst_treant_1x.gif` | `mud_burst_treant_1x.gif` | recorded |
| back-rank target (beetle, slot 3) | `mud_burst_rat-treant-beetle_target3_1x.gif` | `mud_burst_rat-treant-beetle_target3_1x.gif` | recorded |
| five casts at 1.5x | none | `mud_burst_treant_x5_1.5x.gif` | recorded; no before |

### 4. Gale Scythe

[NOTES](after/gale_scythe/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| full formation | `gale_scythe_rat_full_1x.gif` | `gale_scythe_rat_full_1x.gif` | recorded |
| two enemies (rat, treant) | `gale_scythe_rat-treant_1x.gif` | `gale_scythe_rat-treant_1x.gif` | recorded; the treant sits at the right edge of the stage crop, its cut is half out of frame |
| lone enemy | `gale_scythe_rat_1x.gif` | `gale_scythe_rat_lone_1x.gif` | recorded |
| five casts at 1.5x | none | `gale_scythe_rat_full_x5_1.5x.gif` | recorded; no before |

### 5. Velvet Shackles

[NOTES](after/velvet_shackles/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| rat | `velvet_shackles_rat_1x.gif` | `velvet_shackles_rat_1x.gif` | recorded |
| treant | `velvet_shackles_treant_1x.gif` | `velvet_shackles_treant_1x.gif` | recorded |
| inside Bell (darkness 0.9) | `velvet_shackles_rat_dark0.9_1x.gif` | `velvet_shackles_rat_dark0.9_1x.gif` | recorded as `-Darkness 0.9` (same limits as Court) |
| 4:3 layout | none | `velvet_shackles_rat_4x3_1x.gif` | recorded; no before |
| five casts at 1.5x | none | `velvet_shackles_treant_x5_1.5x.gif` | recorded; no before |

### 6. Viper's Bite

[NOTES](after/vipers_bite/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| no Poison | `vipers_bite_rat_1x.gif` | `vipers_bite_rat_1x.gif` | recorded |
| existing Poison (detonation) | `vipers_bite_rat_poison_1x.gif` | `vipers_bite_rat_poison_1x.gif` | recorded; the second number (-9) and the green pop show here and not in the plain clip. The before clip showed no detonation |
| existing Poison, treant | none | `vipers_bite_treant_poison_1x.gif` | recorded; no before |
| inside Bell (darkness 0.9) | `vipers_bite_rat_dark0.9_1x.gif` | `vipers_bite_rat_dark0.9_1x.gif` | recorded as `-Darkness 0.9` |
| five casts at 1.5x | none | `vipers_bite_treant_x5_1.5x.gif` | recorded; no before |

### 7. Ashen Reckoning

[NOTES](after/ashen_reckoning/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| with Poison present | `ashen_reckoning_rat_poison_1x.gif` | `ashen_reckoning_rat_poison_1x.gif` | recorded |
| with Poison, treant | none | `ashen_reckoning_treant_poison_1x.gif` | recorded; no before |
| with Poison, 1.5x | none | `ashen_reckoning_rat_poison_1.5x.gif` | recorded; one cast, no series (the Poison it consumes is gone after the first) |
| refusal without Poison | none | none | not a capture: the refusal is unchanged by the plan |

### 8. Gilded Aegis

[NOTES](after/gilded_aegis/NOTES.md)

Ward is a real cast on any ally (e4adbb39): the ward icon and `+25` bar on the target's plate and the log line "Shawn wraps <ally> in the fleece" show in the 4:3 clips (Odette, Bjorn checked at the cue). The 1x clips are stage-cropped and cannot show the HUD. On Shawn himself the ward is checked only by the stage picture.

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| on Odette | `gilded_aegis_party3_on_odette_1x.gif` | `gilded_aegis_on_odette_1x.gif` | recorded |
| on Bjorn | `gilded_aegis_party3_on_bjorn_1x.gif` | `gilded_aegis_on_bjorn_1x.gif` | recorded; Bjorn is cut at the left edge of the stage crop (the 4:3 clip shows him whole) |
| on Shawn | `gilded_aegis_party3_on_shawn_1x.gif` | `gilded_aegis_on_shawn_1x.gif` | recorded |
| 4:3 layout, on Bjorn | none | `gilded_aegis_on_bjorn_4x3_1x.gif` | recorded; no before |
| 4:3 layout, on Odette (HUD visible) | none | `gilded_aegis_on_odette_4x3_1x.gif` | recorded; no before; real cast, see note below |
| five casts at 1.5x, on Odette vs forest_warden | none | `gilded_aegis_on_odette_vs_forest_warden_full_x5_1.5x.gif` | recorded; no before |

### 9. Borrowed Moment

[NOTES](after/borrowed_moment/NOTES.md)

Hasten is a real cast on a non-caster ally that is not already next: the 4:3 Odette clip shows the log line "Odette moves 2 places earlier in the order" and the tracker reordering. On Bjorn (already next) and on Shawn (the caster is never legal) it stays presentation-only: effect on the body, no order change, no log line.

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| on Odette | `borrowed_moment_party3_on_odette_1x.gif` | `borrowed_moment_on_odette_1x.gif` | recorded |
| on Bjorn | `borrowed_moment_party3_on_bjorn_1x.gif` | `borrowed_moment_on_bjorn_1x.gif` | recorded; Bjorn is cut at the left edge of the stage crop (the 4:3 clip shows him whole) |
| on Shawn | `borrowed_moment_party3_on_shawn_1x.gif` | `borrowed_moment_on_shawn_1x.gif` | recorded |
| 4:3 layout, on Bjorn | none | `borrowed_moment_on_bjorn_4x3_1x.gif` | recorded; no before |
| 4:3 layout, on Odette (HUD visible) | none | `borrowed_moment_on_odette_4x3_1x.gif` | recorded; no before; real cast, see note below |
| three casts at 1.5x, on Odette vs forest_warden | none | `borrowed_moment_on_odette_vs_forest_warden_full_x3_1.5x.gif` | recorded; no before; three, not five: the squad kills the warden by cast 4 |

### 10. Censer of Embers

[NOTES](after/censer_of_embers/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| rat | `censer_of_embers_rat_1x.gif` | `censer_of_embers_rat_1x.gif` | recorded |
| treant | `censer_of_embers_treant_1x.gif` | `censer_of_embers_treant_1x.gif` | recorded |
| series at 1.5x, rat | none | `censer_of_embers_rat_x4_1.5x.gif` | recorded; no before; four casts: Burn kills the rat on cast 5 |
| five casts at 1.5x, treant | none | `censer_of_embers_treant_x5_1.5x.gif` | recorded; no before |

### 11. Burn coal glow

[NOTES](after/burn_coal_glow/NOTES.md)

| Section-3 scenario | Before | After | Status |
|---|---|---|---|
| glow starts (grey rat, Burn applied) | none | `status_burn_rat_1x.gif` | recorded; `status Burn`, 2 s |
| glow ends (Burn runs out) | none | `censer_of_embers_treant_held30s_1x.gif` | recorded; `-Seconds 30` plays the fight on, Shawn attacking each turn. Glow and Burn badge (2, then 1) stay to about frame 490 of 892, then the treant is back to bark brown |

## Not recordable, or recorded as a proxy

- **Bell in the Fog itself** (court, shackles, viper): `-Darkness 0.9` dresses the
  fight with the Round 1 counter and a near-black stage. Bell's fog overlay, its
  backdrop and the toll sound are not part of it. The spell is readable in all three
  clips; the darkness composition (`max(event, spell)`) is pinned by tests, not by GIF.
- **Hasten on Bjorn and on Shawn**: presentation-only by design (Bjorn is already next, Shawn is the caster). Ward on Shawn: not checked on the HUD.

## Flags from the frame check

- Bjorn is cut at the left edge of the stage crop in the 1x Aegis and Borrowed clips;
  the 4:3 clips show him whole. The two-enemy Gale clip cuts the treant at the right edge.
- Burn's glow is a smooth wash on the treant (already orange): readable as "hotter",
  low contrast; see `after/burn_coal_glow/NOTES.md`.
- Court mixed rack: the treant and beetle overlap, so two of the three cores sit on
  one another.

## Tools

`gif.ps1 spell <id> -Seconds <s>` plays the fight on after the cast until the film is
`<s>` seconds long, so a status's end can be filmed. At each player turn the caster
takes the plain attack (a repeat cast would refresh the status being filmed).
`gif.ps1` deletes `frames/` of every run but the newest two after each request
(`-KeepFrames` skips that; `-TrimFrames` runs it alone).
