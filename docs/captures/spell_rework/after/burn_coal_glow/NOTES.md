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
- Burn ending is NOT in any GIF: the scenario never outlasts Burn (still on at the last frame of the
  3-cast clip), and the tool cannot extend a spell run. Ending is pinned by PlayMode
  `BurnCoalGlowTests` (beat snapshot, idle refresh, death), not seen in frames.
