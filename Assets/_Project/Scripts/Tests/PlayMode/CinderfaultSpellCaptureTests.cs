using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // CINDERFAULT, CAST FOR REAL, AGAINST A FULL THREE-ENEMY FORMATION.
    //
    // SpellVfxTests proves WHERE the ground layer and the three eruptions
    // land -- rect sizes and enabled flags, asserted without a pixel. None of
    // that says whether the sequence READS as one connected fault with three
    // things erupting on it, because none of it renders anything. This does,
    // following StaticPilotStageCaptureTests' own recipe: a frame series at
    // BeatSpeedMultiplier 1, sampled at a fixed 1/30s of GAME time via
    // Time.captureDeltaTime (not the wall clock -- writing a PNG costs real
    // milliseconds, and a capture that simply sampled every rendered frame
    // would watch the beat run away from it the harder each frame is to
    // produce), stitched by tools/capture_strip.py into a strip and a GIF.
    //
    // Driven through the REAL click flow (Verb1 SKILL, the submenu row,
    // any enemy plate) rather than FightController.PlaySpellVfxForTest, which
    // SpellVfxTests uses deliberately to isolate the VFX layer from damage
    // resolution. This fixture wants the opposite: the actual skills.json
    // entry, resolved by the actual session, painted by the actual popups --
    // the same seam DamagePopupColorTests already trusts for a single target,
    // extended to three.
    public class CinderfaultSpellCaptureTests
    {
        private const string SkillId = "cinderfault";

        // The sprite SpellCastCaptureTests already picked for placement bugs:
        // wide rather than tall, so a fault's width against a real formation
        // shows against real art instead of three grey plates.
        private const string EnemySpritePath = "Enemies/rat";
        private const int EnemyCount = 3;

        private const float SampleSeconds = 1f / 30f;

        // ~1.4s of game time. The cast itself is authored at 0.78s (docs/
        // handoffs/cinderfault/README.md) with impact at 5/9 of that and a
        // cooling tail past it, plus FightBeatPlayer's own post-impact settle
        // (SettleAfter, floored at 0.16s). 42 frames is
        // StaticPilotStageCaptureTests' own margin for the same shape of
        // window -- one impact, one settle -- and covers this with room to
        // spare for the fault to cool all the way to nothing.
        private const int FrameCount = 42;

        private static string LabelDir => CaptureOutput.LabelDir("cinderfault");

        private FightController _fight;
        private FightBeatPlayer _player;
        private List<CombatantState> _enemies;

        // NORMAL SPEED IS THE WHOLE POINT -- stated rather than inherited,
        // same reasoning as StaticPilotStageCaptureTests.
        [SetUp]
        public void RealTime() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureDeltaTime = 0f;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"'{name}' was not found in the fight scene");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static Canvas RootCanvas() =>
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);

        // Opens the scene, throws away the bootstrap's own fight, and stands
        // one caster opposite a full three-enemy formation.
        //
        // THE HERO GOES FIRST BY SPEED (10 against the enemies' 4), so her
        // cast is the only beat this queues -- a second, enemy-initiated beat
        // beginning mid-capture is exactly the contamination StaticPilotStage
        // CaptureTests' Hold Back trick exists to avoid, and this fixture
        // avoids it by never letting one queue at all rather than draining it
        // away after the fact.
        private IEnumerator StandTheFixtureUp()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _player = UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_player, "the Fight scene has no FightBeatPlayer");

            // The scene's own FightBootstrap has already started a fight and
            // is playing its opening beats. Left running, its playback holds
            // FightController busy and every click below is swallowed.
            _player.Flush();
            yield return null;
            yield return null;

            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == SkillId);
            Assert.IsNotNull(definition, $"'{SkillId}' is not in skills.json");
            var skill = FightEncounterAdapter.Resolve(definition);

            // A PARTY MEMBER WHO HAS THE BOOK. GAP_AUDIT.md's row 3 records
            // that any character can equip Cinderfault -- Shawn's own id,
            // carrying nothing else on the kit, is that claim exercised
            // rather than assumed.
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var kit = new PlayerKit("shawn", CharacterRole.Tank,
                new List<ResolvedSkill> { skill }, null, null, level: 20);

            _enemies = Enumerable.Range(0, EnemyCount)
                .Select(i => new CombatantState($"Rat{i}", false, 5000, 10, 8, 4))
                .ToList();
            var encounter = new CombatEncounter(new[] { hero }, _enemies.ToArray());
            var enemyKits = _enemies
                .Select(e => new EnemyKit(new ResolvedEnemy(e.Name.ToLowerInvariant(), e.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0, spritePath: EnemySpritePath), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
            yield return null;
        }

        // ---- what one sampled frame knows ------------------------------------

        private struct Sample
        {
            public int PopupsBusy;
            public bool GroundEnabled;
        }

        private Sample Read() => new Sample
        {
            PopupsBusy = _player.Popups.Count(p => p != null && !p.IsFree),
            GroundEnabled = _fight.GroundVfxPlayerForTest != null
                && _fight.GroundVfxPlayerForTest.Image != null
                && _fight.GroundVfxPlayerForTest.Image.enabled,
        };

        // Casts Cinderfault through the real menu -- SKILL, its one row, any
        // enemy plate (ResolveDamageAll hits every living opponent regardless
        // of which plate confirmed the cast, per FightController.Input's own
        // OnRowPressed comment) -- then records one Sample per 1/30s for
        // FrameCount samples. onFrame, when given, grabs a pixel copy of the
        // stage for that sample; the behaviour-only test below passes null
        // and pays for none of that.
        //
        // SAMPLED AGAINST THE WALL CLOCK, NOT Time.captureDeltaTime. That
        // pins Time.deltaTime, which is what StaticPilotStageCaptureTests'
        // procedural lunge/flash/recoil reads -- but SpellVfxPlayer.PlayRoutine
        // paces its own frames off Time.realtimeSinceStartup on purpose (its
        // own header: "so a spell cannot outlast the turn that cast it when a
        // test runs the fight at 60x"), which pinning captureDeltaTime does
        // not touch at all. A first version of this fixture pinned it anyway
        // and the ground layer never turned back off inside the window: 42
        // engine frames with no vsync passed in a few milliseconds of REAL
        // time, so the realtime-paced fault had barely begun. BeatSpeedMultiplier
        // is 1 here, so "real time" and "game time" are the same thing this
        // capture wants regardless.
        private IEnumerator TheCast(List<int> impactFrames, Action<int> onFrame, Action<int> onGroundTurnedOff)
        {
            Click("Verb1"); // SKILL -- see FightSubmenuAffordabilityTests for the same convention
            yield return null;

            var row = Named("CharacterSkill0");
            Assert.IsNotNull(row, "the skill submenu did not open its first row -- " +
                                  "Cinderfault should be the only skill on this kit");
            Click("CharacterSkill0");
            yield return null;

            Click("EnemyPlate0");

            float started = Time.realtimeSinceStartup;
            var previous = Read();
            int next = 0;

            while (next < FrameCount)
            {
                if (Time.realtimeSinceStartup - started >= next * SampleSeconds)
                {
                    var sample = Read();
                    if (sample.PopupsBusy > previous.PopupsBusy) impactFrames.Add(next);

                    // THE FAULT TURNING BACK OFF IS THIS BEAT'S OWN ENDING, and
                    // playback is stopped the instant it is seen -- same move
                    // StaticPilotStageCaptureTests makes once its own swing is
                    // at rest. This round fields three enemies, all of them
                    // still standing after one packet cast, and every one of
                    // them gets its own reply beat right behind this one in
                    // the SAME drained batch: left running, a second combatant's
                    // popup rises a beat-gap later and the rest of this window
                    // is that beat, not this one settling. Flushing here is
                    // what makes the remaining samples a genuinely still,
                    // cooled stage rather than the next fight's opening frames.
                    if (previous.GroundEnabled && !sample.GroundEnabled)
                    {
                        onGroundTurnedOff?.Invoke(next);
                        _player.Flush();
                    }

                    previous = sample;

                    onFrame?.Invoke(next);
                    next++;
                }

                yield return null;
            }
        }

        // ---- the assertions, which run in the commit gate ---------------------

        // Everything the capture depends on, checked WITHOUT pixels so the
        // headless suite still covers it -- a capture fixture that self-skips
        // under -nographics and asserts nothing else could rot for weeks with
        // the gate green.
        [UnityTest]
        public IEnumerator TheCastLandsOnAllThreeInOneInstantAndTheFaultCoolsAway()
        {
            yield return StandTheFixtureUp();

            var impactFrames = new List<int>();
            int groundOffFrame = -1;
            yield return TheCast(impactFrames, null, i => { if (groundOffFrame < 0) groundOffFrame = i; });

            Assert.AreEqual(1, impactFrames.Count,
                "all three packets should pop in the same guarded block at the impact instant, " +
                "not a spread of separate rises. Frames: " + string.Join(", ", impactFrames));

            Assert.IsTrue(_enemies.All(e => e.IsAlive),
                "an enemy died mid-capture, so the strip would show a corpse rather than three hits");

            Assert.Greater(groundOffFrame, 0,
                "the ground fault never turned back off inside the window -- it did not cool away");
        }

        // ---- the picture ------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureCinderfaultAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 " +
                              "-Filter PrincesPalace.PlayModeTests.CinderfaultSpellCaptureTests");
                yield break;
            }

            yield return StandTheFixtureUp();

            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");

            var rig = new StageCaptureRig(canvas,
                (int)FightStageAnchors.StageSize.X, (int)FightStageAnchors.StageSize.Y);
            var frames = new List<Texture2D>(FrameCount);
            var impactFrames = new List<int>();
            int groundOffFrame = -1;

            try
            {
                // Read back, do not encode -- EncodeToPNG is the expensive
                // half, and doing it per frame stretches the wall clock the
                // beat's own unscaled-time reactions still run against. The
                // frames are held and written once the window is over.
                yield return TheCast(impactFrames, i => frames.Add(rig.Grab()),
                    i => { if (groundOffFrame < 0) groundOffFrame = i; });
            }
            finally
            {
                rig.Restore();
            }

            if (Directory.Exists(LabelDir)) Directory.Delete(LabelDir, recursive: true);
            Directory.CreateDirectory(LabelDir);

            for (int i = 0; i < frames.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(LabelDir, $"f{i}.png"), frames[i].EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frames[i]);
            }

            int impact = impactFrames.Count > 0 ? impactFrames[0] : -1;
            File.WriteAllText(Path.Combine(LabelDir, "timing.json"), TimingJson(impact, groundOffFrame, rig));
            Debug.Log($"[CinderfaultCapture] wrote {frames.Count} frames to {LabelDir}");

            Assert.AreEqual(1, impactFrames.Count, "expected one shared impact frame for all three targets");
            Assert.Greater(groundOffFrame, 0, "the fault never cooled away inside the window");
        }

        private string TimingJson(int impact, int settled, StageCaptureRig rig)
        {
            string Ms(int frame) => frame < 0
                ? "null"
                : (frame * SampleSeconds * 1000f).ToString("F1", CultureInfo.InvariantCulture);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"skillId\": \"{SkillId}\",");
            json.AppendLine($"  \"enemyCount\": {EnemyCount},");
            json.AppendLine($"  \"beatSpeedMultiplier\": " +
                             $"{FightBeatPlayer.BeatSpeedMultiplier.ToString("F2", CultureInfo.InvariantCulture)},");
            json.AppendLine($"  \"captureIntervalMs\": " +
                             $"{(SampleSeconds * 1000f).ToString("F3", CultureInfo.InvariantCulture)},");
            json.AppendLine($"  \"impactFrame\": {impact},");
            json.AppendLine($"  \"impactMs\": {Ms(impact)},");
            json.AppendLine($"  \"settledFrame\": {settled},");
            json.AppendLine($"  \"settledMs\": {Ms(settled)},");
            json.AppendLine($"  \"cropX\": {rig.CropX}, \"cropY\": {rig.CropY},");
            json.AppendLine($"  \"cropWidth\": {rig.CropWidth}, \"cropHeight\": {rig.CropHeight}");
            json.Append("}");
            return json.ToString();
        }
    }
}
