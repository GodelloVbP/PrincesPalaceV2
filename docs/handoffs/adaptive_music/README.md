# Handoff: Adaptive Music — vertical layering, per floor

Design session 2026-08-07. A **systems** handoff, not a screen handoff.

One song per floor, exported as **9 separate stems**. All nine play
simultaneously and continuously; intensity is expressed by fading stems in
and out, never by starting or stopping them. The run map hears a minimal
bed, a normal fight adds layers, a boss brings everything together.

The technique is called **vertical layering** (or vertical remixing) and it
is the standard approach for adaptive game music. Keeping every stem
running is what guarantees they can never drift out of sync — the moment
you start and stop tracks independently, you inherit timing slop and the
"weird transitions" this design exists to avoid.

**Design goal of this document: adding a new floor's music must be
"drop 9 files in a folder + add one JSON block." No code change, ever.**

Status markers: **[LOCKED]** decided in session · **[PROPOSED]** suggested,
not confirmed · **[OPEN]** needs a call before building.

---

## 1. What exists today

`MusicController.cs` is already well-built and gets several things right
that this design depends on. Read it before changing it.

| Thing | Where | Keep? |
|---|---|---|
| Self-bootstrapping, survives scene loads | `MusicController.cs:77` | **Yes** |
| Two "voices" ping-ponged for a 1.5s crossfade | `MusicController.cs:70` | **Yes**, but repurposed — see §4 |
| A voice is a *pair* of AudioSources (intro + loop) | `MusicController.cs:38` | Widened to 9 |
| `PlayScheduled` against `AudioSettings.dspTime` | `MusicController.cs:214` | **Yes — this is the critical one** |
| Samples-not-seconds for scheduling math | `MusicController.cs:219` | **Yes** |
| Volume re-asserted every frame from `GameSettings.MusicVolume` | `MusicController.cs:246` | **Yes** |
| Missing clip degrades to silence, never throws | `MusicController.cs:203` | **Yes** |
| `MusicTrack` enum → hardcoded path switch | `Music.cs:30` | **No — replaced by the manifest** |
| `AudioLevels.GainFor` per-clip loudness correction | `AudioLevels.cs:53` | **Careful — see §7.3** |

The existing controller already schedules against the audio clock rather
than a coroutine, with a comment explaining exactly why. That instinct is
the foundation of this whole system; do not regress it.

---

## 2. The manifest — the part that matters

New file: `Assets/_Project/Resources/Audio/music_layers.json`

This is the only thing anyone touches to add a floor. It follows the
same posture as `audio_levels.json`, `skills.json`'s `sfxPath`, and
`talents.json` — content as a reviewable data diff, not a code change.

```json
{
  "sets": [
    {
      "id": "floor_1",
      "folder": "Audio/Music/Floor1",
      "bpm": 96,
      "beatsPerBar": 4,
      "gain": 1.0,
      "stems": [
        "01_pad",
        "02_bass",
        "03_kick",
        "04_perc",
        "05_arp",
        "06_strings",
        "07_brass",
        "08_choir",
        "09_lead"
      ],
      "tiers": {
        "ambient": [0, 1],
        "fight":   [0, 1, 2, 3, 4],
        "elite":   [0, 1, 2, 3, 4, 5, 6],
        "boss":    [0, 1, 2, 3, 4, 5, 6, 7, 8]
      }
    }
  ],
  "floors": {
    "1": "floor_1",
    "2": "floor_2",
    "3": "floor_3"
  },
  "hub": "floor_1",
  "fallbackSet": "floor_1"
}
```

**Adding floor 4:** create `Resources/Audio/Music/Floor4/`, drop nine
files in it, copy a `sets` block, add `"4": "floor_4"` to `floors`. Done.

Rules:

- A stem's full Resources path is `folder + "/" + stems[i]`, extensionless,
  matching the convention in `Resources/Audio/README.md`.
- **`stems` order is the mix order and is authoritative.** Tier arrays are
  indices into it. Nine is the expected count but nothing should hard-code
  it — a set with 5 stems or 12 must work.
