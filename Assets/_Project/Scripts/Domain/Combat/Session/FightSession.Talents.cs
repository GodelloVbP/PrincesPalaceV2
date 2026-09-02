using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // The combat-time half of the talent vocabulary: the three wool engines,
    // the Fragile Lamb's wards, the Black Ram's transform and splashes, and
    // Provoke.
    //
    // Ported from v1's FightController.Talents.cs, which is the single largest
    // body of pure rules that was trapped behind a MonoBehaviour. Nothing here
    // touches the engine; the reasoning comments are carried over intact,
    // because every one of them records a decision rather than a mechanic.
    public sealed partial class FightSession
    {
        // ---- the wool engines -------------------------------------------------

        // How much wool `actor` should gain at the start of this turn, all three
        // engines and the baseline considered.
        //
        // The health-gated tiers REPLACE the baseline; everything else adds to
        // whatever wins. Replace keeps the Ram's ceiling at +3 a turn before
        // on-hit gains, which is the economy the ability prices were sanity-
        // checked against; stacking takes it to +4 and makes a 7-cost transform
        // a two-turn purchase from full health.
        private int SignaturePerTurnFor(CombatantState actor)
        {
            var signature = actor?.Signature;
            if (signature == null) return 0;

            int perTurn = signature.GainPerTurn;

            // The Black Ram: the lower he falls, the faster the fleece comes
            // in. BestBelowHealth picks the single strongest SATISFIED tier
            // rather than summing the tiers he has passed.
            int wounded = actor.Talents.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth, actor);
            if (wounded > perTurn)
            {
                perTurn = wounded;
            }

            // Provoke T3: paid for being the one everything is aimed at. Adds
            // on top, because this is income he ARRANGED rather than income his
            // health handed him -- two different engines inside one path.
            int perProvoked = actor.Talents.Best(TalentEffectType.WoolPerProvokedEnemy);
            if (perProvoked > 0)
            {
                perTurn += perProvoked * StatusEffects.CountAfflictedBy(
                    _encounter.Enemies, actor, StatusEffectType.Provoked);
            }

            // The mage path's engine: paid for board state. Same additive
            // reasoning, and mutually exclusive with the Ram's tiers in
            // practice anyway -- you may only ever hold one root.
            int perStatused = actor.Talents.Best(TalentEffectType.WoolPerStatusedEnemy);
            if (perStatused > 0)
            {
                perTurn += perStatused * StatusEffects.CountAfflictedBy(_encounter.Enemies, actor);
            }

            return System.Math.Max(0, perTurn);
        }

        // ---- the Fragile Lamb --------------------------------------------------

        // A Ward lasts effectively forever and is removed by being SPENT, not by
        // expiring. Generous rather than infinite so an ordinary turn-start tick
        // can never quietly expire one first.
        private const int WardDurationTurns = 999;

        // Who has already paid the Lamb's engine this turn.
        //
        // The cap is once per warded combatant per turn, and it is one of two
        // belts holding the same problem: a ward that paid on every hit it
        // absorbed would return more wool than it cost and the economy would run
        // backwards. The other belt is that a ward pops on the first hit at all.
        //
        // Cleared when the WARDER's turn begins, which is what makes "per turn"
        // mean a round of hers rather than a round of anyone's.
        private readonly HashSet<CombatantState> _wardPayoutsThisTurn = new HashSet<CombatantState>();

        // What a Ward does to one incoming hit: reduce it, pay its caster, heal
        // its wearer, and start the grace period on a self-ward.
        //
        // Called from inside the damage funnel, which is where the existing
        // Shielded consumption already lived -- so every typed and untyped
        // damage path reaches it without any of them knowing about wards.
        private int ResolveWard(CombatantState target, int damage)
        {
            var outcome = StatusEffects.ConsumeWard(target, damage);
            var caster = outcome.WardedBy;
            if (caster == null) return outcome.Damage;

            // The engine. Paid for CARRYING a ward when struck, not for the ward
            // breaking -- which is what lets the capstone's never-expiring wards
            // keep earning instead of silently ending her income the moment she
            // finishes her tree.
            int reward = caster.Talents.Best(TalentEffectType.WoolWhenWardedAllyHit);
            if (reward > 0 && caster.IsAlive && _wardPayoutsThisTurn.Add(target))
            {
                GrantSignature(caster, reward);
                AppendMessage(ReferenceEquals(caster, target)
                    ? $"{caster.Name}'s own ward takes the blow - the fleece thickens."
                    : $"{caster.Name}'s ward on {target.Name} is struck - the fleece thickens.");
            }

            if (outcome.Healed > 0)
            {
                AppendMessage($"The ward breaks over {target.Name} and knits {outcome.Healed} back.");
            }

            // Weight of Wool T3. Only a SELF-ward starts the grace period: the
            // bonus it protects is the self bonus, and an ally's ward popping
            // was never worth 2x to begin with.
            if (ReferenceEquals(caster, target) && !StatusEffects.IsWarded(target))
            {
                int grace = caster.Talents.Best(TalentEffectType.WardSelfBonusPersistsTurns);
                if (grace > 0)
                {
                    target.SelfWardGraceTurns = grace;
                }
            }

            return outcome.Damage;
        }

        // Everything Weight of Wool and Gift: Fury add to a swing, summed onto
        // the combatant so CombatMath can read it without being handed the whole
        // encounter.
        //
        // THE ONLY writer of BonusAttackPercent, called immediately before any
        // damage the actor deals. `spendingGift` is false for a cast that heals
        // or buffs -- Gift: Fury is "your next ATTACK", and burning it on a Ward
        // would be a gift the player never got.
        private void RefreshAttackBonus(CombatantState actor, bool spendingGift)
        {
            if (actor == null) return;

            int bonus = 0;

            int perAlly = actor.Talents.Best(TalentEffectType.WardDamageBonusPerAlly);
            if (perAlly > 0)
            {
                foreach (var ally in _encounter.PlayerParty)
                {
                    if (!ReferenceEquals(ally, actor) && ally.IsAlive && WardedByThisActor(ally, actor))
                    {
                        bonus += perAlly;
                    }
                }
            }

            int self = actor.Talents.Best(TalentEffectType.WardDamageBonusSelf);
            if (self > 0 && (WardedByThisActor(actor, actor) || actor.SelfWardGraceTurns > 0))
            {
                bonus += self;
            }

            if (spendingGift)
            {
                bonus += StatusEffects.ConsumeEmpowerment(actor);
            }

            actor.BonusAttackPercent = bonus;
        }

        private static bool WardedByThisActor(CombatantState wearer, CombatantState caster) =>
            ReferenceEquals(StatusEffects.WardedBy(wearer), caster);

        // Applies a Ward to the caster, and to whoever The Flock has widened it
        // to. Returns how many landed, so the caller can say so.
        private int ApplyWard(CombatantState caster)
        {
            int reduction = caster.Talents.Best(TalentEffectType.WardReductionPercent);
            if (reduction <= 0) return 0;

            int landed = 0;
            WardOne(caster, caster, reduction);
            landed++;

            // The Flock. The spread is a PERCENTAGE OF THE WARD'S OWN strength
            // rather than its own number, so deepening Fleece Ward deepens what
            // the flock gets too -- one strand tunes the construct, the other
            // decides how far it reaches.
            int spread = caster.Talents.Best(TalentEffectType.WardSpreadsToAllies);
            if (spread <= 0) return landed;

            bool everyone = caster.Talents.Has(TalentEffectType.WardSpreadsToWholeParty);
            foreach (var ally in _encounter.PlayerParty)
            {
                if (ReferenceEquals(ally, caster) || !ally.IsAlive) continue;

                WardOne(caster, ally, reduction * spread / 100);
                landed++;

                if (!everyone) break;
            }

            return landed;
        }

        private void WardOne(CombatantState caster, CombatantState wearer, int reduction)
        {
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded,
                System.Math.Max(1, reduction), WardDurationTurns, caster);

            // Mending Fleece. Rides along with the ward rather than being its own
            // cast, so the sustain strand costs no extra action and no extra
            // wool -- it makes the thing she was already doing worth more.
            int regenPercent = caster.Talents.Best(TalentEffectType.WardAlsoAppliesRegen);
            if (regenPercent <= 0 || wearer.MaxHealth <= 0) return;

            int turns = caster.Talents.Threshold(TalentEffectType.WardAlsoAppliesRegen);
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Regen,
                System.Math.Max(1, wearer.MaxHealth * regenPercent / 100),
                System.Math.Max(1, turns), caster);
        }

        // Whether this cast will actually do something, checked BEFORE its cost
        // is taken.
        //
        // Shatter with nothing to detonate and a Gift with nobody to give to
        // both have to refuse rather than silently eat the wool and the turn.
        // That is the same courtesy the "can't pay for it" branch extends, and
        // it matters more here: both are conditional on board state the player
        // can misread.
        private bool CanResolveSkill(CombatantState actor, ResolvedSkill skill, out string refusal)
        {
            refusal = null;

            // STILL COOLING. First, because it is the cheapest question and the
            // one whose answer never depends on the board -- and because a
            // player told "no wards to shatter" about a skill they could not
            // have cast anyway has been told the wrong thing.
            int cooling = CooldownRemaining(actor, skill.Id);
            if (cooling > 0)
            {
                refusal = cooling == 1
                    ? $"{skill.DisplayName} is ready next turn."
                    : $"{skill.DisplayName} is ready in {cooling} turns.";
                return false;
            }

            switch (skill.Effect)
            {
                case SkillEffect.Shatter:
                    if (WardsCastBy(actor).Count == 0)
                    {
                        refusal = $"{actor.Name} has no wards out to shatter.";
                        return false;
                    }

                    return true;

                case SkillEffect.GiftMana:
                case SkillEffect.GiftFury:
                case SkillEffect.GiftHaste:
                    if (GiftRecipient(actor) == null)
                    {
                        refusal = $"{actor.Name} has nobody to give it to.";
                        return false;
                    }

                    return true;

                default:
                    return true;
            }
        }

        // Every living combatant currently wearing a ward this actor cast.
        private List<CombatantState> WardsCastBy(CombatantState caster) =>
            _encounter.PlayerParty
                .Where(c => c.IsAlive && WardedByThisActor(c, caster))
                .ToList();

        // Who a Gift lands on.
        //
        // The first living party member who is not the caster, with no picker.
        // That is not a placeholder: the squad is one deep by default and two at
        // most, so "an ally" is unambiguous today -- there is exactly one
        // candidate or none. It needs a real target picker the moment a third
        // party slot exists.
        private CombatantState GiftRecipient(CombatantState caster) =>
            _encounter.PlayerParty.FirstOrDefault(a => !ReferenceEquals(a, caster) && a.IsAlive);

        // Detonates every ward the caster has out. Each one throws a share of her
        // Attack at a random enemy.
        private void ResolveShatter(CombatantState caster)
        {
            var wearers = WardsCastBy(caster);
            int percent = caster.Talents.Best(TalentEffectType.ShatterDamagePercentOfAttack);
            int selfMultiplier = caster.Talents.Best(TalentEffectType.ShatterSelfWardMultiplier);
            bool appliesVulnerable = caster.Talents.Has(TalentEffectType.ShatterAppliesVulnerable);

            var summary = new StringBuilder($"{caster.Name} shatters {wearers.Count} ward(s)!");

            foreach (var wearer in wearers)
            {
                var enemy = RandomLivingEnemy();
                if (enemy == null) break;

                int share = percent;

                // Her OWN ward is worth triple, and detonating it leaves a
                // character with no defensive stats completely open. That trade
                // is the best decision in the path and is deliberately
                // uncushioned -- no smaller ward handed back, no rider.
                if (ReferenceEquals(wearer, caster) && selfMultiplier > 0)
                {
                    share = share * selfMultiplier / 100;
                }

                // Removed BEFORE the damage lands. A ward still on the books
                // while its own detonation resolved would be absorbed by itself
                // if the blast came back around, and would keep counting toward
                // Weight of Wool for the rest of the volley.
                wearer.Statuses.RemoveAll(s => s.Type == StatusEffectType.Shielded
                                               && ReferenceEquals(s.Source, caster));

                int damage = CombatMath.Scale(caster.Attack * share / 100);
                DealDamage(caster, enemy, damage, AttackTypeOf(caster));
                RecordBeatAmount(System.Math.Max(damage, LargestAmountSoFar));
                SetStance(enemy, enemy.IsAlive ? Stances.Hurt : Stances.Defeated);
                summary.Append($" {enemy.Name} takes {damage}.");

                if (!enemy.IsAlive)
                {
                    summary.Append($" {enemy.Name} is defeated!");
                    _killedThisAction = true;
                    RecordKill(caster, enemy);
                }
                else if (appliesVulnerable)
                {
                    StatusEffects.Apply(enemy.Statuses, StatusEffectType.Vulnerable,
                        ShatterVulnerablePercent, ShatterVulnerableTurns, caster);
                    summary.Append(" It is left wide open!");
                }
            }

            AppendMessage(summary.ToString());
        }

        // Shatter T3's rider. Numbers rather than authored fields because the
        // rule is a flag -- the talent says THAT it applies Vulnerable, and one
        // consistent strength for it keeps the node's own text honest.
        private const int ShatterVulnerablePercent = 30;
        private const int ShatterVulnerableTurns = 2;

        private CombatantState RandomLivingEnemy()
        {
            var living = _encounter.LivingEnemies.ToList();
            if (living.Count == 0) return null;
            return _rng == null ? living[0] : living[_rng.NextInt(0, living.Count)];
        }

        // The three Wool Gifts, resolved against whoever GiftRecipient found.
        private void ResolveGift(CombatantState caster, ResolvedSkill skill)
        {
            var ally = GiftRecipient(caster);
            if (ally == null) return;

            switch (skill.Effect)
            {
                case SkillEffect.GiftMana:
                {
                    int percent = caster.Talents.Best(TalentEffectType.GiftManaPercent);
                    int amount = System.Math.Max(1, ally.MaxMana * percent / 100);
                    CombatMath.RestoreMana(ally, amount);
                    AppendMessage($"{caster.Name} presses wool into {ally.Name}'s hands - {amount} mana back.");
                    break;
                }

                case SkillEffect.GiftFury:
                {
                    int percent = caster.Talents.Best(TalentEffectType.GiftAttackBonusPercent);

                    // Generous duration, spent by the swing rather than by the
                    // clock -- see StatusEffectType.Empowered.
                    StatusEffects.Apply(ally.Statuses, StatusEffectType.Empowered,
                        System.Math.Max(1, percent), WardDurationTurns, caster);
                    AppendMessage($"{caster.Name} winds {ally.Name} up - their next swing lands {percent}% harder.");
                    break;
                }

                case SkillEffect.GiftHaste:
                {
                    if (!caster.Talents.Has(TalentEffectType.GiftAppliesImmediateTurn)
                        || !_encounter.PullToFront(ally))
                    {
                        return;
                    }

                    AppendMessage($"{caster.Name} shoves {ally.Name} forward - they go next.");

                    // Mechanic: the shared "position changed" event.
                    NotePositionChanged(ally, caster);
                    break;
                }
            }
        }

        // Turn-start bookkeeping the Lamb needs: her per-turn payout cap resets,
        // and Weight of Wool T3's grace period counts down.
        //
        // Called beside every other duration, so "a turn" means the same thing
        // here as it does for a status or a transform.
        private void TickLambTurnStart(CombatantState actor)
        {
            if (actor == null) return;

            if (actor.Talents.Best(TalentEffectType.WoolWhenWardedAllyHit) > 0)
            {
                _wardPayoutsThisTurn.Clear();
            }

            if (actor.SelfWardGraceTurns > 0)
            {
                actor.SelfWardGraceTurns--;
            }
        }

        // ---- the Black Ram -----------------------------------------------------

        // Sharp Horns T3: what the swing leaves behind.
        //
        // Permanent for the rest of the fight and stacking, which is the one rule
        // in this vocabulary that deliberately compounds -- it is a party
        // contribution, not a personal buff, and the whole point is that the wall
        // everybody has been chipping at gets softer for everybody. Floored at 0
        // rather than allowed negative: below zero it would start ADDING damage
        // through CombatMath's subtraction, a different mechanic than the one
        // authored.
        private void ApplyDefenseShred(CombatantState actor, CombatantState target)
        {
            int shred = actor?.Talents.Best(TalentEffectType.ShredDefenseOnHit) ?? 0;
            if (shred <= 0 || target == null || !target.IsAlive) return;
            if (target.PhysicalDefense <= 0 && target.MagicalDefense <= 0) return;

            // A FLAT WRITE TO BOTH broad Defenses, floored at 0 each -- there
            // is no longer one generic `Defense` field for this to shred, so
            // the same magnitude lands on both PhysicalDefense and
            // MagicalDefense rather than being split or doubled.
            int beforePhysical = target.PhysicalDefense;
            int beforeMagical = target.MagicalDefense;
            target.PhysicalDefense = System.Math.Max(0, target.PhysicalDefense - shred);
            target.MagicalDefense = System.Math.Max(0, target.MagicalDefense - shred);

            int shredded = (beforePhysical - target.PhysicalDefense) + (beforeMagical - target.MagicalDefense);
            if (shredded > 0)
            {
                AppendMessage($"{actor.Name}'s horns leave {target.Name}'s guard {shredded} thinner - permanently.");
            }
        }

        // Trample T2: a kill spills onto whoever was standing next to it.
        private void ApplyKillSplash(CombatantState actor, CombatantState victim)
        {
            int percent = actor?.Talents.Best(TalentEffectType.KillSplashPercentOfAttack) ?? 0;
            if (percent <= 0) return;

            SplashOntoNeighbours(actor, victim, CombatMath.Scale(actor.Attack * percent / 100),
                $"{victim.Name} goes down hard");
        }

        // Explosive's on-kill splash -- the identical mechanism as
        // ApplyKillSplash just above, read from ModifierEffects instead of
        // Talents. A SEPARATE method rather than folding into ApplyKillSplash
        // -- see ModifierEffectType.OnKillSplashPercent's own comment for why
        // two independently-magnituded sources stay two calls rather than one
        // that silently sums them.
        private void ApplyModifierKillSplash(CombatantState actor, CombatantState victim)
        {
            int percent = actor?.ModifierEffects.Best(ModifierEffectType.OnKillSplashPercent) ?? 0;
            if (percent <= 0) return;

            SplashOntoNeighbours(actor, victim, CombatMath.Scale(actor.Attack * percent / 100),
                $"{victim.Name} goes down in a blast");
        }

        // Black Ram Mode's own splash: while the transform is running, every
        // landed hit spills onto the target's neighbours.
        //
        // A percent of the DAMAGE DEALT, where Trample's kill splash is a percent
        // of raw Attack. Different mechanics, and the difference is deliberate:
        // the kill splash fires once on a corpse and needs a figure that does not
        // depend on the overkill that produced it, while this fires on every
        // swing and should track how hard that swing actually landed.
        private void ApplyTransformSplash(CombatantState actor, CombatantState target, int damage)
        {
            var transformation = actor?.Transformation;
            if (transformation == null || transformation.SplashPercent <= 0 || damage <= 0) return;

            SplashOntoNeighbours(actor, target, System.Math.Max(1, damage * transformation.SplashPercent / 100),
                $"{transformation.DisplayName} hits wider than it looks");
        }

        // The shared half of both splashes: who counts as "either side", and
        // saying what happened.
        //
        // "Adjacent" is index adjacency in Enemies, which is the same order the
        // stage draws front-to-back -- so the enemies either side on screen are
        // the enemies either side here, and no new positional concept had to be
        // invented for one talent.
        //
        // Dead neighbours are skipped but still COUNTED as neighbours: a corpse
        // in slot 1 does not make slots 0 and 2 adjacent to each other. Splash
        // never chains -- it is computed from the original hit, never recursively
        // from its own kills, or one blow would cascade down a whole row.
        // Takes the SOURCE as well as the epicentre, so the splash is credited
        // to whoever caused it. Both callers already hold the actor; without it
        // the two widest damage sources in the game would land in nobody's
        // column and the ledger would quietly under-report every Black Ram.
        private void SplashOntoNeighbours(CombatantState source, CombatantState epicentre, int splash, string cause)
        {
            if (epicentre == null || splash <= 0) return;

            var enemies = _encounter.Enemies;
            int index = -1;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (ReferenceEquals(enemies[i], epicentre)) { index = i; break; }
            }

            if (index < 0) return;

            foreach (int neighbour in new[] { index - 1, index + 1 })
            {
                if (neighbour < 0 || neighbour >= enemies.Count) continue;

                var bystander = enemies[neighbour];
                if (bystander == null || !bystander.IsAlive) continue;

                DealDamage(source, bystander, splash, AttackTypeOf(source));
                SetStance(bystander, bystander.IsAlive ? Stances.Hurt : Stances.Defeated);
                AppendMessage($"{cause} - {bystander.Name} takes {splash} from it!");

                if (!bystander.IsAlive)
                {
                    AppendMessage($"{bystander.Name} is defeated!");
                    RecordKill(source, bystander);
                }
            }
        }

        // ---- the transform ------------------------------------------------------

        // Puts the caster under a Transform skill's grant, with Wrath's duration
        // bonus and Charge's party-wide shove folded in.
        private void EnterTransform(CombatantState actor, ResolvedSkill skill)
        {
            var grant = skill.Transform;
            if (actor == null || grant == null || !grant.IsAuthored) return;

            // Re-entering while already transformed refreshes rather than
            // stacking a second set of bonuses -- the same refresh-don't-compound
            // rule StatusEffects.Apply follows, and for the same reason: paying
            // the cost twice should never be strictly better than paying it once
            // and waiting.
            if (actor.Transformation != null)
            {
                Transformation.Exit(actor);
            }

            int turns = grant.turns + actor.Talents.Best(TalentEffectType.TransformDurationBonus);
            var transformation = Transformation.Enter(actor, grant.displayName, turns,
                grant.attackPercent, grant.speedPercent, grant.temporaryHealthPercent, grant.splashPercent);

            // Speed changed, so the SCHEDULER has to be told. Without this the
            // plate would show the bonus and the turn queue would ignore it -- a
            // failure with no visible symptom other than the mode feeling weaker
            // than its numbers.
            _encounter.RefreshSpeed(actor);

            AppendMessage($"{actor.Name} becomes {transformation.DisplayName} for {transformation.TurnsRemaining} turns!");

            // Charge T3. Fires on ENTERING, which is why the rule is worded
            // "entering" rather than "casting".
            if (actor.Talents.Has(TalentEffectType.TransformPushesEveryEnemy))
            {
                foreach (var enemy in _encounter.LivingEnemies.ToList())
                {
                    _encounter.PushBack(enemy, 1);
                }

                AppendMessage("The charge scatters the line - every enemy is driven back!");
            }
        }

        // The transform's own turn-start tick: expire it, hold it, or make it
        // permanent.
        private void TickTransform(CombatantState actor)
        {
            var transformation = actor?.Transformation;
            if (transformation == null) return;

            // The capstone. Checked BEFORE the timer, so reaching the health gate
            // on the very turn the transform would have run out keeps it rather
            // than losing it by one tick.
            if (!transformation.IsPermanent
                && actor.Talents.IsBelowThreshold(TalentEffectType.TransformPermanentBelowHealth, actor))
            {
                transformation.IsPermanent = true;
                AppendMessage($"{actor.Name} stops changing back. This is what he is now.");
            }

            if (transformation.IsPermanent) return;

            // Wrath T3: wounded enough, and the timer simply does not run.
            if (actor.Talents.IsBelowThreshold(TalentEffectType.TransformHoldsBelowHealth, actor)) return;

            transformation.TurnsRemaining--;
            if (transformation.TurnsRemaining > 0) return;

            string name = transformation.DisplayName;
            Transformation.Exit(actor);
            _encounter.RefreshSpeed(actor);
            AppendMessage($"{actor.Name} is no longer {name}.");
        }

        // Wrath T2: a kill during the transform buys more of it, up to a total
        // extension the talent itself states.
        private void ExtendTransformOnKill(CombatantState actor)
        {
            var transformation = actor?.Transformation;
            if (transformation == null || transformation.IsPermanent) return;

            int perKill = actor.Talents.Best(TalentEffectType.TransformExtendOnKill);
            int cap = actor.Talents.Threshold(TalentEffectType.TransformExtendOnKill);
            if (perKill <= 0 || transformation.ExtensionsGranted >= cap) return;

            int granted = System.Math.Min(perKill, cap - transformation.ExtensionsGranted);
            transformation.ExtensionsGranted += granted;
            transformation.TurnsRemaining += granted;
            AppendMessage($"The kill feeds it - {transformation.DisplayName} holds for {granted} turn(s) longer.");
        }

        // ---- Provoke ------------------------------------------------------------

        // Lands the taunt on one enemy, or on all of them once Provoke T3 is
        // bought. Returns how many were actually provoked, so the caller can say
        // something honest when there was nobody left to provoke.
        private int ApplyProvoke(CombatantState actor, CombatantState target)
        {
            if (actor == null) return 0;

            bool everyone = actor.Talents.Has(TalentEffectType.ProvokeHitsEveryEnemy);
            var victims = everyone
                ? _encounter.LivingEnemies.ToList()
                : new List<CombatantState> { target ?? _encounter.FrontEnemy };

            int reduction = actor.Talents.Best(TalentEffectType.ProvokedDamageReductionPercent);
            int count = 0;

            foreach (var victim in victims)
            {
                if (victim == null || !victim.IsAlive) continue;

                // Duration 1 is nominal -- Provoked is spent by the turn it
                // redirects rather than counted down, so the number is only there
                // to satisfy ActiveStatus' own floor.
                StatusEffects.Apply(victim.Statuses, StatusEffectType.Provoked, reduction, 1, actor);
                count++;
            }

            return count;
        }
    }
}
