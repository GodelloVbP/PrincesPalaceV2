# Proposal: a standard workflow for making spell animations

Status: rev 2, 2026-10-05, for independent review. Nothing in it is built yet.
Rev 2 answers the first review (section 11). In short:
- art quality comes first;
- the eye comes before the instruments;
- tooling comes in only when a trial proves the need;
- concurrency and closing follow the repository's own rules.

## 1. Context for a reader new to the project

- **The game.** Prince's Palace is a turn-based roguelike in Unity 6 (2D, URP).
  The simulation resolves each fight first. Then a presentation layer plays
  every action back as a short animated "beat".
- **A spell's look.** It is data: a list of **layers** in
  `ContentData/skills.json`. Each layer is a drawing, a frame sheet or a
  particle emitter, with timing and motion fields: rotate around a pivot, fade
  in, flicker, drift, glow over its life, stage gloom, a stage kick.
- **The engine.** A runtime player draws the layers. A generator turns the JSON
  into game assets.
- **Art.** The owner makes it with ChatGPT from written prompts, in the fight's
  flat cel style (the Giant Rat enemy sheet is the reference), on a green
  screen. Recipes key out the green and cut sheets into frames.
- **Who works.**
  - The owner: a solo developer, the final judge of how things look and sound.
  - An orchestrating AI session: briefs, reviews, talks to the owner.
  - AI implementer agents: edit, test, capture.
  - The repository allows **at most three active agents** and never two owners
    on one file (`docs/WORKFLOW.md`).
- **Evidence tools.** A GIF recorder plays a scripted fight headless and writes
  a GIF plus `timing.json`. Commit gates follow `docs/TESTING.md`: the changed
  gate, which promotes itself to a full run when the change reaches far
  enough or the last green full run is more than 5 commits or 24 h old.
- **The standard a spell must meet.** `docs/SPELL_FEEL.md`. Every spell has a
  **verb** (what it does to reality), a **signature beat** (the one thing only
  it does), and a **"must not read as"** line.

## 2. The problem, measured

**First attempt, ten spells (2026-10-03/04).**
- Agents graded their own GIFs and the orchestrator relayed the grades without
  watching.
- Owner: "everything is just one art sheet, or at least looks like it".
- Six of ten were plainly off. One verb changed by accident: a *bite* became a
  snake head thrown across the stage.

**Pilot, Censer of Embers: 7 rounds, about 14 h wall clock** (`7d2841a7` to
`3ae5344e`). Approved by the owner.

| Round | Defect caught | Root cause |
|---|---|---|
| 1 | swing too fast, burst is one drawing, fire static | first blocking |
| 2 | censer hung off-screen | orchestrator's brief |
| 3 | swing "static, not like a real swing" | orchestrator's brief: pivot at the art's top, an invented "pause at the ends" |
| 4 | twitch at the start, jump at the break | amplitude grown from near zero; a scale punch about a far pivot |
| 5 | light pool reads as a stain, afterimages murky, green specks, a jump returns | polish checked by the agent only |
| 6-7 | afterimages still weak; owner wants no full-body hit flash | owner taste |

Other friction:
- GIFs of 17-26 MB failed to reach the owner's phone.
- One owner question was asked four times.
- The first art batch came back in a painterly style that clashed with the
  fight's cel art. A style rule now exists, but nothing enforces it.
- Shared-tree noise: line-ending phantom changes, stale git locks, generated
  files left uncommitted.

**What actually cost the rounds:** briefs that specified motion wrongly; and
reviewing frames instead of the motion, so weight and rhythm were judged
late. Edit contention did not cost rounds: one spell was in flight at a time.

## 3. Goals and non-goals

**Goals:**
1. Strong, consistent spell art and motion, judged against a reference pack,
   in the fight's style.
2. Fewer owner corrections. Target: 3 owner reviews per spell (motion, art in
   context, close). This is a target, not a limit (section 9).
3. Fast: owner time under 2 minutes per review, answerable from a phone.

**Non-goals:**
- New animation technology: no rigs, no 3D.
- Changing the simulation.
- Automating taste.

## 4. Behavioural contracts

1. **Identity is locked before anything is made.** Each spell's sheet fixes
   these before blocking:
   - the verb;
   - the signature beat;
   - "must read as" and "must not read as";
   - one motion reference per moving element, described as physics or a
     real-world thing ("a censer on a long chain, released from the left"),
     never as invented parameters;
   - intentional exceptions (an instant appearance, a deliberate hold, a sharp
     snap), so later checks don't flag them.