- **`gain` is per SET, not per stem.** See §7.3, this matters a lot.
- Two floors may point at the same set id. Reuse is free.
- **Graceful degradation, house style:** a floor with no mapping falls back
  to `fallbackSet`. A stem file that fails to load leaves that layer silent
  while the other eight play normally. A missing manifest falls back to the
  current non-layered `MusicTrack` behaviour rather than silence.

---

## 3. Intensity tiers [LOCKED]

Four named tiers, not nine independent switches. Nine toggles is more
control than will ever be used and turns every new song into a
nine-dimensional authoring problem instead of a four-state one.

| Tier | When | Rough feel |
|---|---|---|
| `ambient` | Hub, run map, every non-fight overlay | Minimal bed |
| `fight` | `RoomType` normal fight | Rhythm section joins |
| `elite` | `RoomType.EliteFight` | Most of the arrangement |
| `boss` | `RoomType.Boss` | Everything |

This maps cleanly onto the existing `MusicController.TrackFor(state, room)`
(`MusicController.cs:132`), which already resolves overlay state + room type
down to a single value — it just returns a tier instead of a `MusicTrack`.
Note it currently lumps Elite and Boss together; this design splits them.

### [PROPOSED] The extension worth building toward

Tiers describe *where you are*. The system can also react to *what is
happening*, by letting combat state add individual stems on top of the
current tier's set:

- Shawn below 33% HP (his wool engine running hot) → add a tension layer.
- Black Ram Mode active for its 3 turns → add drums.
- Last enemy standing → drop back to a thinner mix for the kill.

That turns the music from a label on your location into a score for your
actions, and it reinforces the exact mechanics in
`docs/handoffs/shawn_talent_rework/`. Build the tier system first; leave a
hook for additive per-stem overrides.

---

## 4. Playback model

**Two levels of transition, and they are different mechanisms.** This is
the central architectural point.

### 4.1 Intensity change (within a floor) — fade stems

The nine sources keep playing. Only their volumes move. Nothing is
scheduled, stopped, or restarted, so sync is structurally impossible to
lose.

### 4.2 Floor change — crossfade whole sets

This is what the existing two-voice ping-pong is *for*. A voice grows from
2 AudioSources to N (one per stem). Voice A holds floor 1's nine stems,
voice B holds floor 2's nine. Changing floor crossfades between voices
exactly as `CrossfadeRoutine` already does — the outgoing voice fades its
whole set down, the incoming fades its active tier up.

That means **2 × 9 = 18 AudioSources** on the controller GameObject, with
all 18 live only during the 1.5s of a floor crossfade. See §8 for why the
import settings have to change because of this.

### 4.3 Starting a set

All stems in a set are scheduled against **one shared `dspTime` value**:

```
double t0 = AudioSettings.dspTime + ScheduleLead;   // ScheduleLead = 0.05
foreach (var source in voice.Stems) source.PlayScheduled(t0);
```

Calling `Play()` on nine sources in a loop can leave them a frame or two
apart, which is audible as phasing on anything percussive.
`MusicController` already uses `PlayScheduled` for its intro/loop handoff
and already documents why — apply the same reasoning across the stem set.

**Store `t0` on the voice.** §5 needs it.

### 4.4 [OPEN] What happens to the boss intro

`MusicTrack.BossBattle` currently has a one-shot intro
(`Music.cs:52`, `Boss_music_intro`) that plays once before its loop, with
gapless scheduling built for it. In a layered system that mechanism has no
obvious home — layering itself provides the escalation.

Three options, no call made:

1. **Drop intros.** Simplest. The boss tier arriving all at once on a
   downbeat is already dramatic.
2. **Keep it as a sting**, fired through `SoundController` as a one-shot
   over the top of the running layers. Keeps the drama, no scheduling
   complexity.
3. **Per-set optional intro stem** that all nine loops are scheduled
   behind. Most faithful to what exists, most complex.

Recommend **2**.

---

## 5. Bar-quantised transitions [PROPOSED]

An instant volume jump to or from zero clicks. Worse, a layer arriving
mid-phrase sounds like a bug rather than a swell. Two fixes, and you want
both:

