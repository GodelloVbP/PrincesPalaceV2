using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // Beat RECORDING. Playback is the view's half and stays in Core.
    //
    // Ported from v1's FightController.Beats.cs with its reasoning intact --
    // this file is mostly comments on purpose, because every one of these rules
    // was learned from a bug that looked like something else.
    public sealed partial class FightSession
    {
        private readonly List<CombatBeat> _beats = new List<CombatBeat>();
        private readonly List<string> _immediateMessages = new List<string>();
        private CombatBeat _recordingBeat;

        // `isCast` is the one thing a beat cannot work out for itself, and it
        // decides whether the actor crosses the stage or stands still.
        private void BeginBeat(CombatantState actor, CombatantState target, bool isCast = false)
        {
            // A cast is rooted and a swing leans in. Either can be overruled
            // afterwards -- by the skill's own authored approach (see
            // ResolveDamageSingle) or by HoldActorPosition below -- which is
            // why this is a starting position rather than a fact.
            NewBeat(BeatCause.Action, actor, target, isCast ? StageApproach.Hold : StageApproach.Lunge);

            if (isCast)
            {
                SetStance(actor, Stances.Cast);
            }

            // THE ACTION SEAM FOR POOL DECAY. Every action a combatant takes
            // opens a beat, so this is the one place that can say "this turn
            // was not idle" without a flag at each verb. Only a pool authored
            // decayUnless: AnyAction reads it; the shipped rule is the
            // narrower Damage one, reported from the damage funnel instead.
            NotePoolActivity(actor, PoolActivity.Action);
        }

        // THE CONSTRUCTOR HALF OF OPENING A BEAT, and nothing else: it builds
        // the beat and makes it the one being recorded. BeginBeat is this plus
        // the action seam below; OpenStatusTickBeat (FightSession.Riders) is
        // this plus its element, and ExpireTransform (FightSession.Talents)
        // is this plus the revert -- both deliberately NOT the action seam:
        // a tick is done to its holder and a form runs out on the clock,
        // neither is something the holder did. Keeping the construction
        // here is what stops the openers drifting apart on what a fresh
        // beat holds, and `cause` is required so none of them can forget to
        // say which it is (CombatBeat.IsAction reads it). `preSnapshot` is a
        // caller's already-taken snapshot (a tick captures the vitals before
        // its row lands); null takes one now.
        private void NewBeat(BeatCause cause, CombatantState actor, CombatantState target, StageApproach approach,
                             Dictionary<CombatantState, Vitals> preSnapshot = null)
        {
            _recordingBeat = new CombatBeat
            {
                Cause = cause,
                Actor = actor,
                Target = target,
                PreSnapshot = preSnapshot ?? SnapshotVitals(),
                Approach = approach,
            };
        }

        // BOTH POOLS HEAR IT. Which of them cares is the pool's own authored
        // DecayUnless, not the caller's business -- a call site deciding
        // which pool an event is "for" is how the two drift apart.
        private static void NotePoolActivity(CombatantState combatant, PoolActivity kind)
        {
            if (combatant == null) return;

            combatant.PrimaryPool?.NoteActivity(kind);
            combatant.SignaturePool?.NoteActivity(kind);
        }

        // Closes the current beat and queues it. The snapshot is taken HERE, at
        // the moment the action resolved, which is the whole point: by the time
        // playback reaches this beat the live state has already moved on
        // through every later action in the same chain.
        private void CommitBeat()
        {
            if (_recordingBeat == null)
            {
                return;
            }

            _recordingBeat.Snapshot = SnapshotVitals();
            RecordShields(_recordingBeat);

            // Captured before the turn advances, so Current really is this
            // beat's actor. The depth comes from FightHudSpec rather than, as
            // in v1, from the LENGTH OF A UI ARRAY -- combat logic reaching
            // into the view to decide how far ahead to simulate.
            _recordingBeat.TurnOrder = _encounter?.UpcomingTurns(_initiativeSlots);

            // AND WHERE EVERYBODY WAS STANDING, taken HERE rather than at
            // BeginBeat, because a Move's whole effect happens between the two:
            // the swap is written to the party list after the beat opens and
            // before it closes, so a formation captured at the top would be the
            // order the mover was trying to leave. Copied at this instant and
            // never referenced -- see BeatFormation.
            _recordingBeat.Formation = BeatFormation.Capture(_encounter);

            RecordFormHitCue(_recordingBeat);

            _beats.Add(_recordingBeat);
            _recordingBeat = null;

            // A toll whose round started while this beat was open plays as
            // its own beat right after it (FightSession.Rounds.RecordRoundStart).
            RecordPendingTolls();
        }

        // WHAT EVERY HIT-CUE AUTHOR ADDS TO THE BLOW -- a worn transform's
        // contact effect (Transformation.Hit) and a fired poolTiers tier's own
        // cue (CombatBeat.PoolTierCue), the two authors this floor has today.
        //
        // HERE, AT COMMIT, rather than in each of the paths that can land one.
        // "Every landed damaging hit by the holder" is a property of the BEAT
        // -- who acted and whether it landed -- and CommitBeat is the one place
        // every beat in the game passes through. Written into any of the melee
        // paths instead it would be four copies of one rule, and the fifth path
        // (an enemy ability, a relic's free swing, whatever lands next) would
        // silently not have it. No skill id and no character id is involved,
        // which is the point: a second author of a cue gets this for free.
        //
        // LANDED DAMAGE ONLY. A heal, a miss, a buff and the transform's own
        // entry beat all commit through here too, and punctuating those would
        // teach the player that the cue means nothing.
        private static void RecordFormHitCue(CombatBeat beat)
        {
            if (beat == null) return;
            if (beat.IsHealing || beat.Missed || beat.Amount <= 0) return;

            ApplyHitCueFloor(beat, beat.Actor?.Transformation?.Hit);
            ApplyHitCueFloor(beat, beat.PoolTierCue);
        }

        // ONE FLOOR, ANY NUMBER OF AUTHORS. Called once per author above, so
        // a beat that landed both a worn form's contact effect and a fired
        // pool tier keeps the larger of the two floors on each field, rather
        // than whichever author happened to apply last.
        private static void ApplyHitCueFloor(CombatBeat beat, TransformHitCue cue)
        {
            if (cue == null || !cue.IsAuthored) return;

            if (cue.vfx != null && cue.vfx.HasArt) beat.FormVfx = cue.vfx;

            // BOTH ARE FLOORS, so a blow that already reads harder than this
            // cue asks for keeps its own reading -- see CombatBeat.Shake's own
            // header for why a floor rather than an override.
            if (cue.shake > beat.Shake) beat.Shake = cue.shake;

            float hitStop = cue.hitStopSeconds > HitStop.MaxSeconds
                ? HitStop.MaxSeconds
                : (cue.hitStopSeconds < 0f ? 0f : cue.hitStopSeconds);
            if (hitStop > beat.FormHitStopSeconds) beat.FormHitStopSeconds = hitStop;
        }

        // THE TIER A poolTiers CAST FIRED, staged at BeginBeat time (see
        // FightSession.Skills.ResolveDamageSingle) so CommitBeat can float its
        // shake/hit-stop through ApplyHitCueFloor alongside a worn form's, and
        // so the row/log label and any test can read the multiplier straight
        // off the beat rather than re-deriving it from live pool state (which
        // has already been spent by the time a beat plays).
        private void RecordPoolTier(PoolTierResolution.Result result)
        {
            if (_recordingBeat == null || !result.Fired) return;

            _recordingBeat.PoolTierDamageMultiplier = result.Tier.DamageMultiplier;
            _recordingBeat.PoolTierCue = result.Tier.Cue;
        }

        private void RecordBeatAmount(int amount, bool isHealing = false)
        {
            if (_recordingBeat == null || amount <= 0)
            {
                return;
            }

            // ADDS BACK WHATEVER A WARD ALREADY TOOK OUT of `amount`, so
            // Amount reads as the PRE-shield figure again -- the same
            // figure it always represented before Wards existed -- and
            // `Amount - Absorbed` (FightBeatPlayer.ShowSingleAmount/
            // RecoilOne, FightController.StageVisuals.FlashOne, HitStrength.
            // Classify) gives the true health-damage remainder rather than
            // subtracting the ward's share a SECOND time.
            //
            // WHY THIS IS SAFE: `resolveWard` (DamagePipeline.AfterDefences)
            // spends the ward and reduces the number BEFORE it ever reaches
            // this call, through FightSession.Talents.ResolveWard, which
            // records the spent amount into `_recordingBeat.Absorbed`
            // ahead of every caller of THIS method -- so by the time
            // `amount` arrives here it has already had that same figure
            // subtracted out of it once, and adding it back is the exact
            // inverse of that one subtraction. A beat with no ward at all
            // has Absorbed == 0, making this a no-op addition.
            //
            // WHY THIS IS NOT SAFE for the FightSession.Ledger.
            // ApplyAndCountDamage: a signature pool's OWN absorb runs
            // through DealDamage, called immediately before this method
            // with this SAME `amount` -- the pool's share is never baked
            // INTO `amount` the way a ward's is, so adding it back here
            // would double it in the opposite direction. Not reachable by
            // any shipped content today: every pools.json row is authored
            // absorbsDamage: false. The day a pool ships with that flag
            // true, THIS line is the one a fixer needs to revisit, and the
            // fix is very likely two figures on the beat rather than one.
            _recordingBeat.Amount = amount + _recordingBeat.Absorbed;
            _recordingBeat.IsHealing = isHealing;
        }

        // CombatBeat.Absorbed's one WRITE METHOD, called from its two SITES
        // -- FightSession.Ledger.ApplyAndCountDamage (a signature pool's
        // absorb) and FightSession.Talents.ResolveWard (a Ward status's) --
        // see CombatBeat.Absorbed's own header for why there are two and why
        // that is still "recorded once" rather than "computed twice".
        // ADDITIVE rather than an assignment: a beat that lands on more than
        // one shielded body in one pass (a splash, a rider), or drains one
        // target's ward AND its signature pool in the same swing, reports
        // the total a shield or shields kept off health, not just the last
        // one asked.
        private void RecordAbsorbed(int absorbed)
        {
            if (_recordingBeat == null || absorbed <= 0)
            {
                return;
            }

            _recordingBeat.Absorbed += absorbed;
        }

        // PHASE D1: marks the beat currently recording as a dodge. Called
        // instead of RecordBeatAmount, never alongside it -- a swing either
        // lands for a number or it misses, never both, so the two calls
        // never have reason to share a beat. See CombatBeat.Missed's own
        // header for why this is a distinct flag rather than a 0 amount.
        private void RecordMiss()
        {
            if (_recordingBeat == null) return;
            _recordingBeat.Missed = true;
        }

        // Roots the actor in place for this beat, for art whose plain-attack
        // pose is itself stationary. Separate from the `isCast` constructor
        // argument because it is decided later -- the intent has to be resolved
        // first -- and because it must NOT also force the cast stance.
        // The approach a skill authored, applied over whatever BeginBeat
        // assumed. Separate from the constructor argument for the reason
        // HoldActorPosition is: the skill is not known until after the beat has
        // been opened, and opening it later would lose everything recorded in
        // between.
        private void ApproachAs(StageApproach approach)
        {
            if (_recordingBeat == null) return;
            _recordingBeat.Approach = approach;
        }

        private void HoldActorPosition()
        {
            if (_recordingBeat == null) return;
            _recordingBeat.Approach = StageApproach.Hold;
        }

        // The extra combatants a multi-target effect should be DRAWN on. Kept
        // beside RecordSpellPresentation because it is the same kind of fact --
        // something the view needs that the resolution already knows.
        //
        // ADDITIVE, and that is a change from "the last caller wins". One beat
        // can now hit wider than once: a sweep records the enemies it swept,
        // and a Black Ram's splash records the neighbours the SAME blow spilled
        // onto. Replacing would silently drop whichever came first, and the
        // symptom of that is an effect missing from a body that visibly took
        // damage. Duplicates are refused rather than tolerated -- an enemy
        // drawn on twice is two overlapping copies of one effect.
        private void RecordSplashTargets(IEnumerable<CombatantState> targets)
        {
            if (_recordingBeat == null || targets == null) return;

            foreach (var target in targets)
            {
                if (target == null || ReferenceEquals(target, _recordingBeat.Target)) continue;

                if (_recordingBeat.SplashTargets == null)
                {
                    _recordingBeat.SplashTargets = new List<CombatantState>();
                }

                if (!_recordingBeat.SplashTargets.Contains(target))
                {
                    _recordingBeat.SplashTargets.Add(target);
                }
            }
        }

        // WHAT ONE COMBATANT OF SEVERAL TOOK, recorded per enemy as the sweep
        // resolves rather than derived afterwards. It cannot be derived: by the
        // time the beat is committed, live health has already moved through the
        // rest of the round, and the beat's own Amount holds the largest single
        // hit rather than any particular enemy's. See BeatTargetResult.
        private void RecordTargetResult(CombatantState target, int amount, bool missed = false, bool crit = false)
        {
            if (_recordingBeat == null || target == null) return;

            if (_recordingBeat.Results == null)
            {
                _recordingBeat.Results = new List<BeatTargetResult>();
            }

            _recordingBeat.Results.Add(new BeatTargetResult(target, amount, missed, crit));
        }

        // How hard this beat insists the stage is kicked, whatever it landed.
        private void RecordShake(float shake)
        {
            if (_recordingBeat == null || shake <= 0f) return;
            _recordingBeat.Shake = shake;
        }

        private void RecordSpellPresentation(SpellPresentation presentation)
        {
            if (_recordingBeat == null) return;

            // COPIED at the boundary. A beat holding the catalogue's own object
            // would let a fight edit the content it was dealt from.
            _recordingBeat.Vfx = (presentation ?? SpellPresentation.None).Copy();
        }

        // What the beat's target should SAY about the blow that just landed.
        //
        // Domain picks the LINE; Core owns the catalog and falls back to an
        // ordinary grunt when a character has no take for the one asked for.
        // That split is the whole reason VoiceLine left Core: the choice
        // depends on health AT THE MOMENT THE BLOW LANDS, and live health has
        // already moved on by the time a beat plays -- asking "are they low?"
        // during playback answers for the end of the round instead.
        //
        // Keyed off the kit rather than IsPlayerSide, so giving a monster a
        // voice later is content and not a code change.
        private void RecordTargetVoice(CombatantState target)
        {
            var kit = KitFor(target);
            if (_recordingBeat == null || target == null || kit == null) return;

            // Going down outranks everything else -- a character does not grunt
            // and then die, they just die.
            var line = !target.IsAlive
                ? VoiceLine.Down
                : VoiceThresholds.IsLowHealth(target.CurrentHealth, target.MaxHealth)
                    ? VoiceLine.LowHealth
                    : VoiceLine.Hurt;

            _recordingBeat.TargetVoiceId = kit.Id;
            _recordingBeat.TargetVoiceLine = line;

            // "Nearly dead" marks a threshold rather than an event, so it is
            // said once per fight. A grunt is an event and repeats.
            _recordingBeat.TargetVoiceOncePerFight = line == VoiceLine.LowHealth;
            _recordingBeat.HasTargetVoice = true;
        }

        // What the ACTOR says landing a regular attack -- their own hit grunt,
        // played when the blow lands rather than when the swing resolved.
        // Regular attacks only: a skill or spell has its own presentation.
        private void RecordActorVoice(CombatantState actor)
        {
            var kit = KitFor(actor);
            if (_recordingBeat == null || actor == null || kit == null) return;

            _recordingBeat.ActorVoiceId = kit.Id;
            _recordingBeat.ActorVoiceLine = VoiceLine.AttackHit;
            _recordingBeat.HasActorVoice = true;
        }

        // What the current beat is already showing. An effect that hits several
        // targets reports its LARGEST single hit rather than a total that
        // matches no one victim's HP drop -- one beat shows one number.
        private int LargestAmountSoFar => _recordingBeat?.Amount ?? 0;

        private void SetStance(CombatantState combatant, string stance)
        {
            if (_recordingBeat == null || combatant == null) return;
            _recordingBeat.Stances[combatant] = stance;
        }

        // The two poses in front of the strike SetStance just recorded -- see
        // CombatBeat's own precedence table for who wears which and when.
        //
        // ON THE BEAT rather than in the Stances map, because they are not
        // stances OF anybody in the map's sense: the map is keyed by combatant
        // and answers "what is this figure wearing", one answer per figure per
        // beat. These are three answers for one figure at three moments, which
        // is a different question and needs the phase in the key rather than
        // the combatant.
        private void RecordPhaseStances(string approachStance, string windupStance)
        {
            if (_recordingBeat == null) return;

            _recordingBeat.ActorApproachStance = string.IsNullOrWhiteSpace(approachStance) ? null : approachStance;
            _recordingBeat.ActorWindupStance = string.IsNullOrWhiteSpace(windupStance) ? null : windupStance;
        }

        // WHAT THIS COMBATANT BECOMES, as of this beat's impact instant. See
        // CombatBeat.Forms for why the form is recorded rather than read off
        // live Transformation state during playback.
        //
        // Recorded even when the folder is empty: a transform that changes
        // only the numbers still says so, and the view answers an empty folder
        // with "keep your own art" rather than having to distinguish it from a
        // beat that mentioned nobody. Allocated lazily, like Results and
        // SplashTargets, because one skill in the game uses it.
        private void RecordForm(CombatantState combatant, string spritePath)
        {
            if (_recordingBeat == null || combatant == null) return;

            if (_recordingBeat.Forms == null)
            {
                _recordingBeat.Forms = new Dictionary<CombatantState, string>();
            }

            _recordingBeat.Forms[combatant] = spritePath ?? "";
        }

        // A pose decided AFTER the beat that earned it was committed -- the
        // victory stance, which is settled once the last enemy is already down
        // and its beat closed. Same retro-attach rule AppendMessage follows, and
        // for the same reason: it belongs to the moment it describes.
        private void PoseOnLatestBeat(CombatantState combatant, string stance)
        {
            if (combatant == null) return;

            if (_recordingBeat != null)
            {
                _recordingBeat.Stances[combatant] = stance;
                return;
            }

            if (_beats.Count > 0)
            {
                _beats[_beats.Count - 1].Stances[combatant] = stance;
            }
        }

        // Log lines belong to the beat that produced them, so they appear in
        // step with the blow rather than all at once up front.
        //
        // The retro-attach rule (AUDIT #13) is the interesting half. Everything
        // a turn decides AFTER its beat was committed -- an extra turn earned
        // by a kill, the victory block -- describes a moment that has not been
        // shown yet. Written straight to the immediate list it lands OLDER than
        // the beat messages describing the very blow that caused it, and the
        // trim drops from the front: the consequence gets discarded to make
        // room for its own cause. Attaching it to the last queued beat puts it
        // back in causal order.
        //
        // This SIMPLIFIES in the split. v1 needed an `_isPlayingBack` gate,
        // because resolution and playback shared a queue and a message arriving
        // during playback was genuinely current. Here resolution finishes
        // entirely before the view drains anything, so there is no such window
        // and no gate.
        // Public, not internal: the Core adapter appends fight-opening lines
        // ("The bog witch blocks your path!") that no command produces, and
        // they have to obey the same ordering rule as everything else.
        public void AppendMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (_recordingBeat != null)
            {
                _recordingBeat.Messages.Add(message);
                return;
            }

            if (_beats.Count > 0)
            {
                _beats[_beats.Count - 1].Messages.Add(message);
                return;
            }

            _immediateMessages.Add(message);
        }

        // One watch per holder, fed at every commit so a break is seen by the
        // beat that caused it even when the placement ended between two beats.
        private readonly Dictionary<CombatantState, PlantedShieldWatch> _shieldWatches =
            new Dictionary<CombatantState, PlantedShieldWatch>();

        private void RecordShields(CombatBeat beat)
        {
            if (_encounter == null) return;

            foreach (var member in _encounter.PlayerParty)
            {
                if (member == null) continue;

                if (!_shieldWatches.TryGetValue(member, out var watch))
                {
                    watch = new PlantedShieldWatch();
                    _shieldWatches[member] = watch;
                }

                var snapshot = watch.Snapshot(member.PlantedShield);
                if (snapshot.Placed || snapshot.Broke) beat.Shields[member] = snapshot;
            }
        }

        private Dictionary<CombatantState, Vitals> SnapshotVitals()
        {
            var snapshot = new Dictionary<CombatantState, Vitals>();
            if (_encounter == null) return snapshot;

            foreach (var c in _encounter.PlayerParty.Concat(_encounter.Enemies))
            {
                snapshot[c] = new Vitals(c.CurrentHealth, c.CurrentMana, SignatureOf(c),
                                         c.MaxHealth, c.MaxMana);
            }

            return snapshot;
        }

        // Signature resource is per-character and optional; anything without
        // one reports 0 rather than being absent from the snapshot, so playback
        // never has to test for a missing key.
        private int SignatureOf(CombatantState combatant) =>
            combatant.SignaturePool?.Current ?? 0;

        // Everything recorded since the last drain, handed to the view. Drains
        // rather than exposes, so a beat cannot be played twice.
        public IReadOnlyList<CombatBeat> DrainBeats()
        {
            var drained = _beats.ToList();
            _beats.Clear();
            return drained;
        }

        public IReadOnlyList<string> DrainImmediateMessages()
        {
            var drained = _immediateMessages.ToList();
            _immediateMessages.Clear();
            return drained;
        }

        // The stance names the view understands. Strings rather than an enum
        // because they are folder names under Resources -- see the stance
        // manifest; Domain names them, the view resolves them to art.
        public static class Stances
        {
            public const string Idle = "idle";
            public const string Attack = "attack";
            public const string Cast = "cast";
            public const string Hurt = "hurt";
            public const string Defeated = "defeated";
            public const string Victory = "victory";
        }
    }
}
