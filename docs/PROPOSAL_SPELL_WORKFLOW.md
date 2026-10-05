# Proposal: a standard workflow for making spell animations

Status: draft for independent review, 2026-10-05. Nothing in it is built yet.

## 1. Context for a reader new to the project

- **The game.** Prince's Palace is a turn-based roguelike in Unity 6 (2D, URP).
  A fight is resolved by the simulation first. Then a presentation layer plays
  each action back as a short animated "beat".
- **A spell's look.** It is data, not code: a list of **layers** in
  `ContentData/skills.json`. Each layer is a drawing, a frame sheet or a
  particle emitter, with timing and motion fields. Examples: rotate around a
  pivot, fade in, flicker, drift, glow over its life, shake the stage.
- **The engine.** A runtime player draws those layers. A generator turns the
  JSON into game assets.
- **Art.** The owner makes it with an image generator (ChatGPT) from written
  prompts: flat cel style, green screen. Tools key out the green and cut the
  sheets into frames.
- **Who works.**
  - The owner: a solo developer, who judges every result by eye.
  - An orchestrating AI session: writes briefs, reviews results, talks to the
    owner.
  - AI implementer agents: edit data and code, run tests, record GIFs.
  - One shared working tree.
- **Evidence.** A GIF recorder plays a scripted fight in a headless Unity
  instance and writes a GIF plus `timing.json`. The commit gate is a Unity
  test suite: about 6,200 EditMode and 2,000 PlayMode tests. A full run takes
  4-6 minutes.

## 2. The problem, measured

Nine of Shawn's ten spells still need the treatment that one spell has just
been through.

**First attempt (2026-10-03/04).** All ten spells were reworked in batches.
- Agents graded their own GIFs as passing, and the orchestrator relayed those
  grades without watching.
- The owner rejected the result: "everything is just one art sheet, or at
  least looks like it".
- On review, six of ten were plainly off. One spell's verb changed by
  accident: a *bite* became *a snake head thrown across the stage*.

**Second attempt (Censer of Embers, the pilot).** It took **7 rounds** over
about 14 hours of wall time (commits `7d2841a7` to `3ae5344e`). The owner
approved the final version. What each round fixed:

| Round | Defect the owner or orchestrator caught | Root cause |
|---|---|---|
| 1 | swing too fast, burst is one big drawing, fire static | first blocking |
| 2 | censer hung off-screen | orchestrator's brief ("hang clearly above the head") |
| 3 | swing "static, not like a real swing" | orchestrator's brief: pivot at the art's top, plus an invented "pause at the ends" |
| 4 | twitch at the start, jump at the break | growing amplitude from near zero; a scale punch about a far pivot |
| 5 | polish: light pool reads as a stain, afterimages murky, green specks, a jump returns | polish pass checked by the agent only |
| 6 | afterimages still weak | — |
| 7 | owner: remove afterimages; no full-body hit flash | owner taste |

Three of the seven rounds trace to the orchestrator's own briefs, not to the
implementing agent. Defects of rounds 4 and 5 were found by ad-hoc scripts the
orchestrator wrote on the spot: a colour-centroid position track per GIF frame,
contact sheets. That track gave false readings on a brown enemy, whose own
pixels matched the lantern's colour.

Other measured friction:
- **GIF size.** GIFs are 17-26 MB, and two of the last three uploads to the
  owner's phone failed.
- **Shared-tree noise.**
  - Phantom changes: about 900 tracked files show as modified with only
    line-ending differences.
  - Stale git locks: an empty `.git/index.lock` was left behind three times,
    after killed processes and a power cut.
  - Uncommitted generated files: twice, regenerated assets were left out of
    the commit that changed their inputs.
  - Gate drift: once, a commit was gated on a test slice after edits made
    later than the last full run.
- **Repeated questions.** One owner question (should melee hits use the new
  edge flash?) was asked four times before it was answered.
