using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // THE RUN-UP (docs/PLAN_REWARD_SCREENS.md C0): from a won fight's killing
    // blow to the moment the Reckoning's wipe starts. Playback only; every
    // time comes from Domain/Rewards/RunUpSchedule.
    //
    // The pieces it extends rather than replaces: the final beat's hit-stop
    // (FightBeatPlayer.HitStopFor), the last enemy's StageDeathFade, the
    // survivors' retro-attached victory stills, the sting and voices that
    // used to play in OnPlaybackFinished, and the AnyEnemyFading wait the
    // darkness and leader-fall endings already use.
    public partial class FightController
    {
        // The warm wash behind the party and the dim over the enemy half
        // (FightScreen.BuildVictoryLight). Alpha is runtime; both start hidden.
        [SerializeField] internal Image victoryWash;
        [SerializeField] internal Image enemyDim;

        // Set once the run-up has run (or been ruled out), so the pass back
        // through OnPlaybackFinished carries on to the Reckoning.
        private bool _runUpEnded;

        // Busy with nothing playing, like _darknessEnding: the stranded-turn
        // rescue must not "recover" it into the Reckoning.
        private bool _runUpEnding;

        private bool _runUpSkipped;

        // Held so a rebind (a new fight on the same controller) can stop a
        // run-up the previous fight left running; left alone it would finish
        // into the new session.
        private Coroutine _runUpRoutine;

        // The hops in flight, stopped by a press or a rebind.
        private readonly List<Coroutine> _hops = new List<Coroutine>();

        // True while a press is swallowed by the run-up.
        public bool RunUpActive => _runUpEnding;

        // A press during the run-up jumps to the wipe and is consumed here:
        // the Reckoning has not opened yet, so it cannot see it.
        internal void SkipRunUp()
        {
            if (_runUpEnding) _runUpSkipped = true;
        }

        private void ResetRunUp()
        {
            if (_runUpRoutine != null) StopCoroutine(_runUpRoutine);
            _runUpRoutine = null;
            StopHops();
            _runUpEnded = false;
            _runUpEnding = false;
            _runUpSkipped = false;
            SetVictoryLight(0f, RunUpSchedule.StyleOf(EncounterClass.Normal));
        }

        // Starts the run-up when this is a won fight that ended on a kill.
        // Survival keeps its own ending (the darkness closing); a loss never
        // gets here. True when it took over; it calls OnPlaybackFinished again.
        private bool BeginRunUp()
        {
            if (_runUpEnded || _session == null || !_session.IsOver || !_session.PlayerWon) return false;
            if (!isActiveAndEnabled) return false;

            var reason = _session.EndReason;
            if (reason != FightEndReason.Defeated && reason != FightEndReason.LeaderFell) return false;

            _runUpEnded = true;
            _runUpEnding = true;
            _runUpSkipped = false;
            _runUpRoutine = StartCoroutine(RunUpRoutine(reason));
            return true;
        }

        private IEnumerator RunUpRoutine(FightEndReason reason)
        {
            float preset = GameSettings.BattleSpeed;
            float pace = FightBeatPlayer.Pace;

            // The leader-fall ending already waited out every body; a kill
            // waits on the last fade, which started when the stop ended.
            RunUpSchedule schedule;
            float t = 0f;
            if (reason == FightEndReason.LeaderFell)
            {
                schedule = RunUpSchedule.Settled(preset);
            }
            else
            {
                float hold = 0f;
                float fade = 0f;
                foreach (var body in enemyDeathFades ?? System.Array.Empty<StageDeathFade>())
                {
                    if (body == null || !body.Fading) continue;
                    hold = Mathf.Max(hold, body.AuthoredHoldSeconds);
                    fade = Mathf.Max(fade, body.AuthoredFadeSeconds);
                }

                float stoppedFor = beatPlayer != null ? beatPlayer.FinalStopWall : 0f;
                schedule = stoppedFor > 0f
                    ? RunUpSchedule.For(_session.EncounterClass, pace, preset, hold, fade)
                    : RunUpSchedule.WithoutStop(_session.EncounterClass, pace, preset, hold, fade);
                t = schedule.StopEnd;
            }

            var survivors = new List<CombatantState>(_session.Encounter.LivingPlayerParty);
            var lifted = new bool[survivors.Count];
            bool stung = false;
            bool released = false;

            while (true)
            {
                if (!_runUpSkipped && Input.GetMouseButtonDown(0)) _runUpSkipped = true;

                if (_runUpSkipped && !schedule.Skipped)
                {
                    schedule = schedule.SkippedAt(t);
                    CompleteRunUp(survivors, lifted);
                }

                if (!stung && t >= schedule.StingAt)
                {
                    stung = true;
                    PlayVictorySting(schedule.VoiceAt - schedule.StingAt);
                }

                if (!schedule.Skipped)
                {
                    // The kill registers: the last enemy sags and, for a
                    // boss, the stage shudders once more.
                    if (!released && reason != FightEndReason.LeaderFell)
                    {
                        released = true;
                        ReleaseTheKill(schedule);
                    }

                    for (int i = 0; i < survivors.Count; i++)
                    {
                        if (lifted[i] || t < schedule.LiftAt(i)) continue;
                        lifted[i] = true;
                        Hop(survivors[i], schedule);
                    }

                    SetVictoryLight(Mathf.Clamp01((t - schedule.WashStart) / Mathf.Max(0.0001f, schedule.WashSecondsWall)),
                        schedule.Look);
                }

                if (schedule.WipeDue(t, AnyEnemyFading())) break;

                yield return null;
                t += Time.deltaTime;
            }

            if (!stung) PlayVictorySting(schedule.VoiceAt - schedule.StingAt);

            // One frame after a press, so the same press cannot reach the
            // screen it just opened.
            if (_runUpSkipped) yield return null;

            _runUpEnding = false;
            _runUpRoutine = null;
            OnPlaybackFinished();
        }

        // The sting marks the kill registering; the voices follow it.
        private void PlayVictorySting(float voiceDelayWall)
        {
            if (_spokeVictory) return;
            _spokeVictory = true;

            SoundController.Play(Sound.VictorySting);
            StartCoroutine(VictoryVoices(Mathf.Max(0f, voiceDelayWall)));
        }

        private IEnumerator VictoryVoices(float delay)
        {
            for (float waited = 0f; waited < delay; waited += Time.deltaTime) yield return null;

            if (_session == null) yield break;
            foreach (var id in _session.VictoryVoiceIds)
            {
                CharacterVoice.Play(id, VoiceLine.Victory, oncePerFight: true);
            }
        }

        // The kill registering: every body still fading sags first, then
        // fades (a normal fight's does not sag), and a boss shudders the
        // stage once more. Stage only; Time.timeScale is never touched.
        private void ReleaseTheKill(RunUpSchedule schedule)
        {
            var look = schedule.Look;
            if (look.Aftershock > 0f) ShakeStage(look.Aftershock);
            if (!look.Collapses) return;

            foreach (var foe in _session.Encounter.Enemies)
            {
                if (foe == null || DeathFadeFor(foe)?.Fading != true) continue;
                AnimatorFor(foe)?.Collapse(look.CollapseYScale, look.CollapseTiltDegrees, schedule.CollapseSecondsWall);
            }
        }

        // A survivor with a victory still hops and lands with a small squash;
        // one without keeps its idle and does not move.
        private void Hop(CombatantState survivor, RunUpSchedule schedule)
        {
            if (survivor == null) return;
            if (StanceAnimationLibrary.Resolve(SpriteFolderFor(survivor), FightSession.Stances.Victory) == null) return;

            // The animator's tweens divide by the fight's pace; the hop is
            // authored in wall seconds, so it is handed over multiplied by it.
            float pace = FightBeatPlayer.Pace;
            float rise = schedule.LiftSecondsWall * RunUpSchedule.LiftRiseFraction * pace;
            float settle = schedule.LiftSecondsWall * (1f - RunUpSchedule.LiftRiseFraction) * pace;
            var animator = AnimatorFor(survivor);
            if (animator == null) return;

            animator.Play(new Vector2(0f, RunUpSchedule.LiftPixels), 0f, rise, 0f, settle);
            _hops.Add(StartCoroutine(Land(animator, schedule)));
        }

        private IEnumerator Land(StageActorAnimator animator, RunUpSchedule schedule)
        {
            for (float t = 0f; t < schedule.LiftSecondsWall; t += Time.deltaTime) yield return null;

            animator.Squash(RunUpSchedule.LandYScale, schedule.LandSecondsWall, schedule.LandSecondsWall);
        }

        private void StopHops()
        {
            foreach (var hop in _hops)
            {
                if (hop != null) StopCoroutine(hop);
            }

            _hops.Clear();
        }

        // A press: the fades complete, the hops are over, the light is full.
        // The sting is not touched.
        private void CompleteRunUp(List<CombatantState> survivors, bool[] lifted)
        {
            StopHops();

            foreach (var body in enemyDeathFades ?? System.Array.Empty<StageDeathFade>())
            {
                if (body != null && body.Fading) body.CompleteNow();
            }

            for (int i = 0; i < survivors.Count; i++)
            {
                if (lifted[i]) AnimatorFor(survivors[i])?.ResetToHome();
            }

            SetVictoryLight(1f, _session.EncounterClass);
        }

        // 0 hides both; 1 is the finished look: the wash at the class's
        // brightness and size, the enemy half dimmed.
        private void SetVictoryLight(float amount, RunUpSchedule.Style look)
        {
            SetAlpha(victoryWash, look.WashAlpha * amount);
            SetAlpha(enemyDim, RunUpSchedule.EnemyDim * amount);
            if (victoryWash != null) victoryWash.rectTransform.localScale = new Vector3(look.WashScale, look.WashScale, 1f);
        }

        private void SetVictoryLight(float amount, EncounterClass fightClass) =>
            SetVictoryLight(amount, RunUpSchedule.StyleOf(fightClass));

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null) return;

            image.gameObject.SetShown(alpha > 0f);
            var colour = image.color;
            colour.a = alpha;
            image.color = colour;
        }
    }
}
