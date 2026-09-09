using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // The manifest's defaulting rules, tested without a texture, a Resources
    // folder or a running scene -- which is the reason StanceManifest deals in
    // sprite PATHS rather than in Sprites.
    //
    // These rules are where a manifest can still go wrong quietly: a row left
    // behind by a re-slice, a sentinel read as a value. Whether an actor HAS an
    // entry at all is asked of real content by
    // EnemyStanceCaptureTests.EveryKitIsInTheStanceManifest.
    public class StanceManifestTests
    {
        private static StanceManifest Build(params RawStanceActor[] actors)
        {
            return new StanceManifest(new RawStanceManifest { actors = new List<RawStanceActor>(actors) });
        }

        private static RawStanceActor Actor(string path, float groundLine)
        {
            return new RawStanceActor { spritePath = path, groundLine = groundLine };
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

        // ---- who owns the ground line ------------------------------------------

        // ABSENT MEANS AUTHORED, and this is the test that makes adding the
        // field a no-op for everything written before it existed. Every entry
        // in the shipped manifest predates it; if absence read as "slicer" they
        // would all silently become numbers a tool may overwrite.
        [Test]
        public void AnEntryWithNoGroundLineSource_IsAuthored()
        {
            var manifest = Build(Actor("Enemies/golem", 69f));

            Assert.AreEqual(StanceManifest.AuthoredSource, manifest.GroundLineSourceFor("Enemies/golem"));
            Assert.AreEqual(StanceManifest.AuthoredSource, manifest.GroundLineSourceFor("Enemies/nobody"),
                "an actor with no entry at all is not a number any tool owns either.");
        }

        [Test]
        public void ASlicerOwnedGroundLine_SaysSo_RegardlessOfCasing()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Enemies/treant",
                groundLine = 8f,
                groundLineSource = "  Slicer ",
            });

            Assert.AreEqual(StanceManifest.SlicerSource, manifest.GroundLineSourceFor("Enemies/treant"),
                "the value is a word in a hand-edited file, so surrounding space and casing must not decide " +
                "whether the next re-slice is allowed to update the number.");
        }

        // The safe direction for a value nobody recognises is "nothing writes
        // over it". The loud direction -- refusing the typo -- is
        // StanceManifestValidationTests' job, so the runtime keeps degrading
        // gracefully and the author still gets told.
        [Test]
        public void AnUnrecognisedGroundLineSource_ReadsAsAuthored()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Enemies/rat",
                groundLine = 8f,
                groundLineSource = "slicerr",
            });

            Assert.AreEqual(StanceManifest.AuthoredSource, manifest.GroundLineSourceFor("Enemies/rat"));
        }

        // The runtime does not care who wrote the number -- it stands the
        // figure on it either way. Pinned so a future change cannot make the
        // ownership field quietly change where a creature's feet land.
        [Test]
        public void TheSourceDoesNotChangeTheGroundLineItself()
        {
            var slicer = Build(new RawStanceActor
            {
                spritePath = "Enemies/treant",
                groundLine = 8f,
                groundLineSource = "slicer",
            });
            var authored = Build(Actor("Enemies/treant", 8f));

            Assert.AreEqual(authored.GroundLineFor("Enemies/treant"),
                slicer.GroundLineFor("Enemies/treant"), 0.001f);
        }

        // ---- how hard an actor breathes ----------------------------------------

        // FULL AMPLITUDE UNLESS SOMEBODY SAYS OTHERWISE. Every stance in the
        // game is a single drawing, so the transform breath is the only thing
        // moving an idle figure and has nothing to compete with.
        [Test]
        public void AnActorWithNoAuthoredBreath_TakesTheFullAmplitude()
        {
            var manifest = Build(Actor("Enemies/rat", 8f));

            Assert.AreEqual(StanceManifest.DefaultBreath, manifest.BreathFor("Enemies/rat"), 0.0001f);
        }

        [Test]
        public void AnAuthoredBreath_OverridesTheDefault()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Enemies/forest_warden",
                groundLine = 8f,
                breath = 0.7f,
            });

            Assert.AreEqual(0.7f, manifest.BreathFor("Enemies/forest_warden"), 0.0001f);
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
            });

            Assert.AreEqual(0f, manifest.BreathFor("Enemies/statue"), 0.0001f);
        }

        [Test]
        public void AnUnknownActor_BreathesTheDefaultRatherThanThrowing()
        {
            var manifest = Build(Actor("Enemies/golem", 69f));

            Assert.AreEqual(StanceManifest.DefaultBreath, manifest.BreathFor("Enemies/nobody"), 0.0001f);
            Assert.AreEqual(StanceManifest.DefaultBreath, manifest.BreathFor(null), 0.0001f);
        }

        // ---- rows that should not cost the whole file --------------------------

        [Test]
        public void AMalformedEntry_IsSkippedRatherThanThrowing()
        {
            var manifest = new StanceManifest(new RawStanceManifest
            {
                actors = new List<RawStanceActor>
                {
                    null,
                    new RawStanceActor { spritePath = "", groundLine = 5f },
                    Actor("Enemies/golem", 69f),
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
            Assert.AreEqual(StanceManifest.DefaultBreath, manifest.BreathFor("Enemies/golem"), 0.0001f);
        }

        // ---- whether an actor flies --------------------------------------------

        // Every actor but one is grounded, and says nothing about it: an
        // absent hover block is the default, not an error.
        [Test]
        public void AnActorWithNoHoverBlock_IsGrounded()
        {
            var manifest = Build(Actor("Enemies/rat", 8f));

            Assert.IsFalse(manifest.HoverFor("Enemies/rat").IsAirborne);
            Assert.IsFalse(manifest.HoverFor("Enemies/nobody").IsAirborne);
        }

        [Test]
        public void AnAuthoredHover_ComesBackWithItsNumbers()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Characters/owl",
                groundLine = 8f,
                hover = new RawHover { height = 70f, bob = 10f, periodSeconds = 2.4f },
            });

            var spec = manifest.HoverFor("Characters/owl");
            Assert.IsTrue(spec.IsAirborne);
            Assert.AreEqual(70f, spec.Height, 0.0001f);
            Assert.AreEqual(10f, spec.Bob, 0.0001f);
            Assert.AreEqual(2.4f, spec.PeriodSeconds, 0.0001f);
        }

        // A block that is present but all zero is what JsonUtility hands back
        // for `"hover": {}` -- grounded, the same as absent.
        [Test]
        public void AnEmptyHoverBlock_IsGrounded()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Characters/owl",
                groundLine = 8f,
                hover = new RawHover(),
            });

            Assert.IsFalse(manifest.HoverFor("Characters/owl").IsAirborne);
        }

        // ---- where a cast leaves this actor's body -----------------------------

        // NULL, not a zeroed struct -- an unauthored actor has to be
        // DISTINGUISHABLE from one authored at (0, 0), because the seam that
        // reads this falls back to a different answer (the caster's own slot
        // origin) only when nothing was said at all.
        [Test]
        public void AnActorWithNoCastPoint_ResolvesToNull()
        {
            var manifest = Build(Actor("Enemies/rat", 8f));

            Assert.IsFalse(manifest.CastPointFor("Enemies/rat").HasValue);
            Assert.IsFalse(manifest.CastPointFor("Enemies/nobody").HasValue);
        }

        // Odette's own numbers (StanceManifest.json), pinned so a future edit
        // to the manifest or its resolver cannot silently move where her
        // spells leave from without a test noticing.
        [Test]
        public void AnAuthoredCastPoint_ComesBackWithItsNumbers()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Characters/owl",
                groundLine = 8f,
                castPoint = new RawCastPoint { dx = 201.5f, dy = 135.5f },
            });

            var point = manifest.CastPointFor("Characters/owl");
            Assert.IsTrue(point.HasValue);
            Assert.AreEqual(201.5f, point.Value.Dx, 0.0001f);
            Assert.AreEqual(135.5f, point.Value.Dy, 0.0001f);
        }

        [Test]
        public void PathsForCastPoint_MatchRegardlessOfStraySlashesOrCasing()
        {
            var manifest = Build(new RawStanceActor
            {
                spritePath = "Characters/owl",
                groundLine = 8f,
                castPoint = new RawCastPoint { dx = 201.5f, dy = 135.5f },
            });

            Assert.AreEqual(201.5f, manifest.CastPointFor("/characters/OWL/").Value.Dx, 0.0001f);
        }
    }
}
