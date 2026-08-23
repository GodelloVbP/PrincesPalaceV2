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

        // The shallowest floor this enemy may be drawn on.
        //
        // A MINIMUM, not a band: floor 5 still rolls rats, and the difficulty
        // curve is what keeps them dangerous there -- 7.7% health a step means
        // a step-80 rat holds about 33,000. What this stops is the opposite,
        // floor 1 drawing a golem, which it could, because the pool was every
        // non-boss enemy at every depth.
        //
        // Worth measuring rather than arguing about. Against the starting party
        // with no gear, one floor-1 room ranged from ONE round (rat, imp,
        // spider, bat, moth, wisp) to TWELVE (golem) -- and a normal room
        // fields one or two, so a double golem was twenty-four rounds at the
        // same depth as a one-round rat. Bosses spread wider still: four rounds
        // for the hollow choir against thirty-two for the throne colossus.
        //
        // The cause underneath is that Defense is SUBTRACTIVE against Attack
        // values of 4-9, so once an enemy's Defense reaches the party's Attack
        // every swing floors at max(1, ...) and the fight stops being about
        // numbers. The golem's Defense 8 puts two of the three starting
        // characters on that floor. Banding the pool is the first half of the
        // answer; the subtractive floor itself is the second, and is not
        // addressed here.
        //
        // 0 or absent means floor 1, so an unbanded enemy is available from the
        // start rather than never -- content should degrade into the game.
        public int minFloor;
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
        // THE LEGACY SINGLE ACTION. A name for the telegraph, a multiplier on
        // this monster's own basic attack, and how often it fires.
        //
        // Kept working, and kept because migrating it is a CONTENT decision
        // rather than a plumbing one: "attack x 1.6" has no equivalent skill id
        // until somebody writes the skill it should have been. A monster
        // authored this way still behaves exactly as it did.
        //
        // Prefer `abilities` for anything new -- see below.
        public string skillName = "";
        public float skillPower = -1f;
        public float skillChance = -1f;

        // WHAT THIS MONSTER CAN DO, as real skills with relative weights.
        //
        // Each entry names a skill id from skills.json, so a monster reaches
        // the whole SkillEffect vocabulary -- damage one, damage all, heal
        // itself, mend its own side, apply a status that belongs to the ability
        // rather than to the monster -- and every skill written for a character
        // in future is available to monsters for free.
        //
        // Weights are RELATIVE and need not sum to anything: 2 against 1 is
        // twice as likely, and adding a third entry does not require rebalancing
        // the first two.
        //
        // Non-empty, this REPLACES the trio above rather than adding to it. Two
        // ways of saying what a monster does, both live at once, is the drift
        // this project keeps writing rules against.
        public RawEnemyAbility[] abilities = Array.Empty<RawEnemyAbility>();

        // HOW OFTEN IT JUST SWINGS, on the same relative scale as the ability
        // weights above. The basic attack is always in the pool -- a monster
        // whose every turn is a special reads as scripted rather than as
        // dangerous -- and this is the dial for it. 0 takes it out entirely.
        public float attackWeight = 1f;

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
        // lands, nested under "vfx" -- the SAME value a skill carries, so a
        // monster's skill shows a prop near the player it hit exactly the way a
        // player skill shows one near the enemy it hit. See SpellPresentation.
        public SpellPresentation vfx = new SpellPresentation();

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

    // One line of a monster's ability list.
    [Serializable]
    public class RawEnemyAbility
    {
        // A skill id from skills.json.
        //
        // A monster's skill is authored in the same file a character's is,
        // because the RULES are identical -- not because the audiences are. It
        // still needs an owner: characterId names the ENEMY that owns it, which
        // keeps it out of every player's button strip (AvailableSkillsFor
        // matches on a character's definitionId) while leaving a typo'd owner
        // just as catchable as it is for a character skill.
        public string skillId = "";

        // Relative likelihood. Zero means "authored but never chosen", which is
        // a legitimate thing to want while tuning and is refused at build time
        // only if EVERY entry is zero.
        public float weight = 1f;
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // enemies.json is one object with an "enemies" array inside it.
    [Serializable]
    public class RawEnemyFile
    {
        public RawEnemyEntry[] enemies = Array.Empty<RawEnemyEntry>();
    }
}
