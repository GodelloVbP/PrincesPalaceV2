# Plan: Shawn's ten template spells, rebuilt to Blackglass quality

Rev 3, for independent review.
- Rev 3 answers the first independent review (7 findings, Q1, Q2) and the
  owner's calls of 2026-10-03.
- Rev 2 answered my own attack on rev 1.
- Repo facts were read on 2026-10-03.
- Standard: `docs/SPELL_FEEL.md`: principles and identity, not Blackglass's
  numbers.
- Evidence of the gap: `docs/audits/spell_feel_2026-10-02/README.md:64-82`.

## 0. The gap, measured

| Spell | Layers today | Audit verdict (README.md) |
|---|---|---|
| Blackglass Spear (reference) | 9: amass, rift, streaks, spear (travels), fire, tear-air, impact, tear, settle | the reference |
| Gilded Aegis | 2: ritual + settle | good shard lock-in, wrong body (see 4.4) |
| Viper's Bite | 3: ritual + settle + detonate | best ritual; show the Poison detonation |
| Ashen Reckoning | 2 | coherent converge-and-burst |
| Borrowed Moment | 2 | distinct hourglass, wrong body (see 4.4) |
| Gale Scythe | 2 | whirlwind, no quiet before the cut |
| Velvet Shackles | 2 | flat plateau; end on the held cuff |
| Censer of Embers | 2 | good swing then flame; smoke too long |
| Thorn Tithe | 2 | brown on brown, tiny accent |
| Court of Whispers | 2 | generic curse circle, not "whispers" |
| Mud Burst | 1 (travels) | reads as fire |

Nine of the ten share one shape:
- one 12-frame sheet on the target (`place: target-centre`, `at: release`);
- a 4-frame particle settle;
- no `shake`.

`hitCueSeconds` 0.3 is cell 6 of a 12-cell sheet played over 0.72 s
(`docs/prompts/2026-10-02_petting_zoo_and_frost_flare.md:190`), not an untuned
default. What the nine lack are beats: a wind-up, a delivery, a payoff of their
own, a signature that stays. Section 4 lists the runtime pieces that *are*
missing. Rev 2's "the engine is not what is missing" was too strong (see
Gale, 3.4).

## 1. Decisions taken

1. **Scope.** The ten spells above, all Shawn's, plus the Burn coal glow
   (owner, O2: yes) and the Kinship relic icon (owner, O3: yes).
   - Out of scope: Crownfall (stage-room limit, AUDIT), Cinderfault
     (`PLAN_BUGS_QA.md` C10) and the enemy abilities.
2. **Pilot first, built from existing assets.** Court of Whispers goes first,
   blocked with frames that already exist. Only the pieces the pilot shows
   to be missing get built, and they are judged moving in a real fight, not
   on a contact sheet. Every later generated piece is built when a spell
   needs it, not up front.
3. **The method is SPELL_FEEL's.**
   - Block with stand-ins (workflow step 3).
   - Commission only the frame that carries meaning: one painted still on
     `#00FF00`, green-keyed (step 6), animated with layer fields.
   - A short sheet only where motion cannot be faked, named per spell.
   - The reason: ChatGPT sheets drift between frames. The Toll of the Flock
     flock count flickers between 3 and 2 sheep (`PLAN_BUGS_QA.md` C11).
4. **Each spell's prompt follows its blocking.** It goes into
   `docs/prompts/2026-10-03_spell_rework.md`, one spell at a time.
5. **Payoff first, everything else follows it.** Each spell states its payoff
   (or payoffs) first. Flash, shake, glow and residue are chosen to serve that
   payoff. None is required.
   - The hard rule as written: each payoff's **sound, damage number and
     accent** share a frame (`SPELL_FEEL.md:133-137`).
   - A buff or ward has no contact frame; its payoff is what it leaves on the
     ally.
6. **"Gloom" is read as both readings:** bloom (`glow`) and literal stage
   darkening.
7. **Gloom preserves event darkness** (review Q1, adopted).
   - Stage darkness has **one owner**: the existing `roundDarkness` image
     (`Core/FightController.Rounds.cs:53`), which Bell in the Fog drives from
     0.9 opacity down to a 0.15 floor (`events.json:1238`).
   - A spell's gloom becomes a second **input** to that same image.
     - The image shows the larger of the two: `max(event, spell)`.
     - The two never stack, so a spell never darkens a stage the event
       already darkened.
     - Gloom is never drawn as a separate veil.
   - Coverage is whatever that image already covers. Spell gloom is bounded:
     Court 0.35, Shackles and Viper 0.20 (owner O1). The rev 2 "50% floor" is
     dropped; it contradicted the event.
8. **Gale Scythe hits all enemies at once** (review Q2, adopted). The sweep
   is decorative across the line; there is one shared payoff frame.
   Staggered per-target payoffs would be a separate scope decision, with a
   mechanism investigation first.
