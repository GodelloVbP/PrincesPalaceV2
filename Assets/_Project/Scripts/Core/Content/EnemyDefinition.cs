using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Content
{
    // An authored enemy template. Enemies reuse StatBlock (no talents, no
    // mana — they only ever basic-attack for now) rather than a parallel
    // stat shape, so combat math never needs to special-case which side of
    // the fight it's looking at.
    public class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier written into save files. Never rename this after a save exists.")]
        public string id;

        public string displayName;

        public StatBlock baseStats = new StatBlock(10, 5, 3, 1);

        [Tooltip("Experience awarded to every surviving party member on victory.")]
        public int expReward = 20;

        [Tooltip("In-run currency awarded on victory.")]
        public int currencyReward = 10;

        [Tooltip("Boss-tier enemies are only picked for the dungeon's final room, and never as a regular or Elite encounter.")]
        public bool isBoss;

        [Tooltip("A player attack of this type deals double damage to this enemy.")]
        public DamageType weakness = DamageType.Fire;

        [Tooltip("A player attack of this type deals half damage to this enemy.")]
        public DamageType resistance = DamageType.Physical;

        [Tooltip("Stagger meter. Every hit costs BreakShield.BaseDepletion, a weakness-matched hit costs BreakShield.WeaknessDepletion; hitting zero skips this enemy's next turn and drops its Defense to zero for the rest of that window. 0 means no stagger meter at all — this enemy cannot be broken.")]
        public int breakShieldPoints;

        [Tooltip("Resources-relative folder of stance sprites (idle/cast/attack/hurt/defeated). Empty means no art yet — the fight stage shows a nameplate instead.")]
        public string spritePath;

        [Tooltip("Which way this monster's art is drawn in its source file. The stage mirrors it as needed so it always faces the party — it does NOT assume every sprite faces the same way.")]
        public PrincesPalace.Domain.Stage.SpriteFacing facing = PrincesPalace.Domain.Stage.SpriteFacing.Right;

        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order. Enemy selection is randomized rather than
        // positional today, so that incidental order was never actually
        // load-bearing — but CharacterDefinition looked exactly as safe
        // right up until a new character's id happened to sort badly (see
        // ContentDatabase.Characters, commit d577703). Sorting explicitly
        // here costs nothing and closes the same trap before anything ever
        // does depend on enemy order.
        public int sortOrder;

        [Tooltip("Display name of this monster's second action. Empty means it only ever attacks.")]
        public string skillName;

        [Tooltip("Multiplies attack when the skill is used.")]
        public float skillPower = 1.5f;

        [Tooltip("Chance per turn of choosing the skill over a plain attack.")]
        public float skillChance;

        public bool HasSkill => !string.IsNullOrWhiteSpace(skillName);

        [Tooltip("Resources-relative path of a f0..fN VFX sequence played over the TARGET when this monster's skill lands. Empty means no prop.")]
        public string vfxPath;

        public float vfxSeconds = 0.6f;

        [Tooltip("Which frame of vfxPath the hit actually lands on, counting from 1.")]
        public int vfxImpactFrame = 3;

        public string sfxPath;

        [Tooltip("Status this monster's attacks apply to whoever they hit — basic attack or skill, either lands it. Not gated to skill use the way a player skill's own status is.")]
        public StatusEffectType appliesStatus;

        [Tooltip("Whether appliesStatus actually does anything. Kept separate from a nullable-enum workaround, same reasoning as SkillDefinition.hasStatus.")]
        public bool hasStatus;

        public int statusMagnitude;
        public int statusDuration;

        [Tooltip("Never placed in the front stage slot (index 0) if the room's other picks give BuildEncounter an alternative.")]
        public bool avoidsFrontSlot;

        [Tooltip("The plain Attack holds position instead of lunging, same as a Skill cast. For a monster whose \"attack\" stance art is aliased from its \"cast\" stance (see slice_actor_sheet.py) -- the pixels are a stationary pose, so lunging toward the target reads as flying rather than striking.")]
        public bool attackHoldsPosition;
    }
}
