using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // One discrete moment of a fight, recorded while combat resolves and
    // replayed afterwards by the view.
    //
    // Combat RESOLVES synchronously, exactly as it always has: one command runs
    // the player's action and every enemy reply to completion, in a single
    // pass. That is not incidental -- a large number of assertions across the
    // fight tests act and then check the result on the very next line, so
    // making resolution itself asynchronous would break all of them for a
    // purely visual gain.
    //
    // What IS sequenced is the presentation. While resolving, each discrete
    // action records a beat: who acted, who was hit, the poses both should
    // strike, the log lines, and a SNAPSHOT of everyone's vitals at that
    // instant. Playback then walks the beats one at a time -- lunge, recoil,
    // hold, return to idle -- driving the labels from each beat's snapshot
    // rather than from live state.
    //
    // The snapshot is the part that cannot be skipped. Without it the HP
    // numbers jump to their final values the moment you act, while the sprites
    // are still mid-swing, which reads worse than no animation at all.
    //
    // This is v1's StageBeat, moved whole. It was already engine-free apart
    // from using Vector3Int as an (hp, mana, signature) tuple, which is now
    // Vitals -- so the recording half of the beat system needs no Unity at all,
    // and the whole of it becomes testable in EditMode.
    public sealed class CombatBeat
    {
        // How much the target took (or was healed) in THIS beat -- the
        // PRE-SHIELD figure, restored to that even when a Ward absorbed
        // part of it (RecordBeatAmount adds the ward's own Absorbed back
        // in; see its own header). Carried on the beat rather than read
        // from live state at playback time for the same reason as the
        // vitals snapshot: by the time a beat plays, later actions in the
        // same chain have already landed.
        public int Amount;
        public bool IsHealing;

        // HOW MUCH OF THIS BEAT'S DAMAGE NEVER REACHED HEALTH, because a
        // shield ate it -- EITHER KIND, through the SAME field, written from
        // TWO sites (both reuse an already-computed number; neither
        // recomputes one):
        //
        //   1. FightSession.Ledger.ApplyAndCountDamage, for the SIGNATURE
        //      POOL's own absorb (ResourcePool.Absorb, currently authored
        //      absorbsDamage: false on every row in pools.json -- live
        //      capability, no live content) -- `toHealth = amount -
        //      result.Absorbed`, the ledger's own bookkeeping line.
        //   2. FightSession.Talents.ResolveWard, for a WARD STATUS's absorb
        //      (Magical Shield, the Fragile Lamb's Ward -- StatusEffects.
        //      ConsumeWard, wired in through DamagePipeline's `resolveWard`
        //      hook) -- spent UPSTREAM of DealDamage, on the raw number
        //      before it ever reaches the ledger.
        //
        // ADDITIVE across the two (and across a beat that lands on more
        // than one shielded body, or drains one target's ward AND its
        // signature pool in the same swing): a beat reports the TOTAL a
        // shield or shields kept off health, which is what the barrier
        // reaction and the hit-strength "fully absorbed" gate both actually
        // need to know, not which mechanism it came from.
        //
        // `Amount - Absorbed`, CLAMPED AT ZERO, IS THE HEALTH DAMAGE that
        // actually landed -- HitStrength.Classify, FightBeatPlayer.
        // ShowSingleAmount/RecoilOne and FightController.StageVisuals.
        // FlashOne all read it this way. That arithmetic is correct for
        // site 2 (Amount is deliberately restored to its pre-ward figure,
        // above) but is a KNOWN GAP for site 1 if it is ever turned on: a
        // signature pool's absorb is not baked into Amount the way a
        // ward's is, so this subtraction would double-count it. Not
        // reachable today for the reason site 1's own comment gives.
        //
        // Zero for every beat that landed clean, which is nearly all of
        // them.
        public int Absorbed;

        // THE ONE FIELD THIS CLASS DID NOT CARRY UNTIL THE POPUP NEEDED IT.
        //
        // FILLED FROM OUTSIDE FOR NEARLY EVERY BEAT, in
        // FightController.AfterResolution -- the one place every drained
        // batch of beats already passes through: that call already has
        // FightSession.ActorAttackType(beat.Actor) on hand (FightHudModel's
        // detail-card label reads the very same method), so painting it onto
        // each beat costs one loop rather than a second recording path
        // threaded through every RecordBeatAmount call site inside
        // FightSession's own several files. Everything FightSession records
        // through BeginBeat leaves it at Physical (the enum's own zero) until
        // that loop runs, exactly as it always implicitly did.
        //
        // THE ONE EXCEPTION IS A BEAT WITH NO ACTOR TO ASK. A status tick
        // declares its element from the status itself, in Domain, because
        // there is nobody whose attack type could answer for it -- see
        // DeclareDamageType and PaintActorDamageType below for which of the
        // two wins.
        //
        // A caster's OWN type, not the packet's -- a fixed-damage skill that
        // authors a type its caster does not carry (frost_flare's Ice half on
        // a Fire-typed caster) still pops in the caster's colour. CombatBeat
        // does not carry which ResolvedSkill produced it at all, only who
        // acted and who was hit, so that finer distinction would need the
        // beat to carry the skill too -- a bigger seam than a popup justifies
        // today.
        //
        // WRITTEN THROUGH ONE OF THE TWO METHODS BELOW, never assigned at a
        // call site. Which of them wins is a precedence rule, and a bare
        // `beat.DamageType = x` is how a precedence rule gets lost.
        public DamageType DamageType;

        private bool _damageTypeDeclared;

        // THE ACTOR'S OWN ATTACK TYPE, painted over every drained beat by
        // FightController.AfterResolution -- and IGNORED for a beat that has
        // already declared its own element.
        //
        // The precedence is in the name because the caller is a loop over
        // every beat in a batch and cannot reasonably ask, per beat, which
        // kind it is holding. A damage-over-time tick is the case that needs
        // it: the tick has no actor at all (FightSession.Riders records it
        // with Actor = null -- nobody is credited for it, the same fact
        // RecordUnattributedDamage states about the ledger), so an
        // unconditional paint would ask ActorAttackType(null), get nothing,
        // and fall through to Physical -- colouring a poison tick's flash and
        // popup like a sword blow.
        //
        // TAKES THE NULLABLE, and owns the fallback. `fromActor` is null for
        // an actor with neither a kit attack type nor an authored enemy
        // attackType, and for no actor at all; both mean "nothing elemental
        // was swung here", which is what Physical has always meant for a beat
        // FightSession never touched. Keeping the `?? Physical` here rather
        // than at the call site is what stops a second caller inventing a
        // different answer for the same absence.
        public void PaintActorDamageType(DamageType? fromActor)
        {
            if (_damageTypeDeclared) return;

            DamageType = fromActor ?? DamageType.Physical;
        }

        // THE BEAT'S OWN ELEMENT, for damage with no actor to read one off --
        // a status tick naming StatusEffects.ElementOf. Outranks the paint
        // above from the moment it is called. Pinned from both sides by
        // CombatBeatTests.ADeclaredDamageTypeSurvivesTheActorPaint.
        public void DeclareDamageType(DamageType type)
        {
            DamageType = type;
            _damageTypeDeclared = true;
        }

        // PHASE D1: the single-target swing or cast this beat represents was
        // DODGED — Amount stays 0 (RecordBeatAmount already no-ops for a
        // non-positive amount, so nothing here forces it) but that 0 must
        // not read as "landed for nothing", which is what a beat with no
        // damage source at all also looks like today (a Hold Back turn, a
        // non-damaging cast). A DISTINCT flag rather than overloading
        // Amount == 0 for the same reason DamagePipeline.Outcome.IsMiss is
        // its own field and not inferred from Damage == 0 — see that
        // struct's own comment. Left false and unset for a multi-target
        // beat (ResolveDamageAll) where one enemy of several dodges and the
        // rest do not — that AOE shape already reports per-enemy results as
        // text in Messages rather than through this single beat-wide
        // Amount/stance pair, so a per-enemy miss reads the same way a
        // per-enemy damage number already does: in the log line, not here.
        public bool Missed;

        // The turn queue as it stood when this beat resolved. Same reasoning as
        // the vitals snapshot: live state has already run the entire chain by
        // the time any beat is played, so reading it during playback shows the
        // END of the round for every beat -- which is why the tracker looked
        // frozen. With a solo party the chain always ends back on the player,
        // so "NOW" never appeared to move off them.
        public IReadOnlyList<CombatantState> TurnOrder;

        // WHICH OF THE TWO THE TRACKER ACTUALLY PAINTS, given the queue the
        // beat being played recorded and the queue live state holds right now.
        //
        // A pure function rather than an `if` inside the view, for the reason
        // ImpactFraction is one: the rule is a decision and Core cannot be
        // tested here, so leaving it in FightController.Hud would mean the
        // fallback below was only ever exercised by looking at a fight.
        //
        // `recorded` is null for the whole input phase -- nothing is playing,
        // so live IS the moment being shown -- and RECORDED-BUT-EMPTY falls
        // back the same way rather than blanking the row: a beat committed
        // with no encounter behind it (a fixture) or with the fight already
        // decided carries nothing, and an empty tracker mid-round reads as a
        // broken HUD rather than as an absence. It costs nothing when the
        // fight really is over, because live UpcomingTurns is empty then too.
        // Exactly the shape FightController.OrderOf already uses for the
        // formation's own recorded-or-live question.
        public static IReadOnlyList<CombatantState> QueueToShow(
            IReadOnlyList<CombatantState> recorded, IReadOnlyList<CombatantState> live)
        {
            if (recorded != null && recorded.Count > 0) return recorded;

            return live ?? System.Array.Empty<CombatantState>();
        }

        // WHERE EVERYBODY STOOD when this beat resolved -- see BeatFormation
        // for why a copy and not the live lists. Same reasoning as TurnOrder
        // directly above, applied to field position rather than to the queue:
        // a Move reorders the party list in place, so the view has to be told
        // the order this beat played at rather than reading the one the round
        // finished on.
        public BeatFormation Formation = BeatFormation.Empty;

        public readonly Dictionary<CombatantState, string> Stances = new Dictionary<CombatantState, string>();

        // ---- the actor's other two phases ------------------------------------
        //
        // Stances[Actor] above is the STRIKE: the drawing worn at the moment
        // of impact and held through the settle. These are the two poses that
        // can come BEFORE it, and they are what makes "he rushes forward, then
        // he holds the hammer over his head, then he slams down" one beat
        // rather than three.
        //
        // KEYED BY PHASE, NOT A SEQUENCE. The phases already exist in
        // FightBeatPlayer and are already timed against the beat's budget --
        // Close's walk-in (CloseSeconds), a Lunge's crouch-and-cross
        // (StaticSwing.WindupSeconds), a Charge's outbound travel
        // (chargeOutSeconds). A phase key picks the drawing for a moment the
        // beat already owns. A list of poses with their own durations would be
        // a second clock, free to disagree with the first about when the blow
        // lands -- which is precisely the class of bug SettleAfter's header
        // and AUDIT.md #59 both record.
        //
        // WHO WEARS WHAT, AND WHEN. Null or empty means unauthored.
        //
        //   approach | windup | Hold                  | Close                 | Lunge / Charge
        //   ---------+--------+-----------------------+-----------------------+----------------------
        //   -        | -      | strike from open      | strike from open      | strike from open
        //            |        | (today, byte for      | (today)               | (today)
        //            |        | byte)                 |                       |
        //   yes      | -      | IGNORED: nothing      | approach during the   | approach during the
        //            |        | travels               | walk-in, strike on    | wind-up, strike on
        //            |        |                       | arrival               | impact
        //   -        | yes    | windup from open,     | strike during the     | windup during the
        //            |        | beat BUYS a wind-up   | walk-in, windup on    | wind-up it already
        //            |        | wait, strike on       | arrival, beat BUYS a  | has, strike on impact
        //            |        | impact                | wind-up wait, strike  |
        //            |        |                       | on impact             |
        //   yes      | yes    | as "-|yes"; approach  | approach, windup,     | approach, windup,
        //            |        | IGNORED               | strike                | strike
        //
        // A bought wind-up is charged to the beat's own `spent` (see
        // FightBeatPlayer's SettleAfter call), so authoring one shortens the
        // settle rather than lengthening the beat. The strike is always set at
        // the impact instant BEFORE PoseVictims and FlashTarget, because
        // SetStance is what re-syncs the hit-flash silhouette to the drawing
        // under it.
        public string ActorApproachStance;
        public string ActorWindupStance;

        // THE TABLE ABOVE, AS TWO PURE FUNCTIONS -- WHICH drawing at which
        // moment. WHEN those moments fall stays in FightBeatPlayer, which is
        // the only thing that knows how long a walk-in or a crouch takes.
        //
        // Here rather than inline in the coroutine for the reason
        // ImpactFraction and QueueToShow are: Core cannot be reached from an
        // EditMode test, so a precedence rule written inside PlayBeats would
        // only ever be exercised by watching a fight -- and the interesting
        // half of this rule is the fallbacks, which are exactly the cases
        // nobody thinks to watch for.
        //
        // WHAT THE ACTOR OPENS THE BEAT IN.
        public static string OpenStanceFor(StageApproach approach, string strikeStance,
                                           string approachStance, string windupStance)
        {
            Normalise(strikeStance, ref approachStance, ref windupStance);

            switch (approach)
            {
                // Nothing crosses, so there is no travel for an approach pose
                // to be worn through -- authoring one on a Hold is ignored,
                // and ignored rather than refused because a skill's approach
                // can be edited without its poses being rewritten.
                case StageApproach.Hold: return windupStance ?? strikeStance;

                // The one approach with a real boundary inside it: it arrives
                // before the blow opens, so the walk-in gets the approach pose
                // and the wind-up gets its own moment (ArrivalStanceFor).
                case StageApproach.Close: return approachStance ?? strikeStance;

                // A Lunge or a Charge has exactly ONE pre-impact interval, and
                // that interval IS the travel -- the crouch-and-cross is the
                // swing, not something in front of it. So the approach pose is
                // the one that fits it, and a wind-up pose stands in only when
                // no approach pose was authored. Authoring both on a Lunge
                // therefore drops the wind-up rather than inventing a halfway
                // point inside a wait that has no halves.
                default: return approachStance ?? windupStance ?? strikeStance;
            }
        }

        // WHAT A Close CHANGES INTO THE MOMENT IT ARRIVES, or null for "keep
        // wearing what it opened in". Null for every other approach: they have
        // no arrival distinct from their impact.
        public static string ArrivalStanceFor(StageApproach approach, string strikeStance,
                                              string approachStance, string windupStance)
        {
            if (approach != StageApproach.Close) return null;

            Normalise(strikeStance, ref approachStance, ref windupStance);
            if (windupStance == null) return null;

            string open = OpenStanceFor(approach, strikeStance, approachStance, windupStance);
            return windupStance == open ? null : windupStance;
        }

        // EVERY DRAWING THE ACTOR WEARS WHILE IT IS STANDING AT THE STAND-OFF
        // POINT, once each, in the order it wears them -- and an EMPTY list
        // when it never changes out of whatever it already has on.
        //
        // The two functions above answer "which pose at which moment" for
        // playback, and both use null to mean "nothing new here, keep wearing
        // what you have". That is exactly right for a caller whose job is to
        // SET a stance and who therefore does nothing on a null. It is a trap
        // for the stand-off, whose job is to MEASURE the poses, because
        // StageActorAnimator.SpanForStance answers a null stance with the
        // IDLE drawing -- a deliberate, documented fallback for a kit missing
        // a pose, and a silent wrong answer here.
        //
        // WHAT THAT COST, MEASURED. The beetle's Barrel Roll is a Charge
        // wearing "turtle_up", which authors no approach or wind-up pose, so
        // ArrivalStanceFor returns null and the reach folded in the beetle's
        // IDLE. Idle reaches 338 canvas pixels past its own centre where
        // turtle_up reaches 212, so the stand-off was set by a drawing 126
        // canvas pixels wider than the one on screen: at enemy-slot depth
        // that is ~84 stage pixels of daylight on a Charge whose whole
        // contract (StageStandOff.ChargeGap) is to end AGAINST what it hit.
        // Photographed at 85.5px by MeleeStandOffCaptureTests.
        //
        // Returning the list rather than three nullable strings is the point:
        // there is no null left for a caller to hand to a measurement, so the
        // mistake stops being expressible rather than being documented again.
        public static IReadOnlyList<string> StandOffStancesFor(StageApproach approach, string strikeStance,
                                                               string approachStance, string windupStance)
        {
            var worn = new List<string>(3);
            Add(worn, OpenStanceFor(approach, strikeStance, approachStance, windupStance));
            Add(worn, ArrivalStanceFor(approach, strikeStance, approachStance, windupStance));
            Add(worn, strikeStance);
            return worn;
        }

        // Blank is unauthored here too -- OpenStanceFor passes a whitespace
        // strike straight back out (Normalise only ever cleans the two phase
        // poses), and a whitespace stance resolves to the idle exactly like a
        // null one does.
        private static void Add(List<string> worn, string stance)
        {
            if (string.IsNullOrWhiteSpace(stance) || worn.Contains(stance)) return;
            worn.Add(stance);
        }

        // Blank is unauthored, and NO STRIKE MEANS NO PHASES.
        //
        // The second half is the load-bearing one: a phase pose is what is
        // worn BEFORE the strike, so with no strike recorded there is nothing
        // to change back into at impact -- and the return-to-idle at the end
        // of a beat walks Stances, which would not mention the actor. The
        // figure would hold its wind-up for the rest of the fight.
        private static void Normalise(string strikeStance, ref string approachStance, ref string windupStance)
        {
            bool none = string.IsNullOrWhiteSpace(strikeStance);

            approachStance = none || string.IsNullOrWhiteSpace(approachStance) ? null : approachStance;
            windupStance = none || string.IsNullOrWhiteSpace(windupStance) ? null : windupStance;
        }

        // ---- what somebody BECOMES on this beat --------------------------------
        //
        // The stance folder a combatant is drawn from after this beat's impact
        // instant, "" for "back to their own art". Null on every beat but a
        // transform's, which is all but one skill in the game.
        //
        // RECORDED, NOT READ LIVE, for the reason the vitals snapshot and the
        // turn queue are: resolution runs the whole round in one synchronous
        // pass, so `actor.Transformation` is already set by the time the
        // transform's own beat opens. A view reading live state would draw the
        // Black Ram from the beat's first frame -- before the flash, before
        // the shake, before anything said it had happened.
        //
        // The REVERT is not recorded here and deliberately so: a transform
        // expires at its holder's turn START (FightSession.TickTransform),
        // which is not an action and opens no beat. It is instead handled by
        // the post-playback resync that puts every combatant's worn folder
        // back on live Transformation state -- so the ram changes back at the
        // END of the round in which its timer ran out.
        public Dictionary<CombatantState, string> Forms;
        public readonly List<string> Messages = new List<string>();
        public Dictionary<CombatantState, Vitals> Snapshot;

        // The voice line this beat's TARGET earned, if any. Decided when the
        // beat resolves and carried, not looked up during playback: live health
        // has already moved on by then, so asking "are they low?" at playback
        // time would answer for the end of the round rather than for this blow.
        public string TargetVoiceId;
        public VoiceLine TargetVoiceLine;
        public bool TargetVoiceOncePerFight;
        public bool HasTargetVoice;

        // What the ACTOR says landing the blow -- a regular Attack's own hit
        // grunt, decided at record time same as the target's voice and for the
        // same reason.
        public string ActorVoiceId;
        public VoiceLine ActorVoiceLine;
        public bool HasActorVoice;

        public CombatantState Actor;
        public CombatantState Target;

        // A spell's presentation, RECORDED rather than played at the moment it
        // resolves. Everything else about a beat already works this way and for
        // the same reason: one command resolves the whole round in a single
        // pass, so anything fired during resolution fires for every action at
        // once, before the first one has been drawn.
        // CARRIED WHOLE. Six fields lived here -- path, seconds, impact frame,
        // from-caster, depart frame, sfx -- and every one had to be copied in by
        // hand from a skill or an enemy that already held the same six. See
        // SpellPresentation for the measurement that collapsed them.
        public SpellPresentation Vfx = new SpellPresentation();

        // EVERYONE ELSE THE EFFECT SHOULD BE DRAWN ON, beyond Target.
        //
        // Almost always empty. It exists for the shape of spell that hits more
        // than one thing at once: ResolveDamageAll opens ONE beat aimed at the
        // first living enemy, which was correct for the damage -- the numbers
        // are recorded per enemy -- and quietly wrong for the art. Three rats
        // took the hit and one of them got the animation.
        //
        // Target stays the primary and is what everything else about a beat
        // means by "the target". This is only ever read by the view.
        public List<CombatantState> SplashTargets;

        // WHAT EACH OF THEM ACTUALLY TOOK. Null for every beat that lands on
        // one thing, which is nearly all of them -- see BeatTargetResult for
        // why the single Amount above could not answer for a sweep and what
        // shipped wrong while it was the only answer available.
        public List<BeatTargetResult> Results;

        // Whether this beat has per-target numbers worth reading. The one
        // question a view asks before deciding which of the two it is drawing,
        // so neither side has to repeat the null-and-empty check.
        public bool HasPerTargetResults => Results != null && Results.Count > 0;

        // WHETHER THE ACTOR CROSSES THE STAGE, AND HOW FAR.
        //
        // Was a bool, ActorHoldsPosition, and a bool could only ever answer one
        // of the two questions a blow asks. "Does it move" was enough while
        // every skill was a spell -- a cast stands still, a swing leans in --
        // and stopped being enough the moment monsters got melee skills: the
        // Warden's Grapple and the Treant's Trunk Slam both resolve through the
        // cast path, so both inherited "hold" and both connected from across
        // the stage without leaving their mark.
        //
        // Set by the ACTION rather than inferred, as the bool was and for the
        // same reason: it must still be right for a skill with no art at all.
        public StageApproach Approach;

        // Kept as the reading it always was, so the one call site that asks the
        // yes/no question does not have to learn a third answer it does not
        // care about.
        public bool ActorHoldsPosition => Approach == StageApproach.Hold;

        // A FLOOR UNDER HOW HARD THIS BEAT KICKS THE STAGE, 0..1.
        //
        // Everything else derives its weight from the damage, which is right
        // almost always and useless for the case this exists for: the Warden's
        // Roar deals nothing at all, so it weighed nothing, so the loudest
        // moment in its fight was also the only silent one. A summon, a
        // transformation and a taunt are all events the floor has to feel and
        // the arithmetic cannot see.
        //
        // A FLOOR, not an override. A damaging skill that also authors one
        // still shakes by its damage when that is the larger of the two, so
        // authoring this can only ever add.
        public float Shake;

        // WHAT THE FORM THE ACTOR IS WEARING ADDS TO THIS BLOW -- the contact
        // effect a TransformHitCue authored, recorded at CommitBeat for any
        // beat the holder landed damage with.
        //
        // A SECOND SLOT RATHER THAN A MERGE INTO Vfx, and the reason is the
        // cue rather than the layers. A layered presentation's hitCueSeconds
        // is THE instant the blow lands and nothing derives it
        // (docs/ART_PIPELINE.md 5b); two presentations have two of them, and
        // appending one's layers to the other's array would silently make the
        // form's contact effect play against the skill's cue -- or force a
        // choice between two authored cues that both mean "now". sfxPath and
        // castSfxPath collide the same way, one clip each. Kept apart, each
        // presentation keeps its own clock and the view plays both: a headbutt
        // that already draws something draws both, which is the whole
        // requirement.
        //
        // Null for every beat in the game except one worn form's landed hits.
        public SpellPresentation FormVfx;

        // AND THE FLOOR UNDER ITS HIT-STOP, in seconds -- the twin of Shake
        // above, for the one reaction that had no authored floor at all.
        // FightBeatPlayer.HitStopFor takes the larger of the blow's own weight
        // and this. Zero for everything that authored none.
        //
        // NOT ONLY A WORN FORM'S ANY MORE, despite the name -- a poolTiers
        // skill's fired tier floors this exact field through the same rule
        // (FightSession.Beats.ApplyHitCueFloor). Kept as one slot rather than
        // a second "PoolTierHitStopSeconds" beside it: the question this
        // answers ("how hard did the blow land, floored by SOMETHING besides
        // the damage number") has one answer per beat, and a beat authoring
        // both a worn form AND a fired tier still wants the larger of the two
        // floors, which is what the shared rule already computes.
        public float FormHitStopSeconds;

        // Vitals as they stood BEFORE this beat's action. Only a spell beat
        // needs it: the bolt takes most of a second to arrive, and dropping the
        // target's HP the instant the beat opens shows the damage before the
        // spell has left the ceiling. Melee lands on the same frame it starts,
        // so it has nothing to wait for.
        public Dictionary<CombatantState, Vitals> PreSnapshot;

        public bool HasSpellAnimation => Vfx != null && Vfx.HasAnimation;

        // THE POOL TIER THIS CAST FIRED, if any -- 0 for every beat that is
        // not a poolTiers skill's cast, which is nearly all of them (multiplier
        // is validated >= 1 by SkillEntryResolver, so 0 cannot be a real fired
        // tier and is a safe "none" sentinel). Read by the view for a floating
        // tag and asserted on directly by tests, alongside the fight log's own
        // text -- see PoolTierResolution.Label for where the two agree.
        public float PoolTierDamageMultiplier;

        // THE TIER'S OWN HIT CUE -- staged at cast time (FightSession.Skills'
        // ResolveDamageSingle) and applied through the SAME floor a worn
        // form's Hit is, at CommitBeat -- see FightSession.Beats.
        // ApplyHitCueFloor. Null for every beat but a fired tier's.
        public TransformHitCue PoolTierCue;

        // How far through the animation the impact falls, as a fraction of its
        // total length. Pure arithmetic -- no Resources.Load, no beat, nothing
        // Unity-specific -- so the exact numbers behind "frame 3 of 6 lands
        // halfway through" are pinned by a test rather than only exercised
        // indirectly through a live animation.
        //
        // Falls back to the midpoint when the frame count cannot be determined
        // (art missing, or a folder that failed to load), which is still nearer
        // the truth than the end.
        public static float ImpactFraction(int impactFrame, int frameCount)
        {
            if (frameCount <= 0)
            {
                return 0.5f;
            }

            // Counting from 1, and clamped: frame 3 of 6 means three frames
            // have shown, so the impact is three sixths of the way in.
            int clamped = impactFrame < 1 ? 1 : impactFrame > frameCount ? frameCount : impactFrame;
            return clamped / (float)frameCount;
        }
    }
}
