# Plan: layered spell presentation — revision 4

**Status:** design gate, revision 4, 2026-09-08. Not started. Supersedes
revisions 1 and 2 of this file in full. Revision 4 is a **targeted amendment**
of revision 3, not a rewrite: the owner approved the direction and named seven
gaps to close before coding. Everything revision 3 said stands except where
the revision-4 log below says otherwise.
**Answers:** `docs/handoffs/spell_layers/BRIEF.md` (owner, 2026-09-08).
**Extends, does not contradict:** `docs/PLAN_SPELL_FEEL.md` §"Revised cast
model" (four moments, `FightBeatPlayer` stays the clock) and §"Minimum
technical changes" (expose the impact moment; add only proven metadata;
extend pools only when exhausted). `docs/SPELL_DESIGN_STANDARD.md`
§"Audiovisual design standard" owns the one-authoritative-impact rule and the
repetition budget; nothing below relaxes either.
**Pilot:** `prismatic_orb`, Water element. **Second validation case:**
`cinderfault`. **Third:** a synthetic caster-anchored two-burst built from art
that already ships.

---

## Review log, revision 4 — owner's gap list

Seven gaps the owner named after approving revision 3's direction, closed here
and nowhere else. No section was restructured, no milestone hour moved, and
nothing was added that a gap did not force.

- **G1 — finite core/wake lifetimes.** Revision 3 spread "when does a layer
  end" across `until`, `seconds`, `fade` and §2c, and left two of its own Water
  layers open-ended: `core` is `until: loop` with no `seconds`, `wake` is
  `until: hold` with no `seconds`. **§2b** now carries the one lifetime table
  for all six kinds — a travelling layer ends **at arrival** unless it authors a
  longer finite `seconds` (which is exactly what the adapter does, §2f, and what
  keeps five shipped spells from being cut short), a follower ends **with the
  layer it follows**, and `fade` is an ending capped at
  `SpellLayerRules.MaxFadeSeconds = 0.5f`, derived from
  `BeatHoldSeconds + BeatGapSeconds` (`FightBeatPlayer.cs:31-32`). **§2d** gains
  two refusals — an unbounded non-emitter layer, and a `fade` past the cap.
  **§6d** and **§8** now state where each of their layers ends, in seconds.
- **G2 — following flight emission with birth-time source sampling.** Revision
  3 had `place: layer:<id>` and `inherit` but never said *when* the source was
  read, so a long tick would have stacked a whole tick's droplets at the step's
  end position. **§3 T13** states it as trigger → condition → outcome: the spawn
  clock is the shared cast clock, particle `i` is born at
  `windowStart + i / rate`, and its `p0`/`v0` are sampled **once, at that
  birth time**, back-dated along the source's path. **§5a** and **§5b** name
  where the source position comes from —
  `SpellPerformance.PositionOf`/`VelocityOf`, the closed form of §2b's `t*t`
  ease and its exact derivative, **the same function the sprite renderer places
  the source with**. The one thing it cannot back-date (a `follow: true`
  anchor that moved) is stated rather than hidden.
- **G3 — unambiguous arrival ownership.** Revision 3 said arrival three times
  and differently each time, including "three targets have three arrival times",
  which nothing in the model made true. **§3's new "Who owns arrival"** gives one
  owner (`SpellPerformance.ArrivalSeconds`, computed once in
  `ResolveAgainstFrames`), one computation (the **first** `travelSeconds > 0`
  layer in authored order — no new field, because **§2d** now refuses a second
  traveller in any cast that schedules `at: arrival`), one publisher and one
  consumer (`SpellSchedule`, nobody else), and `arrival = release` when nothing
  travels. **§2b**'s per-target paragraph is corrected to "one time, N places".
- **G4 — validation consistent with the examples.** Every JSON block in §6d, §7
  and §8 and every adapter row in §2f walked against every rule in **§2d**.
  Six inconsistencies, each fixed in the direction the code supports and each
  listed with its rule and its example below. **§2d** also gains the two
  `ArtPathConvention.Kinds` entries the layer paths need and the sweep branch
  that keeps `ArtPathConventionTests` honest about them.
- **G5 — explicit beat-speed synchronization.** Revision 3 said "the clock reads
  `Time.time`" and stopped, which is half an answer: the beat's waits are
  `Scaled(seconds)`, so a scheduler holding authored seconds against `Time.time`
  is 60× out under a test multiplier. **§3's new contract C1** names one clock
  (`Time.time`, or `ClockOverride`), one conversion
  (`FightBeatPlayer.Scaled`, `:105-106`, the only reader of
  `BeatSpeedMultiplier`), how pause and `Time.captureFramerate` reach every
  layer through it, and what is out of contract. **§10 L1** cross-references it;
  **§9 M4** carries `ASpellAndItsBeatCrossTheHitCueOnTheSameFrame` at
  multiplier 1 and 60, with an exact-arithmetic half and a frame-count half.
- **G6 — overlap/pooling tests including the ground layer.** The ground band was
  a pool of exactly one (`FightScreen.cs:2432`) and no revision-3 test touched
  it under overlap. **§4** adds `SpellGroundRenderers = 2` — forced by per-cast
  ownership, and bounded by `2 × BeatHoldSeconds > 0.78s` so a third fault
  cannot overlap — and extends the content-derived pin to count **per band**.
  **§9 M4** adds the four cases: two Cinderfaults back to back (the second gets
  its own member, it does not supersede), Cinderfault over a live Water tail on
  one target, exhaustion where the **ground** member is the one denied (the cue
  still fires exactly once), and `EndFight` mid-fade. Every one has an
  observable assertion.
- **G7 — actual allocation measurement.** Revision 3 measured
  `GC.GetTotalMemory(false)` — retained memory, which reads the collector's mood
  rather than the code. **§4** replaces it with per-frame GC allocation:
  `ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame")`
  (Unity 6 core module; the older `UnityEngine.Profiling.Recorder` on `GC.Alloc`
  is rejected because it reports counts, not bytes), a four-part definition of
  where warm-up ends, 120 frames of a Water cast over a live Cinderfault tail,
  and two assertions — **zero** bytes from `Tick` via
  `Is.Not.AllocatingGCMemory()`, and a **zero difference** between casting and
  idle for the whole frame, because a Unity frame in this scene is not
  allocation-free and pretending otherwise would make the test a lie. **§9 M8**
  carries it.

**The six inconsistencies G4 found**, each with the rule and the example:

1. **`at: arrival` was layer-scoped and refused Water's own splash.** The rule
   read "a layer scheduled `at: arrival` **belongs to, or references**, a layer
   with `travelSeconds > 0`". §6d's `splash` is `place: target-centre`,
   `at: arrival`, and neither travels nor names `core`. As written,
   `prismatic_orb` fails its own content build. **Fixed toward the example**:
   arrival is a cast-level time (G3), so the rule is cast-scoped.
2. **`impactX`/`impactY` both-or-neither refused Cinderfault's fault.** §7's
   `fault` states `impactY: 0.063` alone, and so does §2f's adapter row for the
   ground layer — the legacy block has a `groundImpactY` and **no
   `groundImpactX` at all**. **Fixed toward the examples**: `place: formation`
   is exempt, because its horizontal centre is the measured span's midpoint
   (`GroundBoxFor`, `FightController.SpellVfx.cs:223-236`) and there is nothing
   for an `impactX` to correct.
3. **The emitter-inertness rule refused every sprite layer.** "Emitter fields
   set on a non-emitter layer" against zero would fire on §6d's `core`,
   `splash`, §7's `fault` and `erupt` and every adapter output, because
   `SpellEmitter.sizeMin`, `sizeMax` and `fadeFrom` all initialise to `1f`.
   **Fixed in the rule**: compared against a default-constructed `SpellEmitter`.
4. **`sort`, `until` and `facing` were mandatory by accident.** §8's two layers
   author none of them; the `sort` rule as written ("is `ground` or `effects`")
   refuses a blank. **Fixed in the rule**, to the shape the codebase already
   uses: `SpellAnchorNames.IsKnown` returns true for blank
   (`SpellAnchor.cs:84-85`) precisely so an unauthored word means the default.
   Blank `render` and `place` stay refused — there is no safe default for what
   draws or where.
5. **`until` and `facing` had no rule at all**, while `render`, `place`, `at`
   and `sort` had four. G1 needs `until` parsed — a lifetime rule stated in
   terms of a word nothing validates is not a rule — and `facing` rides the same
   row because it is the same one-predicate, one-message shape. Two more name
   tables in **§5a**'s row, no new mechanism.
6. **A `still`'s `path` was documented as a sprite and authored as a folder.**
   The field comment said "single sprite (still)"; §6d's `wake` names
   `Spells/prismatic_orb_water_wake`. **Fixed toward the example**, on
   mechanical grounds: `SpellVfxRecipeDriftTests` matches a played path's last
   segment against recipe filenames (`:140-144`) and requires it to be a
   directory on disk (`:170-172`), so `…_wake/f0` would fail both.

**Three citations corrected in passing**, all found while grounding the above:
the non-travelling `SetFacing(1f)` is `FightController.SpellVfx.cs:294` (and
`:523`), not `:494`, which is inside `RenderedSize` (§2f); the beat's impact
wait is `FightBeatPlayer.cs:458`, not `:459` (§3 C1); and §6d's "droplets clear
about 0.73s after release" is 0.69s once each layer's end is computed (§6d).

**Not closed, and why.** Nothing in the seven. The two things revision 3
recorded as unverifiable without running Unity — the 24 `SpellVfxTests`
assertions through a new path, and the allocation figures — are unchanged in
status: G7 specifies *how* to measure, not what the number will be.

---

## Review log, revision 3

An adversarial pass over revision 2 against the tree, section by section.
Nine of its decisions survived; six did not. Everything below is a change
actually made to this document, with the evidence that forced it.

**Overturned.**

- **R1 — the adapter cannot run at content-build time.** Revision 2 §2f had
  `ToLayers()` called by the resolver "and its output is stored". That
  collides with two of its own rules three pages earlier: §2e refuses a
  non-empty `layers` with `layerFormat: 0`, and §2d refuses `layers` and a
  legacy `path` together — and the stored adapter output has exactly both. It
  also rewrites every shipped spell's generated asset for no gain. **Moved to
  runtime**: `ToLayers()` is called once inside `Begin`, authored content is
  never rewritten, and the player still sees only layers, so §0.7's single
  orchestration path is unaffected. §2f, §9 M1/M3.
- **R2 — `SpellLayer` could not express the departure hold, and the adapter's
  mapping for it was wrong.** Today a travelling sheet is *drawn and
  animating on the caster from frame 0* and only starts moving at
  `departure` (`SpellVfxPlayer.cs:190`, `:234-241`). Revision 2 mapped
  `departFrame` to `at: release, offset = (departFrame - 1) / frameRate`,
  which delays the whole layer's start instead of just its motion.
  `SpellVfxTests.ATravellingEffectHoldsAtTheCasterUntilItsChargeIsDone`
  (`:637`) reads the image's position on the frame after the cast (`:671-672`)
  and would find nothing there. **Added `travelDelay`** to the layer, and
  fixed the adapter row. §2b, §2f.
- **R3 — the travel ease was unstated.** The flight lerps `t*t` over the
  departure→arrival window (`SpellVfxPlayer.cs:240-241`). Nothing in revision
  2's vocabulary said so, so a time-driven scheduler would have shipped every
  travelling spell on a linear path — a silent retiming of five spells.
  Written down as the one house rule, not authored. §2b, §3.
- **R4 — T6 breaks three tests revision 2 listed as "green, assertions
  untouched".** Moving `StopVfx` off `Flush` kills
  `SpellVfxTests.FlushReleasesTheGroundLayerAndEveryEruption` (`:1338`), whose
  whole body calls `Flush()` at `:1351` and asserts the layers stopped; and
  `CinderfaultSpellCaptureTests` calls `_player.Flush()` at `:123` and `:231`
  deliberately, the latter inside the capture loop with the comment "Flushing
  here is what makes the remaining samples a genuinely still, cooled stage".
  **`Flush` is split**: `Flush()` supersedes a playback, `EndFight()` abandons
  one and cancels visuals; `OnDisable` calls the latter and so do those three
  sites. The edits are now declared rather than promised away. §3 T6, §9 M3.
