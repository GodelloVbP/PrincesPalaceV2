using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // WHEN A RELIC GETS TO ACT, in one place.
    //
    // Every relic effect in the game reaches into combat at exactly one moment,
    // and until this file existed each one found its own: Dual Wield at line 186
    // of FightSession, Magical Shield at 565 of Skills, Bloodlust at 75 of
    // Riders, and the two counting relics at two more. Five effects, five
    // hand-picked lines across four files, and no list anywhere of what the
    // available moments even were.
    //
    // That is the actual cost of a relic. The mechanic is rarely hard -- "gain a
    // shield", "swing twice", "take another turn" are a few lines each -- and
    // the expensive part is reading a large combat flow to work out where it
    // hooks, and being sure the place chosen fires exactly once, after the thing
    // it reacts to, and on every path that reaches it.
    //
    // THE MOMENTS ARE NOW NAMED AND THERE ARE FIVE. A new relic picks one and
    // adds a branch; it does not go looking.
    //
    //     BeforeSwing   -- the actor is about to make a plain attack
    //     AfterSwing    -- a plain attack has landed and its damage is known
    //     BeforeCast    -- the actor is about to resolve a spell or skill
    //     AfterCast     -- a cast has finished resolving
    //     OnKill        -- the actor's action killed something
    //
    // FIVE AND NOT SIX. An OnTurnStart was written here and deleted before it
    // shipped: nothing called it, and a moment nobody reacts to is a
    // maintenance burden pretending to be an abstraction. The list grows when a
    // relic needs it to, which costs one method and one call -- far less than
    // the confidence lost by a surface that is half real.
    //
    // WHAT THIS IS NOT is a data-driven relic language. RelicEffect's own header
    // makes that argument and it is right: "strike twice", "halve the next hit"
    // and "take another turn" have no shared shape, and a JSON grammar able to
    // express all three would be a programming language with worse tooling. The
    // WHEN is shared. The WHAT is not, and stays C#.
    //
    // Numeric relics do not appear here at all -- a relic that is a number
    // applied to a stat is authored entirely in relics.json through
    // RelicModifier, and always was.
    public partial class FightSession
    {
        // ---- the swing -----------------------------------------------------------

        private void RelicsBeforeSwing(CombatantState actor)
        {
            // The Long Count. Advances its tally and decides whether this swing
            // is the counted one; see FightSession.Potency for why the decision
            // is made here rather than where the damage is finally added up.
            BeginAttackPotency(actor);
        }

        // Damage is passed because a relic reacting to a swing almost always
        // wants to know how big it was -- and because reading it back off the
        // target afterwards would be wrong the moment anything else touches
        // their health in between.
        private void RelicsAfterSwing(CombatantState actor, CombatantState target, int damage)
        {
            EndPotency();

            // The Salt Ledger: a swing pays down what is owed.
            if (HasRelic(actor, RelicEffect.SaltLedger)
                && ReduceCooldowns(actor, FightTuning.SaltLedgerTurns) > 0)
            {
                AppendMessage($"{actor.Name}'s Salt Ledger settles a debt - cooldowns tick down.");
            }

            // Ballerina's Slippers: warming up, to a ceiling, for the rest of
            // the fight. Silent once the cap is reached rather than repeating
            // a line every swing about a bonus that is no longer growing.
            if (HasRelic(actor, RelicEffect.BallerinasSlippers))
            {
                int gained = GrantSpeedPercent(actor, RelicEffect.BallerinasSlippers,
                    FightTuning.SlippersPercentPerSwing, turns: 0,
                    capPercent: FightTuning.SlippersCapPercent);

                if (gained > 0) AppendMessage($"{actor.Name} finds their footing - faster.");
            }

            // Lucky Deck: one roll, every swing -- including Dual Wield's
            // second and Sword in a Box's bonus attack, both of which reach
            // here through this same call.
            RollLuckyDeck(actor, target, damage);

            // Magic Marker: an attack against a marked target consumes it.
            MagicMarkerConsumeOnAttack(actor, target);

            // Ice Fingernail: the swing stacks a slow on the target.
            IceFingernailOnAttack(actor, target);

            // Dancer's Anklet: an attack is one of the three moments that
            // earns an automatic reposition.
            DancersAnkletReposition(actor);
        }

        // ---- the cast ------------------------------------------------------------

        private void RelicsBeforeCast(CombatantState actor)
        {
            // The Charging Crystal, on the same terms as the Long Count above.
            BeginSpellPotency(actor);
        }

        // `skill` is not nullable -- the basic Skill action that used to
        // leave it null had no ResolvedSkill of its own (it was the global
        // spell tier, not a character's kit entry) and is gone
        // (docs/PLAN_SHOP.md §4 Phase E; see TryFirstRune's own comment
        // below for the same fact from the other side).
        private void RelicsAfterCast(CombatantState actor, ResolvedSkill skill, CombatantState target,
                                     int resourceSpent)
        {
            EndPotency();

            // Magical Shield: the next hit taken is halved.
            RaiseMagicalShield(actor);

            // Tin-Foil Pipe: quick until their next turn. Refreshes rather
            // than stacks -- see GrantSpeedPercent.
            if (HasRelic(actor, RelicEffect.TinFoilPipe)
                && GrantSpeedPercent(actor, RelicEffect.TinFoilPipe,
                    FightTuning.PipePercent, FightTuning.PipeTurns) > 0)
            {
                AppendMessage($"{actor.Name}'s pipe crackles - everything speeds up for a moment.");
            }

            // The First Rune, then Sword in a Box -- both are extra actions
            // AFTER everything else a cast can do, in the order a player would
            // read them: the spell repeats itself, and THEN steel follows.
            TryFirstRune(actor, skill, target, resourceSpent);
            TrySwordInABox(actor, target);

            // Rampaging Bull's Horn: fires only on a convergence (Transform)
            // cast -- see its own header.
            RampagingBullsHornOnConvergence(actor, skill);

            // Dancer's Anklet: a cast is the other of the three moments
            // that earns an automatic reposition.
            DancersAnkletReposition(actor);
        }

        // ---- sword in a box --------------------------------------------------------
        //
        // A free plain Attack, on the SAME target a cast just resolved on.
        // Played as its OWN beat, the identical pattern Dual Wield's second
        // swing already uses -- CommitBeat closes the cast's beat, a fresh
        // BeginBeat opens the attack's, and the CALLER's own trailing
        // CommitBeat (in CastSkill) finalises it. Reusing
        // ResolveAttackSwing means this attack counts toward the Long Count,
        // can trigger Lucky Deck, and can earn Bloodlust on a kill exactly as
        // a player-pressed Attack would -- it IS one, just not one the player
        // chose to press.
        //
        // GATED ON THE TARGET BEING AN OPPONENT, not merely alive. A self-cast
        // heal or a Ward on an ally would otherwise have the actor "attack"
        // their own side -- excluded by construction rather than by naming
        // every non-damaging effect.
        private void TrySwordInABox(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || !target.IsAlive) return;
            if (target.IsPlayerSide == actor.IsPlayerSide) return;
            if (!HasRelic(actor, RelicEffect.SwordInABox)) return;

            CommitBeat();
            BeginBeat(actor, target);
            SetStance(actor, Stances.Attack);
            ResolveAttackSwing(actor, target,
                $"{actor.Name}'s Sword in a Box springs open - {actor.Name} strikes {target.Name}");
        }

        // ---- the first rune ---------------------------------------------------------
        //
        // An identical free copy of the cast that just landed, on the same
        // target. SAME resourceSpent as the original -- "a copy" means the
        // same numbers, not a cheaper echo of them -- and the copy costs
        // nothing further: no mana, no cooldown started twice (BeginCooldown
        // already ran once, in the outer CastSkill, before this ever fires).
        //
        // CALLS ResolveCharacterSkillInner DIRECTLY, never the wrapper. It is
        // the half of the cast path that does not touch relics at all --
        // BeginSpellPotency/RelicsBeforeCast/RelicsAfterCast live only in
        // ResolveCharacterSkill, the outer method. Going straight to Inner is
        // what makes a second copy of the Charging Crystal's tally and a
        // second First Rune off the first's own copy both structurally
        // impossible, rather than guarded by a flag that could be forgotten.
        //
        // SCOPED TO A SINGLE DAMAGING TARGET. "A copy of that spell, on the
        // same target" reads cleanly for a DamageSingle cast, and reads as a
        // genuine question for a heal, a Ward, a Transform, or a sweep that
        // already hit everyone. Left out rather than guessed at.
        //
        // `skill` is not nullable: RelicsAfterCast's one remaining caller
        // (ResolveCharacterSkill) always has a real ResolvedSkill -- the
        // basic Skill action this null case used to mean is gone
        // (docs/PLAN_SHOP.md §4 Phase E).
        private void TryFirstRune(CombatantState actor, ResolvedSkill skill, CombatantState target,
                                  int resourceSpent)
        {
            if (actor == null || target == null || !target.IsAlive) return;
            if (!HasRelic(actor, RelicEffect.FirstRune)) return;
            if (skill.Effect != SkillEffect.DamageSingle) return;

            CommitBeat();
            BeginBeat(actor, target, isCast: true);
            AppendMessage($"{actor.Name}'s First Rune flares - the spell lands again!");

            ResolveCharacterSkillInner(actor, skill, target, resourceSpent);
        }

        // ---- the drowned lantern's mark ---------------------------------------------
        //
        // WHO IS MARKED, tracked by target alone rather than by (caster,
        // target) pair. Correct for a one-relic-holder roster -- only the
        // wearer's own spells can ever add to this set, and only the wearer's
        // own attacks are checked against it, so there is no third party who
        // could see or spend a mark that is not theirs. Revisit the moment a
        // second character can carry this relic at once: two wearers marking
        // the same enemy would then need to know whose mark it is.
        private readonly HashSet<CombatantState> _marked = new HashSet<CombatantState>();

        // Called from the two places a SPELL actually lands damage on a
        // specific target: the single-target skill and each hit of a sweep.
        // Not from RelicsAfterCast, because that fires once per ACTION and a
        // sweep's real target is a list, not the one CombatantState the beat
        // happens to be opened on.
        private void ApplyMark(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || !target.IsAlive) return;
            if (!HasRelic(actor, RelicEffect.DrownedLantern)) return;

            _marked.Add(target);
        }

        // Consumed by an ATTACK specifically -- see FightTuning.MarkBonusPercent
        // for why this lives beside PotencyBonus/NecklaceDamageBonus rather than
        // inside TotalDamage itself: TotalDamage is every path a hit can land
        // through, and this is deliberately only one of them.
        private int MarkBonus(CombatantState actor, CombatantState target, int baseAmount)
        {
            if (actor == null || target == null || baseAmount <= 0) return 0;
            if (!HasRelic(actor, RelicEffect.DrownedLantern)) return 0;
            if (!_marked.Remove(target)) return 0;

            int bonus = baseAmount * FightTuning.MarkBonusPercent / 100;
            if (bonus > 0) AppendMessage($"{target.Name}'s mark ignites - bonus damage!");
            return bonus;
        }

        public bool IsMarked(CombatantState target) => target != null && _marked.Contains(target);

        // ---- lucky deck ---------------------------------------------------------------
        //
        // ONE ROLL PER SWING. Hooked from RelicsAfterSwing, which already fires
        // once per ResolveAttackSwing call -- the same call Dual Wield's second
        // hit and Sword in a Box's bonus attack both go through -- so a double
        // attack gets two independent rolls for free, exactly as asked for,
        // with no extra wiring at either of those call sites.
        private void RollLuckyDeck(CombatantState actor, CombatantState target, int damage)
        {
            if (actor == null || target == null || damage <= 0) return;
            if (!HasRelic(actor, RelicEffect.LuckyDeck)) return;

            float roll = _rng?.NextFloat() ?? 0f;

            if (roll < 1f / 3f)
            {
                LuckyDeckHeal(actor);
            }
            else if (roll < 2f / 3f)
            {
                LuckyDeckSplash(actor, target, damage);
            }
            else
            {
                LuckyDeckSlow(actor, target);
            }
        }

        private void LuckyDeckHeal(CombatantState actor)
        {
            int health = actor.MaxHealth * FightTuning.LuckyDeckHealHealthPercent / 100;
            int mana = actor.MaxMana * FightTuning.LuckyDeckHealManaPercent / 100;

            if (health > 0) HealAndCount(actor, health);
            if (mana > 0) CombatMath.RestoreMana(actor, mana);

            AppendMessage($"{actor.Name}'s Lucky Deck turns up a red card - a moment to recover.");
        }

        // A FRACTION OF THE LANDED HIT, applied as raw damage rather than run
        // back through each victim's own defence and resistance. The blow
        // that splashes is the one that already paid its own armour tax; a
        // second full typed resolution per victim would be a different,
        // heavier attack wearing a light relic's name.
        //
        // DealDamage/SetStance/RecordKill is the fourth appearance of this
        // exact shape in the file family (the sweep, Shatter's chain, and
        // Transform splash all do the same three calls in the same order) --
        // worth collapsing the day a fifth shows up and actually causes a gap
        // the way the damage-bonus duplication did, not before.
        private void LuckyDeckSplash(CombatantState actor, CombatantState primary, int damage)
        {
            int splash = damage * FightTuning.LuckyDeckSplashPercent / 100;
            if (splash <= 0) return;

            bool hitAnyone = false;

            foreach (var other in _encounter.OpponentsOf(actor).ToList())
            {
                if (ReferenceEquals(other, primary) || !other.IsAlive) continue;

                DealDamage(actor, other, splash, AttackTypeOf(actor));
                SetStance(other, other.IsAlive ? Stances.Hurt : Stances.Defeated);
                hitAnyone = true;

                if (!other.IsAlive)
                {
                    _killedThisAction = true;
                    RecordKill(actor, other);
                }
            }

            if (hitAnyone)
            {
                AppendMessage($"{actor.Name}'s Lucky Deck turns up a black card - the blow splashes for {splash}!");
            }
        }

        // -30% SPEED FOR ONE TURN -- MIGRATED ONTO Chilled in Phase D2 (the
        // item-modifier plan). This used to read "Speed already has its own
        // machinery, so a StatusEffectType entry that ALSO tried to mean
        // 'slow' would be a second system claiming the same authority the
        // first already has" -- true right up until Chilled's own design
        // resolved exactly that worry: Chilled is NOT a second authority, it
        // is a status whose sole job is to drive the SAME GrantSpeedMalusPercent/
        // RevokeSpeedBuff bookkeeping this call already used, through
        // ApplyChilled (FightSession.SpeedBuffs.cs). Nothing about the
        // arithmetic changed -- same percent, same turns, same true-base
        // measurement, same exact reversal on expiry, all proven correct
        // before this migration ever touched it -- only WHERE the "how long"
        // and "how strong" now live: on a real ActiveStatus
        // (CombatantState.Statuses), visible and tickable through the same
        // machinery every other status already rides, rather than a private
        // number only this one relic's own code knew how to name. Frosty's
        // on-hit Chill (FightSession.ApplyModifierOnHitRiders) now lands the
        // identical status through the identical call.
        //
        // STILL SHOWN via SpeedBonusFrom -- see TagLineFor in
        // FightController.Hud.cs, which reads
        // SpeedBonusFrom(target, StatusEffectType.Chilled) < 0 to print
        // "SLOWED" on the enemy plate now that Chilled, not RelicEffect.LuckyDeck,
        // is the dictionary key the malus actually lands under.
        private void LuckyDeckSlow(CombatantState actor, CombatantState target)
        {
            int lost = ApplyChilled(target, FightTuning.LuckyDeckSlowPercent,
                FightTuning.LuckyDeckSlowTurns, actor);

            if (lost < 0)
            {
                AppendMessage($"{actor.Name}'s Lucky Deck turns up a pale card - {target.Name} slows!");
            }
        }

        // ---- seams for tests -------------------------------------------------
        //
        // Lucky Deck's branch is chosen by ONE RNG draw with nothing else to
        // key a test off. Hunting for a seed that happens to land in a given
        // third would make the test file secretly depend on SeededRandom's
        // exact algorithm, and silently rot the day that algorithm changes for
        // an unrelated reason. These call the three branches directly, so each
        // one's arithmetic is checkable without fighting the roll.
        public void LuckyDeckHealForTest(CombatantState actor) => LuckyDeckHeal(actor);

        public void LuckyDeckSplashForTest(CombatantState actor, CombatantState primary, int damage) =>
            LuckyDeckSplash(actor, primary, damage);

        public void LuckyDeckSlowForTest(CombatantState actor, CombatantState target) =>
            LuckyDeckSlow(actor, target);

        // ---- the one funnel every landed blow goes through -----------------------
        //
        // FOUR CALL SITES USED TO WRITE THIS EXPRESSION BY HAND: a plain
        // swing, a single-target skill, an all-enemies sweep, and the
        // Assassin's execute branch. All four read
        //
        //     outcome.Damage + PotencyBonus(baseAmount) + NecklaceDamageBonus(actor, baseAmount)
        //
        // which is not a coincidence, it is the same sentence copied four
        // times -- and copying it is exactly what let the sweep ship TWICE
        // without one of the two bonuses: the Charging Crystal's PotencyBonus
        // was wired into the single-target path and the plain swing and
        // forgotten on the sweep, and months later the necklace's
        // NecklaceDamageBonus would have repeated the identical gap if it had
        // been added the same way a second time.
        //
        // Every damage-modifying relic from here on adds its bonus INSIDE
        // this one function. A new one can be forgotten at a call site only if
        // every call site is rewritten to forget it, which is not a mistake
        // copy-paste makes by accident.
        //
        // `outcomeDamage` rather than `outcome.Damage` taken directly, because
        // the execute branch needs to stack these bonuses on top of a figure
        // ALREADY multiplied by the execute bonus, not on top of the pipeline's
        // raw output -- the two are different numbers on that one path.
        private int TotalDamage(CombatantState actor, int baseAmount, int outcomeDamage) =>
            outcomeDamage + PotencyBonus(baseAmount) + NecklaceDamageBonus(actor, baseAmount)
                          + RunWideDamageBonus(actor, outcomeDamage);

        // Cursed Idol's own bonus is deliberately NOT folded into
        // TotalDamage above, unlike every sibling bonus there -- TotalDamage
        // has no target-carrying overload the way DealDamage does, and the
        // stack lives on the TARGET, not the actor, so it is read at the one
        // call site that already has both (DealDamage, FightSession.
        // Ledger.cs) rather than threading a fifth parameter through every
        // TotalDamage call site for one relic.

        // Mechanic (d) / Amassing Star. PLAYER SIDE ONLY -- an enemy has no
        // run to accumulate against, and RunWideBonusDamagePercent is set
        // from the PARTY's own RunSnapshot. Applied to the finished
        // outcome figure rather than to baseAmount like Potency/Necklace
        // above, because it is a flat percent of "however hard this swing
        // already lands" -- there is no "own base" reading for a run-wide
        // multiplier the way there is for a per-swing counting relic.
        private int RunWideDamageBonus(CombatantState actor, int outcomeDamage)
        {
            if (actor == null || !actor.IsPlayerSide || outcomeDamage <= 0 || RunWideBonusDamagePercent <= 0)
            {
                return 0;
            }

            return outcomeDamage * RunWideBonusDamagePercent / 100;
        }

        // ---- the necklace --------------------------------------------------------
        //
        // NOT AN EVENT. Every other relic here reacts to something happening;
        // this one is a function of how hurt the wearer is right now, and it
        // has to be true continuously rather than at a moment.
        //
        // Which makes it two different problems wearing one name. The DAMAGE
        // half is asked at the instant a blow is computed and needs no state at
        // all -- a pure function of current health. The SPEED half cannot work
        // that way: speed is a stored number the turn order was told about, so
        // it has to be pushed when it changes rather than pulled when it is
        // read. Recomputed at turn start, which is the only moment the order
        // can act on it anyway.
        //
        // Full value at a quarter health, nothing at full, straight ramp
        // between. The floor is a QUARTER rather than zero because a bonus that
        // only pays at 1hp pays on the turn you die.
        private int NecklaceRampPercent(CombatantState actor, int maxPercent)
        {
            if (actor == null || actor.MaxHealth <= 0) return 0;
            if (!HasRelic(actor, RelicEffect.ToothedNecklace)) return 0;

            int healthPercent = actor.CurrentHealth * 100 / actor.MaxHealth;
            int floor = FightTuning.NecklaceFloorHealthPercent;

            if (healthPercent >= 100) return 0;
            if (healthPercent <= floor) return maxPercent;

            // How far down the ramp, from full health to the floor.
            int travelled = 100 - healthPercent;
            int total = 100 - floor;

            return maxPercent * travelled / total;
        }

        // The damage half, asked at the moment a blow is computed.
        private int NecklaceDamageBonus(CombatantState actor, int baseAmount)
        {
            if (baseAmount <= 0) return 0;

            int percent = NecklaceRampPercent(actor, FightTuning.NecklaceMaxDamagePercent);
            return percent <= 0 ? 0 : baseAmount * percent / 100;
        }

        // The speed half, pushed at turn start. Revoked and re-granted rather
        // than adjusted, because the ramp moves in both directions -- healing
        // has to give the speed back as surely as being hurt hands it over.
        private void RefreshNecklaceSpeed(CombatantState actor)
        {
            if (actor == null) return;

            int percent = NecklaceRampPercent(actor, FightTuning.NecklaceMaxSpeedPercent);
            int already = SpeedBonusFrom(actor, RelicEffect.ToothedNecklace);

            if (percent <= 0)
            {
                if (already > 0) RevokeSpeedBuff(actor, RelicEffect.ToothedNecklace);
                return;
            }

            RevokeSpeedBuff(actor, RelicEffect.ToothedNecklace);
            GrantSpeedPercent(actor, RelicEffect.ToothedNecklace, percent, turns: 0);
        }

        // ---- the kill ------------------------------------------------------------

        // WHAT A BODY IS WORTH, scaled by what it was.
        //
        // Measured off the enemy's authored expReward rather than a level,
        // because a CombatantState has no level -- experience is the number
        // content already uses to say how much a monster is worth, and a second
        // measure of the same thing would drift from it.
        //
        // Player-side only. Nothing pays a monster for killing you.
        private void PayBounty(CombatantState actor, CombatantState victim)
        {
            if (actor == null || victim == null || !actor.IsPlayerSide) return;
            if (!HasRelic(actor, RelicEffect.BountyHunterContract)) return;

            int worth = SourceFor(victim)?.Source.ExpReward ?? 0;
            int paid = worth * FightTuning.BountyPerLevel / 10;
            if (paid <= 0) paid = 1;

            BountyEarned += paid;
            AppendMessage($"{actor.Name} collects on {victim.Name} - {paid} gold.");
        }

        // Banked on the session and read out by whoever settles the fight, the
        // same way experience and currency already travel. A relic reaching for
        // the save directly is what put v1's relic logic in a controller.
        public int BountyEarned { get; private set; }

        // ---- the rest ------------------------------------------------------------

        // CALLED ON EVERY KILL, not only when a relic wants it. Bloodlust keeps
        // chain state that has to be RESET when the chain breaks, so a call
        // skipped because nobody was carrying the relic would leave a stale
        // count behind for whoever picks one up later in the fight.
        // ONCE PER ACTION, however many things that action killed.
        //
        // Bloodlust grants a turn, and a swing that fells two enemies should
        // not grant two. It also has to be called when NOTHING died -- see
        // below.
        private void RelicsOnKill(CombatantState actor)
        {
            TryGrantBloodlust(actor);
        }

        // ONCE PER BODY, which is a different moment and deliberately a second
        // method rather than a flag on the first.
        //
        // A bounty is paid for a corpse, so a splash that fells two pays twice.
        // Collapsing the two moments into one would force whichever relic was
        // written second to be wrong: Bloodlust would grant two turns, or the
        // Contract would pay for one of the two things it killed.
        private void RelicsOnEachKill(CombatantState actor, CombatantState victim)
        {
            PayBounty(actor, victim);
            AmassingStarOnKill(actor, victim);
            EssenceSiphonOnKill(actor, victim);
            InconspicuousKeyOnKill(actor, victim);
        }

    }
}
