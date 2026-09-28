using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // WHO DECIDES WHEN A TRANSFORM IS CAST (M8a of
    // docs/PLAN_EVENTS_BELL_AND_CARAVAN.md). No archetype scores a Transform:
    // it previews no damage, so GreedyAggressive and Lookahead2 rank it at 0
    // and a Black Ram build would sit in wool forever. WhenReady is the plain
    // player habit -- cast it the moment it is ready and not already worn, and
    // until then save the resource it costs -- and Never is the bot's
    // -NoTransform, which takes it off the menu.
    public enum TransformUse
    {
        // The policy's own choice, unfiltered: what every caller got before.
        PolicyDecides,
        WhenReady,
        Never,
    }

    // Plays one already-built FightSession to its end with one IFightPolicy,
    // one command at a time, exactly the way a player would: Begin(), then
    // while the player still has a turn, ask the policy, issue the command
    // through FightAction.Apply, check invariants, record a beat.
    public static class FightRunner
    {
        // A hard stop above FightInvariants.MaxPlayerCommands so a policy
        // that keeps re-offering a command the session silently refuses
        // (CastSkill returning false without spending the turn -- "Shatter
        // with nothing to detonate" and its like) cannot spin forever: the
        // TooManyCommands hit fires first and this loop exits on the same
        // pass, but the cap exists independently so a session with that
        // check somehow bypassed still cannot hang the batch.
        public const int HardCommandCap = FightInvariants.MaxPlayerCommands + 50;

        // HOW LONG A FIGHT MAY GO NOWHERE BEFORE IT COUNTS AS STUCK.
        //
        // "Nowhere" is neither side's total health FALLING FROM ONE COMMAND TO
        // THE NEXT for this many commands running. That is the honest reading
        // of "this fight is not going to end", and the one a command count
        // cannot give -- see FightInvariants.MaxPlayerCommands for the batch
        // that proved it.
        //
        // STEP TO STEP, NOT AGAINST A WATERMARK, and the difference cost a
        // whole batch to learn. The first version asked whether either side
        // had reached a NEW LOW, which reads as the same question and is not:
        // an enemy that HEALS early sets its low before the heal, and every
        // command of a long, steadily winning grind afterwards is measured
        // against a floor the fight can no longer touch. Seed 2, Mid/
        // GreedyDefensive, the floor-4 boss: the Forest Warden healed 1112 ->
        // 1853 in the opening rounds, then fell to 1557 over the next fifty
        // commands -- progress on every reading except "a new low", which is
        // the one that fired. 1,795 rows of it.
        //
        // The cost of the step-to-step reading is a real false NEGATIVE: a
        // fight where the enemy fully heals what the party chips off each
        // round progresses every command and stalls forever. That is a worse
        // bug and a rarer one, and the hard ceiling still catches it -- which
        // is the right way round, because a false positive silently truncates
        // a run the party was winning into a death and corrupts the depth
        // median the whole report is built on.
        //
        // Sixty rather than something tighter because a real fight can spend
        // a long stretch making no numerical progress on purpose -- warding,
        // buffing, rebuilding a resource before spending it -- and a stall
        // detector that fires on a legitimate wind-up is a detector nobody
        // reads.
        public const int StallCommands = 60;

        // `onItemUsed` is the SAVE-SIDE half of drinking a potion, handed in
        // rather than done here: the local satchel below is a copy, and Domain
        // has no save to spend the real stack from. Core's driver passes
        // RunOrchestrator.SpendConsumable, which is the same InventoryOps call
        // FightBootstrap.OnItemUsed makes for the screen. Optional, because a
        // caller playing a one-off fight with no run behind it (a test) has
        // nothing to spend from. Handed the stack's whole ItemInstance, so the
        // save spends the copy the policy picked -- a fake is never spent in
        // place of a genuine one, nor the other way round.
        public static List<InvariantHit> Play(
            FightSession session, IFightPolicy policy, IReadOnlyList<SatchelStack> satchel,
            SeededRandom rng, FightTrace traceOut, Action<ItemInstance> onItemUsed = null,
            TransformUse transformUse = TransformUse.PolicyDecides)
        {
            var hits = new List<InvariantHit>();
            if (session == null || policy == null || rng == null) return hits;

            session.Begin();

            // THE FORMATION AS THE FIGHT OPENED, for PartyOrderIntact. Taken
            // after Begin() because that is when the roster is settled and
            // before any player command can have reordered it.
            var startingPartyOrder = session.Encounter.PlayerParty.ToList();

            // A LOCAL copy, decremented as items are used. The real
            // save-side satchel decrement is Core's job, reached through
            // `onItemUsed` above; this exists so a policy asked twice in the
            // same fight sees an accurate count and cannot "use" more
            // potions than the satchel actually holds.
            var localSatchel = satchel?
                .Select(s => s.WithCount(s.Count))
                .ToList() ?? new List<SatchelStack>();

            int commands = 0;

            // The previous command's readings, which the stall check compares
            // each new pair against. int.MaxValue so the first command of any
            // fight counts as progress and the counter starts from a real
            // reading rather than from zero.
            int previousEnemyHp = int.MaxValue;
            int previousPartyHp = int.MaxValue;
            int commandsSinceProgress = 0;

            while (!session.IsOver && session.IsPlayerTurn && commands < HardCommandCap)
            {
                var actor = session.Current;
                var legal = FightAction.LegalActions(session, actor, localSatchel);

                if (legal.Count == 0)
                {
                    hits.Add(new InvariantHit("NoLegalAction",
                        $"{actor?.Name ?? "(null)"} has no legal action at command {commands}"));
                    break;
                }

                legal = TransformsOffered(session, actor, legal, transformUse, out FightAction? transformNow);
                var action = transformNow ?? policy.Choose(session, actor, legal, rng);
                string label = TraceLabel(session, actor, action);
                FightAction.Apply(session, action);
                commands++;

                if (action.Kind == FightActionKind.Item)
                {
                    var used = action.ItemInstance ?? new ItemInstance(action.ItemId);
                    DecrementSatchel(localSatchel, used);
                    onItemUsed?.Invoke(used);
                }

                // THE TIER THIS COMMAND'S OWN CAST FIRED, if any -- drained
                // off the session rather than read from live state, because
                // by the time a command returns the pool it spent from has
                // already moved (FightSession.Beats.RecordPoolTier's own
                // header). One command can open several beats (the actor's
                // own, then every enemy reply in the same exchange, per F6),
                // so this is the LARGEST PoolTierDamageMultiplier among the
                // beats whose Actor is THIS command's actor -- there is only
                // ever one, but Max rather than First costs nothing and
                // cannot silently pick an enemy's beat if the reference
                // equality check were ever loosened.
                //
                // ALWAYS DRAINED, whether or not a trace is being kept, so
                // the beat list this command opened never sits on the
                // session past the command that produced it -- nothing
                // downstream of the bot ever reads it (no FightController is
                // attached to a headless session), so an undrained list
                // would only ever grow for the rest of the fight.
                float poolTierFired = 0f;
                foreach (var beat in session.DrainBeats())
                {
                    if (beat != null && ReferenceEquals(beat.Actor, actor)
                        && beat.PoolTierDamageMultiplier > poolTierFired)
                    {
                        poolTierFired = beat.PoolTierDamageMultiplier;
                    }
                }

                if (traceOut != null)
                {
                    traceOut.TurnTraces.Add(new TurnTrace
                    {
                        ActorId = actor?.Name ?? "",
                        Action = label,
                        TargetId = action.Target?.Name ?? "",
                        PartyHpAfter = session.Encounter.LivingPlayerParty.Sum(c => c.CurrentHealth),
                        EnemyHpAfter = session.Encounter.LivingEnemies.Sum(c => c.CurrentHealth),
                        PoolTierFired = poolTierFired,
                        PrimaryPoolAfter = actor?.CurrentMana ?? 0,
                    });
                }

                if (traceOut != null)
                {
                    foreach (var member in session.Encounter.PlayerParty)
                    {
                        if (member?.Transformation != null && !traceOut.TransformedActors.Contains(member.Name))
                        {
                            traceOut.TransformedActors.Add(member.Name);
                        }
                    }
                }

                int enemyHp = session.Encounter.LivingEnemies.Sum(c => c.CurrentHealth);
                int partyHp = session.Encounter.LivingPlayerParty.Sum(c => c.CurrentHealth);

                // EITHER side losing health is progress -- a slow win drives
                // the enemy total down, a slow loss drives the party's, and
                // both are fights that are going to end.
                bool moved = enemyHp < previousEnemyHp || partyHp < previousPartyHp;
                commandsSinceProgress = moved ? 0 : commandsSinceProgress + 1;

                previousEnemyHp = enemyHp;
                previousPartyHp = partyHp;

                var turnHits = FightInvariants.Check(session, commands, startingPartyOrder);
                hits.AddRange(turnHits);

                if (turnHits.Any(h => h.Name == "TooManyCommands"))
                {
                    break;
                }

                // THE STALL, reported under the plan's own §3 name because it
                // is the plan's own question -- "a fight that is not over" --
                // asked the way that survives a difficulty curve. See
                // StallCommands above.
                if (commandsSinceProgress >= StallCommands)
                {
                    hits.Add(new InvariantHit("TooManyCommands",
                        $"neither side lost any health for {StallCommands} commands running " +
                        $"(command {commands}, enemies at {enemyHp}, party at {partyHp}); " +
                        "the fight is not going to end"));
                    break;
                }

                hits.AddRange(RescueAStalledEnemyTurn(session, commands));
            }

            if (traceOut != null) traceOut.Turns = commands;

            return hits;
        }

        // THE SAME REPAIR FightController.RescueAStalledEnemyTurn MAKES, and
        // for the same reason -- reported as a bug rather than swallowed.
        //
        // A command is supposed to resolve the whole exchange and hand the turn
        // back (plan F6). It does not always: a fight that spends a second-life
        // charge can come back sitting on an enemy turn nothing resolves. On
        // screen that is a frozen stage with every verb dead, and Update()
        // catches it a frame later. Headless there is no Update, so the loop
        // above would simply fall out of its `IsPlayerTurn` condition and leave
        // a fight that is neither won nor lost -- the run driver would then see
        // an un-settled session, and a whole batch's depth numbers would be
        // measuring a hang rather than the game.
        //
        // So it is repaired identically (resolve the enemy turns that are
        // owed, then re-telegraph if the turn came back) AND recorded, every
        // time, as a StalledEnemyTurn hit. Recording it per occurrence rather
        // than once per fight is deliberate: the report's job is to say how
        // often the live game does this, and a deduplicated count would read as
        // rarer than it is. FightSettlementTests calls this "a pre-existing
        // production fault these tests trip, not one they cause" and leaves it
        // alone; this is the thing that will finally count it.
        // The legal list with TransformUse applied, and the one command to
        // play instead of asking the policy (WhenReady with a Transform ready
        // and none worn). Never takes every Transform out; it never empties
        // the list, because Attack (or an already-skipped turn) carries that
        // guarantee and a Transform is never the only command.
        private static IReadOnlyList<FightAction> TransformsOffered(
            FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal,
            TransformUse use, out FightAction? transformNow)
        {
            transformNow = null;
            if (use == TransformUse.PolicyDecides || actor == null) return legal;

            if (use == TransformUse.Never)
            {
                var rest = legal.Where(a => !IsTransform(session, actor, a)).ToList();
                return rest.Count > 0 ? rest : legal;
            }

            // WhenReady from here. Known at all, ready or not: a Transform
            // on cooldown or short of its resource still shapes the turn.
            if (actor.Transformation != null) return legal;
            bool knowsATransform = session.SkillOptionsFor(actor)
                .Any(o => o.Skill != null && o.Skill.Effect == SkillEffect.Transform);
            if (!knowsATransform) return legal;

            var ready = legal.FirstOrDefault(a => IsTransform(session, actor, a));
            if (ready.Kind == FightActionKind.Skill && session.Encounter.LivingEnemies.Any())
            {
                transformNow = ready;
                return legal;
            }

            // BANK THE RESOURCE for it: every other skill priced in the
            // signature resource (Shawn's wool) is held back until the form is
            // worn. Without this the greedy archetypes spend each 3 wool on
            // Shear and a Black Ram build reaches its 7 in about one fight in
            // ten -- a build a player would never play that way.
            var banked = legal.Where(a => !SpendsSignatureResource(session, actor, a)).ToList();
            return banked.Count > 0 ? banked : legal;
        }

        private static bool SpendsSignatureResource(FightSession session, CombatantState actor, FightAction action)
        {
            if (action.Kind != FightActionKind.Skill) return false;
            var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
            return option.Skill != null && option.Skill.ResourceCost > 0;
        }

        private static bool IsTransform(FightSession session, CombatantState actor, FightAction action)
        {
            if (action.Kind != FightActionKind.Skill) return false;
            var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
            return option.Skill != null && option.Skill.Effect == SkillEffect.Transform;
        }

        private static List<InvariantHit> RescueAStalledEnemyTurn(FightSession session, int commands)
        {
            var hits = new List<InvariantHit>();
            if (session.IsOver || session.IsPlayerTurn) return hits;

            hits.Add(new InvariantHit("StalledEnemyTurn",
                $"after command {commands} the fight is not over and it is not the player's turn; " +
                "resolving the owed enemy turns the way FightController.Update does"));

            session.AutoResolveEnemyTurns();
            if (!session.IsOver && session.IsPlayerTurn) session.PrepareEnemyIntents();

            return hits;
        }

        // The trace's own name for an action, which is NOT FightAction.ToString.
        //
        // docs/BOT_SUMMARY_SCHEMA.md pins TurnTrace.Action to four shapes --
        // "Attack" / "Skill:<name>" / "Item:<name>" / "Move" -- and the
        // summary reads them: consumableUseShare counts entries starting
        // "Item:", skillsNeverUsed collects what follows "Skill:". ToString's
        // debug form ("Skill[0](Rat)") carries the INDEX, which is per-actor
        // and means nothing across a batch, and folds the target into the same
        // string that a coverage check has to key on. Ids rather than display
        // names on both, so those coverage lists can be differenced against
        // ContentDatabase directly.
        //
        private static string TraceLabel(FightSession session, CombatantState actor, FightAction action)
        {
            switch (action.Kind)
            {
                case FightActionKind.Attack:
                    return "Attack";
                case FightActionKind.Skill:
                    var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
                    return "Skill:" + (option.Skill.Id ?? "");
                case FightActionKind.Item:
                    return "Item:" + action.ItemId;
                default:
                    return "Move";
            }
        }

        // The stack that was used, by the one merge predicate -- never merely
        // the first stack sharing its id.
        private static void DecrementSatchel(List<SatchelStack> satchel, ItemInstance used)
        {
            for (int i = 0; i < satchel.Count; i++)
            {
                if (!ItemInstance.SameStack(satchel[i].Instance, used)) continue;

                satchel[i] = satchel[i].WithCount(System.Math.Max(0, satchel[i].Count - 1));
                return;
            }
        }
    }
}
