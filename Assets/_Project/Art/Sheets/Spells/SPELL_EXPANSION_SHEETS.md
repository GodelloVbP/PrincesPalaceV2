# Spell expansion animation sheets

Thirteen generated source atlases, one folder per spell. Built-in image generation; no external API fallback. Each atlas requests a 4x4 layout with 12 main animation cells and 4 independent particle sprites, giving 156 animation cells and 52 reusable particle cells across the set.

These are source artwork deliveries, not runtime-integrated animations. Exact prompts and integration notes are in each spell's README. Existing game content and existing spell assets were not replaced.

Verified all 13 delivered PNGs: 1254 x 1254, 32-bit RGBA, genuine transparent pixels, and byte-identical copies of generated originals. Preferred 2048 size was not honored by the generator. Nominal grid crop edges: 0, 314, 627, 940, 1254; use explicit crop rectangles and inspect gutters during integration.

| Spell | Atlas |
|---|---|
| Borrowed Moment | [borrowed_moment_atlas_v1.png](<Borrowed Moment/borrowed_moment_atlas_v1.png>) |
| Palace Passage | [palace_passage_atlas_v1.png](<Palace Passage/palace_passage_atlas_v1.png>) |
| Gilded Aegis | [gilded_aegis_atlas_v1.png](<Gilded Aegis/gilded_aegis_atlas_v1.png>) |
| Court of Whispers | [court_of_whispers_atlas_v1.png](<Court of Whispers/court_of_whispers_atlas_v1.png>) |
| Crownfall | [crownfall_atlas_v1.png](<Crownfall/crownfall_atlas_v1.png>) |
| Gale Scythe | [gale_scythe_atlas_v1.png](<Gale Scythe/gale_scythe_atlas_v1.png>) |
| Blackglass Spear | [blackglass_spear_atlas_v1.png](<Blackglass Spear/blackglass_spear_atlas_v1.png>) |
| Ashen Reckoning | [ashen_reckoning_atlas_v1.png](<Ashen Reckoning/ashen_reckoning_atlas_v1.png>) |
| Winter's Rebuke | [winters_rebuke_atlas_v1.png](<Winter's Rebuke/winters_rebuke_atlas_v1.png>) |
| Velvet Shackles | [velvet_shackles_atlas_v1.png](<Velvet Shackles/velvet_shackles_atlas_v1.png>) |
| Censer of Embers | [censer_of_embers_atlas_v1.png](<Censer of Embers/censer_of_embers_atlas_v1.png>) |
| Thorn Tithe | [thorn_tithe_atlas_v1.png](<Thorn Tithe/thorn_tithe_atlas_v1.png>) |
| Viper's Bite | [vipers_bite_atlas_v1.png](<Viper's Bite/vipers_bite_atlas_v1.png>) |

Use the water spell's layer arrangement as the integration precedent: animate the main sheet, then emit the independent particle sprites with material-appropriate trajectories. Do not animate the particle row as frames 13-16 of the main spell. Review crop margins, temporal alignment, and impact timing during slicing and playback. Court's figures are spectral VFX, not summoned actors. Palace Passage's paired doors are composed in one cell and need independent door layers if their separation must follow arbitrary actor positions.