2. **Watching means playback.** The orchestrator plays the capture at normal
   speed and at 1.5x, then checks selected stills: edges of the frame,
   composition at the hit, the last frame of the ending. It scores the result
   on the review rubric (section 6), never on "I would defend it". The new
   spell is also played beside the approved spells, for consistency and to
   check it is distinct from them.
3. **Nothing reaches the owner unwatched.** Every owner message names:
   - the rubric result;
   - what is still weak;
   - the reviewed source snapshot: the commit plus the capture run id.
4. **Instruments support the eye; they never replace it.** The check lists
   live in `docs/SPELL_FEEL.md`, "Checks: binding and advisory"; in short:
   - **Binding checks** (block a review):
     - transform discontinuities (jumps) in the spell's own layers;
     - cue timing: sound, number, badge and accent on one frame (from
       `timing.json` and the cue tests);
     - off-screen: every layer the beat sheet marks as "must be visible" stays
       inside the stage crop.
   - **Advisory checks** (reported, never blocking until validated on trial
     spells): pop-in, frozen holds, body coverage at the hit, flash guard. The
     flash guard is measured from rendered frames, because transforms can't
     show it.
5. **Art is reviewed for quality, not only usability.**
   - On arrival: key leak and pivot registration (usability).
   - Then an art contact sheet, beside the reference pack, at actual gameplay
     size, checked against the art rubric: silhouette, materials, proportions
     between cells, readability at size, style match.
   - A rejected piece gets a targeted revision prompt, not a regenerated batch.
6. **Decisions are durable.** Each spell keeps one state record (its NOTES
   file):
   - the identity sheet;
   - the accepted prompt, source image, processing recipe and outputs;
   - the open owner questions, each asked once;
   - the round log.

   Ownership across rounds rests on that record, not on one agent's context.
   An agent may be kept alive for speed, and is replaced from the record
   without loss.
7. **Concurrency follows the repository.**
   - At most three active agents, the orchestrator included, so **at most two
     workers at once**.
   - Up to three spells may be *in flight* (some waiting on the owner or on
     art).
   - Shared writes are serialized: one writer at a time for `skills.json`, the
     content build and the capture runner.
8. **Closing follows `docs/TESTING.md`, on one frozen snapshot.** The capture
   the owner approved, the generated assets and the gate run all describe one
   identified commit. Close with:
   - the changed gate, as TESTING.md defines it;
   - the generated assets committed;
   - the NOTES record updated.
9. **The engine grows only when a blocking proves it must.** Fields used by
   one spell are reviewed at batch close.

## 5. The loop for one spell

| # | Step | Who | Output | Exit |
|---|---|---|---|---|
| 1 | Lock identity and references | orchestrator, from SPELL_FEEL | identity sheet (contract 1) | the owner confirms or edits it in the same message as step 3's GIF |
| 2 | Block with existing assets | worker | stand-in motion; beat table; list of art needed | binding checks pass, or each failure is explained |
| 3 | Watch and approve the motion | orchestrator, then owner | rubric result; phone preview + beat table | owner: ✔ per beat, or ✘ + frame + what they see |
| 4 | Generate art | owner | images from the prompt template (section 7) | — |
| 5 | Inspect the art before wiring | orchestrator | usability report; art contact sheet against the art rubric | accepted, or a targeted revision prompt |
| 6 | Integrate and compare in context | worker, then orchestrator | wired art; polish from the menu (section 8); side-by-side with approved spells | motion rubric and art rubric pass; owner review |
| 7 | Verify and close | worker | contract 8 | snapshot id recorded |

Art requests are batched only for spells whose motion is approved (step 3):
independent, settled designs, never unresolved ones.

## 6. The review rubric (orchestrator, before the owner sees anything)

