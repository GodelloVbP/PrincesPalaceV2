using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The rule UiAutoBind matches on, pinned where it can be read without a
    // scene, a build, or Unity.
    //
    // Worth its own tests because the rule's VALUE is how little it does. A
    // binder that stripped suffixes or scored near-misses would take seven more
    // sites and reintroduce the failure NodeRef was built to kill: a rename that
    // silently lands on a different node. Every case below is one the rule must
    // keep refusing.
    public class UiBindingNamesTests
    {
        [Test]
        public void OnlyTheFirstLetterChangesCase()
        {
            Assert.AreEqual("DetailBody", UiBindingNames.ScreenMemberFor("detailBody"));
            Assert.AreEqual("SubmenuScrollThumb", UiBindingNames.ScreenMemberFor("submenuScrollThumb"));
        }

        [Test]
        public void InnerCapitalsSurviveExactly()
        {
            // Not "Enemyplatehpfills". The screen declares EnemyPlateHpFills and
            // the match has to be character-for-character after the first.
            Assert.AreEqual("EnemyPlateHpFills", UiBindingNames.ScreenMemberFor("enemyPlateHpFills"));
        }

        [Test]
        public void AnAlreadyCapitalisedNameIsReturnedUnchanged()
        {
            Assert.AreEqual("Root", UiBindingNames.ScreenMemberFor("Root"));
        }

        [Test]
        public void NoSuffixIsStripped()
        {
            // The seven real type-suffix pairs in ScreenRegistry -- cardRect
            // from Card, backgroundImage from Background, offerBurstRects from
            // OfferBursts -- stay hand-written on purpose. A rule that reached
            // them would also reach a field whose suffix meant something else.
            Assert.AreEqual("CardRect", UiBindingNames.ScreenMemberFor("cardRect"));
            Assert.AreEqual("BackgroundImage", UiBindingNames.ScreenMemberFor("backgroundImage"));
            Assert.AreEqual("OfferBurstRects", UiBindingNames.ScreenMemberFor("offerBurstRects"));
        }

        [Test]
        public void ADifferentWordIsNeverReached()
        {
            // exitLabels is filled from ExitButtons, and discs from Dots. The
            // rule must produce something those screens do NOT declare, so the
            // binder finds nothing and the explicit line stays load-bearing.
            Assert.AreNotEqual("ExitButtons", UiBindingNames.ScreenMemberFor("exitLabels"));
            Assert.AreNotEqual("Dots", UiBindingNames.ScreenMemberFor("discs"));
        }

        [Test]
        public void EmptyInputIsReturnedAsItCame()
        {
            Assert.IsNull(UiBindingNames.ScreenMemberFor(null));
            Assert.AreEqual("", UiBindingNames.ScreenMemberFor(""));
        }
    }
}