- **One file.** All 29 spell presentation blocks live in one 4,136-line
  `skills.json`, parsed in one place. Only one agent can edit spells at a
  time.

## 3. Goals and non-goals

**Goals:**
1. The owner sees nothing that the orchestrator has not checked against
   explicit criteria.
2. Rounds per spell: 7 for the pilot → **at most 3** (block, art + polish,
   close).
3. Owner time per spell: one look per round, under 2 minutes each, answerable
   from a phone.
4. The remaining nine spells are done in days, not weeks, without lowering the
   bar the owner set on the Censer.

**Non-goals:**
- A new art style.
- New animation technology: no rigs, no 3D.
- Changing how fights are simulated.
- Automating the owner's taste.

## 4. Behavioural contracts

These are the rules the workflow must hold, whatever tools implement them. Each
names how it is checked.

1. **Nothing reaches the owner unwatched.** Every GIF the owner receives has
   first been inspected by the orchestrator, on a contact sheet and on the
   automated checks (contract 4).
   - *Check:* the message to the owner states what was checked and what is
     still weak.
2. **Motion is specified as physics or reference, never as invented
   parameters.**
   - *Example:* "a censer on a long chain, released from the left, swinging
     freely", not "amplitude 35, dwell 1.5".
   - *Check:* the brief template has a "reference" line per moving element.
3. **Pictures before words.** The owner's first review of a spell is a rough
   GIF with its beat table attached, never a text plan on its own.
4. **Every capture is measured, not eyeballed alone.** These checks run on
   every capture:
   - per-frame jumps in any layer's position or angle;
   - pop-in (a layer at full opacity on its first frame);
   - frozen holds over 0.15 s;
   - how much of the struck body is covered at the hit;
   - the flash-safety guard (at most 3 large flashes per second);
   - sound, number and badge on one frame.

   They use exact layer data from the engine, not pixel guesses.
5. **One agent per spell, kept for the spell's whole life.** Its context
   carries between rounds; a fresh agent is the exception.
6. **Iteration is cheap; closing is strict.**
   - Between rounds: focused tests only.
   - To close a spell, all of these must hold: the full suite is green on the
     exact commit; the repository matches its generators; the owner approved
     the GIF at 1x and 1.5x, on a short and a tall enemy; the owner listened
     once.
7. **An owner question is asked once.** Each spell keeps one open-questions
   list. A question stays on it until answered and is never re-asked in
   passing.
8. **The engine grows only when a spell proves it must.** A new layer field
   needs the blocking GIF that shows why. When a batch closes, fields used by
   only one spell are reviewed for merging or removal.
9. **Art is checked on arrival.**
   - Green key leak and frame registration (every cell's pivot on the same
     line) are tested when a file lands, before it is wired.
   - Prompts for several spells go out in one batch.

## 5. The loop for one spell

| Step | Owner of the step | Time box | Output | Exit criterion |
|---|---|---|---|---|
| 1. Block | implementer (kept alive) | 45 min | stand-in motion; beat table (beat, element, timing, motion reference); list of art needed; review sheet | the checks of contract 4 pass, or each failure is explained |
| 2. Check | orchestrator | 5 min | yes/no and a short note | the orchestrator would defend every beat to the owner |
| 3. Owner verdict | owner | 2 min | per beat: ✔, or ✘ plus a frame number plus what they see | — |
| 4. Art | owner | one paste covering up to 3 spells | image files | art check passes |
| 5. Wire and polish | same implementer | 30-min rounds | art wired; polish chosen from the polish menu (section 7) | checks pass; orchestrator check; owner verdict |
| 6. Close | same implementer | 15 min | full gate; generator check; NOTES; commit; menu updated with what worked and what failed | contract 6 |

**Pipelining.** Up to three spells are in flight at once. While the owner
reviews spell A, the implementers of B and C are blocking or wiring. This
depends on enabler E4.

## 6. One-time enablers

Each one is justified by a measured problem in section 2.