**Fade, don't switch.** Roughly 0.4–0.8s for a layer change — much shorter
than the 1.5s used for whole-track crossfades.

**Land the change on a downbeat.** Because every stem in a set started at a
known `t0`, the next bar boundary is exact arithmetic, not a guess:

```
barSeconds  = 60.0 / bpm * beatsPerBar
elapsed     = AudioSettings.dspTime - t0
nextBar     = t0 + Ceil(elapsed / barSeconds) * barSeconds
```

Begin the fade at `nextBar`. This is the single thing that makes the system
feel composed rather than reactive, and it is only possible because §4.3
starts everything from one shared timestamp — which is the real reason that
detail matters.

`bpm` and `beatsPerBar` are in the manifest per set for exactly this.

**[OPEN]** — quantise to the bar, or to a phrase (4 or 8 bars)? Bar is
responsive; phrase is more musical but can leave up to ~20s of lag at
96 BPM. A `quantiseBars` field per set would let it be authored rather than
decided globally.

---

## 6. Export requirements — for whoever writes the music

These are the authoring constraints. Getting any of them wrong produces
problems that cannot be fixed in code.

1. **Identical length, tempo, key, and sample rate across all nine stems.**
   Non-negotiable. Different lengths mean the loops walk apart from each
   other over minutes.
2. **Export all stems from the same session in one pass**, so they are
   sample-aligned from the first sample. Do not render them at different
   times or trim them individually.
3. **No head trimming.** If one stem starts with 40ms of silence removed
   and the others do not, the set is permanently out of phase.
4. **Bypass master-bus processing when printing stems.** This is the most
   common stem-export mistake: a compressor or limiter on the master
   applied to each stem individually means the nine stems no longer sum to
   the mix you wrote. Print with the master chain off, or use a stem-export
   mode that accounts for it.
5. **Reverb and delay tails must live inside the stem that caused them.**
   If reverb is on a shared send, either freeze it per-stem or give the
   send its own stem. Otherwise muting the lead leaves its reverb hanging
   in another layer.
6. **Loop seamlessly at the same point.** Every stem's tail must wrap into
   its own head. Export an exact whole number of bars.
7. **Compose bottom-up, not top-down.** Write the two-stem `ambient` layer
   first, as a complete piece that stands on its own, then add layers that
   enrich without being load-bearing. If you write all nine together and
   strip back, the quiet version reliably sounds *thin* rather than
   *intentional*.
8. **Mix the stems against each other, once, and trust it.** The engine
   applies one gain to the whole set (§7.3) — it will not rebalance stems
   for you, on purpose.

---

## 7. Gotchas

### 7.1 Nine streams is not nine files' worth of cost

Eighteen AudioSources exist; during a floor crossfade all eighteen are
decoding. See §8.

### 7.2 `MusicTrack` stops being the unit

`Music.cs`'s enum maps a *meaning* to a *single file*. Layering needs
(set, tier) instead. The enum can survive as the fallback path for the
no-manifest case, but it is no longer the primary lookup.

### 7.3 `AudioLevels` must not be applied per stem

**This is the one that will silently ruin the mix.**

`AudioLevels.GainFor` exists because delivered clips arrived with 14.6 dB
of loudness spread, and it normalises each clip independently. That is
correct for unrelated one-shots and **actively destructive for stems** — a
quiet pad and a loud lead are quiet and loud *on purpose*. Normalising each
one individually flattens the arrangement the composer wrote.

So: **one `gain` per set**, from the manifest, applied uniformly to all
nine sources. Do not add stems to `audio_levels.json`. Measure the set by
rendering the full mix (all nine at unity) and correcting *that* to the
project's target, then apply the same multiplier to every stem.

Note the existing `Voice.Gain` field (`MusicController.cs:50`) is already
per-voice rather than per-source, so the shape is right — it just needs to
be fed from the manifest instead of `AudioLevels.GainFor`.

### 7.4 `GameSettings.MusicVolume` still multiplies everything

Final source volume is `tierVolume × setGain × GameSettings.MusicVolume`,
re-asserted every frame per the existing `Update` (`MusicController.cs:246`)
so the pause-menu slider stays live during a fade. Do not cache it.

