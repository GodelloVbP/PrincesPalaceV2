# Plan: spell animation, from one drawing to layered beats

Rev 2, 2026-10-04: answers my own attack on rev 1 and the owner's calls
(section 6). Follows `docs/PLAN_SPELL_REWORK.md` (S0-S5 shipped, owner
art wired 79623a1c..87df24b7). Standard: `docs/SPELL_FEEL.md`.

## 0. Diagnosis

The owner watched the ten showcase GIFs (`docs/captures/spell_rework/showcase/`)
and was not happy. Owner's words: "everything is just one art sheet, or at least
looks like it". Confirmed on the frames and in the content:

| | Blackglass (reference) | The ten reworked spells |
|---|---|---|
| Hit cue | 0.7 s | 0.3 s, every one of them |
| Layers | 9 | 3-6 |
| Separately drawn beats | 6 (amass, rift, spear, fire, tear, settle) | 1-2; one still carries the whole spell |
| Payoff to clean | a resolution, then quiet | about 0.4 s, then it stops |

What that produces on screen, spell by spell:

- **Viper's Bite.** A snake head the size of the rat flies across the stage from
  Shawn. The verb was *bite*; it became *throw a head*.
- **Velvet Shackles.** A red velvet blob covers the whole rat. It reads as a
  flower or a sash, not as shackles.
- **Thorn Tithe.** The cage starts at half size and slides down; it does not
  close. It is bigger than the treant and hides it.
- **Court of Whispers.** The courtiers merge into one purple mass on a mixed
  rack. The payoff turns into a circle around the enemy. Shawn's price is a big
  white ring that reads as a targeting reticle.
- **Gale Scythe.** The sweep is good. The hit frame turns every enemy into a
  flat pale-cyan silhouette.
- **Censer of Embers.** The swing is too fast for a censer. The fire at the
  feet is one frozen drawing, not a living fire.
- **Ashen Reckoning.** The best of the ten (owner). It stops mid-phrase: no
  ending. The tint pushed the flame toward salmon.
- **Mud Burst.** Reads as mud. Thin, no aftermath.
- **Gilded Aegis, Borrowed Moment.** The buffs are the best (owner), but small;
  they want more presence.

Process failure, recorded so it is not repeated: every "passes" verdict in the
first pass came from agents grading their own GIFs with the owner gates skipped,
and was relayed without being watched.

## 1. Behavioural contracts

Every spell, every cast. Each contract has a check (section 4).

1. **Beats, not a drawing.** A spell has at least four beats: anticipation
   (energy gathers), delivery (something travels or forms), payoff (contact),
   resolution (it decays and ends quieter than it started). Each beat is
   carried by a different element. No single drawing spans two beats.
2. **The body stays the subject.** At the payoff, no effect hides more than
   about a third of the struck body for longer than 0.1 s. Payoff art is sized
   to the body, never double it.
3. **Nothing frozen.** No element holds still for more than 0.15 s. A held
   element breathes: flicker, alpha flutter, small scale wobble.
4. **No pop-in.** Every layer fades or grows in. Only an impact may cut in.
5. **Depth.** An effect that surrounds a body wraps it: the back half draws
   behind that body, the front half in front, the back smaller and dimmer. This
   is the 2.5D the project does; no rigs, no 3D.
6. **Own rhythm.** Each spell's wind-up length comes from its verb and tier, not
   a shared 0.3 s. A heavy censer swings slowly; a snake strikes fast.
7. **A finish.** Payoff to clean has at least two decaying steps and ends on the
   smallest element (a last ember, a last curl of smoke), like a song's last
   chord, not a cut.
8. **One art style.** Spell art is the fight's style: the Giant Rat sheet
   (`Art/Enemies/Giant_rat_sheet.png`): bold outlines, flat cel tones. Art is
   drawn flat; glow, flicker and darkness are added by the engine.
9. **Principles, not a template.** Contract 1's four beats are the default;
   a buff or a point-blank spell may fold anticipation and delivery into one.
10. **Wind-up by tier.** No spell winds up longer than Blackglass (0.7 s) unless
   it is above Blackglass's tier. Exception: Censer 1.0 s by owner's call
   (three half-swings, then the break).
11. **Payoff on one frame** (unchanged from `SPELL_FEEL.md`): sound, number,
   badge and accent share a frame.

## 2. What the engine needs (smallest forms)

Each is a layer field or a small mechanism, built when the pilot needs it.

