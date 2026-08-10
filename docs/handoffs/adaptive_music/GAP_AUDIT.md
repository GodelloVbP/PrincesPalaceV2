# Gap audit — Adaptive music, handoff vs. built

Audited 2026-08-07, immediately after the implementation pass, against the
handoff `README.md` in this folder. Format per `docs/HANDOFF_TEMPLATE.md`.
Verdict ∈ **match** / **partial** / **missing** / **deliberate-deviation**.

Overall: **the system is built and the design goal holds.** Adding a floor's
music is a folder plus a JSON block, with no code change — that is asserted by
tests rather than claimed. Every architectural point survives: one shared
`dspTime` per set, stems that never stop, two distinct transition mechanisms,
one gain per set, and volume re-asserted every frame.

The single thing that cannot be verified is the thing the handoff already puts
out of scope: **no stems exist**. The game therefore still plays the two
pre-layering beds today, through a fallback that is exercised by the suite
rather than assumed. The moment `Resources/Audio/Music/Floor1/` has nine files
in it, the layered path takes over with no further code change — and two
already-written tests go live to police it.

---

## Systems (§9, "What changes in existing code")

| # | Handoff item | Built | Verdict |
|---|---|---|---|
| 1 | New `MusicLayerManifest` loader, shaped like `AudioLevels` | `MusicLayers.cs` — Resources path, `JsonUtility`, cache, `Reset()`, plus an `Initialize()` injection seam the tests lean on hard | **match** |
| 2 | `Voice` widens from 2 AudioSources to N, holds `t0`/tempo/gain | `MusicController.Voice`. Sources are **pooled and grown on demand** rather than fixed at nine — the legacy intro+loop pair is just a two-stem set as far as the class is concerned | **match** |
| 3 | `SetVolume` takes a tier and walks the stem array | Split further: a voice carries a per-stem `Mix` and a whole-voice `MasterScale`, and `Update` is the only thing that ever writes `AudioSource.volume`. See §"One deviation worth arguing about" below | **deliberate-deviation** |
| 4 | New `SetTier(tier)`, cheap and idempotent | `MusicController.SetTier`. Idempotence lands in `Voice.TargetsFor`, which reports "nothing moved" and skips the fade | **match** |
| 5 | `TrackFor` becomes `TierFor` and splits Elite from Boss | `TierFor` is the decision; `TrackFor` survives as `TierFor` plus a documented collapse, so the two cannot disagree about what an elite room is | **match** |
| 6 | New `SetFloor(int)`, triggering the voice ping-pong | `SetFloor(int?)` — **nullable**, because "not in a run" is a real third answer. See the note below | **deliberate-deviation** |
| 7 | `GameplayManager.ShowOverlay` calls the new entry point | Still one call site, now passing `CurrentRun?.floor`. `PlayForOverlay` composes `SetFloor` + `SetTier` rather than replacing them | **match** |
| 8 | `Resources/Audio/README.md` gains a stems section and the import exception | Both, plus a heading of its own for the `audio_levels.json` trap | **match** |

## The manifest (§2)

| # | Item | Verdict | Note |
|---|---|---|---|
| 9 | One file to add a floor | **match** | `Resources/Audio/music_layers.json`, carrying its own `_readme` |
| 10 | Stem path is `folder + "/" + stem`, extensionless | **match** | Trailing slashes tolerated |
| 11 | `stems` order is the mix order and authoritative | **match** | Stated in the type, the manifest and the resolver's header |
| 12 | Nine is not hard-coded | **match** | Tested at 1, 5 and 12 stems |
| 13 | `gain` per set, not per stem | **match** | Refusing to put stems in `audio_levels.json` is called out in three places, because it is the one that would silently ruin a mix |
| 14 | Two floors may share a set | **match** | Tested |
| 15 | Unmapped floor falls back | **match** | And a floor mapped to a *nonexistent* set is refused instead — see below |
| 16 | A stem that fails to load leaves that layer silent | **match** | Partial failure is graceful, total failure hands over to the fallback |
| 17 | Missing manifest falls back to `MusicTrack` | **match** | As do a malformed one and an invalid one, each logging why |
| 18 | JSON shape as written in §2 | **deliberate-deviation** | `tiers` and `floors` are arrays of records, not name-keyed objects. **JsonUtility cannot deserialise a dictionary at all**, so the handoff's literal JSON is unparseable by Unity. The array form is also what every other content file here uses. The design goal is untouched |

## Intensity tiers (§3)

| # | Item | Verdict |
|---|---|---|
| 19 | Four named tiers, not nine switches | **match** |
| 20 | `ambient` / `fight` / `elite` / `boss` mapped from overlay + room | **match** |
| 21 | Elite split from Boss | **match**, with a test that pins them as different so a future merge cannot pass quietly |
| 22 | [PROPOSED] additive per-stem combat overrides | **missing** — see "What was left out" |

## Playback model (§4) and quantisation (§5)

