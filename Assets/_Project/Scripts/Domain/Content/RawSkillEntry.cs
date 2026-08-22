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

        // Resources-relative folder of this spell's animation frames, played
        // over the stage when it resolves. Empty means no visual.
        public string vfxPath = "";

        // How long the whole animation takes, in seconds.
        public float vfxSeconds = -1f;

        // Which frame of that animation the spell actually LANDS on, counting
        // from 1. A bolt is drawn arriving, not sitting still: the damage,
        // the flash and the number belong to the moment it connects, which is
        // partway through the sequence, not at the end of it. Left unset, a
        // spell lands on its peak frame - see SkillEntryResolver.
        public int vfxImpactFrame = -1;

        // Does this effect TRAVEL, or does it happen where it lands?
        //
        // Almost every sheet is a thing that occurs on the target -- a flare, a
        // bolt striking down, rocks erupting -- and is fitted into a square box
        // centred on them. mud_blast is drawn the other way: a conjuring glyph
        // at the left of the cell, a lance crossing it, an impact at the right.
        // Centred on the target that glyph appears in mid-air a few hundred
        // pixels short of the caster, which is what "it does not come from the
        // character" describes.
        //
        // A flag rather than something inferred from the art. It could be
        // guessed -- the energy's centroid walks left to right across a
        // directional sheet and stays put on a centred one -- and a guess that
        // is right four times out of five puts an effect in the wrong place on
        // the fifth with nothing in the content saying why.
        public bool vfxFromCaster;

        // WHICH FRAME THE EFFECT LEAVES ON, for a vfxFromCaster sheet. 1-based
        // like vfxImpactFrame, and 0 means "from the very first frame".
        //
        // Without it a travelling effect starts drifting the instant it appears,
        // so mud_blast's conjuring glyph spun its eight turns while already
        // halfway across the stage -- it tumbled through the air instead of
        // charging where it was cast. The sequence has two phases and the flight
        // belongs to the second: hold at the caster for the charge, then throw.
        //
        // Authored rather than inferred. It could be guessed from the recipe --
        // the spin ends where the composed frames stop rotating -- but the
        // recipe is a build-time tool and the player has never heard of it, and
        // a sheet that charges without spinning would defeat the guess anyway.
        public int vfxDepartFrame;

        // Resources-relative path of the clip that plays when the skill
        // resolves. A path rather than an entry in the Sound enum because a
        // skill is content: adding a spell should not require editing an
        // enum, and a sound that only one spell uses has no business being a
        // named member alongside ButtonClick. Empty means silent.
        public string sfxPath = "";

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
