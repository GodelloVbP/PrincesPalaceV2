using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Content
{
    // The immutable template for a playable character. The mutable, per-save
    // half lives in Character (talent points spent, talents unlocked); this
    // is the part that is authored once and never changes at runtime.
    public class CharacterDefinition : ScriptableObject, IOrderedContent
    {
        [Tooltip("Stable identifier written into save files. Never rename this after a save exists.")]
        public string id;

        public string displayName;
        public CharacterRole role;

        [Tooltip("Stats before any talents are applied.")]
        public StatBlock baseStats = new StatBlock(20, 10, 5);

        [Tooltip("Classic D&D-style ability scores before any talents are applied.")]
        public AbilityScoreBlock baseAbilityScores = new AbilityScoreBlock(10, 10, 10, 10, 10, 10);

        [Tooltip("Editor-relative asset path to a neutral portrait PNG, for the Character Sheet. Empty until art exists for this character.")]
        public string portraitPath;

        [Tooltip("Resources-relative folder of battle stance sprites (idle/attack/cast/hurt/defeated/victory). Distinct from portraitPath: that is a head-and-shoulders portrait for menus, this is the full-body figure that stands on the fight stage. Empty means no battle art yet — the stage falls back to a plain plate.")]
        public string battleSpritePath;

        [Tooltip("Which way this character's battle art is drawn in its source file. The stage mirrors it as needed so they always face the enemy — it does NOT assume every sprite faces the same way.")]
        public PrincesPalace.Domain.Stage.SpriteFacing battleSpriteFacing = PrincesPalace.Domain.Stage.SpriteFacing.Right;

        [Tooltip("The elemental type of this character's Attack and Skill, checked against an enemy's weakness/resistance.")]
        public DamageType attackType = DamageType.Physical;

        [Tooltip("Id of this character's own private combat resource (e.g. \"wool\"). Empty means they have none, which is the default and is true of everyone but Shawn.")]
        public string signatureResourceId;

        [Tooltip("What the resource is called on screen (e.g. \"Wool\").")]
        public string signatureResourceDisplayName;

        [Tooltip("Cap. The resource starts every fight at zero and builds toward this.")]
        public int signatureResourceCapacity;

        [Tooltip("Gained at the start of each of this character's turns, before Charisma's contribution.")]
        public int signatureGainPerTurn;

        [Tooltip("Gained on top of the per-turn amount when this character uses a basic Attack — the reason their weakest action is still worth taking.")]
        public int signatureGainOnAttack;

        [Tooltip("Gained each time this character is hit. A fleece thickens in a hard winter.")]
        public int signatureGainOnDamageTaken;

        [Tooltip("Whether this resource soaks incoming damage before health. False for Wool since the talent rework — see RawCharacterEntry.signatureAbsorbsDamage.")]
        public bool signatureAbsorbsDamage;

        // Empty id means no resource at all, rather than a zero-capacity
        // one — see CombatantState.Signature for why that distinction is
        // kept sharp.
        public bool HasSignatureResource => !string.IsNullOrWhiteSpace(signatureResourceId);

        // Resources.LoadAll returns assets in filename order, not authoring
        // order — that accidentally matched the intended roster order until
        // a character whose id sorted early (alphabetically) was added
        // after one whose id sorts late, silently reshuffling which
        // characters land in the default squad. ContentDatabase sorts by
        // this explicit field instead, same pattern as
        // UpgradeDefinition.sortOrder.
        [Tooltip("Prince's Favor: this character's luck. The squad's HIGHEST value drives loot rolls - it never compounds across members.")]
        public int princesFavor;

        public int sortOrder;

        // Listed by the authored order ContentBuilder stamped on it.
        public int SortOrder => sortOrder;
    }
}
