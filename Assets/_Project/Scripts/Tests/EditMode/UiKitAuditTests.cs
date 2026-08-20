using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The layer's thesis, stated as tests.
    //
    // Two of these recreate bugs that actually SHIPPED in v1 (fixed in 578ba9e
    // and ec19c3e). If the audit does not reject those trees, this whole
    // construction layer has not earned its existence -- so they are written
    // first and treated as the regression suite for everything else here.
    public class UiKitAuditTests
    {
        private static UiAuditError[] Errors(UiNode tree) => UiAudit.RunAllFrames(tree).ToArray();

        private static UiAuditError[] Of(UiNode tree, UiAuditCheck check) =>
            Errors(tree).Where(e => e.Check == check).ToArray();

        // --- the shipped bugs -------------------------------------------------

        [Test]
        public void ShippedBug_578ba9e_CharacterSelectConfirmStealingClicks_IsRejected()
        {
            // v1, verbatim in shape: the character COUNT came from content while
            // the 90px step and Confirm's y = -220 stayed hardcoded. At a roster
            // of five, character 4 spanned y[-240,-180] and Confirm spanned
            // y[-250,-190] - Confirm is the later sibling, so it silently took
            // the fifth character's clicks. Nobody saw it because nothing called
            // ShowOverlay(CharacterSelect) yet.
            const float firstSlotY = 150f;
            const float slotStep = 90f;
            var slot = new UiVec(260f, 60f);

            var tree = Ui.Panel("CharacterSelectPanel", UiSize.Fixed(1920f, 1080f),
                Enumerable.Range(0, 5)
                    .Select(i => Ui.Button($"Character{i}Button", UiString.FromContent("Hero"),
                        slot, 18, Place.At(0f, firstSlotY - i * slotStep)))
                    .Append(Ui.Button("ConfirmButton", UiString.FromContent("Confirm Squad"),
                        slot, 18, Place.At(0f, -220f)))
                    .ToArray());

            var overlaps = Of(tree, UiAuditCheck.SiblingOverlap);

            Assert.IsNotEmpty(overlaps, "the audit failed to catch a collision this project actually shipped");
            var hit = overlaps.First();
            StringAssert.Contains("Character4Button", hit.Message);
            StringAssert.Contains("ConfirmButton", hit.Message);
            StringAssert.Contains("takes the clicks", hit.Message);
            StringAssert.Contains("Fix by", hit.Message);
        }

        [Test]
        public void ShippedBug_578ba9e_TheFlowRewrite_IsClean_AndAbsorbsAGrowingRoster()
        {
            // The same screen expressed as flow: Confirm is simply the next
            // child, so it CANNOT overlap - and the fix that 578ba9e had to make
            // by hand (deriving Confirm's y from the column bottom) is now the
            // default shape rather than a per-site discipline.
            UiNode Build(int roster) => Ui.Panel("CharacterSelectPanel", UiSize.Fixed(1920f, 1080f),
                Ui.Column("CharacterList", Place.At(0f, 0f), spacing: 30f, UiAlign.Centre,
                    Enumerable.Range(0, roster)
                        .Select(i => Ui.Button($"Character{i}Button", UiString.FromContent("Hero"), new UiVec(260f, 60f), 18))
                        .Append(Ui.Button("ConfirmButton", UiString.FromContent("Confirm Squad"), new UiVec(260f, 60f), 18))
                        .ToArray()));

            CollectionAssert.IsEmpty(Errors(Build(5)), "the flow form of the five-character roster should be clean");

            // The roster growing is exactly what broke v1. Here it just works.
            CollectionAssert.IsEmpty(Errors(Build(6)));
            CollectionAssert.IsEmpty(Errors(Build(8)));
        }

        [Test]
        public void ShippedBug_ec19c3e_StoreRowsOverrunningTheirPanel_IsRejected()
        {
            // v1: the builder sized the row strip off ContentDatabase.Items while
            // the controller filled it from Consumables. The counts disagreed,
            // blank rows ran off the bottom of the panel, and nothing noticed
            // because the two numbers lived in two files.
            var tree = Ui.Panel("StorePanel", UiSize.Fixed(1920f, 1080f),
                Ui.Column("StoreRows", Place.At(0f, 0f), spacing: 10f, UiAlign.Centre,
                        Enumerable.Range(0, 12)
                            .Select(i => Ui.Button($"StoreRow{i}", UiString.FromContent("Item"), new UiVec(600f, 70f), 20))
                            .ToArray())
                    .Sized(UiSize.Fixed(600f, 400f)));

            var capacity = Of(tree, UiAuditCheck.FlowCapacity);

            Assert.IsNotEmpty(capacity, "12 rows of 70px cannot fit 400px and the audit should say so");
            var hit = capacity.First();
            StringAssert.Contains("StoreRows", hit.Message);
            StringAssert.Contains("12 children", hit.Message);
            StringAssert.Contains("Fix by", hit.Message);
        }

        [Test]
        public void ShippedBug_ec19c3e_UiEach_MakesTheTwoCountsOneCount()
        {
            // The actual fix is upstream of the audit: there is no second place
            // to state a count. Ui.Each derives the element count, the names and
            // the binding refs from the same collection, so "sized off Items,
            // filled from Consumables" has no way to be written.
            var consumables = new[] { "Potion", "Elixir", "Bandage" };

            var tree = Ui.Panel("StorePanel", UiSize.Fixed(1920f, 1080f),
                Ui.Column("StoreRows", Place.At(0f, 0f), spacing: 10f, UiAlign.Centre,
                    Ui.Each(consumables, (item, i) =>
                        Ui.Button($"StoreRow{i}", UiString.FromContent(item), new UiVec(600f, 70f), 20))));

            CollectionAssert.IsEmpty(Errors(tree));

            var solved = UiSolver.Solve(tree, UiFrames.Reference);
            // By Kind, not by name prefix: the container is called "StoreRows",
            // which also starts with "StoreRow".
            var rows = solved.Descendants().Where(n => n.Kind == UiNodeKind.Button).ToArray();
            Assert.AreEqual(consumables.Length, rows.Length, "the pool size IS the collection length");
        }

        // --- the individual checks -------------------------------------------

        [Test]
        public void A2_ChildEscapingItsParent_IsRejected_UnlessDeclared()
        {
            UiNode Build(bool declared)
            {
                var glow = Ui.Solid("Glow", "#ffffff", new UiVec(400f, 400f), Place.At(0f, 0f));
                if (declared) glow.AllowOverflow("the glow deliberately bleeds past the card it sits behind");
                return Ui.Panel("Card", Place.At(0f, 0f), UiSize.Fixed(200f, 200f), glow);
            }

            var errors = Of(Build(declared: false), UiAuditCheck.ChildContainment);
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("100px past the left", errors.First().Message);
            StringAssert.Contains("AllowOverflow", errors.First().Message);

            CollectionAssert.IsEmpty(Of(Build(declared: true), UiAuditCheck.ChildContainment));
        }

        [Test]
        public void A4_TwoSiblingsWithTheSameName_IsRejected()
        {
            var tree = Ui.Column("Col", Place.At(0f, 0f), 0f, UiAlign.Centre,
                Ui.Solid("Row", "#ffffff", new UiVec(50f, 20f)),
                Ui.Solid("Row", "#ffffff", new UiVec(50f, 20f)));

            var errors = Of(tree, UiAuditCheck.DuplicateName);
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("two children named 'Row'", errors.First().Message);
        }

        [Test]
        public void A6_ZeroSizedVisibleGraphic_IsRejected()
        {
            var tree = Ui.Panel("Root", UiSize.Fixed(400f, 400f),
                Ui.Solid("Invisible", "#ffffff", new UiVec(0f, 40f), Place.At(0f, 0f)));

            var errors = Of(tree, UiAuditCheck.ZeroSizeGraphic);
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("white quad", errors.First().Message,
                "this is the v1 failure mode - a sprite-less Image renders as a solid white box, not as nothing");
        }

        [Test]
        public void A1_RotatedNodes_AreComparedByTheirTrueFootprint()
        {
            // Unrotated these two miss each other; rotated 45 degrees the long
            // one sweeps across. Testing the unrotated box would silently pass -
            // and the talent tree's edges are rotated stripes.
            UiNode Build(float degrees) => Ui.Panel("Root", UiSize.Fixed(800f, 800f),
                Ui.Solid("Edge", "#ffffff", new UiVec(400f, 10f), Place.At(0f, 0f)).Rotated(degrees),
                Ui.Solid("Orb", "#ffffff", new UiVec(40f, 40f), Place.At(0f, 120f)));

            CollectionAssert.IsEmpty(Of(Build(0f), UiAuditCheck.SiblingOverlap),
                "a flat 10px-tall stripe does not reach an orb 120px above it");
            Assert.IsNotEmpty(Of(Build(45f), UiAuditCheck.SiblingOverlap),
                "rotated 45 degrees its footprint is ~290px tall and does reach");
        }

        [Test]
        public void Decoration_IsExemptFromOverlap_BecauseItCannotTakeAClick()
        {
            // v1's main menu had 110 deliberately overlapping ambient glows.
            // Auditing them would produce thousands of errors that all mean
            // "yes, that is the effect" - and the emitter clears raycastTarget
            // across a Decor subtree, so none of them can steal input anyway.
            var tree = Ui.Panel("Root", UiSize.Fixed(800f, 800f),
                Ui.Panel("Ambience", Place.Stretch(), UiSize.Fill,
                    Ui.Solid("Glow0", "#ffffff", new UiVec(200f, 200f), Place.At(0f, 0f)),
                    Ui.Solid("Glow1", "#ffffff", new UiVec(200f, 200f), Place.At(20f, 20f))).AsDecor());

            CollectionAssert.IsEmpty(Of(tree, UiAuditCheck.SiblingOverlap));
        }

        [Test]
        public void Modal_DimmerCoveringItsContent_IsNotReportedAsACollision()
        {
            var tree = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f),
                Ui.Modal("Confirm", "#000000",
                    Ui.Panel("ConfirmBody", Place.At(0f, 0f), UiSize.Fixed(600f, 300f),
                        Ui.Button("Yes", UiString.FromContent("Delete"), new UiVec(200f, 60f), 18, Place.At(-120f, -80f)),
                        Ui.Button("No", UiString.FromContent("Cancel"), new UiVec(200f, 60f), 18, Place.At(120f, -80f)))));

            CollectionAssert.IsEmpty(Errors(tree));
        }

        [Test]
        public void EveryFrame_IsActuallyAudited_NotJustTheReferenceOne()
        {
            // A proportionally-sized banner (40% of canvas width) and a fixed
            // node beside it. At 16:9 the banner's left edge stops 16px short of
            // the fixed node; at 21:9 the same 40% is 264px wider and runs
            // straight through it.
            //
            // Note the direction, because it is the whole reason the sweep is
            // worth its cost: every audit frame is at least as large as the
            // reference one, so ANCHORED nodes only ever move further apart.
            // What varies dangerously is proportional SIZE.
            var tree = Ui.Panel("Root", UiSize.Fill,
                Ui.Solid("Banner", "#ffffff",
                    Place.Frac(new UiVec(0.3f, 0.45f), new UiVec(0.7f, 0.55f)),
                    UiSize.Fill),
                Ui.Solid("SideButton", "#ffffff", new UiVec(100f, 100f), Place.At(-450f, 0f)));

            var overlaps = Of(tree, UiAuditCheck.SiblingOverlap);
            Assert.IsNotEmpty(overlaps, "the ultra-wide frame should surface this");
            CollectionAssert.IsEmpty(
                overlaps.Where(e => e.Frame.Equals(UiFrames.Reference)).ToArray(),
                "and it should NOT be reported at 16:9, where they genuinely clear each other");
            Assert.IsTrue(overlaps.Any(e => e.Frame.Equals(UiFrames.UltraWide)));
        }
        // ---- one reference stage ------------------------------------------------
        //
        // UiAudit re-solves every screen at UiFrames.Reference; SceneBuilder
        // sets the canvas scaler's own reference resolution. Those were two
        // separate literals meaning "the frame everything is authored against",
        // and if they ever disagreed the whole suite would audit every screen at
        // a size the game does not render at -- passing cleanly, about the
        // wrong layout.
        //
        // The scaler is Editor-side and this is a Domain test, so what is
        // asserted here is the value it now reads. The pairing is the point:
        // there is one number, and this says which.
        [Test]
        public void TheFrameEverythingIsAuthoredAgainstIsSixteenByNine()
        {
            Assert.AreEqual(1920f, UiFrames.Reference.X, 0.001f);
            Assert.AreEqual(1080f, UiFrames.Reference.Y, 0.001f);

            // And it is genuinely the FIRST frame audited, so "authored ==
            // rendered" is checked before any stretched aspect is.
            Assert.AreEqual(UiFrames.Reference.X, UiFrames.All[0].X, 0.001f);
            Assert.AreEqual(UiFrames.Reference.Y, UiFrames.All[0].Y, 0.001f);
        }

        // Everything that means "the stage" derives from it rather than
        // restating it. Spot-checked on the three layouts that had their own
        // copy -- the system menu's, the talent page's slide, and the map's
        // viewport height.
        [Test]
        public void TheLayoutsReadTheStageRatherThanRestatingIt()
        {
            Assert.AreEqual(UiFrames.Reference.X, SystemMenuLayout.StageWidth, 0.001f,
                "the system menu has its own idea of how wide the stage is");
            Assert.AreEqual(UiFrames.Reference.Y, SystemMenuLayout.StageHeight, 0.001f,
                "the system menu has its own idea of how tall the stage is");

            Assert.AreEqual(UiFrames.Reference.X, ConstellationLayout.PageStride, 0.001f,
                "a talent page slides by something other than one screen width, so part of the " +
                "next path would sit beside the current one");
        }

        // --- Ui.Exclusive: alternatives, and only against each other ----------

        private static UiNode TwoPages(bool bothActive)
        {
            var a = Ui.Panel("PageA", Place.Stretch(), UiSize.Fill,
                Ui.Solid("PageAFill", "#FFFFFFFF", Place.Stretch(), UiSize.Fill));
            var b = Ui.Panel("PageB", Place.Stretch(), UiSize.Fill,
                Ui.Solid("PageBFill", "#FFFFFFFF", Place.Stretch(), UiSize.Fill));

            if (!bothActive) b.Inactive();
            Ui.Exclusive(a, b);

            return Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), a, b);
        }

        [Test]
        public void ExclusiveSiblings_DoNotCountAsOverlappingEachOther()
        {
            Assert.IsEmpty(Of(TwoPages(bothActive: false), UiAuditCheck.SiblingOverlap),
                "two pages of one book occupy the same box on purpose");
        }

        [Test]
        public void AnExclusiveNode_IsStillCheckedAgainstEverythingElse()
        {
            // THE WHOLE POINT, and the difference from the AllowOverlap these
            // pages used to carry. That was a blanket: A1 skips a pair when
            // EITHER side has a reason, so a page exempted to sit on its
            // sibling pages stopped being checked against the tab bar and the
            // footer too, permanently and invisibly.
            var page = Ui.Panel("PageA", Place.At(0f, 0f), UiSize.Fixed(400f, 400f));
            var other = Ui.Panel("PageB", Place.At(0f, 0f), UiSize.Fixed(400f, 400f)).Inactive();
            Ui.Exclusive(page, other);

            var footer = Ui.Button("Footer", UiString.FromContent("Go"), new UiVec(200f, 60f), 18,
                Place.At(0f, 0f));

            var errors = Of(Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), page, other, footer),
                UiAuditCheck.SiblingOverlap);

            Assert.IsNotEmpty(errors,
                "a page that belongs to an exclusive set is exempt from its ALTERNATIVES, not from " +
                "an unrelated button parked on top of it");
        }

        [Test]
        public void A8_TwoAlternativesStartingActive_IsRejected()
        {
            // "Exactly one is ever active" was written in six screens and
            // checked in none. Forgetting .Inactive() on the second page draws
            // both at once, which reads as a font or rendering fault long before
            // anyone suspects the declaration.
            var errors = Of(TwoPages(bothActive: true), UiAuditCheck.ExclusiveGroupBothActive);

            Assert.IsNotEmpty(errors, "both pages start active and nothing said so");
            StringAssert.Contains("drawn at once", errors[0].Message);
        }

        [Test]
        public void A8_AllAlternativesInactive_IsFine()
        {
            // AT MOST one, not exactly one: the system menu's panes are all
            // inactive until the controller picks one, and that is correct.
            var a = Ui.Panel("PaneA", Place.Stretch(), UiSize.Fill).Inactive();
            var b = Ui.Panel("PaneB", Place.Stretch(), UiSize.Fill).Inactive();
            Ui.Exclusive(a, b);

            Assert.IsEmpty(Of(Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), a, b),
                UiAuditCheck.ExclusiveGroupBothActive));
        }

        [Test]
        public void ABareContainerDrawnOnTop_IsNotAnOverlap()
        {
            // A1's complaint is that the LATER sibling hides the earlier one
            // and takes its clicks. A Panel with no colour emits no Graphic, so
            // it does neither -- it is a coordinate frame, and the screens that
            // used to spend an AllowOverlap saying so were telling the audit
            // something it can read off the tree.
            var button = Ui.Button("Button", UiString.FromContent("Go"), new UiVec(200f, 60f), 18,
                Place.At(0f, 0f));
            var frame = Ui.Panel("Frame", Place.Stretch(), UiSize.Fill,
                Ui.Label("Caption", UiString.FromContent("x"), new UiVec(100f, 20f), 14,
                    place: Place.At(0f, -300f)));

            Assert.IsEmpty(Of(Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), button, frame),
                UiAuditCheck.SiblingOverlap));
        }

        [Test]
        public void ADrawnSiblingOnTopOfABareContainer_IsStillAnOverlap()
        {
            // The direction that still matters, and the reason the rule above
            // is not simply "ignore containers". Here the Solid is on top of
            // whatever the frame contains, which is exactly the case A1 exists
            // for -- the frame being invisible does not make its CONTENTS
            // invisible.
            var frame = Ui.Panel("Frame", Place.Stretch(), UiSize.Fill,
                Ui.Label("Caption", UiString.FromContent("x"), new UiVec(100f, 20f), 14,
                    place: Place.At(0f, 0f)));
            var cover = Ui.Solid("Cover", "#FF0000FF", new UiVec(400f, 400f), Place.At(0f, 0f));

            Assert.IsNotEmpty(Of(Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), frame, cover),
                UiAuditCheck.SiblingOverlap));
        }

        [Test]
        public void AColouredPanelIsAGraphic_AndStillOverlaps()
        {
            // A Panel gets an Image the moment it is given a colour, which is
            // UiEmitter's rule and therefore has to be this one's too. A rule
            // that keyed on Kind alone would wave through a coloured panel
            // parked on a button.
            var button = Ui.Button("Button", UiString.FromContent("Go"), new UiVec(200f, 60f), 18,
                Place.At(0f, 0f));
            var plate = Ui.Panel("Plate", Place.At(0f, 0f), UiSize.Fixed(400f, 400f)).Coloured("#112233FF");

            Assert.IsNotEmpty(Of(Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), button, plate),
                UiAuditCheck.SiblingOverlap));
        }

        [Test]
        public void Exclusive_WithFewerThanTwoMembers_IsRejected()
        {
            // A set of one is not a set of alternatives, and would read as a
            // licence to overlap whatever happened to be nearby -- which is
            // exactly the blanket this replaced.
            var lone = Ui.Panel("Lone", Place.Stretch(), UiSize.Fill);

            Assert.Throws<System.ArgumentException>(() => Ui.Exclusive(lone));
        }

        // --- A7: an allowance that waives nothing -----------------------------
        //
        // Written because A7 is a check that, on this codebase, is expected to
        // find nothing forever -- the 39 sites it was built for were deleted in
        // the same change. A rule with no live instance and no test is a rule
        // nobody has ever seen work, and the whole point of it is to fire years
        // from now for somebody who has not read any of this.

        [Test]
        public void A7_AnAllowOverlapOnDecor_IsRejectedAsInert()
        {
            var tree = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f),
                Ui.Solid("Plate", "#FFFFFFFF", new UiVec(200f, 200f), Place.At(0f, 0f))
                    .AsDecor()
                    .AllowOverlap("this waives nothing, because decoration is already exempt from A1"));

            var errors = Of(tree, UiAuditCheck.InertOverlapAllowance);

            Assert.IsNotEmpty(errors, "A7 did not notice an AllowOverlap sitting on a decor node");
            StringAssert.Contains("waives nothing", errors[0].Message);
        }

        [Test]
        public void A7_AnAllowOverlapUnderADecorParent_IsAlsoRejected()
        {
            // Decor is INHERITED by the whole subtree, so a child of a decor
            // node is exempt from A1 without carrying the flag itself. That case
            // is the one a same-node check would miss, and it is the reason A7
            // lives in the walk rather than in the AllowOverlap setter.
            var tree = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f),
                Ui.Panel("Ambience", UiSize.Fixed(400f, 400f),
                    Ui.Solid("Glow", "#FFFFFFFF", new UiVec(200f, 200f), Place.At(0f, 0f))
                        .AllowOverlap("inherited decor still means this allowance waives nothing"))
                    .AsDecor());

            Assert.IsNotEmpty(Of(tree, UiAuditCheck.InertOverlapAllowance),
                "A7 only looks at the node's own Decor flag, so an inherited one slips past it");
        }

        [Test]
        public void A7_AnAllowOverlapOnAnOrdinaryNode_IsLeftAlone()
        {
            // The other half, and the one that keeps A7 from being a rule
            // against AllowOverlap itself. On a node that can take a click, the
            // allowance is load-bearing and must not be reported.
            var tree = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f),
                Ui.Button("A", UiString.FromContent("A"), new UiVec(200f, 60f), 18, Place.At(0f, 0f))
                    .AllowOverlap("this one is real: the button takes clicks and still sits on its neighbour"),
                Ui.Button("B", UiString.FromContent("B"), new UiVec(200f, 60f), 18, Place.At(0f, 20f)));

            Assert.IsEmpty(Of(tree, UiAuditCheck.InertOverlapAllowance),
                "A7 fired on a node that is not decoration, where the allowance does real work");
        }
    }
}