| # | Addition | Why | Size |
|---|---|---|---|
| E1 | `rotate`: angle, spin, and a pendulum (amplitude, period, damping) around a pivot inside the frame | Censer swing; Borrowed's hourglass flip; Viper's coil-back | layer field |
| E2 | `fadeIn` seconds | Contract 4; Viper's pop-in | layer field |
| E3 | `scale` over life (from, to, ease); `punch` becomes its special case | Ashen burning down; things growing in place | layer field |
| E4 | `breathe`: flicker or flutter on alpha and scale, using the existing `LanternFlicker` curve | Contract 3; living fire | layer field |
| E5 | Body depth bands: per stage slot, a band behind that body and a band in front of it, alongside today's ground and effects bands | Contract 5; cages, orbits, coils | scene tree + runtime + content (cross-layer) |
| E6 | `orbit` path: an ellipse around the anchor (radii, start angle, sweep, direction) that switches between the E5 back and front bands as it passes behind the body, scaling down at the back | Court's courtiers circling; Aegis shards; Shackles' ribbons | layer field, needs E5 |
| E7 | Frame registration for sheets: every frame cut on one shared canvas with a pivot (the base of the vines, the chain's hook), never cropped per frame | Thorn's slide | slicer recipe |
| E8 | Body echo: an afterimage of the struck body's own sprite, offset and tinted, fading (the silhouette mechanism `StageHitFlash` and `StageBurnGlow` already share) | Borrowed's "faster" on the body; Gale's cut | layer kind |
| E9 | Status visuals on the body: a second user of the Burn coal-glow seam (ward shimmer, haste trail, root cuff, fear shiver). With two users, it becomes a small table | Buffs' presence; the "signature that stays" | Core + scene tree |
| E10 | Hit-flash coverage cap: the typed hit flash is a rim or partial pulse, never a full flat silhouette | Gale's cyan silhouettes | runtime |

Renderer budget: a formation allows 12 spell layer renderers and the ground
band 2 (`FightHudSpec`). Every spell's beats are budgeted against that before
blocking; a spell over budget either shares layers across bodies or the limit is
raised in its own change.

E5 is prototyped first without engine work: an orbit as two hand-off layers
(behind = the ground band, dim and small; in front = the effects band). E5 is
built only if that prototype fails on a full rack.

E10 is scoped to spells: a spell opts its hit flash down to a rim. Melee and
enemy hit flashes are untouched.

E5 is the one cross-layer change (Domain tree, Core runtime, scene generation).
It goes to `senior` with `Escalation: cross-layer`.

Cost to know up front: changing a hit cue means recutting that spell's cast
sound, because `TheSpellCastCuesFitTheirBlows` pins the cast riser to the blow.

## 3. Per spell: the beats

The art each spell needs is listed as separate elements, never a whole scene.
Prompts are written after the spell is blocked with stand-ins, as before.

**Censer of Embers** (pilot). Wind-up about 0.9 s.
- Anticipation: a censer hangs above the target on its chain and swings left,
  then right, slow and heavy like a bell (E1), smoke trailing and breathing (E4).
- Payoff: at the end of the last swing it bursts into a shower of embers, with
  the flash, sound and Burn badge.
- Resolution: a living fire at the feet (a looped flame sheet, E4) that settles
  down and hands over to the coal glow; embers float up and wink out.
- Art: the censer on its chain as one still; a 4-6 frame flame loop on green.

**Court of Whispers** (pilot). Wind-up about 0.7 s.
- Anticipation: the stage darkens (gloom, kept); two or three hooded courtiers
  fade in and circle the enemy (E6): behind it they are smaller and dimmer, in
  front of it full size.
- Payoff: they all lean in to the head at once; Fear lands with a violet shiver
  through the body (E10-style rim, not a circle).
- Resolution: they draw back and come apart into smoke that rises and thins.
- Shawn's price: a small violet flicker on him (E4), no ring.
- Art: the courtier v2 still (in hand); optionally a back-view courtier for the
  far arc; a smoke-wisp particle.

**Viper's Bite.** Fast: about 0.4 s. No snake head (owner: it looks bad).
- Anticipation: two eye-glints open in the dark beside the target.
- Delivery and payoff: jaws form on the body and snap shut (the old jaws sheet),
  with the fangs, number and Poison badge on the cue.
- Resolution: venom drips down the body; green mist fades.
- Detonation (if Poison was there): the painted burst, kept.
- Art: none new; the old jaws sheet, redrawn in the rat style later.

**Thorn Tithe.**
- Anticipation: the soil cracks at the feet, with a dust puff.
- Delivery: vines rise in place from a fixed base (E7); the back vines draw
  behind the body and the front vines in front (E5), so the cage closes
  around it.
- Payoff: the cage snaps shut, with a sap burst, the Thorned badge and a small
  shake.
- Resolution: the thorns sink back into the ground; a few leaves fall.
- The cage is sized to the body.
- Art: the growth sheet split into a back-vine set and a front-vine set, on one
  shared canvas.

**Velvet Shackles.**
- Delivery: ribbons whip out from Shawn's hand and wrap around the target
  (E6, back and front).
- Payoff: a small cuff cinches at the feet with a gold clasp glint and the
  Rooted badge.
- Resolution: the ribbons go slack and dissolve; the cuff stays as the Root
  status visual (E9).
- The cuff is sized to the legs, not the body.
- Art: one ribbon strand; a small cuff.

**Gale Scythe.**
- Anticipation: wind gathers in the sickle on Shawn as a leaf spiral.
- Delivery: the sweep across the line, kept.
- Payoff: one shared frame: a thin bright slash across each body plus a body
  echo knocked back (E8), with a leaf burst and a small shake; no flat
  silhouettes (E10).
- Resolution: leaves spiral away; wind streaks fade.

**Ashen Reckoning.** Keep everything; add the finish.
- Drop the tint so the flame is orange again.
- Resolution: the flames shrink and gutter (E3, E4), ash drifts down, and a last
  thin curl of green smoke rises and thins out over about 0.5 s.

**Mud Burst.** Keep. Add an aftermath: drips run down the body and a puddle at
the feet fades.

**Gilded Aegis.**
- Shards orbit in and click into place one by one (E6, staggered).
- After the lock, a shimmer sweeps across the ward.
- While the ward holds, a faint gold glint on the ally (E9).

**Borrowed Moment.**
- The hourglass flips (E1) instead of sliding.
- Sand pours upward.
- Payoff: the ally's own afterimage leaps forward (E8) as the turn-order chips
  move.
- While hasted, a faint cyan trail (E9).

## 4. Checks

| Contract | Check |
|---|---|
| 1 Beats | the spell's NOTES lists its beats and which layer carries each; reviewed against the GIF |
| 2 Body visible | `spell_feel` gains a coverage measure: the fraction of the body box under effect pixels at the payoff, WARN above about a third |
| 3 Nothing frozen | `spell_feel` WARN: a region unchanged for more than 0.15 s while a layer is live |
| 4 No pop-in | `spell_feel` WARN: a layer's alpha going from 0 to above 0.8 in one frame, unless it is at the hit |
| 6 Rhythm, 7 Finish | both clocks reported per spell; payoff to clean shows at least two decay steps |
| All | I watch every GIF frame strip myself before calling a spell done, and say what I saw |
| Identity | the owner, on the GIFs; nothing is "passed" without the owner |

## 5. Phases

| Phase | Lands | Gate |
|---|---|---|
| A0 | E1 rotate and pendulum, E2 fade-in, E4 breathe: only what the Censer needs | tests; I watch the GIFs |
| A1 | Pilot Censer blocked with stand-ins, shown to the owner within a day; then the owner's art (`docs/prompts/2026-10-04_spell_animation.md`) | owner reviews the GIFs |
| A2 | E3 scale over life, E7 frame registration, E10 spell hit-flash cap, the `spell_feel` WARNs (coverage = effect pixels over the body's silhouette on the hit frame) | tests; I watch the GIFs |
| A3 | E6 orbit on the two-layer prototype; E5 only if that fails (senior, cross-layer) | tests; I watch the GIFs |
| A4 | Pilot Court on E5/E6; Ashen's finish | owner reviews |
| A5 | Viper, Thorn, Shackles, Gale, Mud, one at a time, in that order (worst first) | owner, per spell |
| A6 | E8 body echo; Aegis and Borrowed | owner |
| Later | E9 lasting status visuals (ward glint, haste trail, root cuff); the cel-style redraw of today's painted stills | owner |

The owner reviews after each pilot. A6 waits for A2 and A4 to be accepted.

## 6. Owner calls

Answered 2026-10-04:
1. Style: the fight's style, the Giant Rat sheet (contract 8).
2. The Censer fire: the owner tries a painted flame loop; it gets a glow,
   flicker and gloom pass in the engine.
3. Viper: no head.

Open: recutting cast sounds for wind-ups longer than 0.3 s (capped by tier,
contract 10).
