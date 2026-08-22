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
            _recordingBeat = new CombatBeat
            {
                Actor = actor,
                Target = target,
                PreSnapshot = SnapshotVitals(),
                ActorHoldsPosition = isCast,
            };

            if (isCast)
            {
                SetStance(actor, Stances.Cast);
            }
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

            // Captured before the turn advances, so Current really is this
            // beat's actor. The depth comes from FightHudSpec rather than, as
            // in v1, from the LENGTH OF A UI ARRAY -- combat logic reaching
            // into the view to decide how far ahead to simulate.
            _recordingBeat.TurnOrder = _encounter?.UpcomingTurns(_initiativeSlots);

            _beats.Add(_recordingBeat);
            _recordingBeat = null;
        }

        private void RecordBeatAmount(int amount, bool isHealing = false)
        {
            if (_recordingBeat == null || amount <= 0)
            {
                return;
            }

            _recordingBeat.Amount = amount;
            _recordingBeat.IsHealing = isHealing;
        }

        // Roots the actor in place for this beat, for art whose plain-attack
        // pose is itself stationary. Separate from the `isCast` constructor
        // argument because it is decided later -- the intent has to be resolved
        // first -- and because it must NOT also force the cast stance.
        private void HoldActorPosition()
        {
            if (_recordingBeat == null) return;
            _recordingBeat.ActorHoldsPosition = true;
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

        private Dictionary<CombatantState, Vitals> SnapshotVitals()
        {
            var snapshot = new Dictionary<CombatantState, Vitals>();
            if (_encounter == null) return snapshot;

            foreach (var c in _encounter.PlayerParty.Concat(_encounter.Enemies))
            {
                snapshot[c] = new Vitals(c.CurrentHealth, c.CurrentMana, SignatureOf(c));
            }

            return snapshot;
        }

        // Signature resource is per-character and optional; anything without
        // one reports 0 rather than being absent from the snapshot, so playback
        // never has to test for a missing key.
        private int SignatureOf(CombatantState combatant) =>
            combatant.Signature?.Current ?? 0;

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