---

## 8. Unity import settings — a deliberate deviation

`Resources/Audio/README.md` currently says long music should use
**Streaming**. **For stems, that guidance is wrong** and the README needs
a note saying so.

Streaming costs a decoder and a disk read per source. Eighteen concurrent
streams during a floor crossfade is a lot of simultaneous I/O and a
plausible source of hitching, especially on slower drives.

**Use `Compressed In Memory` (Vorbis) for stems.** Nine ~3-minute stereo
stems compress to roughly 25–30 MB per set, and only two sets are ever
resident. That trade — tens of MB of RAM for eliminated stream contention —
is the right one on desktop.

**[OPEN]** — verify against a real set once floor 1's stems exist. If
memory turns out to be the tighter constraint, the fallback is to make the
outgoing voice release its clips immediately after a crossfade completes.

---

## 9. What changes in existing code

1. **New `MusicLayerManifest` loader** — parses `music_layers.json` the way
   `AudioLevels` parses `audio_levels.json`: `JsonUtility`, cached, `Reset()`
   test seam, every failure path degrading to the existing behaviour rather
   than throwing.
2. **`Voice` widens from 2 AudioSources to N** (`MusicController.cs:38`),
   holds `t0`, `bpm`, `beatsPerBar`, and the set's `gain`.
3. **`SetVolume` takes a tier**, not a scalar — it walks the stem array and
   applies the tier mask.
4. **New `MusicController.SetTier(tier)`**, cheap and idempotent, safe to
   call every overlay change. Returns immediately if the tier is unchanged,
   mirroring the existing early-out at `MusicController.cs:154`.
5. **`TrackFor` becomes `TierFor`** (`MusicController.cs:132`) and splits
   Elite from Boss, which it currently merges.
6. **New `MusicController.SetFloor(int)`**, which triggers the voice
   ping-pong crossfade. Called when `RunSnapshot.floor`
   (`RunSnapshot.cs:54`) changes.
7. **`GameplayManager.ShowOverlay`** — the existing single call site — calls
   `SetTier` instead of `PlayForOverlay`.
8. **`Resources/Audio/README.md`** gains a stems section and the §8
   import-setting exception.

---

## 10. Tests

`MusicControllerTests` and `AudioLevelsTests` already exist; extend rather
than duplicate.

- Every set in the manifest resolves every stem path to a real `AudioClip`.
  (Mirrors the existing test that walks the `Sound` enum — a typo'd path
  should fail the suite, not be silently inaudible.)
- **Every stem in a set has an identical sample count.** This catches the
  single most likely authoring error automatically, and it is the highest
  value test here.
- Every tier's indices are in range for its set's `stems` array.
- Every tier name in the manifest is one the code knows.
- Every floor referenced by content maps to a set, or to `fallbackSet`.
- A missing manifest, a malformed manifest, and a missing stem file each
  degrade rather than throw.
- `TierFor` returns `boss` for Boss, `elite` for EliteFight, `fight` for a
  normal fight, `ambient` otherwise.

---

## 11. Open decisions

1. **Boss intro** — drop, sting, or per-set intro stem (§4.4). Recommend sting.
2. **Quantise to bar or phrase** (§5), and whether it is per-set authored.
3. **Import setting** — confirm Compressed In Memory against real stems (§8).
4. **Stem count** — 9 is the working number, but nothing should hard-code
   it. Confirm the code treats it as data.
5. **Does the hub share floor 1's set**, or get its own? Manifest supports
   either; not decided.
6. **Dynamic per-stem combat overrides** (§3) — build now or leave a hook?
7. **What plays during the boss intro cutscene / run-end**, if those exist.

---

## 12. Out of scope

- **The music itself.** No stems exist yet. Floor 1 is the pilot.
- **The `Sfx` side.** One-shots, voice lines, and spell sounds are
  untouched.
- **Mixer groups / ducking.** Everything here is per-source volume. If
  voice lines later need to duck the music, that is an `AudioMixer` job and
  a separate piece of work.
- **Anything in `docs/handoffs/shawn_talent_rework/`** beyond the §3 note
  that combat state is the natural next driver.
