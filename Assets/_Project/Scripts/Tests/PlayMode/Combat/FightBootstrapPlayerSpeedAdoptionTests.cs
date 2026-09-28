using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // FightBootstrap.Start INSTALLS FightBeatPlayer.PlayerSpeedSource but,
    // before this fix, never re-adopted -- so PlayerSpeedMultiplier stayed
    // whatever FightBeatPlayer.OnEnable last saw, which races AHEAD of
    // Start on a fresh scene load (Unity runs every OnEnable before any
    // Start) and reads the STALE source: the static default, or whatever
    // the previous fight installed. fight.Bind, called right after Start
    // installs the production source, paints the first status-badge pop off
    // that stale multiplier (FightController.Hud.cs's
    // FightBeatPlayer.Scaled(StatusBadgePopSeconds) call).
    //
    // Display 2 is the fastest preset -- BattleSpeed.Preset.Multiplier is
    // Display / TodaysPaceDisplay (1.5), so 2 / 1.5 = 4/3 exactly, and it is
    // never 1 by coincidence the way a slower preset's rounding could be.
    public class FightBootstrapPlayerSpeedAdoptionTests
    {
        private float _savedBattleSpeed;

        [SetUp]
        public void SaveBattleSpeed() => _savedBattleSpeed = GameSettings.BattleSpeed;

        [TearDown]
        public void Restore()
        {
            GameSettings.SetBattleSpeed(_savedBattleSpeed);
            TestGlobals.ResetAll();
        }

        [UnityTest]
        public IEnumerator TheBootstrapInstalledSourceIsAdoptedBeforeTheFirstBeat()
        {
            // THE STALE STATE OnEnable WILL CATCH: whatever a previous fight
            // (or the static default) left behind, standing in for "the
            // multiplier before this scene's own bootstrap ever ran".
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            GameSettings.SetBattleSpeed(2f);

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            // Two settle frames: OnEnable fires synchronously with the scene
            // load, Start (FightBootstrap's own) one frame later --
            // .claude/rules/tests.md.
            yield return null;
            yield return null;

            float expected = BattleSpeed.Nearest(2f).Multiplier;
            Assert.AreEqual(4f / 3f, expected, 1e-5f,
                "fixture: display 2 no longer resolves to 4/3 -- the preset table moved");

            Assert.AreEqual(expected, FightBeatPlayer.PlayerSpeedMultiplier, 1e-5f,
                "PlayerSpeedMultiplier still reads the stale OnEnable-time source -- FightBootstrap.Start " +
                "installed its own source but never re-adopted before Bind painted the first badge pop");
        }

        // FightBeatPlayer.OnEnable no longer adopts at all (item F) -- the
        // bootstrap's own re-adopt above is the only one a fresh fight gets.
        // This pins the ordering that makes deleting OnEnable's call safe:
        // a sentinel adopted before the scene loads survives untouched
        // through the load itself (every OnEnable in it has fired -- Unity
        // runs OnEnable before any Start -- and none of them wrote to it),
        // and only jumps to the bootstrap's own source once Start actually
        // runs.
        [UnityTest]
        public IEnumerator TheMultiplierIsUntouchedThroughOnEnableThenAdoptedAtBootstrapStart()
        {
            // A SENTINEL distinct from both the static default (1f) and
            // whatever this scene's own bootstrap will install (4/3 at
            // display 2, per this fixture's own header), so a read that
            // happens to land on either of those by coincidence cannot be
            // mistaken for "something adopted on OnEnable again".
            FightBeatPlayer.PlayerSpeedSource = () => 2f;
            FightBeatPlayer.AdoptPlayerSpeed();
            Assert.AreEqual(2f, FightBeatPlayer.PlayerSpeedMultiplier, 1e-5f,
                "fixture: sentinel adoption did not take");

            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            GameSettings.SetBattleSpeed(2f);

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);

            // ZERO SETTLE FRAMES: OnEnable fires synchronously with the
            // scene load, Start (FightBootstrap's own) one frame later --
            // .claude/rules/tests.md -- so this is the value as it stands
            // the instant every OnEnable in the load has run and no Start
            // has. The sentinel must still be here.
            Assert.AreEqual(2f, FightBeatPlayer.PlayerSpeedMultiplier, 1e-5f,
                "PlayerSpeedMultiplier moved before FightBootstrap.Start ran -- something is adopting on " +
                "OnEnable again");

            yield return null;
            yield return null;

            float expected = BattleSpeed.Nearest(2f).Multiplier;
            Assert.AreEqual(expected, FightBeatPlayer.PlayerSpeedMultiplier, 1e-5f,
                "the bootstrap's own re-adopt did not take before Bind painted the first badge pop");
        }
    }
}
