using System.Collections.Generic;

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
using PrincesPalace.Domain.Content;
    // Vitals -- so the recording half of the beat system needs no Unity at all,
    // and the whole of it becomes testable in EditMode.
    public sealed class CombatBeat
    {
        // How much the target took (or was healed) in THIS beat. Carried on the
        // beat rather than read from live state at playback time for the same
        // reason as the vitals snapshot: by the time a beat plays, later
        // actions in the same chain have already landed.
        public int Amount;
        public bool IsHealing;

        // The turn queue as it stood when this beat resolved. Same reasoning as
        // the vitals snapshot: live state has already run the entire chain by
        // the time any beat is played, so reading it during playback shows the
        // END of the round for every beat -- which is why the tracker looked
        // frozen. With a solo party the chain always ends back on the player,
        // so "NOW" never appeared to move off them.
        public IReadOnlyList<CombatantState> TurnOrder;

        public readonly Dictionary<CombatantState, string> Stances = new Dictionary<CombatantState, string>();
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

        // A cast does not cross the stage. Set by the action rather than
        // inferred, so it is still right for a skill with no art at all.
        public bool ActorHoldsPosition;

        // Vitals as they stood BEFORE this beat's action. Only a spell beat
        // needs it: the bolt takes most of a second to arrive, and dropping the
        // target's HP the instant the beat opens shows the damage before the
        // spell has left the ceiling. Melee lands on the same frame it starts,
        // so it has nothing to wait for.
        public Dictionary<CombatantState, Vitals> PreSnapshot;

        public bool HasSpellAnimation => Vfx != null && Vfx.HasAnimation;

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
