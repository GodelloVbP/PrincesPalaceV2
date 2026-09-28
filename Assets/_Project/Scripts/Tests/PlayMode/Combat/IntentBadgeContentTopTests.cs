using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // AUDIT #79: the intent badge's head height is measured in CANVAS space.
    //
    // PlaceIntentBadge computes `top - groundLine`, and the ground line is
    // authored in canvas pixels up from the canvas's bottom. The measured top
    // used to come back counted from the bottom of the sprite's Tight crop
    // instead, so every trimmed idle parked its badge low by exactly the
    // crop's Y offset -- the same class of bug the foot ring had in X
    // (.claude/rules/ui.md "Sprites").
    //
    // A LITERAL PIN, not a re-scan (CLAUDE.md gotcha 5): a test that re-read
    // the sprite's pixels through textureRect would share the production
    // scan's crop mapping and agree with it however wrong it was. 278 is an
    // offline PIL scan of Resources/Enemies/rat/idle.png (616x306): the
    // topmost row with alpha > 5 is row 28 from the top, so the content top
    // is 306 - 28 = 278 canvas pixels up from the canvas's bottom.
    public class IntentBadgeContentTopTests
    {
        private const string RatFolder = "Enemies/rat";
        private const float RatIdleContentTopCanvasPx = 278f;

        // Texture compression can move an edge alpha by a notch; the bug this
        // guards against is the crop's full Y offset, several times this.
        private const float TolerancePx = 2f;

        [Test]
        public void TheRatsHeadIsMeasuredFromTheCanvasBottomNotTheCropBottom()
        {
            var idle = StanceAnimationLibrary.Resolve(RatFolder, FightSession.Stances.Idle);
            Assert.IsNotNull(idle, $"fixture: {RatFolder}/idle did not load");

            // THE PIN ONLY DISCRIMINATES ON A TRIMMED SPRITE. If the rat's
            // idle stops being cropped above the canvas bottom, crop space and
            // canvas space coincide and this test passes the old bug too --
            // say so loudly rather than keep passing on nothing.
            Assert.Greater(idle.textureRectOffset.y, TolerancePx,
                $"{RatFolder}/idle is no longer Tight-cropped above its canvas bottom " +
                $"(textureRectOffset.y = {idle.textureRectOffset.y}), so crop space equals canvas space " +
                "and this pin has stopped covering AUDIT #79 -- point it at an actor whose idle is trimmed.");

            float top = FightController.ContentTopForActorForTest(RatFolder);

            Assert.AreEqual(RatIdleContentTopCanvasPx, top, TolerancePx,
                $"the rat's idle content top read {top}, not {RatIdleContentTopCanvasPx} canvas px -- " +
                $"{RatIdleContentTopCanvasPx - idle.textureRectOffset.y} would be the crop-space reading " +
                "(the intent badge sitting low by the crop's own Y offset)");
        }
    }
}
