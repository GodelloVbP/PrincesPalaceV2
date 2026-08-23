using System;

namespace PrincesPalace.Domain.Content
{
    // One character skill, exactly as typed into skills.json. Same -1 / ""
    // sentinel convention as RawEnemyEntry and RawItemEntry: JsonUtility only
    // overwrites fields actually present in the source, so a C# initializer
    // surviving untouched is how the resolver distinguishes "the author
    // omitted this" from "the author wrote 0" — which matters here because 0
    // is a legitimate mana cost.
    [Serializable]
    public class RawSkillEntry
    {
        public string id;
        public string displayName;
        public string description = "";

        // Which character this belongs to. Required: a skill with no owner
        // would be offered to everyone, and the whole point of this content
        // type is that a kit is a character's own.
        public string characterId = "";

        // The level at which it becomes available. 1 means "from the start".
        public int unlockLevel = -1;

        // "DamageSingle" (default), "DamageAll", "HealSelf", "HealParty",
        // "RestorePartyMana" — matched case-insensitively against
        // Domain.Combat.SkillEffect.
        public string effect = "";

        // "SingleEnemy", "AllEnemies", "Self", "Party". Defaults to whatever
        // the effect implies, so most entries never write it.
        public string targeting = "";

        public int manaCost = -1;

        // How much of the owner's signature resource a cast consumes, and
        // whether it takes the lot instead. spendsAllResource still requires
        // resourceCost as a minimum, so a capstone cannot be fired on an
        // empty gauge for nothing.
        public int resourceCost = -1;
        public bool spendsAllResource;

        // Added per point of resource actually spent. Integer on purpose —
        // see SkillResolution.
        public int power = -1;

        // A flat contribution before scaling: the whole amount for a heal,
        // an offset on top of Attack for damage.
        public int flatAmount = -1;

        // Damage only. The one line in Shawn's kit that answers a
        // high-Defense wall; see SkillResolution.Damage.
        public bool ignoresDefense;

        // Fixed, typed damage packets. When present these REPLACE the
        // Attack + power x resource formula entirely: the spell deals exactly
        // what is authored here, and each packet is checked against the
        // target's weakness and resistance on its own.
        //
        // That independence is the point. 25 Fire + 25 Frost against
        // something that resists Frost and burns to Fire is 37 + 12, not 50
        // of something averaged.
        public RawDamageInstance[] damageInstances = Array.Empty<RawDamageInstance>();

        // HOW MANY OF THE CASTER'S OWN TURNS BEFORE IT COMES BACK.
        //
        // 0, the default, is a skill with no cooldown at all -- which is every
        // skill authored before this existed and still most of them. 2 means
        // "use it turn one, then again turn three": the number is counted from
        // the turn it was cast on, which is how an author says it out loud.
        //
        // Counted in the CASTER'S turns rather than in rounds. The turn order
        // is charge-based, so a fast character acts more often than a slow one
        // and a cooldown measured in rounds would be worth twice as much to one
        // of them for reasons nobody chose.
        public int cooldownTurns;

        // DOES A PLAYER EVER PICK THIS FROM A MENU?
        //
        // True for a character's kit. False for a skill a MONSTER owns, and the
        // difference is not cosmetic: nearly every rule about what a skill may
        // cost exists because a player chooses between actions, and a free one
        // would dominate that choice. A monster does not choose -- a weighted
        // draw does -- so "free" is not exploitable and Boulder Slam has no
        // wallet to charge.
        //
        // A FIELD RATHER THAN INFERRED FROM THE OWNER. The resolver cannot see
        // the enemy catalogue, and making it wait for one so it could look up
        // whether characterId names a monster would couple two resolutions that
        // have no other reason to know about each other. ContentDatabase checks
        // the two facts agree once both catalogues exist, which is where every
        // other cross-catalogue check already lives.
        public bool playerSelectable = true;

        // HOW IT LOOKS, nested under "vfx" in the JSON.
        //
        // Six flat fields lived here -- vfxPath, vfxSeconds, vfxImpactFrame,
        // vfxFromCaster, vfxDepartFrame, sfxPath -- and every one of them had a
        // twin on the resolved skill, a twin on the ScriptableObject, and a
        // twin on the combat beat. Adding a seventh meant writing the same
        // field four times and copying it three. Holding the SAME TYPE the rest
        // of the chain holds is what makes a new knob one field instead of
        // four; see SpellPresentation.
        //
        // JsonUtility runs field initialisers before it fills anything in, so
        // an entry with no "vfx" block gets the defaults and an entry with a
        // partial one gets defaults for whatever it left out. That is what
        // retired the -1 and 0 sentinels these fields used to carry.
        public SpellPresentation vfx = new SpellPresentation();


        // One of Domain.Combat.StatusEffectType — Poison, Regen, Protect,
        // Vulnerable, Stun — matched case-insensitively. Empty means this
        // skill applies no status at all, which is most of them. Landed on
        // whoever this skill's own effect already resolves against: a
        // damage effect's status lands on the enemy it hit, a heal effect's
        // lands on whoever was healed — there is no separate targeting
        // concept to author.
        public string appliesStatus = "";

        // Required together with appliesStatus. Meaning depends on the
        // status: damage or healing per tick for Poison/Regen, a percent for
        // Protect/Vulnerable, unused (but still required, as 1) for Stun.
        public int statusMagnitude = -1;

        // How many of the AFFLICTED combatant's own turns the status lasts.
        // Required together with appliesStatus.
        public int statusDuration = -1;

        // What a character needs, from base scores/talents/worn gear, before
        // this skill can be cast at all -- "<ability score> <amount>" lines,
        // same shape as an item's requires. Optional; a skill with none is
        // always castable once unlocked, which is every skill's behaviour
        // today.
        public string[] requires = Array.Empty<string>();

        // Which axis this skill's damage rides: "Auto" (default) derives it
        // from the CASTER's own attackType (Physical -> Weapon, everything
        // else -> Spell) -- right for the overwhelming majority of skills.
        // "Weapon" or "Spell" overrides that derivation for a skill whose
        // flavour does not match the caster's own type (a headbutt authored
        // on a Nature-typed caster is still, narratively, a physical
        // attack). "None" rides neither axis at all.
        public string scalingAxis = "";

        // How many places later in the turn queue this skill knocks its
        // target — the Black Ram's Headbutt. 0 (the default) means it does
        // not touch the queue, which is every other skill in the game.
        //
        // Slots, not charge and not a speed penalty: see
        // TurnOrder.PushBack's own header for why that phrasing is the one
        // that needs no calibration against the scheduler's arbitrary units.
        public int queuePushSlots;

        // What this skill transforms the caster into, for a Transform
        // effect. Left unauthored by every other skill, and rejected by the
        // resolver if authored on one — five numbers sitting in an asset
        // doing nothing is exactly the silent-typo case every other
        // cross-field rule in SkillEntryResolver exists to catch.
        public Combat.TransformGrant transform = new Combat.TransformGrant();
    }

    // One typed packet inside a spell. `type` is a DamageType name -
    // Physical, Fire, Ice, Nature, Poison or Arcane - matched
    // case-insensitively, and "Frost" is accepted as a friendlier spelling of
    // Ice so an author never has to remember which word the enum happened to
    // use.
    [Serializable]
    public class RawDamageInstance
    {
        public string type = "";
        public int amount;
    }

    // JsonUtility cannot deserialize a bare top-level array, so skills.json
    // is one object with a "skills" array inside it.
    [Serializable]
    public class RawSkillFile
    {
        public RawSkillEntry[] skills = Array.Empty<RawSkillEntry>();
    }
}
