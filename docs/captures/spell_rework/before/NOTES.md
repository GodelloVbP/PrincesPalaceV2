# Spell rework, "before" GIFs (S0)

Recorded 2026-10-03 with `tools/gif.ps1 -Manifest spell_rework_before` (21
scenarios, one boot, all passed; run `20261003T220236Z_12461d1ce5fc`). 1920x1080
canvas, stage crop, 60 fps captured / 30 fps shown, Shawn vs the named enemy at
1x. Names are `<spell>_<enemy>[_full][_<prestatus>][_dark<n>][_x<casts>]_<speed>x.gif`.
I looked at the hit-cue frame of every scenario and at twelve samples of the
repeat series; the lines say what is on screen, not whether it is good.

The GIFs are 222 MB together (the repeat series alone is 89 MB), so they are
not committed; regenerate with the manifest.

Not recordable by the runner today (listed in the manifest's `notCovered`):
Court's crowded mixed formation, Mud Burst's back-rank target, Gale Scythe's
two-enemy formation, Aegis and Borrowed on Odette or Bjorn (the cast is aimed
at the caster, and the party is Shawn alone).

| GIF | What it shows |
|---|---|
| court_of_whispers_rat_full_1x | Violet hooded wisps rise around each of the three rats with a glowing core at the front one; Fear badges; log "recoils from the whispering court", then Shawn is afflicted with Vulnerable. No pulse on Shawn seen on the sampled frame. |
| court_of_whispers_treant_full_1x | Same effect, scaled up, over three treants; wisps reach above the crowns, core ring at the foot. |
| court_of_whispers_rat_full_dark0.9_1x | Backdrop near black with a "Round 1" counter (the Bell-style darkness at 0.9); wisps, Shawn and the rats stay as bright as in the plain GIF, so the spell is readable but its own glow is the only light. |
| court_of_whispers_rat_full_x5_1.5x | 864 frames, five casts (hit cue at frame 12 of the first), a rat's lunge at Shawn between casts, idle stages in between; no jump at the joins. Costs and cooldown are waived between casts. |
| thorn_tithe_rat_1x | A ring of brambles with leaf flecks closes around the rat; Thorned badge 3. |
| thorn_tithe_treant_1x | The same thicket as a tall oval around the treant, taller than the body. |
| mud_burst_rat_1x | Brown glob spray crosses from the left and a cream splat cloud covers the rat; damage number on contact; no status badge visible on that frame. |
| mud_burst_treant_1x | Same splat, bigger; the spray fan is wider than the body. |
| gale_scythe_rat_full_1x | One white crescent cut per rat (three), leaf bits, damage numbers 7, 8 and 9 on the same frame. |
| gale_scythe_rat_1x | One crescent on a lone rat, number 8. |
| velvet_shackles_rat_1x | A crimson cuff with a gold chain closes round the rat, red spikes at the edge; Rooted badge 2. |
| velvet_shackles_treant_1x | The cuff spans the treant's whole trunk; the cuff is the largest thing on stage. |
| velvet_shackles_rat_dark0.9_1x | Same cuff over the near-black darkness; Round 1 counter; the cuff is the brightest thing in frame. |
| vipers_bite_rat_1x | Green swirl with two pale fangs closing on the rat, number 3, Poison badge. |
| vipers_bite_rat_poison_1x | Same bite on a rat already poisoned; the Poison tooltip strip shows at the bottom. No separate detonation on the hit frame I looked at. |
| vipers_bite_rat_dark0.9_1x | The bite over the darkness; the green swirl lights the rat. |
| ashen_reckoning_rat_poison_1x | Green poison vapour catches and turns orange fire on the rat; Shawn holds his staff down (not the raised-hand pose of the other casts) and the Move/Item menu rows are visible below him. |
| gilded_aegis_rat_1x | A gold shield ring spins up around Shawn (self cast; the rat is untouched and shows an attack intent). |
| borrowed_moment_rat_1x | A gold and cyan hourglass ring around Shawn, same self-cast layout as Aegis. |
| censer_of_embers_rat_1x | A smoke column with embers climbs from the rat's feet; Burn badge 3. |
| censer_of_embers_treant_1x | Same column over the treant, smoke reaching past the crown. |

## S0 tool checks

- 4:3: `court_of_whispers -Aspect 4:3` writes 1440x1080 frames (GIF 960x720). The
  full layout is there, scaled to fit (enemy plates, turn order, Shawn's panel),
  and nothing is cut off. A first run without fit scaling cropped the right rat
  and both HUD plates; `CanvasCapture.RenderToFile` now has an opt-in
  `fitReference` that the scenario runner uses.
- Darkness: `-Darkness 0.9` binds a round-limit presentation with a held
  opacity through `FightController.Bind`; `roundDarkness` itself is untouched.
  It shows the "Round 1" counter (HasDarkness needs a round limit) but not
  Bell's backdrop, fog overlay or toll sound.
- Mixed formation, ally and back-rank aims (closing the S0 gaps): `-Vs rat,treant,beetle`
  fields that rack in slot order (any list of up to 3); `-Party shawn,odette,bjorn`
  seats the real squad (caster first, list order is rank, front first);
  `-Target odette` / `-Target 3` aim the forced cast at an ally or at enemy slot 3
  through `FightController.ForceFirstActionTarget`. Recorded:
  `court_of_whispers_rat-treant-beetle_full_1x` (beetle sits behind the treant),
  `mud_burst_rat-treant-beetle_target3_1x` (burst lands on the beetle behind the treant),
  `gale_scythe_rat-treant_1x` (two enemies), `gilded_aegis_party3_on_{odette,bjorn,shawn}_1x`
  and `borrowed_moment_party3_on_{odette,bjorn,shawn}_1x`. Ward and Hasten are
  presentation-only (the preview stage refuses them), so the HUD shows Odette's
  turn menu and no ward/haste status appears; only the vfx on the chosen body is real.
  The stage crop cuts Bjorn at the left edge on the aegis-on-Bjorn shot.
