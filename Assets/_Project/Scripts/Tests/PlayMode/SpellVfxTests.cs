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
                if (skill == null || string.IsNullOrWhiteSpace(skill.vfx.path)) continue;

                var frames = FrameSequenceLoader.Load(skill.vfx.path);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"skill '{skill.id}' declares vfxPath '{skill.vfx.path}'");
                }
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.vfx.path)) continue;

                var frames = FrameSequenceLoader.Load(enemy.vfx.path);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"enemy '{enemy.id}' declares vfxPath '{enemy.vfx.path}'");
                }
            }

            Assert.IsEmpty(missing,
                "these resolve to no frames at all, so the effect plays as nothing:\n  "
                + string.Join("\n  ", missing)
                + "\nEither the frames are missing from Resources, or they imported as "
                + "plain Textures rather than Sprites (see StanceSpriteImporter).");
        }

        // ---- the two strike spells are actually reachable ---------------------------
        //
        // Frost Flare and Lightning Bolt shipped for months with unlockLevel 999
        // -- the "authored at a level that can never be reached" idiom the
        // content layer uses to park something. Their frames were on disk, their
        // sfx were on disk, their entries were in skills.json, and no character
        // could ever cast either of them. Nothing failed, because nothing was
        // wrong: an unreachable skill is a legitimate state and looks exactly
        // like a parked one.
        //
        // They are on Shawn's ladder now, in the gaps at 5 and 7. This is the
        // test that notices if they are parked again -- which a bulk edit of
        // unlockLevel would do silently, and which the VFX path tests above
        // cannot see, since a parked skill's art resolves perfectly well.
        [Test]
        public void TheTwoStrikeSpellsSitOnAReachableRungOfShawnsLadder()
        {
            foreach (var (id, level) in new[] { ("frost_flare", 5), ("lightning_bolt", 7) })
            {
                var skill = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == id);

                Assert.IsNotNull(skill, $"'{id}' is not in skills.json at all");
                Assert.AreEqual("sheep", skill.characterId, $"'{id}' is no longer Shawn's");
                Assert.AreEqual(level, skill.unlockLevel,
                    $"'{id}' moved off its rung - if that is deliberate, move it, but 999 means " +
                    "nobody can ever cast it and nothing else will say so");

                // AND ITS ART IS POINTED AT. The sibling test above checks every
                // declared path has frames; this checks the path is declared,
                // which is the other half and the one a merge is likelier to
                // drop.
                Assert.AreEqual($"Spells/{id}", skill.vfx.path, $"'{id}' lost its vfxPath");
                Assert.GreaterOrEqual(skill.vfx.impactFrame, 1,
                    $"'{id}' has no authored impact frame, so its blow lands on the resolver's " +
                    "default rather than on the peak the sequence was composed around");
            }
        }

        // AN ANCHOR THAT DOES NOT PARSE IS A TYPO, and the parser cannot say
        // so on its own. It falls back to Target rather than throwing, which is
        // right at play time -- one misplaced effect beats a fight that will not
        // resolve -- and means a misspelling reaches a player as an explosion in
        // the wrong place with nothing logged.
        //
        // This is the other half of that bargain: the fallback keeps the game
        // running, and the suite refuses the content that would need it.
        [Test]
        public void EveryAuthoredAnchorIsAWordTheParserKnows()
        {
            var wrong = new List<string>();

            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill == null || skill.vfx == null) continue;
                if (!SpellAnchorNames.IsKnown(skill.vfx.anchor))
                {
                    wrong.Add($"skill '{skill.id}' anchors to '{skill.vfx.anchor}'");
                }
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy == null || enemy.vfx == null) continue;
                if (!SpellAnchorNames.IsKnown(enemy.vfx.anchor))
                {
                    wrong.Add($"enemy '{enemy.id}' anchors to '{enemy.vfx.anchor}'");
                }
            }

            Assert.IsEmpty(wrong,
                "These anchors are not words the parser knows, so each one silently becomes " +
                "'target' at play time: " + string.Join(", ", wrong) + ". Valid: " +
                string.Join(", ", SpellAnchorNames.All) + ".");
        }

        // A size of zero or less is not "the default" -- it is a box with no
        // area, which draws nothing at all. The presentation treats it as the
        // default at play time for the same graceful-degradation reason the
        // anchor does, and this refuses the content that relies on it.
        [Test]
        public void EveryAuthoredSizeIsPositive()
        {
            var wrong = new List<string>();

            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill?.vfx == null || string.IsNullOrEmpty(skill.vfx.path)) continue;
                if (skill.vfx.size <= 0f) wrong.Add($"skill '{skill.id}' has size {skill.vfx.size}");
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy?.vfx == null || string.IsNullOrEmpty(enemy.vfx.path)) continue;
                if (enemy.vfx.size <= 0f) wrong.Add($"enemy '{enemy.id}' has size {enemy.vfx.size}");
            }

            Assert.IsEmpty(wrong,
                "A spell box with no area draws nothing: " + string.Join(", ", wrong) +
                ". Leave size out entirely to get the default rather than writing 0.");
        }

        // AN IMPACT POINT IS TWO NUMBERS OR IT IS NONE.
        //
        // impactX/impactY say where inside its own frame a sheet actually
        // strikes, and the view only believes them when BOTH are inside 0..1 --
        // anything else falls back to measuring the sheet's bottom margin, the
        // rule that was wrong for every sheet that is not drawn standing on a
        // floor. So a half-authored or out-of-range point does not fail: it
        // silently reverts a spell to the behaviour it was authored to escape,
        // which is the least visible way to be wrong.
        //
        // Same bargain as the anchor and the size above -- graceful at play
        // time, refused here.
        [Test]
        public void EveryAuthoredImpactPointIsWholeAndInsideItsOwnFrame()
        {
            var wrong = new List<string>();

            void Check(string what, SpellPresentation vfx)
            {
                if (vfx == null || string.IsNullOrEmpty(vfx.path)) return;

                bool anyStated = vfx.impactX != SpellPresentation.Unauthored
                              || vfx.impactY != SpellPresentation.Unauthored;

                if (!anyStated) return;

                if (!vfx.HasImpactPoint)
                {
                    wrong.Add($"{what} states ({vfx.impactX}, {vfx.impactY})");
                }
            }

            foreach (var skill in ContentDatabase.Skills) Check($"skill '{skill?.id}'", skill?.vfx);
            foreach (var enemy in ContentDatabase.Enemies) Check($"enemy '{enemy?.id}'", enemy?.vfx);

            Assert.IsEmpty(wrong,
                "an impact point needs BOTH impactX and impactY, each between 0 and 1 -- these are " +
                "ignored at play time and the spell falls back to bottom-margin placement: " +
                string.Join(", ", wrong));
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
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.vfx.path)) continue;

                int count = FrameSequenceLoader.Load(enemy.vfx.path)?.Length ?? 0;
                if (count > 0 && (enemy.vfx.impactFrame < 1 || enemy.vfx.impactFrame > count))
                {
                    wrong.Add($"enemy '{enemy.id}' impacts on frame {enemy.vfx.impactFrame} of {count}");
                }
            }

            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.vfx.path)) continue;

                int count = FrameSequenceLoader.Load(skill.vfx.path)?.Length ?? 0;
                if (count > 0 && (skill.vfx.impactFrame < 1 || skill.vfx.impactFrame > count))
                {
                    wrong.Add($"skill '{skill.id}' impacts on frame {skill.vfx.impactFrame} of {count}");
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
        public void APreImpactFramesMarginIsIgnoredOnceAnImpactFrameIsGiven()
        {
            // lightning_bolt's actual shape: a thin pre-impact bolt tip (f1/f2)
            // grazes the canvas floor harder than the wide impact burst (f4
            // onward) ever does. Scanning from frame 0 -- the old, unqualified
            // behaviour -- measures the wind-up's ground line and applies it to
            // the landed effect, which is why the strike rendered floating
            // above the target instead of on it. impactFrame is 1-based (frame
            // 5 authored means index 4), and the scan starts one frame early
            // (index 3) to keep a frame of lead-in without reintroducing the
            // travel frames that caused the bug.
            var frames = new[]
            {
                Frame(512, 512, 0.06f),  // f0
                Frame(512, 512, 0.00f),  // f1 -- the outlier: thin tip touches the floor
                Frame(512, 512, 0.00f),  // f2 -- same
                Frame(512, 512, 0.07f),  // f3
                Frame(512, 512, 0.13f),  // f4 -- impact (impactFrame=5, index 4)
                Frame(512, 512, 0.09f),  // f5 -- smallest from f3 onward
                Frame(512, 512, 0.14f),  // f6
            };

            Assert.AreEqual(0.09f, FightController.VfxContentPaddingFraction(null, frames, impactFrame: 5), 0.005f);
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

        private IEnumerator LoadFight() => LoadFight(1);

        // ENEMY COUNT AS A PARAMETER, because a test that skips is a test that
        // reads as coverage without being any. The splash case needs more than
        // one thing on the stage and the default fixture fields exactly one, so
        // it called Assert.Ignore and the capability shipped unverified.
        private IEnumerator LoadFight(int enemyCount)
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

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            // The first stays "Front": a sibling test looks its stage slot up
            // by name, and renaming the fixture's enemy was a change this did
            // not need to make.
            var foes = Enumerable.Range(0, Mathf.Max(1, enemyCount))
                .Select(i => new CombatantState(i == 0 ? "Front" : $"Foe{i}", false, 5000, 10, 8, 4))
                .ToArray();
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                enemyKits, new SeededRandom(5));
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

            var beat = new CombatBeat { Vfx = SpellPresentation.None };

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

        // IT CHARGES WHERE IT WAS CAST, THEN THROWS.
        //
        // The flight used to start on frame zero, so mud_blast's glyph spun its
        // eight charging revolutions while already halfway across the stage --
        // it tumbled through the air instead of winding up. The sequence has two
        // phases and the flight belongs to the second.
        //
        // Measured in SECONDS off the authored numbers rather than in frames,
        // because the frame count is a property of the recipe and this is about
        // what a player sees: for a third of the effect the glyph must not have
        // moved at all.
        [UnityTest]
        public IEnumerator ATravellingEffectHoldsAtTheCasterUntilItsChargeIsDone()
        {
            yield return LoadFight();

            var caster = SlotXOf("Shawn");
            var beat = TravellingBeat();

            // Frame 9 of 26 over 0.65s: the glyph is still at the caster at
            // 0.15s and gone by 0.35s.
            // AT REAL SPEED, opting out of the fixture's 60x. Everything else
            // in this class wants the fight to resolve instantly and does not
            // care how long a frame of a spell lasts; this is the one test that
            // is ABOUT how long, and at 60x its six seconds are a tenth of one.
            // That is not a slow version of the same measurement -- it is a
            // measurement of nothing, and it read as a bug in the player for
            // three rounds.
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            // SLOWED FOR THE MEASUREMENT, and the reason is not convenience.
            // At the shipping 0.65s a frame is 25ms, so a single long frame --
            // routine in batchmode, and possible in a real fight after a heavy
            // beat -- steps the sequence past its whole charge before this can
            // look. That is correct behaviour for a time-based animation and a
            // useless thing to assert against. At 6s the same hitch is 5% of
            // the run and the SHAPE is what gets tested: held, then thrown.
            beat.Vfx.seconds = 6f;
            _fight.PlaySpellVfxForTest(beat);

            // WHERE IT STARTED, not where the slot is. The two agree to within a
            // couple of pixels and are not the same measurement: the stage
            // re-anchors its slots on repaint, so reading the slot again mid-run
            // asks a question that has moved. What is being asserted is that the
            // effect does not travel during its charge, which is about the
            // effect.
            float launch = _player.Image.rectTransform.anchoredPosition.x;
            Assert.AreEqual(caster, launch, 6f, "the effect did not begin on the caster at all");

            // Frame 9 of 26 lands at 2.08s; frame 13 at 3.0s.
            float watched = 0f;
            while (watched < 1.6f)
            {
                Assert.AreEqual(launch, _player.Image.rectTransform.anchoredPosition.x, 1f,
                    $"the effect had already drifted {watched:0.00}s in, during its own wind-up");

                watched += Time.unscaledDeltaTime;
                yield return null;
            }

            while (watched < 3.4f)
            {
                watched += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Greater(Mathf.Abs(_player.Image.rectTransform.anchoredPosition.x - launch), 40f,
                "the effect never left the caster - a throw that does not travel is a charge that " +
                "never fired");
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

        // ---- an effect that lands on more than one thing -------------------------
        //
        // ResolveDamageAll opens ONE beat aimed at the first living enemy,
        // which is correct for the damage -- the numbers are recorded per
        // enemy -- and was quietly wrong for the art: three rats took the hit
        // and one of them got the animation. The beat carries the rest as
        // SplashTargets now, and the pool holds one member per stage slot.
        //
        // ASSERTED ON DISTINCT POSITIONS rather than on a count of enabled
        // Images. A pool that played three effects stacked on one enemy would
        // pass a count and be exactly the bug this replaced.
        [UnityTest]
        public IEnumerator AnEffectThatHitsEveryEnemyDrawsOnEveryEnemy()
        {
            // A FULL STAGE. The pool holds one member per slot, so three is
            // both the realistic worst case and the number that would expose an
            // off-by-one in the member walk.
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies
                .Where(e => e != null && e.IsAlive)
                .ToList();

            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, enemies.Count,
                "the fixture did not field a full stage, so this would prove nothing about a splash");

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

            _fight.PlaySpellVfxForTest(new CombatBeat
            {
                Actor = hero,
                Target = enemies[0],
                SplashTargets = enemies.Skip(1).ToList(),
                Vfx = new SpellPresentation
                {
                    path = "Spells/frost_flare",
                    seconds = 0.52f,
                    impactFrame = 5,
                },
            });
            yield return null;

            var drawn = _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => p.Image != null && p.Image.enabled)
                .Select(p => p.Image.rectTransform.anchoredPosition.x)
                .ToList();

            Assert.AreEqual(enemies.Count, drawn.Count,
                $"{enemies.Count} enemies were struck but {drawn.Count} effect(s) are drawn. An " +
                "all-enemies spell animating on one of them is what SplashTargets exists to fix.");

            Assert.AreEqual(drawn.Count, drawn.Distinct().Count(),
                "two effects are drawn at the same x, so they are stacked on one enemy rather than " +
                "spread across the ones that were hit");
        }

        // The ordinary case has to stay ordinary: a single-target spell must
        // not light up the rest of the pool now that there is a rest of it.
        [UnityTest]
        public IEnumerator ASingleTargetSpellStillDrawsExactlyOnce()
        {
            yield return LoadFight();

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            _fight.PlaySpellVfxForTest(new CombatBeat
            {
                Actor = hero,
                Target = foe,
                Vfx = new SpellPresentation
                {
                    path = "Spells/frost_flare",
                    seconds = 0.52f,
                    impactFrame = 5,
                },
            });
            yield return null;

            int drawn = _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Count(p => p.Image != null && p.Image.enabled);

            Assert.AreEqual(1, drawn,
                "a single-target spell lit " + drawn + " pool members; widening the pool for splash " +
                "effects must not change what an ordinary cast draws");
        }

        // ---- where a sheet says it hits ------------------------------------------
        //
        // The rule these replace: put the box's bottom edge on the target's
        // ground line, corrected by the lowest opaque pixel the sheet reaches.
        // Right for an effect drawn standing on a floor, and wrong for the two
        // other kinds this game already ships --
        //
        //   frost_flare's burst sits a sixth of the way up its frame with
        //   embers falling below it, so the measured floor was the embers and
        //   the strike rendered on the Giant Rat's chest;
        //
        //   mud_burst's impact is halfway up its frame and two thirds of the
        //   way across it, so it detonated above the rat's head and to one side.
        //
        // A sheet can now state its own point of contact, and the box is placed
        // to put that point where the spell is aimed.
        //
        // EVERY EXPECTED VALUE IS A LITERAL with its arithmetic spelled out,
        // never a call back into the code under test -- CLAUDE.md gotcha 5. The
        // whole fixture is chosen to make that possible: a 512x512 sheet in a
        // 380 box renders 380x380, so a frame fraction and a screen distance are
        // the same number times 380.

        [UnityTest]
        public IEnumerator AnAuthoredImpactPointLandsOnTheTargetsGroundLine()
        {
            yield return LoadFight();

            float ground = GroundYOf("Front");

            // 0.25 of the frame's height up from its bottom. The box is 380
            // tall, so that point sits 95 above the box's own bottom edge and
            // therefore 190 - 95 = 95 BELOW the box's centre. For it to land on
            // the ground line the box centre has to be 95 above it.
            _fight.PlaySpellVfxForTest(AimedBeat(impactX: 0.5f, impactY: 0.25f));

            Assert.AreEqual(ground + 95f, _player.Image.rectTransform.anchoredPosition.y, 1.5f,
                "the sheet's stated point of contact did not land on the target's ground line");
        }

        // THE HORIZONTAL HALF, which the old rule did not have at all: the box
        // was centred on the target whatever the sheet had drawn where.
        [UnityTest]
        public IEnumerator AnOffCentreImpactPointMovesTheBoxSideways()
        {
            yield return LoadFight();

            float target = SlotXOf("Front");

            // 0.75 across the frame is 0.25 of 380 = 95 to the RIGHT of the
            // box's centre, so the box has to sit 95 to the LEFT of the target
            // for the drawn impact to land on it.
            _fight.PlaySpellVfxForTest(AimedBeat(impactX: 0.75f, impactY: 0.5f));

            Assert.AreEqual(target - 95f, _player.Image.rectTransform.anchoredPosition.x, 1.5f,
                "an impact drawn off-centre still detonated in the middle of the box");
        }

        // A SHEET THAT SAYS NOTHING MUST NOT MOVE. The measured rule is what
        // golem_boulder is tuned against and what every future unstated sheet
        // will get, so the fallback is as load-bearing as the new path.
        //
        // Asserted as a DIFFERENCE between two casts of the same sheet rather
        // than against an absolute, because the absolute is the measured
        // correction itself -- recomputing it here would be the tautology this
        // file's siblings warn about, and pinning it as a literal would pin the
        // sheet's alpha rather than the rule.
        [UnityTest]
        public IEnumerator ASheetWithNoStatedPointKeepsTheMeasuredPlacement()
        {
            yield return LoadFight();

            _fight.PlaySpellVfxForTest(AimedBeat(impactX: -1f, impactY: -1f));
            var measured = _player.Image.rectTransform.anchoredPosition;

            // The same sheet, stating the point the measured rule would have
            // put there anyway: dead centre horizontally, and a whole box-half
            // up from the ground line. Those two agree only if the sheet has no
            // bottom margin at all -- frost_flare's embers reach its floor, so
            // they very nearly do, and "very nearly" is what the tolerance is.
            _fight.PlaySpellVfxForTest(AimedBeat(impactX: 0.5f, impactY: 0f));
            var stated = _player.Image.rectTransform.anchoredPosition;

            Assert.AreEqual(stated.x, measured.x, 1.5f,
                "an unstated sheet stopped being centred on its target");
            Assert.AreEqual(stated.y, measured.y, 12f,
                "an unstated sheet no longer stands on the ground line -- the measured fallback " +
                "moved, and every sheet that has not been given a point moved with it");
        }

        // MIRRORED, THE CORRECTION GOES THE OTHER WAY. A sheet fired back across
        // the stage has its point of contact the same distance in from the
        // OTHER edge, so a horizontal correction that ignores facing does not
        // merely fail to help -- it doubles the error.
        //
        // Invisible on Shawn, who always casts rightward, and wrong on every
        // enemy: the same shape as every stage bug this project has recorded.
        [UnityTest]
        public IEnumerator AMirroredCastCorrectsTowardsTheOtherEdge()
        {
            yield return LoadFight();

            float hero = SlotXOf("Shawn");

            var beat = TravellingBeat(reversed: true);
            beat.Vfx.impactX = 0.75f;
            beat.Vfx.impactY = 0.5f;

            _fight.PlaySpellVfxForTest(beat);
            yield return null;

            Assert.Less(_player.Image.rectTransform.localScale.x, 0f,
                "fixture: the cast was not mirrored, so this proves nothing about mirroring");

            // Mirrored, 0.75-from-the-left is drawn 0.75 from the RIGHT, so the
            // box sits 95 to the right of what it is aimed at rather than 95 to
            // the left. Read at the END of the flight, which is where the
            // arrival is placed.
            float waited = 0f;
            while (waited < 1.4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(hero + 95f, _player.Image.rectTransform.anchoredPosition.x, 2f,
                "the mirrored cast corrected the same way an unmirrored one does, which puts the " +
                "impact twice as far off as leaving it uncorrected would have");
        }

        // A beat aimed at the fixture's one enemy with a sheet whose frames are
        // a known 512 square. frost_flare rather than a synthetic texture: the
        // placement maths reads the sheet's own dimensions, so a fabricated
        // sprite would be testing a fabricated aspect ratio.
        private CombatBeat AimedBeat(float impactX, float impactY)
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = hero,
                Target = foe,
                Vfx = new SpellPresentation
                {
                    path = "Spells/frost_flare",
                    seconds = 0.52f,
                    impactFrame = 5,
                    impactX = impactX,
                    impactY = impactY,
                },
            };
        }

        // The stage's ground line under a combatant, in the effect pool's own
        // coordinates -- the slot's bottom edge, which is what the figures
        // stand on.
        private float GroundYOf(string name)
        {
            var slot = SlotOf(name);
            var parent = _player.transform.parent;
            return parent.InverseTransformPoint(
                slot.TransformPoint(new Vector3(0f, slot.rect.yMin, 0f))).y;
        }

        private CombatBeat TravellingBeat(bool reversed = false, bool fromCaster = true)
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = reversed ? foe : hero,
                Target = reversed ? hero : foe,
                Vfx = new SpellPresentation
                {
                    path = "Spells/mud_burst",
                    seconds = 0.65f,
                    impactFrame = 13,
                    departFrame = 9,
                    anchor = fromCaster ? "travel" : "target",
                },
            };
        }

        private float SlotXOf(string name)
        {
            var parent = _player.transform.parent;
            return parent.InverseTransformPoint(SlotOf(name).TransformPoint(Vector3.zero)).x;
        }

        private RectTransform SlotOf(string name)
        {
            var combatant = _fight.SessionForTest.Encounter.PlayerParty
                .Concat(_fight.SessionForTest.Encounter.Enemies)
                .First(c => c != null && c.Name == name);

            var slot = _fight.SlotForTest(combatant);
            Assert.IsNotNull(slot, $"'{name}' has no stage slot");
            return slot;
        }

        // ---- the dissolve ----------------------------------------------------------
        //
        // A sheet is six to fourteen drawings over three quarters of a second.
        // Swapped, that is a slideshow: each drawing sits still for 50ms and is
        // replaced, which reads as steps -- and speeding it up does not fix it,
        // because faster steps are still steps and past a point the whole spell
        // is gone before it registers.
        //
        // The incoming frame fades up over the outgoing one instead. Asserted
        // rather than eyeballed because a still frame cannot show it: a capture
        // taken at a frame boundary looks exactly like the stepped version, and
        // that is most of the sequence's running time.
        [UnityTest]
        public IEnumerator TheIncomingFrameFadesUpOverTheOutgoingOne()
        {
            yield return LoadFight();

            // Slowed right down, so the sampling below lands where it means to.
            _player.PlayAt("Spells/mud_burst", 8f, Vector2.zero, new Vector2(380f, 380f));

            var fade = FadeLayer();
            Assert.IsNotNull(fade, "the player has no dissolve layer - see FightScreen.BuildSpellVfx");

            bool sawBlend = false;
            bool sawClean = false;

            float watched = 0f;
            while (watched < 1.6f)
            {
                if (fade.enabled && fade.color.a > 0.05f && fade.color.a < 0.95f) sawBlend = true;

                // AND IT IS NOT ALWAYS BLENDING. A frame that dissolves from the
                // moment it appears is never itself: halfway through, two
                // drawings share the screen at half strength and neither is
                // legible, which for a lightning bolt means two bolts.
                if (!fade.enabled || fade.color.a <= 0.01f) sawClean = true;

                watched += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(sawBlend, "no frame ever blended into the next - the sequence is still a slideshow");
            Assert.IsTrue(sawClean, "every frame was mid-dissolve, so no frame is ever shown on its own");
        }

        // BOTH LAYERS GO WHEN THE EFFECT DOES. The dissolve layer is a child of
        // the frame image, so a leftover alpha on it would paint the last
        // incoming frame over the stage until the next cast overwrote it.
        [UnityTest]
        public IEnumerator StoppingClearsTheDissolveLayerTooNotJustTheFrame()
        {
            yield return LoadFight();

            _player.PlayAt("Spells/mud_burst", 8f, Vector2.zero, new Vector2(380f, 380f));
            yield return null;

            _player.StopImmediately();

            Assert.IsFalse(_player.Image.enabled);
            Assert.IsFalse(FadeLayer().enabled, "the dissolve layer is still drawing a frame of a stopped spell");
        }

        // Reached by NAME rather than through the component's field: a PlayMode
        // test has no access to Core's internals by design, and the emitted node
        // name is already a stable contract because UiAudit fails the build on a
        // duplicate one.
        private UnityEngine.UI.Image FadeLayer() =>
            _player.GetComponentsInChildren<UnityEngine.UI.Image>(includeInactive: true)
                .FirstOrDefault(i => i.name.EndsWith("Next"));
    }
}