- **R5 — `ThePresentationCopiesEveryNewFieldAcrossABoundary` does not already
  catch a forgotten `layers`.** Revision 2 claimed it "is already written to
  catch exactly that". It is hand-written field by field
  (`CinderfaultSpellTests.cs:152-178`) and would pass unchanged with `layers`
  missing from `Copy()`. Worse, `Copy()` is a *shallow* copy and is the only
  boundary three call sites use (`ResolvedSkill.cs:246`, `:283`, `:341`, the
  first reached through `AsElement`'s `MemberwiseClone()` at `:228`), so a
  copied array would alias the catalogue into every beat — the exact bug
  `Copy()`'s own header exists to prevent. **`Copy()` deep-copies `layers`,
  and a reflection pin asserts every public instance field of
  `SpellPresentation` is written by it.** §2f, §9 M1.
- **R6 — the version branch was in three places, not one.** `frames.Length`
  is needed by the hit cue, by `fps: 0`, and by legacy `travelSeconds`;
  revision 2 named only the first. **One Core function,
  `ResolveAgainstFrames`, called once in `Begin`, owns all three and is the
  only place `layerFormat` is read.** §2f, §5c.

**Corrected without overturning.**

- **R7 — clock ownership.** Revision 2 kept `SpellVfxPlayer` "verbatim"
  including its coroutine, which would have left two clocks and two
  schedulers. The renderer loses `PlayRoutine`, `Now()` and `ClockOverride`;
  `SpellPerformancePlayer` owns the clock and reads `Time.time`. The two
  files that must move with the static are named:
  `GlobalStateLintTests.cs:92-94` (a literal regex on the class name) and
  `TestGlobals.cs:71`. §5c, §10 L1.
- **R8 — the pool derivation was arithmetic over a case content cannot
  produce.** `prismatic_orb` is `DamageSingle` (`skills.json`), so "Water on
  three targets is 3 core + 3 wake + 3 splash = 9" describes no cast that
  exists. Replaced with a content-derived pin. §4.
- **R9 — `place: formation` ignores `size`.** Its box is the measured span
  (`GroundBoxFor`, `FightController.SpellVfx.cs:223-236`). Stated, with a
  validation rule. §2b, §2d.
- **R10 — particles need their own pool node.** They cannot come out of
  `SpellVfx`, whose members each carry a dissolve child and a
  `SpellVfxPlayer` (`FightScreen.cs:2377-2408`). Declared between `:316` and
  `:317`. §4, §12.
- **R11 — emitter lifetime (brief §"Lifetime": "emission can stop while
  already emitted particles finish") was unanswered.** Stated. §3 T12.
- **R12 — frame-range validation (brief §"Compatibility": "frame ranges") was
  unanswered.** `startFrame` is now range-checked against the folder by
  extending `SpellVfxRecipeDriftTests.NoSkillTimesABeatToAFrameItsFolder
  DoesNotHave` (`:158`), which already does this for `impactFrame`. §2d, §9 M5.
- **R13 — five of the brief's proof-#5 and proof-#6 cases had contracts but no
  named test**: tail survival across `Play`, concurrent casts on one target,
  cancellation mid-flight, target disappearance, nothing surviving teardown.
  Added to M3 and M4. §9.
- **R14 — the brief's "contact/foreground spray" category is already
  expressible** and revision 2 deferred it unnecessarily: within `effects`,
  draw order is member index and a cast allocates in authored array order, so
  a spray authored after the splash draws over it. L5 now covers only the
  cross-cast case. §4, §10 L5.
- **R15 — five citations were out of range and are fixed**, all in the same
  direction (a line number from a file longer than the real one):
  `SpellAnchor.cs:313-352`/`:355-372` → `:26-73`/`:78-86` (the file is 132
  lines); `FrameSequenceLoader.cs:121`/`:145-151` → `:49`/`:78` (82 lines);
  `ContactCues.cs:173-175`/`:184` → `:21`/`:30` (45 lines);
  `FightController.Input.cs:835` → `:859` (`:835` is `PushLogLine`);
  `CinderfaultSpellCaptureTests`' `timing.json` `:293-300` → `:340`.
  `ContentSchema`'s `EnumBackedFields` runs `:58-91`, not `:58-88`, and §7
  attributed `TheGroundAndTheEruptionRuptureTogether` to
  `CinderfaultSpellTests` when it is `SpellVfxTests.cs:1318`. Every other
  `path:line` in this document was opened and checked; the rest were exact,
  including all 24 test line numbers in §2f.
- **R16 — "arrival and impact are forced to the same instant" overstated it.**
  The travelling path arrives at frame `impactFrame - 1` while
  `ImpactFraction` returns `impactFrame / frameCount`, so they are forced
  exactly one frame apart with no way to author more. The complaint survives;
  the sentence is now true. §1.
- **R17 — T1 is false for a static charge.** `FightBeatPlayer.cs:458` skips
  the impact wait entirely when `staticCharge`, because the charge's outbound
  travel already spent it. Stated. §3 T1.
- **R18 — `EnumBackedFields` cannot document an open vocabulary.** It prints
  `Enum.GetNames` (`ContentSchema.cs:96-99`), so `layer:<id>` and
  `<id>:start` have no representation there. The four new discriminators get
  real enums for the closed words; the open forms live in the field's
  `[ContentDoc]`. §2d.
- **R19 — `SpellLayerRules` returning a `List<string>` is not free.**
  `SkillEntryResolver.TryResolveOne` carries `out string error`, one string.
  Named as a joined list at the call site rather than left as an assumption.
  §2d.
- **R20 — `content_stamp` and `ContentInputHash`, unanswered in revision 2.**
  `Domain/Content` is hashed recursively (`ContentInputHash.cs:59`), so
  `SpellLayer.cs` needs no `Sources` edit but does invalidate the stamp and
  force one content rebuild — which M1 wants anyway.
  `Domain/Combat/Presentation/` is *not* hashed, which is correct only while
  no `Resolved*` record has a field of one of those types; if one ever does,
  `ContentInputCoverageTests` (`:53`) will fail naming the file and the line
  to paste. §2a, §5a.
- **R21 — `ContactCues` is `internal`** and PlayMode tests see no Core
  internals (`docs/CODE_STANDARDS.md` §4a), so §8's fixture spells
  `"Vfx/impact_burst"` out. Six frames confirmed on disk
  (`Resources/Vfx/impact_burst/f0..f5`). §8.
- **R22 — facing is per target today**, not per cast: `:539` sits inside
  `PlayTravellingVfx`, which runs once per struck target. Moving it to the
  cast is a real (and safe) change and is now declared as one. The adapter
  also has to map a *non*-travelling legacy layer to `facing: none`, because
  today those are unconditionally `SetFacing(1f)` (`:494`), or
  `AnOrdinaryEffectAfterAMirroredOneIsNotItselfMirrored` (`:701`) fails. §4, §2f.

**Cut as speculative** (owner's instruction: no abstraction without a second
concrete user).

- **The `<id>:start` / `<id>:end` schedule cues, and the cycle detection they
  required.** No layer in Water, Cinderfault or the synthetic two-burst uses
  one; all three schedule off `release`, `arrival`, `hit` or an offset. They
  were the only reason for a dependency graph, a cycle rule and a
  topological ordering in the scheduler — a framework with zero users, which
  is the shape the owner named. **Deferred until a second user**, with the
  one-line note of what to add. `place: layer:<id>` stays: the wake needs it,
  and it keeps a cheap parent-cycle check of its own.
- **`place: stage`.** No user in either proof spell and no known next one.
  Deferred with the same note; `caster`/`caster-centre` already covers a
  cast-level effect that does not follow a body.
- **The named `MigrateV1ToV2` function** in §2e, which described a migration
  for a version that does not exist. The *policy* stays; the function name
  goes.

**Survived review, in one line each.**

- Extending `SpellPresentation` rather than a successor type: correct, and
  the argument is stronger than revision 2 made it —
  `RawElementChoice.vfx` is a whole `SpellPresentation` (`RawSkillEntry.cs:322`)
  so all four orb elements get layers with no chain change, and AUDIT #60's
  measurement (`AUDIT.md:423-427`: `cooldownTurns` 41 mentions,
  `vfx.groundPath` 8) is quoted accurately.
- `SpellLayer[]` rather than `List<SpellLayer>`: forced.
  `ContentSchema.ElementType` (`:197`) is `t.IsArray ? t.GetElementType() : t`.
- Instancing by the placement word: reproduces Cinderfault's one fault plus N
  plumes with no spell id anywhere, because `PlaySpellGroundVfx` is gated on
  a content field (`HasGroundLayer`) and the fan-out is `StruckBy`
  (`FightController.SpellVfx.cs:198-208`). Walked; it holds.
- One authoritative cue with `arrival` per target: holds. `DamageAll` already
  fires one impact block (`FightBeatPlayer.cs:473-507`) and fans out one
  popup per result inside it (`ShowAmount`, `:590-609`), so "one cue instant,
  N popups" is what the code does. T2's wording is tightened to say that.
- `fps: 0` = fit-to-seconds: reproduces `perFrame = total / frames.Length`
  (`SpellVfxPlayer.cs:177`) exactly, and the dissolve (`:219-231`) is a pure
  function of the fractional frame index, so it survives untouched.
- The Domain/Core split: Domain stays engine-free — the asmdef is
  `noEngineReferences: true` and `UiVec` (`Domain/UiKit/UiVec.cs`) is the
  vector type, not `Vector2`. Named, because a reader would otherwise reach
  for `Vector2` and break the build.
- The beam walkthrough: walked independently. A beam touches
  `SpellLayerNames`, `SpellLayerRules`, one renderer, one factory arm. It
  touches neither `SpellSchedule` nor damage dispatch nor any other renderer.
- `Time.time` as the clock fix: right. `WaitForSeconds` is scaled time,
  `Time.timeScale = 0` freezes both (`SystemMenuController.cs:226`), and
  `Time.captureFramerate` advances `Time.time` but not
  `realtimeSinceStartup` (`RuntimeScreenshotTests.cs:23-26`). Promoted out of
  M8 into M4, where the renderers are rewritten anyway.
- Brief change 4 describes a defect the code does not have: confirmed. The
  only production callers of `Flush` are `Play` (`:239`) and `OnDisable`
  (`:301`); nothing runs at beat end. The narrower defect is real.
- The art measurements in §6b: every one re-measured from the PNG headers and
  the recipe. 1536×1024, 1774×887, 1774×887, 2172×724; the 443/444
  alternation, the 1.10 scale correction and contact_f3's 27%-against-23%
  peak are all quoted correctly from `prismatic_orb_water.json`'s `_notes`.
- L7: `prismatic_orb` really has no `vfx` block and no element carries one.
  There is no "before" to photograph without M0.

**Could not verify without running Unity:** that the 24 named `SpellVfxTests`
assertions still hold through a new path (that is M3's whole job); the
allocation figures; whether `water_core_6f`'s f5→f0 join reads as a loop; and
the `-Runtime` recording's behaviour under the new clock.

---

## 0. What changed from revision 1, and why

One paragraph per numbered change in the brief. (What changed from
*revision 2* is the review log above; this section is the answer to the
brief's own eight items and still reads correctly, with the corrections the
log records folded into §§2-5.)

**1. Fixed blocks → an ordered array of layers.** Revision 1 named four kinds
(`flight`, `trail`, `contact`, `particles`), one of each, and said so out
loud in its own "Assumptions to veto" list. That is four hardcoded slots with
different names, and the fifth thing — a caster-side wind-up, a second burst,
a shared ground layer that is not Cinderfault's — would have been a fifth
block. Revision 2 has **no slots**: a presentation carries `SpellLayer[]
layers`, each layer independently choosing what renders, where it belongs and
when it runs, with any number of instances of any kind.

**2. Emission during flight.** Revision 1's `particles` row read "N sprites
emitted at the contact point at arrival". The water pack's whole reason for a
droplet atlas is shedding *while travelling* — the artist's own note says
"world-space droplet emission … let existing flight particles finish". An
emitter layer now has a schedule window like every other layer, so emission
over the flight and a burst at the hit cue are the same feature used twice.

**3. One authoritative impact.** Revision 1 said the impact instant "is
`flightSeconds` after release (arrival), not a frame index" and, two bullets
later, that `contact`'s "`impactFrame` is where the target responds". Those
are two impact times. Revision 2 separates four times explicitly — release,
arrival, contact playback start, hit cue — and only the **hit cue** is
authoritative. Exactly one hit cue fires per gameplay hit per target
(§3), which is what `docs/SPELL_DESIGN_STANDARD.md` §"One authoritative
impact" already requires of the art.

**4. No beat-end flush of living tails.** Revision 1's L3 said "Flush on beat
end … restores sprite, colour, scale, position and parent of every pooled
member". **The codebase does not do this today and never did** — the only
caller of `FightController.StopSpellVfx` (`FightController.cs:714`) is
`FightBeatPlayer.Flush` (`FightBeatPlayer.cs:271`), and `Flush` runs from
`Play` (`:239`) and `OnDisable` (`:301`), not at the end of a beat. What is
actually true, and narrower, is that **a tail dies when the next round's
playback begins**, because `Play` flushes before starting. Revision 2 keeps
tails alive across beats (which already works) and stops killing them at the
next `Play` (which does not).

**5. Ownership by cast instance, not by slot.** Revision 1 said "one
flight+trail pair per stage slot, one contact player per slot". Today's
ownership is worse than per-slot: `PlaySpellVfx` walks `StruckBy(beat)` and
hands out members by *position in that walk*
(`FightController.SpellVfx.cs:99-106`), so every cast starts again at member
0, and `SpellVfxPlayer.PlayFrom` explicitly `StopCoroutine`s whatever that
member was doing (`SpellVfxPlayer.cs:123`) with a comment defending the
restart. Revision 2 gives each cast a handle that owns its own renderers
until they finish or are cancelled; two casts on one target coexist.

**6. FPS decoupled from travel.** `SpellVfxPlayer.PlayRoutine` computes
`perFrame = total / frames.Length` (`SpellVfxPlayer.cs:176-177`), so a
sequence's frame rate *is* its length divided by its frame count, and the
flight lerp is driven off the fractional frame index between `departure` and
`arrival` (`:234-242`). Change `seconds` to make the projectile faster and the
animation speeds up with it. Revision 2 puts `fps` on the layer and travel
duration on the layer's schedule; `fps: 0` means "fit to `seconds`", which is
what the legacy adapter emits so nothing shipped changes.

**7. One orchestration path via an adapter.** Revision 1 kept two: "it grows
beside it, and the single-block path is the same code it is today". That is
two lifetime/timing implementations forever. Revision 2 normalises every
legacy block into layers through one pure function
(`SpellPresentation.ToLayers()`), and the single-block code path is deleted
once the existing regression tests pass through the new one.

**8. Real-time validation.** Revision 1's gates were captures and a
clock-held PlayMode suite. Revision 2 adds a real-time recording through
`tools/screenshot.ps1 -Runtime`, and §9 says what that can and cannot show —
including a clock defect (§10, L1) that has to be fixed before such a
recording means anything.

---

## 1. The brief's direct questions, answered from the code

### Is gameplay resolution already separate from displayed impact?

**Yes, completely, and it is the strongest property this system has.** The
separation is structural, not a convention:

- `FightSession` resolves the entire round synchronously in one pass and
  *records* beats; the view replays them afterwards (`CombatBeat.cs:9-25`).
- A beat carries `PreSnapshot` (`CombatBeat.cs:215`, taken at `BeginBeat`,
  `FightSession.Beats.cs:26`) and `Snapshot` (`CombatBeat.cs:122`, taken at
  `CommitBeat`, `FightSession.Beats.cs:51`). Both are resolved vitals.
- `RecordSpellPresentation` (`FightSession.Beats.cs:149-156`) copies the
  presentation onto the beat and is the only channel by which art reaches
  playback. It reads nothing back.
- Playback paints the recorded numbers: `PaintVitals(beat.PreSnapshot)`
  (`FightBeatPlayer.cs:355`) at the top of the beat, `PaintVitals(beat.
  Snapshot)` (`:481`) at the impact instant.

**Nothing in presentation can change a damage figure.** It cannot even see
one except as a number to draw.

**One coupling does run the other way and must be named.** Presentation owns
*when* the recorded number is shown. `FightBeatPlayer` waits
`ImpactDelayFor(beat)` (`:457-458`) before the impact block, and
`FightController.ImpactDelayFor` (`FightController.SpellVfx.cs:43-50`)
computes that delay from the **number of PNGs on disk**:
`beat.Vfx.seconds * CombatBeat.ImpactFraction(beat.Vfx.impactFrame,
frameCount)`. If the folder fails to load, `frameCount` is 0 and
`ImpactFraction` returns `0.5f` (`CombatBeat.cs:230-233`) — so a spell whose
art is missing lands its blow halfway through an animation that is not
playing.

That is a bounded fallback, which is what the brief asks for, but it is
derived from an accident rather than authored. **Revision 2 preserves the
contract (presentation supplies a time, never a value) and removes the
accident: a layered presentation authors its hit cue in seconds, so missing
art changes nothing about when the blow lands.** Legacy blocks keep the
existing derivation exactly, through the adapter, so shipped spells do not
retime.

### Where does the impact moment currently fire, and what does it synchronise?

`FightBeatPlayer.PlayBeats`, one guarded `try` block at `:473-507`. In order:

| line | call | what it is |
|---|---|---|
| `:479` | `PoseVictims(beat)` | victim stance, first so the flash silhouette matches |
| `:481` | `PaintVitals(beat.Snapshot)` | the numbers move |
| `:494` | `SoundController.PlayClip(beat.Vfx.sfxPath)` | impact clip |
| `:496` | `ShowAmount(beat)` | damage popup |
| `:497` | `FlashTarget(beat)` | hit flash |
| `:498` | `PlayContactFx(beat)` | melee arc + burst, swing/charge with no authored VFX only |
| `:499-500` | `Recoil`, `Punch` | target deformation |
| `:501` | `ShakeStage(ShakeStrength(beat))` | stage impulse |
| `:502` | `Speak(beat)` | voice |

Reached after `PlayVfx(beat)` (`:434`), the cast clip (`:441`), the wind-up
(`:449-450`) and `WaitForSeconds(Scaled(impact))` (`:458`). Hit-stop follows
at `:526-527`, the settle at `:548`, the fallen fade at `:557`, the inter-beat
gap at `:561`.

**One block already exists and it is the right one.** The brief's "one
authoritative impact cue" is therefore not a new mechanism — it is a promise
that presentation will not create a *second* time. Today it does: the
travelling path passes `beat.Vfx.impactFrame - 1` as the sequence's **arrival**
frame (`FightController.SpellVfx.cs:567-568`) while `ImpactFraction` returns
`impactFrame / frameCount` (`CombatBeat.cs:237-238`), so arrival and impact
are pinned **exactly one frame apart, and there is no way to author any other
gap.** A contact animation that wants to compress for three frames before it
lands has nowhere to put the compression. That is exactly the contradiction
brief item 3 names — and the fix is not to widen the gap but to stop deriving
the two times from one number.

### Current pool ownership and draw order, as declared

Ownership:

- `spellVfxPlayers` — `SpellVfxPlayer[]`, `FightController.cs:327`, backed by
  `Image[] spellVfx` (`:320`). Members handed out by index in the
  `StruckBy(beat)` walk (`FightController.SpellVfx.cs:99-106`), capacity
  `FightHudSpec.StageSlotsPerSide` = 3 (`FightHudSpec.cs:51`,
  `FightScreen.cs:2377`).
- A second cast on a member **restarts** it (`SpellVfxPlayer.cs:123`).
- `PlayContactFx` **borrows members 0 and 1** for the melee arc and burst
  (`FightController.SpellVfx.cs:329-330`), safe today only because
  `FightBeatPlayer` calls it exclusively for beats with no authored VFX path
  (`FightBeatPlayer.cs:498`, `WantsContactFx` at `:694`).
- `spellGroundVfxPlayer` — a pool of exactly one (`FightController.cs:335`,
  `FightScreen.cs:2432`).
- Wiring, all of it at build time: `ScreenRegistry.cs:142-182`.

Draw order, as **declared** in `FightScreen.Build` (uGUI draws later siblings
on top; every one of these is inside the nested canvas at sortingOrder 1000,
`FightScreen.cs:358`):

| line | node | band |
|---|---|---|
| `:276` | `BuildSpellGroundVfx()` | **behind both racks** — the comment at `:270-275` states this is why it is declared here |
| `:289-290` | party stage, then enemy stage | the actors |
| `:299-315` | vignette, tracker, bark, plates, columns, tooltips | HUD |
| `:316` | `BuildSpellVfx()` | **above the HUD** |
| `:317` | `BuildDamagePopups()` | **above the effects** (`:2463-2464` says so) |
| `:339,:343,:348` | reckoning, defeat, system menu | modals |

So two semantic draw categories already exist and are verified against the
build order rather than assumed: **ground** (below actors) and **effects**
(above HUD, below damage UI). §5 keeps exactly those two and adds no third.

Pool members are exempt from the layout audit's sibling-overlap and overflow
rules (`UiAudit.cs:343`, `:88` both return early on `UiNodeKind.Pool`), so a
larger pool costs nothing at build time. The `AllowOverlap("a pool's own rect
is the whole canvas …")` on each pool node (`FightScreen.cs:2408`, `:2455`,
`:2476`) is about the pool versus its own siblings.

### How does `ClockOverride` reach the player?

It does not travel; it is read. `public static Func<float> ClockOverride`
(`SpellVfxPlayer.cs:58`), consulted by `private static float Now()` (`:60`),
which otherwise returns `Time.realtimeSinceStartup`. Written only by tests
(`SpellVfxTests.cs:66` holds it at the cast, `:78` runs it 600s past the end),
reset by `TestGlobals.ResetAll` (`TestGlobals.cs:71`) and by the fixture's own
`[TearDown]` (`SpellVfxTests.cs:41`), and policed as a mutable global by
`GlobalStateLintTests.cs:92-94`.

It governs **sampling only**. The beat's own waits are `WaitForSeconds`, which
is scaled engine time and is untouched by the override. §10 L1 records why
that split is a defect and what to do about it.

### What does the current travel path do with frames versus time?

`SpellVfxPlayer.PlayRoutine` (`:173-253`):

- `total = Mathf.Max(0.001f, FightBeatPlayer.Scaled(seconds))` (`:176`).
- `perFrame = total / frames.Length` (`:177`) — **the frame rate is a
  consequence of the duration**, never authored.
- `arrival = Clamp(arriveAtFrame, 0, frames.Length - 1)` (`:182`),
  `departure = Clamp(departAtFrame, 0, arrival)` (`:190`).
- Each rendered frame computes a fractional index `at = elapsed / perFrame`
  (`:212`), assigns `frames[index]` (`:216`), cross-fades into
  `frames[index + 1]` over the last 45% of the frame (`:219-231`,
  `DissolveFraction` at `:34`), and lerps the position with `t*t` easing
  where `t = (at - departure) / (arrival - departure)` (`:240-241`).

So travel duration, animation rate and the ease are one coupled expression of
`seconds` and `frames.Length`. Both `departFrame` and `impactFrame` are frame
*indices* into a sequence whose duration is the same `seconds` the flight
uses. That is what brief items 3 and 6 are both pointing at.

---

## 2. The composition model, as typed content

### 2a. Where it lives, and why not a new type

`SpellPresentation` (`Domain/Content/SpellPresentation.cs`) gains two fields
and keeps every existing one:

```csharp
[ContentDoc("Ordered layers this spell draws; empty means the single-block fields above are used as-is.")]
public SpellLayer[] layers = Array.Empty<SpellLayer>();

[ContentDoc("Which revision of the layer vocabulary this block was authored against; 0 means the pre-layer format.")]
public int layerFormat;

[ContentDoc("Seconds after the cast opens that the blow lands; the one authoritative impact cue. 0 with layers authored means the blow lands as the beat opens; ignored for a pre-layer block, which derives the cue from impactFrame.")]
public float hitCueSeconds;
```

`hitCueSeconds` is the **only** place a version-1 presentation states its
impact time. It is a property of the cast, not of any layer, precisely because
the brief demands one authoritative cue: a layer cannot own it, or two layers
could disagree. `0` is legal and means "as the beat opens" (§3, T3). A
version-0 block leaves it at `0` and the cue is derived from `impactFrame`
instead, exactly as today.

**Not a successor type**, and the reasons are checkable rather than
aesthetic:

- `SpellPresentation` is already the one collapsed seam for how a spell
  looks. `AUDIT.md` #60 measures the alternative: `vfx.groundPath` cost 8 file
  mentions where `cooldownTurns` cost 41, "lower because `SpellPresentation`
  had already collapsed the middle for those six fields". A parallel type
  means a second field on `RawSkillEntry` (`:127`), `RawElementChoice`
  (`:322`), `ResolvedSkill.Vfx`, `CombatBeat.Vfx` (`CombatBeat.cs:152`) and
  a second `Copy()` — the exact restatement #60 exists to stop.
- `ElementChoice.Vfx` is a whole `SpellPresentation`
  (`RawSkillEntry.cs:313-322`, `ResolvedSkill.AsElement` at `:226-249`), so
  each element of the orb gets a full layer list **for free** with no chain
  change. This is the single largest reason to extend rather than replace.
- `ContentSchema.DiscoverNestedTypes` (`ContentSchema.cs:148-169`) walks any
  `[Serializable]` class in `PrincesPalace.Domain.Content` reachable from a
  `Raw*Entry` field, so `SpellLayer` documents itself in
  `docs/CONTENT_SCHEMA.md` with nothing naming it by hand.
- `ContentInputCoverageTests.EveryDomainTypeAContentRecordReachesIsInThe
  HashedInputs` (`:53`) only demands a hash-list entry for Domain types
  declared **outside** `Domain/Content/` (`:77-78`). A layer type declared
  beside `SpellPresentation` needs no list edit.

**Array, not `List<>`, and this is forced.** `ContentSchema.ElementType`
(`:197`) is `t.IsArray ? t.GetElementType() : t` — it does not unwrap
`List<T>`, so a `List<SpellLayer>` field would print as `List\`1` and its
element type would never be expanded into a table. Every other collection in
the content records is already an array (`RawElementChoice[] elements`,
`RawSkillEntry.cs:309`).

### 2b. One layer

```csharp
[Serializable]
public class SpellLayer
{
    public string id = "";          // stable name; required only when referenced
    public string render = "";      // sprite | still | emitter
    public string place = "";       // caster | caster-centre | target | target-centre
                                    // | formation | layer:<id>
    public bool follow;             // re-read the anchor every tick, vs sample once
    public string at = "";          // release | arrival | hit
    public float offset;            // seconds added to `at`
    public string path = "";        // Resources FOLDER, for sprite and still alike
    public float seconds;           // total playback length; 0 = derive from fps and frames
    public float fps;               // 0 = fit the whole folder into `seconds`
    public int startFrame;          // 1-based; 0 means frame 1
    public string until = "";       // once | loop | hold
    public float fade;              // seconds of alpha ramp-out at the end; 0 = cut
    public float dx, dy;            // local offset from the anchor, reference-frame units
    public float scale = 1f;
    public float size;              // square box; 0 = SpellPresentation.DefaultSize
                                    // ignored by place: formation, which measures its own
    public string facing = "";      // auto | none | reverse
    public string sort = "";        // ground | effects
    public float impactX = SpellPresentation.Unauthored;
    public float impactY = SpellPresentation.Unauthored;
    public float aspect;            // 0 = the sheet's own frame aspect
    public float travelSeconds;     // projectile layers only: departure -> arrival
    public float travelDelay;       // seconds after this layer starts before motion begins
    public SpellEmitter emitter = new SpellEmitter();
}
```

**Three properties of this list are rules rather than fields, and each is a
number that would otherwise have had two homes** (`docs/CODE_STANDARDS.md`
§6):

- **The travel ease is `t*t`, always, and is not authored.** The flight lerps
  `Vector2.Lerp(from, to, t * t)` today (`SpellVfxPlayer.cs:240-241`) with the
  comment that says why — "it leaves fast and hard rather than being dragged
  the whole way". A time-driven scheduler that lerped linearly would silently
  reshape all five travelling spells. One ease, stated here, until a second
  spell wants a different one; then it is a word on the layer, additive under
  `layerFormat: 1`.
- **`travelSeconds` measures departure → arrival, and `travelDelay` is the
  hold before it.** Together they replace `departFrame`, and the split is
  forced: today the sheet is *visible and animating on the caster* through its
  wind-up and only then leaves (`:184-190`, and
  `SpellVfxTests.ATravellingEffectHoldsAtTheCasterUntilItsChargeIsDone`
  `:637`, which reads the image's position one frame after the cast and
  asserts it has not moved for the next 1.6s of a 6s sequence). Expressing the
  hold as a schedule offset would have delayed the drawing too, which is a
  different animation.
- **`place: formation` ignores `size` and `dx`.** Its box is the *measured*
  span of every struck slot and its centre is that span's midpoint
  (`GroundBoxFor` and the measurement above it,
  `FightController.SpellVfx.cs:139-176`, `:223-236`); `aspect` and `impactY`
  still apply. A fault authored wide enough for three reaches half the stage
  past a lone rat, which is why the number is measured and not authored, and
  authoring a second copy of it would be the restatement §6 forbids.
- **A `still`'s `path` names a FOLDER, exactly like a `sprite`'s**, and the
  still draws that folder's `startFrame`. Not a sprite file, and the reason is
  mechanical rather than tidy: `SpellVfxRecipeDriftTests
  .EveryFolderASkillPlaysHasARecordedProvenance` matches a played path's **last
  segment** against recipe filenames (`SpellVfxRecipeDriftTests.cs:140-144`)
  and `NoSkillTimesABeatToAFrameItsFolderDoesNotHave` requires it to be a
  **directory on disk** (`:170-172`). A wake authored as
  `Spells/prismatic_orb_water_wake/f0` would present `f0` as its provenance id,
  find no recipe of that name, and fail both. So §6d's `path`, which names the
  folder, is right and revision 3's field comment ("single sprite") was wrong.
  One loader for both kinds — `FrameSequenceLoader.Load`
  (`FrameSequenceLoader.cs:49`) — and one path convention.

#### Every non-emitter layer's lifetime is finite

**The one place that says so.** Revision 3 spread this across `until`, `fade`,
`seconds` and §2c, and left two of §6d's own layers open-ended: `core` is
`until: loop` with no `seconds`, and `wake` is `until: hold` with no `seconds`.
Read literally, both draw for the rest of the fight. They do not, and here is
the rule rather than the accident:

| the layer | when it ends |
|---|---|
| `travelSeconds > 0` (**the core**) | **at its arrival** — `start + travelDelay + travelSeconds` — whatever `until` says. `until: loop` describes what the sheet does *while it crosses the stage*, not how long the layer lives. An authored `seconds` may extend it past arrival, and must then be `>= travelDelay + travelSeconds`; it may never be open-ended |
| `until: once`, no travel | `start + seconds`, where `seconds` is authored or derived as `frames.Length / fps` (§2f) |
| `until: loop`, no travel | `start + seconds`, and **`seconds` is required** — a loop with no stated end is the unbounded case the rule below refuses |
| `until: hold`, no travel | `start + seconds`, and `seconds` is required — **unless** the layer is placed on `layer:<id>`, which is the next row |
| `place: layer:<id>` (**the wake**) | **with the layer it follows**, and never after it. It ends when its source ends, then fades |
| `render: emitter` | `window + lifeMax` (§2c) — the one kind whose end is derived rather than authored, because the brief requires emission to stop while particles finish |

**The fade is bounded too.** `fade` is an alpha ramp-out that **begins at the
end computed above and extends the layer by at most `fade` seconds**; it is not
a second lifetime. `SpellLayerRules.MaxFadeSeconds = 0.5f`, derived rather than
picked: longer than any authored tail in this plan (the splash's 0.31s, the
droplets' 0.38s) and shorter than `BeatHoldSeconds + BeatGapSeconds = 0.75s`
(`FightBeatPlayer.cs:31-32`), so a fade can outlive the beat that cast it — T5
requires that — but cannot still be running when the beat *after* the next one
opens, which is the point at which a tail stops being attributable to a cast by
eye. A follower's fade is additionally clipped to `MaxFadeSeconds` from its
source's end, so the wake cannot outlive the core by more than a tenth of a
second at its authored `fade: 0.10`.

So §6d's two layers are bounded, and the numbers are checkable: `core` ends at
`0 + 0 + 0.25 = 0.25s`; `wake` ends at `0.25s` and is gone by `0.35s`. §2d
carries the two rules that refuse the alternative.

**Deferred until a second user, with the reason** — both were in revision 2
and neither has a consumer in Water, Cinderfault or the synthetic proof:

- **`at: <id>:start` / `<id>:end`.** All three proof spells schedule off
  `release`, `arrival`, `hit` or an offset. These two words were the *only*
  reason the design needed a dependency graph, a missing-reference rule, a
  cycle rule and a topological order in the scheduler — a framework with no
  users, which is precisely what the owner's instruction refuses. When a
  second user appears: the cue parser gains the two forms, `SpellSchedule`
  resolves them after the absolute cues, and the cycle rule arrives with them.
- **`place: stage`** — a fixed point in the effects pool's rect.
  `caster`/`caster-centre` already carries every cast-level effect either
  proof spell wants. When something needs a screen-fixed anchor, it is one
  word and one arm in the placement resolver.

Every discriminator is a **string parsed case-insensitively**, never the enum
itself, for the reason `SpellPresentation.anchor` already gives at `:62-67`:
`JsonUtility` writes an enum as its **ordinal**, so the file would read
`"render": 2` and reordering the enum would silently repoint every spell. The
parse helpers follow `SpellAnchorNames`' shape exactly (`SpellAnchor.cs:77-85`):
a `Parse` and an `IsKnown` beside it, because "Parse cannot both fall back
safely AND report a typo".

**Unlike `anchor`, an unknown `render` does not fall back.** `anchor`'s
graceful default (`Target`) is safe because a misplaced effect is still an
effect; there is no safe default for *what draws*. All layer validation is
build-time, inside `SkillEntryResolver`, which already collects every error in
one pass (`SkillEntryResolver.cs:28-40`) — so an unknown kind fails the content
build and never reaches a player. That is `docs/CODE_STANDARDS.md` §5's
"degrade, but never quietly lie" applied at the right layer.

`place` takes the existing anchor vocabulary verbatim (`SpellAnchor.cs:26-73`)
plus two additions:

| word | scope | resolves to |
|---|---|---|
| `formation` | **once per cast** | the span and average ground line of every struck slot — today's `PlaySpellGroundVfx` measurement (`FightController.SpellVfx.cs:139-176`) |
| `layer:<id>` | one per instance of the named layer | that layer's current position; with `follow: false` it samples the position at schedule time |

`caster` and `caster-centre` are **cast-level**: one instance however many
targets were struck. `target` and `target-centre` are **target-level**: one
instance per member of `StruckBy(beat)`
(`FightController.SpellVfx.cs:198-208`). `formation` is cast-level.
`layer:<id>` **inherits the scope of the layer it names** — a `layer:core`
emitter is target-level because `core` is a projectile launched per target,
and would be cast-level if `core` were. That rule is a property of the
placement word, not of a spell id, which is precisely the brief's "per-target
visual fan-out must not accidentally repeat a shared ground effect".

`layer:<id>` is the one reference in the vocabulary and therefore the one
thing that can cycle (`a` placed on `b` placed on `a`). One rule in §2d
refuses it; the schedule has no references at all, so there is nothing else to
check.

**`arrival` is one time for the whole cast, at N places.** Revision 3 said a
cast travelling to three enemies "has three arrival times (the slots sit at
different depths and distances)". **That was wrong, and nothing in the model
made it true**: `travelSeconds` is an authored number, not a distance divided
by a speed, and today's travel is likewise the same `seconds` and the same
frame indices whatever the distance (`SpellVfxPlayer.PlayFrom` is handed
`beat.Vfx.seconds` per target, `FightController.SpellVfx.cs:567-568`). Three
projectiles leave one caster and land on three slots **at the same instant, at
three different points**. A target-placed layer scheduled `at: arrival`
therefore opens at one time for every instance, and only its *position*
differs. §3 says who computes that time. The hit cue is a second, independent
authored time — `FightBeatPlayer`'s impact block runs once (`:473-507`) and the
per-target numbers already come from `beat.Results` (`CombatBeat.cs:170-175`).
Nothing reconciles the two: an author who wants compression before the blow
sets `hitCueSeconds` later than arrival, which is exactly what §6d does
(arrival 0.25, cue 0.35).

A projectile is not a placement word — it is a `sprite` layer with
`place: caster` (or `caster-centre`), a `travelSeconds`, and an `at: release`,
which the scheduler reads as "draw on the caster from `at`, hold for
`travelDelay`, then move to the target's matching point over `travelSeconds`
on a `t*t` ease". A layer with `travelSeconds > 0` is a projectile; one
without is not. That keeps travel where the brief puts it — a behaviour
between release and impact, not a kind of thing.

**A projectile is target-level even though its `place` is caster-side**, and
this is the one place the scope rule reads backwards: `caster` says where the
box *starts*, and a spell that travels to three enemies needs three boxes
leaving the same point. So a `travelSeconds > 0` layer fans out over
`StruckBy(beat)` whatever its `place` says, which is exactly what
`PlaySpellVfxOn` does today (`FightController.SpellVfx.cs:241`, reached once
per struck target from the walk at `:99-106`). Stated here rather than left as
a surprise, because it is the only exception in the table.

### 2c. The emitter block

```csharp
[Serializable]
public class SpellEmitter
{
    public string path = "";       // folder of stills; one is chosen per particle
    public float rate;             // particles per second while the window is open
    public int burst;              // particles emitted the instant the window opens
    public float window;           // seconds of emission; 0 = burst only
    public float sourceDx, sourceDy;
    public float spreadDegrees;
    public float aimDegrees;       // 0 = along the cast's facing
    public float speedMin, speedMax;
    public float inherit;          // fraction of the source's forward velocity, 0..1
    public float drag;             // per second; linear
    public float gravity;          // reference-frame units per second squared, negative = down
    public float lifeMin, lifeMax;
    public float sizeMin = 1f, sizeMax = 1f;
    public float spinMin, spinMax; // degrees per second
    public float fadeFrom = 1f;    // fraction of life at which alpha starts falling
    public float endScale = 1f;
    public int seed;               // 0 = derived from the cast index; non-zero = fixed
}
```

That is the brief's minimum list, one field each, no more. **Explicitly no**
collision, no sub-emitters, no colour curves, no per-particle scripting.

`seed` is the repeatability contract: **a preview or a test that sets `seed`
gets the identical droplet field every run**, because the simulation is a pure
function of `(spec, seed, index, age)` (§5b) and never of accumulated state.
`seed: 0` derives from the cast handle's generation counter, so two casts in
one fight differ and neither is reproducible — which is right for play and
wrong for a picture, hence the field.

Which sprite a particle draws is the same hash: `emitter.path` names a folder
and the particle picks `f[hash(seed, index) % frameCount]`. That is the
brief's "sprite selection" and it needs no field, because a folder with one
sprite in it selects that one and a folder with eight selects evenly.

**An emitter layer's lifetime is `window` plus the longest particle life it
can emit**, not `window`. The brief asks for exactly this — "emission can stop
while already emitted particles finish" — and it is the difference between a
shed that stops at the target and a shed whose last drops vanish in mid-air.
`until` and `fade` do not apply to an emitter; a particle's own `fadeFrom`
and `endScale` are its ending. §3 T12 states the cancellation half, and §2b's
lifetime table is the one place all six kinds of ending are listed together.
`window + lifeMax` is finite by construction, which is why the emitter is the
one layer kind §2d's unbounded-lifetime rule does not have to police.

**`follow` is inert on an emitter**, and reading it would be the wrong
question. A sprite follows or samples its anchor; an emitter's particles are
detached the instant they exist, and what matters instead is *where the source
was when each particle was born*. §3 T13 is that rule and §5b is its maths.

### 2d. Validation rules, with the error text shape

All refused at content build, all named with the entry label the resolver
already builds (`SkillEntryResolver.cs:297` shows the existing shape,
`SpellPresentationPaths.Check` at `SpellPresentationPaths.cs:22-31` owns the
path list). Every message names the skill, the layer, and the field:

| rule | message shape |
|---|---|
| `render` is one of three words | `` `frost_flare`: vfx.layers[2].render 'beam' is not a renderer. Known: sprite, still, emitter. `` |
| `place` parses, or is `layer:<id>` | `` `prismatic_orb` element #2: vfx.layers[1].place 'projectile' is not a placement. Known: caster, caster-centre, target, target-centre, formation, layer:<id>. `` |
| `at` parses | `` …vfx.layers[3].at 'impact' is not a schedule point. Known: release, arrival, hit. `` |
| `until` and `facing` parse | `` …vfx.layers[2].until 'forever' is not an end policy. Known: once, loop, hold. `` |
| a non-emitter layer's lifetime is bounded: `until: loop` or `until: hold` needs either `seconds > 0`, a `travelSeconds > 0`, or `place: layer:<id>` | `` …vfx.layers[1] is until 'loop' with no seconds, no travel and nothing to follow, so it would draw for the rest of the fight. Give it seconds, give it travelSeconds, or place it on a layer. `` |
| `fade` is `0 <= fade <= SpellLayerRules.MaxFadeSeconds` (0.5) | `` …vfx.layers[1].fade 1.2 is longer than the 0.5s a layer's ending may take. `` |
| every `layer:` reference names a declared layer | `` …vfx.layers[3].place 'layer:core' names no layer. Declared: wake, splash. `` |
| `layer:` references form no cycle | `` …vfx.layers: 'a' is placed on 'b' which is placed on 'a'. `` |
| `place: formation` authors no `size` or `dx` | `` …vfx.layers[0] is placed on the formation and authors size 380, which the measured span overrides. Remove it. `` |
| `startFrame` is inside the folder | `` …vfx.layers[3].startFrame 12 is past the end of Spells/prismatic_orb_water_contact, which has 9 frames. `` |
| `id` unique when non-empty; required when referenced | `` …vfx.layers[1].id 'core' is used twice. `` |
| `seconds`, `fps`, `travelSeconds`, `window`, `lifeMin/Max`, `rate` non-negative; `lifeMax >= lifeMin`; `speedMax >= speedMin` | `` …vfx.layers[0].fps -12 cannot be negative. `` |
| `render: sprite\|still` requires `path`; `render: emitter` requires `emitter.path` and (`rate > 0` or `burst > 0`) | `` …vfx.layers[2] is an emitter and authors neither rate nor burst, so it emits nothing. `` |
| emitter fields set on a non-emitter layer, or sprite fields (`fps`, `startFrame`, `until`) on an emitter. **Compared against a default-constructed `SpellEmitter`, not against zero** — `sizeMin`, `sizeMax` and `fadeFrom` all initialise to `1f`, so a "non-zero" test would refuse every sprite layer in §6d | `` …vfx.layers[0] is a sprite and authors emitter.gravity, which is inert. Remove it or change render to emitter. `` |
| `impactX`/`impactY` both or neither (existing rule, `SpellPresentation.cs:206-212`), **except on `place: formation`, which may state `impactY` alone** | `` …vfx.layers[1] states impactX and not impactY. `` |
| `travelSeconds > 0` only on `render: sprite\|still` with a caster-side `place`; `travelDelay > 0` only with `travelSeconds > 0` | `` …vfx.layers[4] travels but is placed on the target, so it has nowhere to travel from. `` |
| every `path` and `emitter.path` passes `ArtPathConvention` | reuses `SpellPresentationPaths.Check`'s existing text |
| `layers` non-empty and legacy `path` non-empty together | `` `cinderfault`: vfx authors both layers and the single-block path. Move the block into a layer or delete it. `` |
| `sort` is `ground` or `effects` | `` …vfx.layers[0].sort 'overlay' is not a draw band. Known: ground, effects. `` |
| `hitCueSeconds >= 0`, and `> 0` only when `layerFormat >= 1` | `` `cinderfault`: vfx.hitCueSeconds 0.433 needs layerFormat 1; a pre-layer block derives its cue from impactFrame. `` |
| **any** layer scheduled `at: arrival` requires **some** layer in the cast to have `travelSeconds > 0` — cast-scoped, not layer-scoped | `` …vfx.layers[3] fires at arrival, but nothing in this spell travels. Use release, or give a layer travelSeconds. `` |
| at most one layer has `travelSeconds > 0` while any layer is scheduled `at: arrival` | `` `prismatic_orb` element #2: vfx.layers 'core' and 'shard' both travel, so 'arrival' names two instants. Give one of them travelSeconds 0, or schedule off release with an offset. `` |

**A blank discriminator is the documented default, not an error.** Every
`Parse`/`IsKnown` pair follows `SpellAnchorNames`, whose `IsKnown` returns true
for null or whitespace (`SpellAnchor.cs:84-85`) precisely so an unauthored word
means the default rather than a refusal. So the four rows above refuse a
**non-empty unknown** word only: blank `sort` is `effects`, blank `until` is
`once`, blank `facing` is `auto`, blank `at` is `release`. Blank `render` and
blank `place` are still refused — there is no safe default for what draws or
where. This is what makes §8's two layers legal, which author none of the four.

`SpellPresentationPaths.Check` (`SpellPresentationPaths.cs:22-31`) grows one
loop over `layers` — it is already the type that owns "which of my fields are
paths", and its own header says a fifth path added elsewhere would be
forgotten per caller. Note its current shape is `&&`-chained short-circuits,
so it stops at the first bad path; the loop keeps that shape rather than
changing it here.

**Two `ArtPathConvention` entries and one sweep extension, neither free.**
`ArtPathConvention.Check` errors on a field name its table does not classify
(`ArtPathConvention.cs:112-118`), so `vfx.layers[].path` and
`vfx.layers[].emitter.path` must be added to `Kinds` as
`ArtPathKind.RuntimeLoaded` beside `vfx.path` (`:75`) or every layered spell
fails its own path check. The guard that would normally have caught the
omission **cannot see them**: `ArtPathConventionTests.PathFieldsOnRawEntries`
skips array fields outright (`ArtPathConventionTests.cs:67`) and descends
exactly one level (`:70-76`), so it never reaches `SpellLayer`. Worse, adding
the two keys without extending it makes
`EveryClassifiedFieldNameStillExistsOnSomeRawEntry` (`:123`) fail them as
orphans. So the sweep gains one branch — descend into a `Domain.Content`
array's element type, reported with the `[]` in the dotted name — and that is
an M1 deliverable, not a footnote.

**These rules police authored `layers` only.** `ToLayers()` output never meets
them: it is built at runtime inside `Begin` (§2f) from a block the resolver
already validated, so there is no author to report an error to and no asset to
refuse. Its correctness is pinned instead by M3's regression list, which is the
right instrument — an adapter that emitted an illegal layer would show up as a
retimed spell, not as a message nobody reads.

**`startFrame`'s range check is not a resolver rule** and cannot be: the
frame count is on disk, which Domain cannot see. It goes where the identical
check for `impactFrame` already lives —
`SpellVfxRecipeDriftTests.NoSkillTimesABeatToAFrameItsFolderDoesNotHave`
(`:158`), an EditMode test that reads the recipes. Extending it to walk
`layers[].startFrame` is an M5 deliverable, listed with the provenance
extension it rides along with.

**Two integration costs revision 2 treated as free, named:**

- `SkillEntryResolver.TryResolveOne` carries `out string error` — **one**
  string, not a list. `SpellLayerRules` returning `List<string>` is joined
  with `"; "` at that call site. The one-error-per-*entry* shape is
  `TryResolveAll`'s (`:28-42`) and is not changed here.
- `ContentSchema.EnumBackedFields` (`ContentSchema.cs:58-91`) prints
  `Enum.GetNames` (`:96-99`), so it can only document a **closed** set. The
  six discriminators — `render`, `place`, `at`, `sort`, `until`, `facing` — get
  real enums for their closed words, and `place`'s
  `layer:<id>` and any future open form live in the field's `[ContentDoc]`
  string instead, because there is no enum member that could spell them.
  `SpellPresentation.anchor` is not in that table today; adding it is a
  one-line improvement and is in scope.

### 2e. Format version and migration policy

`layerFormat` is `0` for every block authored before this change and for every
block that authors no layers. A block with `layers` non-empty must state
`layerFormat: 1`; the resolver refuses a non-empty `layers` with
`layerFormat: 0` and says so:

> `` `prismatic_orb` element #2: vfx authors layers but no layerFormat. Add "layerFormat": 1. ``

The policy, stated so a future change cannot be argued about:

- **Adding a word** to `render`, `place`, `at`, `sort`, `until` or `facing`, or a new field with
  an inert default, keeps `layerFormat: 1`. Old content still means what it
  meant.
- **Changing what an existing word or field means** increments to
  `layerFormat: 2`, and version 1 blocks are then either refused with a
  migration message or converted — never reinterpreted silently. No migration
  function is designed here, because there is no version 2 to migrate to and
  naming one now would be a framework with no user; the *policy* is the
  deliverable. `docs/CONTENT_SCHEMA.md` records the default of every field
  automatically (`ContentSchema.AppendTable`, `:176-191`), so "what the
  default was" is answerable per commit from git.
- The pre-layer format is version 0 and stays supported indefinitely: it is
  what the adapter reads.

**Nothing writes `layers` except an author.** The adapter (§2f) runs at
runtime and its output never reaches an asset, so `layerFormat: 0` with a
non-empty `layers` — which the rule above refuses — cannot arise from the
system's own machinery. Revision 2 had the adapter store into the same field
at content-build time, which would have produced exactly that state on every
shipped spell, and would also have tripped §2d's "layers and the single-block
path together" refusal. That is R1 in the review log.

### 2f. The legacy adapter

One pure function on `SpellPresentation`, in Domain, engine-free:

```csharp
public SpellLayer[] ToLayers()
```

It is called **once per cast, inside `Begin`**, when `layers` is empty. Its
output is never stored: authored content stays exactly what an author typed,
and the runtime sees layers and only layers, so there is one orchestration
path. (Revision 2 ran it in the resolver and stored the result, which
contradicted two of its own validation rules — see R1.) Mapping:

| legacy field | becomes |
|---|---|
| `path`, `seconds` | one `render: sprite` layer, `at: release`, `seconds` as given, `fps: 0` (fit to `seconds`), `until: once`. **The authored `seconds` is what ends it, including for a travelling block** — today a travelling sheet arrives on frame `impactFrame - 1` and keeps playing the frames after it at the target until the sequence runs out — the flight fraction is `Clamp01`ed (`SpellVfxPlayer.cs:240`) so it sits on `to` from arrival onward while the loop runs to `total`, and `:248` snaps it there for the last time. That is §2b's "unless it authors a longer finite `seconds`" clause, and it is the clause that keeps the five shipped travelling spells from being cut off at arrival |
| `anchor` = `target` / `caster` / `target-centre` / `caster-centre` | that layer's `place`, verbatim, and **`facing: none`** — today a non-travelling effect is unconditionally `SetFacing(1f)` (`FightController.SpellVfx.cs:294`, and again in the caster-less fallback at `:523`; revision 3 cited `:494`, which is inside `RenderedSize`), and `AnOrdinaryEffectAfterAMirroredOneIsNotItselfMirrored` (`SpellVfxTests.cs:701`) is the test that says so |
| `anchor` = `travel` / `travel-centre` | `place: caster` / `caster-centre`, **`facing: auto`**, plus `travelSeconds` and `travelDelay`, computed below |
| `departFrame` | **`travelDelay`**, not a schedule offset: the sheet is drawn and animating on the caster from frame 0 and only its *motion* waits. `travelDelay = seconds * (departFrame - 1) / frameCount`, and `travelSeconds` below is measured from the end of it |
| `impactFrame` | the cast's **hit cue**, at `seconds * ImpactFraction(impactFrame, frameCount)` — the identical expression `ImpactDelayFor` uses today (`FightController.SpellVfx.cs:49`). Left as `impactFrame` on the presentation rather than baked into `hitCueSeconds`, because `frameCount` is not knowable at content-build time |
| `size` | `size`, unchanged |
| `impactX`, `impactY` | `impactX`, `impactY`, unchanged |
| `groundPath`, `groundSeconds`, `groundImpactFrame`, `groundAspect`, `groundImpactY` | a second layer, `render: sprite`, `place: formation`, `sort: ground`, `at: release`, `facing: none`, `seconds = GroundSeconds` (`SpellPresentation.cs:229`), `aspect = groundAspect`, `impactY = groundImpactY` |
| `sfxPath`, `castSfxPath` | **unchanged and not layers** — see §11 |

**Three things a version-0 block needs that Domain cannot compute**, because
all three depend on `frames.Length`, a Core fact
(`FrameSequenceLoader.Load`, `FrameSequenceLoader.cs:49`, cached at `:78`):

1. the hit cue — `seconds * ImpactFraction(impactFrame, frameCount)`, the
   identical expression `ImpactDelayFor` uses today
   (`FightController.SpellVfx.cs:49`);
2. `fps: 0` → `frames.Length / seconds`;
3. `travelSeconds` and `travelDelay` —
   `seconds * (impactFrame - 1 - (departFrame - 1)) / frameCount` and
   `seconds * (departFrame - 1) / frameCount`. Today's code departs on frame
   `departFrame - 1` and arrives on `impactFrame - 1`
   (`FightController.SpellVfx.cs:567-568`) at `perFrame` × those indices.

Revision 2 named only the first and called it "one branch in one method",
which was not true of a design that needed all three. **All three live in one
named Core function:**

```csharp
SpellPerformance ResolveAgainstFrames(SpellPerformance p, Func<string,int> frameCount)
```

called once from `Begin`, and it is **the only place in the system that reads
`layerFormat`**. A version-1 performance passes through it untouched. Not a
spell id, not a "did this author layers" guess; and it disappears the day the
last version-0 block is re-authored.

**Regression tests that pin the adapter** — all existing, every one must stay
green. Assertions untouched **except the three named at the end of this
section**, which assert the behaviour T6 deliberately changes:

`SpellVfxTests` (PlayMode): `ThePlayerIsWiredAndStartsSilent` (`:511`),
`AMissingSheetIsSilentRatherThanThrowing` (`:523`),
`StoppingImmediatelyClearsAnEffectInFlight` (`:536`),
`APlainSwingHasNoImpactDelayAtAll` (`:547`),
`ATravellingEffectStartsOnTheCasterAndEndsOnTheTarget` (`:567`),
`ATravellingEffectFiresTheWayTheCasterIsFacing` (`:610`),
`ATravellingEffectHoldsAtTheCasterUntilItsChargeIsDone` (`:637`),
`AnOrdinaryEffectAfterAMirroredOneIsNotItselfMirrored` (`:701`),
`AnEffectThatHitsEveryEnemyDrawsOnEveryEnemy` (`:729`),
`ASingleTargetSpellStillDrawsExactlyOnce` (`:777`),
`AnAuthoredImpactPointLandsOnTheTargetsGroundLine` (`:830`),
`AnOffCentreImpactPointMovesTheBoxSideways` (`:849`),
`ASheetWithNoStatedPointKeepsTheMeasuredPlacement` (`:874`),
`AMirroredCastCorrectsTowardsTheOtherEdge` (`:904`),
`TheIncomingFrameFadesUpOverTheOutgoingOne` (`:1026`),
`StoppingClearsTheDissolveLayerTooNotJustTheFrame` (`:1068`),
`AFullFormationGetsThreeEruptionsAndExactlyOneFault` (`:1120`),
`ALoneEnemyGetsOneEruptionAndStillOneFault` (`:1143`),
`EveryLayerIsDrawnAtTheImpactInstantAndNoneSurvivesTheBeat` (`:1172`),
`TheFaultIsWiderUnderThreeEnemiesThanUnderOne` (`:1206`),
`TheFaultReachesEveryEnemyAndSitsNearTheGroundLine` (`:1247`),
`AnOrdinarySpellDrawsNoGroundLayerAtAll` (`:1288`),
`TheGroundAndTheEruptionRuptureTogether` (`:1318`),
`FlushReleasesTheGroundLayerAndEveryEruption` (`:1338`).

`CinderfaultSpellTests` (EditMode): `TheGroundLayerInheritsThePerTargetTiming`
(`:89`), `AnUnsetGroundTimingFallsBackToThePerTargetSequence` (`:108`),
`AnOldSpellWithNoGroundLayerIsUnchanged` (`:131`),
`ThePresentationCopiesEveryNewFieldAcrossABoundary` (`:150`).

`CinderfaultSpellCaptureTests` (PlayMode):
`TheCastLandsOnAllThreeInOneInstantAndTheFaultCoolsAway` (`:251`).

`SpellVfxRecipeDriftTests` (EditMode): `EveryFolderASkillPlaysHasARecorded
Provenance` (`:126`) and `NoSkillTimesABeatToAFrameItsFolderDoesNotHave`
(`:158`) both walk skills' `vfx` blocks; both must be extended to walk
`layers[].path` and `layers[].emitter.path` as well, or new folders enter
without provenance. That extension is a milestone deliverable, not optional.

**`Copy()` must deep-copy `layers`, and no existing test catches it if it
does not.** Revision 2 said
`ThePresentationCopiesEveryNewFieldAcrossABoundary`
(`CinderfaultSpellTests.cs:150`) "is already written to catch exactly that".
It is not: its body is hand-written field by field (`:152-178`) and passes
unchanged with `layers` missing. Two things follow, both M1 deliverables:

- `Copy()` (`SpellPresentation.cs:268-285`) allocates a **new array with
  cloned elements**, each with its own cloned `SpellEmitter`. A shallow array
  copy would alias the catalogue's own layer objects into every beat, which is
  the exact bug `Copy()`'s header exists to prevent — and it matters here more
  than for the scalar fields, because `Copy()` is the *only* boundary and
  three call sites go through it (`ResolvedSkill.cs:246`, `:283`, `:341`), the
  first reached from `AsElement`'s `MemberwiseClone()` (`:228`), which shares
  every reference type it does not overwrite.
- A **reflection pin**: every public instance field of `SpellPresentation`
  must be non-default on the copy of a fully-populated original. That is the
  T1 rung of `docs/CODE_STANDARDS.md` §9's ladder applied to a hand-written
  copy block — it kills the class, not the instance, and it is what should
  have existed before `groundAspect` was added.

**The three tests T6 changes, declared rather than promised away:**

- `SpellVfxTests.FlushReleasesTheGroundLayerAndEveryEruption` (`:1338`) calls
  `Flush()` directly (`:1351`) and asserts both layers stopped. Under T6
  `Flush` no longer stops them; the test calls `EndFight()` instead and keeps
  every assertion. Its name and its header ("an abandoned fight would
  otherwise leave a fault frozen mid-rupture") stay exactly right — abandoning
  a fight is what `EndFight` is.
- `CinderfaultSpellCaptureTests` calls `_player.Flush()` at `:123` (fixture
  teardown) and `:231` (inside the capture loop, with the comment "Flushing
  here is what makes the remaining samples a genuinely still, cooled stage").
  Both become `EndFight()`. Missing this would have produced a capture whose
  "cooled" frames still had a fault in them, and nothing would have failed.

One naming note, stated rather than quietly left: `EveryLayerIsDrawnAtThe
ImpactInstantAndNoneSurvivesTheBeat` does not test beat-end flush — it holds
the clock at the cast, then jumps it 600s past the end and asserts cleanup
(`SpellVfxTests.cs:1179-1196`). Its assertions are correct under the new
contract and stay. Only its name is now misleading; renaming it is a judgement
call for the implementer and costs a call-site sweep of nothing.

---

## 3. The timing contract

Four times, all measured from the beat opening, all independent:

| name | what it is | who owns it |
|---|---|---|
| **release** | the beat's `PlayVfx` instant | `FightBeatPlayer.cs:434`, unchanged |
| **departure** | a projectile layer starts moving | that layer's `travelDelay`, measured from its own start |
| **arrival** | the cast's projectile reaches its destination | `SpellPerformance.ArrivalSeconds`, one number for the cast — see below |
| **contact start** | a target-placed sequence begins playing | that layer's `at` + `offset` |
| **hit cue** | the one instant the blow lands | the presentation, authored once |

Five, not four: revision 2 folded departure into arrival and lost the ability
to reproduce a wind-up held at the caster (R2).

### Who owns arrival

Revision 3 named `arrival` in three places — the table above, §2b's per-target
paragraph and §2d's validation row — and each said something slightly
different. One owner, one computation:

- **Computed by:** `SpellPerformance`, in Domain, once, inside
  `ResolveAgainstFrames` at `Begin` (§2f). Nowhere else. It is a **field on the
  resolved performance**, `float ArrivalSeconds`, not a function anyone can call
  with different arguments and get a different answer.
- **Computed from:** the **arrival layer** — the first layer in authored array
  order with `travelSeconds > 0` — as
  `arrivalLayer.offset + arrivalLayer.travelDelay + arrivalLayer.travelSeconds`,
  all measured from the beat opening. Nothing about distance, depth or the
  target enters it, because nothing in the runtime makes travel time a function
  of distance: `PlayFrom` is handed one `seconds` and one pair of frame indices
  per target today (`FightController.SpellVfx.cs:567-568`).
- **Why "first in authored order" and not a new `arrivalLayer` field:**
  because §2d now refuses a second traveller in any cast that schedules
  `at: arrival`, so "first in authored order" never has to break a tie that
  content can produce. A field that can only ever name one candidate is a field
  with no user. When a second traveller does want its own arrival, the additive
  answer under `layerFormat: 1` is an `arrivalLayer` id on the presentation, and
  the rule that made "first" safe is what gets deleted to allow it. Authored
  order is already load-bearing elsewhere (draw order, §4), so this adds no new
  concept.
- **Published by:** the performance itself, read once by `SpellSchedule` when
  it converts `at: arrival` cues into absolute times.
- **Consumed by:** `SpellSchedule` and nothing else. Not `FightBeatPlayer`,
  which reads only `HitCueSeconds` (T1); not `FightController`; not a renderer;
  not `SpellEmitterSim`. A layer never asks "when did I arrive" — it is
  scheduled at a time the performance already resolved.
- **When no layer travels:** `ArrivalSeconds = 0`, which is release. Defined
  rather than undefined, so the adapter's output (every legacy layer is
  `at: release`, §2f) and any future non-authored path have no hole to fall
  into. §2d's refusal of `at: arrival` in a cast with no traveller stands
  alongside it as a **typo check**, not as the definition: an author who writes
  `arrival` in a spell that does not travel meant `release` and should be told
  so, but the value they would have got is release anyway.
- **Per instance, not per time.** A `target`-placed layer at `at: arrival` gets
  N instances, all opening at `ArrivalSeconds`, each at its own target's
  position (§2b).

Written as trigger → condition → outcome:

- **T1.** Trigger: a cast begins. Condition: the presentation resolves to a
  hit-cue time `H >= 0`. Outcome: the scheduler reports `H`;
  `FightBeatPlayer` waits `Scaled(H)` and fires its existing impact block
  (`:473-507`) unchanged. `ImpactDelayFor` becomes a one-line read of the
  scheduler instead of a frame-count computation.
  **One pre-existing exception, unchanged and named:** a beat with
  `StageApproach.Charge` skips the wait entirely (`FightBeatPlayer.cs:458`,
  `&& !staticCharge`), because the charge's outbound travel already spent it
  (`:452-456`). A spell authored on a charging actor therefore does not get
  its `H`. That is today's behaviour, no spell currently does it, and
  changing it is a combat-pacing decision this plan does not make.
- **T2.** Trigger: the impact block fires. Condition: the beat struck N
  targets. Outcome: **one cue instant for the cast, and one popup per struck
  target inside it**. The block runs once (`:473-507`) and `ShowAmount`
  (`:590-609`) walks `beat.Results` (`CombatBeat.cs:170-175`) drawing a popup
  per result — a dodge shows its own, a landed nothing shows none. So "one cue
  per gameplay hit" is satisfied by a single instant, not by N dispatches;
  nothing in presentation adds or removes a hit. A monster casting from the
  right side is the same path with `facing = -1` (§4) and changes none of it.
- **T3.** Trigger: `H == 0`. Condition: any spell, including one with no art.
  Outcome: legal and common — the impact block fires on the same frame the
  beat opens, which is exactly what every melee beat does today
  (`FightBeatPlayer.cs:458` skips the wait when `impact <= 0`).
- **T4.** Trigger: one tick advances the clock past several scheduled times
  at once (a long frame, a scene load, `BeatSpeedMultiplier = 60`).
  Condition: any number of cues inside `(previous, now]`. Outcome: **each cue
  is delivered exactly once, in schedule order, on that tick.** The scheduler
  is a function of the half-open window, not of "is now >= t", so a cue is
  neither skipped nor repeated. This is the one behaviour that today's
  coroutines cannot state and that `AUDIT.md` #61 (struck, fixed in `c9afc22`)
  cost three flaky runs to discover the hard way.
- **T5.** Trigger: a beat ends while a layer is still drawing. Condition: the
  layer's own lifetime has not expired. Outcome: **it keeps drawing.** The
  beat does not wait for it and does not stop it. The next beat opens on
  schedule.
- **T6.** Trigger: a new round's playback starts (`FightBeatPlayer.Play`,
  `:229`). Condition: tails from the previous round are still alive. Outcome:
  **they keep drawing.** This is brief change 4, expressed against what the
  code actually does — and the mechanism is a **split, not a moved line**:

  | method | what it does | callers |
  |---|---|---|
  | `Flush()` | stop the coroutine, reclaim popups, clear the formation and tracker, fire `_onFinished` — exactly `:252-295` **minus** the `StopVfx` call at `:271` | `Play` (`:239`) |
  | `EndFight()` | `Flush()`, then `CancelAll()` | `OnDisable` (`:301`), and the three test sites named in §2f |

  Two named methods rather than `Flush(bool endingTheFight)`: a bool that
  *selects the behaviour* is refused by `docs/CODE_STANDARDS.md` §5, and
  `Flush(true)` at a call site would tell a reader nothing. The three tests
  that today assert `Flush` stops the layers move to `EndFight` with their
  assertions intact — see §2f, which is where revision 2 wrongly listed them
  as untouched.
- **T7.** Trigger: a cosmetic resource is missing (a folder with no frames)
  or the pool is exhausted. Condition: the hit cue has been scheduled.
  Outcome: **the cue still fires at its authored time.** The scheduler
  dispatches cues; renderers draw. A renderer that could not be obtained
  removes a picture and nothing else. Damage is neither suppressed,
  duplicated, nor delayed, and combat never waits.
- **T8.** Trigger: `FrameSequenceLoader.Load` returns an empty array
  (`FrameSequenceLoader.cs:49`, which caches the result at `:78` whether or
  not it is empty, so the miss costs one probe per folder). Condition:
  a version-1 layer. Outcome: that layer draws nothing; every other layer and
  the cue are unaffected. For a **version-0** block the legacy derivation
  still applies and the cue lands at `0.5 * seconds`
  (`CombatBeat.cs:230-233`) — preserved on purpose, because changing it would
  retime a shipped spell whose art went missing.
- **T9.** Trigger: `Cancel(handle)` — a **visual-only stop**. Condition: the
  cast's gameplay has already resolved (it always has; see §1). Outcome: every
  renderer the cast owns is released and restored; the beat, the damage and
  the log are untouched. There is no such thing as cancelling a combat action
  from presentation, and the module exposes no method that could be mistaken
  for one.
- **T10.** Trigger: the scene is disabled or the fight is abandoned.
  Condition: any. Outcome: `CancelAll()`, everything released, no coroutine
  left frozen mid-frame — the rule `SpellVfxPlayer.OnDisable` (`:149-154`) and
  `FightBeatPlayer.Flush` already follow.
- **T11.** Trigger: the system menu opens mid-cast (`Time.timeScale = 0`,
  `SystemMenuController.cs:226`). Condition: any layer is playing. Outcome:
  **every layer freezes, and resumes where it stopped.** Today it does not —
  see §10 L1, which is a prerequisite for this line being true.
- **T12.** Trigger: an emitter layer's `window` closes. Condition: particles
  it emitted are still inside their `lifeMin..lifeMax`. Outcome: **emission
  stops, the particles finish.** The layer's lifetime is
  `window + lifeMax`, not `window`, so the cast handle keeps owning those
  particles until the last one expires — which is what lets a shed that
  stopped at the target keep falling. This is the brief's "emission can stop
  while already emitted particles finish", which revision 2 did not answer.
  `Cancel(handle)` (T9) is the exception: it releases living particles
  immediately, because a visual-only stop means *stop*.
- **T13.** Trigger: an emitter layer placed on `layer:<id>` has its window open
  across a tick `(prev, now]`. Condition: the source layer is travelling, so
  its position is moving during that tick — and the tick may be long (a scene
  load, `BeatSpeedMultiplier = 60`). Outcome, in four parts:

  1. **The spawn clock is the shared cast clock, not the tick.** Particle `i`
     of the layer is born at `t(i) = windowStart + i / rate`, a time, not an
     event. A tick births every `i` whose `t(i)` lands in `(prev, now]` — so a
     tick spanning six spawn intervals births six particles, at six different
     times, and a tick spanning none births none. `burst` is `t(i) =
     windowStart` for its whole count.
  2. **Each particle is born where the source WAS at `t(i)`, sampled once.**
     Its spawn point is `PositionOf(source, t(i))` — **back-dated**, not the
     source's position at `now`. A long tick therefore lays the six drops
     *along the path the core took during that tick*, which is the difference
     between a shed and six drops stacked at the step's end position. This is
     the one thing that makes emission-during-flight look like emission during
     flight rather than a burst.
  3. **It inherits `inherit` × the source's velocity at that same instant**,
     `inherit * VelocityOf(source, t(i))`, added to its own hashed direction
     and speed (§5b). Also sampled once.
  4. **After birth it is detached, and simulates in stage space** as a pure
     function of `(spawn point, spawn velocity, age)` (§4, §5b). It does not
     follow the source, does not re-read it, and does not move when the source
     is released at arrival. Its age on the tick it is born is `now - t(i)`,
     not zero, so the back-dating survives the first frame as well as the
     birth.

  **Where `PositionOf` lives, and why it is the same function the sprite
  renderer uses.** `SpellPerformance.PositionOf(instance, t)` and
  `VelocityOf(instance, t)`, in Domain, pure, engine-free (`UiVec`, §5a). Every
  layer instance's `from` and `to` are resolved into stage-space constants at
  `Begin` from `SpellCastContext` (§5d), so the pair is arithmetic over the
  authored schedule:

  ```
  u        = clamp01((t - start - travelDelay) / travelSeconds)
  Position = Lerp(from, to, u * u)                        // §2b's one house ease
  Velocity = (to - from) * 2u / travelSeconds             // its exact derivative
  ```

  `SpellPerformancePlayer` calls the same `PositionOf` every tick to place the
  sprite it hands to `SpellVfxPlayer.Show` (§5c), and `SpellEmitterSim` calls it
  at each `t(i)`. **One function, two callers** — which is what keeps the ease
  from having two homes, the failure R3 caught revision 2 committing. A
  non-travelling source has `travelSeconds = 0`, `u` is 1, and `PositionOf`
  returns its anchor with `VelocityOf` returning zero, so `place: target-centre`
  emitters like §6d's `spray` go through the identical path.

  **The one thing it cannot back-date, stated rather than hidden:** a
  `follow: true` source whose *anchor* moved during the tick (a `Move` beat
  rewrites slot positions mid-round, `FightBeatPlayer.cs:327`, `:353`).
  `PositionOf` back-dates the **travel** term only; the anchor term is the
  current sample. Keeping an anchor history would be a per-tick ring buffer
  nothing else in this design needs, and it buys nothing for either proof
  spell — Water's emitter source is a projectile launched from a caster whose
  slot does not move during a cast, so every metre of its motion is in the
  travel term.

**The clock contract, C1 — one time source, one conversion.**

Revision 3 said "the clock reads `Time.time`" (§10 L1) and "`ClockOverride`
governs every layer" and left the reader to assume the beat and the spell would
agree. They agree because of this, and only this:

| the rule | the citation |
|---|---|
| Authored seconds become engine seconds **exactly once**, at `Begin`, through `FightBeatPlayer.Scaled` — the same function the beat's own waits go through. `SpellSchedule` stores engine-second deadlines, never authored ones | `FightBeatPlayer.cs:105-106`, `Scaled(s) = s / BeatSpeedMultiplier`; the beat's wait at `:458` |
| **`BeatSpeedMultiplier` is read inside `Scaled` and nowhere else.** No renderer, no sim and no scheduler multiplies by it a second time | `FightBeatPlayer.cs:103` |
| The scheduler, the sprite renderer and the emitter sim read **one** clock: `SpellPerformancePlayer.Now()`, which is `ClockOverride ?? Time.time`. `Tick(now)` passes that single float down; neither `SpellVfxPlayer` nor `SpellParticleRenderer` calls a clock of its own | §5c; the statics move off `SpellVfxPlayer.cs:58`, `:60` |
| **Pause** reaches every layer for free: `Time.timeScale = 0` stops `Time.time` and stops `WaitForSeconds`, so the beat and the spell freeze on the same mechanism and resume together. This is what today's `Time.realtimeSinceStartup` cannot do | `SystemMenuController.cs:226`; §10 L1 |
| **Capture** likewise: `Time.captureFramerate = 60` advances `Time.time` by exactly 1/60 per frame, so a `-Runtime` recording samples the spell at the pace it draws | `RuntimeScreenshotTests.cs:26` |
| **`ClockOverride` substitutes `Time.time` and nothing else.** Deadlines stay `Scaled`-converted, so a test that holds the clock and a test that runs at multiplier 60 exercise the same arithmetic rather than two code paths | §5c, `TestGlobals.cs:71`, `GlobalStateLintTests.cs:92-94` |

**Out of contract, named:** changing `BeatSpeedMultiplier` *during* a beat.
`Begin` converts the schedule at `FightBeatPlayer.cs:434` and the impact wait
converts at `:458`; a multiplier changed between the two would leave them
disagreeing. It is a test seam set before a fight and reset after — policed as
a global at `GlobalStateLintTests.cs:52-54` — and no production code writes it.

**The test that proves it** (M4): `ASpellAndItsBeatCrossTheHitCueOnTheSameFrame`,
PlayMode, `Time.captureFramerate = 60` so the step is exact. Two halves, both
observable:

- *Arithmetic*: `FightBeatPlayer.Scaled(HitCueSeconds(performance))` equals the
  engine-second offset `SpellSchedule` gives its `hit` cue, at
  `BeatSpeedMultiplier = 1` **and** at `60`. An equality on two floats, not a
  tolerance.
- *Runtime*: record `Time.frameCount` when the module dispatches the `hit` cue
  and when `FightBeatPlayer`'s impact block runs (hooked on `PaintVitals`,
  `:481`), at both multipliers. Assert the two frame counts are **equal** —
  both land inside one frame because both deadlines came from the same
  `Scaled` call and `Time.time` is the accumulator behind both.

**What `FightBeatPlayer` keeps owning**, unchanged: the beat loop, the pre-
and post-snapshot paints (`:355`, `:481`), the wind-up waits (`:449-450`), the
impact block (`:473-507`), hit-stop (`:526`), settle (`:548`), fallen fade
(`:557`), the gap (`:561`), the cast and impact clips (`:441`, `:494`), and
`WantsContactFx`'s decision about which beats get the house melee language
(`:694`). It gains nothing and loses one thing: `ImpactDelayFor` stops being a
function of art on disk. `docs/PLAN_SPELL_FEEL.md:199` said "`FightBeatPlayer`
should remain the clock"; it does.

---

## 4. Lifetime, anchors and pooling

**Ownership is the cast.** `Begin` returns a `CastHandle` (an int plus a
generation counter, so a stale handle cannot address a reused slot). Every
renderer and every particle a cast obtains is recorded against that handle and
released when its own lifetime ends or the handle is cancelled. **A second
cast never restarts or steals a live renderer** — the free list only hands out
members nothing owns. This replaces `SpellVfxPlayer.cs:123`'s deliberate
restart.

**Follow versus sample-once.** `follow: false` is the default. A layer samples
its anchor's position once, at the tick its schedule opens, into the effects
pool's local coordinates — the same conversion `AimPoint` already performs
(`FightController.SpellVfx.cs:425-433`, `parent.InverseTransformPoint(slot.
TransformPoint(...))`). `follow: true` re-reads every tick; if the anchor
becomes null it holds the last sampled position rather than snapping to the
origin.

**Detached particles simulate in stage space.** A particle's parent is the
effects pool node, whose rect is `Place.Stretch()`/`UiSize.Fill` under the
nested canvas (`Ui.cs:695-700`, `FightScreen.cs:358`) — a stable battle
coordinate frame that no slot's depth scale touches. The emitter converts its
source anchor to that frame **at emission time** and the particle's position
is thereafter a pure function of `(spawn point, spawn velocity, age)`. It
therefore cannot follow the projectile after emission and cannot jump when a
target's slot moves (a `Move` beat rewrites slot positions mid-round;
`FightBeatPlayer.cs:327` and `:353` are where that happens).

**Mirroring.** Facing becomes a property of the **cast**, computed once as
`aim.x >= casterX ? 1 : -1` (`FightController.SpellVfx.cs:539`) and applied
before placement, because the horizontal `impactX` correction depends on it
(`:468`, and the comment at `:536-538` records why the order matters).

*This is a change, and it is declared as one.* Today `:539` sits inside
`PlayTravellingVfx`, which runs once **per struck target**, so facing is
currently per target. The two answers agree for every cast that exists,
because a caster never stands between two of its own targets — both racks are
on one side of the stage. Moving it to the cast makes the *whole* cast's
layers agree (a projectile and its wake must not disagree about which way they
point) and removes a per-target recomputation of a per-cast fact. If a future
formation ever straddles a caster, the per-cast answer becomes the wrong one
for the far target and the fix is to compute it per struck slot again — noted
so the choice is visible rather than assumed away.
`facing: auto` (the default for caster-side and projectile layers) takes it;
`facing: none` ignores it — the ground fault does this today for the reason at
`:186-189`, "a fault is symmetrical about the rack it opens under". Particles
mirror their initial velocity's x component by the same sign. Applied as
`rectTransform.localScale.x`, exactly as `SetFacing` does (`SpellVfxPlayer.cs:81-88`).

**Canvas scaling.** All authored offsets, sizes, speeds and accelerations are
in the canvas's reference-frame units per second. The effects pool lives under
one nested canvas at a fixed sortingOrder (`FightScreen.cs:358`) driven by the
project's CanvasScaler, and `UiAudit.RunAllFrames` re-solves the whole screen
at four aspects on every build — so nothing needs a per-resolution correction
and a layer that overflows at one aspect fails the build rather than a player's
screen.

**Target death mid-flight.** The slot survives the death (`StageDeathFade`
fades the sprite; `FightScreen.cs:82`), and the pre-damage snapshot is what
`StruckBy`/`PlaySpellGroundVfx` already read for exactly this reason
(`FightController.SpellVfx.cs:135-138`). Rule: a `target`-placed layer with
`follow: false` lands where the target stood when the cast began, dead or not.
With `follow: true` and the slot destroyed, it holds. Never does the effect
teleport to the pool origin, which is what a null-anchor default would do.

**Draw categories, verified against the declared order.** Two bands, both
already built:

- `sort: ground` → the pool declared at `FightScreen.cs:276`, before
  `BuildStage` at `:281`/`:284`. Behind every figure.
- `sort: effects` → the sprite pool declared at `:316` and the particle pool
  declared immediately after it, both after the whole HUD and before
  `BuildDamagePopups()` at `:317`. Above actors, below damage UI.

**Particles get their own pool node, and it is not a third band.** They
cannot come out of `SpellVfx`: each of its members carries a dissolve child
and a `SpellVfxPlayer` component built by the lambda at
`FightScreen.cs:2377-2408`, all of which is dead weight on a droplet.
`BuildSpellParticles()` is a plain `Ui.Pool` of `Image`s declared between
`:316` and `:317` — same `effects` band, later siblings, so droplets draw
over sprite layers and under the damage number. That is the brief's
"appropriate contact/foreground spray", and it is a node, not a semantic
category.

Within `effects`, draw order is member index. The performance player allocates
a cast's layers in **authored array order from the lowest free index**, so a
single cast on an unfragmented pool draws in the order its JSON lists them —
which is how a spray authored after its splash draws over it, with no
authoring surface for a z-order and nothing to keep in sync. Pinned by one
test. When the pool is fragmented (a second cast starting while the first
still holds low indices) the order between the two casts is allocation order,
not cast order; that residue is L5.

**Pool sizing.** Two new constants in `FightHudSpec` (`FightHudSpec.cs`),
beside `StageSlotsPerSide` and `DamagePopups`, each with the count it was
derived from written at the declaration — the house rule for a reserved
capacity (`docs/CODE_STANDARDS.md` §6, point 4).

Revision 2 derived these from "Water on three targets is 3 core + 3 wake +
3 splash = 9". **`prismatic_orb` is `DamageSingle`** (`skills.json`), so that
cast cannot happen; the arithmetic described no spell. The real worst case
today is Cinderfault: `1 fault + 3 eruptions = 4`, plus the melee borrow's 2,
plus one Water cast's 3 overlapping as a tail — nine. **Counted per band** now
that the two bands have separate constants, that nine is 8 in `effects` (the
fault is the one member in `ground`) — the effects reservation below is
unchanged, because it was already three clear of the total and the split can
only lower it. So:

- `SpellLayerRenderers = 12`, which is `StageSlotsPerSide * 4` and leaves four
  spare over the measured eight.
- `SpellGroundRenderers = 2`, up from today's pool of exactly one
  (`Ui.Pool("SpellGroundVfx", 1, …)`, `FightScreen.cs:2432`, and the single
  `spellGroundVfxPlayer` field at `FightController.cs:335`). **Forced by
  ownership, not by taste:** a second Cinderfault opening while the first
  fault is still cooling must get its own member, because a cast that took over
  a live one would be exactly the `SpellVfxPlayer.cs:123` restart §0.5 exists to
  remove. Two and not more, and the arithmetic is checkable: the fault runs
  0.78s while the shortest possible gap between two beats opening is
  `BeatHoldSeconds` 0.45s (`FightBeatPlayer.cs:31`, and the real gap is larger —
  hit-stop, settle and `BeatGapSeconds` all sit on top), so `2 * 0.45 > 0.78`
  and a third fault can never overlap the first. The ground band stays one
  semantic category with two members; it does not become a pool of twelve.
- `SpellParticles = 64`. One Water cast's steady state is
  `rate * window = 40 * 0.25 = 10` alive during the shed (every droplet's
  `lifeMin` exceeds the window, so all ten are), plus an 18-drop burst = 28
  peak. 64 holds two overlapping casts, which is the case M8 records.

**Neither number is trusted — both are pinned, and the pin is what matters.**
A content test computes, over every skill and element:

```
sort:effects  cast-level layers + target-level layers * StageSlotsPerSide
sort:ground   cast-level layers + target-level layers * StageSlotsPerSide
particles     ceil(rate * window) + burst, summed over emitters, likewise fanned out
```

— counted **per band**, because a spell whose ground layers outgrew
`SpellGroundRenderers` would otherwise be found by a player rather than by the
build — and fails naming the skill when any of the three exceeds its constant.
That makes the
capacity **derived from content** (§6 grade 1) rather than a number somebody
re-measured by hand, which is the failure mode the tab-width table already
demonstrated. Both remain **reservations**, not counts — the rule `WoolPips`
already follows — and the overflow policy below is what happens when a *fight*
rather than the content exceeds them.

**Overflow policy, in priority order.** The hit cue is dispatched by the
scheduler and is never affected (T7). Below that: an emitter that cannot get a
particle **drops that particle silently and keeps emitting**; a sprite layer
that cannot get a renderer is dropped and logged once per cast; nothing ever
allocates a new `GameObject` mid-fight. The existing precedent is
`FightBeatPlayer.ShowAmount`'s "dropped, not queued" fallback
(`FightHudSpec.cs:34-35` cites it).

**Restore-on-release list.** Every renderer handed back restores: `sprite`,
`color` (including alpha), `enabled`, `raycastTarget`, `preserveAspect`,
`rectTransform.anchoredPosition`, `sizeDelta`, `localScale` (which carries the
mirror), `localRotation`, `parent`, and the dissolve child's `sprite`,
`color` and `enabled`. `SpellVfxPlayer.StopImmediately` (`:137-147`) restores
two of those today; the gap is why `PlaySpellGroundVfx` has to call
`SetFacing(1f)` defensively "because a pool member keeps whatever facing the
last thing to use it left behind" (`FightController.SpellVfx.cs:186-189`, and
the same comment again at `:364-366`). One test asserts the full list, and the
two defensive `SetFacing(1f)` calls are then deletable — that is the T2 fix in
`docs/CODE_STANDARDS.md` §9's ladder: one code path owns restoration.

