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

        [Test]
        public void EveryPathPlotsEverySlot()
        {
            for (int path = 0; path < ConstellationLayout.PlotCount; path++)
            {
                // Slot 20 is the capstone; asking for it must not fall back to
                // the origin, which is what an out-of-range read would do.
                Assert.AreNotEqual(0f, ConstellationLayout.StarY(path, Talents.TalentSkeleton.SlotCount - 1),
                    $"path {path} has no coordinate for its capstone");
            }
        }

        [Test]
        public void TheClimbRunsUpTheScreen()
        {
            // A capstone is the thing at the top of a climb. A constellation
            // that grew downward would read as falling.
            for (int path = 0; path < ConstellationLayout.PlotCount; path++)
            {
                Assert.Greater(ConstellationLayout.StarY(path, Talents.TalentSkeleton.SlotCount - 1),
                    ConstellationLayout.StarY(path, 0),
                    $"path {path}'s capstone is not above its root");
            }
        }

        // THE SHARED SPINE LADDER is what guarantees the separation floor: the
        // figures differ only in how far their side nodes swing out, so if one
        // plot drifted off the ladder the clearances below would stop meaning
        // anything even while they still passed.
        [Test]
        public void AllThreePlotsShareOneSpineLadder()
        {
            for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
            {
                if (Talents.TalentSkeleton.DxSlot[slot] != 0) continue;

                float y = ConstellationLayout.StarY(0, slot);

                for (int path = 1; path < ConstellationLayout.PlotCount; path++)
                {
                    Assert.AreEqual(0f, ConstellationLayout.StarX(path, slot), 0.01f,
                        $"path {path}'s slot {slot} is a spine node but is not on the spine");
                    Assert.AreEqual(y, ConstellationLayout.StarY(path, slot), 0.01f,
                        $"path {path}'s slot {slot} has come off the shared ladder");
                }
            }
        }

        [Test]
        public void SideNodesSitOnTheSideTheirSlotSaysTheyDo()
        {
            for (int path = 0; path < ConstellationLayout.PlotCount; path++)
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    float x = ConstellationLayout.StarX(path, slot);

                    switch (Talents.TalentSkeleton.DxSlot[slot])
                    {
                        case -1: Assert.Less(x, 0f, $"path {path} slot {slot} is a left node on the right"); break;
                        case 1: Assert.Greater(x, 0f, $"path {path} slot {slot} is a right node on the left"); break;
                        default: Assert.AreEqual(0f, x, 0.01f, $"path {path} slot {slot} is a spine node off-centre"); break;
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
            for (int path = 0; path < ConstellationLayout.PlotCount; path++)
            {
                for (int a = 0; a < Talents.TalentSkeleton.SlotCount; a++)
                {
                    for (int b = a + 1; b < Talents.TalentSkeleton.SlotCount; b++)
                    {
                        float dx = ConstellationLayout.StarX(path, a) - ConstellationLayout.StarX(path, b);
                        float dy = ConstellationLayout.StarY(path, a) - ConstellationLayout.StarY(path, b);
                        float distance = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);

                        Assert.GreaterOrEqual(distance, ConstellationLayout.SeparationFloor,
                            $"path {path}: slots {a} and {b} are {distance:F0}px apart, inside the floor");
                    }
                }
            }
        }

        [Test]
        public void ASideNodeKeepsItsDistanceFromItsOwnTiersSpine()
        {
            for (int path = 0; path < ConstellationLayout.PlotCount; path++)
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    if (Talents.TalentSkeleton.DxSlot[slot] == 0) continue;

                    Assert.GreaterOrEqual(
                        UnityEngine.Mathf.Abs(ConstellationLayout.StarX(path, slot)),
                        ConstellationLayout.SideClearance,
                        $"path {path} slot {slot} crowds its own tier's spine");
                }
            }
        }

        // The figure has to fit the stage it is drawn on, orbs included. The
        // capstone is the largest and sits at the very top, so it is the one
        // that would go off-canvas first.
        [Test]
        public void EveryPlotFitsTheSkyItIsDrawnIn()
        {
            float halfHeight = UiFrames.Reference.Y * 0.5f;

            for (int path = 0; path < ConstellationLayout.PlotCount; path++)
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    float half = ConstellationLayout.OrbSize(Talents.TalentSkeleton.Kind[slot]) * 0.5f;
                    float y = ConstellationLayout.TreeOriginY + ConstellationLayout.StarY(path, slot);
                    float x = ConstellationLayout.TreeOriginX + ConstellationLayout.StarX(path, slot);

                    Assert.LessOrEqual(y + half, halfHeight,
                        $"path {path} slot {slot} runs off the top of the canvas");
                    Assert.GreaterOrEqual(y - half, -halfHeight,
                        $"path {path} slot {slot} runs off the bottom of the canvas");

                    // Horizontally the bound is the SKY, not the canvas: the
                    // panel takes the right-hand column and a stone under it
                    // would be hidden rather than merely tight.
                    Assert.LessOrEqual(x + half, ConstellationLayout.SkyWidth * 0.5f,
                        $"path {path} slot {slot} runs under the detail panel");
                }
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
            };

            CollectionAssert.AllItemsAreUnique(periods,
                "two of the screen's loops run on the same period, so they synchronise and the " +
                "backdrop starts reading as one repeating layer");
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
    }
}
