using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // Plays not to die. In a fight: heal first, ward second, and only then
    // hit back -- and when it does hit back, it aims at whoever is about to
    // hurt the party the most, not whoever is closest to dead. On the map:
    // rest early and often, bank treasure, and route AROUND an elite when
    // there is any other way forward. On offers and drafts, it reaches for
    // armour and healing when the option actually says so and otherwise
    // falls back to GreedyAggressive's "biggest number" read, because
    // Domain cannot see enough of an offer to do better -- see each method's
    // own header for exactly what was and was not identifiable.
    public sealed class GreedyDefensivePolicy : IFightPolicy, IRunPolicy
    {
        // Below this fraction of max HP, a HEALING option (item or skill)
        // outranks everything else on the menu. Higher than
        // GreedyAggressive's 30% on purpose -- this archetype spends a turn
        // on safety sooner, which is the entire point of it existing.
        private const float HealBelowHealthFraction = 0.50f;

        // Below this fraction, with nothing restorative left to take, this
        // is as much trouble as the archetype's own read of the fight gets.
        // See the last-resort note on Choose() for why that does NOT mean
        // passing the turn.
        private const float DesperateHealthFraction = 0.25f;

        // Rest below this fraction of the party's HP, rather than
        // GreedyAggressive's 50% -- see ChooseNode.
        // A PROPERTY, NOT A CONST, since ShopNodePreference reads it through
        // IRunPolicy -- the wrapper must not walk a 60%-health party past the
        // Rest node into a shop, and a copy of 0.75 inside it would be a
        // second number to keep in step.
        public float RestBelowPartyHpFraction => 0.75f;

        // Livelock guard, found by the balance bot at seed 629 / Late /
        // GreedyDefensive: FightInvariants.TooManyCommands, 201 commands, the
        // last ten all fleece_ward, the enemy's HP never moving.
        //
        // fleece_ward is a SINGLE-HIT shield -- StatusEffects.ConsumeWard
        // removes the Shielded status the moment a hit lands (WardOne applies
        // it with a 999-turn duration, but that duration is never what ends
        // it; the hit is). Against an enemy that lands one every round, the
        // ward is gone again before this policy is next asked, so
        // "!alreadyWarded" is true on every single turn and the ward branch
        // -- which runs before the damage branch below -- recasts it forever
        // without Shawn ever swinging back. The fight cannot end: nothing
        // ever reduces the enemy's HP.
        //
        // Not a game bug: AdvanceAfterAction/AutoResolveEnemyTurns ran fine
        // every round (no StalledEnemyTurn hit was recorded for this seed),
        // and re-warding when unwarded is individually a reasonable read of
        // "defensive". The bug is that the policy has no memory of having
        // just tried that and it not buying any progress. Capped per actor
        // rather than globally, and reset the moment a swing actually lands,
        // so a ward that is genuinely doing its job (the enemy misses, or
        // dies to someone else first) is never penalised -- only a ward that
        // keeps getting immediately spent for zero net progress is.
        //
        // Kept at 3 rather than being retuned to NonDamagingSkillGuard's own
        // default of 2 -- this was the number that fixed seed 629 and
        // BotPolicyTests pins the exact sequence it produces, so changing it
        // now would be re-tuning a working guard to match an unrelated one's
        // default rather than for any reason of its own.
        private const int MaxConsecutiveWardsWithoutASwing = 3;

        // Shared with Lookahead2Policy's own no-progress backstop -- see
        // NonDamagingSkillGuard's header. This was the original of the two
        // (found first, at seed 629) and is the reason the type exists at
        // all. Originally ward-only; now also guards any other repeated
        // 0-damage Skill pick reached by the "hit back" branch's own
        // fallback below, each tracked under its own skill id so the ward
        // count and (say) a repeated Provoke count never bleed into each
        // other.
        private readonly NonDamagingSkillGuard _repeatGuard = new NonDamagingSkillGuard();
        private const string WardGuardKey = "ward";

        // What RecordProgress actually requires now: NOT "a swing was
        // attempted" (that reset unconditionally, seeds 488/772/... in the
        // 20260902-013421 batch: ward,ward,ward,attack-that-does-not-land,
        // ward,ward,ward,attack-that-does-not-land forever -- capped at 3
        // wards in a row same as always, but the cap kept being handed back
        // every fourth turn regardless of whether that attack actually did
        // anything). Total living-enemy HP, snapshotted per actor on every
        // Choose() call; the swing branch below only resets the guard when
        // that total has actually dropped since the LAST time this actor was
        // asked. An attack that keeps missing (or a target that keeps
        // getting healed back up by an ally) now leaves the ward guard
        // tripped instead of handing it a fresh streak of 3 -- the fallback
        // swing keeps being taken every turn from then on, which is what
        // actually ends the fight instead of cycling back to ward.
        private readonly Dictionary<CombatantState, int> _enemyHpAtLastCheck = new Dictionary<CombatantState, int>();

        public FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            // ONE ALLY PER ALLY-FACING SKILL, before anything below ranks
            // anything. LegalActions offers a ward or a gift once per eligible
            // squadmate -- the player's own menu -- and this archetype has no
            // opinion about which squadmate; AllyTargetSelection carries the
            // rules that used to live in the engine. Narrowing here rather
            // than teaching every score to break the tie keeps that judgement
            // in one place.
            legal = AllyTargetSelection.Narrow(session, actor, legal);

            int totalEnemyHp = session.Encounter.LivingEnemies.Sum(e => e.CurrentHealth);
            bool madeRealProgressSinceLastAsk =
                !_enemyHpAtLastCheck.TryGetValue(actor, out var lastEnemyHp) || totalEnemyHp < lastEnemyHp;
            _enemyHpAtLastCheck[actor] = totalEnemyHp;

            bool hurt = actor != null && actor.MaxHealth > 0 &&
                        actor.CurrentHealth <= actor.MaxHealth * HealBelowHealthFraction;

            if (hurt)
            {
                // A healing ITEM specifically -- ItemRestoresMana distinguishes
                // a health potion from a mana one on the same satchel entry,
                // the same read GreedyAggressive uses.
                var healItem = legal.FirstOrDefault(a => a.Kind == FightActionKind.Item && !a.ItemRestoresMana);
                if (healItem.Kind == FightActionKind.Item) return healItem;

                // A healing SKILL -- HealSelf/HealParty, identified off the
                // skill's own SkillEffect rather than its name or index, the
                // same way GreedyAggressive's EstimateDamage reads
                // DamageSingle/DamageAll off the effect rather than guessing
                // from a display string.
                var healSkill = FirstSkillWithEffect(session, actor, legal,
                    SkillEffect.HealSelf, SkillEffect.HealParty);
                if (healSkill.HasValue) return healSkill.Value;
            }

            // A ward/shield skill, taken only while nothing already active
            // would make a second one wasted -- StatusEffects.IsWarded is
            // the same query StatusEffects' own Shatter-eligibility check
            // uses for "is there a ward to detonate", so this reads the
            // actor's status list the same way the rest of Combat does
            // rather than re-deriving it.
            bool alreadyWarded = StatusEffects.IsWarded(actor);
            if (!alreadyWarded)
            {
                if (_repeatGuard.MayChoose(actor, WardGuardKey, MaxConsecutiveWardsWithoutASwing))
                {
                    var wardSkill = FirstSkillWithEffect(session, actor, legal, SkillEffect.Ward);
                    if (wardSkill.HasValue)
                    {
                        _repeatGuard.RecordChosen(actor, WardGuardKey);
                        return wardSkill.Value;
                    }
                }
                // MaxConsecutiveWardsWithoutASwing reached: the ward is
                // treated as unavailable this turn and the fall-through below
                // swings instead -- see this field's own header.
            }

            // Nothing safer to do -- hit back, aimed at whoever THREATENS
            // the party most among enemies something here can actually
            // DAMAGE, not whoever threatens most full stop. Locking onto
            // the most-threatening enemy before checking reach used to drop
            // Attack outright whenever that enemy sat outside melee range,
            // leaving nothing but a 0-damage Skill aimed at the same enemy
            // (fleece_ward's own ward branch above already had an identical
            // livelock, at seed 629, for the identical reason: a per-turn
            // pick with no memory of not converting). See
            // DamagingTargetSelection's own header for the full shape.
            var damaging = legal.Where(a =>
                a.Kind == FightActionKind.Attack ||
                a.Kind == FightActionKind.Skill).ToList();

            if (DamagingTargetSelection.TryChooseDamagingAction(
                    damaging,
                    a => EstimateDamage(session, actor, a),
                    targets => targets.OrderByDescending(t => ThreatOf(session, t)).First(),
                    out var best))
            {
                // Only reset the guard when the swing actually reduced
                // total enemy HP since this actor was last asked -- see
                // _enemyHpAtLastCheck's header. An attempted swing that
                // did not land is not "progress" for livelock-guard
                // purposes even though it is still this turn's chosen
                // action.
                if (madeRealProgressSinceLastAsk)
                {
                    _repeatGuard.RecordProgress(actor);
                }

                return best;
            }

            // Nothing in `legal` can put a positive number on anyone right
            // now -- every damaging candidate whiffs and only non-damaging
            // Skills remain (fleece_ward included, once its own guard above
            // has already capped it for this turn). Same guard, generalised
            // past "ward" specifically: any repeated 0-damage Skill pick is
            // tracked under its own skill id, so alternating between two
            // different non-progressing skills does not let either dodge the
            // cap by hiding behind the other's count.
            var nonDamagingSkill = legal.FirstOrDefault(a =>
                a.Kind == FightActionKind.Skill && EstimateDamage(session, actor, a) == 0);

            if (nonDamagingSkill.Kind == FightActionKind.Skill)
            {
                string skillId = SkillIdFor(session, actor, nonDamagingSkill);
                if (_repeatGuard.MayChoose(actor, skillId, NonDamagingSkillGuard.DefaultMaxConsecutive))
                {
                    _repeatGuard.RecordChosen(actor, skillId);
                    return nonDamagingSkill;
                }

                // Guard tripped: treated as unavailable this turn. An Attack
                // would already have been picked above (its Max(1, ...)
                // floor always keeps its own target eligible), so this is
                // reachable only when no Attack is legal at all.
                // FirstOrDefault + a Kind check is not safe here on its own --
                // FightActionKind.Attack is enum value 0, the same as
                // default(FightAction).Kind, so an empty match would read as
                // a false "yes, found one". Any() first.
                if (legal.Any(a => a.Kind == FightActionKind.Attack))
                {
                    return legal.First(a => a.Kind == FightActionKind.Attack);
                }
            }

            // NOT Move, deliberately, even under DesperateHealthFraction.
            // Stepping a wounded character behind a healthier one is a real
            // defensive play and this archetype does not make it: that is
            // ProtectTheFrontPolicy's whole job, and having two archetypes
            // that both do it would leave nothing measuring what the greedy
            // baseline costs. Reached only when there is truly nothing left
            // to spend the turn on that does anything at all.
            return FightAction.LastResort(legal);
        }

        // The authored skill id behind one legal Skill action -- the key
        // NonDamagingSkillGuard tracks repeats under, read the same way
        // FirstSkillWithEffect below already reads a skill's own SkillEffect
        // off SkillOptionsFor rather than guessing from SkillIndex alone.
        private static string SkillIdFor(FightSession session, CombatantState actor, FightAction action)
        {
            var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
            return option.Skill.Id;
        }

        // How much this enemy is about to hurt the party, read off its own
        // committed intent (EnemyIntent.ExpectedDamage, prepared for every
        // living enemy whenever it becomes the player's turn -- see
        // FightSession.Begin/PrepareEnemyIntents). Scoped to intents that
        // actually land on the party (One/AllOpponents); a Self or
        // AllAllies intent (a buff, a heal, a summon) threatens nobody and
        // reads as zero here even though the enemy could still out-damage
        // others on a future turn. Falls back to the enemy's raw Attack stat
        // when no intent has been committed yet (a session played directly
        // against FightAction.LegalActions without going through
        // FightRunner/Begin, as some of this file's own tests do).
        private static int ThreatOf(FightSession session, CombatantState enemy)
        {
            var intent = session?.IntentDetailFor(enemy);
            if (intent.HasValue)
            {
                var scope = intent.Value.Scope;
                if (scope == EnemyIntentScope.One || scope == EnemyIntentScope.AllOpponents)
                {
                    return intent.Value.ExpectedDamage;
                }

                return 0;
            }

            return enemy?.Attack ?? 0;
        }

        // Same pre-mitigation reading GreedyAggressivePolicy.EstimateDamage
        // uses, and for the same reason -- see that method's own header.
        private static int EstimateDamage(FightSession session, CombatantState actor, FightAction action)
        {
            switch (action.Kind)
            {
                case FightActionKind.Attack:
                    return CombatMath.ComputeAttackDamage(actor, action.Target);
                case FightActionKind.Skill:
                    var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
                    return FightAction.PreviewDamage(session, actor, option, action);
                default:
                    return 0;
            }
        }

        // The first legal Skill action whose authored SkillEffect is one of
        // `effects` -- shared by the heal and ward lookups above so both
        // read "is this skill the thing I am looking for" off the same
        // ResolvedSkillOption source SkillOptionsFor already hands the
        // legality check.
        private static FightAction? FirstSkillWithEffect(
            FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, params SkillEffect[] effects)
        {
            foreach (var candidate in legal)
            {
                if (candidate.Kind != FightActionKind.Skill) continue;

                var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == candidate.SkillIndex);
                if (effects.Contains(option.Skill.Effect))
                {
                    return candidate;
                }
            }

            return null;
        }

        public DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng)
        {
            if (choices.Count == 1) return choices[0];

            // Rest is checked BEFORE treasure and at a higher HP threshold
            // than GreedyAggressive's (75% here vs 50%) -- this archetype
            // would rather top off early than carry a scar into the next
            // fight, per the plan's map rule for GreedyDefensive.
            if (view.PartyHpFraction < RestBelowPartyHpFraction)
            {
                var rest = choices.FirstOrDefault(n => n.Type == RoomType.Rest);
                if (rest != null) return rest;
            }

            var treasure = choices.FirstOrDefault(n => n.Type == RoomType.Treasure);
            if (treasure != null) return treasure;

            var fight = choices.FirstOrDefault(n => n.Type == RoomType.Fight);
            if (fight != null) return fight;

            // Boss is taken over Elite when both are on offer and neither
            // Fight/Rest/Treasure is: a boss room is the one every route
            // through this floor eventually requires, while an elite is
            // optional extra risk this archetype has no reason to seek out.
            var boss = choices.FirstOrDefault(n => n.Type == RoomType.Boss);
            if (boss != null) return boss;

            var elite = choices.FirstOrDefault(n => n.Type == RoomType.EliteFight);
            if (elite != null) return elite;

            return choices[0];
        }

        // Slot/health-bearing identification off an ItemOffer -- NOT
        // possible from Domain. ItemOffer (Domain/Rewards/ItemOffer.cs)
        // carries only ItemId, Tier, Plus, RiftTier and a list of modifier
        // ids; it names no EquipmentSlot and no stat block, both of which
        // exist only after ContentDatabase (Core) resolves the id, which
        // Domain is not allowed to reach (noEngineReferences, and the
        // Resources-backed lookup is explicitly a Core/Editor concern -- see
        // the plan's F1). A modifier id ("hardened", "bulwark", ...) is a
        // rollable affix name with no textual tell for "defensive" either --
        // resolving it needs Domain/Content/ModifierEffect data the same way.
        // So this reads exactly as "the option carries no effect type" in
        // the brief's own words, and falls back to GreedyAggressivePolicy's
        // read of "best": highest tier, then highest plus, ties by rng.
        // The non-identifiability above is still true OF THE OFFER, and is
        // exactly why the score now arrives on the view instead: Core's
        // GearEvaluator resolves the id through ContentDatabase, prices the
        // swap with this archetype's own defensive weights, and hands back one
        // number per offer. The tier/plus read below is the fallback for when
        // nothing scored them.
        public int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng)
        {
            var scores = view.OfferScores;
            if (scores != null && scores.Count == offers.Count && offers.Count > 0)
            {
                return BestIndexTiedByRng(offers.Count, i => scores[i], rng);
            }

            return BestIndexTiedByRng(offers.Count,
                i => (offers[i].Tier, offers[i].Plus),
                rng);
        }

        // HEALTH AND DEFENCE HEAVY -- see GearWeights.Defensive for the ratio
        // and why offence is not zeroed.
        public GearWeights Gear => GearWeights.Defensive;

        public int ChooseStat(IReadOnlyList<StatOption> options, RunView view, SeededRandom rng)
        {
            if (options.Count == 0) return -1;
            var weights = Gear;
            return BestIndexTiedByRng(options.Count, i => weights.Score(options[i].Deltas), rng);
        }

        public int ChooseTalent(IReadOnlyList<TalentOption> options, RunView view, SeededRandom rng)
        {
            if (options.Count == 0) return -1;
            var weights = Gear;
            return BestIndexTiedByRng(options.Count, i => weights.Score(options[i].Deltas), rng);
        }

        // Same non-identifiability as ChooseOffer: RelicOption
        // (Domain/Relics/RelicPool.cs) carries only Id, Rarity and
        // UnlockedBy -- no RelicEffect, which lives on the Core-resolved
        // content and is exactly the "effect type" the brief says to key
        // off when it is there to read. It is not, so this falls back to
        // GreedyAggressivePolicy's pool-order read: rarest by
        // RelicPool.WeightOf, ties by rng.
        // CLEAR THE BAG FIRST, THEN BUY. The one archetype that sells.
        //
        // "Junk" is a bag row that scores at or below zero on this
        // archetype's own defensive weights, or a second stack of something
        // it already carries -- the two readings of "this will never go on
        // anybody" a bag can actually support. CONSUMABLES ARE NEVER JUNK,
        // deliberately: a potion scores zero because it is not equippable,
        // not because it is worthless, and this is the archetype that drinks
        // them. Selling the party's healing to buy armour is the exact
        // trade it exists not to make.
        //
        // One copy per call rather than the whole stack, because a sale
        // renumbers the bag (InventoryOps.TryRemoveAt drops an emptied
        // stack out of the list) and the driver rebuilds the view between
        // calls -- so the next call sees the bag as it now is rather than
        // acting on indices that moved.
        //
        // Buying is GreedyAggressive's rule with this archetype's weights
        // underneath it: same evaluator, same "positive or not at all", and
        // the armour-and-health preference lives in GearWeights.Defensive
        // rather than in a second ranking here. No reroll: paying to see a
        // different shelf is a gamble, and this archetype does not gamble.
        public ShopChoice ChooseShop(ShopView shop, RunView view, SeededRandom rng)
        {
            foreach (var row in shop.Bag)
            {
                if (row.Consumable || row.SellPrice <= 0) continue;
                if (row.Score > 0f && !row.Duplicate) continue;

                return ShopChoice.Sell(row.BagIndex, 1);
            }

            // Cheapest affordable book first, same reasoning
            // GreedyAggressive's own ChooseShop gives for going first: a
            // spell has no gear-slot opportunity cost and no Score to rank
            // by, so price is the only signal and the cheap one preserves
            // gold for whatever the rest of the shelf turns out to hold.
            int cheapestBook = -1;
            int cheapestBookPrice = -1;

            foreach (var card in shop.Cards)
            {
                if (card.Kind != ShopEntryKind.Book || !card.Buyable) continue;
                if (cheapestBook >= 0 && card.Price >= cheapestBookPrice) continue;

                cheapestBookPrice = card.Price;
                cheapestBook = card.Index;
            }

            if (cheapestBook >= 0) return ShopChoice.BuyBook(cheapestBook);

            int bestGear = -1;
            float bestScore = 0f;

            foreach (var card in shop.Cards)
            {
                if (card.Kind != ShopEntryKind.Gear || !card.Buyable) continue;
                if (card.Score <= bestScore) continue;

                bestScore = card.Score;
                bestGear = card.Index;
            }

            if (bestGear >= 0) return ShopChoice.BuyGear(bestGear);

            // CHEAPEST affordable relic, not the rarest -- the one place this
            // archetype's shop rule differs from GreedyAggressive's on
            // purpose. A relic's rarity says how seldom it is drawn, not what
            // it does, so "rarest" is a bet; "cheapest" keeps the most gold
            // in the purse for the next shelf, which is the defensive read of
            // the same shelf.
            int cheapestRelic = -1;
            int cheapestPrice = 0;

            foreach (var card in shop.Cards)
            {
                if (card.Kind != ShopEntryKind.Relic || !card.Buyable) continue;
                if (cheapestRelic >= 0 && card.Price >= cheapestPrice) continue;

                cheapestPrice = card.Price;
                cheapestRelic = card.Index;
            }

            return cheapestRelic >= 0 ? ShopChoice.BuyRelic(cheapestRelic) : ShopChoice.Leave();
        }

        public SpellAssignmentChoice ChooseSpellAssignment(SpellAssignmentView view, RunView runView, SeededRandom rng) =>
            SpellAssignmentDefault.Choose(view);

        public int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng)
        {
            return BestIndexTiedByRng(offer.Count,
                i => -RelicPool.WeightOf(offer[i].Rarity),
                rng);
        }

        // Shared with GreedyAggressivePolicy's own copy -- see that type's
        // header on why this is duplicated rather than factored out (the
        // plan keeps each archetype file self-contained and Phase 6 was
        // asked not to reach back into Phase 2's file to add a dependency
        // between two otherwise-independent archetypes).
        private static int BestIndexTiedByRng<TKey>(int count, System.Func<int, TKey> keyOf, SeededRandom rng)
            where TKey : System.IComparable<TKey>
        {
            var tied = new List<int> { 0 };
            var bestKey = keyOf(0);

            for (int i = 1; i < count; i++)
            {
                var key = keyOf(i);
                int cmp = key.CompareTo(bestKey);
                if (cmp > 0)
                {
                    bestKey = key;
                    tied.Clear();
                    tied.Add(i);
                }
                else if (cmp == 0)
                {
                    tied.Add(i);
                }
            }

            return tied.Count == 1 ? tied[0] : tied[rng.NextInt(0, tied.Count)];
        }
    }
}
