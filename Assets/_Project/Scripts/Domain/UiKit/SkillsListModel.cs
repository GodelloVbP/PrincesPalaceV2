using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.UiKit
{
    public enum SkillsEntryKind
    {
        Header,
        Skill,
        Talent,
        Note,
        Link,
    }

    // One skill as the list needs it: resolved to plain words by Core, which
    // holds the content this is built from.
    public readonly struct SkillListRow
    {
        public readonly SkillSource Source;
        public readonly string Name;
        public readonly string Meta;
        public readonly string Description;

        public SkillListRow(SkillSource source, string name, string meta, string description)
        {
            Source = source;
            Name = name ?? "";
            Meta = meta ?? "";
            Description = description ?? "";
        }
    }

    public readonly struct TalentListRow
    {
        public readonly string Name;
        public readonly string Description;

        public TalentListRow(string name, string description)
        {
            Name = name ?? "";
            Description = description ?? "";
        }
    }

    public sealed class SkillsEntry
    {
        public readonly SkillsEntryKind Kind;

        // A header's or note's words, a link's label, a row's name.
        public readonly string Title;

        // A skill row's right-hand line; empty for a talent.
        public readonly string Meta;
        public readonly string Body;

        public SkillsEntry(SkillsEntryKind kind, string title, string meta = "", string body = "")
        {
            Kind = kind;
            Title = title ?? "";
            Meta = meta ?? "";
            Body = body ?? "";
        }

        // What the pad can stand on. Headers and notes only inform; the rows
        // are focusable so the pad can scroll to them and read them, though a
        // press does nothing (the link is the one entry a press does anything on).
        public bool Focusable => Kind == SkillsEntryKind.Skill || Kind == SkillsEntryKind.Talent
                                 || Kind == SkillsEntryKind.Link;
    }

    // The SKILLS section's list (plan C18, spec 3.3): what is in it, in what
    // order, and which of it a window of the pane's height shows.
    //
    // Pure, like the pack's row arithmetic. The list is windowed by whole
    // ENTRIES rather than pixels, so a row is never cut in half at the window's
    // edge and a wheel notch or a Down press steps one entry. The window holds
    // a bounded number of each kind (the pane pools its nodes by kind), and Fit
    // stops at that bound, so the pools cannot be overrun by any list.
    public static class SkillsListModel
    {
        public static float HeightOf(SkillsEntryKind kind)
        {
            switch (kind)
            {
                case SkillsEntryKind.Header: return DossierLayout.SkillsHeaderHeight;
                case SkillsEntryKind.Skill:
                case SkillsEntryKind.Talent: return DossierLayout.SkillsRowHeight;
                case SkillsEntryKind.Note: return DossierLayout.SkillsNoteHeight;
                case SkillsEntryKind.Link: return DossierLayout.SkillsLinkHeight;
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind,
                    "SkillsEntryKind has no height -- add a case above.");
            }
        }

        // How many of a kind one window can hold. A skill row and a talent row
        // are the same node.
        public static int SlotsOf(SkillsEntryKind kind)
        {
            switch (kind)
            {
                case SkillsEntryKind.Header: return DossierLayout.SkillsHeaderSlots;
                case SkillsEntryKind.Skill:
                case SkillsEntryKind.Talent: return DossierLayout.SkillsRowSlots;
                case SkillsEntryKind.Note: return DossierLayout.SkillsNoteSlots;
                case SkillsEntryKind.Link: return 1;
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind,
                    "SkillsEntryKind has no slot count -- add a case above.");
            }
        }

        // The skills grouped by source in SkillSource order (empty groups
        // hidden), then the talents, the embers line and, at camp, the link.
        // `skills` arrive in the order the character's strip has them, and each
        // group keeps it.
        public static List<SkillsEntry> Build(IReadOnlyList<SkillListRow> skills,
                                              IReadOnlyList<TalentListRow> talents,
                                              int embersLeft, bool showLink)
        {
            var entries = new List<SkillsEntry>();

            if (skills == null || skills.Count == 0)
            {
                entries.Add(new SkillsEntry(SkillsEntryKind.Note, UiStrings.SkillsEmpty.Format()));
            }
            else
            {
                foreach (SkillSource source in System.Enum.GetValues(typeof(SkillSource)))
                {
                    bool headed = false;
                    foreach (var row in skills)
                    {
                        if (row.Source != source) continue;

                        if (!headed)
                        {
                            entries.Add(new SkillsEntry(SkillsEntryKind.Header, SkillListing.HeaderFor(source).Format()));
                            headed = true;
                        }

                        entries.Add(new SkillsEntry(SkillsEntryKind.Skill, row.Name, row.Meta, row.Description));
                    }
                }
            }

            entries.Add(new SkillsEntry(SkillsEntryKind.Header, UiStrings.SkillsTalentsHeading.Format()));

            if (talents == null || talents.Count == 0)
            {
                entries.Add(new SkillsEntry(SkillsEntryKind.Note, UiStrings.SkillsTalentsNone.Format()));
            }
            else
            {
                foreach (var talent in talents)
                {
                    entries.Add(new SkillsEntry(SkillsEntryKind.Talent, talent.Name, "", talent.Description));
                }
            }

            entries.Add(new SkillsEntry(SkillsEntryKind.Note,
                embersLeft == 1 ? UiStrings.SkillsEmbersOne.Format() : UiStrings.SkillsEmbers.Format(embersLeft)));

            if (showLink)
            {
                entries.Add(new SkillsEntry(SkillsEntryKind.Link, UiStrings.SkillsConstellationLink.Format()));
            }

            return entries;
        }

        // How many entries from `top` fit whole in the window, stopping early
        // where a kind's slots run out.
        public static int Fit(IReadOnlyList<SkillsEntry> entries, int top, float viewport)
        {
            int headers = 0, rows = 0, notes = 0, links = 0;
            float used = 0f;
            int count = 0;

            for (int i = System.Math.Max(0, top); i < entries.Count; i++)
            {
                var kind = entries[i].Kind;
                used += HeightOf(kind);
                if (used > viewport + 0.001f) break;

                int taken;
                switch (kind)
                {
                    case SkillsEntryKind.Header: taken = ++headers; break;
                    case SkillsEntryKind.Skill:
                    case SkillsEntryKind.Talent: taken = ++rows; break;
                    case SkillsEntryKind.Note: taken = ++notes; break;
                    default: taken = ++links; break;
                }

                if (taken > SlotsOf(kind)) break;
                count++;
            }

            return count;
        }

        // The furthest the window scrolls: the first top from which everything
        // after fits, so the list ends at the window's foot rather than
        // scrolling on into empty space.
        public static int MaxTop(IReadOnlyList<SkillsEntry> entries, float viewport)
        {
            for (int top = 0; top < entries.Count; top++)
            {
                if (top + Fit(entries, top, viewport) >= entries.Count) return top;
            }

            return 0;
        }

        // The window's top after asking for `index` to be on screen, moving as
        // little as it can. Reaching up to a row that opens a group brings its
        // header along, so the group's name is never scrolled out from under it.
        public static int TopToReveal(IReadOnlyList<SkillsEntry> entries, int top, int index, float viewport)
        {
            if (index < 0 || index >= entries.Count) return top;

            if (index < top)
            {
                bool opensGroup = index > 0 && entries[index - 1].Kind == SkillsEntryKind.Header;
                return opensGroup ? index - 1 : index;
            }

            while (index >= top + Fit(entries, top, viewport) && top < index) top++;
            return top;
        }

        // The next entry the pad can stand on, `step` (+1 or -1) from `from`;
        // -1 when there is none that way.
        public static int NextFocusable(IReadOnlyList<SkillsEntry> entries, int from, int step)
        {
            for (int i = from + step; i >= 0 && i < entries.Count; i += step)
            {
                if (entries[i].Focusable) return i;
            }

            return -1;
        }
    }
}
