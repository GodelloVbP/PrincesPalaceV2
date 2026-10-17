using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The Reckoning's tree.
    //
    // Doubles as the tuning harness for a layout with a hard external
    // constraint: 70% of the frame, centred, is 1344x756, and four party rows
    // plus three offers plus chrome does not fit in one column at that height.
    // Each attempt costs a second here instead of a scene rebuild.
    public class ReckoningScreenTests
    {
        private static UiNode Tree() => ReckoningScreen.Build().Root;

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void ItAuditsCleanInsideTheFightItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(FightScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void NothingSitsOnThePaintedBorder()
        {
            // THE AUDIT CANNOT SEE PAINT. A1 knows the frame node is
            // 1344x896 and will happily let a label sit at its very edge --
            // where the art has a gold border, not a background. Asserted
            // directly, against bounds measured off the keyed image (the
            // plain interior is 91.9% wide and 90.3% tall).
            var screen = ReckoningScreen.Build();
            var offenders = new List<string>();

            foreach (var node in Walk(screen.Frame.Node))
            {
                if (ReferenceEquals(node, screen.Frame.Node)) continue;

                // Pages stretch to fill the frame by design; their CHILDREN
                // are what has to stay inside, and those are walked too.
                if (node.Place.Kind != PlaceKind.At) continue;

                // Only FIXED nodes carry a size worth measuring; anything
                // sized from its children or filling its parent is bounded by
                // something this walk already checked.
                if (node.Size.ModeX != UiSizeMode.Fixed || node.Size.ModeY != UiSizeMode.Fixed) continue;

                float halfWidth = node.Size.X * 0.5f;
                float halfHeight = node.Size.Y * 0.5f;

                if (System.Math.Abs(node.Place.Offset.X) + halfWidth > ReckoningScreen.ContentHalfWidth + 1f)
                {
                    offenders.Add($"{node.Name} runs to x {System.Math.Abs(node.Place.Offset.X) + halfWidth:0}");
                }

                // TOP AND BOTTOM SEPARATELY. The crest reaches 16.7% down and
                // the bottom ornament only 14.6%, so one symmetric bound would
                // either allow a collision at the top or waste 19px at the
                // bottom.
                float top = node.Place.Offset.Y + halfHeight;
                float bottom = node.Place.Offset.Y - halfHeight;

                if (top > ReckoningScreen.ContentTop + 1f)
                {
                    offenders.Add($"{node.Name} reaches y {top:0} into the crest");
                }

                if (bottom < ReckoningScreen.ContentBottom - 1f)
                {
                    offenders.Add($"{node.Name} reaches y {bottom:0} into the bottom border");
                }
            }

            CollectionAssert.IsEmpty(offenders,
                $"content is over the painted border (usable x +/-{ReckoningScreen.ContentHalfWidth:0}, " +
                $"y {ReckoningScreen.ContentBottom:0}..{ReckoningScreen.ContentTop:0}): " +
                string.Join(" | ", offenders));
        }

        [Test]
        public void TheFrameWearsTheSpread()
        {
            // The cloth (reward_spread.png) as a frame holder: the art is a
            // Decor child, so content beside it still audits.
            var frame = ReckoningScreen.Build().Frame.Node;

            Assert.IsFalse(frame.Decor,
                "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = frame.Children.Single(c => c.Kind == UiNodeKind.Sprite);
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_spread.png", art.SpriteKey);
            Assert.IsTrue(art.Decor);
        }

        [Test]
        public void TheContentBoxIsTheSpreadsMeasuredHem()
        {
            Assert.AreEqual(577.92f, ReckoningScreen.ContentHalfWidth, 0.01f);
            Assert.AreEqual(327.04f, ReckoningScreen.ContentTop, 0.01f);
            Assert.AreEqual(-358.4f, ReckoningScreen.ContentBottom, 0.01f);
        }

        [Test]
        public void EveryOfferHasABurstThatStartsInvisible()
        {
            // The burst is tinted per rarity at paint time and spun by the
            // controller. Visible at build would put a white starburst behind
            // three empty rows on every fight that offers nothing.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual(screen.OfferButtons.Count, screen.OfferBursts.Count);
            Assert.AreEqual(screen.OfferButtons.Count, screen.OfferIcons.Count);

            foreach (var burst in screen.OfferBursts)
            {
                StringAssert.EndsWith("00", burst.Node.ColorHex, "alpha 00 until a rarity says otherwise");
            }
        }

        [Test]
        public void TheFrameIsSeventyPercentOfTheReferenceFrame()
        {
            // 70% of the WIDTH, and 3:2 rather than 16:9.
            //
            // The height stopped being 70% when the painted frame landed: that
            // art is 1536x1024, and holding 756 would have stretched its border
            // 18% and distorted every corner ornament on it. Matching the art's
            // own aspect is worth more than matching one number in two
            // directions -- and the extra 140px is exactly what the left column
            // was short of when a party of one left three rows hidden.
            Assert.AreEqual(1920f * 0.7f, ReckoningScreen.PanelWidth, 0.5f);
            Assert.AreEqual(ReckoningScreen.PanelWidth / 1.5f, ReckoningScreen.PanelHeight, 0.5f,
                "the panel no longer matches the frame art's 3:2, so its border will distort");
        }

        [Test]
        public void TheScreenStartsHidden()
        {
            Assert.IsTrue(Tree().StartInactive);
        }

        [Test]
        public void EveryPortraitAndHaulChipStartsHiddenSoAnEmptyPartyDrawsNothing()
        {
            // Three portraits and twelve chips are built; a party is one to
            // three and a haul usually a few. Left visible they would be empty
            // rings and blank chips.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual(3, screen.PortraitButtons.Count);
            Assert.AreEqual(3, screen.DeedRows.Count);
            Assert.AreEqual(12, screen.HaulChips.Count);
            Assert.AreEqual(3 * 4, screen.DeedGlyphs.Count);
            foreach (var node in screen.PortraitButtons.Concat(screen.DeedRows).Concat(screen.HaulChips))
            {
                Assert.IsTrue(node.Node.StartInactive, $"{node.Node.Name} is visible before anything filled it");
            }
        }

        [Test]
        public void TheBoardHasNoTabsPagesTitleOrBars()
        {
            // C8, O2, O4: one board; no ending word, no held relics, no tally.
            var names = Walk(Tree()).Select(n => n.Name).ToList();
            foreach (var gone in new[] { "ReckoningTab0", "ReckoningSpoilsPage", "ReckoningRelicsPage",
                         "ReckoningTallyPage", "ReckoningTitle", "ReckoningTitlePlaque", "ReckoningContinueGlow",
                         "ReckoningRow0BarTrack", "ReckoningGoldLabel" })
            {
                CollectionAssert.DoesNotContain(names, gone);
            }
        }

        [Test]
        public void TheCrestSitsOnTheFramesTopEdge_InsideTheWipe()
        {
            var screen = ReckoningScreen.Build();
            var crest = screen.FrameWipe.Node.Children.Single(c => c.Name == "ReckoningCrest");

            Assert.AreEqual(0f, crest.Place.Offset.X, 0.001f);
            Assert.AreEqual(369.152f, crest.Place.Offset.Y, 0.01f, "on the spread's paw crest");
            Assert.AreEqual(96f, crest.Size.X, 0.001f);
            Assert.LessOrEqual(crest.Place.Offset.Y + crest.Size.Y * 0.5f, ReckoningScreen.PanelHeight * 0.5f,
                "inside the wipe, which would clip its top");
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_crack.png", ReckoningScreen.CrackKey);
        }

        [Test]
        public void TheGoldStackIsOnTheFrame_NotOnAPhase()
        {
            // It counts inside the wipe whichever phase opens.
            var screen = ReckoningScreen.Build();
            CollectionAssert.Contains(screen.Frame.Node.Children.Select(c => c.Name).ToList(), "ReckoningGoldStack");
        }

        [Test]
        public void ThereIsOneCardNodePerCardTheWidestPageShows()
        {
            // OfferRowLayout.MaxCards is the contract: a page shows k + 2 cards,
            // and the tree is emitted once for every save, so it has to hold as
            // many as the widest page can ever produce. Pinned to the literal,
            // not to the formula that produced it: a boss and two anchors is
            // three choices, five cards.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual(5, OfferRowLayout.MaxCards);
            Assert.AreEqual(5, screen.OfferButtons.Count);
            Assert.AreEqual(5, screen.OfferNames.Count);
            Assert.AreEqual(5, screen.OfferDeltas.Count);
            Assert.AreEqual(5, screen.OfferChevrons.Count);
            Assert.AreEqual(5, screen.OfferFeatures.Count);
            Assert.AreEqual(5, screen.OfferSparks.Count);
            Assert.AreEqual(5, screen.OfferPlinths.Count);
            Assert.AreEqual(5, screen.OfferGhosts.Count);
            Assert.AreEqual(5, screen.OfferChains.Count);
            Assert.AreEqual(5, screen.OfferWearerDiscs.Count);
            Assert.AreEqual(5, screen.OfferWearerPortraits.Count);
            Assert.AreEqual(5, screen.OfferBands.Count);
            Assert.AreEqual(5, screen.OfferBandTiers.Count);
            Assert.AreEqual(5, screen.OfferBandPluses.Count);
            Assert.AreEqual(5, screen.HoneButtons.Count);
        }

        [Test]
        public void ACardCarriesNoItemNamePlusAndNoLabelsTheFaceReplaced()
        {
            // Honing is the slot's and is drawn on the band (C5); the old
            // meta, recipient and loses lines are the plinth, the medallion
            // and the focus detail (the lost affix too). A card that still has
            // them is two faces.
            var card = Walk(Tree()).Single(n => n.Name == "ReckoningOffer0");
            var names = Walk(card).Select(n => n.Name).ToList();

            foreach (string gone in new[] { "Meta", "Recipient", "Loses", "Action", "LostDisc", "LostMark" })
            {
                CollectionAssert.DoesNotContain(names, "ReckoningOffer0" + gone);
            }

            foreach (string part in new[] { "Plinth", "Ghost", "Icon", "WearerDisc",
                "WearerPortrait", "Band", "BandTier", "BandPlus", "Name", "Delta", "Chevron", "Feature", "Spark" })
            {
                CollectionAssert.Contains(names, "ReckoningOffer0" + part);
            }
        }

        [Test]
        public void TheCardBackAndTheChainAreTheProcessedArt_AndTheFaceHidesBehindTheBack()
        {
            var card = Walk(Tree()).Single(n => n.Name == "ReckoningOffer0");
            var parts = Walk(card).ToDictionary(n => n.Name, n => n);

            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_card_back.png",
                parts["ReckoningOffer0Back"].SpriteKey);
            Assert.IsTrue(parts["ReckoningOffer0Back"].PreserveAspect, "the back keeps its own aspect");
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_chain.png",
                parts["ReckoningOffer0Chain"].SpriteKey);
            Assert.IsTrue(parts["ReckoningOffer0Chain"].PreserveAspect);

            // The face is one node, a sibling of the back, so one switch hides it all.
            CollectionAssert.Contains(card.Children.Select(c => c.Name).ToList(), "ReckoningOffer0Face");
            Assert.IsTrue(Walk(parts["ReckoningOffer0Face"]).Any(n => n.Name == "ReckoningOffer0Chain"));
            Assert.IsFalse(Walk(parts["ReckoningOffer0Face"]).Any(n => n.Name == "ReckoningOffer0Back"));
        }

        [Test]
        public void TheCacheArtIsASwappableSlot_NotAGlowDisc()
        {
            var screen = ReckoningScreen.Build();

            Assert.IsNull(screen.CacheArt.Node.SpriteKey, "the controller picks the bundle or tin by page class");
            Assert.IsTrue(screen.CacheArt.Node.PreserveAspect);
        }

        [Test]
        public void TheStatGlyphSitsLeftOfTheNumber_AndTheWhetstoneAndDoorCarryGlyphSlots()
        {
            var screen = ReckoningScreen.Build();
            var names = Walk(screen.Root).Select(n => n.Name).ToList();

            foreach (string slot in new[] { "ReckoningOffer0DeltaGlyph", "ReckoningHone0Glyph", "ReckoningHone0Count",
                "ReckoningLeaveDoorGlyph", "ReckoningLeaveHoneGlyph", "ReckoningLeaveHoneCount",
                "ReckoningLeaveCoinGlyph", "ReckoningLeaveCoinCount" })
            {
                CollectionAssert.Contains(names, slot);
            }

            Assert.AreEqual(5, screen.OfferDeltaGlyphs.Count);
            Assert.AreEqual(5, screen.OfferFaces.Count);
            Assert.AreEqual(5, screen.HoneGlyphs.Count);
            for (int count = 1; count <= OfferRowLayout.MaxCards; count++)
            {
                Assert.Less(OfferRowLayout.DeltaGlyphX(count) + RewardArt.GlyphSize * 0.5f + OfferRowLayout.DeltaGlyphGap,
                    OfferRowLayout.DeltaLabelX(count) - OfferRowLayout.DeltaLabelWidth(count) * 0.5f + 0.01f,
                    $"the glyph overlaps the number at {count} cards");
            }
        }

        [Test]
        public void TheGhostIsLeftOfTheItemAndSmallerThanIt()
        {
            // 55% size, behind and to the left (C5). The ghost gate reads the
            // capture; this pins the geometry the capture shows.
            var card = Walk(Tree()).Single(n => n.Name == "ReckoningOffer0");
            var ghost = Walk(card).Single(n => n.Name == "ReckoningOffer0Ghost");
            var icon = Walk(card).Single(n => n.Name == "ReckoningOffer0Icon");

            Assert.Less(ghost.Place.Offset.X, icon.Place.Offset.X, "the ghost is not to the left");
            Assert.AreEqual(icon.Size.X * 0.55f, ghost.Size.X, 0.01f);
            Assert.AreEqual(icon.Size.Y * 0.55f, ghost.Size.Y, 0.01f);
            Assert.AreEqual(0.45f, OfferRowLayout.GhostAlpha, 0.0001f);
        }

        [Test]
        public void TheDimmerIsLightEnoughToLeaveTheFightVisible()
        {
            // The whole argument for an overlay rather than a screen: the stage
            // is still there and the expand plays against it. Dim it to
            // near-black and it becomes a screen with extra steps. Deliberately
            // LIGHTER than the character overlay's, which has the opposite job.
            var dimmer = Walk(Tree()).First(n => n.Name == "ReckoningPanelDimmer");
            int alpha = System.Convert.ToInt32(dimmer.ColorHex.Substring(dimmer.ColorHex.Length - 2), 16);

            Assert.Less(alpha, 0xC0, $"dimmer alpha {dimmer.ColorHex} hides the fight it is supposed to sit over");
            Assert.Greater(alpha, 0x60, "and it still has to make the panel readable");
        }

        [Test]
        public void TheFrameIsNotTheDimmerSoScalingOneDoesNotScaleTheOther()
        {
            // The controller expands the FRAME. If the dimmer were the same
            // node, it would grow from a point too and the fight would flash at
            // full brightness for the first frames of the animation.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual("ReckoningFrame", screen.Frame.Node.Name);
            Assert.AreNotEqual(screen.Root.Name, screen.Frame.Node.Name);
        }

        [Test]
        public void TheFrameIsRevealedByAMaskRatherThanScaled()
        {
            // The open drove frame.localScale.x from 0 for as long as the
            // screen existed. localScale scales CHILDREN, so the border's
            // corner ornaments, the three cards and every label compressed to
            // nothing and sprang out to full width -- the single biggest reason
            // the screen read cheap.
            //
            // The structural half of the fix, asserted here because the
            // animation half is only correct if this shape holds: a clipping
            // mask the same size as the frame, with the frame inside it.
            var screen = ReckoningScreen.Build();
            var wipe = screen.FrameWipe.Node;

            Assert.IsTrue(wipe.Masks,
                "the wipe does not clip, so opening its width would reveal nothing and hide nothing");

            Assert.AreEqual(ReckoningScreen.PanelWidth, wipe.Size.X, 0.5f);
            Assert.AreEqual(ReckoningScreen.PanelHeight, wipe.Size.Y, 0.5f);

            CollectionAssert.Contains(wipe.Children, screen.Frame.Node,
                "the frame is not inside the mask that is supposed to reveal it");

            // At rest the mask is exactly the frame, so a fully open panel is
            // not clipped at all. A mask even a pixel smaller would shave the
            // painted border permanently.
            Assert.AreEqual(wipe.Size.X, screen.Frame.Node.Size.X, 0.5f);
            Assert.AreEqual(wipe.Size.Y, screen.Frame.Node.Size.Y, 0.5f);
        }

        [Test]
        public void TheSweepIsClippedToThePaintedInteriorRatherThanThePanelEdge()
        {
            // The phase change sends the outgoing cards a full width sideways.
            // Nothing bounded them, so they crossed the gold border and floated
            // over the battlefield before disappearing.
            //
            // Clipped to the INTERIOR, not to the panel: a card has to vanish
            // behind the border, which is the only reading under which a
            // painted frame is a frame rather than a picture of one.
            var clip = Walk(Tree()).Single(n => n.Name == "ReckoningPhaseClip");

            Assert.IsTrue(clip.Masks, "the phases are unbounded again");
            Assert.AreEqual(ReckoningScreen.BorderInsetX, clip.Place.Left, 0.5f);
            Assert.AreEqual(ReckoningScreen.BorderInsetX, clip.Place.Right, 0.5f);

            // SYMMETRIC, and that is load-bearing rather than tidy. Every child
            // of a phase is placed relative to the phase's centre, so insetting
            // by the true vertical border -- 16.7% at the crest against 14.6%
            // at the foot -- would move all of them 9px up.
            Assert.AreEqual(0f, clip.Place.Top, 0.01f,
                "an asymmetric inset shifts every child of both phases");
            Assert.AreEqual(0f, clip.Place.Bottom, 0.01f,
                "an asymmetric inset shifts every child of both phases");
        }

        [Test]
        public void TheOffersWearNoButtonChrome()
        {
            // Called fixed twice, and was not. BuildOffer removed the declared
            // children, but UiEmitter falls back to the shared button sprite
            // for any Ui.Button that does not name one -- so "no SpriteKey"
            // means "the default plate", and the three offers stayed big gold
            // plates behind their own art.
            var screen = ReckoningScreen.Build();

            foreach (var offer in screen.OfferButtons)
            {
                Assert.IsTrue(offer.Node.Chromeless,
                    $"{offer.Node.Name} is back to wearing the shared button plate");
                Assert.IsNull(offer.Node.SpriteKey,
                    $"{offer.Node.Name} names a sprite as well as asking for no chrome - one of the two is a mistake");
            }
        }

        [Test]
        public void NoOfferChildCollidesWithItsButtonsGeneratedCaption()
        {
            // UiEmitter names a button's caption "<button>Label" and it never
            // appears in the tree, so a child by that name is invisible to
            // every by-name lookup. Three screens had shipped that bug.
            // UiTreeTestHelpers.UnthemedButtons narrows past a themed
            // button's own "<Name>Label" child, which is Ui.ApplyTheme's
            // real, intended output (see UiNode.Themed), not the
            // emitter-name collision this check exists to catch.
            var offenders = UiTreeTestHelpers.UnthemedButtons(Tree())
                .SelectMany(b => b.Children.Where(c => c.Name == b.Name + "Label").Select(c => c.Name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void TheBoardsRowsStayApart_AtEveryFrame()
        {
            // Top to bottom: portraits, deeds, the haul (receipts included),
            // the detail's band, Continue. Nothing on the board carries an overlap
            // waiver, so this also holds the audit's word; asserted on the
            // solved rects so a row creeping into the next one names itself.
            foreach (var frame in UiFrames.All)
            {
                var all = UiSolver.Solve(Tree(), frame).Descendants().ToList();
                SolvedNode One(string name) => all.Single(n => n.Name == name);

                var column = new[]
                {
                    "ReckoningPortrait0", "ReckoningDeeds0", "ReckoningHaul", "ReckoningBoardTip",
                    "ReckoningContinueButton",
                };
                for (int i = 1; i < column.Length; i++)
                {
                    var above = One(column[i - 1]).Rect;
                    var below = One(column[i]).Rect;
                    Assert.GreaterOrEqual(above.Bottom, below.Top - 0.01f,
                        $"{column[i - 1]} reaches into {column[i]} at {UiFrames.Describe(frame)}");
                }

                var right = One("ReckoningPortrait2").Rect;
                var gold = One("ReckoningGoldStack").Rect;
                Assert.IsFalse(right.Overlaps(gold), $"the right portrait is under the gold stack at {UiFrames.Describe(frame)}");
            }
        }

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
    }
}
