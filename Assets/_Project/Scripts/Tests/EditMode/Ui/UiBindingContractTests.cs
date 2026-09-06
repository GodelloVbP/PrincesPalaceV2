using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // What a wrong binding SAYS, pinned where it can be read without a scene.
    //
    // The message is the whole deliverable of the binding audit. A build-time
    // check that refuses correctly and reports "1 problem" costs its reader
    // the same hunt the check was built to remove -- which controller, which
    // field, what it should have been, what it was. So the four facts are the
    // thing under test, by content and not by shape: each assertion below
    // names a fact, so dropping one fails on that fact rather than on a
    // formatting change.
    //
    // The comparison itself lives in Editor's UiBindingAudit, because "is
    // this the same GameObject" needs an engine. This file covers the half
    // that can be reached from here; the audit's own half is proved by
    // deliberately mis-binding a real screen and reading the build's refusal.
    public class UiBindingContractTests
    {
        private const string Controller = "MapController";
        private const string Field = "floorLabel";
        private const string Expected = "FloorLabel";
        private const string Actual = "SeedLabel";

        [Test]
        public void AWrongNodeNamesTheControllerTheFieldTheExpectedNodeAndTheActualOne()
        {
            string message = UiBindingContract.WrongNode(
                Controller, Field, Expected, Actual, UiBindingContract.Source.AutoBound);

            StringAssert.Contains(Controller, message);
            StringAssert.Contains(Field, message);
            StringAssert.Contains(Expected, message);
            StringAssert.Contains(Actual, message);
        }

        // The fix differs by route -- an auto-bound field that landed wrong
        // means the name rule matched something unintended, an explicit one
        // means the Wire line names the wrong NodeRef -- so a reader must be
        // able to tell them apart without opening ScreenRegistry.
        [Test]
        public void TheTwoRoutesReadDifferently()
        {
            string auto = UiBindingContract.WrongNode(
                Controller, Field, Expected, Actual, UiBindingContract.Source.AutoBound);
            string explicitLine = UiBindingContract.WrongNode(
                Controller, Field, Expected, Actual, UiBindingContract.Source.Explicit);

            Assert.AreNotEqual(auto, explicitLine);
            StringAssert.Contains("auto-bound", auto);
            StringAssert.Contains("explicit", explicitLine);
        }

        // A component from outside the emitted tree is a different fault from
        // a wrong node, and saying "wrong node" about it would send the
        // reader looking for a node that does not exist.
        [Test]
        public void AComponentFromOutsideTheScreenSaysSoRatherThanNamingANode()
        {
            string message = UiBindingContract.NotFromThisScreen(Controller, Field, Actual);

            StringAssert.Contains(Controller, message);
            StringAssert.Contains(Field, message);
            StringAssert.Contains(Actual, message);
            StringAssert.Contains("never emitted", message);
        }

        // The auditor could reach a state where it held a provenance and had
        // no object to compare against, and it returned as though the field
        // were fine. The message for that has to say which of the two it could
        // not do -- name the node it expected, and say plainly that no
        // comparison happened -- or a reader takes it for a wrong-node finding
        // and goes hunting a binding that is not the problem.
        [Test]
        public void ANodeTheScreenNeverEmittedSaysNoComparisonWasPossible()
        {
            string message = UiBindingContract.ProvenanceNodeNotEmitted(Controller, Field, Expected);

            StringAssert.Contains(Controller, message);
            StringAssert.Contains(Field, message);
            StringAssert.Contains(Expected, message);
            StringAssert.Contains("no object for that node", message);

            // And it is neither of the other two, which is the distinction the
            // reader acts on.
            Assert.AreNotEqual(
                UiBindingContract.NotFromThisScreen(Controller, Field, Expected), message);
            Assert.AreNotEqual(
                UiBindingContract.WrongNode(Controller, Field, Expected, Actual,
                    UiBindingContract.Source.Explicit), message);
        }

        [Test]
        public void TheHeaderNamesTheScreenAndHowManyFieldsAreWrong()
        {
            string header = UiBindingContract.Header("Map", 3);

            StringAssert.Contains("Map", header);
            StringAssert.Contains("3", header);
        }
    }
}
