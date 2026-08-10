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
    }
}
