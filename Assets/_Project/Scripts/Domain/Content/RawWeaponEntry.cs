using System;

namespace PrincesPalace.Domain.Content
{
    // One weapon family as authored in weapons.json, before validation.
    //
    // A family is a GENERATOR over tier alone, exactly like an armour set —
    // item-modifier plan Phase B collapsed the second (modifier) axis this
    // file used to carry. One authored "Sword" turns into eleven real
    // weapons, tier 0 through maxTier, each the same steel at a different
    // level of make. What used to differentiate one sword from another (a
    // Sturdy one wanting Strength, a Nimble one wanting Dexterity) is now a
    // ROLLED RIFT MODIFIER on the item instance (modifiers.json), not a
    // second baked family variant — see the item-modifier plan's "model at a
    // glance". A family still carries a single default scaling identity
    // (Sword->STR, Staff->INT, Dagger->DEX) so a weapon with zero rolled
    // modifiers is still a coherent pick, not an inert stick.
    //
    // Public fields and no properties, because UnityEngine.JsonUtility only
    // populates fields — same shape as every other Raw*Entry here.
    [Serializable]
    public class RawWeaponEntry
    {
        // Stable identifier. Item ids are built from it and item ids are
        // written into save files, so renaming a family orphans whatever
        // anyone was holding.
        [ContentDoc("Stable identifier; generated item ids are built from it and item ids live in save files.")]
        public string id;

        // The noun in every generated name: "Sword" gives "Keen Sword".
        [ContentDoc("The noun in every generated name for this family, e.g. 'Sword' gives 'Keen Sword'.")]
        public string displayName;

        [ContentDoc("Flavor text shown to the player.")]
        public string description = "";

        // The highest tier this family goes to. Every tier from 0 through
        // this, inclusive, generates one weapon.
        [ContentDoc("The highest tier this family goes to; every tier 0..maxTier generates one weapon.")]
        public int maxTier = -1;

        [ContentDoc("Gold cost of the tier-0 weapon.")]
        public int cost = -1;
        [ContentDoc("Gold added per tier above 0.")]
        public int costPerTier = -1;
        [ContentDoc("Sort position among weapon families.")]
        public int sortOrder = -1;

        // Which hand. "Weapon" (the alias for Weapon1) unless a family is
        // deliberately off-hand only.
        [ContentDoc("Which hand slot this family is worn in; 'Weapon' is the alias for Weapon1.")]
        public string slot = "Weapon";

        // Attack at tier 0 and at maxTier, interpolated between. This is the
        // FLAT half of a weapon's power; the scaling grade is the other
        // half, and the two are deliberately separate so a low-tier weapon
        // with the right grade can still be the right pick.
        [ContentDoc("Attack at tier 0, interpolated up to attackAtMax.")]
        public int attackAtZero = -1;
        [ContentDoc("Attack at maxTier, interpolated down to attackAtZero.")]
        public int attackAtMax = -1;

        // Folder of per-level art, as sliced by tools/slice_item_sheet.py.
        [ContentDoc("Folder of per-level art sliced by tools/slice_item_sheet.py; used when the family has per-tier art.")]
        public string iconSheet = "";
        [ContentDoc("How many levels the icon sheet holds.")]
        public int iconLevels;

        // A single flat image for the whole family, used only when there is
        // no iconSheet. Not a lesser version of the same thing: a family whose
        // art has been drawn per level should use iconSheet and get a
        // different picture as it improves, and this is for a family that has
        // one drawing and no plans for eleven.
        [ContentDoc("A single flat image for the whole family, used only when there is no iconSheet.")]
        public string iconPath = "";

        // One word per tier, lowest first. "Worn Sword" through "Sovereign
        // Sword": the adjective is what makes a tier-7 blade read as a better
        // object rather than as a bigger number, and it is authored rather
        // than derived so the vocabulary belongs to the game's voice.
        //
        // Short lists are fine — the last adjective covers every tier past
        // the end of the list rather than failing.
        [ContentDoc("One adjective per tier, lowest first, e.g. 'Worn Sword' through 'Sovereign Sword'; the last covers every tier past the list's end.")]
        public string[] tierAdjectives = Array.Empty<string>();

        // The family's default scaling identity — the ONE ability score a
        // base weapon of this family rides (Sword->STR, Staff->INT,
        // Dagger->DEX, per the item-modifier plan's Q2), and the grade it
        // reaches at tier 0 and at maxTier. ANCHORED AT BOTH ENDS and
        // interpolated between, exactly like a set piece's stats — so "C at
        // the bottom, S at the top" is two words rather than eleven letters
        // that drift, and raising maxTier stretches the curve instead of
        // running out of ladder partway up.
        [ContentDoc("The one ability score this family's Attack scales on by default, e.g. Sword->STR.")]
        public string primary;
        [ContentDoc("The scaling grade this family reaches at tier 0.")]
        public string primaryAtZero = "";
        [ContentDoc("The scaling grade this family reaches at maxTier; may not be lower than primaryAtZero.")]
        public string primaryAtMax = "";

        // An optional second stat, on the same two-ended curve, for a family
        // that wants to ride more than one score. Unauthored by every family
        // as of Phase B — the collapse deliberately leaves each base weapon
        // with a single "sole scaling score" identity — but the resolver
        // still supports it for whatever wants it later.
        [ContentDoc("An optional second ability score this family also scales on.")]
        public string secondary = "";
        [ContentDoc("The secondary scaling grade at tier 0.")]
        public string secondaryAtZero = "";
        [ContentDoc("The secondary scaling grade at maxTier.")]
        public string secondaryAtMax = "";

        // Anything else it rides, as flat "<score> <grade>" lines that do not
        // move with tier. For a weapon whose character is a third stat
        // entirely rather than a primary/secondary split.
        [ContentDoc("Flat '<score> <grade>' lines this family also rides, unmoved by tier.")]
        public string[] alsoScalesWith = Array.Empty<string>();

        // What this weapon contributes to SPELL power (the Skill action), as
        // the same flat "<score> <grade>" lines as alsoScalesWith --
        // deliberately NOT a tiered curve like the plain Attack axis above: a
        // staff's whole point is "does not improve your swing, but is
        // genuinely good at casting," a single flat grade says that in one
        // line, and nothing here needs to move with tier independently of
        // the weapon's own attackAtZero/attackAtMax curve. See
        // ItemDefinition.spellScaling.
        [ContentDoc("Flat '<score> <grade>' lines for what this weapon contributes to Skill/spell power, unmoved by tier.")]
        public string[] spellScalesWith = Array.Empty<string>();

        // What a character needs, from everything ELSE worn plus base scores
        // and talents, before this weapon counts as worn -- interpolated the
        // same two-ended way primary/secondary are, so a legendary (high
        // tier) blade demands a high score. See AbilityScoreLineParser.
        [ContentDoc("'<ability score> <amount>' lines gating whether the tier-0 weapon counts as worn.")]
        public string[] requiresAtZero = Array.Empty<string>();
        [ContentDoc("'<ability score> <amount>' lines gating whether the maxTier weapon counts as worn, interpolated between with requiresAtZero.")]
        public string[] requiresAtMax = Array.Empty<string>();
    }

    [Serializable]
    public class RawWeaponFile
    {
        public RawWeaponEntry[] families = Array.Empty<RawWeaponEntry>();
    }
}
