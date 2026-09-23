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
            SpellPerformancePlayer.ClockOverride = null;
        }

        // ---- seeing an effect that lasts 13ms ------------------------------------
        //
        // WHY EVERY "IS IT DRAWN" TEST BELOW HOLDS THE CLOCK.
        //
        // PlayFast runs the fight at 60x, so a cast's whole real-time budget is
        // FightBeatPlayer.Scaled(seconds) -- 13ms for the 0.78s cinderfault
        // below, 8.7ms for a 0.52s flare. A batchmode frame right after a scene
        // load was measured at 24ms and 57ms. So `cast; yield return null;
        // count what is enabled` does not ask "did the cast draw one layer per
        // enemy"; it asks "did the next frame come back inside 13ms", and the
        // answer varies run to run. That is AUDIT.md #61 -- three runs on an
        // unchanged tree, a different test failing each time, because the four
        // coroutines (one fault, three eruptions) each lose that race
        // independently.
        //
        // Holding SpellVfxPlayer's clock at the cast makes elapsed stay zero,
        // so the effect is still mid-playback however long the frame takes.
        // Nothing else about the cast changes: image.enabled, the pool walk and
        // the placement arithmetic all run exactly as they do in a fight.
        //
        // The instant the clock was pinned to, and the instant it is now
        // reading. Kept as fields rather than captured in the lambda because
        // the second helper below has to measure from the FIRST one -- and
        // measuring from Time.time instead reads a frame of real time as
        // sixty cast-seconds at this fixture's speed, which puts every sample
        // past the end of the spell it meant to look at.
        private float _castAt;
        private float _held;

        private void HoldTheClockAtTheCast()
        {
            _castAt = Time.time;
            _held = _castAt;
            SpellPerformancePlayer.ClockOverride = () => _held;
        }

        // The other half of the same seam: jump the clock well past any budget
        // a spell in this file authors, so the next tick of each coroutine runs
        // its own cleanup. TWO frames are waited after this, not one -- the
        // test runner's enumerator and a MonoBehaviour's coroutine resume at
        // different points in a frame, and only the second frame is guaranteed
        // to have a full coroutine tick behind it.
        private static void RunTheClockPastTheEnd()
        {
            float past = Time.time + 600f;
            SpellPerformancePlayer.ClockOverride = () => past;
        }

        // THE THIRD FORM: held, then MOVED TO A NAMED INSTANT OF THE CAST.
        //
        // Anything reading state a cast only has WHILE IT IS LIVE has to hold
        // the clock, and the mirror is the sharpest case -- a released renderer
        // has its facing put back, which is the whole point of the restore
        // list, and at this fixture's 60x a 0.65s flight is 11ms, shorter than
        // one batchmode frame. So `cast; yield; read the mirror` reads a
        // RESTORED member about as often as a live one, and passes or fails on
        // how long that frame took.
        //
        // `castSeconds` is measured from the cast, absolutely, so two calls
        // name two instants rather than accumulating.
        private void HoldTheClockAt(float castSeconds)
        {
            _held = _castAt + FightBeatPlayer.Scaled(castSeconds);
            _fight.PerformancePlayerForTest.Tick(_held);
        }

        // BETWEEN TWO CASTS IN ONE TEST, because a cast now OWNS the renderers
        // it obtained until its own lifetime ends. Two casts back to back
        // inside a single frame is not something a fight does -- beats are
        // BeatHoldSeconds apart and these sequences are milliseconds long at
        // the 60x this fixture runs -- and the second one deliberately gets its
        // OWN member rather than restarting the first's, which is the
        // "a second cast must neither restart nor steal" rule. So a test that
        // reads a fixed pool member after a second cast would be reading the
        // FIRST cast unless the first has ended, which is what a beat boundary
        // does for free in a real fight.
        private void LetTheLastCastFinish() =>
            _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true).EndFight();

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
                if (skill == null || string.IsNullOrWhiteSpace(skill.Data.Vfx.path)) continue;

                var frames = FrameSequenceLoader.Load(skill.Data.Vfx.path);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"skill '{skill.id}' declares vfxPath '{skill.Data.Vfx.path}'");
                }
            }

            // THE SHARED GROUND LAYER IS A SECOND PATH ON THE SAME BLOCK, and a
            // sweep that only walked `path` would leave half of a two-layer
            // spell unguarded -- exactly the gap this test exists to close, one
            // field along.
            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.Data.Vfx.groundPath)) continue;

                var frames = FrameSequenceLoader.Load(skill.Data.Vfx.groundPath);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"skill '{skill.id}' declares vfx.groundPath '{skill.Data.Vfx.groundPath}'");
                }
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Data.Vfx.path)) continue;

                var frames = FrameSequenceLoader.Load(enemy.Data.Vfx.path);
                if (frames == null || frames.Length == 0)
                {
                    missing.Add($"enemy '{enemy.id}' declares vfxPath '{enemy.Data.Vfx.path}'");
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
            // Both moved from the level ladder onto the book ladder in
            // docs/PLAN_SHOP.md Gate 4 -- bookOnly true, unlockLevel gone
            // (SkillEntryResolver reads its absence on a bookOnly skill as
            // int.MaxValue, on purpose: see that file's own comment). A
            // reachable rung is now bookTier > 0, not a level number.
            foreach (var (id, bookTier) in new[] { ("frost_flare", 2), ("lightning_bolt", 3) })
            {
                var skill = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == id);

                Assert.IsNotNull(skill, $"'{id}' is not in skills.json at all");
                Assert.AreEqual("sheep", skill.Data.CharacterId, $"'{id}' is no longer Shawn's");
                Assert.IsTrue(skill.Data.BookOnly, $"'{id}' should be learned-only, not levelled into");
                Assert.AreEqual(bookTier, skill.Data.BookTier,
                    $"'{id}' moved off its rung - if that is deliberate, move it, but 0 means " +
                    "nobody can ever roll it in the shop and nothing else will say so");

                // AND ITS ART IS POINTED AT. The sibling test above checks every
                // declared path has frames; this checks the path is declared,
                // which is the other half and the one a merge is likelier to
                // drop.
                Assert.AreEqual($"Spells/{id}", skill.Data.Vfx.path, $"'{id}' lost its vfxPath");
                Assert.GreaterOrEqual(skill.Data.Vfx.impactFrame, 1,
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
                if (skill == null || skill.Data.Vfx == null) continue;
                if (!SpellAnchorNames.IsKnown(skill.Data.Vfx.anchor))
                {
                    wrong.Add($"skill '{skill.id}' anchors to '{skill.Data.Vfx.anchor}'");
                }
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy == null || enemy.Data.Vfx == null) continue;
                if (!SpellAnchorNames.IsKnown(enemy.Data.Vfx.anchor))
                {
                    wrong.Add($"enemy '{enemy.id}' anchors to '{enemy.Data.Vfx.anchor}'");
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
                if (skill?.Data.Vfx == null || string.IsNullOrEmpty(skill.Data.Vfx.path)) continue;
                if (skill.Data.Vfx.size <= 0f) wrong.Add($"skill '{skill.id}' has size {skill.Data.Vfx.size}");
            }

            foreach (var enemy in ContentDatabase.Enemies)
            {
                if (enemy?.Data?.Vfx == null || string.IsNullOrEmpty(enemy.Data.Vfx.path)) continue;
                if (enemy.Data.Vfx.size <= 0f) wrong.Add($"enemy '{enemy.id}' has size {enemy.Data.Vfx.size}");
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

            foreach (var skill in ContentDatabase.Skills) Check($"skill '{skill?.id}'", skill?.Data.Vfx);
            foreach (var enemy in ContentDatabase.Enemies) Check($"enemy '{enemy?.id}'", enemy?.Data?.Vfx);

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
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Data.Vfx.path)) continue;

                int count = FrameSequenceLoader.Load(enemy.Data.Vfx.path)?.Length ?? 0;
                if (count > 0 && (enemy.Data.Vfx.impactFrame < 1 || enemy.Data.Vfx.impactFrame > count))
                {
                    wrong.Add($"enemy '{enemy.id}' impacts on frame {enemy.Data.Vfx.impactFrame} of {count}");
                }
            }

            foreach (var skill in ContentDatabase.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.Data.Vfx.path)) continue;

                int count = FrameSequenceLoader.Load(skill.Data.Vfx.path)?.Length ?? 0;
                if (count > 0 && (skill.Data.Vfx.impactFrame < 1 || skill.Data.Vfx.impactFrame > count))
                {
                    wrong.Add($"skill '{skill.id}' impacts on frame {skill.Data.Vfx.impactFrame} of {count}");
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

        private IEnumerator LoadFight() => LoadFight(1, 1f);

        private IEnumerator LoadFight(int enemyCount) => LoadFight(enemyCount, 1f);

        // ENEMY COUNT AS A PARAMETER, because a test that skips is a test that
        // reads as coverage without being any. The splash case needs more than
        // one thing on the stage and the default fixture fields exactly one, so
        // it called Assert.Ignore and the capability shipped unverified.
        // STAGE SCALE AS A PARAMETER, for the one suite that needs an enemy
        // authoring something other than the fixture's own default of 1 --
        // fit: target reads this exact number (composed with the slot's own
        // depth curve) off the struck target, and every other test in this
        // file wants the identical stage it always got.
        //
        // SPRITE PATH AS A PARAMETER for the target-bounds suite: every other
        // fixture enemy has no art, so BodyOf falls back to the slot rect and
        // the placement it measures is exactly the one it always was. Only a
        // real drawing has a body narrower than its canvas.
        private IEnumerator LoadFight(int enemyCount, float stageScale, string spritePath = "")
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
            //
            // AND EXPLICITLY NOT THE GROUND LAYER, which carries the same
            // component and is now the FIRST one in the hierarchy -- it is
            // declared before the racks so uGUI draws it behind them (see
            // FightScreen.BuildSpellGroundVfx). GetComponentInChildren walks in
            // sibling order, so a bare call here silently returns the fault
            // rather than pool member 0, and every placement assertion below
            // would be measuring a layer PlaySpellVfx never puts a per-target
            // effect on.
            _player = _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .FirstOrDefault(p => !_fight.GroundVfxPlayersForTest.Contains(p));

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            // The first stays "Front": a sibling test looks its stage slot up
            // by name, and renaming the fixture's enemy was a change this did
            // not need to make.
            var foes = Enumerable.Range(0, Mathf.Max(1, enemyCount))
                .Select(i => new CombatantState(i == 0 ? "Front" : $"Foe{i}", false, 5000, 10, 8, 4))
                .ToArray();
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = PlayModeSparkFixture.Kit();
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0, spritePath: spritePath,
                    stageScale: stageScale), false))
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

            var missing = AimedBeat(impactX: 0.5f, impactY: 0.25f);
            missing.Vfx.path = "Vfx/does_not_exist";
            _fight.PlaySpellVfxForTest(missing);

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
            //
            // BOUNDED ON THE PLAYER'S OWN STATE, not a fixed real-time guess.
            // PlayRoutine already runs its whole travel through
            // FightBeatPlayer.Scaled, so under this fixture's 60x [SetUp] the
            // flight is done in milliseconds -- waiting a flat 1.4s here was
            // ~130x longer than the thing it was waiting for. The 2f ceiling
            // is a failure timeout, not the expected wait.
            float waited = 0f;
            while (_player.IsPlaying && waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(_player.IsPlaying, "the effect never finished its flight inside the timeout");
            Assert.AreEqual(target, _player.Image.rectTransform.anchoredPosition.x, 2f,
                "the effect never reached the thing it was cast at");
        }

        // WIRING CHECK: an actor with an authored castPoint (StanceManifest
        // .json) launches a travelling cast from it rather than from its own
        // slot origin. Characters/owl is the shipped manifest's own entry --
        // reusing it here checks the real content rather than a synthetic
        // fixture that could pass against a broken lookup. Not asserting an
        // exact literal: FightStageAnchors' own depth scale is a live number
        // this file has no business pinning a second time (CastPointPlacement
        // Tests already pins the seam's own arithmetic in EditMode), so this
        // only asserts the DIRECTION and a floor on the MAGNITUDE that the
        // manifest's positive dx implies for a caster who faces right with no
        // mirror -- loose enough to survive a depth-scale retune, tight
        // enough that "the seam stopped firing" cannot pass silently.
        [UnityTest]
        public IEnumerator ATravellingEffectLeavesFromAnAuthoredCastPointRatherThanTheSlotOrigin()
        {
            yield return LoadFight();

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            _fight.BindPartyArt(new List<CombatantState> { hero }, new List<string> { "Characters/owl" });

            var casterOrigin = SlotXOf("Shawn");

            _fight.PlaySpellVfxForTest(TravellingBeat());

            float launchX = _player.Image.rectTransform.anchoredPosition.x;

            Assert.Greater(launchX, casterOrigin + 20f,
                "an authored castPoint did not move the launch off the slot origin -- either the manifest " +
                "lookup or the seam that reads it stopped firing");
        }

        // THE OTHER HALF OF THE SAME SEAM, and the one that shipped broken:
        // prismatic_orb's own "charge" layer is `place: caster-centre` and
        // never travels, and it kept forming at Odette's slot centre -- her
        // spine, roughly, since the slot is sized to her raw stance canvas --
        // while the travelling "core" cast right behind it correctly left
        // from her book, because CasterCastPoint's guard read
        // `layer.Place == SpellPlace.Caster` and CasterCentre is a different
        // enum member. Confirmed on screen before this fix: a runtime
        // capture of the real cast shows the earth charge's dust glyph
        // forming at Odette's neck and shoulder, nowhere near the book her
        // wings are holding open (tools/screenshot.ps1 -Runtime
        // -RuntimeFilter SpellRuntimeCaptureTests, frames 8-15 of
        // spell_prismatic_orb_f*.png).
        //
        // Same fixture and the same loose assertion as the travelling test
        // above and for the same reason -- FightStageAnchors' depth scale is
        // a live number this file has no business pinning twice.
        [UnityTest]
        public IEnumerator ANonTravellingCasterCentreLayerLeavesFromAnAuthoredCastPointToo()
        {
            yield return LoadFight();

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            _fight.BindPartyArt(new List<CombatantState> { hero }, new List<string> { "Characters/owl" });

            var casterOrigin = SlotXOf("Shawn");

            _fight.PlaySpellVfxForTest(CasterCentreBeat());

            float drawnX = _player.Image.rectTransform.anchoredPosition.x;

            Assert.Greater(drawnX, casterOrigin + 20f,
                "an authored castPoint did not move a non-travelling caster-centre layer off the slot " +
                "centre -- either OnCaster stopped covering CasterCentre or the seam that reads it stopped " +
                "firing");
        }

        // A single caster-centre layer that never travels -- prismatic_orb's
        // "charge" in shape, frost_flare's frames standing in for its art,
        // the same substitution TrailingBeat below makes for the same
        // reason: this is about the placement, not about the art.
        private CombatBeat CasterCentreBeat()
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = hero,
                Target = foe,
                Vfx = new SpellPresentation
                {
                    layerFormat = 1,
                    layers = new[]
                    {
                        new SpellLayer
                        {
                            id = "charge",
                            render = "sprite",
                            place = "caster-centre",
                            at = "release",
                            path = "Spells/frost_flare",
                            seconds = 0.2f,
                            until = "once",
                            size = 195f,
                            facing = "auto",
                            sort = "effects",
                        },
                    },
                },
            };
        }

        // MIRRORED WHEN THE CASTER IS ON THE RIGHT. The Bog Witch casts the same
        // spell back across the stage; drawn as-authored her glyph forms on
        // Shawn and her impact lands on herself.
        [UnityTest]
        public IEnumerator ATravellingEffectFiresTheWayTheCasterIsFacing()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TravellingBeat());
            yield return null;
            Assert.Greater(_player.Image.rectTransform.localScale.x, 0f,
                "the hero casts left to right, which is how the sheet is drawn");

            LetTheLastCastFinish();
            HoldTheClockAtTheCast();
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
            //
            // docs/archive/PLAN_BATTLE_SPEED.md G3: pinned alongside it -- "real
            // speed" now means Pace == 1, and BeatSpeedMultiplier alone no
            // longer guarantees that once FightBootstrap can install a
            // settings-backed PlayerSpeedMultiplier on the same fight.
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;

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

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TravellingBeat(reversed: true));
            yield return null;

            LetTheLastCastFinish();
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TravellingBeat(fromCaster: false));
            yield return null;

            Assert.AreEqual(1f, _player.Image.rectTransform.localScale.x, 0.001f,
                "the mirror survived into the next cast");
            Assert.AreEqual(380f, _player.Image.rectTransform.sizeDelta.x, 1f,
                "the box size survived into the next cast");
        }

        // A FOLLOWER'S dx IS LOCAL TO THE CAST, which is the half that was
        // missing. The wake the Water pilot authors sits 118 units BEHIND its
        // core, and "behind" is a direction: cast back across the stage the
        // same number has to put it 118 units the other way, or the wake leads
        // the projectile it is supposed to trail.
        //
        // Invisible on Odette, who is the only caster of the only spell that
        // authors one today, and wrong on every monster -- the exact shape of
        // the mirroring bug the sibling tests above already record twice.
        [UnityTest]
        public IEnumerator AFollowersOffsetIsMirroredWithTheCastThatOwnsIt()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TrailingBeat());
            yield return null;

            var drawn = EffectPlayers();
            Assert.AreEqual(-118f, drawn[1].Image.rectTransform.anchoredPosition.x
                                   - drawn[0].Image.rectTransform.anchoredPosition.x, 1.5f,
                "cast left to right, the trailing layer has to sit its authored dx BEHIND its source");

            LetTheLastCastFinish();
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TrailingBeat(reversed: true));
            yield return null;

            drawn = EffectPlayers();
            Assert.AreEqual(118f, drawn[1].Image.rectTransform.anchoredPosition.x
                                  - drawn[0].Image.rectTransform.anchoredPosition.x, 1.5f,
                "cast back across the stage, the same authored dx has to reach the other way -- " +
                "unmirrored it puts the wake in FRONT of the thing it trails");
        }

        // The effect band in pool order, which is authored layer order: a cast
        // takes the lowest free member per layer, so member 0 is the first
        // layer the block declares and member 1 the second.
        private IReadOnlyList<SpellVfxPlayer> EffectPlayers() =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => !_fight.GroundVfxPlayersForTest.Contains(p))
                .ToList();

        // A travelling layer with something riding it, which is the pilot's
        // core-and-wake pair with frost_flare's frames standing in for both --
        // this is about the offset, not about the art.
        private CombatBeat TrailingBeat(bool reversed = false)
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = reversed ? foe : hero,
                Target = reversed ? hero : foe,
                Vfx = new SpellPresentation
                {
                    layerFormat = 1,
                    layers = new[]
                    {
                        new SpellLayer
                        {
                            id = "core",
                            render = "sprite",
                            place = "caster-centre",
                            at = "release",
                            travelSeconds = 0.30f,
                            path = "Spells/frost_flare",
                            seconds = 0.52f,
                            until = "once",
                            size = 190f,
                            facing = "auto",
                            sort = "effects",
                        },
                        new SpellLayer
                        {
                            id = "trail",
                            render = "still",
                            place = "layer:core",
                            follow = true,
                            at = "release",
                            path = "Spells/frost_flare",
                            until = "hold",
                            dx = -118f,
                            scale = 0.7f,
                            facing = "auto",
                            sort = "effects",
                        },
                    },
                },
            };
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

            HoldTheClockAtTheCast();
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

            HoldTheClockAtTheCast();
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

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(beat);
            yield return null;

            Assert.Less(_player.Image.rectTransform.localScale.x, 0f,
                "fixture: the cast was not mirrored, so this proves nothing about mirroring");

            // Mirrored, 0.75-from-the-left is drawn 0.75 from the RIGHT, so the
            // box sits 95 to the right of what it is aimed at rather than 95 to
            // the left. Read at the END of the flight, which is where the
            // arrival is placed.
            //
            // MOVED TO A NAMED INSTANT RATHER THAN WAITED OUT, and the instant
            // matters: 0.5s is PAST the arrival (frame 13 of 26 over 0.65s is
            // 0.3s) and BEFORE the layer's own end. A released renderer keeps
            // the last position it was given rather than being moved home, so
            // sampling after the end would read whatever the final tick left --
            // which is the launch, not the arrival, for a cast whose last tick
            // was its first.
            HoldTheClockAt(0.5f);
            yield return null;

            Assert.AreEqual(hero + 95f, _player.Image.rectTransform.anchoredPosition.x, 2f,
                "the mirrored cast corrected the same way an unmirrored one does, which puts the " +
                "impact twice as far off as leaving it uncorrected would have");

            // AND PAST THE LAYER'S OWN END, 0.65s, so the cast releases what it
            // held. Asserted at a named instant for the same reason the
            // position above is: waiting the flight out at 60x is a race
            // against one batchmode frame, not a measurement of an ending.
            HoldTheClockAt(0.7f);
            yield return null;

            Assert.IsFalse(_player.IsPlaying,
                "the cast was still drawing 0.7s in, past the 0.65s its own sheet authors");
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

        // ---- fit: target -----------------------------------------------------

        private CombatBeat FitTargetBeat()
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = hero,
                Target = foe,
                Vfx = new SpellPresentation
                {
                    layerFormat = 1,
                    layers = new[]
                    {
                        new SpellLayer
                        {
                            render = "sprite",
                            place = "target-centre",
                            at = "release",
                            path = "Vfx/impact_burst",
                            seconds = 0.24f,
                            size = 200f,
                            fit = "target",
                        },
                    },
                },
            };
        }

        // PINNED AS A RATIO, NOT AGAINST A COMPUTED EXPECTED SIZE -- a test
        // that multiplied `size` by FightStageAnchors.SlotScale itself would
        // be restating BoxForLayer's own formula rather than checking it
        // (docs/CODE_STANDARDS.md gotcha 5). A single front-row enemy carries
        // the identical rank-depth term at both stageScale values, so it
        // cancels out of the ratio and only the authored stageScale is left
        // to explain the difference.
        [UnityTest]
        public IEnumerator FitTargetScalesTheBoxByTheStruckTargetsStageFootprint()
        {
            const float bigStageScale = 1.45f;

            yield return LoadFight(1, 1f);
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(FitTargetBeat());
            yield return null;
            float atOne = _player.Image.rectTransform.sizeDelta.x;

            yield return LoadFight(1, bigStageScale);
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(FitTargetBeat());
            yield return null;
            float atBig = _player.Image.rectTransform.sizeDelta.x;

            Assert.AreEqual(bigStageScale, atBig / atOne, 0.02f,
                "fit: target must scale the box by exactly the target's authored stageScale -- Thorn " +
                "Tithe's ritual on an Elder Treant (stageScale 1.45) must cover its body, not read as a " +
                "patch sized for a rat");
        }

        // ---- the target-bounds contract, against real art ----------------------

        private CombatBeat OneLayerBeat(SpellLayer layer, float hitCue = 0.3f)
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat
            {
                Actor = hero,
                Target = foe,
                Vfx = new SpellPresentation { layerFormat = 1, hitCueSeconds = hitCue, layers = new[] { layer } },
            };
        }

        // THE BODY, NOT THE CANVAS AND NOT stageScale. Rat idle is 391 x 270
        // opaque on a 616 x 306 canvas; treant idle is 348 x 441 on 646 x 478.
        // Both alone in the front rank, so the rank depth cancels and the
        // ratio is the larger opaque extent times stageScale:
        //
        //   body    441 x 1.45 / 391 = 1.6355
        //   canvas  646 x 1.45 / 616 = 1.5206   (what a slot-rect fit gave)
        //   old     1.45                         (the stageScale multiplier)
        [UnityTest]
        public IEnumerator FitTargetScalesByTheVisibleBodyOfRealArt()
        {
            var layer = new SpellLayer
            {
                render = "sprite", place = "target-centre", at = "release",
                path = "Vfx/impact_burst", seconds = 0.24f, size = 200f, fit = "target",
            };

            yield return LoadFight(1, 1f, "Enemies/rat");
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(OneLayerBeat(layer));
            yield return null;
            float onRat = _player.Image.rectTransform.sizeDelta.x;

            yield return LoadFight(1, 1.45f, "Enemies/treant");
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(OneLayerBeat(layer));
            yield return null;
            float onTreant = _player.Image.rectTransform.sizeDelta.x;

            Assert.AreEqual(1.6355f, onTreant / onRat, 0.02f,
                "fit: target must follow the struck body's drawn extent -- the treant's height, the " +
                "rat's width -- not its canvas (1.52) and not its stageScale alone (1.45)");
        }

        [UnityTest]
        public IEnumerator ATargetCentreLayerLandsOnTheDrawnBodyNotTheCanvas()
        {
            yield return LoadFight(1, 1f, "Enemies/rat");

            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);
            var body = _fight.BodyOfForTest(foe);
            var slot = SlotOf("Front");

            // 391 opaque of a 616 canvas: if the body is not clearly narrower
            // than the slot, the art never loaded and this measures the
            // fallback instead of the contract.
            Assume.That(body.Width / slot.rect.width, Is.LessThan(0.8f * Mathf.Abs(slot.localScale.x)),
                "fixture: the rat's art did not load, so this measures the slot fallback");

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(OneLayerBeat(new SpellLayer
            {
                render = "sprite", place = "target-centre", at = "release",
                path = "Vfx/impact_burst", seconds = 0.24f,
            }));
            yield return null;

            var at = _player.Image.rectTransform.anchoredPosition;
            Assert.AreEqual(body.Centre.X, at.x, 1.5f, "target-centre missed the drawn body horizontally");
            Assert.AreEqual(body.Centre.Y, at.y, 1.5f, "target-centre missed the drawn body vertically");
            Assert.Greater(Mathf.Abs(at.x - SlotXOf("Front")), 10f,
                "the rat is drawn 38.5px left of its canvas centre; an aim on the slot would not see that");
        }

        // AMASS IN THE AIR: between the two, above the target's head.
        [UnityTest]
        public IEnumerator ASkyLayerAmassesBetweenCasterAndTargetAboveTheTarget()
        {
            yield return LoadFight(1, 1f, "Enemies/rat");

            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);
            var body = _fight.BodyOfForTest(foe);
            float casterX = SlotXOf("Shawn");

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(OneLayerBeat(new SpellLayer
            {
                render = "sprite", place = "sky", at = "release",
                path = "Vfx/impact_burst", seconds = 0.24f,
            }));
            yield return null;

            var at = _player.Image.rectTransform.anchoredPosition;
            Assert.Greater(at.x, Mathf.Min(casterX, body.Centre.X) + 50f, "the air point is not between the two");
            Assert.Less(at.x, Mathf.Max(casterX, body.Centre.X) - 50f, "the air point is not between the two");
            Assert.Greater(at.y, body.Top + 50f, "the air point is not above the target -- it amassed on the body");
        }

        // FIRED FROM THE AIR, TIP FIRST. The tip is read back through the
        // renderer's own transform -- mirror, rotation and all -- so this
        // checks the drawn result, not SpellFlight's arithmetic a second time.
        [UnityTest]
        public IEnumerator AFiredSkyLayerTurnsOntoItsFlightAndLandsItsTipOnTheBody()
        {
            yield return LoadFight(1, 1f, "Enemies/rat");

            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);
            var body = _fight.BodyOfForTest(foe);

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(OneLayerBeat(new SpellLayer
            {
                render = "still", place = "sky", at = "release",
                path = "Vfx/impact_burst", seconds = 0.5f,
                travelSeconds = 0.2f, orient = "path", artDegrees = 0f,
                impactX = 1f, impactY = 0.5f,
            }, hitCue: 0.2f));
            yield return null;

            HoldTheClockAt(0.3f);
            yield return null;

            var rect = _player.Image.rectTransform;
            float turn = Mathf.DeltaAngle(0f, rect.localEulerAngles.z);
            Assert.Less(turn, -5f, "a sheet painted pointing right, fired down at the rat, was not turned down");

            var parent = _player.transform.parent;
            var tip = parent.InverseTransformPoint(
                rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.center.y, 0f)));
            Assert.AreEqual(body.Centre.X, tip.x, 2f, "the tip did not land on the body");
            Assert.AreEqual(body.Centre.Y, tip.y, 2f, "the tip did not land on the body");
        }

        // fit: none (the default) is the case every OTHER test in this file
        // already pins by never authoring the word -- AnOrdinaryEffectAfterA
        // MirroredOneIsNotItselfMirrored alone asserts sizeDelta.x == 380
        // against the unauthored default, on a stageScale-1 fixture. Nothing
        // here restates that; this suite's whole addition is the multiplier.

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

            // STEPPED THROUGH A REAL CAST rather than driven at the renderer.
            // The renderer no longer owns a schedule to be "playing" against --
            // the module decides which frame and how far into its dissolve, and
            // the renderer draws that answer -- so sampling means holding the
            // module's clock and walking it. Which is also what makes the walk
            // deterministic: every sample below lands where it means to instead
            // of wherever the next frame happened to arrive.
            var beat = AimedBeat(impactX: 0.5f, impactY: 0.25f);
            float start = Time.time;
            float clock = start;
            SpellPerformancePlayer.ClockOverride = () => clock;

            _fight.PlaySpellVfxForTest(beat);

            var fade = FadeLayer();
            Assert.IsNotNull(fade, "the player has no dissolve layer - see FightScreen.BuildSpellVfx");

            bool sawBlend = false;
            bool sawClean = false;

            // The cast's whole scaled budget, in forty steps -- enough that
            // several fall inside a frame's held 55% and several inside its
            // dissolving 45%.
            float budget = FightBeatPlayer.Scaled(0.52f);
            for (int i = 0; i <= 40; i++)
            {
                clock = start + budget * i / 40f;
                _fight.PerformancePlayerForTest.Tick(clock);

                if (fade.enabled && fade.color.a > 0.05f && fade.color.a < 0.95f) sawBlend = true;

                // AND IT IS NOT ALWAYS BLENDING. A frame that dissolves from the
                // moment it appears is never itself: halfway through, two
                // drawings share the screen at half strength and neither is
                // legible, which for a lightning bolt means two bolts.
                if (!fade.enabled || fade.color.a <= 0.01f) sawClean = true;
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

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(AimedBeat(impactX: 0.5f, impactY: 0.25f));
            yield return null;

            _player.StopImmediately();

            Assert.IsFalse(_player.Image.enabled);
            Assert.IsFalse(FadeLayer().enabled, "the dissolve layer is still drawing a frame of a stopped spell");
        }

        // ---- the shared ground layer ----------------------------------------------
        //
        // ONE FAULT, HOWEVER MANY ENEMIES, and it is the "one" that needs a
        // test. The per-target pool draws one effect per thing struck, which is
        // the behaviour the splash tests above pin; the ground layer is the
        // opposite rule living beside it, and the two are one loop apart.

        private const string GroundPath = "Spells/cinderfault_ground";
        private const string EruptionPath = "Spells/cinderfault_eruption";

        private static CombatBeat TwoLayerBeat(CombatantState actor, List<CombatantState> struck)
        {
            return new CombatBeat
            {
                Actor = actor,
                Target = struck[0],
                SplashTargets = struck.Skip(1).ToList(),
                Vfx = new SpellPresentation
                {
                    path = EruptionPath,
                    seconds = 0.78f,
                    impactFrame = 5,
                    impactX = 0.5f,
                    impactY = 0.129f,
                    groundPath = GroundPath,
                    groundImpactY = 0.063f,
                },
            };
        }

        private SpellVfxPlayer Ground => _fight.GroundVfxPlayerForTest;

        private List<SpellVfxPlayer> DrawnEruptions() =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => p != null && !ReferenceEquals(p, Ground))
                .Where(p => p.Image != null && p.Image.enabled)
                .ToList();

        [UnityTest]
        public IEnumerator AFullFormationGetsThreeEruptionsAndExactlyOneFault()
        {
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, enemies.Count,
                "the fixture did not field a full stage, so this would prove nothing about a formation");

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, enemies));
            yield return null;

            Assert.IsNotNull(Ground, "the scene has no shared ground layer");
            Assert.IsTrue(Ground.Image.enabled, "the fault was never drawn");
            Assert.AreEqual(enemies.Count, DrawnEruptions().Count,
                "one eruption per enemy standing in the fault");
        }

        // PARTIAL FORMATIONS. Empty and dead slots are simply not in the beat's
        // target list, so the count of eruptions follows the list rather than
        // the stage -- and the fault still appears exactly once.
        [UnityTest]
        public IEnumerator ALoneEnemyGetsOneEruptionAndStillOneFault()
        {
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, new List<CombatantState> { enemies[0] }));
            yield return null;

            Assert.AreEqual(1, DrawnEruptions().Count,
                "two empty slots must not be given eruptions of their own");
            Assert.IsTrue(Ground.Image.enabled, "one enemy is still standing on a fault");
        }

        // THE OTHER HALF OF THE SAME GUARANTEE, and the test AUDIT.md #61 was
        // missing: the two above prove N eruptions and one fault are drawn AT
        // THE IMPACT INSTANT, and nothing proved they all go away again.
        //
        // Both halves are asserted here against the same cast, which is what
        // makes this the regression test for #61 rather than a new feature
        // test. The count is a function of WHERE THE CLOCK IS -- three drawn
        // while it is held at the cast, none once it is past the end. A test
        // that waits a frame instead of holding the clock is sampling that same
        // function at an instant it does not control, which is why one run
        // counted three eruptions and a fault, and the next counted three
        // eruptions and no fault on identical source.
        [UnityTest]
        public IEnumerator EveryLayerIsDrawnAtTheImpactInstantAndNoneSurvivesTheBeat()
        {
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, enemies));
            yield return null;

            Assert.AreEqual(enemies.Count, DrawnEruptions().Count,
                "held at the cast, one eruption per enemy has to be on screen");
            Assert.IsTrue(Ground.Image.enabled, "held at the cast, the fault has to be on screen");

            RunTheClockPastTheEnd();
            yield return null;
            yield return null;

            Assert.IsEmpty(DrawnEruptions(),
                "an eruption is still drawn after its own sequence ended");
            Assert.IsFalse(Ground.Image.enabled,
                "the fault is still drawn after its own sequence ended");
            Assert.IsFalse(Ground.IsPlaying,
                "the ground layer's coroutine outlived the beat it was started for");
        }

        // THE FAULT IS SIZED TO THE FORMATION, not to a slot and not to a
        // constant. Asserted as a RELATION between two casts rather than
        // against a pixel figure: the exact span depends on where the stage
        // anchored its slots, which is not a number this test should own -- but
        // three enemies must produce a wider fault than one, or the size is
        // coming from somewhere other than the enemies.
        [UnityTest]
        public IEnumerator TheFaultIsWiderUnderThreeEnemiesThanUnderOne()
        {
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, new List<CombatantState> { enemies[0] }));
            yield return null;
            float alone = Ground.Image.rectTransform.sizeDelta.x;

            LetTheLastCastFinish();
            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, enemies));
            yield return null;
            float formation = Ground.Image.rectTransform.sizeDelta.x;

            Assert.Greater(formation, alone,
                "the fault is the same width for one enemy as for three, so it is not measured off the slots");
            Assert.Greater(alone, 0f, "a fault of no width is a zero-sized graphic");
        }

        // THE PLACEMENT ITSELF, not just the width relation above -- two things
        // that both have to hold in the SAME cast: the box has to physically
        // REACH every enemy standing in it (a fault that is merely wider for
        // three than for one can still sit centred on the wrong one), and it
        // has to sit near the formation's own ground line rather than however
        // tall the sheet's own canvas happens to be.
        //
        // A FIXED TOLERANCE FOR THE GROUND LINE, not the exact arithmetic:
        // the box's bottom edge sits the sheet's own authored bottom margin
        // (groundImpactY times the box's own height) below the ground line,
        // and recomputing that formula here to check itself is exactly the
        // tautology CLAUDE.md gotcha 5 warns against -- a bug in the formula
        // would reproduce itself in the test. A fixed bound is not: at the
        // square aspect this pins against (tools/screenshots/runtime/
        // cinderfault/unlabelled/f6.png and f10.png, 2026-09-05), the ground
        // sheet's uncropped 512x512 canvas held content only ~40% as tall as
        // it was wide, so a three-enemy formation's box came out several
        // hundred units on a side and put this edge 70+ units off the ground
        // line. 40 fails that and passes the aspect the sheet was cropped to
        // (see content_crop in tools/slice_spell_sheet.py).
        [UnityTest]
        public IEnumerator TheFaultReachesEveryEnemyAndSitsNearTheGroundLine()
        {
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var parent = _player.transform.parent;

            float CentreX(CombatantState c) =>
                parent.InverseTransformPoint(_fight.SlotForTest(c).TransformPoint(Vector3.zero)).x;
            float GroundY(CombatantState c)
            {
                var slot = _fight.SlotForTest(c);
                return parent.InverseTransformPoint(slot.TransformPoint(new Vector3(0f, slot.rect.yMin, 0f))).y;
            }

            float leftmost = enemies.Min(CentreX);
            float rightmost = enemies.Max(CentreX);
            float averageGround = enemies.Average(GroundY);

            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, enemies));
            yield return null;

            var rect = Ground.Image.rectTransform;
            float left = rect.anchoredPosition.x - rect.sizeDelta.x * 0.5f;
            float right = rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f;
            float bottom = rect.anchoredPosition.y - rect.sizeDelta.y * 0.5f;

            Assert.LessOrEqual(left, leftmost + 1f,
                "the fault's left edge does not reach the leftmost enemy's own slot centre");
            Assert.GreaterOrEqual(right, rightmost - 1f,
                "the fault's right edge does not reach the rightmost enemy's own slot centre");
            Assert.Less(Mathf.Abs(bottom - averageGround), 40f,
                "the fault's box does not sit near the formation's own ground line -- " +
                "see content_crop in tools/slice_spell_sheet.py for the aspect bug this pins");
        }

        // A SPELL THAT AUTHORS NO GROUND LAYER MUST DRAW NONE. Every spell in
        // the game but one is in this case, and the whole claim that the new
        // fields are inert rests on it.
        [UnityTest]
        public IEnumerator AnOrdinarySpellDrawsNoGroundLayerAtAll()
        {
            yield return LoadFight();

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            // HELD, so "no ground layer" is proven rather than inferred from
            // an effect that had already finished by the time anyone looked.
            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(new CombatBeat
            {
                Actor = hero,
                Target = foe,
                Vfx = new SpellPresentation { path = "Spells/frost_flare", seconds = 0.52f, impactFrame = 5 },
            });
            yield return null;

            Assert.IsFalse(Ground.Image.enabled,
                "a single-layer spell lit the shared ground layer");
        }

        // BOTH LAYERS RUPTURE ON THE SAME INSTANT, which is what makes the
        // damage number, the flash and the rock landing one event rather than
        // three. Measured off the sliced sequences themselves rather than
        // asserted about the content: the frame COUNT is a property of
        // tools/slice_spell_sheet.py's recipe, and a recipe edit that changed
        // one sequence's length and not the other's is exactly what would break
        // this without touching a single number in skills.json.
        [Test]
        public void TheGroundAndTheEruptionRuptureTogether()
        {
            var ground = FrameSequenceLoader.Load(GroundPath);
            var eruption = FrameSequenceLoader.Load(EruptionPath);

            Assert.IsNotNull(ground, GroundPath + " resolves to no frames");
            Assert.IsNotNull(eruption, EruptionPath + " resolves to no frames");
            Assert.AreEqual(9, ground.Length, "the ground recipe composes nine frames");
            Assert.AreEqual(9, eruption.Length, "the eruption recipe composes nine, so one impactFrame drives both");

            // Frame 5 of 9 in both, so the same fraction of the same duration.
            Assert.AreEqual(CombatBeat.ImpactFraction(5, ground.Length),
                CombatBeat.ImpactFraction(5, eruption.Length), 0.0001f,
                "the two layers no longer land on the same instant");
        }

        // FLUSH RECLAIMS BOTH POOLS. The ground layer is a separate node with a
        // separate lifetime, so an abandoned fight would otherwise leave a fault
        // frozen mid-rupture behind an empty stage.
        [UnityTest]
        public IEnumerator FlushReleasesTheGroundLayerAndEveryEruption()
        {
            yield return LoadFight(FightHudSpec.StageSlotsPerSide);

            var enemies = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(TwoLayerBeat(hero, enemies));
            yield return null;

            Assert.IsTrue(Ground.Image.enabled, "nothing was playing, so this would prove nothing about EndFight");

            // ENDFIGHT RATHER THAN FLUSH, and the name change is the point.
            // Flush supersedes a playback and runs at the start of the NEXT
            // round, where killing a living tail is the beat-end cleanup this
            // design removes. Abandoning a fight is what EndFight is, and
            // abandoning a fight is what this test is about -- the header and
            // every assertion below are unchanged.
            _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true).EndFight();

            Assert.IsFalse(Ground.Image.enabled, "the fault is still drawing over an abandoned fight");
            Assert.IsEmpty(DrawnEruptions(), "an eruption is still drawing over an abandoned fight");
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