**Allocation measurement — per frame, not retained.** Revision 3 measured
`GC.GetTotalMemory(false)` across ten casts. That is **retained** memory: it
reads whatever the collector happened not to have swept, so a tick path
allocating a kilobyte every frame passes whenever a GC lands inside the window,
and a test that quietly measures the collector's mood instead of the code is
worse than no test. Replaced with the thing actually asked for — bytes
allocated **in a frame**.

**The API, chosen and cited.** `Unity.Profiling.ProfilerRecorder.StartNew(
ProfilerCategory.Memory, "GC Allocated In Frame")`, reading `LastValue` once per
frame. It is in the `UnityEngine` core module on Unity 6 (6000.5.7f1,
`ProjectSettings/ProjectVersion.txt`) — no package to add, and nothing in
`Packages/manifest.json` changes. The older `UnityEngine.Profiling.Recorder.Get(
"GC.Alloc")` is rejected on a checkable ground: it reports sample **counts** and
nanoseconds, not bytes, so it cannot answer "how many bytes". `ProfilerRecorder
.Valid` is checked and a `false` **fails with a message** rather than passing —
a recorder that never started would otherwise make every assertion below read
zero.

**Where warm-up ends**, stated so the steady state is a defined thing and not a
vibe. Warm-up is over once all four have happened:

