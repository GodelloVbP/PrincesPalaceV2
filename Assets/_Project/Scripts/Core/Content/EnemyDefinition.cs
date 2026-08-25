using System;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;
using UnityEngine;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Content
{
    // An authored enemy template. Enemies reuse StatBlock (no talents, no
    // mana — they only ever basic-attack for now) rather than a parallel
    // stat shape, so combat math never needs to special-case which side of
    // the fight it's looking at.
    public class EnemyDefinition : ScriptableObject, IOrderedContent
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

        // See RawEnemyEntry.minFloor.
        public int minFloor = 1;

        [Tooltip("Player attacks of these types deal half again to this enemy. May name several, or none.")]
        public DamageType[] weaknesses = { DamageType.Fire };

        [Tooltip("Player attacks of these types deal half to this enemy. May name several, or none. An element must never appear in both lists — ContentDatabase.Validation rejects that, because CombatMath scores a weakness first and the resistance would silently never apply.")]
        public DamageType[] resistances = { DamageType.Physical };

        // The pair as one value, which is the shape every damage path wants —
        // see ElementalAffinity. Built on demand rather than stored, because
        // the arrays above are what Unity serialises and a cached copy beside
        // them would be a second source of truth that a hand-edit could
        // desynchronise. Called once per enemy when a fight is built.
        public ElementalAffinity Affinity => ElementalAffinity.Of(weaknesses, resistances);

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
        // One serialized value, like a skill's. See SpellPresentation.
        public SpellPresentation vfx = new SpellPresentation();

        // WHAT THIS MONSTER CAN DO, as skill ids and relative weights.
        //
        // RawEnemyAbility rather than a third type: it is [Serializable] with
        // public fields, which is what both JsonUtility and Unity's own
        // serialiser need, and inventing a parallel shape for the same two
        // values is the duplication SpellPresentation exists to argue against.
        public RawEnemyAbility[] abilities = Array.Empty<RawEnemyAbility>();

        // The basic attack's own weight in that pool. See RawEnemyEntry.
        public float attackWeight = 1f;

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

        [Tooltip("How the plain Attack travels when it is not holding position: lunge (default lean), close (arrive then strike), or charge (a committed rush that connects on the impact frame). A string rather than the enum for the same reason the timing loop is one -- a generated asset must not repoint if the enum is reordered.")]
        public string attackApproach = "";

        // See RawEnemyEntry.stageScale -- how big this monster stands, on top
        // of the depth scale its slot already carries.
        public float stageScale = 1f;

        // See RawEnemyEntry.slotSpan -- how many of the stage's positions it
        // takes up, which is what stops a room fielding more creature than it
        // has floor for.
        public int slotSpan = 1;

        // Listed by the authored order ContentBuilder stamped on it.
        public int SortOrder => sortOrder;
    }
}
