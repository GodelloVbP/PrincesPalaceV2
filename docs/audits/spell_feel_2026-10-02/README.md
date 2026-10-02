# Spell feel audit, 2026-10-02

All 24 skills with a `vfx` block, reviewed against `docs/SPELL_FEEL.md`.
Captures: `docs/captures/spell_feel/` (`INDEX.md`; GIFs and stills on disk,
not in git). Per-spell detail: `shawn_strikes.md`, `shawn_rituals.md`,
`monsters_and_gaps.md` in this folder.

Limits of the evidence: the harness casts everything as Shawn at 1x, 16:9,
one target. Monster direction and scale, 1.5x, 4:3, multi-target and the
damage number's spawn frame are unverified. Ward, Reclaim, Hasten and
Reposition skills, `bog_mud_burst` and `trunk_slam` were reviewed from data
and sheets only (harness gaps below).

## Roster-level findings, ranked

1. **One white hurt flash ends every damaging cast.** The target turns into a
   solid white silhouette for 6-8 frames on contact. It is the loudest frame of
   every spell and identical for all of them, so it erases each spell's element
   colour and adds a second large flash to every cast. Fix (code, one place):
   tint the flash toward the hit's damage type and shorten it to ~3 frames.
2. **Cast length does not follow cost.** Crownfall (6 MP, 1.07 s) and Winter's
   Rebuke (8 MP, 1.03 s) are as long as Blackglass (12 MP); Cinderfault (15 MP,
   AoE) is the shortest at 0.38 s; Court of Whispers (14 MP) runs the same
   0.72 s as the 7-MP rituals. Boulder Slam plays on every golem turn.
3. **Element colour language is broken on six skills.** Frost Flare reads as
   lightning (white-violet column, no fire or ice); Mud Burst reads as a fire
   stream; Cinderfault never shows Nature green; Thorn Tithe's thorns are brown
   on a brown treant; Bog Mud Burst and Spore Cloud show red Physical numbers
   under earth and poison art.
4. **The ritual template makes four Afflicts read alike.** Thorn Tithe, Court of
   Whispers, Velvet Shackles and Censer of Embers share one beat: aura round the
   target, flash at 0.3 s, fade, plus identical cell rate, fade, particle
   physics and some seeds.
5. **Two ally spells play on the caster.** Gilded Aegis and Borrowed Moment are
   SingleAlly but `place: caster-centre`, so the shield and hourglass appear on
   Shawn, not the ally who gets them. Bug.
6. **Payoffs with nothing drawn.** Spore Cloud's cloud is invisible (a few
   20 px droplets) while its authored tail runs 2.3 s; Dark Chains' drag has no
   tether (`follow` is read by nothing); Viper's Bite's Poison detonation and
   Thorn Tithe's retaliation have no visual; Ashen Reckoning's two damage
   packets share one accent and sound.
7. **Audio.** Thorn Tithe's and Borrowed Moment's impact transients land 12-14
   frames after the visual payoff; Velvet Shackles has no riser; Gilded Aegis
   has no sound of its own; Death Knell's 1.4 s toll plays over a 0.2 s picture.
   Mud Burst, Frost Flare and Lightning Bolt still use legacy `impactFrame`
   with no `hitCueSeconds` (authored 13/5/5 vs recorded contact 20/18/18), so
   their sync is unverified.
8. **Unpresented skills.** Odette's Mend and Prism Ward have no visual; Bjorn's
   Slam and Rampage play the same generic burst at every Fury tier; Bellow and
   Hold the Line are silent and invisible (Provoked and Fortified have no
   status cue).

## Pairs that read alike, and the one change that separates them

| Pair | Shared trait | Separating change |
|---|---|---|
| Frost Flare / Lightning Bolt | vertical column on the target, same 0.52 s rhythm, same ember | Frost Flare becomes a fire-meets-ice collision at the target using the owner's new sheet; Lightning loses its 0.2 s build and strikes in 1-2 frames |
| Winter's Rebuke / Crownfall | sky hang, diagonal spear, burst, shards, ~1 s | Crownfall drops vertically onto the target (the name says fall); Winter's keeps the diagonal and its frost ring |
| Cinderfault / Mud Burst | orange flame shapes and ember tail | Mud Burst becomes lobbed brown mud with a ground splat, no flame |
| Thorn / Court / Velvet / Censer | aura, flash at 0.3 s, fade | thorns burst up from the floor; hooded whisperers lean in and the target recoils; the shackle stays as a held final image; the censer's swing travels across the floor |