1. one full cast of the heaviest performance has been begun, ticked to its last
   layer's end and released;
2. `FrameSequenceLoader` holds a cache entry for every folder both performances
   touch — it caches on first probe and caches even an empty result
   (`FrameSequenceLoader.cs:56`, `:78`), so the first draw of any folder is a
   one-off disk cost that belongs to no frame under test;
3. both pool nodes have been activated once (`SetActive`, the wake-up
   `SpellVfxPlayer.PlayFrom` performs at `:129`), so no first-activation cost
   lands in the window;
4. five further frames have passed and been discarded.

**The measurement.** `N = 120` steady-state frames, `Time.captureFramerate = 60`
(`RuntimeScreenshotTests.cs:26`) so the window is a fixed two seconds of game
time, during a Water cast with a Cinderfault ground layer still cooling from the
previous beat — the overlapping case, because the empty case would prove
nothing about pooling.

**Two assertions, and only one of them is "zero".**

- **Zero, on the module's own tick.** `Assert.That(() => player.Tick(now),
  UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory())` —
  `com.unity.test-framework` 1.7.0 (`Packages/manifest.json`), available in
  PlayMode, and it measures the delegate rather than the frame. This is the
  assertion that carries the meaning: **no `new` in the tick path**, including
  no `List`/array/closure/boxed enumerator per tick, which is the class of bug
  the design's pooling exists to prevent.
- **A difference of zero, on the whole frame.** A Unity frame in this scene is
  not allocation-free and never was — `FightBeatPlayer` news a `WaitForSeconds`
  per wait (`:458`), `StruckBy` is an iterator (`FightController.SpellVfx.cs:198`),
  Canvas and TMP rebuild on their own schedule. So the recorder measures the same
  scene twice for the same N frames, idle and during the overlapping cast, and
  asserts **`median(during) - median(idle) == 0` bytes**, with the max-frame
  delta reported in the failure message. Comparing against itself rather than
  against a made-up ceiling is what keeps the number from being a constant
  somebody re-measured once and then defended.

This is a milestone evidence line and one test (M8). It adds no subsystem: no
profiler wrapper, no allocation budget type, no per-frame counter shipped in the
build.

---

## 5. Module seams, with real names

### 5a. Domain (engine-free, EditMode-testable)

| type | file | what it owns |
|---|---|---|
| `SpellLayer`, `SpellEmitter` | `Domain/Content/SpellLayer.cs` | the authored shape |
| `SpellLayerNames`, `SpellPlaceNames`, `SpellCueNames`, `SpellSortNames`, `SpellEndNames`, `SpellFacingNames` | `Domain/Content/SpellLayerNames.cs` | `Parse` + `IsKnown` per discriminator, `SpellAnchorNames`' shape (`SpellAnchor.cs:77-85`) — six, not four: `until` is what §2b's lifetime rule is stated in terms of, and `facing` is the same shape in the same examples |
| `SpellPresentation.ToLayers()` | `Domain/Content/SpellPresentation.cs` | the legacy adapter |
| `SpellLayerRules` | `Domain/Content/SpellLayerRules.cs` | every rule in §2d, returning `List<string>` |
| `SpellPerformance` | `Domain/Combat/Presentation/SpellPerformance.cs` | the **resolved** model: layers with absolute times, instance counts and anchor kinds already decided; owns `ArrivalSeconds` (§3) and `PositionOf`/`VelocityOf`, the one evaluation of a layer's eased path at any `t` that both the sprite renderer and the emitter sim read (§3 T13) |
| `SpellSchedule` | `Domain/Combat/Presentation/SpellSchedule.cs` | `Events Crossed(float from, float to)` — pure, half-open, deterministic |
| `SpellEmitterSim` | `Domain/Combat/Presentation/SpellEmitterSim.cs` | `float BirthOf(SpellEmitter spec, int index)` and `Particle At(SpellEmitter spec, int seed, int index, UiVec p0, UiVec v0, float age)` — pure math. The birth-sampled `p0`/`v0` are **arguments**, so the sim never reaches for a source and stays a function of its inputs (§3 T13, §5b) |

`Domain/Combat/Presentation/` is a new folder inside the existing
`PrincesPalace.Domain` asmdef, so no `.asmdef` is added anywhere near a
partial class (`docs/CODE_STANDARDS.md` §4's hard rule).

**Domain stays engine-free, and the vector type is `UiVec`**
(`Domain/UiKit/UiVec.cs`), not `Vector2` — the asmdef carries
`noEngineReferences: true` and a reflex `using UnityEngine` in
`SpellEmitterSim` would not compile. `Math.Exp` for the closed form is
`System.Math` and is fine. Said out loud because it is the one thing a reader
writing the emitter will reach for first.

**Neither of these three types may become a field of a `Resolved*` record.**
`ContentInputHash` hashes `Domain/Content` recursively
(`ContentInputHash.cs:59`) but names `Domain/Combat` files one at a time
(`:68-76`), and `ContentInputCoverageTests
.EveryDomainTypeAContentRecordReachesIsInTheHashedInputs` (`:53`) walks a
record's public fields transitively and fails naming the file and the line to
paste. `SpellPerformance` is built at `Begin` from a `SpellPresentation` and
is never authored, so the rule holds — but it holds by construction, not by
accident, and the day someone caches one on `ResolvedSkill` the test will say
so. Conversely `SpellLayer.cs` sitting in `Domain/Content` needs **no**
`Sources` edit and **does** change `content_stamp`, forcing one content
rebuild — which M1 wants anyway.

### 5b. The emitter simulation is closed form, and that is load-bearing

A particle's state is computed from its age, never integrated:

```
k     = drag                       (per second, linear)
a     = (0, gravity)
birth = windowStart + index / rate                  the particle's own birth time
p0    = PositionOf(source, birth) + (sourceDx, sourceDy)
v0    = direction(seed, index) * speed(seed, index)
        + inherit * VelocityOf(source, birth)
