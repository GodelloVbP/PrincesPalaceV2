using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // THE ATTACK MOTION, PHOTOGRAPHED AS A FRAME SERIES: a Lunge (travel and
    // strike in one motion) against a Close (travel completes, then the strike
    // plays at the target), for a party member's plain attack and an enemy's.
    //
    // Both approaches are played by the real FightBeatPlayer at normal speed
    // on a pinned 1/30s capture clock, so the two series are frame-aligned and
    // the order of travel and strike is readable off the strip. Frames land in
    // tools/screenshots/runtime/attack_motion/<PP_CAPTURE_LABEL>/<subject>_<approach>/f{i}.png
    // with the stance each frame wore and the actor's distance from its mark in
    // frames.txt.
    //
    // The commit gate runs headless, where there is nothing to photograph; the
    // assertion that holds without pixels is that a Close is still travelling
    // after the Lunge of the same beat has finished crossing -- travel then
    // strike -- and that both end back on their mark.
    public class AttackMotionCaptureTests
    {
        private const ulong Seed = 20260904UL;
        private const float SampleSeconds = 1f / 30f;
        private const int MaxFrames = 120;

        private FightController _fight;
        private string _root;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private static bool _outputDirReady;

        [OneTimeSetUp]
        public static void ResetOutputDirFlag() => _outputDirReady = false;

        [SetUp]
        public void UseAThrowawaySaveRootWithARealSquad()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 0f;

            _root = Path.Combine(Path.GetTempPath(), "pp-attackmotion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            SaveData.TestSquadOfThreeEnabled = true;
            RunManager.StartRun(Seed);
        }

        [TearDown]
        public void Restore()
        {
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();

            Time.captureDeltaTime = 0f;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private static Canvas RootCanvas() =>
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);

        private sealed class Stand
        {
            public string Combatant;
            public RectTransform Slot;
            public StageActorAnimator Animator;
            public Image Sprite;
            public string Folder;
        }

        private Stand StandAt(string prefix, int index, string folder)
        {
            var go = Named($"{prefix}{index}Slot");
            Assert.IsNotNull(go, $"no {prefix}{index}Slot");
            return new Stand
            {
                Combatant = Named($"{prefix}{index}Nameplate")?.GetComponent<TMPro.TMP_Text>()?.text,
                Slot = go.GetComponent<RectTransform>(),
                Animator = go.GetComponent<StageActorAnimator>(),
                Sprite = Named($"{prefix}{index}Sprite")?.GetComponent<Image>(),
                Folder = folder,
            };
        }

        private IEnumerator OpenTheScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var scenePlayer = UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>();
            if (scenePlayer != null) scenePlayer.Flush();
            yield return null;
            yield return null;
        }

        private FightBeatPlayer PlayerDriving(Stand actor, Stand target,
                                              CombatantState actorState, CombatantState targetState)
        {
            var go = new GameObject("AttackMotionPlayer");
            _spawned.Add(go);
            var player = go.AddComponent<FightBeatPlayer>();

            player.WireStageForTest(
                c => ReferenceEquals(c, actorState) ? actor.Slot
                    : ReferenceEquals(c, targetState) ? target.Slot : null,
                c => ReferenceEquals(c, actorState) ? actor.Animator
                    : ReferenceEquals(c, targetState) ? target.Animator : null);

            player.WireStancesForTest(
                (c, stance) =>
                {
                    var stand = ReferenceEquals(c, actorState) ? actor
                        : ReferenceEquals(c, targetState) ? target : null;
                    if (stand?.Sprite == null) return;

                    var art = StanceAnimationLibrary.Resolve(stand.Folder, stance);
                    if (art != null) stand.Sprite.sprite = art;
                },
                (_, __) => { }, () => { });

            return player;
        }

        private static CombatBeat Beat(CombatantState actor, CombatantState target, StageApproach approach)
        {
            var beat = new CombatBeat { Actor = actor, Target = target, Amount = 12, Approach = approach };
            beat.Stances[actor] = FightSession.Stances.Attack;
            return beat;
        }

        private sealed class Series
        {
            public string Name;
            public readonly List<Texture2D> Frames = new List<Texture2D>();
            public readonly List<string> Lines = new List<string>();
            public float PeakTravel;
            public int FramesToPeak = -1;
            public int FramesToStrikePose = -1;
            public int LastMovingFrame = -1;
            public Vector2 EndOffset;
        }

        // One beat, played to its end and a little past, one frame per sample.
        private IEnumerator Play(string name, Stand actor, Stand target,
                                 CombatantState actorState, CombatantState targetState,
                                 StageApproach approach, StageCaptureRig rig, Series series)
        {
            series.Name = name;

            // Both figures at rest first, so the series opens on a still stage.
            float restDeadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < restDeadline
                   && (target.Animator.IsPlaying || actor.Animator.IsPlaying
                       || Vector2.Distance(target.Slot.anchoredPosition, target.Animator.Mark) > 0.01f
                       || Vector2.Distance(actor.Slot.anchoredPosition, actor.Animator.Mark) > 0.01f))
            {
                yield return null;
            }

            var idle = StanceAnimationLibrary.Resolve(actor.Folder, FightSession.Stances.Idle);
            if (idle != null) actor.Sprite.sprite = idle;

            var player = PlayerDriving(actor, target, actorState, targetState);
            var mark = actor.Animator.Mark;
            string strikeName = StanceAnimationLibrary.Resolve(actor.Folder, FightSession.Stances.Attack)?.name;

            bool finished = false;
            Time.captureDeltaTime = SampleSeconds;
            player.Play(new[] { Beat(actorState, targetState, approach) }, () => finished = true);

            int afterFinish = 0;
            for (int i = 0; i < MaxFrames; i++)
            {
                yield return null;

                float travel = Vector2.Distance(actor.Slot.anchoredPosition, mark);
                string wearing = actor.Sprite.sprite != null ? actor.Sprite.sprite.name : "";

                if (travel > series.PeakTravel + 0.5f)
                {
                    series.PeakTravel = travel;
                    series.FramesToPeak = i;
                }

                if (travel > 1f) series.LastMovingFrame = i;
                if (series.FramesToStrikePose < 0 && wearing == strikeName) series.FramesToStrikePose = i;

                series.Lines.Add($"f{i} t={i * SampleSeconds * 1000f:F0}ms travel={travel:F1} wearing={wearing}");
                if (rig != null) series.Frames.Add(rig.Grab());

                if (finished && ++afterFinish > 12) break;
            }

            Time.captureDeltaTime = 0f;
            series.EndOffset = actor.Slot.anchoredPosition - mark;
            player.Flush();
        }

        private static void Write(string dir, Series s)
        {
            string sub = Path.Combine(dir, s.Name);
            Directory.CreateDirectory(sub);
            for (int i = 0; i < s.Frames.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(sub, $"f{i}.png"), s.Frames[i].EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(s.Frames[i]);
            }

            File.WriteAllText(Path.Combine(sub, "frames.txt"), string.Join("\n", s.Lines));
        }

        [UnityTest]
        public IEnumerator ACloseStrikesAfterTheTravelAndALungeStrikesDuringIt()
        {
            yield return OpenTheScene();

            var shawn = StandAt("Party", 0, "Characters/sheep");
            var enemy = StandAt("Enemy", 0, null);
            enemy.Folder = enemy.Combatant == "Giant Rat" ? "Enemies/rat"
                : enemy.Combatant == "Ironback Beetle" ? "Enemies/beetle"
                : enemy.Combatant == "Forest Troll" ? "Enemies/forest_warden" : null;
            Assert.IsNotNull(enemy.Folder, $"no art folder known for enemy '{enemy.Combatant}'");
            var shawnState = _fight.Session.Encounter.PlayerParty.First(c => c.Name == shawn.Combatant);
            var enemyState = _fight.Session.Encounter.Enemies[0];

            var canvas = RootCanvas();
            bool photograph = CanvasCapture.IsSupported && canvas != null;
            StageCaptureRig rig = photograph
                ? new StageCaptureRig(canvas, (int)FightStageAnchors.StageSize.X, (int)FightStageAnchors.StageSize.Y)
                : null;

            string dir = CaptureOutput.LabelDir("attack_motion");
            if (photograph && !_outputDirReady)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                _outputDirReady = true;
            }

            var lunge = new Series();
            var close = new Series();
            var enemyLunge = new Series();
            var enemyClose = new Series();

            try
            {
                yield return Play("pc_attack_lunge", shawn, enemy, shawnState, enemyState, StageApproach.Lunge, rig, lunge);
                yield return Play("pc_attack_close", shawn, enemy, shawnState, enemyState, StageApproach.Close, rig, close);
                yield return Play("enemy_attack_lunge", enemy, shawn, enemyState, shawnState, StageApproach.Lunge, rig, enemyLunge);
                yield return Play("enemy_attack_close", enemy, shawn, enemyState, shawnState, StageApproach.Close, rig, enemyClose);
            }
            finally
            {
                rig?.Restore();
            }

            if (photograph)
            {
                Write(dir, lunge);
                Write(dir, close);
                Write(dir, enemyLunge);
                Write(dir, enemyClose);
            }

            Assert.Greater(lunge.PeakTravel, 5f, "the lunge never left its mark");
            Assert.Greater(close.PeakTravel, 5f, "the close never left its mark");

            // TRAVEL THEN STRIKE: a Close wears no strike pose until it has
            // arrived, so the strike pose shows up at least a handful of frames
            // after the travel began; a Lunge (strike pose from the opening
            // frame) shows it on frame 0.
            Assert.GreaterOrEqual(close.FramesToStrikePose, 3,
                "the close put its strike pose on before the figure had finished jumping in. " + string.Join(" | ", close.Lines.Take(12)));
            Assert.LessOrEqual(lunge.FramesToStrikePose, 1,
                "the lunge no longer wears its strike from the opening frame, so this fixture is not showing the before");

            Assert.GreaterOrEqual(enemyClose.FramesToStrikePose, 3,
                "the enemy's close put its strike pose on before it had jumped in. " + string.Join(" | ", enemyClose.Lines.Take(12)));
            Assert.Less(Mathf.Abs(enemyLunge.EndOffset.x) + Mathf.Abs(enemyClose.EndOffset.x), 0.5f, "an enemy swing did not come home");

            Assert.Less(Mathf.Abs(lunge.EndOffset.x) + Mathf.Abs(lunge.EndOffset.y), 0.5f, "the lunge did not come home");
            Assert.Less(Mathf.Abs(close.EndOffset.x) + Mathf.Abs(close.EndOffset.y), 0.5f, "the close did not come home");
        }

        // ODETTE'S RING STAYS ON THE FLOOR while her hover bobs and the stage repaints.
        [UnityTest]
        public IEnumerator AFlyersFootRingHoldsOneHeightOverAHoverCycle()
        {
            yield return OpenTheScene();
            FightController.BreathSpeedMultiplier = 1f;

            Stand odette = null;
            for (int i = 0; i < 3 && odette == null; i++)
            {
                var s = StandAt("Party", i, "Characters/owl");
                if (s.Combatant == "Odette") odette = s;
            }
            Assert.IsNotNull(odette, "Odette is not in the party");

            var shadow = (RectTransform)odette.Slot.GetChild(0);
            var canvas = RootCanvas();
            bool photograph = CanvasCapture.IsSupported && canvas != null;
            var rig = photograph
                ? new StageCaptureRig(canvas, (int)FightStageAnchors.StageSize.X, (int)FightStageAnchors.StageSize.Y)
                : null;
            string dir = Path.Combine(CaptureOutput.LabelDir("attack_motion"), "odette_ring");
            if (photograph) { if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir); }

            float min = float.MaxValue, max = float.MinValue, bodyMin = float.MaxValue, bodyMax = float.MinValue;
            var lines = new List<string>();
            Time.captureDeltaTime = SampleSeconds;
            try
            {
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                    float ring = shadow.position.y, body = odette.Sprite.rectTransform.position.y;
                    min = Mathf.Min(min, ring); max = Mathf.Max(max, ring);
                    bodyMin = Mathf.Min(bodyMin, body); bodyMax = Mathf.Max(bodyMax, body);
                    lines.Add($"f{i} ring={ring:F1} body={body:F1}");
                    if (rig != null && i % 3 == 0)
                    {
                        var tex = rig.Grab();
                        File.WriteAllBytes(Path.Combine(dir, $"f{i}.png"), tex.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                rig?.Restore();
            }

            if (photograph) File.WriteAllText(Path.Combine(dir, "frames.txt"), string.Join(System.Environment.NewLine, lines));
            Assert.Greater(bodyMax - bodyMin, 1f, "Odette is not hovering, so this proves nothing");
            Assert.Less(max - min, 1.5f, $"the ring moved {max - min:F1}px while she hovered: " + string.Join(" | ", lines.Take(10)));
        }
    }
}
