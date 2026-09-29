using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

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

        // A round's own beat (BeatCause.RoundStart): one beat's hold plus its
        // gap, so a toll reads as a beat of the fight's rhythm rather than a
        // flicker between two blows. Scaled by the battle speed like the rest.
        public const float RoundStartHoldSeconds = BeatHoldSeconds + BeatGapSeconds;

        // How long a pull's chains hold their target before he is dragged
        // (CombatBeat.PreFormation): the bind's own snap, Dark Chains' 0.3s.
        public const float PullHoldSeconds = 0.3f;

        // THE SETTLE AFTER THE BLOW, AND IT IS A FLOOR RATHER THAN A REMAINDER.
        //
        // A pure "what's left of BeatHoldSeconds once the stance is paid for"
        // remainder can hit zero or go negative once a heavy blow's hit-stop
        // is also taken out of it (see `remaining` below), leaving an
        // animated blow no pause at all: blows would run into each other
        // with nothing between them and the damage number no still frame to
        // be read against. A floor rather than a bigger budget, which would
        // make every light beat sit longer doing nothing.
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

        // AND THE FLOOR A WORN FORM PUTS UNDER IT, the twin of ShakeStrength's
        // above. Read here rather than at the two call sites so the wind-up a
        // Charge waits out (Charge's own contact hold) and the freeze the
        // impact instant takes cannot disagree about how long the blow stops
        // for. Already clamped to HitStop.MaxSeconds at record time.
        public static float HitStopFor(CombatBeat beat)
        {
            if (beat == null) return 0f;

            float own = HitStop.SecondsFor(Weight(beat));
            return beat.FormHitStopSeconds > own ? beat.FormHitStopSeconds : own;
        }

        // The test seam. A PlayMode test that had to wait real seconds per beat
        // would take longer than the whole EditMode suite; this lets one run a
        // twelve-beat round in well under a second without changing a single
        // rule about ordering.
        public static float BeatSpeedMultiplier = 1f;

        // THE PLAYER-FACING HALF OF THE PRODUCT. Never written directly -- AdoptPlayerSpeed, below,
        // is the only writer, so nothing outside it can leave this
        // disagreeing with what PlayerSpeedSource would currently say.
        public static float PlayerSpeedMultiplier { get; private set; } = 1f;

        // The seam FightBootstrap installs the settings-backed reader
        // behind, before fight.Bind. Static default () => 1f, so a fixture
        // that never touches battle speed at all -- most of them -- adopts
        // exactly the constant this project always ran at. A fixture that
        // DOES touch it (or anything downstream of FightBootstrap.Start
        // installing the production source) must re-pin this explicitly
        // rather than trust the default to still be what it started as: the
        // shared PlayMode fight-loading helper and TestGlobals.ResetAll both
        // do, because this is a static surviving across the whole PlayMode
        // process, not per-fixture state.
        public static Func<float> PlayerSpeedSource = () => 1f;

        // Contract 1 and 2: the source is read in exactly this one place, so
        // PlayerSpeedMultiplier can never disagree with what it would
        // currently say between one adoption and the next.
        //
        // PUBLIC: called by FightBootstrap.Start, immediately after it
        // installs a new PlayerSpeedSource (see that call site's own
        // comment), by the per-beat loop below, and by any test that changes
        // PlayerSpeedSource mid-test and needs the change to stick in
        // PlayerSpeedMultiplier without waiting for the next beat (T5's
        // "changing the delegate alone changes nothing" case, revision 3
        // point 2) -- PlayMode has no InternalsVisibleTo grant, so a test
        // caller needs this to be public, not a wrapper method that forwards
        // to an internal one.
        public static void AdoptPlayerSpeed()
        {
            PlayerSpeedMultiplier = PlayerSpeedSource != null ? PlayerSpeedSource() : 1f;
        }

        // THE PRODUCT, read in exactly one place -- Scaled/Unscaled below,
        // and SpellPerformancePlayer.Begin's own capture of a cast's
        // PaceAtStart (contract 3). A third reader computing
        // BeatSpeedMultiplier * PlayerSpeedMultiplier itself at its own call
        // site would be a second home for the product, free to disagree
        // with this one the day either factor changes on its own.
        public static float Pace => BeatSpeedMultiplier * PlayerSpeedMultiplier;

        // Contract 8: a product at or below zero collapses to zero rather
        // than a division blowing up into infinity or NaN -- today's
        // BeatSpeedMultiplier-only behaviour, unchanged by PlayerSpeedMultiplier
        // joining the product it is checked against.
        public static float Scaled(float seconds) =>
            Pace <= 0f ? 0f : seconds / Pace;

        public bool IsPlaying { get; private set; }

        // A read-only view of the pool, for the tests that assert it comes back
        // whole. Read-only rather than exposing the field: nothing outside this
        // class has any business swapping the array, and a leak test that could
        // replace the pool would not be testing the leak.
        public IReadOnlyList<DamagePopup> Popups => popups ?? System.Array.Empty<DamagePopup>();

        private Coroutine _running;
        private Action _onFinished;

        // Which playback is current. Bumped by every Supersede, so a stopped
        // playback's finally can tell it has been replaced -- see PlayBeats.
        private int _playback;

        // The nested steps of the current playback, each a coroutine of its
        // own (StartStep), kept so Supersede can stop them with their parent.
        private readonly List<Coroutine> _steps = new List<Coroutine>();

        // Set by the controller so playback can find where a combatant is
        // standing without knowing anything about stages or slots.
        internal Func<CombatantState, RectTransform> SlotFor;

        // SlotFor's twin for the animator that lives on that slot. The view
        // already knows which array holds it (ScreenRegistry populated it at
        // build time), so this hands it over the same door SlotFor uses
        // rather than making playback derive it through the transform via
        // SlotFor + GetComponent.
        internal Func<CombatantState, StageActorAnimator> AnimatorFor;

        // Paints a set of vitals. Called TWICE per beat -- once with what stood
        // before the blow, once with what stood after.
        internal Action<IReadOnlyDictionary<CombatantState, Vitals>> PaintVitals;

        // PaintVitals' twin for POSITION: which formation the stage should be
        // drawing. Called once at the top of each beat, and with null when
        // playback ends -- after which live state is the moment being shown.
        //
        // A separate delegate rather than reading beat.Formation off a beat
        // the view was handed, for the same reason PaintVitals is one: the
        // view is told what to paint and never given the beat to interpret.
        internal Action<BeatFormation> PaintFormation;

        // AND THE SAME FOR THE TURN QUEUE: which upcoming-turns list the
        // initiative tracker should be drawing. Called at the top of each beat
        // with what that beat recorded, and with null when playback ends.
        //
        // The third thing that has to be told the moment rather than allowed to
        // read live state, and the last one still doing so: the chain resolves
        // in one pass, so the row showed the order the ROUND finished on for
        // every beat of it. Same shape as PaintFormation beside it -- payload,
        // not the beat -- and driven from the same call sites, so there is one
        // notion of "the beat being shown" rather than two that can drift.
        internal Action<IReadOnlyList<CombatantState>> PaintTurnOrder;

        // Whether the stage is still walking figures to the marks that
        // formation implies. Playback waits it out before going on -- see the
        // call site.
        internal Func<bool> FormationIsMoving;
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

        // SetStance's twin one level up: which stance FOLDER a combatant is
        // drawn from, for the length of a transformation. Playback owns the
        // moment (the impact instant of the beat that recorded the change);
        // the view owns what changing form looks like -- the re-layout against
        // the new actor's manifest entry, and the silhouette flash that covers
        // the swap. Exactly the split ShakeStage already draws.
        internal Action<CombatantState, string> WearForm;

        // AND BACK TO LIVE STATE, called where PaintFormation(null) and
        // PaintTurnOrder(null) are and for the same reason: every beat has
        // been shown, so live Transformation state is now the moment on
        // screen. This is also the whole of how a transform's REVERT reaches
        // the stage -- it expires at its holder's turn start, which opens no
        // beat, so there is nothing to record it on.
        internal Action ResyncForms;

        // How hard to kick the stage, 0..1. The view owns which transforms
        // that means -- see FightController.ShakeStage -- and playback owns
        // when and how hard, because only it knows where the impact is.
        internal Action<float> ShakeStage;

        internal Action<CombatBeat> FlashTarget;

        // A ROUND STARTED AT THIS BEAT (CombatBeat.RoundStarted): the view
        // steps its round counter, sounds the toll and steps the overlay. The
        // view owns what a round looks like; playback owns when -- the moment
        // the beat that carries the round opens.
        internal Action<int> PresentRound;

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

        // THE CONTACT EFFECT A WORN FORM ADDS, on top of whatever the beat
        // already drew.
        //
        // A THIRD DOOR RATHER THAN A BRANCH INSIDE EITHER OF THE OTHER TWO.
        // PlayVfx plays what the SKILL authored, at the top of the beat, on
        // the skill's own clock. PlayContactFx is the HOUSE's default for a
        // swing that authored nothing, and is gated on the beat authoring no
        // spell (WantsContactFx). This is neither: it belongs to the FORM the
        // actor is wearing, it fires whatever the skill authored, and it never
        // replaces anything -- a headbutt that already draws something draws
        // both. Folding it into PlayContactFx would mean loosening that gate
        // for one case and re-tightening it for the other inside a method
        // whose whole job is the case it excludes.
        //
        // Fired at the impact instant, beside the house's, because it is the
        // same kind of statement about the same moment.
        internal Action<CombatBeat> PlayFormHitFx;

        // THE WIRING SEAMS FOR A PLAYMODE TEST, and the only public doors onto
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

        // THE SECOND DOOR, for SlotFor/AnimatorFor rather than PlayContactFx.
        //
        // Without this, proving that Lunge/RecoilOne/Punch reach a real
        // StageActorAnimator through AnimatorFor -- rather than through a
        // GetComponent this change removed -- would still mean loading the
        // whole Fight scene, because a real FightController is otherwise the
        // only thing that ever assigns these two fields. A fixture of a few
        // bare RectTransform/StageActorAnimator GameObjects can hand in its
        // own pair instead, with no scene at all.
        public void WireStageForTest(Func<CombatantState, RectTransform> slotFor,
                                     Func<CombatantState, StageActorAnimator> animatorFor)
        {
            SlotFor = slotFor;
            AnimatorFor = animatorFor;
        }

        // THE THIRD DOOR, for the poses themselves and for what a figure is
        // drawn AS.
        //
        // Same justification as WireContactFxForTest's: WHICH DRAWING GOES ON
        // WHEN is a decision rather than a picture. A beat can now carry three
        // actor poses and a form change, and the whole of what is worth
        // pinning about them is the ORDER -- approach, wind-up, strike, and
        // the form landing on the impact instant rather than at the open,
        // which is the difference between a flash that hides a transformation
        // and a transformation that has already happened by the time anything
        // flashes.
        //
        // Reproducing that order through a real fight would mean authoring an
        // actor with three extra stances on disk, and the art it would assert
        // against is exactly the art that has not been delivered when the rule
        // is being written. Deliberately narrow: three delegates in, nothing
        // readable back out.
        public void WireStancesForTest(Action<CombatantState, string> setStance,
                                       Action<CombatantState, string> wearForm,
                                       Action resyncForms)
        {
            SetStance = setStance;
            WearForm = wearForm;
            ResyncForms = resyncForms;
        }

        // THE FIFTH DOOR: the log feed and the spell's impact lead, so a
        // PlayMode test can pin WHEN a beat's sentence reaches the log against
        // the beat clock without loading a fight. Same narrow posture as the
        // doors above: two delegates in, nothing readable back out.
        public void WireLogForTest(Action<string> pushLine, Func<CombatBeat, float> impactDelayFor)
        {
            PushLine = pushLine;
            ImpactDelayFor = impactDelayFor;
        }

        public void Play(IReadOnlyList<CombatBeat> beats, Action onFinished)
        {
            // CLEARED BEFORE THE FLUSH, so the flush below has nobody to notify.
            //
            // This playback supersedes the last one, and the caller is about to
            // be told when THIS one ends. Letting Flush fire the previous
            // callback here would clear the controller's busy flag a frame
            // after it set it, and the player could act in the middle of the
            // round they just started.
            _onFinished = null;
            Supersede();

            _onFinished = onFinished;
            IsPlaying = true;
            _running = StartCoroutine(PlayBeats(beats, _playback));
        }

        // SUPERSEDE, NOT FLUSH -- and this is the same distinction one level
        // down from the one EndFight already draws.
        //
        // Flush hands the stage back to LIVE state, which is right for a
        // playback that is being abandoned with nothing to follow it. It is
        // exactly wrong here. A round resolves in ONE synchronous pass before
        // a single beat plays, so by the time Play is called live state is the
        // END of the round about to be drawn -- and handing the stage to it
        // paints the round's outcome on the frame before its first beat opens.
        //
        // The formation and the turn queue get away with reading live state
        // early: PlayBeats' very first act is to paint both from beat 1, on
        // the same frame, so an early read is overwritten before anything
        // renders. Forms do not, because a form is only repainted at a
        // transform's impact instant -- ResyncForms reading live state here
        // would put an actor in a transformed form from the opening frame of
        // the beat that is supposed to turn them into it, and WearForm would
        // then find the folder it is about to wear already worn, take its
        // "nothing actually changed" early return, and never flash the
        // transform.
        //
        // So this stops the coroutine and puts the borrowed popups back --
        // everything a supersede genuinely needs -- and leaves the stage
        // alone. Whatever the previous playback left drawn is replaced by the
        // incoming beat's own paint, which is the only description of the
        // moment that is not from the future.
        private void Supersede()
        {
            // FIRST, so a stopped playback's own finally (PlayBeats) sees it is
            // no longer the current one and neither paints nor reports.
            _playback++;

            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            // The nested steps a beat was waiting on (CloseIn, a wind-up) run
            // as coroutines of their own -- see StartStep -- so stopping the
            // parent is not enough to stop them.
            foreach (var step in _steps)
            {
                if (step != null) StopCoroutine(step);
            }
            _steps.Clear();

            if (popups != null)
            {
                foreach (var popup in popups)
                {
                    if (popup != null) popup.Reclaim();
                }
            }

            IsPlaying = false;
        }

        // Stops playback dead and puts every borrowed thing back.
        //
        // Reclaiming the popups here is the point: without it an abandoned
        // fight leaks one popup per in-flight number out of a pool of six
        // that is never refilled.
        public void Flush()
        {
            Supersede();

            // An abandoned round leaves the stage on live state, same rule as
            // the normal completion path below and for the same reason. The
            // tracker with it: a queue frozen on a beat that will now never
            // finish playing would outlive the round it described.
            //
            // THE HALF Play DELIBERATELY DOES NOT TAKE -- see Supersede. Here
            // nothing is going to be drawn after this, so live state is the
            // moment on screen and painting it is the only honest answer.
            PaintFormation?.Invoke(null);
            PaintTurnOrder?.Invoke(null);
            ResyncForms?.Invoke();

            // AND WHOEVER WAS WAITING IS TOLD.
            //
            // FightController sets _isBusy before Play and clears it ONLY in
            // this callback, and _isBusy is half of CanAct. Without this, a
            // playback stopped rather than finished would leave the
            // controller busy for the rest of the fight: every verb dead,
            // every click ignored, nothing on screen saying why.
            //
            // Same capture-null-invoke as the normal completion path, so a
            // callback that reaches back into this player cannot be run twice.
            var finished = _onFinished;
            _onFinished = null;
            finished?.Invoke();
        }

        // SUPERSEDING A PLAYBACK AND ABANDONING A FIGHT ARE TWO THINGS, kept
        // as two separate methods rather than one.
        //
        // Flush runs from Play, which is the start of the NEXT round -- so
        // stopping every visual there is what killed a tail the instant the
        // following round began. Combat completion and visual completion are
        // distinct; a droplet still falling from the last blow is not a bug for
        // the next beat to clean up.
        //
        // TWO NAMES RATHER THAN Flush(bool endingTheFight). A bool that selects
        // the behaviour rather than being the state is refused by
        // docs/CODE_STANDARDS.md "Functions", and `Flush(true)` at a call
        // site would tell a reader nothing.
        public void EndFight()
        {
            Flush();
            StopVfx?.Invoke();
        }

        // NO OnEnable ADOPTION HERE. FightBootstrap.Start owns the first
        // adoption of a fresh fight (installs PlayerSpeedSource, then calls
        // AdoptPlayerSpeed explicitly before Bind paints anything), and
        // PlayBeats' own per-beat call (below) owns every one after that --
        // an OnEnable adoption would only ever run before FightBootstrap.
        // Start on the same load (Unity runs every OnEnable before any
        // Start) and read whichever source the PREVIOUS fight left behind,
        // a value nothing here paints before the bootstrap's own call
        // overwrites it.
        private void OnDisable()
        {
            // A scene change mid-round is exactly the abandoned-fight case, and
            // it must not be the caller's job to remember.
            EndFight();
        }

        // THE FOURTH TEST SEAM, alongside WireContactFxForTest/WireStageForTest
        // above and IsPlaying: a PlayMode test observing what pace each beat
        // actually adopted (the beat's index and PlayerSpeedMultiplier at
        // its top), without reaching into the coroutine that plays it. A
        // public field rather than a Wire method because PlayMode has no
        // InternalsVisibleTo grant (see DamagePopupColorTests' own comment)
        // and this needs to be settable directly from one.
        public Action<int, float> BeatStarted;

        // IF THE PLAYBACK STOPS FOR ANY REASON, IsPlaying GOES FALSE AND THE
        // FINISH PATH RUNS EXACTLY ONCE.
        //
        // An exception inside a coroutine stops it dead and Unity only logs
        // it. Before this, the one try/catch in a beat covered the impact
        // block, so a throw from anything else in the beat -- the opening
        // SetStance, the lunge, PlayVfx (content-driven), ImpactDelayFor --
        // left IsPlaying true and _onFinished unfired. FightController's busy
        // flag is cleared only by that callback, and its watchdog
        // (RescueAStrandedTurn) trusts IsPlaying, which the dead coroutine
        // owned: every verb dead until the scene was left.
        //
        // Two layers, because C# refuses `yield return` inside a try that has
        // a catch -- so a beat cannot simply be wrapped in one:
        //
        //  1. EVERY STEP OF A BEAT IS ADVANCED INSIDE A CATCH (GuardedSteps).
        //     A throw abandons that one beat -- its outcome is still landed,
        //     see AbandonBeat -- and playback carries on with the next. One
        //     bad sprite path costs one beat's show, not the fight.
        //
        //  2. THE FINISH IS IN A FINALLY, which C# does allow around a yield.
        //     Whatever stops this iterator by throwing -- including the few
        //     lines of it outside layer 1 -- still unwinds through it. Gated on
        //     `playback` so a SUPERSEDED playback, whose finish belongs to
        //     Flush or to nobody (Play), does nothing if Unity disposes it.
        private IEnumerator PlayBeats(IReadOnlyList<CombatBeat> beats, int playback)
        {
            try
            {
                int beatIndex = 0;

                foreach (var beat in beats)
                {
                    // Advanced by hand rather than yielded as a nested
                    // coroutine: a yielded enumerator costs a frame even when
                    // it has nothing left to do (see the CloseIn note below),
                    // and one frame per beat is a timing change nobody asked
                    // for. GuardedSteps itself cannot throw, so this loop
                    // needs no catch of its own.
                    var fault = new StepFault();
                    var steps = GuardedSteps(PlayBeat(beat, beatIndex++), fault);
                    while (steps.MoveNext()) yield return steps.Current;

                    if (fault.Failed) AbandonBeat(beat);
                }
            }
            finally
            {
                if (playback == _playback) FinishPlayback();
            }
        }

        private sealed class StepFault
        {
            public bool Failed;
        }

        // Runs `steps` with every MoveNext inside a catch. A throw is logged,
        // marks `fault`, and ends the run -- the rest of that iterator is not
        // safe to resume.
        //
        // A NESTED ENUMERATOR (CloseIn, StaticSwing.Windup) is started as a
        // coroutine of its own through this same guard, which is what Unity
        // does with a yielded IEnumerator anyway, so its frame timing is
        // unchanged; it is only the guard that is new. Unguarded, a throw in
        // one would stop it and strand its parent waiting forever.
        private IEnumerator GuardedSteps(IEnumerator steps, StepFault fault)
        {
            while (Advance(steps, fault, out object current))
            {
                if (current is IEnumerator nested)
                {
                    var inner = new StepFault();
                    yield return StartStep(nested, inner);

                    if (inner.Failed)
                    {
                        fault.Failed = true;
                        yield break;
                    }
                    continue;
                }

                yield return current;
            }
        }

        private static bool Advance(IEnumerator steps, StepFault fault, out object current)
        {
            current = null;
            try
            {
                if (!steps.MoveNext()) return false;
                current = steps.Current;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                fault.Failed = true;
                return false;
            }
        }

        private Coroutine StartStep(IEnumerator nested, StepFault fault)
        {
            var step = StartCoroutine(GuardedSteps(nested, fault));
            _steps.Add(step);
            return step;
        }

        // A BEAT WHOSE SHOW BROKE STILL HAPPENED. The session resolved it
        // before a frame was drawn, so the numbers it left are true whether
        // or not the flash played: landed here without the show, so the next
        // beat's pre-snapshot does not jump and a killed figure does not stay
        // standing. Each piece guarded on its own -- whatever broke the beat
        // may well break one of these too, and the others still have to run.
        private void AbandonBeat(CombatBeat beat)
        {
            if (beat == null) return;

            // Its sentence too, if the break came before the impact instant
            // got to say it -- a round that resolved must not vanish from the
            // log because its picture failed.
            Guard(() => LogBeat(beat));
            Guard(() => PaintVitals?.Invoke(beat.Snapshot));
            Guard(() => FadeTheFallen?.Invoke(beat));
            Guard(() =>
            {
                foreach (var pair in beat.Stances) SetStance?.Invoke(pair.Key, FightSession.Stances.Idle);
            });
        }

        // Pushes a beat's lines to the log exactly once, whichever of the
        // impact instant and AbandonBeat reaches it first. Reference equality
        // on the beat is enough: beats are one object per resolved action and
        // playback shows them strictly one after another.
        private CombatBeat _loggedBeat;

        private void LogBeat(CombatBeat beat)
        {
            if (beat == null || ReferenceEquals(beat, _loggedBeat)) return;
            _loggedBeat = beat;
            if (beat.Messages == null) return;
            foreach (var line in beat.Messages) PushLine?.Invoke(line);
        }

        private static void Guard(Action step)
        {
            try
            {
                step();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private void FinishPlayback()
        {
            _running = null;
            _steps.Clear();
            IsPlaying = false;

            // BACK TO LIVE STATE. Every beat has been shown, so the order the
            // round finished on is now the order on screen -- and the stage
            // must not go on drawing the last beat's copy of it, or a Move
            // made on the final beat of a round would be undone the moment
            // anything repainted from live data. The queue goes back with it,
            // and unlike the formation it visibly MOVES when it does: the last
            // beat of a round is the last enemy's, so slot 0 holds that
            // monster until this line hands the row back to whoever the
            // schedule says acts next. That step is the point of the tracker.
            //
            // Guarded like everything else on this path: the callback below
            // is the one thing that must not be skipped.
            Guard(() => PaintFormation?.Invoke(null));
            Guard(() => PaintTurnOrder?.Invoke(null));

            // AND WHOSE SKIN EACH FIGURE IS IN, from the same moment and for
            // the same reason. This is where a transform's revert becomes
            // visible: it expired at its holder's turn start, which is not an
            // action and records no beat, so the round in which the timer ran
            // out ends with the ram walking back out as himself.
            Guard(() => ResyncForms?.Invoke());

            var finished = _onFinished;
            _onFinished = null;
            finished?.Invoke();
        }

        // How long a toll's own effect runs: its longest layer (start offset,
        // length and fade), or a single block's seconds. Unscaled.
        internal static float TollSecondsOf(CombatBeat beat)
        {
            var vfx = beat?.Vfx;
            if (vfx == null) return 0f;
            if (!vfx.HasLayers) return vfx.HasAnimation ? vfx.seconds : 0f;

            float longest = 0f;
            foreach (var layer in vfx.layers)
            {
                if (layer == null) continue;
                longest = Mathf.Max(longest, layer.offset + layer.seconds + layer.fade);
            }

            return longest;
        }

        private IEnumerator PlayBeat(CombatBeat beat, int beatIndex)
        {
            // Contract 1: adopted before ANY Scaled/Unscaled call this
            // beat's body makes, so every conversion in it sees one
            // product for the beat's whole duration -- even if the
            // player steps the setting again before the NEXT beat opens
            // (contract 4).
            AdoptPlayerSpeed();
            BeatStarted?.Invoke(beatIndex, PlayerSpeedMultiplier);

            // THE ROUND, BEFORE ANYTHING ELSE THE BEAT SHOWS: a round that
            // started inside this beat's recording is a statement about the
            // moment the beat opens. Guarded -- a missing clip or overlay is
            // the view's graceful no-op, and a throw in it must not cost the
            // blow its show.
            if (beat.RoundStarted > 0) Guard(() => PresentRound?.Invoke(beat.RoundStarted));

            // A ROUND'S OWN BEAT has no actor, no target and nothing landed:
            // the toll above is the whole of it. It holds long enough for the
            // counter to be read and the toll to ring before the next beat
            // opens, and skips every step below, each of which is about a
            // figure doing something to another.
            if (beat.Cause == BeatCause.RoundStart)
            {
                Guard(() => LogBeat(beat));

                // A TOLL THAT PRESENTS (PLAN_BELLWETHER_KIT 1.7): the rallying
                // monster is the beat's actor, wears its rally pose and plays
                // its effect for the length of the hold -- or of the effect,
                // if that runs longer -- then stands down.
                if (beat.Actor != null)
                {
                    string pose = null;
                    beat.Stances.TryGetValue(beat.Actor, out pose);
                    if (!string.IsNullOrEmpty(pose)) Guard(() => SetStance?.Invoke(beat.Actor, pose));
                    if (beat.Vfx != null && beat.Vfx.HasArt) Guard(() => PlayVfx?.Invoke(beat));

                    yield return new WaitForSeconds(Scaled(Mathf.Max(RoundStartHoldSeconds, TollSecondsOf(beat))));

                    if (!string.IsNullOrEmpty(pose))
                        Guard(() => SetStance?.Invoke(beat.Actor, FightSession.Stances.Idle));
                    yield break;
                }

                yield return new WaitForSeconds(Scaled(RoundStartHoldSeconds));
                yield break;
            }

            // THE PRE-SNAPSHOT, which is the whole reason the session
            // records two.
            //
            // A spell's bolt takes most of a second to arrive. Dropping the
            // target's HP the instant the beat opens shows the damage before
            // the spell has left the ceiling -- the number moves, then the
            // thing that caused it happens. Painting what stood BEFORE the
            // blow and only landing the after-state at the impact frame is
            // what puts cause back in front of effect.
            // THE FORMATION FIRST, THEN THE NUMBERS.
            //
            // Where everybody stands is settled before anything else about
            // the beat is drawn, because the rest of the beat is measured
            // against it: TravelFor reads the two figures' marks to
            // work out where the actor stands to strike, and the damage
            // popup is placed off the target's slot. Painted from the
            // beat's own snapshot rather than from the live lists for the
            // reason the vitals are -- a Move rewrites the party order in
            // place, so live state is the order the ROUND finished on.
            //
            // UNLESS THE BEAT MOVES SOMEBODY AT ITS IMPACT (Dark Chains, a
            // drag after a hit -- CombatBeat.PreFormation): then the stage
            // opens on where everybody stood BEFORE, and the pull is painted at
            // the impact instant below, so the chains land and THEN he is
            // dragged, rather than walking to the front before they arrive.
            PaintFormation?.Invoke(beat.PreFormation ?? beat.Formation);

            // AND WHO IS UP NEXT AS OF THIS BEAT, from the same moment and
            // for the same reason -- see PaintTurnOrder's own header. Sent
            // here beside the formation rather than at the impact frame
            // because the queue describes the beat as a whole rather than
            // any instant inside it: whoever held the turn when it resolved
            // holds slot 0 for as long as the beat is drawn.
            PaintTurnOrder?.Invoke(beat.TurnOrder);

            // AND THE FIGURES HAVE TO ARRIVE BEFORE THE BEAT GOES ON.
            //
            // A Move's own beat is the case this is for: the two party
            // members cross over about a third of a second, and the enemy
            // reply that follows in the SAME round would otherwise open
            // while they were still passing each other -- aiming at the
            // gap between them, landing its flash on whichever figure
            // happened to be nearer. The other case is a line closing up
            // over a corpse that has just finished fading.
            //
            // A while rather than a fixed wait: the walk's length is the
            // animator's business (StageActorAnimator.GlideSeconds, scaled
            // like everything else on the beat clock), and a beat that
            // waited its own guess at that number would drift the day it
            // changed. Nothing is moving on the overwhelming majority of
            // beats, so this costs one delegate call.
            //
            // AND NEITHER OF THE TWO FIGURES THIS BEAT IS ABOUT IS STILL
            // REELING FROM AN EARLIER ONE. One gate rather than two
            // consecutive whiles, because there is one claim being made
            // here and everything below depends on all of it: the bodies
            // this beat measures are standing on the marks it measures
            // them by. See StillReeling for what a second attacker used
            // to land on.
            //
            // BEFORE PaintVitals, so the numbers on screen through the
            // settle are still the ones the last blow left -- the reel
            // being waited out belongs to that blow, and painting the
            // incoming beat's pre-snapshot over it would move a health
            // bar for a hit that has not opened yet.
            while ((FormationIsMoving != null && FormationIsMoving()) || StillReeling(beat))
            {
                yield return null;
            }

            PruneReeling();

            PaintVitals?.Invoke(beat.PreSnapshot);

            // THE LOG LINE IS NOT PUSHED HERE ANY MORE -- see LogBeat, called
            // at the impact instant below. A beat's lines are whole sentences
            // the session wrote after resolving it ("casts Crownfall on the
            // Treant for 10!"), so pushing them at the open printed the
            // damage a whole spell's travel before the spike arrived.

            // THE ACTOR'S POSE NOW; EVERYONE ELSE'S AT THE IMPACT INSTANT.
            //
            // The beat records a stance for every combatant it mentions.
            // Applying all of them when the beat opens is wrong for the one
            // taking the blow: the victim would wear its "hurt" drawing
            // through the attacker's whole wind-up, flinching from a swing
            // that has not left its mark yet -- a whole crouch-and-cross of
            // pre-emptive flinching for a StaticSwing wind-up, and longer
            // still for any spell with a travel time. Cause has to come
            // before effect on the stage as well as in the log, which is the
            // same argument the two snapshots make.
            //
            // The actor is different: its stance IS the wind-up, so it has
            // to be worn from the first frame. PoseVictims below is the
            // other half, called from the impact block.
            //
            // AND IT CAN NOW BE UP TO THREE DRAWINGS RATHER THAN ONE --
            // approach, wind-up, strike. CombatBeat's own header carries
            // the precedence table; what follows is that table, applied.
            // Everything about it collapses to today's single SetStance
            // for a beat that authored neither of the two new poses, which
            // is every beat in the game bar one.
            string strikeStance = null;
            if (beat.Actor != null) beat.Stances.TryGetValue(beat.Actor, out strikeStance);

            // WHICH drawing at which moment is CombatBeat's rule, so an
            // EditMode test can pin the fallbacks; WHEN each moment falls
            // is this file's, because only it knows what a walk-in or a
            // crouch costs.
            string openStance = CombatBeat.OpenStanceFor(
                beat.Approach, strikeStance, beat.ActorApproachStance, beat.ActorWindupStance);
            string arrivalStance = CombatBeat.ArrivalStanceFor(
                beat.Approach, strikeStance, beat.ActorApproachStance, beat.ActorWindupStance);

            // Whether this beat has a wind-up POSE at all, which is what
            // decides below whether it also has to buy the WAIT to hold it
            // through. Blank-is-unauthored, plus the no-strike-no-phases
            // rule CombatBeat.Normalise states -- read through the same
            // two functions above rather than re-tested here, so there is
            // one answer to "did this beat author a wind-up".
            bool hasWindup = !string.IsNullOrWhiteSpace(strikeStance)
                             && !string.IsNullOrWhiteSpace(beat.ActorWindupStance);

            // What the actor is wearing right now, so the impact instant
            // can tell whether it still has to change into the strike.
            string wornStance = openStance;

            if (beat.Actor != null && openStance != null)
            {
                SetStance?.Invoke(beat.Actor, openStance);
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
            // The PlayMode tests covering this poll for the impact with a
            // deadline rather than sampling a fixed frame, because a Lunge's
            // own wind-up (StaticSwing) makes it land a frame or two later on
            // purpose. The trap above is still a trap, and a new
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

            // ARRIVED, AND NOW HE RAISES IT. The one moment a Close has
            // that no other approach does: the walk-in is over and the
            // blow has not started, which is exactly where "then he holds
            // his hammer over his head" goes.
            if (arrivalStance != null)
            {
                SetStance?.Invoke(beat.Actor, arrivalStance);
                wornStance = arrivalStance;
            }

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

            // THE CAST CUE, at the moment the beat opens. The pressure
            // building through a spell's wind-up, as against the transient
            // that punctuates its contact -- see SpellPresentation's
            // castSfxPath for why the two are separate paths and not one.
            // An empty path is a silent no-op, so this needs no guard.
            SoundController.PlayClip(beat.Vfx.castSfxPath);

            // Wind-up: the crouch and the cross for a Lunge, or the
            // charge's own outbound travel (chargeOutSeconds, floored at
            // ChargeMinOutSeconds) for a Charge -- up to the moment the
            // blow would connect either way. A spell instead waits out its
            // VFX's impact fraction below; only one of these three is ever
            // non-zero for a given beat, so they add rather than compete.
            if (staticSwing) yield return StaticSwing.Windup();
            else if (staticCharge) yield return StaticSwing.Windup(chargeOutSeconds);

            // A WIND-UP THE BEAT DID NOT ALREADY HAVE, and the only thing
            // in this file that authoring a pose can BUY.
            //
            // A Lunge and a Charge already wait one out above, so a pose
            // authored on either simply gets worn through the wait that
            // was there. A Hold and a Close do not: a Hold never crosses
            // anything, and a Close's walk-in finishes before the blow
            // opens -- so without this a raised hammer would be drawn for
            // one frame and then be a slam.
            //
            // StaticSwing's OWN NUMBER rather than a second constant: the
            // crouch before a swing and the hammer held at the top of its
            // arc are the same beat of anticipation, and two constants for
            // one idea drift. Not StaticSwing.Windup() itself, though --
            // that plays the swing's whoosh, and the cue belongs to a
            // weapon cutting air rather than to every pose that pauses.
            //
            // CHARGED TO `spent` BELOW, never added on top: the settle
            // gives back exactly what this took, so a beat with a wind-up
            // is the same length as one without. Adding time here instead
            // is the "beat runs long" failure SettleAfter's own header
            // records going unnoticed once already.
            float boughtWindup = 0f;
            if (hasWindup && !staticSwing && !staticCharge)
            {
                boughtWindup = StaticSwing.WindupSeconds;
                yield return new WaitForSeconds(Scaled(boughtWindup));
            }

            // SKIPPED FOR A STATIC CHARGE: chargeOutSeconds already IS
            // Max(ChargeMinOutSeconds, impact) -- see ChargeOutSeconds --
            // so the wind-up just waited out at least this much. Waiting
            // it again here would either do nothing (the common case, no
            // spell) or double an authored spell's own impact delay.
            float impact = ImpactDelayFor == null ? 0f : ImpactDelayFor(beat);
            if (impact > 0f && !staticCharge) yield return new WaitForSeconds(Scaled(impact));

            // THE LOG SAYS IT WHEN THE STAGE SHOWS IT: on the impact frame,
            // beside the damage number, for every approach -- a spell's
            // travel, a lunge's cross, a charge's rush and a close-in walk
            // have all been waited out by this line. Guarded on its own
            // rather than inside the block below, so a flash or popup that
            // throws cannot take the sentence down with it.
            Guard(() => LogBeat(beat));

            // The blow lands: the numbers move, the target flashes and the
            // floating figure appears, all on the same frame.
            //
            // GUARDED HERE AS WELL AS BY GuardedSteps. That outer guard keeps
            // the fight alive but abandons the rest of the beat; this one
            // is finer, so a bad flash or popup still leaves the hold, the
            // fade and the return to idle playing at their proper moments.
            //
            // Logged rather than swallowed -- this is the house's graceful
            // degradation, not a silence.
            try
            {
                // THE STRIKE, and it goes on first of everything here.
                //
                // ONLY IF THE ACTOR IS NOT ALREADY IN IT. A beat that
                // authored no phase poses opened in the strike and must
                // make exactly the SetStance calls it has always made --
                // an unconditional call here would fire a second one on
                // every beat in the game, repainting the whole stage for
                // nothing and quietly changing what the ordering pins in
                // this suite are measuring.
                if (strikeStance != null && wornStance != strikeStance)
                {
                    SetStance?.Invoke(beat.Actor, strikeStance);
                    wornStance = strikeStance;
                }

                // AND WHOEVER BECAME SOMETHING ELSE, on the same frame.
                // Before the flash below for the reason PoseVictims is:
                // changing form re-syncs the hit-flash silhouette to the
                // new drawing, and the whole point of the flash is to be
                // the shape of what he turned INTO.
                ApplyForms(beat);

                // FIRST, before the flash: SetStance is what re-syncs the
                // hit-flash overlay's silhouette to the drawing under it,
                // and a flash shaped like the pose the victim just left is
                // worse than no flash.
                PoseVictims(beat);

                PaintVitals?.Invoke(beat.Snapshot);

                // THE IMPACT CLIP, HERE RATHER THAN AT THE TOP OF THE BEAT --
                // firing it beside PlayVfx would put a spell's own sound a
                // whole impact delay ahead of the blow it describes, since
                // the number, the flash and the recoil all happen here. A
                // spell with a sound but no frames is still audible:
                // ImpactDelayFor returns 0 for a beat with no frames, so a
                // frames-less cast reaches this line on the same frame it
                // would have from the top of the beat.
                SoundController.PlayClip(beat.Vfx.sfxPath);

                ShowAmount(beat);
                FlashTarget?.Invoke(beat);
                if ((staticSwing || staticCharge) && WantsContactFx(beat)) PlayContactFx?.Invoke(beat);

                // AFTER the house's, so a form's heavier burst draws OVER
                // the arc rather than under it -- authored order is draw
                // order within a band (docs/ART_PIPELINE.md 5b), and two
                // casts obtain pool members in the order they were begun.
                // Ungated on approach: a form's blow punctuates the same
                // whether it lunged, charged or stood still.
                if (beat.FormVfx != null) PlayFormHitFx?.Invoke(beat);
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

            // THE PULL (see the PreFormation note at the top of the beat): the
            // chains land, hold while they snap taut, and only then is he
            // dragged -- painted PullHoldSeconds after the impact rather than
            // on it, so the bind is seen closing on him where he stood. The
            // hold is charged to the settle below, so the beat is no longer.
            float pullHold = 0f;
            if (beat.PreFormation != null)
            {
                pullHold = PullHoldSeconds;
                yield return new WaitForSeconds(Scaled(pullHold));
                Guard(() => PaintFormation?.Invoke(beat.Formation));
            }

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
            //
            // AND A BOUGHT WIND-UP IS SPENT TIME LIKE ANY OTHER. It is
            // added to StillPoseSeconds rather than replacing it: the 0.08
            // is the notional cost of showing a drawing, which the beat
            // still does, and boughtWindup is real seconds this beat
            // actually waited on top of it.
            float spent = staticSwing ? StaticSwing.WindupSeconds
                : staticCharge ? chargeOutSeconds
                : StillPoseSeconds + boughtWindup;
            spent += pullHold;
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

        // ONE NUMBER PER THING THE BEAT LANDED ON.
        //
        // A sweep records what each enemy took (CombatBeat.Results); everything
        // else records one Amount against one Target. Both reach the same
        // placement below, so the two paths cannot drift apart on WHERE a
        // number appears -- only on how many there are.
        private void ShowAmount(CombatBeat beat)
        {
            if (beat.HasPerTargetResults)
            {
                foreach (var result in beat.Results)
                {
                    // Each result carries its own hit/miss, so the rule the
                    // single-target case applies once is applied per enemy: a
                    // dodge shows its own popup, a landed nothing shows none.
                    if (result.Target == null) continue;
                    if (!result.Missed && result.Amount <= 0) continue;

                    PopNumber(beat, result.Target, result.Amount, result.Missed, result.Crit);
                }

                return;
            }

            ShowSingleAmount(beat);
        }

        private void ShowSingleAmount(CombatBeat beat)
        {
            // PHASE D1: a miss shows its OWN popup even though Amount stays
            // 0 -- the exact case FlashCombatant/Recoil/Punch above still
            // correctly skip (nothing landed, so no hit reaction), but the
            // player still needs to be TOLD nothing landed, or a dodge reads
            // as no different from a Hold Back turn or a non-damaging cast.
            // See CombatBeat.Missed and DamagePopup.PlayMiss's own headers.
            if (beat.Target == null) return;
            if (beat.Missed)
            {
                PopNumber(beat, beat.Target, beat.Amount, missed: true);
                return;
            }

            // WHAT ACTUALLY REACHED HEALTH, not the raw swing -- a shield
            // (CombatBeat.Absorbed's own header) can eat part or all of
            // Amount before it gets there, and a popup that showed the raw
            // figure over a target whose HP bar barely moved would disagree
            // with the bar it sits above.
            int healthDamage = beat.Amount - beat.Absorbed;
            if (healthDamage < 0) healthDamage = 0;

            // THE ABSORBED POPUP, whenever there is one to show -- BEFORE
            // the health-damage return below, so a FULLY absorbed hit still
            // gets its own popup (PlayAbsorbed's own header: this is the
            // "shown rather than nothing" case) rather than falling through
            // the healthDamage <= 0 guard into silence.
            if (beat.Absorbed > 0) PopAbsorbed(beat.Target, beat.Absorbed);

            if (healthDamage <= 0) return;

            PopNumber(beat, beat.Target, healthDamage, missed: false, crit: beat.Crit);
        }

        // ONE NUMBER, WHEREVER IT CAME FROM. Both paths above end here, so the
        // headroom arithmetic has one home rather than one per path.
        //
        // ABOVE THE FIGURE, not on it: spawning at the slot's own centre
        // (the middle of the combatant) would have the number rise out from
        // behind the sprite it is describing, spending its first frames --
        // the opaque ones -- hidden by it. Starting a head above means the
        // whole punch is visible and the rise carries it clear rather than
        // into view.
        //
        // MEASURED OFF THE SLOT rather than a constant: enemy and party slots
        // are not the same height, and a fixed offset would sit on one and
        // float over the other.
        private void PopNumber(CombatBeat beat, CombatantState target, int amount, bool missed, bool crit = false)
        {
            var popup = FreePopup();
            if (popup == null) return;   // every one still in flight; the number is dropped, not queued

            var at = PopupAnchor(target, 0f);
            float lifeSeconds = Scaled(DamagePopup.LifeSeconds);

            if (missed) popup.PlayMiss(at, lifeSeconds);
            else popup.Play(at, amount, beat.IsHealing, beat.DamageType, lifeSeconds, crit);
        }

        // THE ABSORBED POPUP -- ITS OWN CALL, not a branch inside PopNumber,
        // because a PARTIAL absorb shows BOTH: the health-damage number
        // (what got through) and this one (what a shield kept), at the same
        // moment, for the same beat. Two "-n"-shaped popups stacked on one
        // anchor would read as one hit disagreeing with itself, so this one
        // rises from a SECOND anchor, one popup-height further up, rather
        // than sharing PopNumber's.
        private void PopAbsorbed(CombatantState target, int absorbed)
        {
            var popup = FreePopup();
            if (popup == null) return;

            var at = PopupAnchor(target, AbsorbedPopupExtraHeadroom);
            float lifeSeconds = Scaled(DamagePopup.LifeSeconds);
            popup.PlayAbsorbed(at, absorbed, lifeSeconds);
        }

        // Roughly one popup's own rise-and-settle height above PopNumber's
        // usual spot -- enough that two numbers for one hit read as a
        // stack, not a smear.
        private const float AbsorbedPopupExtraHeadroom = 26f;

        private Vector2 PopupAnchor(CombatantState target, float extraHeadroom)
        {
            var slot = SlotFor?.Invoke(target);
            if (slot == null) return Vector2.zero;

            return slot.anchoredPosition +
                   new Vector2(0f, slot.rect.height * 0.5f + PopupHeadroom + extraHeadroom);
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

            var (animator, offset) = TravelFor(beat, StageStandOff.Gap);
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

            // ASKED OF WHETHER ANYTHING LANDS ON THE TARGET, not of HasArt.
            // HasArt also counts a bare `groundPath` -- a fault opening under
            // the whole formation -- which puts nothing on the STRUCK target's
            // own body. Reading that as "brings its own art" would leave a
            // ground-only spell with no impact language on the target at all:
            // no house arc and no per-target art of its own (there is none).
            // HasPerTargetArt is the same question
            // `path` alone always answered for a pre-layer block, generalised
            // to a layer with a per-target placement.
            return beat.Vfx == null || !beat.Vfx.HasPerTargetArt;
        }

        // THE COMMITTED RUSH. Like Lunge in order -- the travel runs alongside
        // the swing rather than before it -- but with no gap left at the end
        // (StageStandOff.ChargeGap), and timed to be at full extent exactly
        // when the blow lands.
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

            var (animator, offset) = TravelFor(beat, StageStandOff.ChargeGap);
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
        // THE SAME DISTANCE AS A LUNGE, now that both stop at the same
        // measured stand-off point rather than at their own fraction of the
        // gap. That collapse is deliberate: "how close do you have to be to
        // hit this thing" was never a property of the approach, and the two
        // fractions that said otherwise (0.70 and 0.78) were two guesses at
        // one number. What still separates a Close from a Lunge is the ORDER
        // -- arrive, THEN swing -- which is the difference this method's own
        // header opens with and the only one worth having.
        //
        // THE HOLD IS TAKEN OUT OF NOTHING, unlike the hit-stop. It is real
        // extra time and the beat is genuinely longer for it, because there is
        // no honest way to show a creature crossing the stage inside a budget
        // that assumed it stood still. Authored per skill, so only the blows
        // that want it pay for it.
        private IEnumerator CloseIn(CombatBeat beat)
        {
            if (beat.Approach != StageApproach.Close) yield break;

            var (animator, offset) = TravelFor(beat, StageStandOff.Gap);
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
        // it. Shared by all three crossing approaches so they cannot disagree
        // about which direction the stage runs in.
        //
        // A STAND-OFF POINT, NOT A FRACTION OF THE GAP -- see
        // Domain/Stage/StageStandOff for why a fraction of the gap breaks
        // (stopping a body short from the back rank, or landing a hammer
        // inside whatever it slammed). Everything about WHERE
        // is arithmetic over two measured bodies, which is in Domain and
        // pinned there; everything about WHICH DRAWING to measure is here,
        // because only playback knows what the actor will be wearing when it
        // arrives.
        private (StageActorAnimator animator, Vector2 offset) TravelFor(CombatBeat beat, float gap)
        {
            // The same trigger the wind-up and the contact effects read
            // (CrossesToATarget): a beat with nobody else to reach does not
            // travel, so self-targeting is caught here rather than left to
            // fall through to a zero-length "cross" just because the two
            // marks happen to be identical.
            if (beat == null || !CrossesToATarget(beat)) return (null, Vector2.zero);

            var from = SlotFor?.Invoke(beat.Actor);
            var to = SlotFor?.Invoke(beat.Target);
            if (from == null || to == null) return (null, Vector2.zero);

            var animator = AnimatorFor?.Invoke(beat.Actor);
            if (animator == null) return (null, Vector2.zero);

            var targetAnimator = AnimatorFor?.Invoke(beat.Target);

            // MARKS, NOT LIVE POSITIONS. The animator composes travel and
            // hover on top of the mark, so an offset measured from a live
            // position would double-count whatever the figure was already
            // doing -- and a hovering TARGET would drag its attacker into the
            // air rather than being met on the floor it casts its shadow on.
            var actorMark = ToUiVec(animator.Mark);
            var targetMark = ToUiVec(targetAnimator != null ? targetAnimator.Mark : to.anchoredPosition);

            // THE GOAL SCALE, not the slot's live one: breath, punch and
            // stretch all write the slot's localScale, so reading it back
            // would fold a 3% wobble into where the attacker stops.
            //
            // The target's own silhouette is taken from its IDLE. Its hurt
            // pose is what it wears a frame later, it is usually narrower, and
            // it recoils out of the way regardless -- so measuring the resting
            // body is both the conservative choice and the stable one.
            var targetSpan = targetAnimator != null
                ? targetAnimator.SpanForStance(FightSession.Stances.Idle)
                : OpaqueSpan.None;

            var offset = StageStandOff.TravelTo(
                actorMark, ReachOf(animator, beat), ScaleOf(animator, from),
                targetMark, targetSpan, ScaleOf(targetAnimator, to),
                gap);

            return (animator, new Vector2(offset.X, offset.Y));
        }

        private static float ScaleOf(StageActorAnimator animator, RectTransform slot)
        {
            float scale = animator != null ? animator.GoalScale.x : slot.localScale.x;
            return scale <= 0f ? 1f : scale;
        }

        // THE WIDEST DRAWING THE ACTOR WILL WEAR WHILE IT IS STANDING THERE.
        //
        // Not simply the strike pose. The actor holds the stand-off point from
        // the moment it arrives until the recoil pulls it home, and it wears
        // up to three drawings in that window (approach, wind-up, strike --
        // CombatBeat's own precedence table). Measuring only one of them
        // leaves the other two free to clip: Bjorn's rush reaches 206px past
        // his canvas centre, his overhead 166 and his slam 237, so a distance
        // set by the rush puts the hammer 31 canvas pixels inside the target
        // at the one instant the picture is about contact.
        //
        // THE DRAWINGS IT ACTUALLY WEARS, AND ONLY THOSE. CombatBeat's own
        // header says what the empty list means and what it cost to learn:
        // asking SpanForStance about a phase this beat does not author
        // answers with the IDLE, and a stand-off set by an idle the actor
        // never wears is daylight nobody authored.
        //
        // Costs at most three cached texture-rect lookups per crossing beat.
        private static OpaqueSpan ReachOf(StageActorAnimator animator, CombatBeat beat)
        {
            string strike = null;
            if (beat.Actor != null) beat.Stances.TryGetValue(beat.Actor, out strike);

            var worn = CombatBeat.StandOffStancesFor(
                beat.Approach, strike, beat.ActorApproachStance, beat.ActorWindupStance);

            // Nothing authored anywhere on this beat, so the figure crosses
            // the stage in whatever it already has on -- which is what a null
            // stance resolves to, and here that is the right answer rather
            // than the wrong one.
            if (worn.Count == 0) return animator.SpanForStance(null);

            var span = animator.SpanForStance(worn[0]);
            for (int i = 1; i < worn.Count; i++) span = Widest(span, animator.SpanForStance(worn[i]));
            return span;
        }

        // BOTH PAIRS, INDEPENDENTLY. The pose with the longest reach is not
        // necessarily the pose with the widest body -- Bjorn's overhead is his
        // thickest drawing and his slam his longest -- so taking one pair's
        // winner and copying its other pair across would measure a body that
        // no single drawing has.
        private static OpaqueSpan Widest(OpaqueSpan a, OpaqueSpan b)
        {
            return new OpaqueSpan(
                Mathf.Min(a.Left, b.Left), Mathf.Max(a.Right, b.Right),
                Mathf.Min(a.MassLeft, b.MassLeft), Mathf.Max(a.MassRight, b.MassRight));
        }

        private static UiVec ToUiVec(Vector2 v) => new UiVec(v.x, v.y);

        // WHOEVER THIS BEAT TURNED INTO SOMETHING ELSE.
        //
        // Null on every beat but a transform's, so this costs one null test
        // per beat. Handed straight over the WearForm door rather than
        // interpreted here, the same posture PaintFormation takes: playback
        // says who changes and when, the view owns what changing looks like.
        private void ApplyForms(CombatBeat beat)
        {
            if (beat.Forms == null) return;

            foreach (var pair in beat.Forms) WearForm?.Invoke(pair.Key, pair.Value);
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
            // EVERYONE THE SWEEP LANDED ON, not only its primary. A sweep's
            // Results carry a per-enemy amount, so "nothing landed, nothing
            // flinches" is applied per enemy rather than once for the whole
            // cast -- an enemy that dodged Cinderfault must not recoil from it
            // while the two beside it do.
            if (beat.HasPerTargetResults)
            {
                foreach (var result in beat.Results)
                {
                    if (result.Amount <= 0) continue;

                    // A sweep's BeatTargetResult carries no per-target
                    // Absorbed, so this path classifies off the raw amount
                    // alone.
                    RecoilOne(beat, result.Target, result.Amount, absorbed: 0, missed: result.Missed);
                }

                return;
            }

            if (beat.Amount <= 0 && beat.Absorbed <= 0) return;
            RecoilOne(beat, beat.Target, beat.Amount, beat.Absorbed, beat.Missed);
        }

        // ONE HIT, CLASSIFIED AND THEN MOVED. HitStrength.Classify answers
        // "how hard did this land" from the numbers alone; this method's own
        // job shrank to mapping that answer onto an animator call -- see
        // HitStrength's own header for why the classification itself lives
        // in Domain rather than here.
        private void RecoilOne(CombatBeat beat, CombatantState target, int amount, int absorbed, bool missed)
        {
            if (target == null) return;

            // Nobody flinches away from themselves. A self-heal still gets its
            // number, just no recoil.
            if (ReferenceEquals(target, beat.Actor)) return;

            var tier = HitStrength.Classify(amount, absorbed, target.MaxHealth, missed, KilledOnThisBeat(beat, target));
            if (tier == HitStrength.Tier.None) return;

            var animator = AnimatorFor?.Invoke(target);
            if (animator == null) return;

            float distance = HitStrength.DistanceFor(tier);
            float dwellSeconds = RecoilDwellSeconds * HitStrength.DwellFractionFor(tier);

            float dx = target.IsPlayerSide ? -distance : distance;
            var offset = new Vector2(dx, 0f);

            // THE RETURN LEG IS THIS BEAT'S, not every mover's. Passed as an
            // argument rather than by raising StageActorAnimator.ReturnSeconds
            // -- that constant is the recovery for a lunge, a charge and a
            // walk-in as well, and a reel four times slower is a statement
            // about being HIT, not about coming home from anything. NOT
            // scaled by tier: the spring-back reads the same whichever
            // distance it is coming back from, only the dwell at full
            // extent (above) is what a light tap shortens.
            animator.Play(offset, Scaled(dwellSeconds), returnSeconds: RecoilReturnSeconds);

            // AND A LATER BEAT AIMED AT THIS BODY HAS TO WAIT FOR IT -- see
            // StillReeling.
            if (!_reeling.Contains(animator)) _reeling.Add(animator);
        }

        // WHETHER THIS BEAT'S OWN SNAPSHOT LEFT THE TARGET AT ZERO -- read
        // off Snapshot rather than the combatant's LIVE health for the exact
        // reason every other beat field prefers its own snapshot (this
        // file's header, and CombatBeat's): resolution has already run the
        // whole round by the time any beat plays, so live health answers for
        // the END of the round on every beat, not for this one.
        private static bool KilledOnThisBeat(CombatBeat beat, CombatantState target)
        {
            return beat?.Snapshot != null
                && beat.Snapshot.TryGetValue(target, out var vitals)
                && vitals.Health <= 0;
        }

        // WHOEVER IS STILL COMING HOME FROM A BLOW.
        //
        // Pruned rather than cleared per beat, because the reel is 1.505s and
        // a beat is 0.75s: a figure struck on beat 1 is still reeling during
        // beat 3, so a register that only remembered the previous beat would
        // forget it exactly when it still mattered. Nothing accumulates --
        // PruneReeling drops an animator the frame it stops moving, and the
        // list is at most one entry per combatant on the stage.
        private readonly List<StageActorAnimator> _reeling = new List<StageActorAnimator>();

        // WHETHER THIS BEAT IS ABOUT TO MEASURE A BODY THAT IS NOT WHERE ITS
        // MARK SAYS IT IS.
        //
        // THE PROBLEM THIS EXISTS FOR. TravelFor measures the stand-off
        // against both figures' MARKS -- it has to, see its own header -- so
        // it silently assumes the bodies are standing on them. At a 0.4175s
        // reel that was true by the time the next beat opened. At 1.505s it
        // is not: the second and third attacker of a round arrive at a target
        // still shoved RecoilDistance further away than the stand-off aimed
        // at, and the blow lands in 45px of daylight. The one picture this
        // whole stand-off mechanism exists to produce is a blow landing ON
        // something.
        //
        // THE ACTOR AND THE TARGET, NOT THE WHOLE STAGE. Those two are what
        // TravelFor measures, so those two are what the claim is about. A
        // THIRD figure still easing home from its own hit while this blow
        // lands somewhere else is not a defect -- it is what a reel four
        // times slower than the beat looks like, and it was asked for.
        //
        // THE COST, MEASURED. A beat's tail after the impact instant is about
        // 0.625s (hit-stop plus SettleAfter plus BeatGapSeconds), so a reel of
        // 1.505s has about 0.88s left when the very next beat opens, 0.255s
        // when the one after that does, and nothing by the third. This gate
        // therefore pays up to 0.88s, and only when a blow is aimed at a body
        // that was just hit -- focused fire and sweeps, not every beat.
        //
        // Widening it to the whole stage would be the stronger claim and pays
        // that 0.88s on EVERY damaging beat after the first: about 4.4s on a
        // six-beat round, which more than doubles it. If that is wanted
        // anyway, this predicate is the one place to change.
        //
        // WAIT, RATHER THAN HURRY THE SPRING-BACK. Cutting the return short
        // when the next beat needs the mark would make the reel's length
        // depend on what happens after it, so the same blow would read
        // differently in a duel and in a crowd. Waiting costs pacing and says
        // so; it does not cost the reel its shape.
        private bool StillReeling(CombatBeat beat)
        {
            if (beat == null || _reeling.Count == 0) return false;

            return IsReeling(beat.Actor) || IsReeling(beat.Target);
        }

        private bool IsReeling(CombatantState who)
        {
            if (who == null) return false;

            var animator = AnimatorFor?.Invoke(who);

            // BOTH HALVES. IsPlaying alone would also be true of a figure
            // mid-LUNGE, and _reeling alone would still name one whose reel
            // finished a frame ago.
            return animator != null && animator.IsPlaying && _reeling.Contains(animator);
        }

        private void PruneReeling()
        {
            for (int i = _reeling.Count - 1; i >= 0; i--)
            {
                var animator = _reeling[i];
                if (animator == null || !animator.IsPlaying) _reeling.RemoveAt(i);
            }
        }

        // NO TRAVEL FRACTIONS: a target position is a stand-off point (see
        // StageStandOff, Domain/Stage), never a fraction of the gap. A
        // fraction of the gap leaves a residual that GROWS with the gap, so
        // the same swing stops a body short from the back rank and looks
        // fine from the front one, and a fraction knows nothing about how
        // far the attacker's own weapon reaches, so a slam can go through
        // whatever it is slamming.
        //
        // `staticSwing` decides only the anticipation LEAD; TravelFor moves
        // nothing for a beat that does not cross to a target, because the
        // actor and the target are then the same figure on the same mark.

        // RecoilDistance (the flat 45px every struck figure slides) restates
        // as HitStrength.HeavyDistance, the top of the three-tier table,
        // rather than a second literal this file could drift from.

        // ---- how long a struck figure reels ----------------------------------
        //
        // A reel is three legs and the slowdown is spread over the two that
        // are MOTION:
        //
        //   PUSH OUT, StageActorAnimator.LungeSeconds, 0.055s -- UNCHANGED.
        //   It is what puts the body where the flash and the damage number
        //   already are, so stretching it slides the impact frame off the
        //   one those two land on. The blow still connects as hard as it did.
        //
        //   DWELL, below, 0.2025s -> 0.81s. The motionless beat at full
        //   extent.
        //
        //   SPRING BACK, 0.16s -> 0.64s. The recovery -- stretching the dwell
        //   alone would make the reel four times LONGER without making
        //   anything about it four times SLOWER, since that extra time is a
        //   figure standing still and "too fast" is a complaint about
        //   velocity. This is the leg the eye actually reads as the flinch
        //   easing off.
        //
        // IN BEATS, NOT IN SECONDS, and that is the other half of the fix.
        // The dwell was always a fraction of BeatHoldSeconds; replacing it
        // with a flat 1.45f took the reel out of the beat's system of units,
        // so the preset and the beat budget could move underneath it. Both
        // legs are now multiples of the beat, so the reel is 3.2 beats long
        // at every speed and stays that way if the budget ever moves.
        //
        // NOT 4x THE DISTANCE. RecoilDistance above is unchanged: a body
        // shoved four times as far reads as a knockback, which is a different
        // move from a flinch.
        private const float ReelSlowdown = 4f;

        // The dwell is 0.45 of a beat.
        public const float RecoilDwellBeats = 0.45f * ReelSlowdown;

        // The spring-back is StageActorAnimator's flat ReturnSeconds, 0.16s,
        // which is this much of a 0.45s beat. Written as the arithmetic
        // rather than as 1.4222f so the number cannot be read as arbitrary:
        // it is that constant, in beats, times four.
        public const float RecoilReturnBeats = (0.16f / BeatHoldSeconds) * ReelSlowdown;

        // THE AUTHORED LENGTHS, before the preset. 0.81s and 0.64s.
        //
        // Neither is scaled here, and the two are handed to the animator
        // through different doors because the animator already treats them
        // differently: holdSeconds arrives pre-scaled, every tween duration
        // arrives raw and is scaled inside PlayRoutine. RecoilOne therefore
        // scales the dwell and does not scale the return -- see its call.
        public const float RecoilDwellSeconds = BeatHoldSeconds * RecoilDwellBeats;
        public const float RecoilReturnSeconds = BeatHoldSeconds * RecoilReturnBeats;

        // How long the walk in takes. Long enough to read as a decision and
        // short enough that a fight full of them does not become a parade.
        private const float CloseSeconds = 0.26f;

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

            var animator = AnimatorFor?.Invoke(beat.Target);

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