age   = now - birth
p(t)  = p0 + (v0 - a/k)/k * (1 - e^(-k t)) + (a/k) * t      for k > 0, t = age
p(t)  = p0 + v0 t + a t^2 / 2                                for k = 0
```

`p0` and `v0` are **sampled once, at `birth`, not at the tick that discovers
the particle** — §3 T13 is the rule and `SpellPerformance.PositionOf`
/`VelocityOf` are where the sampling comes from, the same pair the sprite
renderer places the source with.

Three things follow, all of them requirements the brief states:

1. **A large clock step is correct by construction.** Nothing accumulates, so
   sampling at `t` gives the same answer whether one tick or fifty got there.
2. **Seeded previews and tests repeat exactly.** `direction` and `speed` are
   hashed from `(seed, index)`, not drawn from a shared RNG stream, so a
   particle's history does not depend on how many other particles existed.
3. **One simulator, not two.** The Python preview
   (`tools/slice_spell_sheet.py --preview`) can render the sprite layers, but
   it will **not** re-implement this — the honest answer to brief item 7 is
   that emitters are previewed through `tools/preview.ps1 -Spell` and
   `tools/screenshot.ps1 -Runtime`, both of which run the real code, and the
   Python preview prints "emitter layers not simulated; see the runtime
   capture" rather than drawing a second, divergent approximation.

### 5c. Core (engine, PlayMode-testable)

| type | file | what it owns |
|---|---|---|
| `SpellPerformancePlayer` | `Core/SpellPerformancePlayer.cs` | the module: **owns the clock**, holds handles, ticks the schedule, allocates and restores renderers, and holds the one `layerFormat` branch (`ResolveAgainstFrames`, §2f) |
| `SpellVfxPlayer` | `Core/SpellVfxPlayer.cs` | **kept as the `sprite`/`still` renderer and nothing else**; the dissolve (`:34`, `:219-231`), `SetFacing` (`:81-88`) and `Frames` (`:161`) survive verbatim, and `PlayRoutine` (`:173-253`), `Now()` (`:60`) and `ClockOverride` (`:58`) do not |
| `SpellParticleRenderer` | `Core/SpellParticleRenderer.cs` | drives N pooled `Image`s from `SpellEmitterSim` |
| `FightController.SpellVfx.cs` | existing | **shrinks**: keeps the placement measurement (`AimPoint` `:425`, `BoxCentreFor` `:460`, `RenderedSize` `:484`, `VfxDeadSpaceBelow` `:607`, `GroundBoxFor` `:223`, the formation span `:139-174`), loses the orchestration |

**One clock, one scheduler.** Revision 2 said `SpellVfxPlayer` was "kept …
verbatim", which would have left its coroutine advancing frames off its own
`Now()` while `SpellPerformancePlayer` advanced the schedule off another —
two clocks and two orchestrators, which is the thing §0.7 exists to prevent.
So the renderer's whole timing half moves out and it gains one method:

```csharp
void Show(Sprite frame, Sprite next, float blend, Vector2 at, Vector2 size);
```

The `ClockOverride` static moves with the clock, to
`SpellPerformancePlayer`, and **two files must move with it, both of which
name the class in a string**: `GlobalStateLintTests.cs:92-94`, whose entry is
a literal regex `SpellVfxPlayer\.ClockOverride\s*=` plus the reset it demands,
and `TestGlobals.cs:71`. Every fixture that holds the clock
(`SpellVfxTests.cs:66`, `:78`, teardown at `:41`) follows the rename. The
seam's *shape* — a public `Func<float>` with an argued header and a policed
reset — is kept exactly, because §7 of `docs/CODE_STANDARDS.md` is what
earned it.

`docs/PLAN_SPELL_FEEL.md:199` set the bar for extracting a
`SpellPerformancePlayer`: "if three or more primitives need shared lifecycle
management, then extract … with a measured reason". The measured reason is
three renderer kinds, N instances each, with independent lifetimes that
outlive the beat.

### 5d. The interface combat calls

```csharp
public CastHandle Begin(SpellPerformance performance, SpellCastContext where);
public void       Tick(float now);
public void       Cancel(CastHandle handle);
public void       CancelAll();
public float      HitCueSeconds(SpellPerformance performance);
```

`SpellCastContext` is the narrow set of things Core must supply that Domain
cannot name: the caster's slot, the struck slots in order, the formation span,
and the facing sign. Four methods and a query; no interface per class, no
container, no reflection — `docs/CODE_STANDARDS.md` §5's "what this project
rejects" list is followed rather than argued with.

Call sites change as follows and nowhere else:

- `FightController.cs:548` — `beatPlayer.PlayVfx = PlaySpellVfx` becomes a
  one-line `Begin`.
- `FightController.cs:551` — `beatPlayer.ImpactDelayFor` becomes
  `HitCueSeconds`.
- `FightController.cs:552` — `beatPlayer.StopVfx = StopSpellVfx` becomes
  `CancelAll`, wired to `EndFight` rather than `Flush` (T6).
- Nothing in `FightController.Update` (`FightController.Input.cs:859` — not
  `:835`, which is `PushLogLine`). `SpellPerformancePlayer` is a MonoBehaviour
  on the effects pool node and calls `Tick(Now())` from its own `Update`, so
  the module owns its clock rather than borrowing the fight controller's frame
  and the fight controller gains no responsibility. `Tick(float)` stays public
  for the tests, which drive it directly.

### 5e. Adding one new rendering behaviour: a beam

A walkthrough, not a request to build one.

**Changes:** `SpellLayerNames` gains the word `beam`. `SpellLayerRules` gains
its rules (a beam needs two anchors and a width; it may not carry `fps` or
`travelSeconds`). A `SpellBeamRenderer` is written in Core, and
`SpellPerformancePlayer`'s renderer factory gains one arm. `FightScreen` gains
a pool if beams cannot share the effects pool's `Image` members — they can, a
stretched `Image` is a beam, so probably not. `docs/ART_PIPELINE.md` §5b gains
a row.

**Does not change:** `FightBeatPlayer` — not one line. `FightSession`,
`CombatBeat`, `DealDamage`, any damage formula. `SpellSchedule` — a beam is
scheduled by the same three cue words. `SpellEmitterSim`. `SpellVfxPlayer` and
`SpellParticleRenderer`. Every existing spell's content. The adapter.
`ScreenRegistry`'s wiring, unless a new pool was needed.

That is the seam test: a new *rendering* behaviour is local; timing,
scheduling, damage dispatch and unrelated renderers are not touched.

### 5f. Why pooled uGUI `Image`s and not Unity's Particle System

Against the three things the brief names:

- **Coordinate space.** Everything on this stage is uGUI: the slots, the depth
  scale (`FightStageAnchors.SlotScale`, cited at
  `FightController.SpellVfx.cs:375`), the placement maths, the popups. A
  `ParticleSystem` lives in world space and would need its own camera-to-canvas
  conversion for every anchor — a second coordinate system whose disagreements
  with the first would be exactly the class of bug this file's own header calls
  "where the bottom of the effect actually is".
- **Sorting.** Draw order here is sibling order inside one nested canvas
  (`FightScreen.cs:358`, and the ground layer's placement argument at `:270-275`).
  A `ParticleSystem` sorts by renderer queue and would sit either wholly in
  front of or wholly behind the canvas; putting droplets above the projectile
  and below the damage number is not expressible.
- **Performance.** 64 pooled `Image`s is one extra canvas rebuild batch at
  worst; the screen already carries 12 popup panels, 3 spell images and a full
  HUD. Nothing here is near a budget.

Scenes are generated (`CLAUDE.md` rule 1), and a `ParticleSystem` would mean
either a prefab — which this project does not use for UI — or a large block of
new `SceneBuilder` code that knows about layout, which is the one thing
`SceneBuilder` is not allowed to know.

---

## 6. The Water pilot

### 6a. The naming mismatch, and why nothing is renamed

Gameplay id: `prismatic_orb` (`skills.json`, and `docs/PLAN_PRISMATIC_ORB.md`).
Art folder: `Assets/_Project/Art/Sheets/Spells/prismatic_bolt/water/`. Recipe
id: `prismatic_orb_water` (`Art/Sheets/recipes/prismatic_orb_water.json`).

**No rename is needed, because the recipe already bridges them**: its
`sources[].sheet` entries name `Spells/prismatic_bolt/water/water_core_6f.png`
while the recipe file itself — and therefore the `Resources/Spells/` folder it
writes — is `prismatic_orb_water`. `SpellVfxRecipeDriftTests
.EveryFolderASkillPlaysHasARecordedProvenance` (`:126-155`) matches a played
folder's **last path segment** against recipe filenames (`:140-144`), so the
source sheet's directory name is invisible to it. The plan therefore leaves
`prismatic_bolt/` alone as the delivery folder's name and keeps every runtime
id on `prismatic_orb_*`. One sentence goes into
`Art/Sheets/Spells/prismatic_bolt/water/README.md` recording that the folder
name is the artist's and the ids are the game's.

### 6b. The delivered art, and what is wrong with it

Measured, not assumed:

| file | pixels | grid | state |
|---|---|---|---|
| `water_core_6f.png` | 1536 × 1024 | 3 × 2, 512px cells | cut today into `prismatic_orb_water` frames 0-5 |
| `water_contact_8f.png` | 1774 × 887 | 4 × 2, **443/444 alternating** | cut today into frames 6-14 |
| `water_particles_8.png` | 1774 × 887 | 4 × 2, same alternation | **cut by nothing; no recipe reads it** |
| `water_wake.png` | 2172 × 724 | single sprite | **cut by nothing; no recipe reads it** |

Half the delivery has no seat, which is the pressure this plan exists under.

Known defects to inspect, each with its evidence:

1. **The 443/444 alternation.** The README gives explicit column edges
   `0, 444, 887, 1331, 1774` and rows `0, 444, 887`. The existing recipe's
   `_notes` **accept a uniform floor-divided grid instead**, losing up to one
   row/column of edge pixels per cell, on the argument that it is "nothing
   against a 512-padded canvas". That trade was made for a sequence that gets
   padded onto 512×512 anyway. For the particle atlas — where each cell becomes
   a small sprite drawn at a few dozen pixels — re-examine it: state explicit
   rectangles in the new `_drops` recipe, or record in its `_notes` why the
   truncation is still acceptable. Do not leave it unstated.
2. **Scale disagreement between the two sheets.** The recipe's `_notes` record
   the measurement: `core_f5`'s content bbox is 454×472 of 512, `contact_f0`'s
   is 425×417 of 444 — a 7-13% gap, currently corrected by scaling
   `contact_f0` by 1.10. That correction must be carried into the split
   recipes, not lost.
3. **Contact frame 1 contains an approaching ball.** The brief says so and the
   recipe's `_notes` confirm it ("the compact ball with spray just starting").
   The combined recipe keeps it as a bridge frame. **The split contact layer
   plays from frame 2** (`startFrame: 2`), because after arrival the ball has
   already arrived and replaying its approach is the "competing impact time"
   the design standard forbids. Frame 1 stays in the folder so the choice is
   an authored number rather than a deleted asset.
4. **Loop seam.** `water_core_6f` is an "intended six-frame internal motion
   loop" per the README, never tested as one — today it is played straight
   through, once, as part of a 15-frame sequence. Inspect the f5→f0 join in
   the preview GIF before committing to `until: loop`.
5. **Core halo and alpha edges.** The README notes "a soft blue halo" and the
   recipe records `keyed: false` (0% opaque-black sampled across all four
   PNGs), so nothing has been re-keyed. Check the halo does not read as a
   rectangle against the dark stage backdrop.
6. **The wake has no stated pivot.** 2172 × 724 with a "thin left end, thick
   right attachment end". Its attachment point must be authored as
   `impactX`/`impactY` on the wake layer (the same fields, the same meaning),
   because a scan will find the thin tail.

### 6c. The slice/pivot recipe plan

The single combined recipe becomes four, so each layer has its own folder with
its own provenance:

| recipe | source | cells | output |
|---|---|---|---|
| `prismatic_orb_water_core.json` | `water_core_6f.png` | 6, in order | `Resources/Spells/prismatic_orb_water_core/f0..f5`, 512×512 |
| `prismatic_orb_water_contact.json` | `water_contact_8f.png` | 8, `contact_f0` scaled 1.10, `contact_f3` `hold: 2` at 1.06 | `…_contact/f0..f8`, padded to 512×512 |
| `prismatic_orb_water_wake.json` | `water_wake.png` | 1 | `…_wake/f0` |
| `prismatic_orb_water_drops.json` | `water_particles_8.png` | 8, explicit rectangles | `…_drops/f0..f7` |

The existing `prismatic_orb_water.json` and its 15-frame folder **stay on
disk, unplayed**, as the baseline the "after" is measured against.
`SpellVfxRecipeDriftTests` walks content, not folders (`:138`), so an unplayed
folder costs nothing.

`pad_to_common_canvas` (recorded in the recipe's `_notes` and in
`docs/ART_PIPELINE.md:320-323`) already handles the mixed cell sizes; the
`sources` multi-sheet feature is no longer needed once each layer has its own
recipe, which is a simplification the split buys.

### 6d. The authoring example

Under `prismatic_orb`'s Water element, in `skills.json`:

```json
{
  "type": "Water",
  "vfx": {
    "layerFormat": 1,
    "sfxPath": "Audio/Sfx/water_impact",
    "hitCueSeconds": 0.35,
    "layers": [
      {
        "id": "core",
        "render": "sprite",
        "place": "caster-centre",
        "travelSeconds": 0.25,
        "at": "release",
        "path": "Spells/prismatic_orb_water_core",
        "fps": 24, "until": "loop",
        "size": 190, "facing": "auto", "sort": "effects"
      },
      {
        "id": "wake",
        "render": "still",
        "place": "layer:core", "follow": true,
        "at": "release",
        "path": "Spells/prismatic_orb_water_wake",
        "until": "hold", "fade": 0.10,
        "dx": -70, "scale": 0.85,
        "impactX": 0.94, "impactY": 0.50,
        "facing": "auto", "sort": "effects"
      },
      {
        "id": "shed",
        "render": "emitter",
        "place": "layer:core",
        "at": "release", "sort": "effects",
        "emitter": {
          "path": "Spells/prismatic_orb_water_drops",
          "rate": 40, "window": 0.25,
          "sourceDx": -40,
          "spreadDegrees": 55, "aimDegrees": 200,
          "speedMin": 90, "speedMax": 220,
          "inherit": 0.35, "drag": 3.5, "gravity": -1400,
          "lifeMin": 0.22, "lifeMax": 0.38,
          "sizeMin": 0.35, "sizeMax": 0.7,
          "spinMin": -180, "spinMax": 180,
          "fadeFrom": 0.6, "endScale": 0.8
        }
      },
      {
        "id": "splash",
        "render": "sprite",
        "place": "target-centre",
        "at": "arrival",
        "path": "Spells/prismatic_orb_water_contact",
        "startFrame": 2, "fps": 26, "until": "once",
        "size": 300, "facing": "auto", "sort": "effects",
        "impactX": 0.50, "impactY": 0.50
      },
      {
        "id": "spray",
        "render": "emitter",
        "place": "target-centre",
        "at": "hit", "sort": "effects",
        "emitter": {
          "path": "Spells/prismatic_orb_water_drops",
          "burst": 18,
          "spreadDegrees": 150, "aimDegrees": 70,
          "speedMin": 160, "speedMax": 520,
          "drag": 2.0, "gravity": -1600,
          "lifeMin": 0.18, "lifeMax": 0.34,
          "sizeMin": 0.3, "sizeMax": 0.9,
          "spinMin": -360, "spinMax": 360,
          "fadeFrom": 0.55, "endScale": 0.7
        }
      }
    ]
  }
}
```

**Initial numbers and where they come from.** Travel 0.25s sits in the brief's
0.2-0.3s band; six core frames at 24fps is exactly one loop over that travel,
which is the cheapest way to make the seam question decidable. The hit cue at
0.35s is arrival plus 0.10s — at 26fps that is 2-3 contact frames of
compression and crown-rise before the peak, and the recipe's own `_notes`
identify `contact_f3` as the peak (27% ink coverage against the runner-up's
23%). Droplets live 0.18-0.38s, so the last one clears 0.69s after
release against a 0.45s beat hold (`FightBeatPlayer.BeatHoldSeconds`, `:31`) —
the tail genuinely outlives the beat, which is the point. (Revision 3 said
"about 0.73s" here, which took the shed's `lifeMax` and the spray's start time;
the layer-by-layer arithmetic is below.) `size: 190` for the
core against `SpellPresentation.DefaultSize`'s 380 (`:202`) makes the ball a
compact mass rather than a screen-filling glow; `size: 300` for the splash is
below the default so the crown does not swallow the target's head. `gravity`
is in reference-frame units per second squared, so -1400 falls about 175 units
in 0.5s — roughly a third of a slot's height. **Every one of these is a first
guess to be tuned at battlefield scale against the captures, not a
measurement.**

**Where each layer ends, since two of the five state no `seconds`.** By §2b's
lifetime table, not by anything special to Water: `core` is `until: loop` but
travels, so it ends **at arrival, 0.25s**, having looped its six frames at 24fps
exactly once on the way; `wake` follows `core`, so it ends when `core` does and
its `fade: 0.10` clears it by **0.35s**; `splash` runs eight of nine frames at
26fps from `arrival`, ending at **0.558s**; `shed` is `window + lifeMax`,
**0.63s**; `spray` is `burst` at the cue plus `lifeMax`, **0.69s**. Nothing here
is open-ended, and the last of it clears about a quarter-second after the 0.45s
beat hold (`FightBeatPlayer.cs:31`) — the tail outliving the beat that T5 exists
to allow.

### 6e. In-game preview instructions

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Spell prismatic_orb
```

