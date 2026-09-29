using System;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat
{
    // ONE CLAMPED COUNTER WITH A CAPACITY, THREE GAIN TRIGGERS AND A DECAY.
    // Every combat resource in the game is this: mana, Shawn's Wool, and
    // whatever a character trades mana for. Was `SignatureResource` -- renamed
    // rather than copied, because the runtime it already was is the runtime a
    // primary pool needs, and a second class would have meant two answers to
    // "what does spending look like".
    //
    // A combatant carries at most two of these in two NAMED slots (see
    // CombatantState.PrimaryPool / .SignaturePool) rather than a keyed list:
    // nothing indexes a pool by id, and two fields keep "which pool does this
    // cost spend" unambiguous at the call site instead of at a lookup.
    //
    // WHAT MAKES TWO POOLS FEEL DIFFERENT IS THE AUTHORED NUMBERS, not the
    // code. Mana starts full, never decays, and only ever goes down: a budget.
    // A signature resource starts empty and grows: an arc, where the character
    // holding it is weakest on turn one and strongest at the end. Both shapes
    // come out of the same fields; see ContentData/pools.json.
    //
    // Engine-free and pure. Nothing here knows what a pool is FOR; the rules
    // about who has one and what it buys live in Core.
    public sealed class ResourcePool
    {
        public string Id;
        public string DisplayName;

        // What the combat meter's tag reads, e.g. "MP". Also what a skill's
        // cost column prints beside its number (FightHudModel.CostLabel), so
        // a pool that is not mana never shows a cost in "MP".
        public string ShortTag = "";

        public int Current;
        public int Max;

        public int GainPerTurn;

        // ONE MEANING, TWO SEAMS. On both pools this is "gained on a damaging
        // action by the owner"; what differs is which actions the slot's own
        // call site counts, and that is deliberate rather than drift.
        //
        //   PRIMARY  -- fired from the damage funnel (FightSession.Ledger's
        //               ApplyAndCountDamage), so a plain attack, a damaging
        //               skill, an enemy ability all pay it, once per action
        //               however many targets they hit, and a miss or a heal
        //               pays nothing. That is Fury's authored promise: "gains
        //               when he deals damage".
        //   SIGNATURE -- fired only from the Attack verb (FightSession's
        //               ExecuteAttack). ONLY a basic swing builds Wool, which
        //               is what makes swinging anyway a deliberate choice on
        //               a character whose attack barely dents a defence.
        //
        // The narrower one is a rule about Wool, not about this field, so the
        // name stays gainOnAttack on both.
        public int GainOnAttack;

        public int GainOnDamageTaken;

        // What an IDLE turn costs. Zero for mana and for every signature
        // resource today, so this whole half is inert until a pool authors
        // it -- see TickTurnStart for what "idle" means and DecayUnless for
        // who decides.
        public int DecayPerIdleTurn;

        // What stops a turn counting as idle. Damage (dealt or taken) is the
        // narrow reading and the shipped one; AnyAction is the wide one. An
        // enum rather than a bool because the owner can widen the rule by
        // authoring, with no code change -- see PoolDecayTrigger.
        public PoolDecayTrigger DecayUnless = PoolDecayTrigger.Damage;

        // Whether a mana potion, RestorePartyMana and the bot's missing-mana
        // accounting refill this pool. TRUE for mana, and true by default:
        // an unopinionated pool is mana-shaped and it is the exception that
        // opts out. FALSE would make CombatMath.RestoreMana a no-op here --
        // without which a mana potion is a rage potion.
        public bool RestoredByManaEffects = true;

        // Whether the holder can equip spell books. Read by phase C's four
        // gates; nothing consults it yet.
        public bool AllowsSpellBooks = true;

        // How the fight HUD paints this pool's meter, carried off the
        // definition rather than looked up: the meter is built at scene-build
        // time from FightHudPalette and recoloured at runtime in phase D, and
        // the runtime half needs the numbers in hand where the combatant is.
        public bool Pulse;
        public string BrightHex = "";
        public string DeepHex = "";
        public string TextHex = "";

        // How much damage ONE point soaks. The resource is counted in small
        // whole numbers because that is what its skills spend — Shear costs
        // three of them — while damage lives on the x10 health scale. Without
        // this the two halves of the resource cannot both be tuned: a pool
        // big enough to be armour would make a resource-scaling skill absurd,
        // and a pool small enough to spend would be soaked through by a
        // single hit.
        public int AbsorbPerPoint = 1;

        // Whether this resource soaks damage at all.
        //
        // FALSE for Wool as of the talent rework (handoff §2): wool stopped
        // being a damage sponge and became the thing the talent tree's
        // abilities spend. The two jobs were always in tension — banking for
        // armour and banking to spend are the same decision made twice — and
        // the tree only becomes a real set of choices once the resource has
        // exactly one use.
        //
        // The soak machinery below is deliberately KEPT rather than deleted.
        // It is tuned (see ContentDatabase.SignatureAbsorbPerPoint's own
        // long comment on why 2 and not 10), it is the obvious shape for a
        // future resource that IS armour, and deleting it would throw away a
        // real balance investigation to save one branch.
        public bool AbsorbsDamage;

        // WHAT THE HOLDER HAS DONE SINCE THEIR PREVIOUS TURN START. Two
        // flags, not one, because the two DecayUnless readings ask different
        // questions and a single "did something" flag could only answer one
        // of them. Reset by TickTurnStart once the answer has been used.
        //
        // On the pool rather than on CombatantState because a combatant can
        // hold two pools with different DecayUnless values, and "was this
        // turn idle" is then two different answers about one turn.
        private bool _dealtOrTookDamage;
        private bool _acted;

        // THE COMPATIBILITY SHAPE, and the one 21 call sites still build a
        // signature resource through. Starts EMPTY (the arc), no decay, and
        // mana-shaped defaults for everything a signature resource has never
        // had an opinion about.
        public ResourcePool(string id, string displayName, int max, int gainPerTurn, int gainOnAttack, int gainOnDamageTaken, int absorbPerPoint = 1, bool absorbsDamage = false)
        {
            Id = id;
            DisplayName = displayName;
            ShortTag = displayName ?? "";
            Max = Math.Max(0, max);
            GainPerTurn = gainPerTurn;
            GainOnAttack = gainOnAttack;
            GainOnDamageTaken = gainOnDamageTaken;
            AbsorbPerPoint = Math.Max(1, absorbPerPoint);
            AbsorbsDamage = absorbsDamage;
            Current = 0;
        }

        // FROM A CONTENT ROW. Capacity and per-turn gain are passed in rather
        // than read off the definition, because both are the END of a
        // precedence chain the definition is only the base of: ability
        // scores, talents, relics, the reward track and gear modifiers all
        // have a say, and that chain lives in Core where the character is
        // (ContentDatabase.BuildPrimaryPool). The definition owns everything
        // NOBODY else contributes to.
        public ResourcePool(ResolvedPool definition, int capacity, int gainPerTurn)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            Id = definition.Id;
            DisplayName = definition.DisplayName;
            ShortTag = definition.ShortTag;

            Max = Math.Max(0, capacity);
            GainPerTurn = Math.Max(0, gainPerTurn);
            GainOnAttack = Math.Max(0, definition.GainOnAttack);
            GainOnDamageTaken = Math.Max(0, definition.GainOnDamageTaken);

            DecayPerIdleTurn = Math.Max(0, definition.DecayPerIdleTurn);
            DecayUnless = definition.DecayUnless;

            RestoredByManaEffects = definition.RestoredByManaEffects;
            AllowsSpellBooks = definition.AllowsSpellBooks;

            Pulse = definition.Pulse;
            BrightHex = definition.BrightHex;
            DeepHex = definition.DeepHex;
            TextHex = definition.TextHex;

            AbsorbsDamage = definition.AbsorbsDamage;

            Current = StartingValue(definition, Max);
        }

        // EVERY FIELD IS COPIED, never a reference to the ResolvedPool kept.
        // A definition's record is shared by every fight that fields the pool
        // (ContentDatabase hands out one instance for the whole session), so
        // a pool holding the record would put a per-fight counter one field
        // access away from the catalogue -- exactly what ContentIsolationTests
        // exists to catch.
        private static int StartingValue(ResolvedPool definition, int max)
        {
            switch (definition.StartRule)
            {
                case PoolStartRule.Zero: return 0;
                case PoolStartRule.Value: return Math.Min(max, Math.Max(0, definition.StartValue));
                default: return max;
            }
        }

        public bool IsFull => Current >= Max;

        // Returns how much was ACTUALLY gained after clamping, not what was
        // asked for. Callers use the difference to tell the player their
        // fleece is overflowing — waste they can see is what makes spending
        // feel urgent, where a silent clamp would just look like the number
        // stopped working.
        public int Gain(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int before = Current;
            Current = Math.Min(Max, Current + amount);
            return Current - before;
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && Current >= amount;
        }

        // All or nothing. A partial spend would let an ability fire at a
        // fraction of its cost and a fraction of its effect, which is a
        // balance hole rather than a graceful degradation.
        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            Current -= amount;
            return true;
        }

        // CLAMPED, NOT ALL-OR-NOTHING, and both exist on purpose. TrySpend is
        // what a COST uses: an ability either fires at full price or does not
        // fire. This is what a DRAIN uses -- Runic's ward conversion, a
        // status that eats the pool -- where taking whatever is there is the
        // whole point. Returns how much actually left the pool.
        public int SpendUpTo(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int before = Current;
            Current = Math.Max(0, Current - amount);
            return before - Current;
        }

        // Spends up to `amount` of DAMAGE and reports how much it covered.
        // This is the armour half of the resource: incoming damage eats it
        // before HP, so banking is defence and spending is offence, out of
        // one pool.
        //
        // Partial points round UP against the holder — a graze still scuffs a
        // whole point of fleece. Nothing is actually lost to that today,
        // since the smallest possible hit is exactly one point's worth
        // (CombatMath floors damage at one, then scales it), but the rule is
        // stated rather than assumed so a future half-scale damage source
        // cannot quietly make the pool infinite against chip damage.
        public int Absorb(int amount)
        {
            if (!AbsorbsDamage || amount <= 0 || Current <= 0)
            {
                return 0;
            }

            int absorbed = Math.Min(Current * AbsorbPerPoint, amount);
            Current -= (absorbed + AbsorbPerPoint - 1) / AbsorbPerPoint;
            return absorbed;
        }

        // THE HOLDER DID SOMETHING. Called by the fight at the two seams that
        // already exist for it: every action opens a beat, and every point of
        // damage funnels through one method. Recorded rather than acted on,
        // because the question it answers -- "was the turn just ended idle"
        // -- is only asked at the NEXT turn start.
        //
        // Damage implies action: a swing that connects is not somehow less of
        // an action than a Move. Stated here rather than at each call site so
        // no caller has to remember to report both.
        public void NoteActivity(PoolActivity kind)
        {
            _acted = true;
            if (kind == PoolActivity.Damage)
            {
                _dealtOrTookDamage = true;
            }
        }

        // THE HOLDER'S TURN OPENS: gain, then decay, then forget.
        //
        // ORDER MATTERS AND IS THE AUTHORED ONE (plan P4). Gain first, so a
        // pool with both a per-turn trickle and an idle decay nets the
        // difference rather than decaying a number the trickle was about to
        // replace. Decay second and only when the turn just ended was idle by
        // this pool's own DecayUnless rule. Never below zero, never above Max
        // -- both clamps live in Gain/SpendUpTo rather than being restated.
        //
        // MANA'S NUMBERS MAKE THIS A NO-OP TODAY except for the per-turn
        // gain: DecayPerIdleTurn is 0 on the shipped row, so the whole decay
        // half is authored-in-waiting. That is deliberate, not dead code --
        // see ContentData/pools.json's own note.
        public void TickTurnStart() => TickTurnStart(allowDecay: true);

        // Whether the turn now starting would drain this pool: nothing the
        // holder has done since the last turn start met DecayUnless, and the
        // pool has something to lose. Read by an engine that forgives a drain
        // (Bloodfire's first idle turn) before the tick spends it.
        public bool WouldDecayThisTurn =>
            DecayPerIdleTurn > 0 && Current > 0
            && !(DecayUnless == PoolDecayTrigger.AnyAction ? _acted : _dealtOrTookDamage);

        // `allowDecay: false` is an engine that switches idle decay off for
        // its holder (the Juggernaut's, FuryEngine.SuppressesIdleDecay): the
        // gain and the forgetting still happen, only the drain is skipped.
        public void TickTurnStart(bool allowDecay)
        {
            Gain(GainPerTurn);

            bool kept = DecayUnless == PoolDecayTrigger.AnyAction ? _acted : _dealtOrTookDamage;
            if (!kept && allowDecay)
            {
                SpendUpTo(DecayPerIdleTurn);
            }

            _acted = false;
            _dealtOrTookDamage = false;
        }
    }

    // WHAT COUNTS AS HAVING DONE SOMETHING, from the fight's side. Paired
    // with PoolDecayTrigger, which is the AUTHORED side of the same
    // question: a row says which of these kinds it needs to see, and the
    // fight reports whichever kind actually happened.
    public enum PoolActivity
    {
        // Any action at all: a Move, an item, a Provoke that hit nothing.
        Action,

        // Damage dealt or damage taken. Implies Action.
        Damage,
    }
}