| # | Enabler | Problem it removes | Estimate | Risk | Cheaper alternative |
|---|---|---|---|---|---|
| E1 | **Layer telemetry**: the capture run writes every live layer's position, angle, scale and opacity per frame. A `spell_review` command turns that into a contact sheet plus the contract-4 checks. | ad-hoc, sometimes false pixel measurements | 2-3 h | the telemetry must match what is drawn; pinned by a test that compares one layer's logged position with its rendered pixels | pixel-only checks (proved unreliable on the pilot) |
| E2 | **Phone-size preview**: each capture also writes a GIF of 480 px or less, under 5 MB | failed uploads; slow review | 30 min | none notable | sending stills only (loses the motion) |
| E3 | **Art check**: on arrival, a tool reports key leak and per-cell pivot alignment for every new sheet | a bad sheet found only in motion, hours later | 1 h | false alarms on intentionally loose sheets; mitigated by tolerance per recipe | eyeballing the contact sheet |
| E4 | **One presentation file per spell**, referenced from `skills.json` by skill id; the generator merges them | one agent at a time on spells | 2-3 h | content hash and ordering changes; schema docs; ensuring no two sources define one spell (the generator refuses duplicates) | a short-lived edit lock on `skills.json` with agents rebasing their block (cheaper, but serial for writes and error-prone) |
| E5 | **Tree hygiene**: a `.gitattributes` line-ending rule (one normalising commit); stale-lock clearing in the tools when no git process is alive; a gate step that fails if regenerated assets differ from the committed ones | phantom changes, locks, uncommitted generated files | 1 h | the normalising commit touches about 900 files once; it must not mix with any other change | keep living with it |

**Total:** about 7-9 hours of agent time, with no owner involvement.

E4 changes the content pipeline across editor tooling and runtime loading. It
must land atomically and needs its own focused review.

## 7. The polish menu

A written list of engine-made effects, each with when to use it, what it costs
(renderers, particles, flashes) and what failed. It is seeded from the Censer
pilot:

| Worked | Failed |
|---|---|
| lantern glow over its life | light pool (read as a stain) |
| turn sparks | afterimages (murky over dark or green bodies) |
| stage kick (cast layers exempt) | |
| glowing embers | |
| smoke from the fire, ash | |
| body glow that follows the fire | |
| light stage gloom | |

Each spell's close step updates the menu, so knowledge accumulates instead of
being rediscovered.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Parallel spells overload the owner with reviews | at most three in flight; one combined message per round |
| Telemetry says "clean" while the GIF looks wrong | the orchestrator still watches the sheet (contract 1); telemetry adds to the eye, never replaces it |
| Time boxes are optimistic (agent warm-up, Unity boot about 3 min per capture batch) | measure actual round times on the first batch and adjust; manifests record all of a spell's scenarios in one Unity boot |
| Engine fields pile up | contract 8's review at batch close |
| E4 breaks content loading | atomic change, full gate, generator check, and a test that every spell resolves to exactly one presentation source |

## 9. How we know it works

Record for each spell in the first batch of three:
- the number of rounds;
- owner minutes;
- wall-clock hours;
- defects the owner found that the checks should have caught.

The success bar is goal 2 (at most 3 rounds) with zero owner-found defects of
the kinds the checks cover (jumps, pop-ins, off-screen elements, flat-colour
flashes). If the first batch misses it, revisit before the next batch.

## 10. Questions for the reviewer

1. Is E4 (one file per spell) worth its cost, or does the edit-lock
   alternative suffice for three spells in flight?
2. Are the contract-4 checks the right ones? Is anything the owner rejected on
   the pilot still uncovered?
3. Is "one agent per spell, kept alive" sound, given that agent context grows
   with every round? Should there be a reset point?
4. Is three spells in flight the right number for a solo owner reviewing on a
   phone?
5. What in this proposal would you cut to get the first batch started sooner?