## Per-skill verdicts

| Skill | Verdict | Top fix | Cost |
|---|---|---|---|
| blackglass_spear | reference | none | |
| winters_rebuke | strong identity (frost ring) | cut the dead sky hang, ~0.75 s total | content |
| crownfall | good crown art, too long for 6 MP | vertical drop, ~0.7 s | content |
| lightning_bolt | right art, ramps instead of striking | full-length bolt in 1-2 frames; author hitCue | content |
| frost_flare | reads as lightning | wire the new sheet as a collision | content + art wiring |
| mud_burst | reads as fire | lobbed brown mud + ground splat; author hitCue | new frame or emitter retint |
| cinderfault | no accent (real), no Nature | silent widening crack, then a distinct eruption; green ash | content |
| vipers_bite | best ritual (held jaws, snap) | show the Poison detonation | code (small) |
| ashen_reckoning | coherent converge-and-burst | split the two packets 2-3 frames apart | code |
| gilded_aegis | good shard lock-in, wrong body | anchor on the ally; give it a sound | content (bug) |
| borrowed_moment | distinct hourglass, wrong body | anchor on the ally; retime impact transient | content (bug) + audio |
| palace_passage | strongest art meaning, hides Shawn 0.5 s | door on both seats, swap at 0.3 s behind them | code check |
| gale_scythe | whirlwind, no quiet before the cut | thin the early cells so the crescent is the only big event | content |
| velvet_shackles | flat plateau (real) | end on the held cuff; add a riser | content + audio |
| censer_of_embers | good swing then flame | trim smoke to ~0.75 s; coal glow while Burn lasts | content + code |
| thorn_tithe | brown on brown, tiny accent | thorns burst up from the floor in Nature green | new frame |
| court_of_whispers | generic curse circle, not "whispers" | hooded courtiers lean in; pulse on Shawn for his Vulnerable price | new frame |
| bellwether_scratch | solid; rake hidden in the white flash | stagger the claw strokes so they land at flash peak | content |
| death_knell | strongest read in the roster | hold the aftermath ~0.3 s; badge tinted Void | content |
| boulder_slam | stalagmite pop 11 frames before the hit, covers the body | pop on contact; golem wind-up pose | content |
| spore_cloud | the cloud does not exist | raise emitter rate and size, burst at the hit, cut the tail | content |
| dark_chains | bind reads, drag is undrawn | tether the drag (make `follow` work) | code |
| bog_mud_burst | reuses mud_burst; Physical number | Earth damage type (owner call: affinity) | content |
| trunk_slam | generic contact burst | splinter-and-dust burst + `shake` | content |

## Owner calls

1. Hurt-flash change (finding 1): tint and shorten globally?
2. Damage types on Spore Cloud and Bog Mud Burst: changing them to Poison/Earth
   recolours the number but changes affinity maths.
3. Frost Flare's new sheet: final? Wiring it is the top fix above.
4. Cost-to-length budget (finding 2): agree that cheap frequent spells should be
   the short ones?

Bugs, fixed without asking unless you object: the Gilded Aegis / Borrowed
Moment anchors, the three legacy `impactFrame` spells without `hitCueSeconds`,
and the harness gaps below.

## Harness and tool gaps

Diagnosis with `file:line` in `monsters_and_gaps.md` part B2.

- Ward, Reclaim, Hasten, Reposition refused by `PreviewStage.SupportedEffects`.
- `trunk_slam`: a short cast frees early and fails `Assert.Greater(lit, 20)`.
- `bog_mud_burst`: rear-reach skill with one enemy fielded in slot 1.
- `at: hit` spells: the hit-cue hook installs after the cue has crossed.
- 1.5x: no `PP_RUNTIME_SPEED`; the test resets `BeatSpeedMultiplier` to 1.
- Enemy casts aimed at the party cannot be captured (larger: needs an
  enemy-side forced-ability hook).
- `tools/spell_feel.py`: for a no-gather ritual it should take frame 0 as the
  start; today it WARNs a false desync on six rituals. Spore Cloud's WARNs are
  the next turn's poison tick inside the recording window.