9. **Time budgets are hypotheses, judged on GIFs** (owner O4: "GIF it").
   - Two clocks, defined separately:
     - **cast-start → payoff**: the wind-up and delivery;
     - **payoff → clean**: residue.
   - Both are battle-time seconds. At 1.5x they are the authored seconds
     divided by 1.5, as `spell_feel.ps1 -Speed` already reports.
   - Starting hypotheses come from tier neighbours (SPELL_FEEL "Budget": "judge
     each against its tier-neighbours", no duration table). Each spell's pair is
     settled by a side-by-side GIF of **five consecutive casts** next to its
     tier neighbours, at 1x and 1.5x, looked at by the owner.
10. **One new layer field, `tint`**, but only if the pilot proves it is
    needed. It is a colour multiplied into sprite and emitter frames, so one
    greyscale generated piece serves every element. The alternative is one
    folder per element and piece.

## 2. Evidence: what checks what

No command result stands in for an artistic acceptance. Each risk has one kind
of evidence.

| Kind | What it checks | Tool |
|---|---|---|
| **Automated, fails the build** | timing pins per spell (literal tests), the darkness composition (`max(event, spell)`), `tint` behaviour, generator determinism | unit and PlayMode tests in the commit gate |
| **Automated, report only** | residue tail, accent present, contact in sync with `hitCueSeconds` (`tools/spell_feel.py:40`); both clocks measured | `tools/spell_feel.ps1` (WARNs; never a pass or fail) |
| **Manual frame inspection, by me** | flash count per second over five consecutive casts at 1.5x (at most 3 large flashes, SPELL_FEEL Guards); number and status badge readable under every effect and under gloom; the caster-cost pulse on Shawn visible | strips and GIFs, a written note per spell |
| **Listening, owner** | sound, number and accent on one frame; repeated casts don't smear; cast cue vs blow at 1.5x | in game, five casts in a row |
| **Owner judgment** | the identity test sentence; side-by-side against Shawn's other spells (SPELL_FEEL step 5); time hypotheses | GIFs, then in game |

**4:3 capture.** Neither `spell_feel.ps1` nor `gif.ps1` takes an aspect ratio
today. `CanvasCapture.RenderToFile(canvas, path, width, height)`
(`Core/CanvasCapture.cs:55`) already accepts one, so `gif.ps1` gains an
`-Aspect 4:3` option that passes 1440x1080 through the scenario runner. This is
built in S0 and checked against one known 4:3 layout.

**Consecutive casts.** `gif.ps1` records one cast per scenario. S0 adds a
`-Repeat <n>` option: the same spell cast n times in one fight, with the battle
pacing in between.

## 3. Per-spell design (payoff first)

Each spell lists its **payoff**, then its verb, its signature, an
**identity test** (what a first-time viewer must say), its sources and its
capture scenarios. Source tags: (a) owner still or sheet; (e) existing asset;
(b) generated, only when a spell needs it.

1. **Court of Whispers** (Fear on all enemies; Shawn pays with Vulnerable).
   **Pilot.**
   - **Payoffs:**
     - The Fear lands on every enemy at once; sound, the Fear badges and the
       accent share one frame.
     - Shawn's price: a violet pulse on him, plus his Vulnerable badge, after
       the Fear lands.
   - Verb: *haunt*.
   - Signature: hooded courtiers lean in around each enemy, then recede.
   - Sources:
     - (a) one still of a leaning courtier, mirrored per enemy, with the lean
       made by `dx` travel and fade;
     - (e) the current court frames as stand-ins;
     - gloom 0.35 during the wind-up, lifted on the payoff.
   - Audio: `PLAN_BUGS_QA.md` C9, built.
   - Identity test: "they are whispering to them".
   - Captures:
     - a full 3-enemy formation, plus a crowded mixed formation;
     - Shawn's cost pulse visible;
     - inside Bell in the Fog at round 1 (event darkness 0.9), where the
       spell's gloom must change nothing;
     - five casts at 1.5x.
2. **Thorn Tithe** (Thorned)
   - **Payoff:** the Thorned status lands as the vines close.
   - Verb: *sprout*.
   - Sources: (a) a 3-4 frame sheet of vines rising (growth cannot be faked);
     (b) a sap spray if needed.
   - Identity test: "green thorns from the ground".
   - Captures: rat, treant.
3. **Mud Burst** (Earth, Vulnerable)
   - **Payoff:** the splat. Sound, the number and the Vulnerable badge on one
     frame.
   - Verb: *splatter*.
   - Sources: (a) a glob still (travelling, arced) plus a splat still. Matte,
     no glow.
   - Identity test: "mud", not "fire".
   - Captures: rat, treant, a back-rank target.
4. **Gale Scythe** (Wind, all enemies, pushes turn order)
   - **Payoff:** one frame for all enemies (decision 8). The blade's sweep
     crosses the line before it.
   - Verb: *reap*.
   - Sources: (a) a crescent still on a span-aligned, decorative pass;
     (e) speed streaks.
   - Identity test: "one cut across all of them".
   - Captures: a full formation, two enemies, a lone enemy.
5. **Velvet Shackles** (Root)
   - **Payoff:** the cinch. Sound and the Root badge.
   - Verb: *bind*.
   - Sources: (a) a short cinch sheet plus a held-cuff still; gloom 0.20.
   - Identity test: "tied down".
   - Captures: rat, treant, inside Bell.
6. **Viper's Bite** (Poison; sets off existing Poison)
   - **Payoffs:** the bite (number, Poison badge) and, when Poison was already
     there, the detonation as a second payoff with its own sound and number.
   - Verb: *strike*.
   - Sources: (a) a lunge still plus a bite still; (e) the green pop
     (`whenConsumed`); gloom 0.20.
   - Identity test: "a snake bit it".
   - Captures: no Poison; with existing Poison (`PP_RUNTIME_PRESTATUS`
     Poison); inside Bell.
7. **Ashen Reckoning** (consumes Poison, deals Poison + Fire)
   - **Payoff:** the ignition. Its two packets are already staggered; each
     packet's number gets its own accent.
   - Verb: *ignite*.
   - Sources: (a) a burst still; (e) motes and embers.
   - Identity test: "the poison caught fire".
   - Captures: with Poison present; its refusal without Poison is unchanged
     by this plan.
8. **Gilded Aegis** (ward on an ally)
   - **Payoff:** the ward settles on the ally. No contact frame; the ward
     badge shows.
   - Verb: *encase*.
   - Sources: (e) the existing shard art; (b) a mote stream from Shawn if the
     GIF needs a delivery.
   - Identity test: "a golden shield on my ally".
   - Captures: on Odette, on Bjorn, on Shawn himself.
9. **Borrowed Moment** (hastens an ally)
   - **Payoff:** the ally moves up the turn order. The order change is
     visible.
   - Verb: *quicken*.
   - Sources: (e) the existing hourglass; (b) upward sand if needed.
   - Identity test: "time sped up for my ally".
   - Captures: on each ally.
10. **Censer of Embers** (Burn)
    - **Payoff:** the Burn badge as the embers catch.
    - Verb: *smolder*.
    - Sources: (e) the existing swing, smoke trimmed.
    - Identity test: "set alight".
    - Captures: rat, treant.
    - The lingering glow is item 11.
11. **Burn coal glow** (owner O2): a status visual on any burning body, from
    any source.
    - **Mechanism investigation first:** check whether a per-status visual on
      the body already exists to extend (status tint, badges, StatusCues), and
      report.
    - Then build it in that system, not as a Censer layer.
    - Its own GIF while Burn lasts, and gone when it ends.
12. **Kinship relic icon** (owner O3): one prompt in the same file,
    green-keyed, at the existing relic icon size. It lands through the relic
    icon pipeline (`key_green_screen.py`). It does not wait for the pilot.

## 4. What I build, and when

1. **S0 tooling:** `gif.ps1 -Aspect 4:3` and `-Repeat <n>` (section 2), plus a
   capture route into Bell in the Fog's darkness (a scenario that sets
   `roundDarkness` at a given level).