Built: `docs/SPELL_FEEL.md`, "Review rubric". In short: six motion items scored
at 1x and 1.5x (beats in order, physical weight, one focal point, body readable
at the hit, an ending that decays, distinct from and consistent with approved
spells) and five art items on a contact sheet at gameplay size (silhouette,
style match, proportions across cells, materials, nothing in "must not read
as"). Each item is ✔, ✘ (blocks) or ~ (named to the owner).

## 7. The art loop

- **The reference pack** (built: `docs/spell_art/REFERENCE.md`; built once,
  from what exists):
  - the Giant Rat sheet;
  - the owner-approved Censer pieces;
  - palette and material notes;
  - frames at actual gameplay size;
  - three rejected examples with why: painterly glow; a body-sized snake head;
    a slide instead of a closing cage.
- **The prompt template** (built: `docs/prompts/TEMPLATE_spell_art.md`):
  - the shared style block;
  - the reference images to attach;
  - per piece: filename, canvas, grid, pivot, the identity lines;
  - a delivery specification: folder, name, green.
- **Targeted revision:** a short prompt naming the one change ("same image, the
  vines' base on one line in every cell"), never a regeneration of the batch.
- **One record per piece:** accepted prompt, source image, recipe, output,
  stored in the spell's NOTES.

## 8. The polish menu

Built: `docs/SPELL_FEEL.md`, "Polish menu". In short: engine-made effects, each
with its layer fields and its cost, seeded from the pilot. Worked: lantern glow
over life, turn sparks, stage kick with cast layers exempt, glowing embers,
smoke, ash, body glow following the fire, light gloom. Failed: light pool (a
stain), afterimages (murky). Each close updates the menu.

## 9. Measures

Record for each trial spell:

| Measure | Why it is recorded |
|---|---|
| owner reviews | target 3 |
| internal retries | not hidden by the owner-review count |
| generation attempts per art piece | — |
| active work time and waiting time | where the hours actually go |
| owner handling time, including generating, downloading and uploading art | — |
| owner-found defects the binding checks should have caught | target 0 |
| final quality against the reference pack | the owner's verdict on the rubric |

A spell that hits 3 reviews with a bland result is a failure of goal 1, not a
success of goal 2.

## 10. Rollout

1. **Before the first trial** (about 1-2 h):
   - phone previews: the capture writes a GIF of 480 px or less, under 5 MB;
   - the reference pack and the prompt template;
   - the NOTES state-record template (`docs/captures/spell_rework/NOTES_TEMPLATE.md`);
   - the rubric.

   Built 2026-10-05, as `anim_preview.gif` beside `anim.gif` (`tools/gif.py`,
   skipped with `gif.ps1 -NoPreview`).
2. **Trial 1, one spell.** Ashen Reckoning: the owner rates it best, and it
   needs an ending. It tests the loop with the least art risk.
3. **Trial 2, two spells with different needs.** Court of Whispers (motion
   around a body) and Viper's Bite (contact on the body), worked at most two at
   a time.
4. **Then decide, on what the trials expose.** Each item is built only if a
   trial shows the need:
   - layer telemetry for the binding checks, extending the existing capture
     run and `timing.json`, not a new command;
   - the arrival art check;
   - promoting advisory checks to binding.
5. **Deferred, built only on evidence:**
   - **One presentation file per spell:** only if measured edit contention on
     `skills.json` delays work.
   - **Tree hygiene:** line-ending normalization, stale-lock clearing and a
     generated-asset check. Three separate small changes, each on its own
     schedule, none a prerequisite for art.

## 11. Changes from rev 1, by review finding

| Finding | Rev 2 |
|---|---|
| 1 No art quality standard | contract 1 (identity), contract 5 and section 6 (art rubric), section 7 (reference pack) |
| 2 "Watched" missed rhythm and weight | contract 2: playback at 1x and 1.5x, stills, side-by-side, a rubric |
| 3 Checks would reject good effects and miss bad ones | contract 4: three narrow binding checks with exceptions in the identity sheet; the rest advisory; off-screen added |
| 4 Infrastructure ahead of evidence | section 10: previews and templates first; telemetry after trials; file split and hygiene deferred |
| 5 Art loop underspecified | section 7: template, references, delivery spec, targeted revisions, a record per piece |
| 6 Concurrency and closing | contract 7 (two workers), contract 6 (durable record), contract 8 (TESTING.md gate, frozen snapshot) |
| 7 The metric rewards weak output | section 9: retries, attempts, time split and quality measured; review count is a target |

## 12. Questions for the reviewer

1. Are the three binding checks the right minimum?
2. Is Ashen alone enough for trial 1, given that it needs little new art?
3. Is the art rubric specific enough to fail a clean but bland sheet?
