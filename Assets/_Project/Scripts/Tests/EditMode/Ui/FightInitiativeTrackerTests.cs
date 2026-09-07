using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The turn queue at the top-left of the fight screen, pinned at the one
    // place a headless test can reach it: what the TREE declares each chip is
    // made of.
    //
    // Why this class exists at all. The tracker shipped invisible -- six chips
    // that drew nothing but a capital letter apiece -- and every existing test
    // passed the whole time, because they all check the same two things:
    // FightScreenTests checks the nodes EXIST and are the right count, and
    // TurnOrderTests checks the ORDER handed to them is right. Nothing checked
    // that a chip has anything to draw, which is the only reason the row was
    // ever going to be looked at.
    //
    // The two halves of a chip are filled from opposite directions and only one
    // of them is testable here:
    //
    //   the ICON is per-combatant, so the tree cannot name it and the
    //   controller assigns it every repaint (FightController.Hud
    //   RefreshInitiative, through StanceSpriteFor). That half is Core, needs
    //   an engine, and belongs to PlayMode.
    //
    //   the RING is one fixed piece of art on every chip, so the tree is where
    //   it belongs -- and a tree-declared sprite key is exactly what this host
    //   can read. That is what is pinned below.
    public class FightInitiativeTrackerTests
    {
        private static FightScreen Screen() => FightScreen.Build();

        // THE REGRESSION, stated as the thing that was actually wrong: the
        // emphasis ring was declared `Ui.Sprite(name, null, ...)`, and
        // FightController.ShowSprite refuses to switch on an Image with no
        // sprite (deliberately -- a sprite-less Image renders as a solid white
        // quad, which is the bug that rule was written for). A null key there
        // therefore does not mean "blank until the controller fills it", as it
        // does for the icon beside it; it means "can never be visible", because
        // nothing anywhere assigns to initiativeRings[i].sprite.
        [Test]
        public void EveryInitiativeRingDeclaresItsArt()
        {
            var s = Screen();

            Assert.AreEqual(FightHudSpec.InitiativeSlots, s.InitiativeRings.Count,
                "the tracker should declare one ring per slot");

            foreach (var ringRef in s.InitiativeRings)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ringRef.Node.SpriteKey),
                    $"'{ringRef.Node.Name}' names no art, so ShowSprite can never switch it on and the " +
                    "acting combatant is never ringed -- see FightController.Hud.RefreshInitiative");
            }
        }

        // The counterpart, and the reason the check above cannot simply be
        // "no Sprite node in this tree has a null key". The icon's key is null
        // ON PURPOSE: it is per-combatant art the controller assigns each
        // repaint. Stated here so a later reader can tell the two nulls apart
        // without re-deriving which one was the bug.
        [Test]
        public void EveryInitiativeIconLeavesItsArtToTheController()
        {
            var s = Screen();

            foreach (var iconRef in s.InitiativeIcons)
            {
                Assert.IsTrue(string.IsNullOrEmpty(iconRef.Node.SpriteKey),
                    $"'{iconRef.Node.Name}' bakes a sprite into the tree, but the chip is supposed to carry " +
                    "the combatant's own idle pose, which only RefreshInitiative can know");
            }
        }

        // Both halves of a chip are stacked on the same rect, so the ring is
        // useless unless it reaches OUTSIDE the icon it frames. Pinned because
        // the padding is the only thing making the ring visible at all once it
        // has art, and a later tidy-up of the layout constants would silently
        // hide it again.
        [Test]
        public void TheRingFramesTheIconFromOutsideIt()
        {
            var s = Screen();

            Assert.Greater(Domain.Stage.FightStageAnchors.InitiativeRingPadding, 0f,
                "a ring inset to the icon's own bounds is drawn underneath it and reads as nothing");
        }
    }
}
