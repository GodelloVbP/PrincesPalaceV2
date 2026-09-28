using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The Options pane actually drives GameSettings.
    //
    // The binding is by KEY, and a key with no case behind it throws rather
    // than silently doing nothing -- which is right, but only ever fires when
    // a player opens the pane. This walks every row so the build finds it
    // instead.
    public class OptionsPaneTests
    {
        private OptionsController _options;
        private float _savedBattleSpeed;

        // BattleSpeed is the one GameSettings value on this pane with a
        // production-timing consequence once FightBootstrap.Start reads it --
        // every OTHER row this file pokes
        // (sound, fps, window, resolution) is inert outside a real device, so
        // this is the first setting here that actually needs the capture/
        // restore pattern GameSettingsTests already established, rather than
        // being left wherever the last case in this file put it.
        [SetUp]
        public void SaveBattleSpeed() => _savedBattleSpeed = GameSettings.BattleSpeed;

        [TearDown]
        public void RestoreBattleSpeed() => GameSettings.SetBattleSpeed(_savedBattleSpeed);

        private IEnumerator OpenOptions()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(SystemMenuTab.Options);
            yield return null;

            _options = Object.FindAnyObjectByType<OptionsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_options, "the Options pane has no controller");
        }

        // The one that catches a row added to OptionRows with nothing behind
        // it. Refresh reads every binding, so if any key is unhandled this
        // throws where a build can see it.
        [UnityTest]
        public IEnumerator EveryRowResolvesToASetting()
        {
            yield return OpenOptions();

            Assert.DoesNotThrow(() => _options.Refresh(),
                "a row in OptionRows has no GameSettings binding behind it");
        }

        [UnityTest]
        public IEnumerator RestoringDefaultsPutsEverySettingBack()
        {
            yield return OpenOptions();

            GameSettings.SetSoundVolume(0.11f);
            GameSettings.SetMusicVolume(0.22f);
            GameSettings.SetFpsLimitIndex(GameSettings.FpsLimits.Length - 1);
            GameSettings.SetWindowModeIndex(0);
            GameSettings.SetBattleSpeed(2f);

            _options.RestoreDefaults();

            Assert.AreEqual(GameSettings.DefaultVolume, GameSettings.SoundVolume, 0.001f);
            Assert.AreEqual(GameSettings.DefaultVolume, GameSettings.MusicVolume, 0.001f);
            Assert.AreEqual(GameSettings.DefaultFpsLimitIndex, GameSettings.FpsLimitIndex);
            Assert.AreEqual(GameSettings.DefaultWindowModeIndex, GameSettings.WindowModeIndex);
            Assert.AreEqual(BattleSpeed.DefaultDisplay, GameSettings.BattleSpeed, 0f);
        }

        // Ends are ends. Wrapping would drop a player from 4K to 1280x720 on one
        // click past the top, which on a stepper they are holding is the kind of
        // surprise that costs a screen resolution and a temper.
        [UnityTest]
        public IEnumerator SteppersClampRatherThanWrap()
        {
            yield return OpenOptions();

            GameSettings.SetResolutionIndex(GameSettings.Resolutions.Length - 1);
            GameSettings.SetResolutionIndex(GameSettings.Resolutions.Length);
            Assert.AreEqual(GameSettings.Resolutions.Length - 1, GameSettings.ResolutionIndex,
                "stepping past the largest resolution wrapped to the smallest");

            GameSettings.SetResolutionIndex(-1);
            Assert.AreEqual(0, GameSettings.ResolutionIndex,
                "stepping below the smallest resolution wrapped to the largest");

            // T3: the battle-speed row is a
            // stepper too, so it must clamp at both ends the same way.
            GameSettings.SetBattleSpeed(BattleSpeed.Rows[BattleSpeed.Rows.Count - 1].Display);
            _options.Step("battlespeed", +1);
            Assert.AreEqual(BattleSpeed.Rows[BattleSpeed.Rows.Count - 1].Display, GameSettings.BattleSpeed, 0f,
                "stepping past the fastest preset wrapped to the slowest");

            GameSettings.SetBattleSpeed(BattleSpeed.Rows[0].Display);
            _options.Step("battlespeed", -1);
            Assert.AreEqual(BattleSpeed.Rows[0].Display, GameSettings.BattleSpeed, 0f,
                "stepping below the slowest preset wrapped to the fastest");
        }

        // Every group the pane draws has to bind to something. The design's own
        // rule -- a control that stores nothing is worse than an absent one,
        // because the player moves it, believes something changed, and is wrong.
        [Test]
        public void NoGroupIsEmptyAndNoRowIsUnkeyed()
        {
            foreach (var group in OptionRows.Groups)
            {
                Assert.Greater(group.Rows.Count, 0, $"group '{group.Key}' draws a card with no rows in it");

                foreach (var row in group.Rows)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Key),
                        $"a row in '{group.Key}' has no key, so nothing can bind it");
                }
            }

            CollectionAssert.AllItemsAreUnique(OptionRows.AllRows.Select(r => r.Key).ToList(),
                "two option rows share a key, so they would drive the same setting");
        }

        [Test]
        public void TheCardsFitWithoutScrolling()
        {
            Assert.IsTrue(OptionsLayout.CardsFit(OptionRows.Groups),
                $"column 0 needs {OptionsLayout.ColumnHeight(OptionRows.Groups, 0)}px and column 1 needs " +
                $"{OptionsLayout.ColumnHeight(OptionRows.Groups, 1)}px of {OptionsLayout.UsableHeight}px");
        }
    }
}
