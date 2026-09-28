using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // P5's own art check, over the two per-TrackReward lookups ScreenRegistry
    // sizes and binds: markByReward (the
    // rail's mark, RewardTrackLayout.IconFor) and cardArtByReward (the focus
    // card's medallion, RewardTrackLayout.CardArtKeyFor).
    //
    // NOT re-proving RewardTrackDefinitionTests.
    // EveryRewardKindResolvesAnArtKeyAndAName, which already walks the same
    // enum and asserts both lookups resolve per kind -- that is a Domain-layer
    // fact about the switch statements themselves. What is new here, and is a
    // UI-layer property rather than a Domain one, is what the CARD actually
    // needs: a visual that tells this reward kind apart from every other. Two
    // kinds sharing one would still compile and still resolve -- the card would
    // simply draw the same thing for both, with nothing in a diff to say so.
    //
    // THE RULE IS "A DISTINCT VISUAL", NOT "A DISTINCT SPRITE", and phase 5
    // changed it deliberately rather than relaxing it. The old wording forced
    // twenty-one pictures out of a set that honestly contains nine, and phase 4
    // met it by borrowing status icons -- writing in its own note that four of
    // them were "chosen because they are different from each other and for no
    // other reason". A rule that can only be satisfied by an arbitrary answer
    // produces arbitrary answers. So a kind's visual is now its medallion OR
    // its word (RewardTrackLayout.CardVisualKeyFor), exactly one of the two,
    // and the distinctness this file cares about is checked over both channels
    // at once -- which is strictly stronger than the old check, because it also
    // refuses a word that collides with another word.
    public class RewardTrackScreenTests
    {
        [Test]
        public void EveryRewardKindBindsAMarkAndADistinctCardVisual()
        {
            var visuals = new HashSet<string>();
            int kindCount = 0;

            foreach (TrackReward reward in Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;
                kindCount++;

                string mark = RewardTrackLayout.IconFor(reward);
                string visual = RewardTrackLayout.CardVisualKeyFor(reward);

                Assert.IsFalse(string.IsNullOrEmpty(mark), $"{reward} has no rail mark key");
                Assert.IsFalse(string.IsNullOrEmpty(visual),
                    $"{reward} has neither a card medallion nor a card glyph -- the plate would draw nothing");

                Assert.IsTrue(visuals.Add(visual),
                    $"{reward}'s card visual ({visual}) is shared with another reward kind -- " +
                    "the card would draw the same thing for both");
            }

            Assert.AreEqual(kindCount, visuals.Count,
                "every reward kind should resolve to a card visual nothing else resolves to");
        }

        // EXACTLY ONE OF THE TWO, for every kind. Both would draw a word over a
        // medallion in the same 86px rect; neither is the blank plate the old
        // arrangement was trying to avoid.
        [Test]
        public void AKindHasAMedallionOrAWordAndNeverBoth()
        {
            foreach (TrackReward reward in Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;

                bool art = !string.IsNullOrEmpty(RewardTrackLayout.CardArtKeyFor(reward));
                bool glyph = !string.IsNullOrEmpty(RewardTrackLayout.CardGlyphFor(reward));

                Assert.AreNotEqual(art, glyph,
                    $"{reward} resolves {(art ? "both a medallion and a word" : "neither a medallion nor a word")}");
            }
        }

        // SIX CHARACTERS, because the slot is 86 wide at 26pt in Chakra Petch
        // and the fit audit does not measure runtime text. Nothing else keeps
        // this ceiling; a seventh letter would touch the mat's own edges and no
        // build would say so.
        [Test]
        public void NoCardGlyphIsWiderThanTheSlot()
        {
            foreach (TrackReward reward in Enum.GetValues(typeof(TrackReward)))
            {
                string glyph = RewardTrackLayout.CardGlyphFor(reward);
                if (string.IsNullOrEmpty(glyph)) continue;

                Assert.LessOrEqual(glyph.Length, 6,
                    $"{reward}'s card glyph '{glyph}' is longer than the 86px plate can hold at " +
                    $"{RewardTrackScreen.CardGlyphFont}pt");

                Assert.AreEqual(glyph.ToUpperInvariant(), glyph,
                    $"{reward}'s card glyph '{glyph}' is not upper case -- every token on this screen is");
            }
        }

        // NO STATUS ICON IS LEFT ON THIS SCREEN. Phase 4 borrowed seven from
        // Art/UI/Status/Processed -- flat cel art in cool violets, on a card
        // whose every other slot is a painted gold medallion -- and phase 5
        // took them back off. Asserted as a PREFIX check rather than by listing
        // the seven, so borrowing an eighth fails too.
        [Test]
        public void NoCardMedallionComesFromTheStatusSet()
        {
            foreach (TrackReward reward in Enum.GetValues(typeof(TrackReward)))
            {
                string key = RewardTrackLayout.CardArtKeyFor(reward);
                if (string.IsNullOrEmpty(key)) continue;

                StringAssert.StartsWith(RewardTrackScreen.IconRoot, key,
                    $"{reward}'s medallion is not from the talent tree's painted set");
            }
        }

        // ---- the tree itself -------------------------------------------------
        //
        // THIS SCREEN HAD NO EDITMODE AUDIT, alone among the panes, and the
        // absence cost a full six-minute scene build to find every overlap.
        // Every other screen's own *ScreenTests carries one line of this;
        // RewardTrackScreen was reachable only through SystemMenuScreen.Build,
        // which no EditMode test solves. UiAudit.RunAllFrames is the same four
        // canvas aspects UiEmitter runs at scene-build time, so a failure here
        // is the failure the build would have reported, four seconds sooner.
        [Test]
        public void TheTreeAuditsCleanAtEveryAspect()
        {
            var errors = UiAudit.RunAllFrames(RewardTrackScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 8 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(8).Select(e => e.ToString())));
        }

        // ---- completion, and the ten levels past it ---------------------------

        // THE DIVIDER SITS IN THE GAP BETWEEN TWO CAPTIONS, which is the whole
        // reason it needs no overlap allowance. A caption box is 170 on a 190
        // pitch, so every midpoint has 20px of clear air; this asserts the
        // divider is inside that and not merely thin.
        [Test]
        public void ThePrestigeDividerFitsInTheGapBetweenTwoCaptions()
        {
            float gap = RewardTrackLayout.NodePitch - RewardTrackLayout.CaptionWidth;
            Assert.AreEqual(20f, gap, 0.01f, "the caption/pitch relationship moved -- repin the divider");
            Assert.Less(RewardTrackLayout.PrestigeDividerWidth, gap,
                "the divider is wider than the clear air between two captions");

            // Halfway between the last combat node and the first identity one,
            // not on either of them.
            float thirty = RewardTrackLayout.NodeOffsetX(RewardTrack.CompletionLevel);
            float thirtyOne = RewardTrackLayout.NodeOffsetX(RewardTrack.CompletionLevel + 1);
            Assert.AreEqual((thirty + thirtyOne) * 0.5f, RewardTrackLayout.PrestigeBoundaryOffsetX, 0.01f);
        }

        // THE WASH COVERS THE TEN AND NOTHING ELSE. Its left edge is the
        // boundary and its right edge is the content's own, so level 30 stands
        // off it and level 31 stands on it.
        [Test]
        public void ThePrestigeGroundStartsAtTheBoundaryAndRunsToTheEnd()
        {
            float left = RewardTrackLayout.PrestigeWashOffsetX - RewardTrackLayout.PrestigeWashWidth * 0.5f;
            float right = RewardTrackLayout.PrestigeWashOffsetX + RewardTrackLayout.PrestigeWashWidth * 0.5f;

            Assert.AreEqual(RewardTrackLayout.PrestigeBoundaryOffsetX, left, 0.01f,
                "the wash does not start at the boundary");
            Assert.AreEqual(RewardTrackLayout.ContentWidth * 0.5f, right, 0.01f,
                "the wash stops short of the content's own right edge");

            Assert.Less(RewardTrackLayout.NodeOffsetX(RewardTrack.CompletionLevel), left,
                "level 30 stands on the prestige ground");
            Assert.Greater(RewardTrackLayout.NodeOffsetX(RewardTrack.CompletionLevel + 1), left,
                "level 31 does not stand on the prestige ground");
        }

        // THE WORD SITS BELOW THE LEVEL NUMBER AND ABOVE THE BAND'S FLOOR,
        // which is the only clear strip a node's column has left.
        [Test]
        public void TheStretchWordsSitInTheStripUnderTheLevelNumbers()
        {
            float wordTop = RewardTrackLayout.StretchLabelY + RewardTrackLayout.StretchLabelHeight * 0.5f;
            float wordBottom = RewardTrackLayout.StretchLabelY - RewardTrackLayout.StretchLabelHeight * 0.5f;

            float numberBottom = RewardTrackLayout.LevelNumberY - RewardTrackLayout.LevelNumberHeight * 0.5f;
            Assert.Less(wordTop, numberBottom, "the word runs into the level number above it");

            float contentFloor = -RewardTrackLayout.ScrollContentHeight * 0.5f;
            Assert.Greater(wordBottom, contentFloor, "the word escapes the scrolled content");
        }

        // ---- the card's footer, now two facts wide ----------------------------

        [Test]
        public void TheCardsStateAndFightsLinesShareTheFooterWithoutTouching()
        {
            float stateRight = RewardTrackLayout.CardStateCentreX
                               + RewardTrackLayout.CardStateWordsWidth * 0.5f;
            float fightsLeft = RewardTrackLayout.CardFightsCentreX
                               - RewardTrackLayout.CardFightsWidth * 0.5f;

            Assert.AreEqual(RewardTrackLayout.CardFightsGap, fightsLeft - stateRight, 0.01f,
                "the two halves of the footer do not leave exactly the authored gap between them");

            Assert.AreEqual(RewardTrackLayout.CardTextRight,
                RewardTrackLayout.CardFightsCentreX + RewardTrackLayout.CardFightsWidth * 0.5f, 0.01f,
                "the fights line is not flush with the card's own text column");

            Assert.Greater(RewardTrackLayout.CardFightsWidth, 0f,
                "the state's box has eaten the whole footer");
        }
    }
}
