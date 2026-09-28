using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The sky's arithmetic: where a star sits, and how paging between trees
    // moves.
    //
    // All of it pure, so the whole navigation model is pinned before a single
    // GameObject exists -- which is the point of the layer. The screen and the
    // controller will both call these, and a constellation drawn at build time
    // that disagreed with the one re-anchored at runtime is the exact drift
    // FightSubmenuLayout was extracted to stop.
    public class ConstellationLayoutTests
    {
        // ---- the plots -----------------------------------------------------------
        //
        // HAND-PLOTTED IS THE WHOLE POINT AND ALSO THE WHOLE RISK. The tree was
        // dx x depth, which could not draw a figure but also could not put two
        // stones in the same place. Sixty-three authored coordinate pairs can,
        // and a mistyped one is invisible until somebody looks at that path.
        //
        // So none of this checks a pair. It checks the PROPERTIES the design
        // says every plot must have -- which is what a fourth path would have
        // to satisfy too, and is the only form of this test that survives one
        // being added.

        // ---- the detail rail (balance-bot item 3, 2026-09-03) --------------------
        //
        // Pins the rail's rows in on-screen top-to-bottom ORDER (not just
        // that each box has room), because the regression this closed was an
        // ordering bug -- the kicker printed below the name it was meant to
        // sit above -- that a pure non-overlap check would not have caught on
        // its own.

        [Test]
        public void DetailRailRowsRunTopToBottomInReadingOrder()
        {
            Assert.Greater(ConstellationLayout.PanelHeaderY, ConstellationLayout.PanelPathY);
            Assert.Greater(ConstellationLayout.PanelPathY, ConstellationLayout.PanelKickerY,
                "the kicker is the state, printed ABOVE the name - it must sit higher on screen than the name below it");
            Assert.Greater(ConstellationLayout.PanelKickerY, ConstellationLayout.PanelNameY);
            Assert.Greater(ConstellationLayout.PanelNameY, ConstellationLayout.PanelPriceY);
            Assert.Greater(ConstellationLayout.PanelPriceY, ConstellationLayout.PanelBodyY);
            Assert.Greater(ConstellationLayout.PanelBodyY, ConstellationLayout.PanelRefusalY);
        }

        [Test]
        public void DetailRailRowsDoNotOverlap()
        {
            // Each row's own half-height clearance against the next, positive
            // margin required - see ConstellationLayout's own Gap consts.
            float pathBottom = ConstellationLayout.PanelPathY - 17f;
            float kickerTop = ConstellationLayout.PanelKickerY + 11f;
            Assert.Greater(pathBottom, kickerTop);

            float kickerBottom = ConstellationLayout.PanelKickerY - 11f;
            float nameTop = ConstellationLayout.PanelNameY + 48f;
            Assert.Greater(kickerBottom, nameTop);

            float nameBottom = ConstellationLayout.PanelNameY - 48f;
            float priceTop = ConstellationLayout.PanelPriceY + 13f;
            Assert.Greater(nameBottom, priceTop);

            float priceBottom = ConstellationLayout.PanelPriceY - 13f;
            float bodyTop = ConstellationLayout.PanelBodyY + ConstellationLayout.PanelBodyHeight * 0.5f;
            Assert.Greater(priceBottom, bodyTop);

            float bodyBottom = ConstellationLayout.PanelBodyY - ConstellationLayout.PanelBodyHeight * 0.5f;
            float refusalTop = ConstellationLayout.PanelRefusalY + 30f;
            Assert.Greater(bodyBottom, refusalTop);
        }

        [Test]
        public void DetailRailBodyUsesTheSpaceItActuallyHas()
        {
            // Was a flat 240 -- the same failure mode reported on the pane
            // (item 3): a guessed height that leaves the container's own
            // room unclaimed. Pinned above its old value so a future
            // shrink of the rail is a deliberate edit, not a silent revert.
            Assert.Greater(ConstellationLayout.PanelBodyHeight, 240f);
        }

        // EVERY AUTHORED FIGURE, for every character that has one, plus the
        // spire an unplotted path falls back to. The invariants below walk
        // this rather than a path index, so a character's plots are held to
        // the same floors the moment they are registered -- no per-character
        // copy of any check.
        private static System.Collections.Generic.IEnumerable<(string character, int path)> EveryPlot()
        {
            foreach (var character in ConstellationLayout.PlottedCharacters)
            {
                for (int path = 0; path < ConstellationLayout.PlotCountFor(character); path++)
                {
                    yield return (character, path);
                }
            }

            yield return ("no_such_character", 0);
        }

        private static float X((string character, int path) plot, int slot) =>
            ConstellationLayout.StarX(plot.character, plot.path, slot);

        private static float Y((string character, int path) plot, int slot) =>
            ConstellationLayout.StarY(plot.character, plot.path, slot);

        [Test]
        public void EveryPathPlotsEverySlot()
        {
            foreach (var plot in EveryPlot())
            {
                // Slot 20 is the capstone; asking for it must not fall back to
                // the origin, which is what an out-of-range read would do.
                Assert.AreNotEqual(0f, Y(plot, Talents.TalentSkeleton.SlotCount - 1),
                    $"{plot} has no coordinate for its capstone");
            }
        }

        [Test]
        public void TheClimbRunsUpTheScreen()
        {
            // A capstone is the thing at the top of a climb. A constellation
            // that grew downward would read as falling.
            foreach (var plot in EveryPlot())
            {
                Assert.Greater(Y(plot, Talents.TalentSkeleton.SlotCount - 1), Y(plot, 0),
                    $"{plot}'s capstone is not above its root");
            }
        }

        // THE SHARED SPINE LADDER is what guarantees the separation floor: the
        // figures differ only in how far their side nodes swing out, so if one
        // plot drifted off the ladder the clearances below would stop meaning
        // anything even while they still passed.
        [Test]
        public void EveryPlotSharesOneSpineLadder()
        {
            for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
            {
                if (Talents.TalentSkeleton.DxSlot[slot] != 0) continue;

                float y = ConstellationLayout.StarY(0, slot);

                foreach (var plot in EveryPlot())
                {
                    Assert.AreEqual(0f, X(plot, slot), 0.01f,
                        $"{plot}'s slot {slot} is a spine node but is not on the spine");
                    Assert.AreEqual(y, Y(plot, slot), 0.01f,
                        $"{plot}'s slot {slot} has come off the shared ladder");
                }
            }
        }

        [Test]
        public void SideNodesSitOnTheSideTheirSlotSaysTheyDo()
        {
            foreach (var plot in EveryPlot())
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    float x = X(plot, slot);

                    switch (Talents.TalentSkeleton.DxSlot[slot])
                    {
                        case -1: Assert.Less(x, 0f, $"{plot} slot {slot} is a left node on the right"); break;
                        case 1: Assert.Greater(x, 0f, $"{plot} slot {slot} is a right node on the left"); break;
                        default: Assert.AreEqual(0f, x, 0.01f, $"{plot} slot {slot} is a spine node off-centre"); break;
                    }
                }
            }
        }

        // THE FLOOR THE DESIGN MEASURED, and the reason it exists: a lit sprite
        // draws at its full box, so two stones closer than this overlap their
        // own art. Authored as 94px and 108px at 1600x900; these are those at
        // x1.2.
        [Test]
        public void NoTwoStonesSitCloserThanTheSeparationFloor()
        {
            foreach (var plot in EveryPlot())
            {
                for (int a = 0; a < Talents.TalentSkeleton.SlotCount; a++)
                {
                    for (int b = a + 1; b < Talents.TalentSkeleton.SlotCount; b++)
                    {
                        float dx = X(plot, a) - X(plot, b);
                        float dy = Y(plot, a) - Y(plot, b);
                        float distance = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);

                        Assert.GreaterOrEqual(distance, ConstellationLayout.SeparationFloor,
                            $"{plot}: slots {a} and {b} are {distance:F0}px apart, inside the floor");
                    }
                }
            }
        }

        [Test]
        public void ASideNodeKeepsItsDistanceFromItsOwnTiersSpine()
        {
            foreach (var plot in EveryPlot())
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    if (Talents.TalentSkeleton.DxSlot[slot] == 0) continue;

                    Assert.GreaterOrEqual(UnityEngine.Mathf.Abs(X(plot, slot)),
                        ConstellationLayout.SideClearance,
                        $"{plot} slot {slot} crowds its own tier's spine");
                }
            }
        }

        // THE EDGES HAVE TO READ AS CONNECTIONS. A hand-plotted figure can run
        // a limb straight through a stone it does not join, and then the lit
        // line looks like it feeds that stone -- the one lie a talent tree
        // cannot tell. Every edge keeps the lit glow's half-width clear of
        // every stone that is not one of its own two ends.
        [Test]
        public void NoEdgeRunsThroughAStoneItDoesNotJoin()
        {
            foreach (var plot in EveryPlot())
            {
                for (int child = 0; child < Talents.TalentSkeleton.SlotCount; child++)
                {
                    foreach (int parent in Talents.TalentSkeleton.Parents[child])
                    {
                        float ax = X(plot, parent), ay = Y(plot, parent);
                        float bx = X(plot, child), by = Y(plot, child);

                        for (int other = 0; other < Talents.TalentSkeleton.SlotCount; other++)
                        {
                            if (other == child || other == parent) continue;

                            float clear = ConstellationLayout.OrbSize(Talents.TalentSkeleton.Kind[other]) * 0.5f
                                + ConstellationLayout.EdgeGlowWidth * 0.5f;

                            float gap = DistanceToSegment(X(plot, other), Y(plot, other), ax, ay, bx, by);

                            Assert.GreaterOrEqual(gap, clear,
                                $"{plot}: edge {parent}-{child} passes {gap:F0}px from slot {other}");
                        }
                    }
                }
            }
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy);
            t = System.Math.Max(0f, System.Math.Min(1f, t));
            float cx = ax + dx * t - px, cy = ay + dy * t - py;
            return UnityEngine.Mathf.Sqrt(cx * cx + cy * cy);
        }

        // The figure has to fit the stage it is drawn on, orbs included. The
        // capstone is the largest and sits at the very top, so it is the one
        // that would go off-canvas first.
        [Test]
        public void EveryPlotFitsTheSkyItIsDrawnIn()
        {
            float halfHeight = UiFrames.Reference.Y * 0.5f;

            foreach (var plot in EveryPlot())
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    float half = ConstellationLayout.OrbSize(Talents.TalentSkeleton.Kind[slot]) * 0.5f;
                    float y = ConstellationLayout.TreeOriginY + Y(plot, slot);
                    float x = ConstellationLayout.TreeOriginX + X(plot, slot);

                    Assert.LessOrEqual(y + half, halfHeight,
                        $"{plot} slot {slot} runs off the top of the canvas");
                    Assert.GreaterOrEqual(y - half, -halfHeight,
                        $"{plot} slot {slot} runs off the bottom of the canvas");

                    // Horizontally the bound is the SKY, not the canvas: the
                    // panel takes the right-hand column and a stone under it
                    // would be hidden rather than merely tight.
                    Assert.LessOrEqual(x + half, ConstellationLayout.SkyWidth * 0.5f,
                        $"{plot} slot {slot} runs under the detail panel");
                }
            }
        }

        // TreeWidth claims to be the widest figure. A plot wider than it would
        // make every consumer of that number wrong without failing anything.
        [Test]
        public void TreeWidthReallyIsTheWidestFigure()
        {
            foreach (var plot in EveryPlot())
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    Assert.LessOrEqual(UnityEngine.Mathf.Abs(X(plot, slot)) * 2f + ConstellationLayout.OrbCap,
                        ConstellationLayout.TreeWidth + 0.01f,
                        $"{plot} slot {slot} is wider than TreeWidth says any figure is");
                }
            }
        }

        // ---- which figure each character draws -----------------------------------
        //
        // Pinned by NAME, so the test says what the owner decided (Bjorn's
        // path order is Sentinel, Einherjar, Juggernaut) rather than repeating
        // sixty-three coordinates.
        [TestCase(0, "shield")]
        [TestCase(1, "axe")]
        [TestCase(2, "paw")]
        public void BjornDrawsShieldAxeAndPaw(int path, string figure)
        {
            Assert.AreEqual(figure, ConstellationLayout.PlotId("bear", path));
        }

        [TestCase(0, "ram")]
        [TestCase(1, "lamb")]
        [TestCase(2, "spire")]
        public void ShawnStillDrawsHisOwnFigures(int path, string figure)
        {
            Assert.AreEqual(figure, ConstellationLayout.PlotId("sheep", path));
        }

        [TestCase("owl", 0)]
        [TestCase("no_such_character", 1)]
        [TestCase(null, 0)]
        [TestCase("bear", 3)]
        [TestCase("bear", -1)]
        public void AnythingUnplottedFallsBackToTheSpire(string character, int path)
        {
            Assert.AreEqual("spire", ConstellationLayout.PlotId(character, path));
        }

        // The path-only API predates the character key and meant Shawn's all
        // along -- the screen tree and controller still call it, and nothing
        // they draw may move until they are switched over.
        [Test]
        public void ThePathOnlyApiIsShawns()
        {
            Assert.AreEqual(ConstellationLayout.PlotCountFor("sheep"), ConstellationLayout.PlotCount);

            for (int path = -1; path <= ConstellationLayout.PlotCount; path++)
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    Assert.AreEqual(ConstellationLayout.StarX("sheep", path, slot), ConstellationLayout.StarX(path, slot));
                    Assert.AreEqual(ConstellationLayout.StarY("sheep", path, slot), ConstellationLayout.StarY(path, slot));
                }
            }
        }

        // Different characters draw different figures -- the whole point of
        // the key. Guards against a registry that maps every id to one table.
        [Test]
        public void BjornsFiguresAreNotShawns()
        {
            for (int path = 0; path < 3; path++)
            {
                bool differs = false;
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    differs |= ConstellationLayout.StarX("bear", path, slot) != ConstellationLayout.StarX("sheep", path, slot)
                        || ConstellationLayout.StarY("bear", path, slot) != ConstellationLayout.StarY("sheep", path, slot);
                }

                Assert.IsTrue(differs, $"bear path {path} draws Shawn's figure");
            }
        }

        // ---- sizing marks role, not point cost -----------------------------------
        //
        // The design's own rule, and the thing the stretch had lost entirely:
        // every orb was one size, so the capstone at the top of an eight-tier
        // climb looked exactly like the first chain node above the root.
        [Test]
        public void TheCapstoneAndTheConvergenceAreLargerThanAChainOrb()
        {
            Assert.Greater(ConstellationLayout.OrbSize("cap"), ConstellationLayout.OrbSize("merge"),
                "the capstone should out-rank the convergence");
            Assert.Greater(ConstellationLayout.OrbSize("merge"), ConstellationLayout.OrbSize("normal"),
                "the convergence should out-rank a chain orb");
        }

        [Test]
        public void AnUnknownKindIsAnOrdinaryOrb()
        {
            // Graceful degradation, house style: a kind added to the skeleton
            // and not to the size table draws as a normal orb rather than as
            // nothing.
            Assert.AreEqual(ConstellationLayout.OrbNormal, ConstellationLayout.OrbSize("something_new"), 0.01f);
        }

        // One path's tree has to fit the sky it is drawn in, orbs included --
        // the widest orb hangs half its width past the outermost branch.
        [Test]
        public void OnePathsTreeFitsTheSky()
        {
            Assert.LessOrEqual(ConstellationLayout.TreeHeight, ConstellationLayout.SkyHeight + 0.01f,
                $"a path is {ConstellationLayout.TreeHeight:F0}px tall in a " +
                $"{ConstellationLayout.SkyHeight:F0}px sky");
        }

        // ---- paging --------------------------------------------------------------

        [Test]
        public void OnlyTheCurrentTreeIsCentred()
        {
            Assert.AreEqual(0f, ConstellationLayout.PageX(1, 1), 0.01f);
            Assert.AreEqual(-ConstellationLayout.PageStride, ConstellationLayout.PageX(0, 1), 0.01f);
            Assert.AreEqual(ConstellationLayout.PageStride, ConstellationLayout.PageX(2, 1), 0.01f);
        }

        [Test]
        public void TheNeighboursAreGenuinelyOffStage()
        {
            // A full screen width apart, so a neighbour never peeks in at the
            // edge -- which is what makes three trees read as three PLACES
            // rather than three tabs.
            Assert.GreaterOrEqual(ConstellationLayout.PageStride, 1920f);
        }

        [Test]
        public void SteppingRightFromTheLeftTreeLandsOnTheMiddle()
        {
            Assert.AreEqual(1, ConstellationLayout.Step(0, +1, 3));
        }

        [Test]
        public void SteppingRightFromTheMiddleLandsOnTheRight()
        {
            Assert.AreEqual(2, ConstellationLayout.Step(1, +1, 3));
        }

        [Test]
        public void SteppingLeftComesBack()
        {
            Assert.AreEqual(1, ConstellationLayout.Step(2, -1, 3));
            Assert.AreEqual(0, ConstellationLayout.Step(1, -1, 3));
        }

        [Test]
        public void TheEndsAreEndsRatherThanAWrap()
        {
            // Wrapping would make the arrows lie about where the edges are: a
            // player who has paged to the far right should be able to tell they
            // are there without counting.
            Assert.AreEqual(0, ConstellationLayout.Step(0, -1, 3), "already at the left");
            Assert.AreEqual(2, ConstellationLayout.Step(2, +1, 3), "already at the right");

            Assert.IsFalse(ConstellationLayout.CanStep(0, -1, 3));
            Assert.IsFalse(ConstellationLayout.CanStep(2, +1, 3));
            Assert.IsTrue(ConstellationLayout.CanStep(1, -1, 3));
            Assert.IsTrue(ConstellationLayout.CanStep(1, +1, 3));
        }

        [Test]
        public void ASingleTreeOffersNoArrowsAtAll()
        {
            Assert.IsFalse(ConstellationLayout.CanStep(0, -1, 1));
            Assert.IsFalse(ConstellationLayout.CanStep(0, +1, 1));
        }

        [Test]
        public void NoTreesAtAllIsSurvivable()
        {
            Assert.AreEqual(0, ConstellationLayout.Step(0, +1, 0));
        }

        // ---- the slide -------------------------------------------------------------

        [Test]
        public void TheSlideStartsStoppedAndEndsArrived()
        {
            Assert.AreEqual(0f, ConstellationLayout.SlideProgress(0f), 0.0001f);
            Assert.AreEqual(1f, ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds), 0.0001f);
        }

        [Test]
        public void OvershootingTheSlideStaysArrived()
        {
            // A frame that overshoots must not wrap into a second slide, which
            // is what an unclamped curve would do.
            Assert.AreEqual(1f, ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 3f), 0.0001f);
        }

        [Test]
        public void TheSlideEasesAtBothEnds()
        {
            // A slide that starts and stops abruptly is a cut with extra steps.
            // Ease-in-out is what lets the player keep their bearings.
            float quarter = ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 0.25f);
            float half = ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 0.5f);
            float threeQuarters = ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 0.75f);

            Assert.AreEqual(0.5f, half, 0.0001f, "the midpoint is halfway across");
            Assert.Less(quarter, 0.25f, "slow off the mark");
            Assert.Greater(threeQuarters, 0.75f, "and slow into the arrival");
        }

        [Test]
        public void TheSkyArrivesExactlyOnTheTargetTree()
        {
            // The assertion that makes the slide safe to interrupt: wherever it
            // is stopped, finishing puts the target dead centre.
            float arrived = ConstellationLayout.SlideOffset(0, 2, 1f);

            Assert.AreEqual(-2f * ConstellationLayout.PageStride, arrived, 0.01f);
            Assert.AreEqual(0f, arrived + ConstellationLayout.PageX(2, 0) - ConstellationLayout.PageStride * 0f, 0.01f * ConstellationLayout.PageStride);
        }

        [Test]
        public void TheSkyStartsOnTheTreeItLeft()
        {
            Assert.AreEqual(
                -ConstellationLayout.PageStride * 1f,
                ConstellationLayout.SlideOffset(1, 2, 0f),
                0.01f);
        }

        // The guarantee that used to be a runtime guard.
        //
        // SlideProgress divided by SlideSeconds behind an `if (SlideSeconds <=
        // 0f)` check that the compiler folded away, because the constant is
        // positive -- CS0162, unreachable code. A guard that cannot fire is
        // noise that hides the next real one, so it was removed and the
        // condition it protected moved here.
        //
        // It protected something real: at zero the division of a zero elapsed
        // is NaN, every comparison in the method is false against NaN, and the
        // slide returns NaN instead of a progress. Failing here is how that
        // stays impossible.
        [Test]
        public void TheSlideHasAPositiveDuration()
        {
            Assert.Greater(ConstellationLayout.SlideSeconds, 0f,
                "SlideProgress divides by this -- at zero it returns NaN for a zero elapsed");
        }

        [Test]
        public void SlideProgressStaysWithinItsRangeAndIsNeverNaN()
        {
            foreach (float elapsed in new[] { -1f, 0f, 0.01f, 0.21f, 0.41f, ConstellationLayout.SlideSeconds, 99f })
            {
                float p = ConstellationLayout.SlideProgress(elapsed);

                Assert.IsFalse(float.IsNaN(p), $"NaN progress at elapsed {elapsed}");
                Assert.That(p, Is.InRange(0f, 1f), $"out of range at elapsed {elapsed}");
            }
        }

        // ---- the arrows clear the panel ------------------------------------------
        //
        // The one geometric fact the audit found and nothing else would have.
        //
        // Both arrows were placed symmetrically about the CANVAS, which put the
        // right-hand one at x 665 underneath a panel spanning 504 to 960: a
        // live button drawn beneath an opaque column, taking clicks meant for
        // whatever is under it. The sky is offset left by half the panel width
        // and the arrows have to follow it, which makes them symmetric about
        // the tree and asymmetric about the screen.
        [Test]
        public void NeitherPagingArrowSitsUnderThePanel()
        {
            float panelLeft = ConstellationLayout.PanelCentreX - ConstellationLayout.PanelWidth * 0.5f;
            float half = ConstellationLayout.ArrowWidth * 0.5f;

            Assert.Less(ConstellationLayout.ArrowRightX + half, panelLeft,
                "the forward arrow is drawn under the detail panel, where it is invisible and " +
                "still takes clicks");
            Assert.Less(ConstellationLayout.ArrowLeftX + half, panelLeft,
                "the back arrow is drawn under the detail panel");
        }

        [Test]
        public void BothPagingArrowsStayOnScreen()
        {
            float edge = UiFrames.Reference.X * 0.5f;
            float half = ConstellationLayout.ArrowWidth * 0.5f;

            Assert.GreaterOrEqual(ConstellationLayout.ArrowLeftX - half, -edge,
                "the back arrow hangs off the left of the canvas");
            Assert.LessOrEqual(ConstellationLayout.ArrowRightX + half, edge,
                "the forward arrow hangs off the right of the canvas");
        }

        // ---- the resting stagger -------------------------------------------------
        //
        // 491ms against a 3200ms cycle. The number is arbitrary and the
        // PROPERTY is not: 21 stones must not share a phase, or the tree
        // breathes in unison and reads as a blink rather than as a fire.
        [Test]
        public void NoTwoStonesRestOnTheSamePhase()
        {
            var seen = new System.Collections.Generic.HashSet<float>();

            for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
            {
                Assert.IsTrue(seen.Add(ConstellationLayout.RestingPhase(slot)),
                    $"slot {slot} shares its resting phase with an earlier slot, so the two " +
                    "stones breathe together");
            }
        }

        [Test]
        public void EveryRestingPhaseFallsInsideItsOwnCycle()
        {
            for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
            {
                Assert.That(ConstellationLayout.RestingPhase(slot),
                    Is.InRange(0f, ConstellationLayout.RestingCycleMs / 1000f));
            }
        }

        // ---- the catch -----------------------------------------------------------
        //
        // A stone that scales to 1 and stops has switched on; a stone that goes
        // PAST 1 and settles has caught. The overshoot is the whole difference,
        // and it has to come back -- an overshoot left standing is a stone
        // permanently the wrong size, which no layout audit can see because it
        // measures the built scene rather than a running one.
        [Test]
        public void TheCatchOvershootsAndComesBack()
        {
            Assert.AreEqual(1f, ConstellationLayout.CatchScale(0f), 0.0001f,
                "the stone starts at the wrong size");
            Assert.AreEqual(1f, ConstellationLayout.CatchScale(1f), 0.0001f,
                "the catch never settles back, so a kindled stone stays enlarged");

            float peak = 0f;
            for (int i = 0; i <= 100; i++)
            {
                peak = System.Math.Max(peak, ConstellationLayout.CatchScale(i / 100f));
            }

            // 1.18 is the design's figure. Pinned as a literal rather than
            // recomputed from the production expression, which would only
            // assert that the expression equals itself.
            Assert.AreEqual(1.18f, peak, 0.01f,
                "the overshoot is not the design's 18%");
        }

        [Test]
        public void TheCatchNeverShrinksTheStone()
        {
            for (int i = 0; i <= 200; i++)
            {
                Assert.GreaterOrEqual(ConstellationLayout.CatchScale(i / 200f), 1f,
                    "the catch dips below full size, so the stone pinches inward before it grows");
            }
        }

        // ---- the loops -----------------------------------------------------------

        [Test]
        public void ABreatheRunsFromNothingToFullAndBack()
        {
            Assert.AreEqual(0f, ConstellationLayout.Breathe(0f), 0.0001f);
            Assert.AreEqual(1f, ConstellationLayout.Breathe(0.5f), 0.0001f);
            Assert.AreEqual(0f, ConstellationLayout.Breathe(1f), 0.0001f);
        }

        // Sampled across a long stretch rather than at a few points: Repeat is
        // hand-rolled here because Domain cannot reach Mathf, and a truncation
        // that goes the wrong way for negative input is exactly the kind of
        // thing that only shows up at one sign.
        [Test]
        public void ACycleStaysInsideItsOwnUnitInterval()
        {
            for (float t = -50f; t < 400f; t += 0.37f)
            {
                float c = ConstellationLayout.Cycle(t, 7.2f);

                Assert.That(c, Is.InRange(0f, 1f), $"cycle left its range at t {t}");
                Assert.IsFalse(float.IsNaN(c), $"NaN cycle at t {t}");
            }
        }

        [Test]
        public void AZeroPeriodLoopSitsStillRatherThanDividingByNothing()
        {
            Assert.AreEqual(0f, ConstellationLayout.Cycle(12f, 0f));
            Assert.IsFalse(float.IsNaN(ConstellationLayout.Cycle(12f, 0f)));
        }

        // THE PERIODS MUST DISAGREE. Two layers on the same period are one
        // layer with extra draw calls, and the whole claim of the backdrop is
        // that it never repeats a frame.
        [Test]
        public void NoTwoBackdropLayersShareAPeriod()
        {
            var periods = new System.Collections.Generic.List<float>
            {
                ConstellationLayout.StarPanNearSeconds,
                ConstellationLayout.StarPanFarSeconds,
                ConstellationLayout.CloudBreatheSeconds,
                ConstellationLayout.CloudDrift(0),
                ConstellationLayout.CloudDrift(1),
                ConstellationLayout.CloudDrift(2),
                ConstellationLayout.StreakPeriod(0),
                ConstellationLayout.StreakPeriod(1),
                ConstellationLayout.CoreFlickerSeconds,
                ConstellationLayout.HaloCrackleSeconds,
                ConstellationLayout.ReadyPulseSeconds,
                ConstellationLayout.CapCoronaSeconds,
                ConstellationLayout.EmberFastSeconds,
                ConstellationLayout.EmberSlowSeconds,
            };

            CollectionAssert.AllItemsAreUnique(periods,
                "two of the screen's loops run on the same period, so they synchronise and the " +
                "backdrop starts reading as one repeating layer");
        }

        // ---- the ember flickers like a fire ---------------------------------------
        //
        // Measured off the design's own recording: its hot centre swings 2.4x in
        // area with peaks 0.6 to 1.0 seconds apart and never on a beat. A single
        // sine cannot do that -- it gives evenly spaced peaks of equal height,
        // which reads as a stone swelling and sinking rather than burning.
        [Test]
        public void TheEmbersFlickerIsIrregular()
        {
            var peaks = new System.Collections.Generic.List<float>();
            float previous = 0f, before = 0f;

            for (float t = 0f; t < 30f; t += 1f / 60f)
            {
                float v = ConstellationLayout.EmberFlicker(t, 0f);

                Assert.That(v, Is.InRange(0f, 1f), $"the flicker left its range at t {t}");

                if (previous > before && previous > v) peaks.Add(previous);

                before = previous;
                previous = v;
            }

            Assert.Greater(peaks.Count, 20, "the flicker barely peaks over 30 seconds");

            // THE PEAKS ARE OF DIFFERENT HEIGHTS, which is the property. A sine
            // of any period gives peaks that are all exactly 1.
            float tallest = 0f, shortest = 1f;
            foreach (float p in peaks)
            {
                if (p > tallest) tallest = p;
                if (p < shortest) shortest = p;
            }

            Assert.Greater(tallest - shortest, 0.15f,
                "every peak of the flicker is the same height, so it is a breathe with extra " +
                "arithmetic rather than a fire");
        }

        // The two rates must not divide into each other, or their sum settles
        // into a short repeating pattern and the irregularity above is a lie
        // that happens to hold for the first few seconds.
        [Test]
        public void TheEmbersTwoRatesAreIncommensurate()
        {
            float ratio = ConstellationLayout.EmberSlowSeconds / ConstellationLayout.EmberFastSeconds;

            for (int n = 1; n <= 6; n++)
            {
                for (int d = 1; d <= 6; d++)
                {
                    Assert.Greater(System.Math.Abs(ratio - (float)n / d), 0.02f,
                        $"the ember's two rates sit at {n}:{d}, so their sum repeats every " +
                        $"{d * ConstellationLayout.EmberFastSeconds:0.##}s");
                }
            }
        }

        // The lean is a LEAN, not a centring. A push that moved the selected
        // stone to the middle would drag the whole tree under a pointer that is
        // comparing two stones side by side.
        [Test]
        public void ThePushInLeansTowardsTheStoneWithoutCentringIt()
        {
            const float starX = 400f;
            float offset = ConstellationLayout.PushInOffset(starX);

            Assert.Less(offset, 0f, "a stone to the right should pull the sky left");
            Assert.Less(System.Math.Abs(offset), starX * 0.5f,
                "the push is closer to a centring than to a lean");
            Assert.AreEqual(0f, ConstellationLayout.PushInOffset(0f), 0.0001f,
                "a stone already at the centre moves the sky");
        }

        // ---- the whole figure fits -----------------------------------------------
        //
        // A STONE IS NOT THE ONLY THING DRAWN AT A SLOT. Each carries a name
        // under it and the two gated ones carry a reading above, and the figure
        // is 1071px tall once those are counted against a 1080 canvas -- eight
        // pixels of slack, all of which the authored origin spent at the top,
        // clipping the root's name off the bottom edge.
        //
        // UiAudit cannot see this: every one of those labels declares
        // AllowOverflow, because a name IS wider than the stone it belongs to,
        // and the exemption that makes the horizontal case legal waives the
        // vertical one with it.
        [Test]
        public void TheWholeFigureFitsTheCanvas()
        {
            float top = UiFrames.Reference.Y * 0.5f;
            var offenders = new System.Collections.Generic.List<string>();

            for (int path = 0; path < 3; path++)
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    string kind = Talents.TalentSkeleton.Kind[slot];
                    float y = ConstellationLayout.TreeOriginY + ConstellationLayout.StarY(path, slot);

                    // The name, which every stone draws.
                    float nameBottom = y + ConstellationLayout.LabelY(kind)
                                       - ConstellationLayout.LabelHeight * 0.5f;

                    if (nameBottom < -top)
                    {
                        offenders.Add($"path {path} slot {slot}: its name reaches {nameBottom:0.#}, " +
                                      $"below the canvas at {-top}");
                    }

                    // The gate reading, which only the two gated singles draw.
                    if (kind != "merge" && kind != "cap") continue;

                    float gateTop = y + ConstellationLayout.CollarSize(kind) * 0.5f + 14f
                                    + ConstellationLayout.LabelHeight * 0.5f;

                    if (gateTop > top)
                    {
                        offenders.Add($"path {path} slot {slot}: its gate reading reaches " +
                                      $"{gateTop:0.#}, above the canvas at {top}");
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "part of a constellation is drawn off the canvas - move ConstellationLayout." +
                "TreeOriginY, do not shrink the stones");
        }

        // The user's "halve the crackle" ask (session brief, 2026-09-03) meant
        // the halo crackle on the orb auras, not TalentEdgeCrackle's edge
        // flicker -- that one got reverted to 17/29. 50% slower here means the
        // period grows, so it's a double: 4.6 to 9.2.
        [Test]
        public void HaloCrackleIsFiftyPercentSlowerPerTheUsersRequest()
        {
            Assert.AreEqual(9.2f, ConstellationLayout.HaloCrackleSeconds);
        }
    }
}
