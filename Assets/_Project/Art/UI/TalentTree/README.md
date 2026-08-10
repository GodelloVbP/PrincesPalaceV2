# Talent tree kit art — SOURCES

These are the raw generations, still on their green screen. **Nothing here is
loaded by the game yet** — see the status note below before generating
anything. `tools/key_green_screen.py` cuts the green off them and writes the
real sprites to `Processed/<name>.png`.

    py tools/key_green_screen.py

## Naming

One file in, one keyed file of the same name out — no grouping, no animation
frames. Exact filenames expected:

    orb_lit.png
    orb_unlit.png
    branch_lit.png
    branch_unlit.png

`orb_*` are the node medallions (invested vs. locked/available), styled after
the glowing/dark orbs already on the Hub's own Talents building
(`Assets/_Project/Resources/Hub/talents/f0.png` — go look at that file before
generating; it's the exact reference this kit is matching). `branch_*` is a
single vertical strip of gnarled branch bark with a glowing (or dim) crack
running along it, meant to be stacked repeatedly to build a branch of any
length — **not** one fixed painting of a whole tree. See
`RELIC_AND_TALENT_TREE_ART_PROMPTS.md` at the repo root for the exact
copy-paste prompts and the reasoning for building it this way rather than as
one hand-painted tree per path.

## Sizes

- `orb_lit.png` / `orb_unlit.png` — square, 1024×1024, subject centred with
  roughly 10% padding.
- `branch_lit.png` / `branch_unlit.png` — 512×1536 (tall, not square), and
  **must tile seamlessly top-to-bottom** — the top few pixel rows need to
  match the bottom few, since the engine stacks copies end-to-end to build a
  branch of arbitrary length. Check this by placing two copies of the
  generated image directly above one another and looking for a seam.

None of these are resampled by the keyer — they pass through at whatever
resolution they're generated at, so generate at exactly the sizes above.

## Status: wired

This kit has been wired into `SceneBuilder.Talents.cs`/`TalentController`
since Phase 7 (2026-08-01): a 3-path × 21-tier scrollable tree, one shared
scroll across all 3 columns. A later full-bleed/tree-shape pass reused
`orb_lit`/`branch_tile_set` again for a root crest and three angled limbs
forking up to each path's row 0 (`BuildTalentRootAndLimbs` in
`SceneBuilder.Talents.cs`) — a dedicated `trunk_base.png`/`branch_fork.png`
would replace that placeholder cleanly, since it reuses the same
rotated-segment technique regardless of which texture it stretches across
the limb, but nothing is blocked on generating one.
