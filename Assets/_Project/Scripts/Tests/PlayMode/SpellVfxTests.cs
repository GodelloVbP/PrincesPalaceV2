using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
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

        // ---- every declared path has art behind it -------------------------------

        // THE GOLEM'S BOULDER SLAM PLAYED NOTHING FOR SIX WEEKS and nothing
        // reported it. Its six frames shipped at textureType 0, so
        // Resources.Load<Sprite> returned null on f0, the frame probe stopped
        // on the first miss, and the enemy's signature attack simply had no
        // effect -- a silent failure with no error, no warning and no visual
        // difference from an enemy that was never given one.
        //
        // StanceSpriteImporter now sets the texture type on import, which fixes
        // the cause. This is the half that would have NOTICED: a path declared
        // in content and answered by nothing is a build failure rather than
        // something for a player to not-see.
        //
        // Deliberately over content rather than over the folder: art with no
        // path pointing at it is spare art, which is harmless. A path with no
        // art is a hole.
        [Test]
        public void EveryDeclaredVfxPathHasFramesBehindIt()
        {
            var missing = new List<string>();

            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.vfxPath)) continue;

                var frames = FrameSequenceLoader.Load(skill.vfxPath);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"skill '{skill.id}' declares vfxPath '{skill.vfxPath}'");
                }
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.vfxPath)) continue;

                var frames = FrameSequenceLoader.Load(enemy.vfxPath);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"enemy '{enemy.id}' declares vfxPath '{enemy.vfxPath}'");
                }
            }

            Assert.IsEmpty(missing,
                "these resolve to no frames at all, so the effect plays as nothing:\n  "
                + string.Join("\n  ", missing)
                + "\nEither the frames are missing from Resources, or they imported as "
                + "plain Textures rather than Sprites (see StanceSpriteImporter).");
        }

        // And the impact frame has to be a frame that exists -- an effect whose
        // hit lands on frame 8 of a six-frame sequence lands at the end
        // instead, quietly, which is the same class of miss one step along.
        [Test]
        public void EveryImpactFrameIsInsideItsOwnSequence()
        {
            var wrong = new List<string>();

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.vfxPath)) continue;

                int count = FrameSequenceLoader.Load(enemy.vfxPath)?.Length ?? 0;
                if (count > 0 && (enemy.vfxImpactFrame < 1 || enemy.vfxImpactFrame > count))
                {
                    wrong.Add($"enemy '{enemy.id}' impacts on frame {enemy.vfxImpactFrame} of {count}");
                }
            }

            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.vfxPath)) continue;

                int count = FrameSequenceLoader.Load(skill.vfxPath)?.Length ?? 0;
                if (count > 0 && (skill.vfxImpactFrame < 1 || skill.vfxImpactFrame > count))
                {
                    wrong.Add($"skill '{skill.id}' impacts on frame {skill.vfxImpactFrame} of {count}");
                }
            }

            Assert.IsEmpty(wrong, "the hit lands outside the animation:\n  " + string.Join("\n  ", wrong));
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

        // ---- an effect that crosses the stage --------------------------------------
        //
        // mud_blast is drawn as a conjuring glyph on the left, a lance across
        // the middle and an impact on the right. Fitted into a 380 square on the
        // target -- which is what every other sheet wants -- the glyph appears
        // in open air short of the caster, and the spell reads as arriving from
        // nowhere. These pin the placement that fixes it.

        [UnityTest]
        public IEnumerator ATravellingEffectStartsOnTheCasterAndEndsOnTheTarget()
        {
            yield return LoadFight();

            var caster = SlotXOf("Shawn");
            var target = SlotXOf("Front");
            Assume.That(Mathf.Abs(target - caster), Is.GreaterThan(200f),
                "fixture: the two have to be far enough apart for the flight to be measurable");

            // READ BEFORE YIELDING. PlayFrom writes the start position
            // synchronously and the coroutine begins easing on its very first
            // frame, so a yield here reads the effect already 6px underway --
            // which is correct behaviour and a flaky assertion, and it failed
            // that way exactly once before this comment was written.
            _fight.PlaySpellVfxForTest(TravellingBeat());

            Assert.AreEqual(caster, _player.Image.rectTransform.anchoredPosition.x, 2f,
                "the effect does not begin on the caster, which is the whole complaint it fixes");

            // Out to the end of the sequence, where it has to have arrived.
            float waited = 0f;
            while (waited < 1.4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(target, _player.Image.rectTransform.anchoredPosition.x, 2f,
                "the effect never reached the thing it was cast at");
        }

        // MIRRORED WHEN THE CASTER IS ON THE RIGHT. The Bog Witch casts the same
        // spell back across the stage; drawn as-authored her glyph forms on
        // Shawn and her impact lands on herself.
        [UnityTest]
        public IEnumerator ATravellingEffectFiresTheWayTheCasterIsFacing()
        {
            yield return LoadFight();

            _fight.PlaySpellVfxForTest(TravellingBeat());
            yield return null;
            Assert.Greater(_player.Image.rectTransform.localScale.x, 0f,
                "the hero casts left to right, which is how the sheet is drawn");

            _fight.PlaySpellVfxForTest(TravellingBeat(reversed: true));
            yield return null;
            Assert.Less(_player.Image.rectTransform.localScale.x, 0f,
                "cast back across the stage the sheet has to be mirrored");
        }

        // THE MIRROR IS STATE A CAST LEAVES ON A SHARED IMAGE. One player
        // serves every spell in the fight, so a spell cast right-to-left would
        // hand the next one a mirrored box -- a bug that only ever appears in
        // the SECOND spell and never in the one that caused it.
        [UnityTest]
        public IEnumerator AnOrdinaryEffectAfterAMirroredOneIsNotItselfMirrored()
        {
            yield return LoadFight();

            _fight.PlaySpellVfxForTest(TravellingBeat(reversed: true));
            yield return null;

            _fight.PlaySpellVfxForTest(TravellingBeat(fromCaster: false));
            yield return null;

            Assert.AreEqual(1f, _player.Image.rectTransform.localScale.x, 0.001f,
                "the mirror survived into the next cast");
            Assert.AreEqual(380f, _player.Image.rectTransform.sizeDelta.x, 1f,
                "the box size survived into the next cast");
        }

        private CombatBeat TravellingBeat(bool reversed = false, bool fromCaster = true)
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = reversed ? foe : hero,
                Target = reversed ? hero : foe,
                VfxPath = "Spells/mud_burst",
                VfxSeconds = 0.78f,
                VfxImpactFrame = 11,
                VfxFromCaster = fromCaster,
            };
        }

        private float SlotXOf(string name)
        {
            var combatant = _fight.SessionForTest.Encounter.PlayerParty
                .Concat(_fight.SessionForTest.Encounter.Enemies)
                .First(c => c != null && c.Name == name);

            var slot = _fight.SlotForTest(combatant);
            Assert.IsNotNull(slot, $"'{name}' has no stage slot");

            var parent = _player.transform.parent;
            return parent.InverseTransformPoint(slot.TransformPoint(Vector3.zero)).x;
        }
    }
}
