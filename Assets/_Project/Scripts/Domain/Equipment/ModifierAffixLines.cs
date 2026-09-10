using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Equipment
{
    // What a rolled modifier's effects say, as text -- the AFFIXES-section
    // half of what ItemStatLines already does for flat stats. Same split as
    // that file (see its own header): this takes ALREADY-RESOLVED plain
    // data -- a modifier's id, its display name, and its already-scaled
    // ModifierEffect(s) -- and returns the GAIN/LOSS/unchanged classified,
    // formatted lines. No ContentDatabase call and no ModifierDefinition
    // reference anywhere in here.
    //
    // IN DOMAIN, where v1 (and v2 until now) had this in Core.ItemDescription
    // -- moved for the identical reason ItemStatLines was: reaching
    // ContentDatabase.ModifierEffectsForItem/GetModifier directly meant the
    // gain/loss/neutral affix-diff logic could only be exercised through the
    // PlayMode/ScriptableObject content pipeline, never the cheap
    // Domain-only EditMode suite. Core.ItemDescription is the thin adapter
    // that resolves those ContentDatabase calls first and hands the plain
    // result here.
    public static class ModifierAffixLines
    {
        // One resolved modifier effect: the id GroupBy below keys against,
        // the name it prints under, and the one scaled effect it grants. A
        // modifier authoring several effects appears as several entries
        // sharing the same Id/DisplayName -- LinePairs' GroupBy(Id) re-merges
        // them onto one line, the same job Core.ItemDescription's own
        // GroupBy(ModifierDefinition) did before this moved.
        public readonly struct Effect
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly ModifierEffect Value;

            public Effect(string id, string displayName, ModifierEffect value)
            {
                Id = id;
                DisplayName = displayName;
                Value = value;
            }
        }

        // A modifier id currently worn in the slot a candidate would occupy
        // -- just enough to name it in a "Losing: <name>" line when the
        // candidate does not carry it too. No effect data: a lost affix's
        // own numbers are not shown, only that it would be gone.
        public readonly struct EquippedModifier
        {
            public readonly string Id;
            public readonly string DisplayName;

            public EquippedModifier(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }
        }

        // "Fiery -- +20% Fire dmg on hit", one line per modifier -- grouped
        // by Id so a modifier authoring several effects still prints one
        // line, joining its fragments with ", ". GroupBy over an
        // already-ordered `effects` list preserves first-seen order (LINQ's
        // own documented behaviour), so modifiers print in the same order
        // they were rolled/authored in, not resorted.
        //
        // Null/empty input and an input whose every fragment formats empty
        // both return null, never an empty list -- the same "nothing to
        // show" signal ModifierComparisonLines below and
        // ItemStatLines.ModifierSection both key off of.
        //
        // `primaryPool` is the VIEWER's pool, passed straight through to
        // ModifierEffectText so a Max Mana affix can say "no effect on Fury"
        // to the character it would do nothing for. Optional and
        // null-means-mana: a listing with no viewer (the shop's plain card)
        // reads exactly as it always has.
        public static List<(string Id, string Line)> LinePairs(IReadOnlyList<Effect> effects,
            ResolvedPool primaryPool = null)
        {
            if (effects == null || effects.Count == 0)
            {
                return null;
            }

            var lines = new List<(string, string)>();
            foreach (var group in effects.GroupBy(e => e.Id))
            {
                string fragments = string.Join(", ", group
                    .Select(e => ModifierEffectText.Describe(e.Value, primaryPool))
                    .Where(fragment => fragment.Length > 0));

                if (fragments.Length == 0) continue;
                lines.Add((group.Key, $"{group.First().DisplayName} -- {fragments}"));
            }

            return lines.Count == 0 ? null : lines;
        }

        // The AFFIXES section, compared against whatever is CURRENTLY
        // EQUIPPED in the slot the candidate would occupy -- the same
        // "VS. EQUIPPED" posture the numeric stat delta already takes
        // (ItemStatLines.DeltaLines), extended to affixes.
        //
        // A candidate line whose id is NOT among `equipped` is a GAIN and
        // gets wrapped GainHex, matching how a positive stat delta is
        // coloured. An id present on both is unchanged and stays plain --
        // the same "no colour" treatment AppendDelta gives a stat that does
        // not move (an affix line cannot simply be omitted the way a
        // zero-delta stat is without looking like the item lost the affix
        // entirely, so it prints uncoloured instead). Anything `equipped`
        // that is NOT on the candidate is a LOSS with no candidate line to
        // attach to, so it gets its own trailing "Losing: <name>" line in
        // LossHex -- mirroring "Also breaks: <slots>"/"Also unlocks:
        // <slots>" in ItemStatLines.Body, the existing precedent for a
        // consequence that is not part of the candidate's own arithmetic.
        //
        // `equipped` null/empty (nothing worn there yet, or a caller with no
        // comparison to make) degrades to EXACTLY LinePairs' own plain
        // listing -- no colour, no loss lines.
        //
        // Runs even when `candidateEffects` itself is null: an item with
        // zero affixes replacing one that had some is still a real loss.
        public static IReadOnlyList<string> ComparisonLines(IReadOnlyList<Effect> candidateEffects,
            IReadOnlyList<EquippedModifier> equipped, ResolvedPool primaryPool = null)
        {
            var pairs = LinePairs(candidateEffects, primaryPool);

            if (equipped == null || equipped.Count == 0)
            {
                return pairs?.Select(pair => pair.Line).ToList();
            }

            var equippedIds = new HashSet<string>(equipped.Select(e => e.Id));
            var candidateIds = new HashSet<string>();
            var lines = new List<string>();

            if (pairs != null)
            {
                foreach (var pair in pairs)
                {
                    candidateIds.Add(pair.Id);
                    lines.Add(equippedIds.Contains(pair.Id)
                        ? pair.Line
                        : ItemStatLines.Coloured(ItemStatLines.GainHex, pair.Line));
                }
            }

            // A duplicate id in `equipped` (should not happen -- a slot
            // carries one rolled set of modifier ids -- but this is plain
            // data with no schema enforcing that) prints "Losing" once, not
            // once per repeat.
            var lostSeen = new HashSet<string>();
            foreach (var modifier in equipped)
            {
                if (candidateIds.Contains(modifier.Id) || !lostSeen.Add(modifier.Id)) continue;
                lines.Add(ItemStatLines.Coloured(ItemStatLines.LossHex, $"Losing: {modifier.DisplayName}"));
            }

            return lines.Count == 0 ? null : lines;
        }
    }
}
