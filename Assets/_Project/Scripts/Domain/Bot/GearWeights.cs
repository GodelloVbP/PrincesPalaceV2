namespace PrincesPalace.Domain.Bot
{
    // WHAT AN ARCHETYPE WANTS OUT OF A STAT, as five numbers.
    //
    // The archetype has to be the source of its own preferences -- that is the
    // whole reason the four of them exist -- but the numbers those preferences
    // are applied TO live behind ContentDatabase, which Domain cannot see (the
    // plan's F1, and GreedyDefensivePolicy.ChooseOffer's own note on exactly
    // this wall: an ItemOffer names an id and a tier and nothing about what
    // wearing it would do). So the preference travels DOWN as a weight vector
    // and Core's GearEvaluator applies it to the sheet's own derived numbers.
    //
    // Deliberately the same vector for gear and for stat points. A profile that
    // says "offence first" when picking a sword and "health first" when
    // spending a level is not one archetype, it is two -- and the archetype gap
    // is the one graph the whole batch exists to draw.
    //
    // Weights are FLOAT and unnormalised. The score they produce is only ever
    // compared against another score from the same weights, so scale carries no
    // meaning; what carries meaning is the ratio between the five.
    public readonly struct GearWeights
    {
        // Damage. Scored off the sheet's own DMG figure (WeaponPower.
        // DisplayDamage of whatever is live in the main hand) rather than
        // StatBlock.attack, because gear contributes nothing to the latter --
        // see ItemDescription.SimulateEquip's header.
        public readonly float Offence;

        public readonly float Health;

        // Physical and magical mitigation summed. One number rather than two
        // because no archetype in the plan distinguishes them, and a weight
        // nobody sets differently is a knob that only adds a way to be wrong.
        public readonly float Defence;

        public readonly float Speed;
        public readonly float ManaRegen;

        public GearWeights(float offence, float health, float defence, float speed, float manaRegen)
        {
            Offence = offence;
            Health = health;
            Defence = defence;
            Speed = speed;
            ManaRegen = manaRegen;
        }

        // OFFENCE-HEAVY. Health is not zero: an aggressive player still takes
        // the breastplate over nothing, they simply take the sword over the
        // breastplate. A zero would make every non-weapon slot a coin flip,
        // which is not "aggressive", it is "half-dressed".
        //
        // The 4:1 ratio against health is set against the units rather than
        // picked: one point of DMG is worth roughly four points of max health
        // at these tiers (a hit lands every turn, a health pool is spent once),
        // so equal weights would let a +80 health belt outrank a +20 DMG sword.
        public static GearWeights Aggressive => new GearWeights(
            offence: 4f, health: 1f, defence: 2f, speed: 1f, manaRegen: 0.5f);

        // HEALTH AND DEFENCE HEAVY, the mirror of the above with the same
        // reasoning run the other way: a defensive player still carries a
        // weapon, they simply take the shield first.
        public static GearWeights Defensive => new GearWeights(
            offence: 1f, health: 1.5f, defence: 8f, speed: 1f, manaRegen: 0.5f);

        // EVERY AXIS EQUAL. Not RandomLegal's -- see Indifferent below for the
        // difference, which is the whole reason both exist. Kept for a future
        // policy that genuinely wants "all five matter the same", and as the
        // neutral thing a test can rank against.
        public static GearWeights Uniform => new GearWeights(1f, 1f, 1f, 1f, 1f);

        // NO OPINION AT ALL, which is not the same as caring about everything
        // equally: an all-ones vector still ranks a +40 health belt above a +2
        // speed ring, and RandomLegal is supposed to rank nothing. Every
        // candidate scores exactly zero, they all tie, and Core's evaluator
        // resolves the tie the way it resolves every tie -- with the seeded
        // rng. That IS "uniform among the legal candidates", falling out of
        // the mechanism the other three archetypes already use rather than a
        // second code path beside it.
        public static GearWeights Indifferent => new GearWeights(0f, 0f, 0f, 0f, 0f);

        // Whether this vector ranks anything. Read by Core's evaluator for the
        // one decision a zero score cannot express on its own: whether "no
        // candidate beat what is worn" means keep wearing it (a ranking
        // archetype found nothing better) or means draw again (an indifferent
        // one never finds anything better, and would never change clothes).
        public bool RanksNothing =>
            Offence == 0f && Health == 0f && Defence == 0f && Speed == 0f && ManaRegen == 0f;

        // The scalar an evaluator ranks on. Both callers (a candidate item's
        // stat delta, and what one stat point would derive) go through here, so
        // "which sword" and "where does the level go" cannot drift apart.
        public float Score(int offenceDelta, int healthDelta, int defenceDelta, int speedDelta, int manaRegenDelta)
        {
            return Offence * offenceDelta
                 + Health * healthDelta
                 + Defence * defenceDelta
                 + Speed * speedDelta
                 + ManaRegen * manaRegenDelta;
        }

        public float Score(StatDeltas deltas) =>
            Score(deltas.Offence, deltas.Health, deltas.Defence, deltas.Speed, deltas.ManaRegen);
    }

    // What one hypothetical change (wearing a candidate, or placing one stat
    // point) would move, in the five axes GearWeights prices.
    //
    // Filled in Core, off the character sheet's own readers -- see
    // Core/Bot/GearEvaluator. Domain never computes these; it only ranks them.
    public readonly struct StatDeltas
    {
        public readonly int Offence;
        public readonly int Health;
        public readonly int Defence;
        public readonly int Speed;
        public readonly int ManaRegen;

        public StatDeltas(int offence, int health, int defence, int speed, int manaRegen)
        {
            Offence = offence;
            Health = health;
            Defence = defence;
            Speed = speed;
            ManaRegen = manaRegen;
        }

        public static StatDeltas Zero => new StatDeltas(0, 0, 0, 0, 0);
    }

    // One ability score a level-up could go into, with what placing a point
    // there would actually derive.
    //
    // The deltas are measured, not tabled: Core places the point on a throwaway
    // copy and re-reads the sheet's numbers, so a retuned AbilityDerivation
    // moves the bot's choice with no edit here. That matters more than it
    // looks -- Strength and Intelligence derive NOTHING through
    // AbilityDerivation and reach damage through weapon scaling instead, so a
    // hand-written "aggressive puts points in Strength" would be wrong for a
    // character holding a DEX weapon and right for one holding a STR weapon,
    // and would never notice the difference.
    public readonly struct StatOption
    {
        // Stats.AbilityScore, as its integer value -- Domain.Bot deliberately
        // does not take a using on Domain.Stats for one enum, and the driver
        // hands back whichever option index the policy picked anyway.
        public readonly int Score;
        public readonly string Name;
        public readonly StatDeltas Deltas;

        public StatOption(int score, string name, StatDeltas deltas)
        {
            Score = score;
            Name = name;
            Deltas = deltas;
        }
    }

    // One talent orb that is affordable and unlocked right now.
    //
    // `Deltas` is the same measured shape a StatOption carries -- Core unlocks
    // the id on a throwaway copy and re-reads the sheet -- so "damage/offence
    // effects" and "health/ward/heal" are read off what the talent DOES rather
    // than off its name. A talent whose whole effect is a combat rule change
    // (an extra attack, an execute threshold) derives nothing measurable and
    // scores zero here; that is a stated limit of this policy, not a bug, and
    // it is the same limit GreedyAggressive's damage estimate already has for
    // the eight non-previewable skills.
    public readonly struct TalentOption
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Cost;
        public readonly StatDeltas Deltas;

        public TalentOption(string id, string name, int cost, StatDeltas deltas)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Deltas = deltas;
        }
    }
}
