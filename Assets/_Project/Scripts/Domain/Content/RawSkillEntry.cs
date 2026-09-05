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
        [ContentDoc("Stable identifier; written into save files and never renamed once used.")]
        public string id;
        [ContentDoc("The name shown on the skill's own button.")]
        public string displayName;
        [ContentDoc("Flavor text shown to the player; read by no formula.")]
        public string description = "";

        // Which character this belongs to. Required: a skill with no owner
        // would be offered to everyone, and the whole point of this content
        // type is that a kit is a character's own.
        [ContentDoc("The character this skill belongs to; required so it is never offered to everyone.")]
        public string characterId = "";

        // The level at which it becomes available. 1 means "from the start".
        [ContentDoc("The character level this skill becomes available at; see notes for the bookOnly exception.")]
        public int unlockLevel = -1;

        // "DamageSingle" (default), "DamageAll", "HealSelf", "HealParty",
        // "RestorePartyMana" — matched case-insensitively against
        // Domain.Combat.SkillEffect.
        [ContentDoc("Which SkillEffect this casts, matched case-insensitively.")]
        public string effect = "";

        // "SingleEnemy", "AllEnemies", "Self", "Party". Defaults to whatever
        // the effect implies, so most entries never write it.
        [ContentDoc("Which SkillTargeting this hits; defaults to whatever the effect implies.")]
        public string targeting = "";

        [ContentDoc("Mana spent to cast; a skill must cost this and/or resourceCost.")]
        public int manaCost = -1;

        // How much of the owner's signature resource a cast consumes, and
        // whether it takes the lot instead. spendsAllResource still requires
        // resourceCost as a minimum, so a capstone cannot be fired on an
        // empty gauge for nothing.
        [ContentDoc("How much of the owner's signature resource a cast consumes.")]
        public int resourceCost = -1;
        [ContentDoc("Whether the cast takes the caster's entire signature resource instead of resourceCost.")]
        public bool spendsAllResource;

        // Added per point of resource actually spent. Integer on purpose —
        // see SkillResolution.
        [ContentDoc("Added per point of resource actually spent when casting.")]
        public int power = -1;

        // A flat contribution before scaling: the whole amount for a heal,
        // an offset on top of Attack for damage.
        [ContentDoc("A flat contribution before scaling: the whole amount for a heal, an offset on top of Attack for damage.")]
        public int flatAmount = -1;

        // Damage only. The one line in Shawn's kit that answers a
        // high-Defense wall; see SkillResolution.Damage.
        [ContentDoc("Whether this skill's damage skips the target's Defense entirely.")]
        public bool ignoresDefense;

        // Fixed, typed damage packets. When present these REPLACE the
        // Attack + power x resource formula entirely: the spell deals exactly
        // what is authored here, and each packet is checked against the
        // target's weakness and resistance on its own.
        //
        // That independence is the point. 25 Fire + 25 Frost against
        // something that resists Frost and burns to Fire is 37 + 12, not 50
        // of something averaged.
        [ContentDoc("Fixed, typed damage packets that replace the Attack/power formula entirely; each is checked against weakness and resistance on its own.")]
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
        [ContentDoc("How many of the caster's own turns must pass before this skill can be cast again; 0 means no cooldown.")]
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
        [ContentDoc("Whether a player ever picks this from a menu, as against a monster-only skill drawn by weighted chance.")]
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
        [ContentDoc("How the skill looks and sounds when it resolves; see SpellPresentation.")]
        public SpellPresentation vfx = new SpellPresentation();


        // One of Domain.Combat.StatusEffectType — Poison, Regen, Protect,
        // Vulnerable, Stun — matched case-insensitively. Empty means this
        // skill applies no status at all, which is most of them. Landed on
        // whoever this skill's own effect already resolves against: a
        // damage effect's status lands on the enemy it hit, a heal effect's
        // lands on whoever was healed — there is no separate targeting
        // concept to author.
        [ContentDoc("Which StatusEffectType this skill applies on landing, or empty for none.")]
        public string appliesStatus = "";

        // Required together with appliesStatus. Meaning depends on the
        // status: damage or healing per tick for Poison/Regen, a percent for
        // Protect/Vulnerable, unused (but still required, as 1) for Stun.
        [ContentDoc("The magnitude of the applied status; required together with appliesStatus.")]
        public int statusMagnitude = -1;

        // How many of the AFFLICTED combatant's own turns the status lasts.
        // Required together with appliesStatus.
        [ContentDoc("How many of the afflicted combatant's own turns the applied status lasts; required together with appliesStatus.")]
        public int statusDuration = -1;

        // What a character needs, from base scores/talents/worn gear, before
        // this skill can be cast at all -- "<ability score> <amount>" lines,
        // same shape as an item's requires. Optional; a skill with none is
        // always castable once unlocked, which is every skill's behaviour
        // today.
        [ContentDoc("'<ability score> <amount>' lines gating whether this skill can be cast at all.")]
        public string[] requires = Array.Empty<string>();

        // Which axis this skill's damage rides: "Auto" (default) derives it
        // from the CASTER's own attackType (Physical -> Weapon, everything
        // else -> Spell) -- right for the overwhelming majority of skills.
        // "Weapon" or "Spell" overrides that derivation for a skill whose
        // flavour does not match the caster's own type (a headbutt authored
        // on a Nature-typed caster is still, narratively, a physical
        // attack). "None" rides neither axis at all.
        [ContentDoc("Which axis this skill's damage rides: Auto (derive from the caster's own attackType), Weapon, Spell, or None.")]
        public string scalingAxis = "";

        // How many places later in the turn queue this skill knocks its
        // target — the Black Ram's Headbutt. 0 (the default) means it does
        // not touch the queue, which is every other skill in the game.
        //
        // Slots, not charge and not a speed penalty: see
        // TurnOrder.PushBack's own header for why that phrasing is the one
        // that needs no calibration against the scheduler's arbitrary units.
        [ContentDoc("How many places later in the turn queue this skill knocks its target; 0 means it does not touch the queue.")]
        public int queuePushSlots;

        // What this skill transforms the caster into, for a Transform
        // effect. Left unauthored by every other skill, and rejected by the
        // resolver if authored on one — five numbers sitting in an asset
        // doing nothing is exactly the silent-typo case every other
        // cross-field rule in SkillEntryResolver exists to catch.
        [ContentDoc("What the caster transforms into, for a Transform effect; rejected if authored on any other effect.")]
        public Combat.TransformGrant transform = new Combat.TransformGrant();

        // Which of the caster's OWN stance folders plays while this skill
        // resolves. Empty means the long-standing default of "cast" — every
        // skill authored before this field existed still plays exactly the
        // pose it always did. Set this when a monster owns more than one
        // skill and each should look like a different thing happening,
        // rather than three abilities sharing one generic casting pose.
        [ContentDoc("Which of the caster's own stance folders plays while this skill resolves; empty means the default cast pose.")]
        public string stance = "";

        // HOW THE CASTER GETS TO WHAT IT IS HITTING. "hold", "lunge" or
        // "close"; empty means hold, which is what every skill did before this
        // existed and therefore changes nothing for the ones that say nothing.
        //
        // A STRING rather than the enum, for the reason `anchor` and
        // StanceManifest's `loop` are: JsonUtility writes an enum as its
        // ordinal, so the file would read "approach": 2 and reordering the
        // enum would silently repoint every skill in the game.
        //
        // Worth authoring on any melee skill. A monster's claws going through
        // the skill path rather than the plain-attack one is not a reason for
        // them to stop reaching, and until this field existed it was.
        [ContentDoc("How the caster gets to what it is hitting: hold, lunge, or close; empty means hold.")]
        public string approach = "";

        // How hard this skill kicks the stage on its own account, 0..1. Zero
        // means "whatever the damage was worth", which is every skill that has
        // not thought about it. See CombatBeat.Shake for the blow that has to
        // be felt without landing.
        [ContentDoc("How hard this skill kicks the stage on its own account, 0..1; 0 means whatever the damage was worth.")]
        public float shake;

        // Summon only. The enemy id (Assets/_Project/ContentData/enemies.json)
        // this skill calls in on the caster's own side. Required together
        // with summonCap; empty means this skill does not summon anything,
        // which is every skill but a Summon effect's.
        [ContentDoc("The enemy id this skill calls onto the caster's own side, for a Summon effect.")]
        public string summonEnemyId = "";

        // Summon only. Don't summon another one once the caster's side
        // already fields this many LIVING copies of summonEnemyId — the cap
        // is read at the moment the ability is DRAWN, not at the moment it
        // resolves, so a boss capped at two rats picks something else that
        // turn instead of visibly failing to call a third. Required
        // together with summonEnemyId, same both-fields-or-neither rule
        // appliesStatus/statusMagnitude/statusDuration already follow.
        [ContentDoc("The most living copies of summonEnemyId allowed on the caster's side before this skill stops being drawn.")]
        public int summonCap = -1;

        // A SPELL LEARNED FROM A BOOK RATHER THAN BY LEVELLING (docs/
        // PLAN_SHOP.md §1a). False on every skill in the tree today,
        // including the five this will eventually apply to -- Phase A ships
        // the field and bookTier below purely additively, and only Phase E
        // flips this to true (and removes unlockLevel) on the five. The
        // resolver refuses an entry that authors both bookOnly and
        // unlockLevel: an absent unlockLevel resolves to int.MaxValue
        // (SkillEntryResolver's own DefaultUnlockLevel does NOT apply to a
        // book-only entry), which is what keeps a book-only skill off the
        // level ladder without a second gating mechanism.
        [ContentDoc("Whether this skill is learned from a shop book rather than by levelling.")]
        public bool bookOnly;

        // The shop's price band for this spell as a book (1-4, ShopPricing's
        // four-entry lookup) — authored independently of bookOnly and read
        // by the shop's roll from day one (Phase A/gate 3), because "which
        // spells can be found or bought as a book" and "does owning one
        // supersede the level route" are two different questions with two
        // different answers during the staged rollout. 0 means this skill
        // is not book-eligible at all, which is every skill but the five.
        [ContentDoc("The shop's price band (1-4) for this spell as a book; 0 means not book-eligible.")]
        public int bookTier;

        // DOES THE FRONT-RANK RULE APPLY TO THIS SKILL? False, the default,
        // is every skill authored before this existed and every ranged or
        // magical one authored after — CombatEncounter.CanMeleeReach only
        // ever gets asked about a skill that says true here. A hand striking
        // through a monster's own bodyguard is a different claim than a bolt
        // of lightning doing it, and only the first one needed a rule.
        //
        // Only means anything on a SingleEnemy skill — see
        // SkillEntryResolver's own check, the same "this field has no
        // meaning on that effect" rule ignoresDefense and queuePushSlots
        // already follow.
        [ContentDoc("Whether the front-rank melee-reach rule applies to this SingleEnemy skill.")]
        public bool meleeReach;
    }

    // One typed packet inside a spell. `type` is a DamageType name, matched
    // case-insensitively -- see DamageType's own header for the current
    // eleven -- and "Frost" is accepted as a friendlier spelling of Ice so an
    // author never has to remember which word the enum happened to use.
    [Serializable]
    public class RawDamageInstance
    {
        [ContentDoc("A DamageType name this packet is typed as, matched case-insensitively ('Frost' is accepted for Ice).")]
        public string type = "";
        [ContentDoc("The flat amount this packet deals, on the same x10 scale as every other damage number.")]
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
