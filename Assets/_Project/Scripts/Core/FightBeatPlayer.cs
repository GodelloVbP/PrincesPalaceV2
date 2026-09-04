using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace
{
    // Plays back what the session already resolved.
    //
    // The whole round landed in one synchronous pass before this runs, so every
    // beat here describes a moment that has ALREADY happened. That is what makes
    // the snapshots load-bearing: painting live state during playback would show
    // the end of the round on the first blow.
    //
    // PAINT FIRST, THEN MOVE. Each beat paints its own snapshot before it
    // animates, so the numbers on screen are the numbers as they stood when that
    // blow landed. Reversing the two makes a beat show the NEXT beat's health
    // for a frame, which reads as a hit landing early.
    public class FightBeatPlayer : MonoBehaviour
    {
        // Clear of the figure's crown, so the punch does not start inside the
        // sprite's own outline.
        private const float PopupHeadroom = 18f;

        [SerializeField] internal DamagePopup[] popups;

        // How long a beat holds, and the gap between beats.
        public const float BeatHoldSeconds = 0.45f;
        public const float BeatGapSeconds = 0.3f;

        // THE SETTLE AFTER THE BLOW, AND IT IS A FLOOR RATHER THAN A REMAINDER.
        //
        // The hold used to be purely what was LEFT of BeatHoldSeconds once the
        // stance had been paid for, and for every animated actor in the game
        // that remainder was negative. Every stance is six frames, every one of
        // them is authored at 0.08s -- the manifest's default, which all four
        // authored actors also happen to state -- so a stance costs 0.48s
        // against a 0.45s budget. `remaining > 0f` was therefore never true.
        //
        // The effect is not subtle once you know to look for it: an animated
        // blow got NO pause at all. The last frame of the swing was followed
        // immediately by the return to idle and the next beat, so blows ran
        // into each other with nothing between them and the damage number had
        // no still frame to be read against. It reads as fast and twitchy,
        // which is exactly what it is.
        //
        // FrameHoldCurve's own header predicted this in as many words -- "a
        // stance that quietly ran long would eat the pause after it" -- and it
        // did, from the moment stances went to six frames.
        //
        // A floor rather than a bigger budget, because raising BeatHoldSeconds
        // would fix the animated actors by making the flat-art ones sit even
        // longer doing nothing. This leaves a flat-art beat exactly as it was
        // (0.08 + 0.37 = 0.45) and gives an animated one its settle back.
        public const float MinSettleSeconds = 0.16f;

        // How long to hold after a stance of `stanceSeconds` has played.
        //
        // Split out so the arithmetic can be tested without a fight: this is
        // the line that silently went to zero, and nothing could see it,
        // because "no pause" and "a pause of zero" are the same code path.
        public static float SettleAfter(float stanceSeconds)
        {
            float remaining = BeatHoldSeconds - stanceSeconds;
            return remaining < MinSettleSeconds ? MinSettleSeconds : remaining;
        }

        // ---- how hard this beat hit ------------------------------------------

        // BOTH OF THESE ARE ONE LINE, and deliberately: the arithmetic lives in
        // Domain.Combat.Session.HitStop where an EditMode test can reach it,
        // and what is left here is reading a beat -- which is the only part
        // that needs to know what a CombatBeat is.
        public static float Weight(CombatBeat beat) =>
            beat == null ? 0f : HitStop.Weight(beat.Amount, beat.Target?.MaxHealth ?? 0, beat.IsHealing);

        // How loud this beat's effects should be, 0..1 -- the weight put
        // through the shared response curve. Every one of the three reactions
        // reads THIS rather than Weight, so they cannot disagree about how hard
        // the blow was; see HitStop.Response for why the curve exists at all.
        public static float Strength(CombatBeat beat) => HitStop.Response(Weight(beat));

        // What the STAGE feels, which is not always what the target felt. Takes
        // the larger of the blow's own weight and whatever floor the beat
        // authored, so a skill that deals nothing can still land -- see
        // CombatBeat.Shake.
        public static float ShakeStrength(CombatBeat beat) =>
            beat == null ? 0f : Mathf.Max(Strength(beat), Mathf.Clamp01(beat.Shake));

        public static float HitStopFor(CombatBeat beat) => HitStop.SecondsFor(Weight(beat));

        // The test seam. A PlayMode test that had to wait real seconds per beat
        // would take longer than the whole EditMode suite; this lets one run a
        // twelve-beat round in well under a second without changing a single
        // rule about ordering.
        public static float BeatSpeedMultiplier = 1f;

        public static float Scaled(float seconds) =>
            BeatSpeedMultiplier <= 0f ? 0f : seconds / BeatSpeedMultiplier;

        public bool IsPlaying { get; private set; }

        // A read-only view of the pool, for the tests that assert it comes back
        // whole. Read-only rather than exposing the field: nothing outside this
        // class has any business swapping the array, and a leak test that could
        // replace the pool would not be testing the leak.
        public IReadOnlyList<DamagePopup> Popups => popups ?? System.Array.Empty<DamagePopup>();

        private Coroutine _running;
        private Action _onFinished;

        // Set by the controller so playback can find where a combatant is
        // standing without knowing anything about stages or slots.
        internal Func<CombatantState, RectTransform> SlotFor;

        // The world-space MIRROR of SlotFor -- a rig-resolved combatant's
        // own animator, so a lunge/recoil/punch moves the rig itself and
        // not only the (hidden) uGUI slot's shadow/nameplate rack. Returns
        // null for a combatant with no world slot animator resolvable,
        // which every AlsoPlay*/AlsoPunch* call below already treats as
        // "nothing to do" via the null-conditional -- see
        // FightController.WorldAnimatorFor for why this is safe to call
        // unconditionally rather than gated on "is this rig-resolved".
        internal Func<CombatantState, StageActorAnimator> WorldAnimatorFor;
        // Paints a set of vitals. Called TWICE per beat -- once with what stood
        // before the blow, once with what stood after.
        internal Action<IReadOnlyDictionary<CombatantState, Vitals>> PaintVitals;
        internal Action<string> PushLine;

        // Starts a beat's spell effect, and says how long after the beat opens
        // its blow actually lands. Both live on the controller because both need
        // the loaded frames.
        internal Action<CombatBeat> PlayVfx;
        internal Action<CombatBeat> FadeTheFallen;
        internal Func<CombatBeat, float> ImpactDelayFor;
        internal Action StopVfx;

        // Poses an actor and repaints the stage. The controller owns the sprite
        // lookup; playback only says WHO holds WHAT and WHEN.
        internal Action<CombatantState, string> SetStance;

        // How hard to kick the stage, 0..1. The view owns which transforms
        // that means -- see FightController.ShakeStage -- and playback owns
        // when and how hard, because only it knows where the impact is.
        internal Action<float> ShakeStage;

        // Raised by Flush, read by every reaction coroutine. See Flinch.
        private bool _flushed;

        // How a combatant's chosen stance actually PLAYS -- windup, impact
        // timing, follow-through, whether it returns to rest -- regardless
        // of whether it is a frame sheet or a rig underneath. Resolved by
        // the controller, the only thing that knows which art style a
        // combatant wears; the `abandon` param is threaded straight through
        // to whichever StanceStepper/RigStancePlayer call ends up driving
        // it, so a flinch on a victim can still be cut short mid-beat the
        // way it always could.
        internal Func<CombatantState, string, Func<bool>, IStancePlayback> PlaybackFor;
        internal Action<CombatBeat> FlashTarget;

        // The attack graphic and the impact burst, for a blow that draws
        // neither for itself.
        //
        // SEPARATE FROM PlayVfx rather than folded into it, because the two
        // answer different questions. PlayVfx plays what a SKILL authored --
        // its own sheet, its own size, its own travel, all content. This is
        // the house's default contact language for a swing that authored
        // nothing, and it fires at the impact instant rather than at the top
        // of the beat. Merging them would put "did this beat author a spell"
        // inside a method whose whole job is playing the spell it authored.
        internal Action<CombatBeat> PlayContactFx;

        // THE WIRING SEAM FOR A PLAYMODE TEST, and the only public door onto
        // any of the delegates above.
        //
        // Every one of them is internal and Core grants InternalsVisibleTo to
        // the EDITOR assembly alone -- deliberately, so a PlayMode test cannot
        // reach into controller state to make itself pass (Core/AssemblyInfo.
        // cs states the rule). That costs nothing anywhere else on this
        // screen, because everything else is observable by driving a real
        // fight. WHICH BEATS GET CONTACT EFFECTS is not: it is a decision
        // rather than a picture, and reproducing its six cases through content
        // would mean authoring six enemies to assert a rule that has nothing
        // to do with any of them.
        //
        // Deliberately narrow -- two delegates in, nothing readable back out,
        // the same posture FightController's own *ForTest seams take.
        public void WirePlaybackForTest(Func<CombatantState, string, Func<bool>, IStancePlayback> playbackFor,
                                        Action<CombatBeat> playContactFx)
        {
            PlaybackFor = playbackFor;
            PlayContactFx = playContactFx;
        }

        public void Play(IReadOnlyList<CombatBeat> beats, Action onFinished)
        {
            // CLEARED BEFORE THE FLUSH, so the flush below has nobody to notify.
            //
            // This playback supersedes the last one, and the caller is about to
            // be told when THIS one ends. Letting Flush fire the old callback
            // here would clear the controller's busy flag a frame after it set
            // it, and the player could act in the middle of the round they just
            // started.
            _onFinished = null;
            Flush();

            // AFTER the flush, which raises it. A run that supersedes another
            // is still a run, and a latched flag would make every reaction in
            // it give up on its first frame.
            _flushed = false;

            _onFinished = onFinished;
            IsPlaying = true;
            _running = StartCoroutine(PlayBeats(beats));
        }

        // Stops playback dead and puts every borrowed thing back.
        //
        // Reclaiming the popups here is the point. v1's Flush left them running,
        // and its DamagePopup.Clear had no call sites at all, so an abandoned
        // fight leaked one popup per in-flight number out of a pool of six that
        // is never refilled.
        public void Flush()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            // THE REACTIONS TOO. Each victim's flinch is its own coroutine so
            // it can outlive the beat that caused it -- which means stopping
            // the main one leaves them writing frames into a stage the next
            // encounter has already reused. The flag is belt to the braces:
            // StopCoroutine handles the ones in flight, and any that starts
            // between here and the rebind reads it and gives up.
            _flushed = true;

            foreach (var pair in _flinching)
            {
                if (pair.Value != null) StopCoroutine(pair.Value);
            }

            _flinching.Clear();

            if (popups != null)
            {
                foreach (var popup in popups)
                {
                    if (popup != null) popup.Reclaim();
                }
            }

            // An abandoned fight must not leave a spell frozen mid-frame over an
            // empty stage. Same rule as the popups, and the same reason it is
            // here rather than at the call sites.
            StopVfx?.Invoke();

            IsPlaying = false;

            // AND WHOEVER WAS WAITING IS TOLD, which it never was.
            //
            // FightController sets _isBusy before Play and clears it ONLY in
            // this callback, and _isBusy is half of CanAct. So a playback
            // stopped rather than finished left the controller busy for the
            // rest of the fight: every verb dead, every click ignored, nothing
            // on screen saying why. "The buttons just dont work."
            //
            // Same capture-null-invoke as the normal completion path, so a
            // callback that reaches back into this player cannot be run twice.
            var finished = _onFinished;
            _onFinished = null;
            finished?.Invoke();
        }

        private void OnDisable()
        {
            // A scene change mid-round is exactly the abandoned-fight case, and
            // it must not be the caller's job to remember.
            Flush();
        }

        private IEnumerator PlayBeats(IReadOnlyList<CombatBeat> beats)
        {
            foreach (var beat in beats)
            {
                // THE PRE-SNAPSHOT, which is the whole reason the session
                // records two.
                //
                // A spell's bolt takes most of a second to arrive. Dropping the
                // target's HP the instant the beat opens shows the damage before
                // the spell has left the ceiling -- the number moves, then the
                // thing that caused it happens. Painting what stood BEFORE the
                // blow and only landing the after-state at the impact frame is
                // what puts cause back in front of effect.
                PaintVitals?.Invoke(beat.PreSnapshot);

                if (beat.Messages != null)
                {
                    foreach (var line in beat.Messages) PushLine?.Invoke(line);
                }

                // THE ACTOR'S POSE NOW; EVERYONE ELSE'S AT THE IMPACT INSTANT.
                //
                // The beat records a stance for every combatant it mentions,
                // and all of them used to be applied here, when the beat
                // opened. For the one taking the blow that is the wrong
                // moment: the victim wore its "hurt" drawing through the
                // attacker's whole wind-up, so a figure flinched from a swing
                // that had not left its mark. Invisible while a still-drawing
                // attacker had no wind-up at all (impact WAS the opening
                // frame); four frames of pre-emptive flinching once
                // StaticStancePlayback gave it one, and a full stance's worth
                // for any animated attacker or any spell with a travel time.
                // Cause has to come before effect on the stage as well as in
                // the log, which is the same argument the two snapshots make.
                //
                // The actor is different: its stance IS the wind-up, so it has
                // to be worn from the first frame. PoseVictims below is the
                // other half, called from the impact block.
                if (beat.Actor != null && beat.Stances.TryGetValue(beat.Actor, out var actorStance))
                {
                    SetStance?.Invoke(beat.Actor, actorStance);
                }

                // BEFORE THE LUNGE AND BEFORE THE SPELL, and it yields, so
                // everything below waits for the figure to arrive. That is the
                // whole of the difference between Close and Lunge.
                //
                // THE BRANCH IS OUT HERE, not left to CloseIn's own guard, and
                // that is not tidiness. `yield return someEnumerator` costs a
                // frame even when the enumerator yield-breaks on its first
                // line: Unity resumes the parent on the next update either way.
                // Written as an unconditional yield it therefore delayed the
                // hit flash and the damage popup by a frame on EVERY beat in
                // the game, for a feature three skills use -- caught by two
                // PlayMode tests that sampled exactly one frame after the click,
                // which is the only reason it was caught at all.
                //
                // THOSE TESTS NOW POLL FOR THE IMPACT WITH A DEADLINE instead,
                // because a still-drawing Lunge has since gained a real
                // wind-up (StaticStancePlayback, below) and lands a frame or
                // two later on purpose. The trap above is still a trap; it is
                // only no longer one that a frame-after-the-click sample would
                // catch, so a new unconditional yield here has to be caught by
                // reading, not by the suite.
                // COMPUTED BEFORE THE APPROACH FIRES, because a Charge needs it:
                // its whole point is to arrive on the impact frame, so it has to
                // know how long the wind-up runs before it dispatches the travel.
                var playback = PlaybackOf(beat);

                // WHETHER THIS BEAT'S SWING IS THE STILL-DRAWING KIND, decided
                // ONCE here and read twice below (the lunge's anticipation
                // lead, and the contact effects at impact). Asking PlaybackOf
                // again at the impact instant would resolve the stance a
                // second time and could disagree with the performance actually
                // in flight -- the two cues would then be gated on different
                // answers to the same question.
                bool staticSwing = playback is StaticStancePlayback;

                if (beat.Approach == StageApproach.Close) yield return CloseIn(beat);

                Lunge(beat, staticSwing);
                Charge(beat, playback);
                PlayVfx?.Invoke(beat);

                // A beat's own clip, if it authored one. Unconditional and
                // BEFORE the positioning work, because a spell with a sound but
                // no frames should still be audible -- an empty path is a silent
                // no-op, which is why this needs no guard of its own.
                SoundController.PlayClip(beat.Vfx.sfxPath);

                // Wind-up: the actor's own performance up to and including its
                // impact moment. A spell instead waits out its VFX's impact
                // fraction -- whichever of the two this beat has, only one of
                // them is non-zero, so they add rather than compete.
                yield return playback.Windup();

                float impact = ImpactDelayFor == null ? 0f : ImpactDelayFor(beat);
                if (impact > 0f) yield return new WaitForSeconds(Scaled(impact));

                // The blow lands: the numbers move, the target flashes and the
                // floating figure appears, all on the same frame.
                //
                // GUARDED, because a throw here does not just lose a hit
                // flash. An exception inside a coroutine stops that coroutine
                // dead: the loop never reaches its end, _onFinished never
                // fires, and FightController stays busy for the rest of the
                // fight with every verb disabled. One bad sprite path or one
                // null in a delegate would take the whole fight down, and the
                // only trace is a line in the console.
                //
                // Logged rather than swallowed -- this is the house's graceful
                // degradation, not a silence.
                try
                {
                    // FIRST, before the flash: SetStance is what re-syncs the
                    // hit-flash overlay's silhouette to the drawing under it,
                    // and a flash shaped like the pose the victim just left
                    // is worse than no flash. FlinchFrames further down
                    // resolves each victim's playback off the pose this
                    // applied, which is the ordering PlaybackFor's own header
                    // relies on.
                    PoseVictims(beat);

                    PaintVitals?.Invoke(beat.Snapshot);
                    ShowAmount(beat);
                    FlashTarget?.Invoke(beat);
                    if (staticSwing && WantsContactFx(beat)) PlayContactFx?.Invoke(beat);
                    Recoil(beat);
                    FlinchFrames(beat);
                    Punch(beat);
                    ShakeStage?.Invoke(ShakeStrength(beat));
                    Speak(beat);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }

                // HIT-STOP, and it is the single cheapest thing on this whole
                // screen for making a blow feel like it landed.
                //
                // Everything freezes for a moment at contact -- the attacker
                // mid-swing, the target mid-flinch, the numbers already on
                // screen. The eye reads the pause as the blow meeting
                // resistance, which is the one thing a hand-drawn frame cannot
                // show and a timing change can. It is what Darkest Dungeon and
                // every Vlambeer game do and it is why their static sprites
                // hit harder than most animation.
                //
                // TAKEN OUT OF THE BEAT'S OWN BUDGET, not added to it. The
                // hold below is what gives the player time to read the damage
                // number, and FrameHoldCurve's whole contract is that a stance
                // costs exactly SecondsPerFrame x FrameCount -- a pause that
                // simply appeared here would stretch every beat and
                // desynchronise the round. Subtracted from `remaining`, with
                // SettleAfter's own floor still doing its job underneath.
                float stop = HitStopFor(beat);
                if (stop > 0f) yield return new WaitForSeconds(Scaled(stop));

                // Follow-through, AT THE SAME PACE as the wind-up.
                //
                // The obvious alternative -- stretch the remaining frames to fill
                // the hold -- is what v1 did and it is visibly wrong: the Giant
                // Rat's six-frame swing ran its first three at 0.08s and its last
                // three at 0.15s, one animation changing speed halfway through.
                // That is the "feels a bit blocky" report. Matching the pace makes
                // it one continuous motion and leaves the beat's total length
                // untouched, which matters because the hold is what gives the
                // player time to read the damage number.
                yield return playback.FollowThrough();

                // A ONE-SHOT THAT RETURNS plays back down to the start -- the
                // beetle's Shell Up uncurling out of the sealed ball it curled
                // into. Without this a curl-and-hold stance snapped from its
                // last frame straight to idle, which reads as the shell popping
                // open rather than opening.
                //
                // Real extra time, like CloseIn and unlike the hit-stop: the
                // release is a motion the beat did not previously spend, so it
                // is added rather than taken out of the settle.
                if (playback.ReturnsToStart)
                {
                    yield return playback.Release();
                }

                // WHAT THE STILL-DRAWING WRAPPER DOES TO THIS, in numbers,
                // because "the beat got longer" is the failure mode
                // SettleAfter's own header records going unnoticed once.
                //
                // A flat pose used to report TotalSeconds 0.08 (one frame at
                // the manifest's pace) and actually spend nothing: its
                // Windup() yield-broke immediately. So the beat charged 0.08
                // against the 0.45 budget and then spent only 0.37 of it. The
                // wrapper reports 0.125 (anticipation 0.07 + lunge 0.055) and
                // spends exactly that, so the beat now runs the full 0.45 it
                // was always budgeted -- 0.08 longer in wall time, none of it
                // new budget.
                //
                // The floor is the only thing that can take it further, and
                // only at the very top of the range: at HitStop.MaxSeconds
                // (0.18) the remainder is 0.145, which clamps up to
                // MinSettleSeconds 0.16 and adds 0.015. AFlatPoseKeepsTheBeat-
                // ItAlreadyHad still holds -- it pins SettleAfter's own
                // arithmetic for a 0.08 stance, which is unchanged.
                float remaining = SettleAfter(playback.TotalSeconds + stop);
                yield return new WaitForSeconds(Scaled(remaining));

                // Back to idle before the next beat opens, so a pose belongs to
                // the blow that caused it rather than persisting until something
                // else happens to overwrite it. The defeated stay defeated --
                // the controller decides that from IsAlive, not from here.
                // The fallen fade AFTER the hold, so the defeated pose is seen
                // before it goes. Fading on the frame the blow lands would make
                // a kill read as the figure being deleted rather than dying.
                FadeTheFallen?.Invoke(beat);

                foreach (var pair in beat.Stances) SetStance?.Invoke(pair.Key, FightSession.Stances.Idle);

                // Back to the top of the idle loop, which is no longer the
                // hypothetical this comment used to describe: three actors ship
                // a six-frame idle and FightController.IdleBreathing steps it
                // from here on. PoseCombatant already zeroes the frame on a
                // pose CHANGE, so this is belt and braces for the case where
                // the actor was idle all along.
                if (beat.Actor != null) playback.ResetToRest();

                yield return new WaitForSeconds(Scaled(BeatGapSeconds));
            }

            _running = null;
            IsPlaying = false;

            var finished = _onFinished;
            _onFinished = null;
            finished?.Invoke();
        }

        private void ShowAmount(CombatBeat beat)
        {
            // PHASE D1: a miss shows its OWN popup even though Amount stays
            // 0 -- the exact case FlashCombatant/Recoil/Punch above still
            // correctly skip (nothing landed, so no hit reaction), but the
            // player still needs to be TOLD nothing landed, or a dodge reads
            // as no different from a Hold Back turn or a non-damaging cast.
            // See CombatBeat.Missed and DamagePopup.PlayMiss's own headers.
            if (beat.Target == null) return;
            if (!beat.Missed && beat.Amount <= 0) return;

            var popup = FreePopup();
            if (popup == null) return;   // every one still in flight; the number is dropped, not queued

            // ABOVE THE FIGURE, not on it.
            //
            // This used to spawn at the slot's own centre, which is the middle
            // of the combatant -- so the number rose out from behind the sprite
            // it was describing and spent its first frames, the opaque ones,
            // hidden by it. Starting a head above means the whole punch is
            // visible and the rise carries it clear rather than into view.
            //
            // Measured off the slot rather than a constant: enemy and party
            // slots are not the same height, and a fixed offset would sit on
            // one and float over the other.
            var slot = SlotFor?.Invoke(beat.Target);
            var at = Vector2.zero;

            if (slot != null)
            {
                at = slot.anchoredPosition
                     + new Vector2(0f, slot.rect.height * 0.5f + PopupHeadroom);
            }

            if (beat.Missed)
            {
                popup.PlayMiss(at);
            }
            else
            {
                popup.Play(at, beat.Amount, beat.IsHealing, beat.DamageType);
            }
        }

        private DamagePopup FreePopup()
        {
            if (popups == null) return null;

            foreach (var popup in popups)
            {
                if (popup != null && popup.IsFree) return popup;
            }

            return null;
        }
    
        // The actor steps toward its target and back.
        //
        // Skipped entirely when the beat says the actor HOLDS POSITION -- a cast
        // is delivered from where it stands, and so is a monster whose plain
        // attack art is itself a stationary pose. Without that check the view
        // lunged a ground-slamming golem across the stage while its art showed
        // it rooted, which read as the creature flying.
        //
        // `staticSwing` buys the anticipation leg, and ONLY a still-drawing
        // actor gets one. An animated actor's sheet already draws its own
        // wind-up frames, so a transform crouch in front of them would be two
        // wind-ups played back to back; and the impact instant is only pushed
        // back to match for the wrapped case (StaticStancePlayback.
        // WindupSeconds), so handing a lead to anything else would land the
        // blow before the figure had finished loading.
        private void Lunge(CombatBeat beat, bool staticSwing)
        {
            if (beat.Approach != StageApproach.Lunge) return;

            var (animator, offset) = TravelFor(beat, staticSwing ? StaticLungeFraction : LungeFraction);
            float hold = Scaled(BeatHoldSeconds) * 0.45f;
            float lead = staticSwing ? StageActorAnimator.AnticipationSeconds : 0f;
            animator?.Play(offset, hold, -1f, lead);
            AlsoPlayWorld(beat.Actor, offset, hold, leadSeconds: lead);
        }

        // Whether this blow should draw the house's own contact effects.
        //
        // NOT for a beat that authored a spell: a cast brings its own sheet,
        // its own impact point and its own sound, and adding a slash arc on
        // top of a frost flare is two effects arguing about what just
        // happened. NOT for a heal or a zero-amount beat either -- there is no
        // contact to punctuate, and a miss already says so with its own popup.
        private static bool WantsContactFx(CombatBeat beat)
        {
            if (beat == null || beat.Amount <= 0 || beat.IsHealing) return false;

            return beat.Vfx == null || string.IsNullOrEmpty(beat.Vfx.path);
        }

        // Plays the SAME travel on the world-space mirror of `who`'s slot.
        // Never gated on the uGUI animator existing -- a combatant can be
        // rig-resolved with no uGUI StageActorAnimator doing anything
        // visible, and the two are independent lookups (SlotFor vs
        // WorldAnimatorFor), so this always tries on its own.
        private void AlsoPlayWorld(CombatantState who, Vector2 offset, float holdSeconds, float outSeconds = -1f,
                                   float leadSeconds = 0f)
        {
            WorldAnimatorFor?.Invoke(who)?.Play(offset, holdSeconds, outSeconds, leadSeconds);
        }

        private void AlsoPunchWorld(CombatantState who, float strength)
        {
            WorldAnimatorFor?.Invoke(who)?.Punch(strength);
        }

        // THE COMMITTED RUSH. Like Lunge in order -- the travel runs alongside
        // the swing rather than before it -- but nearly the whole way in, and
        // timed to be at full extent exactly when the blow lands.
        //
        // The two numbers that make it "arrive on the impact frame":
        //
        //   OUT takes the wind-up's own length, so the figure is still crossing
        //   while its frames wind up and plants as the blow connects. The
        //   wind-up is frames [0, impact) at the sheet's uneven pace, so this
        //   is the SUM of their holds rather than impact x secondsPerFrame --
        //   FrameHoldCurve spends that budget front-loaded, and a flat estimate
        //   would arrive early. The impact delay (a spell's VFX lead, zero for
        //   a plain rush) is added because the blow lands after it too.
        //
        //   HOLD keeps the charger planted against its target through the
        //   hit-stop, so the bump is a beat of contact rather than an instant
        //   graze, and the return then plays out over the follow-through.
        //
        // Fire-and-forget like Lunge, NOT a coroutine like CloseIn: it costs
        // the beat no extra time, because it fits inside the wind-up the beat
        // already spends.
        private void Charge(CombatBeat beat, IStancePlayback playback)
        {
            if (beat.Approach != StageApproach.Charge) return;

            var (animator, offset) = TravelFor(beat, ChargeFraction);
            if (animator == null) return;

            // Unscaled, because Play scales the out-tween itself -- the one
            // place a duration handed to Play is expected raw rather than
            // pre-scaled (holdSeconds is the other way round; see PlayRoutine).
            float windup = playback?.WindupSeconds ?? 0f;
            float impactDelay = ImpactDelayFor == null ? 0f : ImpactDelayFor(beat);
            float outSeconds = Mathf.Max(ChargeMinOutSeconds, windup + impactDelay);

            float hold = Scaled(HitStopFor(beat) + ChargeContactSeconds);
            animator.Play(offset, hold, outSeconds);
            AlsoPlayWorld(beat.Actor, offset, hold, outSeconds);
        }

        // THE OTHER APPROACH: get there FIRST, then swing.
        //
        // The difference from a lunge is the order rather than the distance. A
        // lunge is the swing -- the two are one motion, so the figure is still
        // travelling when the blow lands. This arrives before the stance so
        // much as opens, which is what "step in front of it and then bring the
        // thing down" actually looks like, and it is why this one is a
        // coroutine while Lunge is a call.
        //
        // FURTHER IN, TOO. CloseFraction is nearly the whole gap: the point is
        // to be standing over the target, and a figure that closed the same
        // third a lunge does would read as a slow lunge rather than as an
        // approach.
        //
        // THE HOLD IS TAKEN OUT OF NOTHING, unlike the hit-stop. It is real
        // extra time and the beat is genuinely longer for it, because there is
        // no honest way to show a creature crossing the stage inside a budget
        // that assumed it stood still. Authored per skill, so only the blows
        // that want it pay for it.
        private IEnumerator CloseIn(CombatBeat beat)
        {
            if (beat.Approach != StageApproach.Close) yield break;

            var (animator, offset) = TravelFor(beat, CloseFraction);
            if (animator == null) yield break;

            // Held for the whole beat rather than for the tween's own length:
            // Play returns the figure to its mark when the hold expires, and a
            // hold that ended at the top of the stance would walk the creature
            // home again halfway through its own slam.
            float hold = Scaled(BeatHoldSeconds + CloseSeconds);
            animator.Play(offset, hold);
            AlsoPlayWorld(beat.Actor, offset, hold);
            yield return new WaitForSeconds(Scaled(CloseSeconds));
        }

        // Where this beat's actor is travelling to, and the thing that moves
        // it. Shared by both approaches so they cannot disagree about which
        // direction the stage runs in.
        private (StageActorAnimator animator, Vector2 offset) TravelFor(CombatBeat beat, float fraction)
        {
            if (beat?.Actor == null || beat.Target == null) return (null, Vector2.zero);

            var from = SlotFor?.Invoke(beat.Actor);
            var to = SlotFor?.Invoke(beat.Target);
            if (from == null || to == null) return (null, Vector2.zero);

            var animator = from.GetComponent<StageActorAnimator>();
            if (animator == null) return (null, Vector2.zero);

            // A fraction of the way, not all of it: the figures are meant to
            // close the gap, not swap places.
            float dx = (to.anchoredPosition.x - from.anchoredPosition.x) * fraction;
            return (animator, new Vector2(dx, 0f));
        }

        // Every stance the beat recorded for someone OTHER than its actor,
        // applied on the frame the blow lands. See the note at the top of
        // PlayBeats for why these do not go on with the actor's.
        private void PoseVictims(CombatBeat beat)
        {
            foreach (var pair in beat.Stances)
            {
                if (ReferenceEquals(pair.Key, beat.Actor)) continue;
                SetStance?.Invoke(pair.Key, pair.Value);
            }
        }

        // The other half of a blow: the figure taking it flinches AWAY.
        //
        // Away is decided by which side it is on rather than by where the blow
        // came from -- the party faces right and the monsters face left, so a
        // recoil is always backwards into its own half. Deriving it from the
        // attacker's position instead would send a back-row monster stumbling
        // TOWARD the party when it was hit from behind by a status tick.
        private void Recoil(CombatBeat beat)
        {
            if (beat.Target == null || beat.Amount <= 0) return;

            // Nobody flinches away from themselves. A self-heal still gets its
            // number, just no recoil.
            if (ReferenceEquals(beat.Target, beat.Actor)) return;

            var slot = SlotFor?.Invoke(beat.Target);
            var animator = slot == null ? null : slot.GetComponent<StageActorAnimator>();
            if (animator == null) return;

            float dx = beat.Target.IsPlayerSide ? -RecoilDistance : RecoilDistance;
            var offset = new Vector2(dx, 0f);
            float hold = Scaled(BeatHoldSeconds) * 0.45f;
            animator.Play(offset, hold);
            AlsoPlayWorld(beat.Target, offset, hold);
        }

        private const float LungeFraction = 0.35f;

        // HOW FAR A STILL-DRAWING SWING TRAVELS. Twice the lean-in above, and
        // the difference is what the pilot capture showed: at 0.35 the witch
        // stopped 210px into a ~600px gap, her staff swinging at air a figure
        // and a half short of Shawn, while his flash and number said he had
        // been hit. An animated actor's sheet draws its weapon reaching out,
        // so a lean is enough to sell the contact; a single drawing's weapon
        // reaches exactly as far as the figure is carried, so the figure has
        // to be carried to where the weapon lands. Short of Close's 0.78 so
        // the depth-scaled front figure does not swallow the one it hits.
        private const float StaticLungeFraction = 0.70f;

        private const float RecoilDistance = 45f;

        // Nearly all the way. Not all of it -- two figures sharing a mark
        // overlap, and the depth scaling means the nearer one would simply
        // swallow the other.
        private const float CloseFraction = 0.78f;

        // How long the walk in takes. Long enough to read as a decision and
        // short enough that a fight full of them does not become a parade.
        private const float CloseSeconds = 0.26f;

        // Further than a Close, because a charger is meant to END against what
        // it hit rather than a step short of it, and the recoil shoves the
        // target back on contact so the two never actually overlap. Not the
        // full gap for the same reason Close is not: the nearer, larger figure
        // would otherwise swallow the one it slammed.
        private const float ChargeFraction = 0.86f;

        // A floor under the rush's travel time, for the degenerate case of a
        // charge with flat art (no wind-up to fill): without it the out-tween
        // would be near-zero and the figure would teleport into the target
        // rather than cross to it. A real charge overrides this with its own
        // wind-up length, which is longer.
        private const float ChargeMinOutSeconds = 0.18f;

        // How long past the hit-stop the charger stays planted against its
        // target before rolling back -- the difference between a bump that
        // lands and one that only grazes. Added to HitStopFor, so a heavier
        // blow already dwells longer and this is the shared minimum on top.
        private const float ChargeContactSeconds = 0.06f;

        // How this beat's actor plays its pose. Falls back to a no-op
        // performance when there is no controller wired at all, or no actor
        // -- the bare fixture FightBeatPacingTests.NewPlayer() exercises,
        // and the same graceful posture AnimationOf always took for a
        // combatant with no kit, no art or no timing.
        private IStancePlayback PlaybackOf(CombatBeat beat)
        {
            if (PlaybackFor == null || beat?.Actor == null) return EmptyStancePlayback.Instance;

            string stance = beat.Stances.TryGetValue(beat.Actor, out var pose)
                ? pose
                : FightSession.Stances.Idle;

            var playback = PlaybackFor(beat.Actor, stance, null) ?? EmptyStancePlayback.Instance;

            // KEYED ON THE CLASS OF BEAT, never on who is swinging. The three
            // conditions are the whole definition of "a still-drawing melee
            // swing", and every one of them is load-bearing:
            //
            //   LUNGE, because Hold is a cast delivered from where it stands
            //   (nothing crosses, so there is nothing to anticipate), Close
            //   already arrives before the stance opens, and Charge times its
            //   own travel against WindupSeconds -- wrapping it would feed
            //   Charge a wind-up that includes the travel it is trying to fit
            //   inside, and the rush would arrive late by its own length.
            //
            //   NO MOTION, because that is what "one drawing" means here. A
            //   multi-frame or rig actor's stance already spends real time on
            //   its own wind-up and must stay pixel-identical.
            //
            //   A TARGET THAT IS SOMEBODY ELSE, because there is no crossing
            //   to a self-targeted beat, and TravelFor would return no
            //   animator for it anyway -- so the wrapper would push the impact
            //   back by a travel that never happens.
            if (beat.Approach != StageApproach.Lunge) return playback;
            if (playback.HasMotion) return playback;
            if (beat.Target == null || ReferenceEquals(beat.Target, beat.Actor)) return playback;

            return new StaticStancePlayback(playback);
        }

        // The squash a struck figure takes, scaled by how hard it was hit.
        //
        // Beside Recoil rather than inside it because they are two different
        // statements about one blow -- Recoil says the body was MOVED, this
        // says it was COMPRESSED -- and only one of them should be skipped for
        // a self-targeted heal.
        private void Punch(CombatBeat beat)
        {
            if (beat?.Target == null) return;
            if (ReferenceEquals(beat.Target, beat.Actor)) return;

            float strength = Strength(beat);
            if (strength <= 0f) return;

            var slot = SlotFor?.Invoke(beat.Target);
            var animator = slot == null ? null : slot.GetComponent<StageActorAnimator>();

            // A FLOOR UNDER THE STRENGTH, unlike the shake. A shake that is
            // barely there is honest about a small hit; a squash that is
            // barely there just looks like the sprite is vibrating, so a hit
            // either deforms the target properly or leaves it alone.
            float squash = Mathf.Max(0.55f, strength);
            animator?.Punch(squash);
            AlsoPunchWorld(beat.Target, squash);
        }

        // EVERY POSED VICTIM'S OWN FRAMES, which nothing stepped until now.
        //
        // StepActorFrames drives the beat's ACTOR, and that was the whole of
        // what this class animated: anything else the beat posed was put into
        // its stance and then held frame 0 of it. Invisible for every kit with
        // flat single-frame art, which was all of them until the Beetle and
        // the Treant arrived with six.
        //
        // OVER beat.Stances, NOT beat.Target. The first cut of this fix read
        // the named target only, which is right for a plain swing and wrong
        // for every beat that hurts more than one thing -- an AoE poses each
        // enemy it lands on (FightSession.Skills), Lucky Deck's splash poses
        // each neighbour, a kill-splash poses the bystander. Those all flinched
        // exactly one monster and left the rest frozen, which is the same bug
        // this method exists to fix, one target along. beat.Stances is already
        // the complete set and is applied wholesale at the top of the beat; the
        // rule was never "the target animates", it was "everything posed
        // animates, except the two things somebody else is driving".
        //
        // AT IMPACT, not when the beat's stances are applied. The poses all go
        // on at the top of the beat, before the wind-up, so animating from
        // there would play the flinch before the blow that causes it -- which
        // is the exact cause-before-effect complaint PaintVitals' own comment
        // above records being fixed once already.
        //
        // SEPARATE COROUTINES rather than yields, because the reactions have to
        // run alongside the attacker's follow-through rather than pausing it.
        // The beat owns its own length; these are decoration inside it.
        private void FlinchFrames(CombatBeat beat)
        {
            if (beat == null || PlaybackFor == null) return;

            foreach (var posed in beat.Stances)
            {
                var victim = posed.Key;

                // The actor's own performance is driven by playback above --
                // and nobody flinches away from themselves, which is the rule
                // Recoil also states.
                if (victim == null || ReferenceEquals(victim, beat.Actor)) continue;

                // A body is the CONTROLLER's to animate: FightController's
                // FadeTheFallen owns the death, times it against the fade, and
                // outlives this beat. Two things stepping one corpse would
                // fight over the frame index.
                if (posed.Value == FightSession.Stances.Defeated) continue;

                Flinch(victim, PlaybackFor(victim, posed.Value, () => _flushed));
            }
        }

        // ONE HANDLE PER VICTIM, stopped before it is replaced.
        //
        // Two beats can land on the same target faster than its flinch plays --
        // a six-frame hurt against a shorter attack is enough -- and two
        // steppers writing one combatant's frame index fight over it and repaint
        // the stage twice as often for the privilege. The same rule
        // StageActorAnimator.Play states for movement, for the same reason.
        private readonly Dictionary<CombatantState, Coroutine> _flinching =
            new Dictionary<CombatantState, Coroutine>();

        private void Flinch(CombatantState victim, IStancePlayback playback)
        {
            if (playback == null || !playback.HasMotion) return;

            if (_flinching.TryGetValue(victim, out var running) && running != null)
            {
                StopCoroutine(running);
            }

            // ABANDONED IF THE FIGHT MOVES ON, which the corpse stepper has
            // had since it was written and this did not. A reaction outlives
            // nothing -- but Flush() stops only the main playback coroutine, so
            // a fight abandoned mid-beat left these writing frames into the
            // next encounter's slots. The abandon check lives INSIDE playback
            // now (threaded through PlaybackFor's third argument above), polled
            // every frame the same way it always was; the check between the two
            // phases here is belt to that same braces.
            _flinching[victim] = StartCoroutine(FlinchRoutine(playback));
        }

        private IEnumerator FlinchRoutine(IStancePlayback playback)
        {
            yield return playback.Windup();
            if (_flushed) yield break;
            yield return playback.FollowThrough();
        }

        // What the two of them say about the blow.
        //
        // The LINE was chosen when the beat was recorded, because it depends on
        // health at the moment the blow landed and live health has already moved
        // on by now. All that is left here is playing it.
        private static void Speak(CombatBeat beat)
        {
            if (beat.HasActorVoice)
            {
                CharacterVoice.Play(beat.ActorVoiceId, beat.ActorVoiceLine, oncePerFight: false);
            }

            if (beat.HasTargetVoice)
            {
                // "Nearly dead" marks a threshold rather than an event, so it is
                // said once per fight. A grunt is an event and repeats.
                CharacterVoice.Play(beat.TargetVoiceId, beat.TargetVoiceLine, beat.TargetVoiceOncePerFight);
            }
        }
}
}
