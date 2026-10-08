# Spell VFX arc audit — 2026-09-25

Scope: the 17 `bookOnly` spells in `ContentData/skills.json`. This is a visual
and timing audit of their shipped source sheets, sliced frames, and authored
VFX layers. It does not change gameplay or the delivered VFX.

## What a complete cast needs

1. **Gather:** visible energy, material, or an object approaches a decisive
   state. The first frame may establish a shape, but the first several frames
   must change its energy or tension rather than merely hold it.
2. **Resolve:** one unmistakable event at the authored hit cue. For a control or
   status spell, the cast lands and hands its continuing effect to the status
   indicator; the cast animation itself still finishes.
3. **Clear:** after the hit, the main form breaks down or withdraws. The final
   two frames lose the main silhouette and stop introducing large fresh
   droplets, leaves, shards, or sparks. A small residual glow or stain can
   remain only if the next frame and layer timing explain how it leaves.
   Separately spawned particles must finish in the same visual cadence.

Review at the actual target size and in motion. A transparent last frame alone
does not prove a good exit: the previous frame and the particle layer can
still look like a continuing spell. Conversely, a status effect does not need
the cast VFX to remain on the target for its full duration.

`SpellFrameCursor` plays the last frame through `EndSeconds`, then fades that
unchanged frame over `FadeSeconds`. Thus `fade: 0.14` does not make the sheet
*draw* a dissolve; it only fades the final drawing after the sequence ends.
An emitter with only `burst` emits once at hit, but its particles remain until
their individual `lifeMax`. These are two independent tails.

## Per-spell assessment

`Good` means the existing material has a readable three-beat arc on the sheet
and no obvious long terminal hold. `Review` means a credible arc with a weak
start or exit. `Rework` means the exit fails the above contract or is held long
after its event. This is an art-direction assessment; runtime previews remain
the acceptance gate.

| Spell | Gather | Resolve | Clear | Call and instruction |
| --- | --- | --- | --- | --- |
| Mud Burst | Review | Good | Rework | The projectile is already formed at frame 0; the impact at frame 13 reads. Frames 19–25 retain a broad debris cloud, and the legacy block has no authored fade. Make the last third collapse toward small falling earth flecks and test the cut after frame 25. |
| Frost Flare | Good | Good | Review | A small spark builds into the strike; the last frame retains a bright ground ember. Let the ember contract/dim over the last two frames rather than ending as a distinct new flare. |
| Cinderfault | Good | Good | Review | Ground fissure and eruption align. Both nine-frame layers end on visible cracks/rocks with no authored fade. Close the eruption first, then let the remaining fissure cool to a thin dark scar before clearing. |
| Lightning Bolt | Good | Good | Review | The spark becomes a bolt and impact; frame 8 is still a bright ground strike. Shrink and dim the contact mark in the final frames so it does not pop off. |
| Gilded Aegis | Good | Good | Good | Fragments assemble into a shield and break into smaller light. Keep the ward's ongoing state in its status UI; the cast VFX can clear. Confirm small-target playback. |
| Winter's Rebuke | Good | Good | Rework | The final frost-cloud frame is selected as a `still` tear and held for 0.85 s after hit, then faded for 0.45 s. This is a long frozen terminal image. Give the tear a short animated collapse to cold mist or a much shorter hold/fade; inspect particles against its new endpoint. |
| Viper's Bite | Good | Good | Rework | Jaws snap decisively, but frames 8–11 keep full green curls and bright hanging drops. A 12-particle hit burst remains independently. Replace the last four main frames with a downward draining, thinning venom trail ending near empty; stop adding new droplets in those frames. Retune the hit burst only after the sheet reads as resolved. |
| Crownfall | Good | Good | Good | Crown gathers, falls, and shatters into progressively fewer shards. The shatter layer and burst are short. Confirm the tail on a small target. |
| Ashen Reckoning | Good | Good | Good | Poison gathers into an ember impact; smoke and fragments taper substantially. Check that the 0.62 s hit burst does not visually restart the plume after the main form clears. |
| Blackglass Spear | Good | Good | Rework | Spear and contact are clear. The terminal void vortex remains a `still` for 0.85 s after hit plus a 0.45 s fade; the last source frame is still dense. Replace that hold with a brief contracting tear that seals, or provide a near-empty final tear still and shorten the hold. |
| Borrowed Moment | Review | Good | Review | The hourglass is already complete in frame 0, though energy builds around it. The last frame still reads as a smaller hourglass/time spiral, and the particle burst lives 0.70 s after hit. Give the opening a clearer acceleration and the end a single spent grain/light point. |
| Gale Scythe | Good | Good | Good | Wind blade grows, cuts, then loses almost all its body by the last frame. Particle life is shorter than the sprite's full fade. Confirm at formation scale. |
| Palace Passage | Good | Good | Rework | Door outlines build and link, but both are still bright in the final frame and the hit burst continues 0.65 s. Have the paired doors close or fold into the swapped allies, then vanish to one small glint; preserve the paired-door read through impact. |
| Velvet Shackles | Review | Good | Review | Ribbons appear early and tie at impact. The last frames leave isolated petals and threads, with a 0.65 s particle tail. Make the bindings visibly cinch, then withdraw into the rooted-status handoff; avoid fresh flying scraps at the cut. |
| Censer of Embers | Review | Good | Review | The censer is fully visible immediately, then the flame erupts. The final smoke is thin but embers remain and the burst lives 0.70 s after hit. Have the censer prime first and let the cast embers die into the Burn status, without a new flare near the end. |
| Thorn Tithe | Good | Good | Rework | Roots form and clamp, but frames 8–11 replace the ring with suspended leaves, bark, and bright sap beads; frame 11 still has separate intact pieces. A 13-particle hit burst adds another tail. Settle the roots into the target, then take leaves/sap downward and out; finish with no newly suspended droplets. |
| Court of Whispers | Good | Good | Review | The court gathers and thins to wisps. The hit burst lasts up to 0.80 s, outliving the 0.72 s sheet and its 0.14 s fade. Check whether the late particles resemble a new whisper; shorten or soften them if so. |

