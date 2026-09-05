using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    public class UiKitStringsTests
    {
        [Test]
        public void TheWalletSummary_FormatsIdenticallyForBuilderAndRuntime()
        {
            // The confirmed v1 drift pair, pinned. SceneBuilder.Hub.cs:127 baked
            // "Gold: 0    Relics: 0" while HubController.cs:67 wrote
            // $"Gold: {save.Gold}    Relics: {save.Relics}" - two hand-typed
            // copies of a four-space separator. One entry now serves both, so
            // the initial text and every refresh cannot disagree.
            Assert.AreEqual("Gold: 0    Relics: 0", UiStrings.WalletSummary.Format(0, 0));
            Assert.AreEqual("Gold: 340    Relics: 2", UiStrings.WalletSummary.Format(340, 2));
        }

        [Test]
        public void TemplatedEntries_PinTheirRenderedForm()
        {
            Assert.AreEqual("Floor 7 - 3h 12m", UiStrings.SlotDetail.Format(7, "3h 12m"));
            Assert.AreEqual("340 GOLD", UiStrings.SlotGold.Format(340));
            Assert.AreEqual("Delete slot 2? This cannot be undone.", UiStrings.ConfirmDelete.Format(2));
        }

        [Test]
        public void PlainEntries_FormatToThemselves()
        {
            Assert.AreEqual("Play", UiStrings.Play.Format());
            Assert.AreEqual("C O M M A N D", UiStrings.CommandTitle.Format());
        }

        [Test]
        public void All_EnumeratesEveryDeclaredEntry_AndIsNotVacuouslyShort()
        {
            // A vacuity guard, not a coverage check. All is derived by
            // reflection now, so it cannot miss an entry the way the old
            // hand-written 92-line copy could -- and that is exactly why the
            // three tests below need this one: a walk that silently returned
            // nothing would make all of them pass on an empty set.
            var declared = typeof(UiStrings)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Count(f => f.FieldType == typeof(UiString));

            Assert.AreEqual(declared, UiStrings.All.Length,
                "All must enumerate every public static UiString field on UiStrings");
            Assert.Greater(UiStrings.All.Length, 200,
                "the manifest carries ~300 entries; a count this low means the walk found nothing");
        }

        [Test]
        public void EveryTemplatedEntry_CarriesAnAuditSample_ThatIsNotJustTheTemplate()
        {
            // The text-fit audit measures AuditSample, because a template's real
            // width depends on values that do not exist at build time. An entry
            // that left its sample as the raw template would be measured with
            // "{0}" in place of "999999" and every overflow would ship.
            var offenders = UiStrings.All
                .Where(s => s.IsTemplated && s.AuditSample == s.Template)
                .Select(s => s.Key)
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "These templated entries need a realistic worst-case AuditSample: " + string.Join(", ", offenders));
        }

        [Test]
        public void EveryEntry_HasAUniqueKeyAndANonEmptyTemplate()
        {
            var keys = UiStrings.All.Select(s => s.Key).ToArray();
            CollectionAssert.AllItemsAreUnique(keys);

            foreach (var s in UiStrings.All)
            {
                Assert.IsTrue(s.IsValid, $"'{s.Key}' has no template");
                Assert.IsNotEmpty(s.Template, $"'{s.Key}' is empty");
                Assert.IsFalse(s.IsContent, $"'{s.Key}' is authored copy and must not be marked as content");
            }
        }

        [Test]
        public void FromContent_IsMarkedAsContent_AndPassesItsValueThrough()
        {
            var name = UiString.FromContent("Shawn");
            Assert.IsTrue(name.IsContent);
            Assert.AreEqual("Shawn", name.Format());

            var missing = UiString.FromContent(null);
            Assert.AreEqual(string.Empty, missing.Format(), "missing content degrades to empty, never throws");
        }

        [Test]
        public void AnEmptyUiString_IsRejectedByTheLabelFactory()
        {
            // default(UiString) has no template. Ui.Label rejecting it is what
            // stops a forgotten manifest entry becoming a silently blank label.
            Assert.Throws<System.ArgumentException>(
                () => Ui.Label("Broken", default, new UiVec(100f, 40f)));
        }
    }
}
