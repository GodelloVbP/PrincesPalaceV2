# Burn coal glow (PLAN_SPELL_REWORK 3.11)

Status visual `StageBurnGlow`: additive silhouette of the body (shader `UIBurnGlow`,
hot at the feet, a quarter at the crown), breathing on `FlickerCurve.Ember`.
On from the beat whose snapshot carries Burn, off on the beat it is gone or the body dies.

Files (untracked captures):
- `status_burn_rat.gif` / `_strip.png`: grey rat, frame 0 no Burn, glow rises over 0.3 s. Clearest read.
- `v2_additive_x3.gif`: Censer of Embers vs treant, 3 casts; Burn badge up for the whole clip.
- `v1_flat_wash_invisible.gif`: first attempt (flat alpha-blended orange, 0.34). Treant looked unchanged. Rejected.

Frame verdict (mine, owner gate skipped):
- Rat: reads as heat, warm light climbing from the feet. Good.
- Treant (already orange-brown): treant body red channel 76 before, 130-155 burning, breathing visible in
  the numbers; to the eye it is "brighter, hotter", low contrast. Does not fight the spell's own flames.
- It is a smooth wash, not coal texture or embers; no under-glow on the ground.
- Burn ending: filmed in `baseline/censer_of_embers_treant_held30s_1x.gif` (`-Seconds 30` plays the
  fight on): glow and badge (2, then 1) hold to about frame 490 of 892, then the treant is bark brown.
  Also pinned by PlayMode `BurnCoalGlowTests` (beat snapshot, idle refresh, death).
