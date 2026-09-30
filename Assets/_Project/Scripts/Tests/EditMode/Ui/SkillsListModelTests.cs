using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The SKILLS list (plan C18, spec 3.3): its groups, its talents and embers,
    // and the window over it.
    public class SkillsListModelTests
    {
        private static SkillListRow Row(SkillSource source, string name) =>
            new SkillListRow(source, name, "meta", "description");

        private static List<SkillListRow> Skills(params (SkillSource, string)[] rows) =>
            rows.Select(r => Row(r.Item1, r.Item2)).ToList();

        private static string[] Titles(IEnumerable<SkillsEntry> entries) => entries.Select(e => e.Title).ToArray();

        // ---- what is in it ---------------------------------------------------------------

        [Test]
        public void SkillsAreGroupedBySourceInTheGroupsOrderAndEmptyGroupsAreHidden()
        {
            var entries = SkillsListModel.Build(
                Skills((SkillSource.RewardTrack, "Shear"), (SkillSource.Innate, "Slam"),
                       (SkillSource.Talent, "Provoke"), (SkillSource.Innate, "Hack")),
                new List<TalentListRow>(), 0, showLink: false);

            CollectionAssert.AreEqual(
                new[]
                {
                    "INNATE", "Slam", "Hack",
                    "FROM TALENTS", "Provoke",
                    "FROM THE REWARD TRACK", "Shear",
                    "TALENTS", "No talents kindled yet.", "0 embers unspent",
                },
                Titles(entries));
        }

        [Test]
        public void ATalentIsATwoLineRowWithNoRightHandFacts()
        {
            var entries = SkillsListModel.Build(Skills((SkillSource.Innate, "Slam")),
                new List<TalentListRow> { new TalentListRow("Charge", "Unlocks Headbutt.") }, 3, showLink: false);

            var talent = entries.Single(e => e.Kind == SkillsEntryKind.Talent);
            Assert.AreEqual("Charge", talent.Title);
            Assert.AreEqual("Unlocks Headbutt.", talent.Body);
            Assert.AreEqual("", talent.Meta);
            Assert.AreEqual("3 embers unspent", entries[entries.Count - 1].Title);
        }

        [Test]
        public void OneEmberReadsInTheSingular()
        {
            var entries = SkillsListModel.Build(Skills((SkillSource.Innate, "Slam")),
                new List<TalentListRow>(), 1, showLink: false);

            Assert.AreEqual("1 ember unspent", entries[entries.Count - 1].Title);
        }

        [Test]
        public void TheConstellationLinkIsTheLastEntryAndOnlyWhenAsked()
        {
            var with = SkillsListModel.Build(Skills((SkillSource.Innate, "Slam")), new List<TalentListRow>(), 0, showLink: true);
            var without = SkillsListModel.Build(Skills((SkillSource.Innate, "Slam")), new List<TalentListRow>(), 0, showLink: false);

            Assert.AreEqual(SkillsEntryKind.Link, with[with.Count - 1].Kind);
            Assert.AreEqual("Open the constellation", with[with.Count - 1].Title);
            Assert.IsFalse(without.Any(e => e.Kind == SkillsEntryKind.Link));
        }

        [Test]
        public void ACharacterWithNoSkillsSeesTheEmptySentenceInPlaceOfTheGroups()
        {
            var entries = SkillsListModel.Build(new List<SkillListRow>(), new List<TalentListRow>(), 0, showLink: false);

            Assert.AreEqual("Nothing learned yet.", entries[0].Title);
            Assert.AreEqual(SkillsEntryKind.Note, entries[0].Kind);
        }

        [Test]
        public void OnlyRowsAndTheLinkCanTakeTheFocus()
        {
            var entries = SkillsListModel.Build(Skills((SkillSource.Innate, "Slam")),
                new List<TalentListRow> { new TalentListRow("Charge", "x") }, 2, showLink: true);

            CollectionAssert.AreEqual(
                new[] { SkillsEntryKind.Skill, SkillsEntryKind.Talent, SkillsEntryKind.Link },
                entries.Where(e => e.Focusable).Select(e => e.Kind).ToArray());
        }

        // ---- the window -------------------------------------------------------------------

        // Heights: header 44, row 96, note 44, link 64, in a 660 window.
        private static List<SkillsEntry> ManySkills(int count, bool link = true)
        {
            var skills = new List<SkillListRow>();
            for (int i = 0; i < count; i++) skills.Add(Row(SkillSource.Innate, "Skill " + i));
            return SkillsListModel.Build(skills, new List<TalentListRow>(), 4, link);
        }

        [Test]
        public void TheWindowHoldsWholeEntriesOnly()
        {
            var entries = ManySkills(10);

            // header 44 + six rows 576 = 620; a seventh row (716) does not fit.
            Assert.AreEqual(7, SkillsListModel.Fit(entries, 0, 660f));
        }

        [Test]
        public void ASmallListNeverScrolls()
        {
            var entries = ManySkills(2);

            Assert.AreEqual(0, SkillsListModel.MaxTop(entries, 660f));
            Assert.AreEqual(entries.Count, SkillsListModel.Fit(entries, 0, 660f));
        }

        [Test]
        public void TheWindowScrollsExactlyFarEnoughToShowTheEnd()
        {
            var entries = ManySkills(10);
            int max = SkillsListModel.MaxTop(entries, 660f);

            Assert.Greater(max, 0);
            Assert.AreEqual(entries.Count, max + SkillsListModel.Fit(entries, max, 660f), "the last entry is in view at the end");
            Assert.Less(SkillsListModel.Fit(entries, max - 1, 660f) + max - 1, entries.Count,
                "one entry earlier does not reach the end, so the list never scrolls into empty space");
        }

        [Test]
        public void RevealingAnEntryBelowTheWindowMovesItJustFarEnough()
        {
            var entries = ManySkills(10);
            int last = 10;    // header at 0, skills 1..10

            int top = SkillsListModel.TopToReveal(entries, 0, last, 660f);

            Assert.Greater(top, 0);
            Assert.Less(last, top + SkillsListModel.Fit(entries, top, 660f), "the entry is in view");
            Assert.GreaterOrEqual(last, SkillsListModel.Fit(entries, top - 1, 660f) + top - 1,
                "one entry earlier would not have shown it");
        }

        [Test]
        public void RevealingTheFirstRowOfAGroupBringsItsHeaderToo()
        {
            var entries = ManySkills(10);

            Assert.AreEqual(0, SkillsListModel.TopToReveal(entries, 3, 1, 660f));
        }

        [Test]
        public void TheNextFocusableSkipsHeadersAndNotesInBothDirections()
        {
            var entries = SkillsListModel.Build(Skills((SkillSource.Innate, "Slam")),
                new List<TalentListRow>(), 4, showLink: true);
            // 0 INNATE, 1 Slam, 2 TALENTS, 3 none note, 4 embers note, 5 link
            Assert.AreEqual(5, SkillsListModel.NextFocusable(entries, 1, +1));
            Assert.AreEqual(1, SkillsListModel.NextFocusable(entries, 5, -1));
            Assert.AreEqual(-1, SkillsListModel.NextFocusable(entries, 1, -1));
            Assert.AreEqual(-1, SkillsListModel.NextFocusable(entries, 5, +1));
        }

        // The pane pools its nodes by kind, and Fit stops at the pool. For the
        // biggest list the window can show, that bound must never be what
        // decides the window: the height is.
        [Test]
        public void NoWindowIsEverCutShortByThePoolsOfNodes()
        {
            var skills = new List<SkillListRow>();
            foreach (SkillSource source in System.Enum.GetValues(typeof(SkillSource)))
            {
                for (int i = 0; i < 3; i++) skills.Add(Row(source, source + " " + i));
            }

            var entries = SkillsListModel.Build(skills,
                new List<TalentListRow> { new TalentListRow("a", ""), new TalentListRow("b", "") }, 5, showLink: true);

            int heightOnly;
            for (int top = 0; top <= SkillsListModel.MaxTop(entries, 660f); top++)
            {
                float used = 0f;
                heightOnly = 0;
                for (int i = top; i < entries.Count; i++)
                {
                    used += SkillsListModel.HeightOf(entries[i].Kind);
                    if (used > 660f) break;
                    heightOnly++;
                }

                Assert.AreEqual(heightOnly, SkillsListModel.Fit(entries, top, 660f), $"top {top}: a pool ran out before the height did");
            }
        }

        [Test]
        public void TheSlotCountsMatchTheLayoutsBounds()
        {
            // 660 tall holds six 96 rows and no seventh.
            Assert.AreEqual(6, DossierLayout.SkillsRowSlots);
            Assert.AreEqual(96f, DossierLayout.SkillsRowHeight, 0.001f);
            Assert.AreEqual(660f, DossierLayout.DetailHeight, 0.001f);

            // Five sources and TALENTS: six headers, two notes at most and a link.
            Assert.AreEqual(5, System.Enum.GetValues(typeof(SkillSource)).Length);
            Assert.AreEqual(6, DossierLayout.SkillsHeaderSlots);
            Assert.AreEqual(3, DossierLayout.SkillsNoteSlots);
        }
    }
}
