using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // THE MANIFEST AGAINST THE ART IT DESCRIBES.
    //
    // StanceManifestTests next door pins the DEFAULTING RULES with synthetic
    // rows and no files. This one asks the other half of the question: does the
    // committed Resources/StanceManifest.json still describe the PNGs actually
    // on disk? A re-slice changes a canvas; the number in the file does not
    // change with it, and the symptom is a figure standing a few pixels into
    // the floor -- which nobody notices until a screenshot happens to catch it.
    //
    // HOW THE GROUND LINE IS RE-MEASURED, and why it is a MEDIAN.
    //
    // Per stance: canvas height minus one, minus the lowest row carrying any
    // opaque pixel. That is the same arithmetic slice_actor_sheet.py performs
    // when it composites -- it bottom-aligns every stance's foot row to
    // `ground_y - 1` on a canvas of `canvas_h`, so `canvas_h - 1 - footRow` is
    // the number it prints.
    //
    // Across stances: the MEDIAN, not the minimum, and that choice is the whole
    // reason this file can measure at all. A scan believes whatever it finds,
    // and what it finds is sometimes not the feet -- the golem's slam erupts an
    // earth spike ~50px below its own feet (attack and cast measure 20 against
    // the other four stances' 64) and Shawn's idle plants a staff ~33px below
    // his (idle measures 9 against the other five's 41). Those are exactly the
    // two cases the manifest was created to stop the RUNTIME guessing at, so a
    // validator that took the minimum would re-introduce the bug in the shape
    // of a failing test. A median over an actor's stances discards them,
    // because an actor's feet are where MOST of its drawings put them.
    //
    // THE 8px BAND is deliberately loose. Slicing is not deterministic across a
    // change to the source sheet, an anchor mode or a delivery scale, and a
    // pose-to-pose disagreement of a pixel or two is normal (the slicer's own
    // one-ground-line check allows 6). What the band catches is a number left
    // behind by a re-slice, which moves by tens of pixels, not by one.
    public class StanceManifestValidationTests
    {
        private const float MaxDriftPx = 8f;

        // Below this the sweep is not measuring the roster any more, whatever
        // its result says. Eight actors ship today.
        private const int MinimumActorsMeasured = 5;

        private sealed class Entry
        {
            internal string SpritePath;
            internal float GroundLine;
            internal string Source;
            internal string RawSource;
            internal float? CastPointDx;
            internal float? CastPointDy;
            internal float? HeadSize;
        }

        private static string Root() => RepoTree.Root();

        private static List<Entry> Manifest()
        {
            string path = Path.Combine(Root(), "Assets", "_Project", "Resources", "StanceManifest.json");
            Assert.IsTrue(File.Exists(path), $"no stance manifest at '{path}'");

            var entries = new List<Entry>();
            foreach (string block in JsonBlocks.ObjectsInArray(File.ReadAllText(path), "actors"))
            {
                string sprite = JsonBlocks.String(block, "spritePath");
                if (string.IsNullOrWhiteSpace(sprite)) continue;

                string rawSource = JsonBlocks.HasKey(block, "groundLineSource")
                    ? JsonBlocks.String(block, "groundLineSource")
                    : null;

                string castPointBlock = JsonBlocks.ObjectFor(block, "castPoint");
                string headBlock = JsonBlocks.ObjectFor(block, "head");

                entries.Add(new Entry
                {
                    SpritePath = sprite.Trim().Trim('/'),
                    GroundLine = (float)(JsonBlocks.Number(block, "groundLine") ?? 0d),
                    RawSource = rawSource,
                    Source = string.IsNullOrWhiteSpace(rawSource)
                        ? StanceManifest.AuthoredSource
                        : rawSource.Trim().ToLowerInvariant(),
                    CastPointDx = castPointBlock == null ? (float?)null : (float?)(JsonBlocks.Number(castPointBlock, "dx") ?? 0d),
                    CastPointDy = castPointBlock == null ? (float?)null : (float?)(JsonBlocks.Number(castPointBlock, "dy") ?? 0d),
                    HeadSize = headBlock == null ? (float?)null : (float?)(JsonBlocks.Number(headBlock, "size") ?? 0d),
                });
            }

            Assert.IsNotEmpty(entries, "no actors parsed out of the manifest -- its shape changed under the parse.");
            return entries;
        }

        // ONLY THE TWO WORDS. An unrecognised value resolves to "authored" at
        // runtime, which is the safe direction (nothing overwrites it) and also
        // the silent one: a typo would read as a deliberate override forever.
        // The strictness lives here rather than in StanceManifest so the
        // runtime keeps degrading gracefully and the author still gets told.
        [Test]
        public void EveryGroundLineSourceIsOneOfTheTwoWords()
        {
            var wrong = Manifest()
                .Where(e => e.RawSource != null
                            && e.Source != StanceManifest.SlicerSource
                            && e.Source != StanceManifest.AuthoredSource)
                .Select(e => $"{e.SpritePath} says '{e.RawSource}'")
                .ToList();

            Assert.IsEmpty(wrong,
                "groundLineSource is 'slicer' or 'authored' and nothing else. These entries say something " +
                "else, and the runtime reads every one of them as 'authored' -- which means a typo intended " +
                "to hand the number to the slicer would silently keep it from ever being updated: " +
                string.Join(", ", wrong));
        }

        // EVERY ENEMY FRAMES ITS OWN HEAD (8852f19d). The plate icon crops
        // the idle still to the authored `head` box; without one it falls
        // back to a square off the top of the figure, which frames an upright
        // body and misses anything that is not -- the beetle's head is out
        // front at shoulder height, the treant's face is mid-trunk. A new
        // enemy that forgot the box would ship that fallback silently, so the
        // box is required of every Enemies/ entry, with a positive size.
        [Test]
        public void EveryEnemyAuthorsAHeadBox()
        {
            var enemies = Manifest()
                .Where(e => e.SpritePath.StartsWith("Enemies/", StringComparison.Ordinal))
                .ToList();

            Assert.IsNotEmpty(enemies, "no Enemies/ entries parsed -- the manifest's shape changed under the parse.");

            var missing = enemies
                .Where(e => !e.HeadSize.HasValue || e.HeadSize.Value <= 0f)
                .Select(e => e.HeadSize.HasValue ? $"{e.SpritePath} (size {e.HeadSize.Value})" : e.SpritePath)
                .ToList();

            Assert.IsEmpty(missing,
                "every enemy in StanceManifest.json authors a `head` box {dx, dy, size} (castPoint's convention: " +
                "dx from the canvas centre, dy above the ground line), set by eye on the idle still -- " +
                "the plate icon crops to it. Missing or non-positive: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryAuthoredGroundLineStillMatchesTheArt()
        {
            var stale = new List<string>();
            var unexplained = new List<string>();
            int measured = 0;

            foreach (var entry in Manifest())
            {
                string folder = Path.Combine(Root(), "Assets", "_Project", "Resources",
                    entry.SpritePath.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(folder))
                {
                    // An entry with no art is a different fault, and
                    // EnemyArtCompletenessTests asks it from the content side.
                    continue;
                }

                float? median = MedianGroundLine(folder);
                if (median == null) continue;
                measured++;

                float drift = Math.Abs(entry.GroundLine - median.Value);
                if (drift <= MaxDriftPx) continue;

                string report = $"{entry.SpritePath} authors {entry.GroundLine} but its stances measure " +
                                $"{median.Value} (drift {drift:0.#}px)";

                if (entry.Source == StanceManifest.SlicerSource)
                {
                    stale.Add(report + " -- groundLineSource is 'slicer', so nothing here was ever a judgement: " +
                              "re-run tools/slice_actor_sheet.py for this actor");
                    continue;
                }

                if (!ReadmeExplainsTheGroundLine(entry.SpritePath))
                {
                    unexplained.Add(report);
                }
            }

            Assert.GreaterOrEqual(measured, MinimumActorsMeasured,
                $"only {measured} actors were measured, so this sweep is not covering the roster -- either the " +
                "manifest lost entries or the stills moved out from under Resources/.");

            Assert.IsEmpty(stale,
                "these ground lines are the slicer's own and have drifted away from the art it produced:\n  "
                + string.Join("\n  ", stale));

            Assert.IsEmpty(unexplained,
                "these ground lines override what the art measures by more than " + MaxDriftPx + "px, and " +
                "nothing says why:\n  " + string.Join("\n  ", unexplained) + "\n\n" +
                "An override is allowed and is sometimes the only correct answer -- the golem's slam erupts " +
                "below its feet and a scan reads the spike as the floor. What is not allowed is an override " +
                "nobody wrote a reason for, because it is indistinguishable from a number a re-slice left " +
                "behind. Put a line naming 'groundLine' in the actor's Art/**/README.md, or set " +
                "groundLineSource to 'slicer' and re-slice.");
        }

        // The actor's own README is the register for this, because it is
        // already where the slicer invocation and the delivery numbers live --
        // a reason for the ground line belongs beside the command that
        // measured a different one.
        private static bool ReadmeExplainsTheGroundLine(string spritePath)
        {
            string readme = Path.Combine(Root(), "Assets", "_Project", "Art",
                spritePath.Replace('/', Path.DirectorySeparatorChar), "README.md");

            return File.Exists(readme)
                   && File.ReadAllText(readme).IndexOf("groundLine", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // A castPoint pointing into empty air on its OWN cast still -- a typo
        // in dx or dy, or a point authored against the wrong canvas -- the
        // same class of mistake groundLine's 8px band exists to catch, one
        // axis short of it: a dx or dy off by enough lands the effect
        // nowhere near the actor rather than merely on the wrong pixel of
        // them, and nothing else would ever notice, because an unauthored
        // castPoint is a perfectly valid, silent fallback.
        [Test]
        public void EveryAuthoredCastPointLiesInsideItsCastStillsAlphaBBox()
        {
            var offStage = new List<string>();
            var noCastStill = new List<string>();
            int checkedCount = 0;

            foreach (var entry in Manifest())
            {
                if (entry.CastPointDx == null || entry.CastPointDy == null) continue;

                string castPng = Path.Combine(Root(), "Assets", "_Project", "Resources",
                    entry.SpritePath.Replace('/', Path.DirectorySeparatorChar), "cast.png");

                if (!File.Exists(castPng))
                {
                    noCastStill.Add($"{entry.SpritePath} authors a castPoint but has no cast.png at '{castPng}'");
                    continue;
                }

                var png = PngAlpha.Read(castPng);
                var bbox = png.AlphaBBox();
                if (bbox == null)
                {
                    noCastStill.Add($"{entry.SpritePath}'s cast.png is wholly transparent -- nothing to check a castPoint against");
                    continue;
                }

                checkedCount++;

                // The inverse of RawCastPoint's convention: dx is pixels from
                // the canvas's own horizontal centre, dy is pixels above the
                // actor's groundLine -- so the canvas pixel a castPoint names
                // is (width/2 + dx, height - 1 - (groundLine + dy)), the
                // second term converting "up from the ground line" into the
                // top-down row PngAlpha reads in.
                float x = png.Width / 2f + entry.CastPointDx.Value;
                float canvasYFromBottom = entry.GroundLine + entry.CastPointDy.Value;
                float row = png.Height - 1 - canvasYFromBottom;

                var (left, top, right, bottom) = bbox.Value;
                if (x < left || x > right || row < top || row > bottom)
                {
                    offStage.Add($"{entry.SpritePath} authors castPoint (dx {entry.CastPointDx}, dy {entry.CastPointDy}), " +
                                 $"which lands at canvas pixel ({x:0.#}, {row:0.#}) on a {png.Width}x{png.Height} " +
                                 $"cast.png whose opaque content is x[{left}..{right}] y[{top}..{bottom}] -- " +
                                 "outside the drawing, which reads as a typo in dx, dy or groundLine");
                }
            }

            Assert.IsEmpty(noCastStill,
                "these actors author a castPoint this test could not check against art:\n  " +
                string.Join("\n  ", noCastStill));

            Assert.IsEmpty(offStage,
                "these castPoints land outside their own cast.png's opaque content:\n  " +
                string.Join("\n  ", offStage));

            Assert.Greater(checkedCount, 0,
                "no actor in the manifest authors a castPoint -- if that's still true this assertion should be " +
                "deleted, and if it's not, this sweep silently stopped checking the ones that do");
        }

        private static float? MedianGroundLine(string folder)
        {
            var perStance = new List<float>();

            foreach (string file in Directory.GetFiles(folder, "*.png").OrderBy(f => f, StringComparer.Ordinal))
            {
                var png = PngAlpha.Read(file);
                int lowest = png.LowestOpaqueRow();
                if (lowest < 0) continue;
                perStance.Add(png.Height - 1 - lowest);
            }

            if (perStance.Count == 0) return null;

            perStance.Sort();
            int mid = perStance.Count / 2;
            return perStance.Count % 2 == 1
                ? perStance[mid]
                : (perStance[mid - 1] + perStance[mid]) / 2f;
        }
    }
}
