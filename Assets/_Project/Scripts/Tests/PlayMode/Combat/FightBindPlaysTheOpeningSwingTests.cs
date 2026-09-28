using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // THE FIX FOR "a faster enemy's opening swing is invisible until the
    // player acts, then plays bundled with the player's own action."
    //
    // FightSession.Begin() resolves a faster enemy's opening swing into the
    // beat queue before the player ever gets a turn (RelicsOnCombatBegin,
    // GrantTurnStart, AutoResolveEnemyTurns, AutoResolveEggTurns -- all ahead
    // of turn one). Nothing used to drain that queue until the player's own
    // first action reached AfterResolution, so the swing played silently,
    // then reappeared stitched onto whatever the player did next.
    //
    // FightController.Bind now drains and plays it itself, held behind
    // IsBusy until it settles, before input opens. This is that fix, proven
    // two ways: the beat is gone from the session's queue as soon as Bind
    // returns (so the player's own first DrainBeats -- AfterResolution's, the
    // same call this test makes directly -- can never bundle it in), and
    // IsBusy is true for the frames the swing takes to play, then clears on
    // its own once it is done.
    public class FightBindPlaysTheOpeningSwingTests
    {
        // PINNED, not left to whatever the previous test in the run left
        // behind -- BeatSpeedMultiplier and PlayerSpeedSource are statics
        // (FightBeatPlayer.cs) shared across the whole PlayMode suite, and a
        // fixture that leaves either at a slow-motion value would make this
        // test's own bounded wait below flaky rather than wrong. 8x is fast
        // enough that the swing settles in well under the frame bound while
        // still being a real, multi-frame animation rather than an instant.
        [SetUp]
        public void FastAndPinned()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 8f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
        }

        [UnityTest]
        public IEnumerator AnEnemyThatWinsInitiative_HasItsOpeningSwingPlayedOnBind()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player, "the Fight scene has no FightBeatPlayer");

            // The scene's own FightBootstrap already bound and is playing a
            // fight of its own. Superseded rather than finished, so nothing
            // calls its onFinished -- which is exactly why Bind resets
            // IsBusy itself now rather than trusting it, and why this test
            // does not assert anything about IsBusy until AFTER its own Bind.
            player.Flush();

            // Speed 50 against the hero's 10: the enemy holds turn one and
            // FightSession.Begin resolves its swing before the player gets a
            // turn at all.
            var hero = new CombatantState("Hero", true, 500, 20, 20, 10);
            var fast = new CombatantState("Fast", false, 200, 20, 15, 50);

            var session = new FightSession(
                new CombatEncounter(new[] { hero }, new[] { fast }), null, null, new SeededRandom(3));

            Assert.IsFalse(session.IsPlayerTurn, "fixture check: the enemy must hold turn one");

            session.Begin();

            // ASSERTED, not assumed: if this fails the enemy never swung and
            // the rest of the test would be photographing an empty stage.
            Assert.Less(hero.CurrentHealth, hero.MaxHealth,
                "the enemy's opening swing did not resolve, so there is no beat for Bind to drain");

            fight.Bind(session, EncounterClass.Normal);

            // THE CORE CLAIM: the beat Begin() queued is gone from the
            // session as soon as Bind returns. This is the exact call
            // AfterResolution makes when the player's first action resolves
            // -- an empty drain here is what stops that action from ever
            // bundling the enemy's opening swing underneath its own.
            Assert.AreEqual(0, session.DrainBeats().Count,
                "the enemy's opening beat is still queued after Bind, so the player's first action " +
                "would still drain and play it bundled with their own");

            // HELD CLOSED while it plays. Bind found a beat to play and a
            // real FightBeatPlayer to play it through, so it must have gone
            // busy rather than opening input immediately.
            Assert.IsTrue(fight.IsBusy,
                "Bind found an opening beat to play but never went busy, so nothing is holding input " +
                "closed while it plays");

            // AND CLEARS ON ITS OWN, within a generous real-time bound (not a
            // frame count -- how long a frame takes is not this test's
            // concern). No further input is given -- if this fires, the
            // fix's own completion path (OnPlaybackFinished) never ran.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(fight.IsBusy,
                "the opening swing never finished playing within 10 real seconds, so input never opened");

            Assert.IsTrue(session.IsPlayerTurn || session.IsOver,
                "the swing finished but the turn was not handed back to the player");
        }
    }
}
