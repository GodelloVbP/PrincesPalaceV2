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
        // stance had been paid for, and back when a stance was six drawings at
        // 0.08s it cost 0.48s against a 0.45s budget -- so `remaining > 0f`
        // was never true and an animated blow got NO pause at all. Blows ran
        // into each other with nothing between them and the damage number had
        // no still frame to be read against.
        //
        // The sheets are gone and the floor stays, because the arithmetic that
        // produced that bug has not: a heavy blow's hit-stop is taken out of
        // this same remainder (see `remaining` below), so a big enough hit can
        // still spend the whole budget and leave nothing to read the number
        // against. A floor rather than a bigger budget, which would make every
        // light beat sit longer doing nothing.
        public const float MinSettleSeconds = 0.16f;

        // WHAT A STILL POSE COSTS AGAINST THE BEAT BUDGET, for every beat that
        // is not a lunge -- a cast delivered from the spot, an approach, a
        // status tick.
        //
        // A drawing takes no time to show, so honestly this is zero. It is
        // 0.08 because that is what the beat has always charged (one frame at
        // the frame sheets' pace, back when a flat pose resolved to a
        // one-frame animation), and the settle after it is 0.37s of a 0.45s
        // beat. Zeroing it would silently lengthen every non-lunge beat in the
        // game by 80ms; the pacing is the product, so the number stays and
        // says what it is instead.
        public const float StillPoseSeconds = 0.08f;

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
        // Deliberately narrow -- one delegate in, nothing readable back out,
        // the same posture FightController's own *ForTest seams take.
        public void WireContactFxForTest(Action<CombatBeat> playContactFx)
        {
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
                // instant); a whole crouch-and-cross of pre-emptive flinching
                // once StaticSwing gave it one, and longer still for any spell
                // with a travel time. Cause has to come before effect on the
                // stage as well as in the log, which is the same argument the
                // two snapshots make.
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
                // because a Lunge has since gained a real wind-up (StaticSwing)
                // and lands a frame or two later on purpose. The trap above is
                // still a trap; it is only no longer one that a
                // frame-after-the-click sample would catch, so a new
                // unconditional yield here has to be caught by reading, not by
                // the suite.

                // WHETHER THIS BEAT IS A SWING THAT CROSSES THE STAGE, decided
                // ONCE here and read three times below -- the lunge's
                // anticipation lead, the wind-up it waits out, and the contact
                // effects at impact. All three have to agree, and asking again
                // at the impact instant is how they would come to disagree.
                bool staticSwing = IsStaticSwing(beat);

                // THE CHARGE TWIN OF staticSwing -- see IsStaticCharge. A
                // Charge never gets a lunge-style anticipation lead (it is not
                // a lean, it is a committed rush that is already crossing
                // during the swing), so this is read for the wind-up and the
                // contact effects only, never for Lunge's third use above.
                bool staticCharge = IsStaticCharge(beat);

                if (beat.Approach == StageApproach.Close) yield return CloseIn(beat);

                Lunge(beat, staticSwing);

                // THE CHARGE'S OWN OUTBOUND TRAVEL TIME, computed once and
                // read on both sides of AUDIT.md #59: Charge below hands it to
                // the animator as the out-tween's duration, and the wind-up a
                // few lines down waits out the SAME number before the impact
                // instant fires. One value rather than each side deriving its
                // own is what keeps the charger's arrival and the target's
                // flinch from drifting apart again.
                float chargeOutSeconds = Charge(beat);

                PlayVfx?.Invoke(beat);

                // A beat's own clip, if it authored one. Unconditional and
                // BEFORE the positioning work, because a spell with a sound but
                // no frames should still be audible -- an empty path is a silent
                // no-op, which is why this needs no guard of its own.
                SoundController.PlayClip(beat.Vfx.sfxPath);

                // Wind-up: the crouch and the cross for a Lunge, or the
                // charge's own outbound travel (chargeOutSeconds, floored at
                // ChargeMinOutSeconds) for a Charge -- up to the moment the
                // blow would connect either way. A spell instead waits out its
                // VFX's impact fraction below; only one of these three is ever
                // non-zero for a given beat, so they add rather than compete.
                if (staticSwing) yield return StaticSwing.Windup();
                else if (staticCharge) yield return StaticSwing.Windup(chargeOutSeconds);

                // SKIPPED FOR A STATIC CHARGE: chargeOutSeconds already IS
                // Max(ChargeMinOutSeconds, impact) -- see ChargeOutSeconds --
                // so the wind-up just waited out at least this much. Waiting
                // it again here would either do nothing (the common case, no
                // spell) or double an authored spell's own impact delay.
                float impact = ImpactDelayFor == null ? 0f : ImpactDelayFor(beat);
                if (impact > 0f && !staticCharge) yield return new WaitForSeconds(Scaled(impact));

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
                    // and a flash shaped like the pose the victim just left is
                    // worse than no flash.
                    PoseVictims(beat);

                    PaintVitals?.Invoke(beat.Snapshot);
                    ShowAmount(beat);
                    FlashTarget?.Invoke(beat);
                    if ((staticSwing || staticCharge) && WantsContactFx(beat)) PlayContactFx?.Invoke(beat);
                    Recoil(beat);
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
                // number, and a pause that simply appeared here would stretch
                // every beat and desynchronise the round. Subtracted from
                // `remaining`, with SettleAfter's own floor still doing its
                // job underneath.
                float stop = HitStopFor(beat);
                if (stop > 0f) yield return new WaitForSeconds(Scaled(stop));

                // WHAT THE BEAT ACTUALLY SPENT, which is the number the settle
                // has to be sized against -- "the beat got longer" is the
                // failure mode SettleAfter's own header records going
                // unnoticed once.
                //
                // A swing spends StaticSwing.WindupSeconds and a charge spends
                // chargeOutSeconds -- both report exactly what their own
                // wind-up wait just spent, or SettleAfter would hand back a
                // settle sized for a shorter beat than the one that actually
                // played, and the whole beat would run long (the "no pause"
                // bug SettleAfter's own header records). Everything else
                // spends nothing and is charged StillPoseSeconds, which is
                // what a flat pose has always cost. The floor is the only
                // thing that can take a beat further, and only at the top of
                // the range: at HitStop.MaxSeconds a swing's or a charge's
                // remainder clamps up to MinSettleSeconds.
                float spent = staticSwing ? StaticSwing.WindupSeconds
                    : staticCharge ? chargeOutSeconds
                    : StillPoseSeconds;
                yield return new WaitForSeconds(Scaled(SettleAfter(spent + stop)));

                // Back to idle before the next beat opens, so a pose belongs to
                // the blow that caused it rather than persisting until something
                // else happens to overwrite it. The defeated stay defeated --
                // the controller decides that from IsAlive, not from here.
                // The fallen fade AFTER the hold, so the defeated pose is seen
                // before it goes. Fading on the frame the blow lands would make
                // a kill read as the figure being deleted rather than dying.
                FadeTheFallen?.Invoke(beat);

                foreach (var pair in beat.Stances) SetStance?.Invoke(pair.Key, FightSession.Stances.Idle);

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
        // `staticSwing` buys the anticipation leg, and only a beat that
        // actually crosses the stage gets one -- see IsStaticSwing. The impact
        // instant is pushed back by exactly the same number
        // (StaticSwing.WindupSeconds), so handing a lead to anything else
        // would land the blow before the figure had finished loading.
        private void Lunge(CombatBeat beat, bool staticSwing)
        {
            if (beat.Approach != StageApproach.Lunge) return;

            var (animator, offset) = TravelFor(beat, staticSwing ? StaticLungeFraction : LungeFraction);
            float hold = Scaled(BeatHoldSeconds) * 0.45f;
            float lead = staticSwing ? StageActorAnimator.AnticipationSeconds : 0f;
            animator?.Play(offset, hold, -1f, lead);
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

        // THE COMMITTED RUSH. Like Lunge in order -- the travel runs alongside
        // the swing rather than before it -- but nearly the whole way in, and
        // timed to be at full extent exactly when the blow lands.
        //
        // The two numbers that make it "arrive on the impact frame":
        //
        //   OUT (ChargeOutSeconds, below) takes as long as the beat has before
        //   the blow lands, so the figure is still crossing while the wind-up
        //   runs and plants as the blow connects. Returned rather than only
        //   consumed here, because PlayBeats' own wind-up wait -- the fix for
        //   AUDIT.md #59 -- has to wait out this EXACT number too, or the
        //   impact instant fires before the charger arrives again.
        //
        //   HOLD keeps the charger planted against its target through the
        //   hit-stop, so the bump is a beat of contact rather than an instant
        //   graze, and the return then plays out over the follow-through.
        //
        // Fire-and-forget like Lunge, NOT a coroutine like CloseIn: it costs
        // the beat no extra time on its own, because it fits inside the
        // wind-up the beat spends waiting on the same value.
        //
        // Returns 0 for a beat that is not a Charge, or a Charge with nobody
        // to travel to -- the caller has nothing to wait on either way.
        private float Charge(CombatBeat beat)
        {
            if (beat.Approach != StageApproach.Charge) return 0f;

            // ONE HOME for the out-tween's length: computed here so Play
            // below and PlayBeats' wind-up wait can never disagree about it.
            float outSeconds = ChargeOutSeconds(beat);

            var (animator, offset) = TravelFor(beat, ChargeFraction);
            if (animator != null)
            {
                // Unscaled, because Play scales the out-tween itself -- the
                // one place a duration handed to Play is expected raw rather
                // than pre-scaled (holdSeconds is the other way round; see
                // PlayRoutine).
                float hold = Scaled(HitStopFor(beat) + ChargeContactSeconds);
                animator.Play(offset, hold, outSeconds);
            }

            return outSeconds;
        }

        // THE FLOOR-OR-LONGER TRAVEL TIME a Charge commits to. A spell's own
        // VFX lead if it authored one and that lead is the longer of the two
        // (ImpactDelayFor); otherwise ChargeMinOutSeconds, because without a
        // floor the out-tween would be near-zero for a plain rush and the
        // figure would teleport into the target rather than cross to it.
        private float ChargeOutSeconds(CombatBeat beat)
        {
            float impactDelay = ImpactDelayFor == null ? 0f : ImpactDelayFor(beat);
            return Mathf.Max(ChargeMinOutSeconds, impactDelay);
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

        // A floor under the rush's travel time. A charge that authors a spell
        // overrides it with that spell's own impact lead, which is longer; a
        // plain rush has nothing to fill and would otherwise teleport.
        private const float ChargeMinOutSeconds = 0.18f;

        // How long past the hit-stop the charger stays planted against its
        // target before rolling back -- the difference between a bump that
        // lands and one that only grazes. Added to HitStopFor, so a heavier
        // blow already dwells longer and this is the shared minimum on top.
        private const float ChargeContactSeconds = 0.06f;

        // WHETHER THIS BEAT IS A LUNGE THAT HAS TO CROSS THE STAGE, which is
        // the whole of what "give it a crouch, then a wind-up, then the house's
        // contact effects" is keyed on. KEYED ON THE CLASS OF BEAT, never on
        // who is swinging -- every actor in the game wears a single drawing per
        // stance, so there is no art to ask. Each condition is load-bearing:
        //
        //   LUNGE, because Hold is a cast delivered from where it stands
        //   (nothing crosses, so there is nothing to anticipate), Close already
        //   arrives before the stance opens, and Charge is its own case
        //   (IsStaticCharge, below) -- it does not buy Lunge's separate
        //   anticipation lead, because the rush IS the travel rather than a
        //   lean in front of it.
        //
        //   TARGETS SOMEBODY ELSE (CrossesToATarget) -- see that helper.
        private static bool IsStaticSwing(CombatBeat beat)
        {
            return beat != null && beat.Approach == StageApproach.Lunge && CrossesToATarget(beat);
        }

        // THE CHARGE TWIN OF IsStaticSwing. A Charge always crosses -- that is
        // the whole point of the approach -- so this exists only to gate the
        // beats a rush cannot happen for: a self-targeted beat (nothing to
        // travel to) or one missing an actor or target entirely.
        //
        // Once this fix (AUDIT.md #59) went in, a Charge's wind-up stopped
        // being "none" and became ChargeOutSeconds -- the SAME value Charge()
        // hands the animator, read once by both. Wrapping it in IsStaticSwing
        // instead was rejected on purpose: that predicate also gates Lunge's
        // separate anticipation lead, and a Charge must never get one (the
        // rush IS the travel, not a lean before it).
        private static bool IsStaticCharge(CombatBeat beat)
        {
            return beat != null && beat.Approach == StageApproach.Charge && CrossesToATarget(beat);
        }

        // Shared by both: there is no crossing to a self-targeted beat, and
        // TravelFor would return no animator for one missing an actor or
        // target anyway -- so the impact would be pushed back by a wind-up
        // that waits on a travel that never happens.
        private static bool CrossesToATarget(CombatBeat beat)
        {
            return beat.Actor != null && beat.Target != null && !ReferenceEquals(beat.Target, beat.Actor);
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
