using System;

namespace PrincesPalace.Domain.Content
{
    // One armour set as authored in itemsets.json, before validation.
    //
    // A set is a GENERATOR, not a list of items. It names a material, the
    // five pieces that material comes in, and what each piece is worth at
    // its weakest and at its strongest; the resolver expands that into one
    // real item per TIER. Authoring three sets of five pieces produces
    // 165 items, and adding a fourth material is fifteen lines rather than
    // fifty-five entries.
    //
    // Tier is the only axis baked here. The player-facing PLUS rides on the
    // item instance instead, so honing a piece never generates an asset.
    //
    // Public fields and no properties, because UnityEngine.JsonUtility only
    // populates fields — same shape as every other Raw*Entry here.
    [Serializable]
    public class RawItemSetEntry
    {
        // Stable identifier. Item ids are built from it, and item ids are
        // written into save files, so renaming a set orphans everything
        // anyone was wearing.
        public string id;

        // What the material is called in item names: "Leather" gives
        // "Hardened Leather Coif".
        public string displayName;

        public string description = "";

        // One word per tier, lowest first — "Ragged" through "Sovereign".
        // Weapons have carried these since they were authored; armour did
        // not, which is why a top-tier coif used to be called "Leather Coif
        // +10" and read as a number rather than as a better object. Short
        // lists are fine: the last adjective covers every tier past the end.
        public string[] tierAdjectives = Array.Empty<string>();

        // The highest tier this set goes to. Every piece is generated at
        // tier 0 through tier this, inclusive, so 10 means eleven items per
        // piece. RAISING it later is safe — the existing tiers keep their
        // exact values, because a piece's numbers are anchored at both ends
        // rather than accumulated per step.
        public int maxTier = -1;

        // Gold for the tier-0 piece, and how much each further tier adds.
        public int cost = -1;
        public int costPerTier = -1;

        public int sortOrder = -1;

        // WHAT THIS MATERIAL IS FOR, as a share per ability score, and the
        // thing that turns a set from fifteen hand-typed numbers into one
        // authored idea.
        //
        // Lines read "<score> <hundredths>": "strength 40" is a weight of
        // 0.40. Hundredths rather than decimals so this file stays
        // integer-parsed like every other stat line in it.
        //
        // GearScaling turns them into real numbers — the weight times the
        // slot's share times the global base, then up the tier curve. Every
        // ability score a piece grants comes from here; nothing is
        // hand-authored per piece any more (balance redesign Phase 4, D4).
        //
        // The SUM is the set's budget, and the one number to watch when
        // balancing: 1.00 is point-neutral against a pure single-stat style,
        // and the dual sets deliberately run at 1.10, because breadth is worth
        // less than reaching a threshold.
        public string[] styleWeights = Array.Empty<string>();

        // WHAT THIS MATERIAL PROTECTS WITH, as a percentage share per combat
        // stat -- balance redesign Phase 4 (D4). The other half of the same
        // idea styleWeights is: instead of every piece hand-authoring its
        // own physicalDefense/maxHealth numbers (which is how the same-tier
        // budgets drifted apart in the first place), a set names ONE profile
        // and GearScaling spends it, at each slot's own share of the budget,
        // the same way styleWeights spends the ability-score half.
        //
        // Lines read "<stat> <percent>": "physicalDefense 35" is 35%. The
        // five lines (maxHealth, physicalDefense, magicalDefense, speed,
        // manaRegen) MUST sum to exactly 100 -- there is no such thing as an
        // unspent budget point, on purpose, for the same reason a style's
        // weights summing to something meaningful mattered before. attack is
        // not a legal name here: gear stopped granting flat attack in Phase
        // 3, and this profile only ever spends the 60% combat-stat share of
        // a piece's budget (the other 40% is styleWeights, unchanged).
        //
        // ONE profile per SET, shared by all five of its slot pieces -- a
        // piece's own budget differs only through its slot's weight, not
        // through a different split of stats. baseStats/topStats used to let
        // a piece override this by hand; they are GONE from the schema. A
        // piece naming either is a content-build error now, not a silent
        // override -- see ItemSetEntryResolver.
        public string[] statProfile = Array.Empty<string>();

        public RawSetPiece[] pieces = Array.Empty<RawSetPiece>();
    }

    // One piece of a set — the thing that becomes eleven items.
    [Serializable]
    public class RawSetPiece
    {
        // Stable, and only unique WITHIN its set: the generated item id is
        // "<set>_<piece>_p<tier>", so "coif" may appear in several sets.
        // The "p" is historical — it stood for plus, and now the number
        // after it is the tier. Ids are save-file contracts, so the letter
        // stays exactly where it is rather than being tidied.
        public string id;

        // The piece's own name, which is what makes a set read as a
        // material rather than a prefix: steel has a Helmet where leather
        // has a Coif.
        public string displayName;

        // Head, Necklace, Torso, Legs, Shoes, Gloves, Weapon1, Weapon2.
        public string slot;

        // REMOVED from the schema, balance redesign Phase 4 (D4) -- "derive
        // everything, hand-author nothing per-piece". Every combat stat a
        // piece grants now comes from its set's statProfile (above) and its
        // own slot's share of the budget; every ability score still comes
        // from styleWeights, unchanged. These two fields are kept here ONLY
        // so a stale JSON file with either key still deserializes into
        // something the resolver can see and reject LOUDLY, with a message
        // pointing at statProfile, instead of the key silently vanishing the
        // way an unrecognised JsonUtility field always does. Do not author
        // either one; ItemSetEntryResolver refuses any piece where they are
        // non-empty.
        public string[] baseStats = Array.Empty<string>();
        public string[] topStats = Array.Empty<string>();

        // What a character needs, from everything ELSE worn plus base scores
        // and talents, before this piece counts as worn -- two authored ends,
        // interpolated the same way every other stat here is, but pure
        // ability-score lines only (see AbilityScoreLineParser). A legendary
        // piece is high-tier, and tier interpolation is what makes it demand
        // a high score.
        public string[] requiresAtZero = Array.Empty<string>();
        public string[] requiresAtMax = Array.Empty<string>();

        // Folder of this piece's art, sliced one PNG per item level by
        // tools/slice_item_sheet.py: "<iconSheet>/level_1.png" upward. Empty
        // means the piece has no art yet, which is a supported state — the
        // icon Image is simply left off rather than drawn as a white square.
        //
        // Levels are spread across the tier range rather than mapped one to
        // one, because a sheet has ten drawings and a set has eleven tiers.
        // See ItemSetEntryResolver.IconLevelFor.
        public string iconSheet = "";

        // How many levels that sheet holds. Zero means the standard ten.
        // Authorable so a sheet of a different size cannot silently mis-map
        // every piece that uses it — the resolver has no filesystem to count
        // them with, and a wrong count here shows up as a missing-art warning
        // at scene build rather than as a quietly wrong icon.
        public int iconLevels;
    }

    // JsonUtility needs a class to deserialise the file's root object into.
    [Serializable]
    public class RawItemSetFile
    {
        public RawItemSetEntry[] sets = Array.Empty<RawItemSetEntry>();
    }
}
