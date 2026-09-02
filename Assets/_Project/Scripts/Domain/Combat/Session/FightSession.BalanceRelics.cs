using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // The balance pass's own relics -- Magic Marker, Jar of Bear Urine,
    // World Ender's Crown, Cursed Idol, Amassing Star, and Rampaging Bull's
    // Horn. (Pointy Nail on the End of a Stick has no code here at all --
    // it is a pure RelicModifierType.ArmorPenetrationFlat entry in
    // relics.json, applied at kit-build time exactly like every other
    // numeric-only relic.)
    //
    // Each of these rides a mechanic built as its own reusable Domain
    // facility (Marks, Fear, FallingOffStacks, RunWideBonusDamagePercent,
    // CombatantState.ArmorPenetration, ConvergenceGate) rather than being
    // wired as one-off logic private to the relic -- see each facility's
    // own header for why, and for the other things it is meant to serve
    // besides the one relic that happens to be first through it.
    public partial class FightSession
    {
        // ---- jar of bear urine ----------------------------------------------------

        // Called once from Begin(), before the first GrantTurnStart -- every
        // enemy on the field is marked from the very first beat, whichever
        // side acts first.
        private void RelicsOnCombatBegin()
        {
            foreach (var kit in _playerKits)
            {
                var actor = kit.Key;
                if (!HasRelic(actor, RelicEffect.JarOfBearUrine)) continue;

                foreach (var enemy in _encounter.LivingEnemies)
                {
                    Marks.Apply(enemy, actor);
                }

                AppendMessage($"{actor.Name} uncorks the jar - every enemy reeks, and is marked.");
            }

            // Loaded Dice: balance pass 2 -- see FightSession.
            // BalanceRelics2.LoadedDiceOnCombatBegin.
            LoadedDiceOnCombatBegin();
        }

        // ---- magic marker -----------------------------------------------------------

        // Every spell that lands marks its target -- called from the same
        // three call sites the Drowned Lantern's own ApplyMark already uses
        // (FightSession.Skills.cs), through the SHARED Marks facility
        // rather than the Lantern's private HashSet. The two marks are
        // independent: a target can carry both at once, and each is
        // consumed by its own relic only.
        private void MagicMarkerApplyMark(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || !target.IsAlive) return;
            if (!HasRelic(actor, RelicEffect.MagicMarker)) return;

            Marks.Apply(target, actor);
        }

        // An ATTACK against a marked target consumes it and refunds 20% of
        // the actor's own missing primary resource. Hooked from
        // RelicsAfterSwing, the same funnel Dual Wield's second hit and
        // Sword in a Box's bonus attack both already pass through -- a
        // double attack can consume two independent marks (there is only
        // ever one mark on a given target at a time, so in practice this
        // means the SECOND swing finds nothing left to consume unless a
        // fresh cast re-marked the target in between).
        private void MagicMarkerConsumeOnAttack(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null) return;
            if (!HasRelic(actor, RelicEffect.MagicMarker)) return;
            if (!Marks.ConsumeMark(target)) return;

            int restored = RestorePrimaryResource(actor, FightTuning.MagicMarkerRestorePercent);
            if (restored > 0)
            {
                AppendMessage($"{actor.Name}'s mark ignites - {restored} restored!");
            }
        }

        // RESOURCE-AGNOSTIC: Shawn's Wool (Signature) if he has one, mana
        // otherwise -- "primary resource" reads as whichever pool the
        // actor's own kit actually spends to act, and every combatant has
        // at most one of the two. Returns how much was actually restored,
        // so a caller with nothing missing can skip its own message rather
        // than announcing a zero.
        private int RestorePrimaryResource(CombatantState actor, int percentOfMissing)
        {
            if (actor == null || percentOfMissing <= 0) return 0;

            if (actor.Signature != null)
            {
                int missing = actor.Signature.Max - actor.Signature.Current;
                int restore = missing * percentOfMissing / 100;
                return restore > 0 ? actor.Signature.Gain(restore) : 0;
            }

            if (actor.MaxMana > 0)
            {
                int missing = actor.MaxMana - actor.CurrentMana;
                int restore = missing * percentOfMissing / 100;
                if (restore <= 0) return 0;

                int before = actor.CurrentMana;
                CombatMath.RestoreMana(actor, restore);
                return actor.CurrentMana - before;
            }

            return 0;
        }

        // ---- world ender's crown -----------------------------------------------------

        // Who has already fired since the last time they were above the
        // threshold -- the "re-arms above 30%" half of the spec. A
        // session-scoped set rather than a field on CombatantState: this is
        // per-FIGHT bookkeeping about a per-fight relic, the same shape
        // _marked (FightSession.Relics.cs) already uses for the same
        // reason.
        private readonly System.Collections.Generic.HashSet<CombatantState> _crownFired =
            new System.Collections.Generic.HashSet<CombatantState>();

        // Called from DealDamage (FightSession.Ledger.cs) after EVERY hit
        // that changes a player's health -- the one funnel every damage
        // path already shares, so a crossing can never be missed because
        // one of several damage call sites forgot to check for it.
        private void WorldEndersCrownCheck(CombatantState target)
        {
            if (target == null || !target.IsPlayerSide || target.MaxHealth <= 0) return;
            if (!HasRelic(target, RelicEffect.WorldEndersCrown)) return;

            float fraction = (float)target.CurrentHealth / target.MaxHealth;

            if (fraction >= FightTuning.WorldEndersCrownHealthFraction)
            {
                // Back above the line -- re-arm for the next crossing.
                _crownFired.Remove(target);
                return;
            }

            if (!target.IsAlive || !_crownFired.Add(target)) return;

            foreach (var enemy in _encounter.LivingEnemies)
            {
                Fear.Apply(enemy, FightTuning.WorldEndersCrownFearTurns, target);
            }

            AppendMessage($"{target.Name}'s crown flares as they falter - every enemy recoils in fear!");
        }

        // ---- cursed idol --------------------------------------------------------------

        // Every hit the wearer lands on an enemy adds a stack (mechanic c),
        // called from DealDamage. AddStack no-ops past the cap on its own.
        private void CursedIdolOnHit(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || target.IsPlayerSide) return;
            if (!HasRelic(actor, RelicEffect.CursedIdol)) return;

            FallingOffStacks.AddStack(target, FightTuning.CursedIdolStackKey,
                FightTuning.CursedIdolStackTurns, FightTuning.CursedIdolMaxStacks);
        }

        // The bonus itself, read inside DealDamage (FightSession.Ledger.cs)
        // rather than TotalDamage -- the stack lives on the TARGET and
        // DealDamage is the one call site that already has both actor and
        // target in hand where TotalDamage's own callers do not all carry
        // one uniformly. "Lowered resistance" is spent as bonus damage on
        // the SAME hit that is landing, the same "additive bonus folded
        // into the one funnel" shape NecklaceDamageBonus already uses.
        private int CursedIdolBonus(CombatantState actor, CombatantState target, int outcomeDamage)
        {
            if (actor == null || target == null || outcomeDamage <= 0) return 0;
            if (!HasRelic(actor, RelicEffect.CursedIdol)) return 0;

            int percent = FallingOffStacks.Magnitude(target, FightTuning.CursedIdolStackKey,
                FightTuning.CursedIdolPercentPerStack,
                FightTuning.CursedIdolPercentPerStack * FightTuning.CursedIdolMaxStacks);

            return percent <= 0 ? 0 : outcomeDamage * percent / 100;
        }

        // ---- amassing star ------------------------------------------------------------

        public int BonusDamagePercentEarned { get; private set; }

        // Called from RelicsOnEachKill (FightSession.Relics.cs), the ONE
        // per-body hook every other per-kill relic (the Bounty Hunter
        // Contract) already shares -- a splash that fells two enemies pays
        // twice, same as the bounty.
        private void AmassingStarOnKill(CombatantState actor, CombatantState victim)
        {
            if (actor == null || victim == null || !actor.IsPlayerSide) return;
            if (victim.IsSummon) return;
            if (!HasRelic(actor, RelicEffect.AmassingStar)) return;

            BonusDamagePercentEarned += FightTuning.AmassingStarPercentPerKill;
            AppendMessage($"{actor.Name}'s star grows brighter - the run itself hits harder now.");
        }

        // ---- rampaging bull's horn ------------------------------------------------------

        // Called from RelicsAfterCast (FightSession.Relics.cs). `skill` is
        // null for the basic spell action, which is never a convergence
        // ability, so the null-check alone excludes it correctly.
        private void RampagingBullsHornOnConvergence(CombatantState actor, ResolvedSkill? skill)
        {
            if (actor == null || !skill.HasValue || skill.Value.Effect != SkillEffect.Transform) return;
            if (!HasRelic(actor, RelicEffect.RampagingBullsHorn)) return;

            // Protect is the existing "incoming damage reduced by Magnitude
            // percent, decays by turn count" status -- exactly this relic's
            // shape, so it needs no status of its own. Refresh-not-stack
            // (StatusEffects.Apply's own rule) means re-casting a
            // convergence ability while the reduction still stands never
            // compounds it.
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Protect,
                FightTuning.BullsHornReductionPercent, FightTuning.BullsHornDurationTurns, actor);

            AppendMessage($"{actor.Name}'s horn lowers - the next blows land softer.");
        }

        // ---- seams for tests -------------------------------------------------
        //
        // World Ender's Crown and Cursed Idol both react to a raw damage
        // event (DealDamage, FightSession.Ledger.cs) rather than to a
        // specific attack/cast path, so a test that wants to pin their
        // arithmetic without fighting a full swing's own variance/scaling
        // needs a way to fire that event directly -- the same reasoning
        // FightSession.Relics.LuckyDeckHealForTest and its siblings already
        // establish for Lucky Deck's own roll.
        public void DealDamageForTest(CombatantState actor, CombatantState target, int amount, DamageType type) =>
            DealDamage(actor, target, amount, type);

        public void HealForTest(CombatantState target, int amount) => HealAndCount(target, amount);
    }
}
