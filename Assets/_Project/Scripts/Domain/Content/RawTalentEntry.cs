using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One talent, exactly as typed into talents.json.
    //
    // No -1 sentinels here, unlike the other Raw* types: every numeric field
    // is a BONUS where 0 and "omitted" mean precisely the same thing, so
    // there is nothing to distinguish. There is no `cost` — a talent's price
    // in embers comes from WHERE it sits in the skeleton
    // (ContentDatabase.OrbCost), never from content; see ResolvedTalent's
    // constructor for what the authored field used to get wrong.
    [Serializable]
    public class RawTalentEntry
    {
        [ContentDoc("Stable identifier; written into save files as an unlocked-talent id.")]
        public string id;
        [ContentDoc("The name shown on this talent's node.")]
        public string displayName;
        [ContentDoc("Flavor/rules text shown to the player.")]
        public string description = "";

        // CharacterDefinition.id of the only character who can take this, or
        // empty for a node every character shares. Empty is the old
        // behaviour — before this field existed, every talent was available
        // to everyone.
        [ContentDoc("The one character who can take this talent, or empty for a node every character shares.")]
        public string characterId = "";

        // column = which of the 3 paths (0-2). row = slot index (0-20) into
        // the FIXED per-path skeleton (Talent Tree v2, 2026-08-02): a single
        // root, a 3x3 grid climbing to a first convergence, a 3-way branch
        // climbing to the capstone. The skeleton shape itself lives in
        // SceneBuilder.Talents.cs/TalentController.cs as shared geometry,
        // not authored per talent -- slot order is fixed, so "row" is still
        // a strictly-increasing depth-ish index even though several talents
        // now share the same visual TIER (three at once for the grid/branch
        // rows). Two talents available to the same character may not share
        // a (column,row) slot; talents belonging to DIFFERENT characters
        // may, since only one of them is ever on screen at a time.
        [ContentDoc("Which of the 3 paths (0-2) this talent sits in.")]
        public int column;
        [ContentDoc("The slot index (0-20) into that path's fixed skeleton.")]
        public int row;

        // Ids of talents that unlock this one. A chain node names exactly
        // one; a convergence node (the two per path where the 3x3 grid
        // gathers into one trunk, and where the 3-way branch gathers into
        // the capstone) names all 3 of its parents -- ANY ONE of them
        // invested is enough (OR, not AND). There is no AND-of-multiple
        // case in this tree's shape, so a multi-entry array always means
        // convergence. Must belong to the same character (or be shared) and
        // sit on a strictly lower row/slot, which makes cycles impossible
        // by construction rather than by a check.
        [ContentDoc("Ids of talents that unlock this one; a chain node names one, a convergence node names all of its parents (any one invested is enough).")]
        public string[] prerequisites = Array.Empty<string>();

        // Point-gate (new, Talent Tree v2): in addition to the prerequisite
        // check above, requires at least this many points ALREADY SPENT
        // in this talent's own path (same characterId + column), regardless
        // of which specific route got the player there. 0 (the default)
        // means no gate. Only the two convergence nodes per path use this
        // in the delivered design (8 at the first convergence, 20 at the
        // capstone), but it is a normal per-node field, not special-cased
        // to convergence talents specifically.
        [ContentDoc("Requires at least this many points already spent in this talent's own path before it can be taken; 0 means no gate.")]
        public int minSpent;

        [ContentDoc("Combat stats granted while this talent is unlocked.")]
        public StatBlock statBonus;
        [ContentDoc("Ability scores granted while this talent is unlocked.")]
        public AbilityScoreBlock abilityScoreBonus;

        [ContentDoc("Maximum mana granted while this talent is unlocked.")]
        public int maxManaBonus;
        [ContentDoc("Percent reduction to skill mana costs granted while this talent is unlocked.")]
        public int skillManaCostReduction;

        // Signature-resource upgrades: a deeper fleece, and a faster one.
        [ContentDoc("Signature-resource capacity granted while this talent is unlocked.")]
        public int signatureCapacityBonus;
        [ContentDoc("Signature resource gained per turn, granted while this talent is unlocked.")]
        public int signaturePerTurnBonus;

        [ContentDoc("An items.json id granted once into the owner's stash when this talent is taken.")]
        public string grantsStartingItemId = "";

        // Rules this talent grants that are not a number added to a stat —
        // the whole payload vocabulary of the reworked Shawn tree. `type` is
        // a Domain.Combat.TalentEffectType member name, matched
        // case-insensitively; see that enum for what each one means and which
        // of magnitude/threshold it reads.
        //
        // A LIST rather than one entry, because several nodes genuinely grant
        // two rules at once: the Black Ram's root is "+1 wool per hit taken"
        // AND two health-gated per-turn tiers, which is one idea expressed as
        // three entries rather than three nodes.
        [ContentDoc("Non-numeric rules this talent grants; see RawTalentEffect.")]
        public RawTalentEffect[] effects = Array.Empty<RawTalentEffect>();

        // A skills.json id this talent puts on the owner's combat strip —
        // Provoke, Headbutt, Black Ram Mode. Empty for most talents.
        //
        // Reuses the SKILL system rather than inventing a parallel
        // talent-ability one. A talent-granted ability is a thing on the
        // command strip with a cost, a target and an effect, which is exactly
        // what a SkillDefinition already is; the only thing missing was a
        // route to unlock one other than levelling. Authored with an unlock
        // level no character can reach (999), the same marker skills.json
        // already uses for the two event-taught spells.
        [ContentDoc("A skills.json id this talent adds to the owner's combat strip.")]
        public string grantsSkillId = "";

        // The skills.json id a skill-scoped rule on this node upgrades -- the
        // Einherjar's Slam nodes name their Slam here. Names the skill
        // instead of letting combat infer it from a skill's shape (a
        // single-target poolTiers strike), so a second skill of that shape
        // cannot inherit the riders by accident. Required on a node that
        // carries a skill-scoped effect, refused on any other.
        [ContentDoc("A skills.json id (owned by this talent's character) that this node's skill-scoped effects (the Slam* rules) upgrade; required with such an effect, refused without one.")]
        public string appliesToSkillId = "";

        // Editor-time path to this talent's archetype glyph, loaded by
        // SceneBuilder — see RelicDefinition.iconPath's own comment. Empty
        // (the default) is a supported state, not a hole: 315 unique icons
        // is not a sane art budget, so most talents share one of a small
        // set of archetype icons by grant shape (HP, attack, mana, ...)
        // rather than each carrying its own.
        [ContentDoc("Editor-time path to this talent's archetype glyph; empty is a supported state, most talents share a small set of archetype icons.")]
        public string iconPath = "";
    }

    // One rule from a talent's `effects` list, before the enum name has been
    // parsed. Separate from Domain.Combat.TalentEffect for exactly one
    // reason: JsonUtility deserialises an enum field from an integer and
    // nothing else, so the authored file has to carry a string and something
    // has to turn it into the enum. That something is TalentEntryResolver,
    // where every other content-shaped judgment in this project already
    // lives. Authoring the integer instead would be unreadable and, worse,
    // would silently re-point at a different rule the moment anyone inserted
    // an enum member.
    [Serializable]
    public class RawTalentEffect
    {
        [ContentDoc("Which Domain.Combat.TalentEffectType this rule grants, matched case-insensitively.")]
        public string type = "";
        [ContentDoc("The rule's magnitude; meaning depends on type.")]
        public int magnitude;
        [ContentDoc("The rule's threshold; meaning depends on type.")]
        public int threshold;
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawTalentFile
    {
        [ContentDoc("This file's talents; see RawTalentEntry.")]
        public RawTalentEntry[] talents = Array.Empty<RawTalentEntry>();
    }
}
