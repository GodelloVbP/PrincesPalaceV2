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
        [ContentDoc("Stable identifier; used to derive an unauthored weakness/resistance pair and matched against monster references elsewhere in content.")]
        public string id;
        [ContentDoc("The name shown for this monster in the fight UI.")]
        public string displayName;
        [ContentDoc("The monster's maximum health.")]
        public int maxHealth = -1;
        [ContentDoc("The monster's base attack stat.")]
        public int attack = -1;

        // The old single `defense` field is GONE, not renamed to either of
        // these — see StatType's own header. Both optional; -1 means
        // "derive it" (EnemyEntryResolver.DefensePerHealth and its half),
        // same sentinel convention as every other omittable stat here.
        [ContentDoc("Flat damage reduction against Physical attacks; -1 derives it from maxHealth.")]
        public int physicalDefense = -1;
        [ContentDoc("Flat damage reduction against non-Physical attacks; -1 derives it from maxHealth.")]
        public int magicalDefense = -1;

        [ContentDoc("The monster's speed, which drives how often it acts in the charge-based turn order.")]
        public int speed = -1;
        [ContentDoc("Experience granted to the party on defeating this monster.")]
        public int expReward = -1;
        [ContentDoc("Currency granted to the party on defeating this monster.")]
        public int currencyReward = -1;
        [ContentDoc("Whether this monster only ever appears in the dungeon's final room.")]
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
        [ContentDoc("The shallowest floor this enemy may be drawn on; 0 or absent means floor 1.")]
        public int minFloor;

        // The damage type this monster's own attacks and abilities carry.
        // Optional; blank means Physical, same default an unauthored
        // character's attackType gets (CharacterEntryResolver). Enemies
        // have no CombatantKit the way a player's PlayerKit carries an
        // AttackType, so without this MagicalDefense was a dead stat
        // against every monster in the game -- nothing a monster did was
        // ever typed. Case-insensitive, parsed the same way
        // CharacterEntryResolver parses a character's own attackType.
        [ContentDoc("The DamageType this monster's attacks and abilities carry; blank means Physical.")]
        public string attackType = "";

        // WHAT THIS MONSTER TAKES BADLY AND WHAT IT SHRUGS OFF.
        //
        // A COMMA-SEPARATED LIST, not a single element: "Fire" and
        // "Fire, Ice" are both valid, and a creature that should burn and
        // freeze no longer has to pick one. Order does not matter and
        // whitespace around each name is trimmed; names are matched
        // case-insensitively against DamageType, and one bad name in the list
        // fails the build rather than being skipped (a typo'd element would
        // otherwise just quietly stop applying).
        //
        // The field names stayed singular on purpose. Renaming them to
        // `weaknesses`/`resistances` would mean either migrating every entry in
        // enemies.json in the same breath or accepting both spellings forever,
        // and "both spellings forever" is the drift this file's own abilities
        // comment argues against below. What changed is the grammar of the
        // value, not what the field means.
        //
        // Blank still means OMITTED and is derived from the id (see
        // EnemyEntryResolver.DeriveDamageTypePair) -- the same sentinel every
        // other optional field here uses. To say a monster genuinely has no
        // weakness, write "none": that is an authored answer, and it is the one
        // thing an empty list could never be distinguished from an omission.
        [ContentDoc("A comma-separated list of DamageType names this monster takes +50% from; blank derives one from the id, 'none' means genuinely no weakness.")]
        public string weakness = "";
        [ContentDoc("A comma-separated list of DamageType names this monster takes -50% from; blank derives one from the id, 'none' means genuinely no resistance.")]
        public string resistance = "";

        // Stagger meter capacity. -1 means "derive it from maxHealth", same
        // sentinel convention as every other omittable stat here — see
        // EnemyEntryResolver.BreakShieldPerHealth. 0 is a real, authorable
        // value distinct from "omitted": it means this enemy has no stagger
        // meter at all and cannot be broken.
        [ContentDoc("Stagger meter capacity; -1 derives it from maxHealth, 0 means this monster has no stagger meter at all.")]
        public int breakShieldPoints = -1;

        // Optional. Resources-relative folder of stance sprites; omitted
        // for monsters that have no art yet.
        [ContentDoc("Resources-relative folder of this monster's stance sprites; empty means no art yet.")]
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
        [ContentDoc("The legacy single second action's telegraph name; superseded by abilities for anything new.")]
        public string skillName = "";
        [ContentDoc("The legacy second action's multiplier on this monster's own basic attack.")]
        public float skillPower = -1f;
        [ContentDoc("The legacy second action's odds of being picked on any given turn.")]
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
        [ContentDoc("This monster's real skills (skills.json ids) with relative selection weights; replaces skillName/skillPower/skillChance when non-empty.")]
        public RawEnemyAbility[] abilities = Array.Empty<RawEnemyAbility>();

        // HOW OFTEN IT JUST SWINGS, on the same relative scale as the ability
        // weights above. The basic attack is always in the pool -- a monster
        // whose every turn is a special reads as scripted rather than as
        // dangerous -- and this is the dial for it. 0 takes it out entirely.
        [ContentDoc("The relative weight of the monster's plain attack against its ability weights; 0 removes plain attacks entirely.")]
        public float attackWeight = 1f;

        // Benched, not deleted. An inactive monster keeps its full entry
        // here — stats, weakness, rewards, the lot — but no asset is built
        // for it, so it cannot spawn. Used to narrow the pool to the
        // monsters that actually have art while the stage is being tuned;
        // flipping it back is a one-word edit, with nothing to re-type.
        // Defaults to true so an entry that never mentions it still spawns.
        [ContentDoc("Whether this monster can actually spawn; false benches the entry without deleting it.")]
        public bool active = true;

        // Which way this monster's art is drawn in its source file, so the
        // stage knows whether to mirror it. Defaults to "right" because
        // both sheets authored so far (Bog Witch, Stone Golem) face right —
        // stated explicitly rather than left implicit, since a wrong
        // default here shows up as a monster fighting with its back turned.
        [ContentDoc("Which way this monster's art is drawn in its source file, so the stage knows whether to mirror it.")]
        public string facing = "right";

        // Optional VFX played over the TARGET when this monster's skill
        // lands, nested under "vfx" -- the SAME value a skill carries, so a
        // monster's skill shows a prop near the player it hit exactly the way a
        // player skill shows one near the enemy it hit. See SpellPresentation.
        [ContentDoc("VFX played over the target when this monster's skill lands; see SpellPresentation.")]
        public SpellPresentation vfx = new SpellPresentation();

        // Optional status this monster's ATTACK (basic attack or skill,
        // either lands it) applies to whoever it hits — same shape as
        // SkillDefinition's appliesStatus/statusMagnitude/statusDuration,
        // deliberately not skill-gated: a monster whose whole gimmick is
        // "its claws bleed" shouldn't need an authored skill just to carry
        // that. Required together, same "mixing field sets is rejected"
        // rule as the skill version.
        [ContentDoc("Which StatusEffectType this monster's basic attack or skill applies to whoever it hits, or empty for none.")]
        public string appliesStatus = "";
        [ContentDoc("The magnitude of the applied status; required together with appliesStatus.")]
        public int statusMagnitude = -1;
        [ContentDoc("How many of the afflicted combatant's own turns the applied status lasts; required together with appliesStatus.")]
        public int statusDuration = -1;

        // Encounter-placement gimmick: this monster is never put in the
        // front stage slot (index 0) if the room's other picks give
        // BuildEncounter an alternative — a fragile monster that wants to
        // hide behind something sturdier. Silently ineligible to honour
        // (e.g. every pick this room avoids the front) rather than an
        // error: it's a preference, not a hard rule content authoring can
        // violate.
        [ContentDoc("Whether this monster is never placed in the front stage slot when the room's other picks give an alternative.")]
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
        [ContentDoc("Whether this monster's plain-attack art is a stationary pose rather than a forward strike.")]
        public bool attackHoldsPosition;

        // HOW THIS MONSTER'S PLAIN ATTACK TRAVELS, when it is not holding
        // position: "lunge" (the default lean), "close" (arrive then strike),
        // or "charge" (a committed rush that connects on the impact frame).
        // Empty means lunge, which is what every plain swing has always done,
        // so nothing that leaves it blank changes.
        //
        // A SKILL authors its own approach on the skill itself; this is only
        // for the basic attack, which has no skill entry to carry one. The two
        // never collide: attackHoldsPosition still wins outright, since a
        // stationary slam that also declared a charge is a contradiction the
        // hold resolves in favour of not moving.
        [ContentDoc("How this monster's plain attack travels when not holding position: lunge (default), close, or charge.")]
        public string attackApproach = "";

        // HOW BIG THIS MONSTER STANDS, as a multiplier on the depth scale its
        // slot already carries. 0 means unset and reads as 1.
        //
        // The stage scales every figure by DEPTH alone, which is correct for
        // perspective and says nothing about what the creature is. Two monsters
        // in the same slot are the same size, so a rat and a treant differ only
        // by however big their sheets happen to have been drawn -- and the
        // sheets are all cut to a similar canvas, because that is what the
        // slicing tools do. A thing meant to read as ELITE has no way to.
        //
        // MULTIPLIED, not substituted, so the perspective survives: a big
        // monster in the back row is still smaller than the same monster in
        // front, which is the whole illusion the stage rests on.
        [ContentDoc("A multiplier on this monster's stage size, on top of its slot's own depth scale; 0 means unset and reads as 1.")]
        public float stageScale;

        // HOW MANY OF THE STAGE'S POSITIONS THIS MONSTER OCCUPIES.
        //
        // 0 means unset and reads as 1. A monster that takes two leaves room
        // for one companion instead of two, which is the difference between "a
        // big monster" and "a big monster that dominates the room".
        //
        // Counted by the encounter builder against the same three slots the
        // stage has, so a room can never be dealt more creature than there is
        // floor to stand on.
        [ContentDoc("How many of the stage's positions this monster occupies; 0 means unset and reads as 1.")]
        public int slotSpan;

        // IN THE ROOM ROLL OR NOT. An event's own monster (the Bellwether)
        // is fought only where its event puts it, so it has to stay out of
        // RunEncounter.Pool() while still getting an asset -- which `active`
        // cannot say, since false writes no asset at all.
        [ContentDoc("Whether room fights may roll this monster; false keeps it out of the room pool while an event fight can still name it. Distinct from active, which builds no asset at all.")]
        public bool rollable = true;

        // A fight-long attack stack gained as each round starts
        // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.2). Both zero means none.
        [ContentDoc("An attack stack this monster gains as each round starts, for the rest of the fight; see RawEnemyRally. Omitted or all zero means none.")]
        public RawEnemyRally rallyPerRound = new RawEnemyRally();

        // A fixed sequence on this monster's own acting turns
        // (docs/PLAN_BELLWETHER_KIT.md 1.6 / 3.7): each entry starts on the
        // listed acting turns and then takes the following acting turns, one
        // skill each, until done. Every other turn is the weighted draw.
        [ContentDoc("Fixed skill sequences on this monster's own acting turns (a stunned or skipped turn does not count); every other turn is the weighted draw. See RawEnemyScheduleEntry. Omitted or empty means none.")]
        public RawEnemyScheduleEntry[] schedule = Array.Empty<RawEnemyScheduleEntry>();
    }

    // One scheduled sequence: where it starts and what it plays.
    [Serializable]
    public class RawEnemyScheduleEntry
    {
        [ContentDoc("The acting turns (1 = this monster's first) on which the sequence starts; each at least 1, no repeats, and no two sequences may overlap.")]
        public int[] onTurns = Array.Empty<int>();
        [ContentDoc("The skill ids played in order, one per acting turn, never split; each must also be in abilities (weight 0 keeps it out of the draw).")]
        public string[] skills = Array.Empty<string>();
    }

    // An enemy's per-round rally. Both fields or neither.
    [Serializable]
    public class RawEnemyRally
    {
        [ContentDoc("Attack percent each stack adds (8 = +8%); above 0 when maxStacks is set.")]
        public int attackPercentPerStack;
        [ContentDoc("The most stacks the rally reaches; at least 1 when attackPercentPerStack is set.")]
        public int maxStacks;
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
        [ContentDoc("A skill id from skills.json this monster may draw.")]
        public string skillId = "";

        // Relative likelihood. Zero means "authored but never chosen", which is
        // a legitimate thing to want while tuning and is refused at build time
        // only if EVERY entry is zero.
        [ContentDoc("The relative likelihood this ability is chosen; 0 means authored but never drawn unless every entry is 0.")]
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
