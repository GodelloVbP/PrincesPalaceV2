using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // The manifest's defaulting and clamping rules, tested without a texture,
    // a Resources folder or a running scene -- which is the reason StanceManifest
    // takes a frame COUNT rather than a Sprite[].
    //
    // These rules are where a manifest can still go wrong quietly: an entry
    // that half-specifies a stance, or one left behind by a re-slice. The
    // pixel-level "does this match the art" question belongs to
    // StanceManifestValidationTests, which needs real textures.
    public class StanceManifestTests
    {
        private static StanceManifest Build(params RawStanceActor[] actors)
        {
            return new StanceManifest(new RawStanceManifest { actors = new List<RawStanceActor>(actors) });
        }

        private static RawStanceActor Actor(string path, float groundLine, params RawStanceTiming[] stances)
        {
            return new RawStanceActor
            {
                spritePath = path,
                groundLine = groundLine,
                stances = new List<RawStanceTiming>(stances),
            };
        }

        [Test]
        public void AnAuthoredGroundLine_IsReturnedExactly()
        {
            var manifest = Build(Actor("Enemies/golem", 69f));

            Assert.AreEqual(69f, manifest.GroundLineFor("Enemies/golem"), 0.001f,
                "The authored number IS the answer -- rounding or adjusting it here would move a figure that " +
                "someone deliberately placed.");
        }

        // A fractional ground line is a real measurement landing between two
        // pixels, not a mistake. Rounding it would shift every figure that
        // uses one, which is the class of accident this file exists to stop.
        [Test]
        public void AFractionalGroundLine_SurvivesIntact()
        {
            var manifest = Build(Actor("Enemies/bog_witch", 10.5f));

            Assert.AreEqual(10.5f, manifest.GroundLineFor("Enemies/bog_witch"), 0.001f);
        }

        [Test]
        public void AnUnknownActor_FallsBackToTheCanvasBottom()
        {
            var manifest = Build(Actor("Enemies/golem", 69f));

            Assert.AreEqual(StanceManifest.DefaultGroundLine, manifest.GroundLineFor("Enemies/nobody"), 0.001f,
                "An actor with no entry stands on its own canvas bottom -- the assumption the stage made before " +
                "the manifest existed. Deliberately not 'measure it instead': a gap should be a visible one the " +
                "validator names, not something a heuristic hides.");
            Assert.IsFalse(manifest.HasActor("Enemies/nobody"),
                "...and the gap has to be reportable, which is what the validator asks.");
        }

        [Test]
        public void PathsMatch_RegardlessOfStraySlashesOrCasing()
        {
            var manifest = Build(Actor("Enemies/golem", 69f));

            Assert.AreEqual(69f, manifest.GroundLineFor("/Enemies/golem/"), 0.001f,
                "A path typed into JSON and one built by concatenation differ by slashes, and that must not decide " +
                "whether a golem stands on the floor.");
            Assert.AreEqual(69f, manifest.GroundLineFor("enemies/GOLEM"), 0.001f);
        }

        [Test]
        public void AnAuthoredStance_UsesItsOwnTiming()
        {
            var manifest = Build(Actor("Enemies/golem", 69f,
                new RawStanceTiming { stance = "attack", secondsPerFrame = 0.05f, impactFrame = 4, soundFrame = 2 }));

            var timing = manifest.TimingFor("Enemies/golem", "attack", 6);

            Assert.AreEqual(0.05f, timing.SecondsPerFrame, 0.001f);
            Assert.AreEqual(4, timing.ImpactFrame);
            Assert.AreEqual(2, timing.SoundFrame);
        }

        // The midpoint used to be the only answer, applied to everything. It
        // survives ONLY as the unauthored fallback: "frame 3 of 6" was never a
        // fact about the art, just an average that happened to be tolerable.
        [Test]
        public void AnUnauthoredStance_FallsBackToTheOldMidpointGuess()
        {
            var manifest = Build(Actor("Characters/sheep", 43f));

            var timing = manifest.TimingFor("Characters/sheep", "attack", 6);

            Assert.AreEqual(StanceManifest.DefaultSecondsPerFrame, timing.SecondsPerFrame, 0.001f);
            Assert.AreEqual(3, timing.ImpactFrame, "ceil(6/2) -- the behaviour this replaced.");
        }

        // Half-specifying a stance is the likely authoring slip: someone sets
        // the pace and forgets the impact frame. The missing half must default
        // rather than come back as zero, which would fire the impact instantly.
        [Test]
        public void AHalfSpecifiedStance_DefaultsOnlyTheMissingHalf()
        {
            var manifest = Build(Actor("Enemies/rat", 12f,
                new RawStanceTiming { stance = "attack", secondsPerFrame = 0.04f }));

            var timing = manifest.TimingFor("Enemies/rat", "attack", 6);

            Assert.AreEqual(0.04f, timing.SecondsPerFrame, 0.001f, "The authored half stands.");
            Assert.AreEqual(3, timing.ImpactFrame,
                "The unauthored half defaults. Zero here would mean 'impact before frame 1', i.e. the blow landing " +
                "before the swing has drawn a single frame.");
            Assert.AreEqual(3, timing.SoundFrame);
        }

        // The staleness case: art re-sliced from six frames to three, manifest
        // not updated. Waiting for frame 4 of a 3-frame animation would hang
        // the beat on a frame that never arrives.
        [Test]
        public void AnImpactFramePastTheEnd_IsClampedToTheLastRealFrame()
        {
            var manifest = Build(Actor("Enemies/golem", 69f,
                new RawStanceTiming { stance = "attack", impactFrame = 6, soundFrame = 6 }));

            var timing = manifest.TimingFor("Enemies/golem", "attack", 3);

            Assert.AreEqual(3, timing.ImpactFrame,
                "A re-slice that shortens an animation must not leave the beat waiting on a frame that no longer " +
                "exists. The validator reports the staleness; this stops it being a hang in the meantime.");
            Assert.AreEqual(3, timing.SoundFrame);
        }

        [Test]
        public void ASingleFrameStance_ImpactsOnItsOnlyFrame()
        {
            var manifest = Build(Actor("Enemies/bog_witch", 10.5f));

            var timing = manifest.TimingFor("Enemies/bog_witch", "attack", 1);

            Assert.AreEqual(1, timing.ImpactFrame,
                "Frame 1 of 1 means instantly, which is what the flat-file art did before any of this existed.");
        }

        // ---- how hard an actor breathes ---------------------------------------

        // THE DEFAULT IS A RULE, NOT A CONSTANT, and this is the pair that says
        // so. A single still drawing has nothing else moving it and gets the
        // full amplitude; a sheet that already steps six frames gets a third,
        // so the transform carries the eye between drawings instead of
        // competing with them.
        //
        // Same shape as StanceTiming.Steady defaulting off `idle`: a rule
        // supplies the usual answer so a new actor is right with no authoring
        // at all, and anything that disagrees says so.
        [Test]
        public void AStillDrawingBreathesFullyAndASheetThatMovesBreathesLess()
        {
            var manifest = Build(Actor("Enemies/rat", 12f));

            Assert.AreEqual(1f, manifest.BreathFor("Enemies/rat", 1), 0.0001f,
                "A single idle.png cannot breathe on its own, so the transform is all there is.");
            Assert.AreEqual(BreathCurve.SheetScale, manifest.BreathFor("Enemies/rat", 6), 0.0001f,
                "A six-frame idle is already moving the figure; a full breath on top reads as two " +
                "animations disagreeing.");
        }

        [Test]
        public void AnAuthoredBreath_OverridesTheRuleForEitherKindOfSheet()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Enemies/forest_warden",
                groundLine = 14f,
                breath = 0.7f,
                stances = new List<RawStanceTiming>(),
            });

            Assert.AreEqual(0.7f, manifest.BreathFor("Enemies/forest_warden", 6), 0.0001f);
            Assert.AreEqual(0.7f, manifest.BreathFor("Enemies/forest_warden", 1), 0.0001f);
        }

        // ZERO IS THE UNSET SENTINEL, so "none" has to be said some other way,
        // and negative is it -- see RawStanceActor.breath. It comes back as
        // zero rather than passed through, because a negative amplitude
        // reaching BreathCurve would breathe the figure inside out.
        [Test]
        public void ANegativeBreath_MeansNoneRatherThanAnInvertedOne()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Enemies/statue",
                groundLine = 0f,
                breath = -1f,
                stances = new List<RawStanceTiming>(),
            });

            Assert.AreEqual(0f, manifest.BreathFor("Enemies/statue", 1), 0.0001f);
        }

        [Test]
        public void AnUnknownActor_TakesTheRuleRatherThanThrowing()
        {
            var manifest = Build(Actor("Enemies/golem", 69f));

            Assert.AreEqual(1f, manifest.BreathFor("Enemies/nobody", 1), 0.0001f);
            Assert.AreEqual(BreathCurve.SheetScale, manifest.BreathFor(null, 6), 0.0001f);
        }

        // ---- the idle peak hold ------------------------------------------------

        // Authored endHold rides through the resolver to the timing the runtime
        // reads, alongside everything else on the stance. Zero when nobody says
        // otherwise, which is the symmetric-linger default LoopCycle always had.
        [Test]
        public void AnAuthoredEndHold_ReachesTheResolvedTiming()
        {
            var manifest = Build(Actor("Enemies/beetle", 8f,
                new RawStanceTiming { stance = "idle", secondsPerFrame = 0.2f, endHold = 0.5f }));

            var idle = manifest.TimingFor("Enemies/beetle", "idle", 6);
            Assert.AreEqual(0.5f, idle.EndHoldSeconds, 0.0001f);

            var bare = manifest.TimingFor("Enemies/beetle", "idle", 6);
            Assert.AreEqual(0.2f, bare.SecondsPerFrame, 0.0001f, "the sweep pace is untouched by the hold");
        }

        [Test]
        public void AStanceWithNoEndHold_ResolvesToZeroRatherThanNegative()
        {
            var manifest = Build(Actor("Enemies/rat", 12f,
                new RawStanceTiming { stance = "idle", secondsPerFrame = 0.1f }));

            Assert.AreEqual(0f, manifest.TimingFor("Enemies/rat", "idle", 6).EndHoldSeconds, 0.0001f);
        }

        // ---- a one-shot that returns to its start ------------------------------

        // Shell Up curls in and has to uncurl again; `returns` is what the beat
        // player reads to play the frames back down. Off for everything that
        // does not say so, which is every existing stance.
        [Test]
        public void AnAuthoredReturns_ReachesTheResolvedTiming()
        {
            var manifest = Build(Actor("Enemies/beetle", 8f,
                new RawStanceTiming { stance = "shell_closed", secondsPerFrame = 0.12f, returns = true },
                new RawStanceTiming { stance = "attack", secondsPerFrame = 0.07f }));

            Assert.IsTrue(manifest.TimingFor("Enemies/beetle", "shell_closed", 6).ReturnsToStart,
                "the curl must uncurl -- returns did not survive the resolver");
            Assert.IsFalse(manifest.TimingFor("Enemies/beetle", "attack", 6).ReturnsToStart,
                "a plain one-shot does not play backwards");
        }

        [Test]
        public void AMalformedEntry_IsSkippedRatherThanThrowing()
        {
            var manifest = new StanceManifest(new RawStanceManifest
            {
                actors = new List<RawStanceActor>
                {
                    null,
                    new RawStanceActor { spritePath = "", groundLine = 5f },
                    Actor("Enemies/golem", 69f, null, new RawStanceTiming { stance = "", secondsPerFrame = 1f }),
                },
            });

            Assert.AreEqual(69f, manifest.GroundLineFor("Enemies/golem"), 0.001f,
                "One bad row must not cost the whole manifest -- graceful degradation on missing content is the " +
                "house style, and a stage that silently drops every ground line would be a spectacular version " +
                "of the bug this file prevents.");
        }

        [Test]
        public void ANullManifest_ResolvesToDefaultsRatherThanThrowing()
        {
            var manifest = new StanceManifest(null);

            Assert.AreEqual(StanceManifest.DefaultGroundLine, manifest.GroundLineFor("Enemies/golem"), 0.001f);
            Assert.AreEqual(3, manifest.TimingFor("Enemies/golem", "attack", 6).ImpactFrame);
        }
    }
}