2. **Pilot-demanded only:**
   - **Gloom input.** `roundDarkness` gets a spell input with `max`
     composition and a fade in and out authored on the spell. It is built in
     S1, because Court needs it.
   - **`tint` and `make_spell_fx.py` pieces:** only those the Court blocking
     shows are missing.
3. **Per later spell:** further generated pieces (drips, sap, sand) are built
   when that spell's blocking needs them. Each is judged moving in its spell's
   GIF.
4. **Ally anchoring check.** "Ally spells anchor on the ally" was fixed after
   the audit (2026-10-02 pass). Aegis and Borrowed are captured on each ally to
   confirm it. This is a check, not a planned fix.
5. **Bloom** already reaches the fight (Fight.unity references
   `MenuVolumeProfile.asset`, `PipelineBuilder.cs:27`). Each spell uses `glow`
   only where its payoff wants a hot core.

## 5. Phases, each with its gate

| Phase | Lands | Gate (section 2 kinds) |
|---|---|---|
| S0 | `-Aspect`, `-Repeat`, Bell-darkness capture; before GIFs of all ten in their section-3 scenarios; Kinship icon prompt | tool tests; me: GIFs inspected |
| S1 | Pilot Court: blocked from existing assets, then gloom input, then whatever else the blocking proves missing, then its prompt, then the still, then recapture, then pins | automated: pins, darkness composition; report: spell_feel; me: frame notes; owner: identity, side-by-side, time hypotheses, listening |
| S2 | Owner accepts the pilot, or sends it back | owner |
| S3 | The other nine, in this order: Thorn, Mud, Gale, Shackles, Viper, Ashen, Aegis, Borrowed, Censer. Each with its own pins, commit and the same gates | per spell |
| S4 | Burn coal glow: investigation, then build | automated pin; owner GIF |
| S5 | GIF baseline refreshed with every section-3 scenario | coverage table lists all of them |

## 6. Open owner calls

None. O1-O4 are answered (gloom strengths adopted with the max composition
instead of the 50% floor; coal glow in; Kinship in; time budgets judged on
GIFs).
