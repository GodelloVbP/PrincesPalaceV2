using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHAT A NAVCONTEXT DOES WITH A PRESS IT WAS HANDED, provable without a
    // scene because NavContext is engine-free by construction
    // (docs/CODE_STANDARDS.md "Layering"). NavContextStackTests owns which
    // context is top; this owns what that context does once it is.
    //
    // Three presses share one shape -- Cancel, the shoulders' tab step, the
    // triggers' section step -- and one of them does not: Start reports back
    // whether it was answered, because escape drives both the SystemMenu and
    // the Cancel axis and only the handler that ran can say which of the two
    // a frame's press was spent on. That asymmetry is the thing most worth
    // pinning here, since nothing about the call site looks different.
    public class NavContextDispatchTests
    {
        private sealed class Strip : INavTabStrip, INavSectionStrip
        {
            public readonly List<int> TabSteps = new List<int>();
            public readonly List<int> SectionSteps = new List<int>();

            public void StepTab(int direction) => TabSteps.Add(direction);
            public void StepSection(int direction) => SectionSteps.Add(direction);
        }

        private static NavContext Bare() => new NavContext(null, null, null);

        // ---- section step (the triggers) ---------------------------------------

        [Test]
        public void RaiseSectionStep_OnAContextWithNoSectionStrip_IsAbsorbed()
        {
            // Every context but the few that declare one -- a trigger pull on
            // the hub, the map, a modal, must reach nothing rather than fall
            // through to something that looked close enough.
            Assert.DoesNotThrow(() => Bare().RaiseSectionStep(-1));
            Assert.DoesNotThrow(() => Bare().RaiseSectionStep(1));
        }

        [Test]
        public void RaiseSectionStep_OnAContextWhoseProviderAnswersNull_IsAbsorbed()
        {
            // The provider is a FUNCTION so it can answer "none right now"
            // (NavContext's own header on why the claimant is one), and a
            // null answer has to be as safe as no provider at all.
            var context = new NavContext(null, null, null, sectionStrip: () => null);

            Assert.DoesNotThrow(() => context.RaiseSectionStep(1));
        }

        [Test]
        public void RaiseSectionStep_PassesTheDirectionStraightThrough()
        {
            var strip = new Strip();
            var context = new NavContext(null, null, null, sectionStrip: () => strip);

            context.RaiseSectionStep(-1);
            context.RaiseSectionStep(1);

            Assert.AreEqual(new[] { -1, 1 }, strip.SectionSteps);
        }

        [Test]
        public void TheTwoStripsAreSeparateOffers_ATriggerNeverReachesTheTabStrip()
        {
            // One object can be both, and that is exactly the case where a
            // mix-up would be invisible: the shoulders and the triggers are
            // different controls, and a context is free to declare either,
            // both, or neither.
            var strip = new Strip();
            var context = new NavContext(null, null, null,
                tabStrip: () => strip, sectionStrip: () => strip);

            context.RaiseSectionStep(1);

            Assert.AreEqual(new[] { 1 }, strip.SectionSteps);
            CollectionAssert.IsEmpty(strip.TabSteps, "a trigger pull reached the tab strip");
        }

        [Test]
        public void ATabStripAloneDoesNotAnswerATrigger()
        {
            var strip = new Strip();
            var context = new NavContext(null, null, null, tabStrip: () => strip);

            context.RaiseSectionStep(-1);

            CollectionAssert.IsEmpty(strip.SectionSteps);
            CollectionAssert.IsEmpty(strip.TabSteps);
        }

        // ---- the Start button ----------------------------------------------------

        [Test]
        public void RaiseSystemMenu_WithNoHandler_AbsorbsThePressAndSaysSo()
        {
            // The false is what the dispatcher acts on: it means "this press
            // is still unspent", which is what keeps escape closing a modal
            // that has no menu to open.
            Assert.IsFalse(Bare().RaiseSystemMenu());
        }

        [Test]
        public void RaiseSystemMenu_WithAHandler_RunsItOnceAndReportsItHandled()
        {
            int opened = 0;
            var context = new NavContext(null, null, null, systemMenu: () => opened++);

            Assert.IsTrue(context.RaiseSystemMenu());
            Assert.AreEqual(1, opened);
        }

        [Test]
        public void RaiseSystemMenu_ReportsHandled_EvenWhenItsHandlerDoesNothingVisible()
        {
            // The menu's own handler legitimately does nothing when a pane
            // claims the press (INavCancelClaim -- Party mid-carry), and that
            // press is still SPENT. Reporting it unhandled would hand the
            // same press to Cancel and close the menu over the open carry,
            // which is the exact hazard the claim exists to prevent.
            var context = new NavContext(null, null, null, systemMenu: () => { });

            Assert.IsTrue(context.RaiseSystemMenu());
        }

        [Test]
        public void StartAndCancelAreSeparateHandlers_NeitherStandsInForTheOther()
        {
            int cancelled = 0;
            int opened = 0;
            var context = new NavContext(null, null, cancel: () => cancelled++, systemMenu: () => opened++);

            context.RaiseSystemMenu();
            Assert.AreEqual(1, opened);
            Assert.AreEqual(0, cancelled, "Start fell through to Cancel");

            context.RaiseCancel();
            Assert.AreEqual(1, cancelled);
            Assert.AreEqual(1, opened, "Cancel reached the systemMenu handler");
        }

        [Test]
        public void ARootsCancelBeingNull_IsNotAnError()
        {
            // Hub, Map and Fight are roots: there is no level above them to
            // step back to, so their Cancel is deliberately nothing at all
            // rather than a second way into the menu.
            var root = new NavContext(null, null, cancel: null, systemMenu: () => { });

            Assert.DoesNotThrow(() => root.RaiseCancel());
        }

        // ---- the Fight shape -----------------------------------------------------

        [Test]
        public void ForFight_CarriesASystemMenuHandler_WithoutGainingASelectableSet()
        {
            // The one handler the non-selecting shape shares with the
            // ordinary one. It must not drag any of the rest along with it:
            // Fight has no GameObject to select, ever (plan section 3), and
            // a Fight context that started answering ContainsSelectable would
            // break the dispatcher's own branch condition.
            int opened = 0;
            var fight = NavContext.ForFight(null, systemMenu: () => opened++);

            Assert.IsTrue(fight.RaiseSystemMenu());
            Assert.AreEqual(1, opened);
            Assert.IsTrue(fight.IsNonSelecting);
            Assert.IsNull(fight.Entry);
            CollectionAssert.IsEmpty(fight.Selectables);
        }

        [Test]
        public void ForFight_WithNoHandler_AbsorbsStartTheSameWayAnyOtherContextDoes()
        {
            Assert.IsFalse(NavContext.ForFight(null).RaiseSystemMenu());
        }

        // ---- submit claim (the dialogue stage) ---------------------------------

        private sealed class SubmitClaim : INavSubmitClaim
        {
            public bool Answer;
            public int Asked;

            public bool ClaimSubmit()
            {
                Asked++;
                return Answer;
            }
        }

        [Test]
        public void RaiseSubmit_OnAContextWithNoClaim_AnswersFalse()
        {
            // False is what leaves Submit to the selected Button, untouched:
            // every context but the event panel's.
            Assert.IsFalse(Bare().RaiseSubmit());
            Assert.IsFalse(new NavContext(null, null, null, submitClaim: () => null).RaiseSubmit());
        }

        [Test]
        public void RaiseSubmit_AsksTheClaimEachPress_AndReportsItsAnswer()
        {
            var claim = new SubmitClaim { Answer = true };
            var context = new NavContext(null, null, null, submitClaim: () => claim);

            Assert.IsTrue(context.RaiseSubmit());
            claim.Answer = false;
            Assert.IsFalse(context.RaiseSubmit());
            Assert.AreEqual(2, claim.Asked);
        }
    }
}
