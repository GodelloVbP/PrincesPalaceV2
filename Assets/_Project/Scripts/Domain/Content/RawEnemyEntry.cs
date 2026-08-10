using System;

namespace PrincesPalace.Domain.Content
{
    // One monster, exactly as a human typed it into enemies.json. Every
    // numeric field defaults to -1 and weakness/resistance default to ""
    // specifically so EnemyEntryResolver can tell "the author left this
    // blank" apart from "the author wrote 0" — Unity's JsonUtility only
    // overwrites fields that are actually present in the source JSON, so a
    // field's C# initializer survives untouched when the author omits it.
    // -1 is safe as a sentinel because no real stat is ever negative.
    [Serializable]
    public class RawEnemyEntry
    {
        public string id;
        public string displayName;
        public int maxHealth = -1;
        public int attack = -1;
        public int defense = -1;
        public int speed = -1;
        public int expReward = -1;
        public int currencyReward = -1;
        public bool isBoss;
        public string weakness = "";
        public string resistance = "";

        // Stagger meter capacity. -1 means "derive it from maxHealth", same
        // sentinel convention as every other omittable stat here — see
        // EnemyEntryResolver.BreakShieldPerHealth. 0 is a real, authorable
        // value distinct from "omitted": it means this enemy has no stagger
        // meter at all and cannot be broken.
        public int breakShieldPoints = -1;

        // Optional. Resources-relative folder of stance sprites; omitted
        // for monsters that have no art yet.
        public string spritePath = "";

        // Optional second action. A monster with a skillName can choose it
        // instead of a plain attack; one without only ever attacks, which is
        // every monster that predates this. skillPower multiplies its
        // attack, skillChance is the odds of picking it on any given turn.
        // -1 sentinels so "omitted" is distinguishable from "written as 0".
        public string skillName = "";
        public float skillPower = -1f;
        public float skillChance = -1f;

        // Benched, not deleted. An inactive monster keeps its full entry
        // here — stats, weakness, rewards, the lot — but no asset is built
        // for it, so it cannot spawn. Used to narrow the pool to the
        // monsters that actually have art while the stage is being tuned;
        // flipping it back is a one-word edit, with nothing to re-type.
        // Defaults to true so an entry that never mentions it still spawns.
        public bool active = true;

        // Which way this monster's art is drawn in its source file, so the
        // stage knows whether to mirror it. Defaults to "right" because
        // both sheets authored so far (Bog Witch, Stone Golem) face right —
        // stated explicitly rather than left implicit, since a wrong
        // default here shows up as a monster fighting with its back turned.
        public string facing = "right";

        // Optional VFX played over the TARGET when this monster's skill
        // lands — same four fields as SkillDefinition's own (see
        // RawSkillEntry), same -1/"" sentinels, so a monster's skill can
        // show a prop near the player it hit exactly the way a player
        // skill already shows one near the enemy it hit.
        public string vfxPath = "";
        public float vfxSeconds = -1f;
        public int vfxImpactFrame = -1;
        public string sfxPath = "";

        // Optional status this monster's ATTACK (basic attack or skill,
        // either lands it) applies to whoever it hits — same shape as
        // SkillDefinition's appliesStatus/statusMagnitude/statusDuration,
        // deliberately not skill-gated: a monster whose whole gimmick is
        // "its claws bleed" shouldn't need an authored skill just to carry
        // that. Required together, same "mixing field sets is rejected"
        // rule as the skill version.
        public string appliesStatus = "";
        public int statusMagnitude = -1;
        public int statusDuration = -1;

        // Encounter-placement gimmick: this monster is never put in the
        // front stage slot (index 0) if the room's other picks give
        // BuildEncounter an alternative — a fragile monster that wants to
        // hide behind something sturdier. Silently ineligible to honour
        // (e.g. every pick this room avoids the front) rather than an
        // error: it's a preference, not a hard rule content authoring can
        // violate.
        public bool avoidsFrontSlot;

        // For a monster whose plain-Attack ART is a stationary pose rather
        // than a forward strike — most commonly one whose "attack" stance is
        // an alias of its "cast" stance (see slice_actor_sheet.py's ACTORS
        // manifest, e.g. golem's "attack": "cast"), so the pixels are
        // literally its ground-slam skill animation reused for the plain
        // roll. Without this, FightController.Turns.cs still LUNGES the
        // actor toward its target on a plain attack (only a Skill, via its
        // StanceCast stance, holds position) — the stage figure crosses and
        // often climbs toward the target while its art shows a rooted slam,
        // which reads as the monster flying rather than striking. True marks
        // the plain attack as holding position exactly the way a cast does.
        public bool attackHoldsPosition;
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // enemies.json is one object with an "enemies" array inside it.
    [Serializable]
    public class RawEnemyFile
    {
        public RawEnemyEntry[] enemies = Array.Empty<RawEnemyEntry>();
    }
}
