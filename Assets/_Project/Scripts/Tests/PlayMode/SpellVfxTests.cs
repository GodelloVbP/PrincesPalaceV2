using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Where a spell's frames actually land.
    //
    // Almost all of this is one question -- where is the BOTTOM of the effect --
    // and each answer below was found by an effect erupting somewhere
    // anatomically wrong. None of it had a test in v1; the bugs were found by
    // looking at the screen and the fixes were verified the same way.
    public class SpellVfxTests
    {
        private FightController _fight;
        private SpellVfxPlayer _player;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        // A sheet of a given aspect with a given transparent margin along the
        // bottom, in frame-height fractions. The two things the correction is
        // built out of, made explicit.
        private static Sprite Frame(int width, int height, float bottomMarginFraction)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            int margin = Mathf.RoundToInt(height * bottomMarginFraction);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, y < margin ? new Color(0f, 0f, 0f, 0f) : Color.white);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private static Sprite BlankFrame(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, clear);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        // ---- the content-margin correction --------------------------------------

        [Test]
        public void ArtThatReachesItsOwnFloorNeedsNoCorrection()
        {
            // Shawn's two spells fill their canvas exactly, which is why this bug
            // stayed hidden until enemy VFX arrived at other crops.
            var frames = new[] { Frame(512, 512, 0f) };

            Assert.AreEqual(0f, FightController.VfxContentPaddingFraction(null, frames), 0.001f);
        }

        [Test]
        public void TheMinimumMarginAcrossTheSheetIsWhatCounts()
        {
            // The margin IS animation -- a bolt strikes to its canvas floor and
            // its afterglow then retracts upward. Correcting per frame would drag
            // the effect down the screen as it faded; correcting by the SMALLEST
            // margin takes the effect at its fullest extent, which is its real
            // ground line, and still lets the fade retreat on its own.
            var frames = new[]
            {
                Frame(512, 512, 0.30f),
                Frame(512, 512, 0.05f),   // fullest extent
                Frame(512, 512, 0.40f),
            };

            Assert.AreEqual(0.05f, FightController.VfxContentPaddingFraction(null, frames), 0.005f);
        }

        [Test]
        public void ABlankWindUpFrameIsIgnoredRatherThanBelieved()
        {
            // golem_boulder's f0 is deliberately empty. Letting it report 0 would
            // claim the effect reaches the floor before it has appeared -- which
            // is worse than not correcting, because it is confidently wrong.
            var frames = new[]
            {
                BlankFrame(512, 512),
                Frame(512, 512, 0.20f),
            };

            Assert.AreEqual(0.20f, FightController.VfxContentPaddingFraction(null, frames), 0.005f);
        }

        [Test]
        public void AnEntirelyBlankSheetFallsBackToNoCorrection()
        {
            // Graceful degradation, and "uncorrected" is exactly the old
            // behaviour rather than something new and worse.
            var frames = new[] { BlankFrame(64, 64), BlankFrame(64, 64) };

            Assert.AreEqual(0f, FightController.VfxContentPaddingFraction(null, frames), 0.001f);
        }

        [Test]
        public void AnUnreadableFrameReportsUnmeasurableRatherThanZero()
        {
            // The distinction the negative return exists for: "cannot tell" and
            // "reaches the floor" must not collapse into the same answer.
            Assert.Less(FightController.BottomPaddingFraction(null), 0f);
        }

        [Test]
        public void APaddedFrameReportsItsOwnMargin()
        {
            Assert.AreEqual(0.25f, FightController.BottomPaddingFraction(Frame(64, 64, 0.25f)), 0.02f);
        }

        // ---- the scene half -------------------------------------------------------

        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            // Through the hierarchy, INCLUDING INACTIVE. The VFX node is built
            // inactive -- there is nothing to show until something is cast -- and
            // FindAnyObjectByType skips inactive objects entirely, so the
            // global lookup finds nothing and the failure reads as "the component
            // was never wired" rather than "it is asleep".
            _player = _fight.GetComponentInChildren<SpellVfxPlayer>(includeInactive: true);

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 0, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 0, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePlayerIsWiredAndStartsSilent()
        {
            yield return LoadFight();

            Assert.IsNotNull(_player, "the scene has no SpellVfxPlayer");
            Assert.IsNotNull(_player.Image, "its Image was never bound");
            Assert.IsFalse(_player.Image.enabled, "nothing is cast yet");
            Assert.IsFalse(_player.Image.raycastTarget,
                "the effect is drawn above the stage and must never eat a click meant for an enemy");
        }

        [UnityTest]
        public IEnumerator AMissingSheetIsSilentRatherThanThrowing()
        {
            // Every spell in the game reaches this path today, because no VFX
            // sheet is authored yet. It has to be a no-op, not an exception.
            yield return LoadFight();

            _player.PlayAt("Vfx/does_not_exist", 0.6f, Vector2.zero, new Vector2(380f, 380f));

            Assert.IsFalse(_player.IsPlaying);
            Assert.IsFalse(_player.Image.enabled);
        }

        [UnityTest]
        public IEnumerator StoppingImmediatelyClearsAnEffectInFlight()
        {
            yield return LoadFight();

            _player.StopImmediately();

            Assert.IsFalse(_player.IsPlaying);
            Assert.IsFalse(_player.Image.enabled);
        }

        [UnityTest]
        public IEnumerator APlainSwingHasNoImpactDelayAtAll()
        {
            // Which is what keeps the whole impact-frame machinery invisible for
            // the overwhelming majority of beats.
            yield return LoadFight();

            var beat = new CombatBeat { VfxPath = "", VfxSeconds = 0f };

            Assert.AreEqual(0f, _fight.ImpactDelayFor(beat), 0.0001f);
        }
    }
}
