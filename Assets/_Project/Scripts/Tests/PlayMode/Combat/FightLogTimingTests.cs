using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;
using Object = UnityEngine.Object;

namespace PrincesPalace.PlayModeTests
{
    // THE LOG SAYS "FOR 10!" WHEN THE 10 LANDS, NOT WHEN THE BUTTON IS PRESSED.
    //
    // The bug (qa-2026-09-26, spell_crownfall_vs_treant_*.png): "Shawn casts
    // Crownfall on Treant for 10!" was already in the log on the preview's
    // _before frame, with the spike still overhead and the treant unhurt.
    // FightBeatPlayer pushed a beat's lines when the beat OPENED, and a beat's
    // lines are whole sentences the session wrote after resolving it -- so the
    // damage was read a whole spell's travel before it was shown.
    //
    // Pinned on the beat's own clock (Time.timeAsDouble) at a fixed frame
    // length, the same technique FightBeatPacingTests uses for the charge's
    // travel, and against LITERAL times rather than the player's constants.
    public class FightLogTimingTests
    {
        // A spell's impact lead as FightController would hand it over for a
        // cast whose hit frame sits half a second in. Literal.
        private const float SpellImpactSeconds = 0.5f;

        // StaticSwing's anticipation plus its outbound tween. Literal, and the
        // same number FightBeatPacingTests pins on its own.
        private const float SwingSeconds = 0.125f;

        private const float SampleSeconds = 0.01f;
        private const float TimingSlop = 0.005f;
        private const int FrameBudget = 500;

        private const string CastLine = "Shawn casts Crownfall on Treant for 10!";
        private const string SwingLine = "Shawn attacks Treant for 7 damage!";

        [UnityTest]
        public IEnumerator ASpellsLineReachesTheLogAtItsImpactNotAtThePress()
        {
            var player = NewPlayer();

            var pushed = new List<(string line, double at)>();
            player.WireLogForTest(line => pushed.Add((line, Time.timeAsDouble)),
                                  b => b.Approach == StageApproach.Hold ? SpellImpactSeconds : 0f);

            var beat = Beat(StageApproach.Hold, 10, CastLine);

            Time.captureDeltaTime = SampleSeconds;
            double start = Time.timeAsDouble;
            bool finished = false;
            player.Play(new List<CombatBeat> { beat }, () => finished = true);

            // A quarter of a second in: the spell is still in the air.
            int frames = 0;
            while (Time.timeAsDouble - start < 0.25 && frames++ < FrameBudget) yield return null;
            Assert.AreEqual(0, pushed.Count,
                "the cast's line was in the log " + (Time.timeAsDouble - start).ToString("F3") +
                "s into a beat whose blow lands at " + SpellImpactSeconds + "s -- the damage was " +
                "announced before it happened");

            while (!finished && frames++ < FrameBudget) yield return null;
            Assert.IsTrue(finished, "the beat never finished");

            Assert.AreEqual(1, pushed.Count, "the line was pushed " + pushed.Count + " times, not once");
            Assert.AreEqual(CastLine, pushed[0].line);
            Assert.GreaterOrEqual(pushed[0].at - start, SpellImpactSeconds - TimingSlop,
                "the line reached the log before the spell's impact instant");
            Assert.Less(pushed[0].at - start, SpellImpactSeconds + 0.05,
                "the line reached the log well after the impact instant -- it should land with the number");
        }

        // EVERY ATTACK, NOT JUST SPELLS: a lunge's line lands on the very
        // frame its contact effect fires, which is the impact frame.
        [UnityTest]
        public IEnumerator ASwingsLineReachesTheLogOnTheFrameTheBlowLands()
        {
            var player = NewPlayer();

            int lineFrame = -1;
            double lineAt = -1.0;
            player.WireLogForTest(line => { lineFrame = Time.frameCount; lineAt = Time.timeAsDouble; },
                                  _ => 0f);

            int contactFrame = -1;
            player.WireContactFxForTest(_ => { if (contactFrame < 0) contactFrame = Time.frameCount; });

            var beat = Beat(StageApproach.Lunge, 7, SwingLine);

            Time.captureDeltaTime = SampleSeconds;
            double start = Time.timeAsDouble;
            bool finished = false;
            player.Play(new List<CombatBeat> { beat }, () => finished = true);

            int frames = 0;
            while (!finished && frames++ < FrameBudget) yield return null;
            Assert.IsTrue(finished, "the beat never finished");

            Assert.Greater(contactFrame, 0, "the swing never fired its contact effect, so there is no impact to compare to");
            Assert.AreEqual(contactFrame, lineFrame,
                "the swing's line and its contact effect landed on different frames");
            Assert.GreaterOrEqual(lineAt - start, SwingSeconds - TimingSlop,
                "the swing's line reached the log while the attacker was still crossing");
        }

        // ---- fixture -------------------------------------------------------------

        private static CombatantState Fighter(string name, bool playerSide) =>
            new CombatantState(name, playerSide, 30, 10, 5, 5);

        private static CombatBeat Beat(StageApproach approach, int amount, string line)
        {
            var beat = new CombatBeat
            {
                Actor = Fighter("Shawn", true),
                Target = Fighter("Treant", false),
                Amount = amount,
                Approach = approach,
            };
            beat.Stances[beat.Actor] = FightSession.Stances.Attack;
            beat.Messages.Add(line);
            return beat;
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private FightBeatPlayer NewPlayer()
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);
            var player = go.AddComponent<FightBeatPlayer>();

            // Real speed: the waits under test are fractions of a second, and
            // scaling them down would put them under one frame's length.
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            return player;
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureDeltaTime = 0f;
        }
    }
}