writes `tools/screenshots/preview/spell_prismatic_orb_{before,impact,after}.png`
(the sample→filename dictionary at `PreviewCaptureTests.cs:375-377`), with the
impact sample located from `fight.ImpactDelayFor` rather than recomputed
(`:362`). **Add one more sample** at impact plus 0.30s so the droplets are in
a picture — `FlankSamples` is a `const int = 2` at `:296` and the dictionary
extends by one entry.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Spell prismatic_orb -Launch
```

plays it in the open Editor instead (`preview.ps1:348-356`).

Neither is a gate: `preview.ps1` runs one capture fixture and no part of the
suite.

---

## 7. Cinderfault through the same model

The adapter already produces the right answer, which is the point — but
Cinderfault is also re-authored as explicit layers so the model is proven to
*express* it, not merely to tolerate it.

```json
"vfx": {
  "layerFormat": 1,
  "sfxPath": "Audio/Sfx/cinderfault_impact",
  "castSfxPath": "Audio/Sfx/cinderfault_pressure",
  "hitCueSeconds": 0.433,
  "layers": [
    { "id": "fault",  "render": "sprite", "place": "formation",
      "at": "release", "path": "Spells/cinderfault_ground",
      "seconds": 0.78, "until": "once",
      "impactY": 0.063, "facing": "none", "sort": "ground" },
    { "id": "erupt",  "render": "sprite", "place": "target",
      "at": "release", "path": "Spells/cinderfault_eruption",
      "seconds": 0.78, "until": "once",
      "impactX": 0.5, "impactY": 0.129, "facing": "none", "sort": "effects" }
  ]
}
```

- **One shared ground sequence**: `place: formation` is cast-level, so one
  instance regardless of how many enemies were struck. No spell id appears
  anywhere in orchestration.
- **Separate target plumes**: `place: target` fans out over `StruckBy(beat)`.
- **Aligned peaks**: both layers start at `release` and run 0.78s; the hit cue
  at 0.433s is `0.78 * 5/9` — the same arithmetic
  `SpellPresentation.GroundImpactFrame`'s fallback produces today
  (`SpellPresentation.cs:230`, and `SpellVfxTests
  .TheGroundAndTheEruptionRuptureTogether` at `:1318` — an EditMode-shaped
  `[Test]` living in the PlayMode fixture, not in `CinderfaultSpellTests` as
  revision 2 said — is the test that says the two must peak together).
  Authored once now rather than defaulted twice.
- **`fault` states `impactY` and no `impactX`, and that is legal**, by the
  `place: formation` exemption §2d now carries. A formation's horizontal centre
  is the *measured* midpoint of the struck span (`GroundBoxFor`,
  `FightController.SpellVfx.cs:223-236`), so there is nothing for an `impactX`
  to correct — and the legacy block it replaces has no `groundImpactX` field to
  map from either (§2f's ground row), so the adapter would have produced the
  same shape. Revision 3's rule was a straight both-or-neither and would have
  refused this block, its own second proof spell.
- Neither layer travels and neither schedules `at: arrival`, so
  `ArrivalSeconds` is 0 and nothing reads it (§3). Cinderfault is the case that
  proves arrival is not load-bearing for a spell that does not use it.

Must still pass, unmodified: `AFullFormationGetsThreeEruptionsAndExactlyOne
Fault` (`SpellVfxTests.cs:1120`), `ALoneEnemyGetsOneEruptionAndStillOneFault`
(`:1143`), `TheFaultIsWiderUnderThreeEnemiesThanUnderOne` (`:1206`),
`TheFaultReachesEveryEnemyAndSitsNearTheGroundLine` (`:1247`),
`AnOrdinarySpellDrawsNoGroundLayerAtAll` (`:1288`),
`FlushReleasesTheGroundLayerAndEveryEruption` (`:1338`),
`TheGroundAndTheEruptionRuptureTogether` (`:1318`),
`CinderfaultSpellCaptureTests.TheCastLandsOnAllThreeInOneInstantAndTheFault
CoolsAway` (`:251`), and the `timing.json` written at `:340` — whose
numbers are the deterministic comparison this project actually has.

---

## 8. The synthetic caster-anchored proof

Two overlapping bursts on the caster, from art that already ships, authored
**in the test fixture only** — never in `skills.json`, so no content or
recipe register is touched:

```
layer A: render sprite, place caster-centre, at release, offset 0.00, path Vfx/impact_burst, seconds 0.24, scale 1.00
layer B: render sprite, place caster-centre, at release, offset 0.18, path Vfx/impact_burst, seconds 0.24, scale 1.35
```

(`offset`, not a `<id>:start` reference — those are deferred, §2b. This is the
proof that the *scheduler* handles overlapping instances, which is the part
the brief asks for; it needs no dependency graph to show it.)

Both layers author **no `sort`, no `until` and no `facing`**, deliberately: they
are the case that pins §2d's blank-word defaults (effects / once / auto), which
revision 3's rule table would have refused as unknown draw bands. Both state
`seconds`, so both are bounded by §2b's `until: once` row without needing travel
or a source to follow.

`Vfx/impact_burst` (`ContactCues.ImpactBurstPath`, `ContactCues.cs:21`) is six
frames — `Resources/Vfx/impact_burst/f0..f5`, counted on disk — at
`BurstSeconds = 0.24` (`:30`). **The fixture spells the path out as a
literal**: `ContactCues` is `internal` and `InternalsVisibleTo` names the
Editor assembly only (`docs/CODE_STANDARDS.md` §4a), so a PlayMode test cannot
reference the constant. What this proves that Water and Cinderfault do
not: **two instances of one renderer kind**, on a **caster** anchor,
**scheduled at different offsets**, with **overlapping lifetimes** — B starts
while A is 75% through. Assertions: both renderers drawn simultaneously at
t = 0.20; distinct pool members; A releases at 0.24 while B keeps drawing; both
released by 0.42; the pool comes back whole.

This is the brief's proof #4, and it is the cheapest evidence in the plan that
the model is not four slots with new names.

---

## 9. Gates and milestones, in order

Each is a commit-sized unit with its own evidence. Estimates are agent hours
with the reasoning attached, because a number with no reasoning cannot be
argued with.

**M0 — Baseline (0.5 h).** `prismatic_orb` authors no `vfx` at all today and
the 15-frame `Resources/Spells/prismatic_orb_water` folder is played by
nothing, so **there is no "before" in game.** Author the single-block version
(one `path`/`seconds`/`impactFrame` on the Water element) purely to photograph
it. Then `preview.ps1 -Spell` for `prismatic_orb`, `lightning_bolt`,
`mud_burst`, `frost_flare`, `cinderfault`. *Evidence:* five capture sets
committed under `tools/screenshots/preview/`, plus Cinderfault's `timing.json`.
*Half an hour because it is five scripted runs and no code.*

**M1 — Content shape, rules, adapter (2.5 h).** `SpellLayer`, `SpellEmitter`,
the six name tables and their six enums, `SpellLayerRules`, `ToLayers()`,
`Copy()` deep-copying `layers`, the six `ContentSchema.EnumBackedFields`
entries, `SpellPresentationPaths` extended,
`docs/CONTENT_SCHEMA.md` regenerated. *Evidence:* new EditMode tests for every
rule in §2d, each asserting the **message text shape** and not just the
refusal; the reflection pin that every public instance field of
`SpellPresentation` survives `Copy()`, plus an aliasing assert that mutating
`copy.layers[0]` leaves the original alone (R5 — the existing test does
neither); `ContentSchemaTests.GeneratedMarkdownMatchesCommittedFile` (`:82`)
green; `ContentInputCoverageTests` (`:53`) green;
`CinderfaultSpellTests.ThePresentationCopiesEveryNewFieldAcrossABoundary`
(`:150`) green. Plus, from revision 4's §2d: a test per new row — the
unbounded-lifetime refusal, the `fade` ceiling, the `until`/`facing` parse, the
two-travellers-with-arrival refusal, the `formation` exemption from
`impactX`/`impactY` **and** the `place: target` case still refusing a lone
`impactX`, and one that pins the blank-word defaults (a layer authoring none of
`sort`/`until`/`facing`/`at` resolves to effects/once/auto/release and is
**not** refused); the emitter-inertness rule asserted against a
default-constructed `SpellEmitter` rather than against zero, with §6d's `core`
as the case that must pass; the two `ArtPathConvention.Kinds` entries with
`ArtPathConventionTests.EveryClassifiedFieldNameStillExistsOnSomeRawEntry`
(`:123`) green, which requires the array-descent branch at
`ArtPathConventionTests.cs:67` in the same commit. *Still two and a half hours:
~22 rules rather than ~16, but the six added are the cheapest shape in the set
(one predicate, one message) and the sweep branch is four lines. The adapter's
departure/travel split remains the part that needs care.*

**M2 — The scheduler (1.5 h).** `SpellPerformance` and `SpellSchedule` in
Domain, with no Core caller yet. *Evidence:* EditMode tests, no scene, for:
exact cue delivery; a step spanning three cues delivering each once in order;
a zero-delay cue; two concurrent performances not interfering; a cue at
exactly the window boundary delivered once and not twice. Plus revision 4's
lifetime and arrival contracts, both pure and both EditMode: **every layer of
§6d and §7 has a finite end**, asserted as literal seconds against §2b's table
(`core` 0.25, `wake` 0.25 with the fade clearing by 0.35, `splash` 0.308 — `startFrame: 2` is
1-based, so eight of the contact folder's nine frames at 26fps — `shed`
`window + lifeMax` 0.63, `fault` and `erupt`
0.78) — literals, not the production expression recomputed (`CLAUDE.md` gotcha
5); **a follower ends with its source** when the source is cancelled early
rather than running its authored length; **`ArrivalSeconds` is one number**,
equal for every instance of a three-target `at: arrival` layer, computed from
the first travelling layer, and **0 when nothing travels**. *Down from revision
2's two hours: cutting `<id>:start`/`<id>:end` removed the dependency graph,
the dangling-reference rule, the cycle rule and the topological order, leaving
arithmetic over a sorted list.*

**M3 — The module and the bridge (3 h).** `SpellPerformancePlayer`, the five
methods of §5d, `ResolveAgainstFrames` (§2f), `FightController` rewired at the
three call sites of §5d, the module's own `Update`, and `Flush`/`EndFight`
split (T6). **Every legacy spell now runs through the new path.**
*Evidence:* the entire existing `SpellVfxTests` and `CinderfaultSpellTests`
list from §2f, green, with assertions unchanged except the three §2f names
that move from `Flush` to `EndFight`; `CinderfaultSpellCaptureTests` green;
plus five new PlayMode tests for the contracts the brief's proof #5 names and
revision 2 left unstaffed — **a tail alive across a `Play`** (T6), **two casts
on one target coexisting** (T9/§4), **`Cancel` mid-flight releasing only that
cast**, **a target dying mid-flight** (§4), and **nothing surviving
`EndFight`** (T10). *Evidence, replacing revision 2's "compared by eye":*
Cinderfault's `timing.json` (`CinderfaultSpellCaptureTests.cs:340`) compared
field for field against M0's, and `ImpactDelayFor` pinned as a **literal** for
each of the five M0 spells before and after — a number, not an impression.
*Three hours because this is the risky commit and its whole value is that
nothing changes.*

**M4 — Renderers, clock and pools (4 h).** `SpellVfxPlayer` reduced to the
sprite renderer, losing `PlayRoutine`/`Now()`/`ClockOverride`;
`SpellPerformancePlayer` takes the clock and reads **`Time.time`** (§10 L1 —
promoted here from M8, because it is the same edit as taking the clock and
because `GlobalStateLintTests.cs:92-94` and `TestGlobals.cs:71` have to move
in the same commit or the lint fails); `SpellParticleRenderer`;
`SpellEmitterSim` with its own EditMode tests (closed-form position at t, seed
repeatability, a 50-tick step matching a 1-tick step); the **three**
`FightHudSpec` constants and the **content-derived, per-band** pins of §4; the
enlarged sprite pool, the enlarged ground pool and the new particle pool in
`FightScreen`; `ScreenRegistry` wiring.
*Evidence:* the restore-on-release test with the full list from §4; the two
defensive `SetFacing(1f)` calls deleted; the authored-order draw test; the
pool-exhaustion test proving the hit cue still fires; the pause test
(`Time.timeScale = 0`, advance frames, assert the frame index does not move);
`run_tests_parallel.ps1 -BuildScenes`.

*Plus revision 4's three groups, all with observable assertions and none "by
eye":*

**Birth-time source sampling (§3 T13).** One tick of 0.20s taken across a
0.25s flight with `rate: 40`: assert **eight** particles exist (0.20 × 40), that
their eight `p0` values are eight *distinct* points, and that each equals
`PositionOf(core, birth)` for its own birth time — so the drops lie along the
core's path and not stacked at the step's end. A second assertion on the same
tick: the ages descend from 0.20 to 0.005, none of them zero. A third: with
`inherit: 0.35`, a particle born at `t` has
`v0 - direction*speed == 0.35 * VelocityOf(core, t)`, which is non-zero at
mid-flight and larger later, because the `t*t` ease accelerates. And a fourth,
the one that would have caught the whole class: **fifty 4ms ticks and one 200ms
tick produce the same eight `p0` values**.

**Overlap and pooling, the ground band included (the brief's Cinderfault
proof).** Four PlayMode tests:

1. *Two Cinderfaults back to back*, the second beginning while the first fault
   is still cooling. **The second gets its own ground member; it does not
   supersede.** Assert the two casts hold *different* members of the ground
   pool, that the first's frame index keeps advancing after the second begins
   (it was not restarted — the `SpellVfxPlayer.cs:123` behaviour this design
   removes), and that both are released at their own ends.
2. *Cinderfault and a Water tail overlapping on one target.* Assert the Water
   cast's core/wake/splash and the Cinderfault's eruption on that same slot hold
   distinct `effects` members, that the fault holds a `ground` member while they
   do, that neither cast's handle owns a renderer belonging to the other, and
   that both hit cues fired once each.
3. *Pool exhaustion where the ground member is the one that cannot be
   allocated.* Drive `SpellGroundRenderers` casts to hold every ground member,
   then cast Cinderfault. Assert: no ground layer drawn, one line logged, and —
   the point — `PaintVitals`/`ShowAmount` still fire **exactly once** at the
   authored cue time (T7), the beat's duration unchanged to the frame. Not
   suppressed, not duplicated, not delayed.
4. *`EndFight` mid-fade on a ground member.* Begin Cinderfault, tick to inside
   the fault's `fade`, call `EndFight()`. Assert the member is released, the
   full restore list of §4 applied to it (sprite null, alpha 1, `localScale.x`
   1, parent and `sizeDelta` back to the pool's), and the ground pool reports
   every member free — the state the *next* fight starts from.

**Beat-speed synchronization (§3 C1).**
`ASpellAndItsBeatCrossTheHitCueOnTheSameFrame`, both halves, at
`BeatSpeedMultiplier = 1` and at `60`.

*Four hours still: revision 2's three and a half plus the clock move it had
parked in a one-hour milestone. The seven tests above are assertions against
machinery this milestone already builds — the pools, the sim and the clock —
rather than new machinery, which is why they do not move the number.*

**M5 — Water (3 h).** Four recipes, four folders, the `skills.json` element
block of §6d, the extra preview sample, tuning against captures. Both drift
tests extended: `EveryFolderASkillPlaysHasARecordedProvenance` (`:126`) to
walk `layers[].path` and `layers[].emitter.path`, and
`NoSkillTimesABeatToAFrameItsFolderDoesNotHave` (`:158`) to range-check
`layers[].startFrame` — the brief's "frame ranges", which revision 2 did not
answer and which is where `startFrame: 2` gets its only guard. *Evidence:*
both drift tests green with the extensions, and each **failing first** against
a deliberately bad `startFrame` (a lint that scans nothing passes everything);
`preview.ps1 -Spell prismatic_orb` four pictures. *Three hours, most of it
tuning, which is iteration against pictures rather than code.*

**M6 — Cinderfault re-authored (1 h).** The §7 block replaces the legacy
ground fields. *Evidence:* the eight named Cinderfault tests green with no
edits; `timing.json` compared against M0's. *One hour because if M3 was right
this is a content edit.*

**M7 — Synthetic two-burst (0.5 h).** §8, as a PlayMode test.

**M8 — Real-time recording and allocation (1 h).** The clock fix landed in M4,
which is what makes this milestone mean anything. `tools/screenshot.ps1
-Runtime -RuntimeFilter SpellRuntimeCaptureTests` records a fight at
`Time.captureFramerate = 60` (`RuntimeScreenshotTests.cs:26`) including **two
consecutive casts while the first cast's droplets are still visible**.

**The allocation test of §4**, as one PlayMode test: `ProfilerRecorder` on
`ProfilerCategory.Memory` / "GC Allocated In Frame", the four-part warm-up, 120
frames at `Time.captureFramerate = 60` of a Water cast over a live Cinderfault
tail, measured twice (idle and casting). Two assertions: `Tick` allocates zero
via `Is.Not.AllocatingGCMemory()`, and the per-frame median difference between
the two runs is zero bytes, with the worst frame's delta in the failure message.
A recorder that reports `Valid == false` fails the test rather than passing it.

*Evidence, so this is an observation and not a
judgement:* the recording's own fixture asserts, on the frame the second cast
opens, that at least one particle owned by the first cast's handle is still
alive — the pictures then show a human what the assert already knows.

**What `-Runtime` can and cannot capture.** It **can**: drive a real scene with
`Update()` ticking, at a deterministic 1/60s per frame regardless of how long a
frame really takes (`RuntimeScreenshotTests.cs:23-26`), writing PNGs to
`tools/screenshots/runtime/`. It **cannot**: run in the commit gate, because
the gate passes `-nographics` and `CanvasCapture.IsSupported` is false there
(`:45-51`) — so this is evidence produced on demand, never a gate. It also
**wipes the previous run's PNGs**, so a comparison set must be copied out
first. And it cannot produce a video; it produces a frame series, and a
video is `ffmpeg` over that series.

**M9 — Gate and docs (1 h).**
`run_tests_parallel.ps1 -BuildContent -BuildScenes`, ~4 minutes.
`docs/ART_PIPELINE.md` §5b gains the layer vocabulary beside the existing
`vfx` block table; `docs/CODE_MAP.md` gains the new files;
`docs/handoffs/spell_layers/GAP_AUDIT.md` is written section-by-section
against the brief, per `docs/HANDOFF_TEMPLATE.md`.

**Total: 18 agent hours** (0.5 + 2.5 + 1.5 + 3 + 4 + 3 + 1 + 0.5 + 1 + 1),
across three or four sessions. Revision 2 also said 18, and the identical
total is a coincidence of two changes that cancelled: the review **removed**
half an hour from M2 by cutting the schedule-reference machinery, and **added**
half an hour to M4 for the clock move it had hidden inside M8. Revision 1 said
6-10, and the gap to that is itemisable: revision 1 costed four hardcoded
blocks bolted to the existing player (roughly M4 and M5 alone); this costs a
pure scheduler with its test suite, a pure emitter simulation, an adapter that
must reproduce shipped behaviour exactly, one risky commit whose value is that
nothing changes, and two proof spells instead of one.

If the estimate is wrong it is most likely wrong at M3, where "every existing
test green through a new path" is the kind of work that is either two hours or
six. The second most likely is M1's `Copy()` pin, which will find whatever
else the hand-written block has been quietly dropping.

---

## 10. Observed limitations

Things the repository is measured to do, that constrain or contradict the
brief. Each has evidence and a proposed nearest thing.

**L1 — The spell clock ignores pause and ignores frame-rate capture, and both
matter.** `SpellVfxPlayer.Now()` returns `Time.realtimeSinceStartup`
(`:60`) while `FightBeatPlayer` waits on `WaitForSeconds`, which is scaled
engine time. Two consequences, both real:

- `SystemMenuController` sets `Time.timeScale = 0` on open (`:226`) and the
  system menu is reachable mid-fight (`FightScreen.cs:319-328`,
  `ScreenRegistry.cs:126`). So opening the menu mid-cast **freezes the beat
  and fast-forwards the spell**: the animation keeps running on wall time and
  is over by the time the player resumes. That is a live defect today, and it
  makes the brief's "pause/time-scale behaviour applies consistently to every
  layer" impossible to honour without changing this line.
- `Time.captureFramerate` (set to 60 by `RuntimeScreenshotTests.cs:26`)
  advances `Time.time` and `Time.unscaledTime` by exactly 1/60 per frame but
  does **not** touch `realtimeSinceStartup`. A deterministic real-time
  recording — the brief's proof #7 — would therefore sample the spell at wall
  speed while frames advance at a fixed step, and the recording would not show
  what a player sees.

*Nearest thing, and it is the right thing:* the performance player's clock
reads **`Time.time`** — the same clock `WaitForSeconds` uses — with the
`ClockOverride` seam kept in shape but **moved to
`SpellPerformancePlayer`** along with the clock, which drags
`GlobalStateLintTests.cs:92-94` (a literal regex on the old class name) and
`TestGlobals.cs:71` with it. Under normal play `Time.time` and
`realtimeSinceStartup` are indistinguishable; they differ only at pause, under
capture, and under any future slow-motion, and in all three cases `Time.time`
is the correct answer. Pinned by a test that sets `Time.timeScale = 0`,
advances frames, and asserts the layer's frame index does not move
(`Time.timeScale` is already a policed global,
`GlobalStateLintTests.cs:84-86`).

**Scheduled into M4, not M8.** Revision 2 parked it inside M8's one hour as "a
prerequisite". It is not a prerequisite bolted onto a recording milestone — it
is the same edit as moving the clock out of the renderer, and doing it
anywhere else means the lint is red for a commit.

**"Reads `Time.time`" is half the answer; §3's C1 is the other half.** A clock
alone does not make a spell and its beat agree — the *conversion* has to be
shared too, because the beat's waits are `Scaled(seconds)` and a scheduler
holding authored seconds against `Time.time` would be 60× out under a test
multiplier. C1 names `FightBeatPlayer.Scaled` (`:105-106`) as the one
conversion, `Time.time` as the one clock, and the test that asserts the two
cross the cue on the same frame at multiplier 1 and 60.

**L2 — The impact delay is a function of PNGs on disk.**
`ImpactDelayFor` (`FightController.SpellVfx.cs:43-50`) multiplies
`beat.Vfx.seconds` by `CombatBeat.ImpactFraction(impactFrame, frameCount)`,
and `frameCount` comes from `FrameSequenceLoader`. Missing art gives
`frameCount == 0` and a 0.5 fraction (`CombatBeat.cs:230-233`). The brief asks
that missing cosmetic artwork not affect gameplay timing; for **version-1**
content this plan delivers that (the cue is authored in seconds). For
**version-0** content it deliberately does not, because fixing it would retime
every shipped spell whose folder is momentarily unreadable, which is a
different and larger change. Stated rather than quietly fixed.

**L3 — Byte-identical captures cannot be promised, and the brief already
concedes this.** The available deterministic comparison is numeric: the
`timing.json` `CinderfaultSpellCaptureTests` already writes (`:340`, described at `:292-304`) —
impact frame indices, the ground box's rect, every living slot's centre x and
ground y in one parent-local space. The plan extends that file rather than
introducing a pixel-diff harness, and pictures remain evidence for a human.

**L4 — `ContentSchema` cannot document a `List<>`.** `ElementType`
(`ContentSchema.cs:197`) unwraps arrays only. `layers` must therefore be
`SpellLayer[]`. Not a limitation on the design, but it is a constraint the
implementer will otherwise discover through a confusing generated table.

**L5 — Cross-cast draw order is allocation order.** Within a cast, authored
array order is draw order and that is enough for the brief's
"contact/foreground spray" — a spray authored after its splash gets a higher
member index and draws over it (§4). What is *not* controlled is the order
**between two casts** overlapping in one pool: it is whichever got the lower
member indices. The fix, if it ever reads wrong, is a `foreground` band as a
further pool node between `FightScreen.cs:316` and `:317` — one screen edit
and a `-BuildScenes`. Not built now because nothing demonstrates the need,
which is the same test `docs/PLAN_SPELL_FEEL.md` §"Extend existing pools only
when exhausted" applies.

**L6 — Half the water delivery is currently unreachable.**
`water_particles_8.png` and `water_wake.png` are read by no recipe and cut into
no folder; verified by reading
`Art/Sheets/recipes/prismatic_orb_water.json`, whose `sources` names only the
core and contact sheets. M5 is where they get seats; until then, any claim
about how the water spell will look is about two of its four files.

**L7 — `prismatic_orb` has no `vfx` block at all.** Verified by reading its
entry in `Assets/_Project/ContentData/skills.json`. So the "existing Water
baseline" the brief's proof #1 asks for **does not exist in game** and has to
be manufactured (M0) before it can be captured. Note also that this file is
uncommitted and being edited by a concurrent session; the reading above is of
the working tree as of this document's date.

**L8 — `PlayContactFx` borrows spell pool members 0 and 1.**
`FightController.SpellVfx.cs:329-330`, safe today only because
`FightBeatPlayer` calls it exclusively for beats with no authored VFX
(`:498`, `WantsContactFx` at `:694`). Once ownership is per-cast, that borrow
must go through the same allocator or it will steal a live renderer — and T6
makes it *more* likely, because after T6 a tail from the previous round can
still hold members 0 and 1 when the next beat's melee blow lands. Converting
it is a one-line change inside M3 and is called out here so it is not
discovered as a bug.

**L9 — `Copy()` is a hand-written field list and has been dropping fields.**
`SpellPresentation.Copy()` (`:268-285`) and the test that guards it
(`CinderfaultSpellTests.cs:150-178`) are both written out by hand, so a field
added and not copied is silent — the identical shape AUDIT #60 records firing
twice in `FightEncounterAdapter` (`transform`, then `bookOnly`/`bookTier`).
M1's reflection pin closes it for this type. Recorded as a limitation rather
than a fix because the same shape exists in the four other chain files #60
names, and generalising it is that finding's work, not this plan's.

---

## 11. Assumptions

Separate from the above, because these are choices rather than measurements.
Any of them flips on a word.

1. **Three renderer kinds is the right first set** — sprite sequence, still,
   ballistic emitter. Taken directly from the brief's own table. No beam, no
   trail-mesh, no persistent field.
2. **Audio is not a layer.** `sfxPath` and `castSfxPath` stay on
   `SpellPresentation` and stay fired by `FightBeatPlayer` (`:441`, `:494`).
   The brief's "what renders" column lists three visual kinds and no audio,
   `docs/PLAN_SPELL_FEEL.md` §"Correct sound semantics" already settled the
   two-cue model, and adding per-layer audio now would be the speculative
   engine the owner rejected. If a layer ever needs its own transient, a
   `sfxPath` on `SpellLayer` is an additive change under `layerFormat: 1`.
3. **Linear drag plus constant gravity covers water, fire, earth and wind.**
   It is the smallest model that produces the brief's "drops inherit part of
   forward velocity, separate visibly, slow through drag and fall through
   gravity", and it is closed-form, which §5b spends.
4. **Emitters are cosmetic and droppable; sprite layers are not.** Under
   pressure the droplets thin out before a crown disappears.
5. **The Water numbers in §6d are first guesses.** Travel 0.25s, cue +0.10s,
   40 drops/s, 18-drop burst. All tunable at battlefield scale; none of them
   is a measurement.
6. **Existing spells are migrated at runtime, not in authoring.**
   `lightning_bolt`, `mud_burst`, `frost_flare`, `bog_mud_burst` and
   `golem_boulder` keep their single blocks in `skills.json` and pass through
   `ToLayers()`. Only Cinderfault is re-authored, and only to prove the model
   can say what it already does.
7. **`Assets/_Project/Art/Sheets/Spells/prismatic_bolt/` keeps its name.**
   Nothing in the runtime reads it; the recipes bridge it (§6a).
8. **12 renderers and 64 particles are reservations, not ceilings.** The
   numbers themselves are the weakest thing in this document — revision 2's
   derivation described a three-target Water cast that `DamageSingle` cannot
   produce. What carries the weight is not the constants but the
   content-derived pin in §4 that fails naming the skill when content outgrows
   them, and the overflow policy that makes exceeding them cosmetic.
9. **The travel ease is one house rule, not an authored word** (§2b). Taken
   from what ships (`SpellVfxPlayer.cs:240-241`). A second spell wanting a
   different ease makes it a word; none does yet.
10. **A projectile fans out per target even though its `place` is
    caster-side** (§2b). This is today's behaviour and the only exception in
    the scope table; it would be wrong for a spell that wanted one shared
    projectile splitting at the end, and no such spell exists.

---

## 12. What this deliberately does not do

- **No gameplay change of any kind.** No damage formula, no targeting, no
  element-choice mechanic, no cooldown, no beat ordering. The one behavioural
  change outside presentation is L1's clock, which fixes a pause defect.
- **No audio fields** beyond the two that exist (§11.2).
- **No graph editor, scripting language, reflection plugin framework, or
  interface per class.** The brief forbids all four; `docs/CODE_STANDARDS.md`
  §5 independently rejects the last one.
- **No Unity Particle System, no prefabs, no `SceneBuilder` layout knowledge**
  (§5f, `CLAUDE.md` rule 1).
- **No second simulator.** The Python preview keeps rendering sprite layers
  and declines to draw emitters rather than approximating them (§5b).
- **No collision, sub-emitters or colour curves** on particles.
- **No new semantic draw band.** Two categories, `ground` and `effects`, both
  already built. The particle pool added in §4 is a second *node* inside
  `effects`, not a third band; a `foreground` band waits on L5.
- **No schedule references** (`<id>:start` / `<id>:end`) and therefore no
  dependency graph, cycle rule or topological ordering — deferred until a
  second user, §2b.
- **No `place: stage`** — deferred on the same test, §2b.
- **No migration function** for a `layerFormat: 2` that does not exist; the
  policy is written, the code is not (§2e).
- **No rename of gameplay content**, and no rename of the art delivery folder
  (§6a).
- **No migration of the four remaining element packs** (Earth, Fire, Wind) —
  each is an art delivery, and this plan proves the shape with the one that
  exists.