| # | Item | Verdict | Note |
|---|---|---|---|
| 23 | Intensity change fades stems, schedules nothing | **match** | Tested: an intensity change must not re-assign a single clip |
| 24 | Floor change crossfades whole voices | **match** | The existing 1.5s ping-pong, unchanged in shape |
| 25 | All stems scheduled against ONE `dspTime` | **match** | And `t0` is stored on the voice, which is what makes §5 possible |
| 26 | 0.4–0.8s layer fade vs. 1.5s track crossfade | **match** | 0.6s |
| 27 | Land the change on a downbeat | **match** | `MusicClock`, pure and unit-tested including the exactly-on-a-boundary case |
| 28 | [OPEN] bar or phrase, per set | **resolved** | `quantiseBars` per set, defaulting to 1. Phrase quantisation is the same arithmetic with a bigger window, which is why it could be a field instead of a second code path |
| 29 | [OPEN] boss intro: drop / sting / intro stem | **resolved** | The recommended option — an optional `stingPath` per set, fired through `SoundController` when a set first reaches Boss |

## Tests (§10)

Every test the handoff asks for exists, with one reshaped:

| # | Asked for | Built |
|---|---|---|
| 30 | Every stem path resolves to a real clip | **Reshaped.** A set must be either **fully present or entirely absent** — never half. With no stems recorded, "all nine load" fails for a reason that is not a bug and "nothing loads" passes forever; the half-delivered case (eight files and a typo in the ninth) is the one that is *always* wrong, and it is what the original check was for. Goes live on its own the day the folder appears |
| 31 | **Every stem in a set has an identical sample count** | Built, and identical sample *rate* too. Skips a set with no clips yet |
| 32 | Tier indices in range | Resolver test |
| 33 | Tier names are ones the code knows | Resolver test |
| 34 | Every floor maps to a set or the fallback | Resolver tests, both directions |
| 35 | Missing / malformed / missing-stem all degrade | Three tests, including one that drives the real controller against a set whose folder does not exist |
| 36 | `TierFor` returns the right tier for each room | Built, plus one pinning Elite ≠ Boss |

The layered path is driven **for real** rather than mocked: a stand-in set
points at the four music files that already exist, so `MusicController` loads
clips through `Resources` exactly as it will in production. A set of
synthesised `AudioClip`s could never be found by `Resources.Load`, and every
such test would have silently exercised the fallback while claiming to test
layering.

---

## One deviation worth arguing about

**§9.3 asks for `SetVolume(tier)`. What is built splits volume in two:** a
per-stem `Mix` and a per-voice `MasterScale`, multiplied together (with the
set's gain and the player's slider) in `Update`, which is the only place
`AudioSource.volume` is ever written.

The reason is that the two transitions have to *compose*. A floor change can
legitimately begin while a tier fade is still moving individual stems inside
the outgoing set. With one volume number per source, the crossfade and the
layer fade would both be writing it and the last one to run each frame would
win — an intermittent stutter that only appears when a player walks into a
boss room on the same frame the floor ticks over. With the two separated,
neither routine knows about the other and both stay live against the pause
menu's slider for free.

**§9.6 asks for `SetFloor(int)`; it is `SetFloor(int?)`.** Null means "not in a
run", which plays the manifest's `hub` set. Collapsing that onto floor 1 gets
the Hub wrong in both directions: a player who just cleared floor 3 would keep
hearing floor 3's song underneath the shop, and the manifest's own `hub`
field — which exists precisely so the hub can share a floor's set or not —
would never be read at all.

---

## What was left out

**The [PROPOSED] per-stem combat overrides (§3).** The handoff says "build the
tier system first; leave a hook". The hook is the architecture rather than an
API: the mix is computed per stem from a target array, so an additive override
is a second contributor to that array and not a new mechanism. No public method
was added for it, because there are no stems to add and no way to say which
index means "tension" until a real set exists — an API whose only possible
argument is a guess is worse than none.

**Everything in §12.** The music itself, the `Sfx` side, and mixer groups /
ducking are all untouched.

---

## Open decisions (§11), and what was done with each

1. **Boss intro** → **sting**, the recommended option. Optional `stingPath` per
   set, fired through `SoundController` on first entering Boss. Data-driven, so
   dropping it later is deleting a manifest field rather than code.
2. **Quantise to bar or phrase** → **authored per set**, `quantiseBars`,
   default 1. Neither answer is forced globally because the right one depends
   on the song.
3. **Import setting** → documented as Compressed In Memory with the reasoning,
   and flagged as **unverified**. Cannot be confirmed without a real set; the
   stated fallback (release the outgoing voice's clips after a crossfade) is
   recorded in the README.
4. **Stem count as data** → confirmed, and tested at 1, 5 and 12.
5. **Does the hub share floor 1's set** → the manifest supports either; it
   currently says yes, in one line anyone can change. A missing `hub` falls
   through to `fallbackSet`.
6. **Per-stem combat overrides** → hook only, no API. See above.
7. **Boss intro cutscene / run-end** → nothing to do. Neither screen exists;
   run-end returns to the Hub, which is an overlay change and already handled.

---

## One rule the handoff did not specify

**A floor mapped to a set id that does not exist is an ERROR; a floor with no
mapping at all falls back silently.** The handoff only describes the second.
The difference is worth having: one is a typo and one is a schedule, and a
typo'd set id that quietly fell back would give you floor 1's music on floor 4
with nothing anywhere saying why.