## Commission and implementation order

1. **New terminal art first:** Viper's Bite, Thorn Tithe, Palace Passage. Keep
   each spell's first eight frames and the same canvas/anchor. Replace only
   frames 9–12 of the 4×4 source atlas; cells 13–16 remain separate particle
   sprites. Deliver a new versioned source sheet, then slice with the existing
   recipe into a scratch output directory for comparison. Do not hand-edit the
   shipped `Resources/Spells/` frames.
2. **Terminal hold and art together:** Winter's Rebuke and Blackglass Spear.
   Their `tear` layers hold source frame 12 as a still. Supply a short multi-
   frame contracting/closing tear (or a truly spent final still) and author its
   lifetime/fade so it does not hang over the target after the event. Preserve
   the amass, spear, and hit timing while assessing this change.
3. **Legacy finishing pass:** Mud Burst, then Cinderfault, Frost Flare, and
   Lightning Bolt. Their old VFX blocks or short layered sequences need an
   exit checked at playback speed; do not solve a terminal drawing solely by
   adding a long fade.
4. **Review group:** Borrowed Moment, Velvet Shackles, Censer of Embers, Court
   of Whispers. Keep a spell only if the last visible object/particle is
   declining in size, opacity, and activity. Gilded Aegis, Crownfall, Ashen
   Reckoning, and Gale Scythe require confirmation, not new sheets on current
   evidence.

For a replacement atlas, keep 4 columns × 4 rows, first 12 cells as one
fixed-anchor sequence, last four as **independent** reusable particle sprites,
genuine alpha, and generous cell gutters. Do not put a burst of new particles
in frame 12. Frame 10 should plainly be smaller/quieter than frame 9, frame 11
quieter than frame 10, and frame 12 near-empty at game scale. Preserve the
impact in cell 6 unless the authored hit cue is revised with the art.

## Acceptance evidence

- Show actual frame exports for start, just before hit, hit, frames 9–12, and
  the first empty frame; check alignment, alpha, and sprite dimensions.
- Use a live or captured playback at both a small enemy and a large one. View
  the whole cast at normal speed, then step through its final 0.3 s. The
  targeted spell must settle before the fight returns attention to the next
  action; no fully formed object or newly flying debris may be present at cut.
- Check `impact`, `tail`, and `after` previews plus the separately emitted
  particles. A static screenshot is supporting evidence, not a substitute for
  watching the transition.
- Keep source sheet, slice recipe, emitted PNGs, and their Unity `.meta` files
  together; use the art-pipeline recipe replay and the repository's
  change-class verification gate before calling an implemented change done.

Review sheets generated for this audit:
`output/spell-vfx-audit/expansion-arc-1.png`, `expansion-arc-2.png`,
`expansion-tail-1.png`, `expansion-tail-2.png`, and `legacy-arc.png`.
