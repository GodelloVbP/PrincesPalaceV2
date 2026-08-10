using System;

namespace PrincesPalace.Domain.Content
{
    // One weapon family as authored in weapons.json, before validation.
    //
    // A family is a GENERATOR over two axes, and the second one is what this
    // file exists for. Armour sets vary along tier alone; a weapon varies
    // along tier AND modifier, so one authored "Sword" turns into a Sturdy
    // one and a Nimble one at every tier — the same blade, differently
    // balanced, wanting a different character to hold it.
    //
    // That is the whole point of the modifier axis: a run should be able to
    // hand you a sword that is wrong for you, so that the one that is right
    // for you feels like a find rather than an entitlement.
    //
    // Public fields and no properties, because UnityEngine.JsonUtility only
    // populates fields — same shape as every other Raw*Entry here.
    [Serializable]
    public class RawWeaponEntry
    {
        // Stable identifier. Item ids are built from it and item ids are
        // written into save files, so renaming a family orphans whatever
        // anyone was holding.
        public string id;

        // The noun in every generated name: "Sword" gives "Nimble Keen
        // Sword".
        public string displayName;

        public string description = "";

        // The highest tier this family goes to. Every modifier is generated
        // at tier 0 through tier this, inclusive.
        public int maxTier = -1;

        public int cost = -1;
        public int costPerTier = -1;
        public int sortOrder = -1;

        // Which hand. "Weapon" (the alias for Weapon1) unless a family is
        // deliberately off-hand only.
        public string slot = "Weapon";

        // Attack at tier 0 and at maxTier, interpolated between. This is the
        // FLAT half of a weapon's power; the scaling grades are the other
        // half, and the two are deliberately separate so a low-tier weapon
        // with the right grades can still be the right pick.
        public int attackAtZero = -1;
        public int attackAtMax = -1;

        // Folder of per-level art, as sliced by tools/slice_item_sheet.py.
        // Shared by every modifier of the family — a Sturdy sword and a
        // Nimble one are the same steel.
        public string iconSheet = "";
        public int iconLevels;

        // A single flat image for the whole family, used only when there is
        // no iconSheet. Not a lesser version of the same thing: a family whose
        // art has been drawn per level should use iconSheet and get a
        // different picture as it improves, and this is for a family that has
        // one drawing and no plans for eleven.
        public string iconPath = "";

        // One word per tier, lowest first. "Worn Sword" through "Sovereign
        // Sword": the adjective is what makes a tier-7 blade read as a better
        // object rather than as a bigger number, and it is authored rather
        // than derived so the vocabulary belongs to the game's voice.
        //
        // Short lists are fine — the last adjective covers every tier past
        // the end of the list rather than failing.
        public string[] tierAdjectives = Array.Empty<string>();

        public RawWeaponModifier[] modifiers = Array.Empty<RawWeaponModifier>();
    }

    // One way a family can be balanced: which stat it favours, which it
    // merely tolerates, and what it is called.
    [Serializable]
    public class RawWeaponModifier
    {
        // Unique within its family only — the generated id is
        // "<family>_<modifier>_p<tier>". The "p" is historical (it stood for
        // plus); ids are save-file contracts, so it stays.
        public string id;

        // The adjective that leads the name: "Sturdy", "Nimble".
        public string displayName;

        public string description = "";

        // The stat this modifier is built around, and the grade it reaches
        // at tier 0 and at maxTier. ANCHORED AT BOTH ENDS and interpolated
        // between, exactly like a set piece's stats — so "C at the bottom, S
        // at the top" is two words rather than eleven letters that drift, and
        // raising maxTier stretches the curve instead of running out of
        // ladder partway up.
        public string primary;
        public string primaryAtZero = "";
        public string primaryAtMax = "";

        // The stat it still works with, on the same two-ended curve. This is
        // what stops a mismatched weapon from being literally unusable: a
        // Nimble sword in a Strength build's hands is worse, not dead.
        public string secondary = "";
        public string secondaryAtZero = "";
        public string secondaryAtMax = "";

        // Anything else it rides, as flat "<score> <grade>" lines that do not
        // move with plus. For a weapon whose character is a third stat
        // entirely rather than a primary/secondary split.
        public string[] alsoScalesWith = Array.Empty<string>();

        // What this weapon contributes to SPELL power (the Skill action),
        // as the same flat "<score> <grade>" lines as alsoScalesWith --
        // deliberately NOT a tiered primary/secondary curve like the plain
        // Attack axis above: a staff's whole point is "does not improve your
        // swing, but is genuinely good at casting," a single flat grade
        // says that in one line, and nothing here needs to move with tier
        // independently of the weapon's own attackAtZero/attackAtMax curve.
        // See ItemDefinition.spellScaling.
        public string[] spellScalesWith = Array.Empty<string>();

        // What a character needs, from everything ELSE worn plus base scores
        // and talents, before this weapon counts as worn -- interpolated the
        // same two-ended way primary/secondary are, so a legendary (high
        // tier) blade demands a high score. See AbilityScoreLineParser.
        public string[] requiresAtZero = Array.Empty<string>();
        public string[] requiresAtMax = Array.Empty<string>();
    }

    [Serializable]
    public class RawWeaponFile
    {
        public RawWeaponEntry[] families = Array.Empty<RawWeaponEntry>();
    }
}
