using System;

namespace PrincesPalace.Domain.Combat
{
    // THE SENTINEL'S PLANTED SHIELD (docs/PLAN_BJORN_CONSTELLATIONS.md section
    // 2, Phase 4 row 4a): Plant the Shield, Shield Bash, Shieldwall, and the
    // reactive branches Thornwall and Spellbreaker.
    //
    // A WARD, NOT A NEW KIND OF SHIELD. Brace is a Ward skill, and a ward is
    // already one Shielded entry in the holder's status list that
    // DamagePipeline.AfterDefences drains last, before health. Planting puts
    // up exactly such an entry (FightSession.RaiseWard, the one place a ward
    // goes up) and this object remembers WHICH entry it is -- so the badge,
    // the drain order, Shatter and the HUD all treat it as the ward it is, and
    // only the planted shield's own rules live here: its size, its lifetime,
    // the wait before it can go down again, what it has soaked, and how it
    // answers the blows that land on it.
    //
    // SHIELDWALL IS THE SAME ENTRY, TWICE AS BIG, COVERING THE PARTY. It still
    // lives in the holder's own list (his hits drain it in his normal ward
    // order); a hit on any other living ally reaches it after that ally's own
    // wards and before their health (FightSession.PlantedShield). One pool,
    // however many allies it is guarding.
    //
    // OFF BY DEFAULT: nothing here does anything until the wiring calls
    // FightSession.PlantShield, and every reactive parameter is 0/false. The
    // session asks for this object on every ward resolution, and for a
    // combatant who has never planted it answers "nothing placed, no hooks".
    public sealed class PlantedShield
    {
        // ---- the rules, set by the wiring (plan values as defaults) ------------

        // "Lasts until broken or 3 turns" -- the holder's own turns, on
        // TurnWindow's clock.
        public int LifetimeTurns = 3;

        // "3-turn wait before it can be placed again", from the moment it
        // ends (broken, expired, or bashed).
        public int ReplaceWaitTurns = 3;

        // Shield Bash T2: "wait after a Bash is 1 turn instead of 3". The
        // shorter wait applies only when ShortWaitAfterBash is set.
        public int BashWaitTurns = 1;
        public bool ShortWaitAfterBash;

        // Shieldwall (ultimate): the next placement covers every living ally,
        // as ONE pool of ShieldwallMultiplier x the planted shield's points.
        public bool CoversParty;
        public const int ShieldwallMultiplier = 2;

        // Shieldwall's Fury: at most this much per span between two of the
        // holder's turn ends, however many allies it soaks for.
        public int PartyFuryCapPerTurn = 40;

        // Break shards: when on, the attacker whose hit broke the shield takes
        // BreakShardPercent of the placement's size (PlacedPoints) as
        // Physical. Decided 2026-09-28: 20% of the shield's max HP, so the
        // shards grow with the same defences the shield does (130 -> 26 for
        // today's Bjorn, a 260 wall -> 52). Off until the node sets it.
        public bool BreakShards;
        public int BreakShardPercent = 20;

        // Floored, at least 1 while on. Read after the break ends the
        // placement, which is why PlacedPoints outlives End.
        public int BreakShardDamage => BreakShards ? Math.Max(1, PercentOf(PlacedPoints, BreakShardPercent)) : 0;

        // Thornwall: percent of a physical hit from a physical MOVE (a strike
        // or charge -- CombatActions.IsPhysicalMove, the codebase's
        // definition of a melee action) returned to the attacker. 0 = off.
        public int ThornsPercent;

        // Spellbreaker: percent of a MAGIC hit (any DamageType other than
        // Physical) reflected to its caster, as that hit's own type. 0 = off.
        public int ReflectMagicPercent;

        // Spellbreaker T2: a magic hit raises FightSession.SpellHitShieldHolder
        // naming the caster. The Silence status itself is content-hashed
        // (StatusEffect.cs) and lands with the Unity session.
        public bool SilenceCasterOnSpellHit;

        // ---- the placement, owned by FightSession ------------------------------

        // The Shielded entry this placement put up, or null when nothing is
        // planted. Still referenced for the instant between a hit emptying it
        // and the session ending the placement.
        public ActiveStatus Ward { get; private set; }

        public bool IsPlaced => Ward != null;

        // True when the CURRENT placement was made as a Shieldwall (CoversParty
        // is read at placement, so buying the ultimate mid-placement does not
        // re-shape a shield already down).
        public bool IsShieldwall { get; private set; }

