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
    // One player action, one enemy reply, played out on paper rather than on
    // the session. For every legal action this scores the expected party HP
    // swing -- this action's own effect, plus a large bonus if it kills its
    // target, minus what the enemies still standing afterwards are expected
    // to do back -- and takes the best score, ties broken by `rng`.
    //
    // ESTIMATED, NOT SIMULATED. FightSession/CombatEncounter/CombatantState
    // offer no clone or snapshot (checked: CombatantState's fields are plain
    // mutable ints with no copy constructor, CombatEncounter takes its lists
    // by reference in its constructor, FightSession has no Clone/Snapshot
    // member anywhere in its five partial files), and the plan is explicit
    // that Domain gets no such thing added for the bot's sake -- that would
    // be new production combat code. So instead of executing a hypothetical
    // turn, this reads the same pre-mitigation preview numbers
    // GreedyAggressivePolicy already trusts to RANK options
    // (CombatMath.ComputeAttackDamage, PreviewBasicSpellPower,
    // PreviewSkillPower) for "what this action does", and the enemies'
    // already-committed EnemyIntent.ExpectedDamage (FightSession.
    // IntentDetailFor, prepared for the player's whole turn by
    // PrepareEnemyIntents) for "what comes back" -- no variance roll, no
    // defence pass, no ward, no second-order effect of THIS action changing
    // an enemy's own next intent (an intent already committed this turn does
    // not re-roll just because the player who has not acted yet is being
    // asked to evaluate their options). Two lookahead turns deep by name,
    // one preview deep by construction.
    //
    // DETERMINISTIC given `rng`: every number this reads (CurrentHealth,
    // Attack, the preview queries, IntentDetailFor) is a plain read with no
    // side effect, and the only draw is the tie-break below.
    public sealed class Lookahead2Policy : IFightPolicy, IRunPolicy
    {
        // Large enough that a kill always outranks a chip-damage option
        // however big the chip number is at these HP scales (fixtures and
        // real content alike sit in the tens to low thousands), and far
        // below int overflow when several such bonuses sum on a
        // multi-target consideration.
        private const int KillBonus = 1_000_000;

        // The map/offer/relic brain is NOT re-specified here -- the plan
        // says to reuse GreedyAggressive's IRunPolicy rather than invent a
        // second map strategy for what is fundamentally still "play to
        // win". Composition, not inheritance: BotRunDriver's own registry
        // (Core/Bot/BotRunDriver.cs) wants ONE object per archetype
        // implementing both interfaces, so this type implements IRunPolicy
        // itself and forwards every call.
        private readonly GreedyAggressivePolicy _runBrain = new GreedyAggressivePolicy();

        // Backstop for the guard below -- see NonDamagingSkillGuard's own
        // header for why a per-turn score cannot be the whole fix, and this
        // policy's ScoreOf/isHeal comments for the primary fix (capping a
        // heal's own effect to what is actually missing). Independent of
        // Gift skills or DamageAll being individually tuned later: whatever
        // effect ScoreOf gives 0 of its own to (Provoke, Ward, Shatter,
        // BuffParty, Transform, the three Gifts, Summon) is, by definition,
        // never converting anything this policy can see -- so a memoryless
        // score keeps re-picking whichever of them currently beats
        // `-incoming`, forever, exactly the way GreedyDefensive's ward did
        // at seed 629 before it got the same kind of guard.
        private readonly NonDamagingSkillGuard _repeatGuard = new NonDamagingSkillGuard();

        public FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            var livingEnemies = session.Encounter.LivingEnemies.ToList();

            var tied = new List<FightAction> { legal[0] };
            long bestScore = ScoreOf(session, actor, legal[0], livingEnemies, _repeatGuard, out _);

            for (int i = 1; i < legal.Count; i++)
            {
                long score = ScoreOf(session, actor, legal[i], livingEnemies, _repeatGuard, out _);
                if (score > bestScore)
                {
                    bestScore = score;
                    tied.Clear();
                    tied.Add(legal[i]);
                }
                else if (score == bestScore)
                {
                    tied.Add(legal[i]);
                }
            }

            var chosen = tied.Count == 1 ? tied[0] : tied[rng.NextInt(0, tied.Count)];

            string chosenKey = NonProgressingSkillKey(session, actor, chosen);
            if (chosenKey != null)
            {
                _repeatGuard.RecordChosen(actor, chosenKey);
            }
            else
            {
                _repeatGuard.RecordProgress(actor);
            }

            return chosen;
        }

        // The skill id for every Skill EXCEPT a damaging one -- null only for
        // DamageSingle/DamageAll (and for every non-Skill action). A heal
        // used to be excluded here too, on the theory that ScoreOf's own cap
        // (missing HP/mana) already made it honest; it does not, when the
        // party is taking roughly as much chip damage as the heal restores
        // each turn -- "missing" never runs out, so a heal that is genuinely
        // converting HP every single cast can still win the pick forever and
        // enemy HP never moves (seeds across the 20260902-013421 batch: 183
        // rows, all woolgathering, all repeated past any plausible "still
        // buying something" count). Folding it into the same guard as the
        // effects ScoreOf cannot preview at all is the fix: real or not, a
        // repeat that never gets interrupted by an actual damaging pick is
        // exactly the shape the guard exists to catch.
        private static string NonProgressingSkillKey(FightSession session, CombatantState actor, FightAction action)
        {
            if (action.Kind != FightActionKind.Skill) return null;

            var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
            bool isDamage = option.Skill.Effect == SkillEffect.DamageSingle || option.Skill.Effect == SkillEffect.DamageAll;

            return isDamage ? null : option.Skill.Id;
        }

        // The expected party HP swing of taking `action` right now: this
        // action's own effect (damage dealt, or HP restored, weighted
        // 1-for-1 against the incoming term so both sides read in the same
        // unit) plus KillBonus if it drops its target, minus the enemies
        // still standing afterwards' combined expected damage next turn.
        private static long ScoreOf(
            FightSession session, CombatantState actor, FightAction action, List<CombatantState> livingEnemies,
            NonDamagingSkillGuard repeatGuard, out string nonProgressingKey)
        {
            long ownEffect = 0;
            bool killsTarget = false;
            nonProgressingKey = null;

            switch (action.Kind)
            {
                case FightActionKind.Attack:
                {
                    int dmg = CombatMath.ComputeAttackDamage(actor, action.Target);
                    ownEffect = dmg;
                    killsTarget = action.Target != null && dmg >= action.Target.CurrentHealth;
                    break;
                }
                case FightActionKind.BasicSpell:
                {
                    int dmg = session.PreviewBasicSpellPower(actor);
                    ownEffect = dmg;
                    killsTarget = action.Target != null && dmg >= action.Target.CurrentHealth;
                    break;
                }
                case FightActionKind.Skill:
                {
                    var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
                    var effect = option.Skill.Effect;

                    // Only the same effects PreviewSkillPower can actually
                    // answer without throwing -- see GreedyAggressivePolicy.
                    // EstimateDamage's header for the exhaustive reasoning.
                    // A skill outside this set (Provoke, Ward, Shatter,
                    // BuffParty, Transform, the three Gifts, Summon) scores
                    // its own effect as 0: this policy has no way to weigh
                    // "forces an attack redirect" or "wards the caster"
                    // against a raw HP swing, so it is honest about
                    // contributing nothing to THAT half of the score rather
                    // than guessing. It can still win the pick on the
                    // strength of the incoming-damage term alone (casting it
                    // costs a turn, same as everything else on the menu, and
                    // no action here removes that cost from the comparison).
                    bool isDamage = effect == SkillEffect.DamageSingle || effect == SkillEffect.DamageAll;
                    bool isHeal = effect == SkillEffect.HealSelf || effect == SkillEffect.HealParty
                                                                  || effect == SkillEffect.RestorePartyMana;

                    if (isDamage)
                    {
                        int dmg = session.PreviewSkillPower(actor, option.Skill);
                        ownEffect = dmg;
                        killsTarget = action.Target != null && dmg >= action.Target.CurrentHealth;
                    }
                    else if (isHeal)
                    {
                        // CAPPED TO WHAT IS ACTUALLY MISSING -- the root
                        // cause of the seed 18 (and 418 further rows across
                        // the batch this was found in) livelock: an
                        // uncapped heal scores its full preview amount even
                        // at or near full health, where it restores nothing
                        // real. woolgathering (flatAmount 40 + power 30 per
                        // point of Wool spent, so a flat ~160 whenever the
                        // caster is not already low on Wool) was consistently
                        // OUTSCORING a real attack whose pre-mitigation
                        // preview happened to sit below that flat number for
                        // this matchup, so it won every single turn and
                        // enemy HP never moved -- the trace shows party HP
                        // oscillating in a narrow high band the whole time,
                        // not sitting at a fixed low value, which is what
                        // said "the heal itself is fine, the SCORE is wrong"
                        // rather than "this actor is stuck needing to heal".
                        //
                        // The Item branch above already caps a health potion
                        // to MaxHealth-CurrentHealth for the identical
                        // reason; heal skills never got the same treatment.
                        // RestorePartyMana previews through the same
                        // SkillResolution.Amount case as the two HP heals
                        // (there is no way to tell "restores mana" from
                        // "restores HP" without re-deriving `effect`, which
                        // this already has) and is capped the same way, in
                        // its own unit -- missing PARTY mana rather than
                        // missing party HP, which also fixes the "wrong
                        // unit" quirk this branch used to warn about rather
                        // than merely documenting it.
                        int previewed = session.PreviewSkillPower(actor, option.Skill);
                        int missing;
                        if (effect == SkillEffect.RestorePartyMana)
                        {
                            missing = session.Encounter.LivingPlayerParty
                                .Sum(c => System.Math.Max(0, c.MaxMana - c.CurrentMana));
                        }
                        else if (effect == SkillEffect.HealParty)
                        {
                            missing = session.Encounter.LivingPlayerParty
                                .Sum(c => System.Math.Max(0, c.MaxHealth - c.CurrentHealth));
                        }
                        else
                        {
                            missing = actor != null ? System.Math.Max(0, actor.MaxHealth - actor.CurrentHealth) : 0;
                        }

                        ownEffect = System.Math.Min(previewed, missing);

                        // Same backstop as the effects below that ScoreOf
                        // cannot preview at all -- see NonProgressingSkillKey.
                        // The cap above already makes a heal near full health
                        // score honestly, but a heal that keeps restoring a
                        // REAL amount every turn because incoming chip damage
                        // refills "missing" just as fast is a genuine
                        // standoff, not overhealing, and the cap alone cannot
                        // see that: it only looks at THIS turn. Two picks
                        // running with no damaging action landing in between
                        // is the same "tried it, not converting" signal the
                        // else branch already acts on.
                        nonProgressingKey = option.Skill.Id;
                        if (repeatGuard != null &&
                            !repeatGuard.MayChoose(actor, nonProgressingKey, NonDamagingSkillGuard.DefaultMaxConsecutive))
                        {
                            ownEffect -= KillBonus;
                        }
                    }
                    else
                    {
                        // Backstop for whatever the comment above already
                        // scores at 0 -- see NonDamagingSkillGuard's own
                        // header. Flagging the key here (rather than only in
                        // NonProgressingSkillKey, which Choose() also calls)
                        // means the penalty below and the eventual
                        // RecordChosen/RecordProgress bookkeeping read off
                        // the exact same "is this effect one we cannot
                        // preview" test, so the two can never drift apart.
                        nonProgressingKey = option.Skill.Id;
                        if (repeatGuard != null &&
                            !repeatGuard.MayChoose(actor, nonProgressingKey, NonDamagingSkillGuard.DefaultMaxConsecutive))
                        {
                            // Large enough to outweigh any realistic
                            // ownEffect-incoming spread so this drops below
                            // every other legal option this turn -- but
                            // finite, so if it is the ONLY legal action
                            // (FightAction.LegalActions never returns empty;
                            // HoldBack alone would still beat this) it is
                            // still chosen rather than the loop having
                            // nothing to return.
                            ownEffect -= KillBonus;
                        }
                    }

                    break;
                }
                case FightActionKind.Item:
                {
                    // Tops the actor off (FightAction's own ItemAmountProxy
                    // comment) -- so the HP swing a healing item is worth is
                    // exactly the HP it was missing. A mana item moves no HP
                    // bar and scores 0 here, same treatment
                    // GreedyDefensivePolicy gives it.
                    if (!action.ItemRestoresMana && actor != null)
                    {
                        ownEffect = System.Math.Max(0, actor.MaxHealth - actor.CurrentHealth);
                    }

                    break;
                }
                default:
                    // HoldBack: no direct HP effect either way.
                    break;
            }

            long incoming = 0;
            foreach (var enemy in livingEnemies)
            {
                if (killsTarget && ReferenceEquals(enemy, action.Target)) continue;
                incoming += ThreatOf(session, enemy);
            }

            return ownEffect - incoming + (killsTarget ? KillBonus : 0);
        }

        // The enemy's own committed intent (EnemyIntent.ExpectedDamage),
        // which is prepared for every living enemy before the player's turn
        // begins (FightSession.Begin/PrepareEnemyIntents) and IS the "public
        // intent reader" the plan points at. Falls back to the enemy's raw
        // Attack when nothing has been committed yet (a session driven
        // directly against FightAction.LegalActions without FightRunner, as
        // this file's own tests and BotPolicyTests both do at least once).
        private static int ThreatOf(FightSession session, CombatantState enemy)
        {
            var intent = session?.IntentDetailFor(enemy);
            return intent.HasValue ? intent.Value.ExpectedDamage : (enemy?.Attack ?? 0);
        }

        public DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng) =>
            _runBrain.ChooseNode(choices, view, rng);

        public int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng) =>
            _runBrain.ChooseOffer(offers, view, rng);

        public int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng) =>
            _runBrain.ChooseRelic(offer, view, rng);

        // Gear, stat points and talents go the same way as the three above,
        // and for the identical reason: the plan says this archetype's edge is
        // its FIGHT lookahead, so anything else it did differently would
        // contaminate the one comparison it exists to make.
        public GearWeights Gear => _runBrain.Gear;

        public int ChooseStat(IReadOnlyList<StatOption> options, RunView view, SeededRandom rng) =>
            _runBrain.ChooseStat(options, view, rng);

        public int ChooseTalent(IReadOnlyList<TalentOption> options, RunView view, SeededRandom rng) =>
            _runBrain.ChooseTalent(options, view, rng);
    }
}