        public readonly TurnWindow Lifetime = new TurnWindow();
        public readonly TurnWindow ReplaceWait = new TurnWindow();

        // Everything this placement has soaked, from the holder and from every
        // ally it covered. Shield Bash scales from it; reset at each placement.
        public int AbsorbedThisPlacement { get; private set; }

        // The size the latest placement went up at (the ward's points then).
        // Kept after the placement ends, for the break shards.
        public int PlacedPoints { get; private set; }

        // Shieldwall Fury paid since the holder's last turn end.
        public int PartyFuryThisTurn { get; internal set; }

        public bool CanPlace => !IsPlaced && !ReplaceWait.IsOpen;

        // ---- the numbers ------------------------------------------------------

        // SHIELD HP = 2 x (Defense + Magical Defense) + 10% max HP.
        // Integer rounding: the 10% is FLOORED (maxHealth / 10), and nothing
        // else in the formula can round. Defense and Magical Defense are the
        // combatant's live PhysicalDefense and MagicalDefense at the moment of
        // placement (gear, relics and talents included); a negative sum reads
        // as 0. At least 1, so a placement is never an empty ward (ApplyWard
        // refuses a non-positive pool).
        public static int PointsFor(int defense, int magicalDefense, int maxHealth)
        {
            int armour = Math.Max(0, defense + magicalDefense);
            return Math.Max(1, 2 * armour + Math.Max(0, maxHealth) / 10);
        }

        public static int PointsFor(CombatantState holder) =>
            holder == null ? 0 : PointsFor(holder.PhysicalDefense, holder.MagicalDefense, holder.MaxHealth);

        // Shieldwall's single pool: twice the planted shield.
        public static int ShieldwallPointsFor(CombatantState holder) => ShieldwallMultiplier * PointsFor(holder);

        // THE PER-HIT FURY CLAMP for damage the wall soaks for an ally: the
        // Sentinel root's engine (plan section 2) applied to what was
        // absorbed -- floor(absorbed x 150 / holder max HP), clamped to 3-25.
        // Phase 2 builds the root engine for hits Bjorn takes himself; this is
        // the same formula so the two can merge onto one helper then.
        public const int FuryScale = 150;
        public const int FuryPerHitMin = 3;
        public const int FuryPerHitMax = 25;

        public static int FuryForAbsorbedHit(int absorbed, int holderMaxHealth)
        {
            if (absorbed <= 0 || holderMaxHealth <= 0) return 0;

            long scaled = (long)absorbed * FuryScale / holderMaxHealth;
            return (int)Math.Max(FuryPerHitMin, Math.Min(FuryPerHitMax, scaled));
        }

        // What the cap still allows this turn out of `perHit`.
        public int PartyFuryAllowance(int perHit) =>
            Math.Max(0, Math.Min(perHit, PartyFuryCapPerTurn - PartyFuryThisTurn));

        // Shield Bash T1: "base damage + 30% of what the shield absorbed this
        // placement". Floored.
        public static int BashBonus(int absorbed, int percent) =>
            absorbed <= 0 || percent <= 0 ? 0 : (int)((long)absorbed * percent / 100);

        // A reactive percent of a hit, floored ("return 15%" of 13 is 1).
        public static int PercentOf(int amount, int percent) =>
            amount <= 0 || percent <= 0 ? 0 : (int)((long)amount * percent / 100);

        // ---- transitions, called by FightSession only ---------------------------

        internal void Place(ActiveStatus ward, bool asShieldwall, bool onHoldersTurn)
        {
            Ward = ward;
            IsShieldwall = asShieldwall;
            AbsorbedThisPlacement = 0;
            PlacedPoints = ward?.Magnitude ?? 0;
            Lifetime.Close();
            Lifetime.Open(LifetimeTurns, onHoldersTurn);
        }

        internal void NoteAbsorbed(int amount)
        {
            if (amount > 0) AbsorbedThisPlacement += amount;
        }

        // Ends the placement and starts the wait. Idempotent: ending what is
        // not placed does nothing (a break and an expiry in the same instant
        // start one wait, not two).
        internal void End(int waitTurns, bool onHoldersTurn)
        {
            if (!IsPlaced) return;

            Ward = null;
            IsShieldwall = false;
            Lifetime.Close();
            ReplaceWait.Open(waitTurns, onHoldersTurn);
        }
    }
}
