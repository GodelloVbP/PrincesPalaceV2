using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
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
        // nothing to spend from.
        public static List<InvariantHit> Play(
            FightSession session, IFightPolicy policy, IReadOnlyList<SatchelStack> satchel,
            SeededRandom rng, FightTrace traceOut, Action<string> onItemUsed = null)
        {
            var hits = new List<InvariantHit>();
            if (session == null || policy == null || rng == null) return hits;

            session.Begin();

            // A LOCAL copy, decremented as items are used. The real
            // save-side satchel decrement is Core's job, reached through
            // `onItemUsed` above; this exists so a policy asked twice in the
            // same fight sees an accurate count and cannot "use" more
            // potions than the satchel actually holds.
            var localSatchel = satchel?
                .Select(s => new SatchelStack(s.ItemId, s.DisplayName, s.Count, s.RestoresMana))
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

                var action = policy.Choose(session, actor, legal, rng);
                string label = TraceLabel(session, actor, action);
                FightAction.Apply(session, action);
                commands++;

                if (action.Kind == FightActionKind.Item)
                {
                    DecrementSatchel(localSatchel, action.ItemId);
                    onItemUsed?.Invoke(action.ItemId);
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
                    });
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

                var turnHits = FightInvariants.Check(session, commands);
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
        // "Attack" / "Skill:<name>" / "Item:<name>" / "HoldBack" -- and the
        // summary reads them: consumableUseShare counts entries starting
        // "Item:", skillsNeverUsed collects what follows "Skill:". ToString's
        // debug form ("Skill[0](Rat)") carries the INDEX, which is per-actor
        // and means nothing across a batch, and folds the target into the same
        // string that a coverage check has to key on. Ids rather than display
        // names on both, so those coverage lists can be differenced against
        // ContentDatabase directly.
        //
        // The basic spell is labelled as a skill by its display name, because a
        // ResolvedSpellTier has no id of its own -- so it appears in the
        // batch's skill usage without ever matching a skills.json id, which is
        // the honest answer rather than inventing one.
        private static string TraceLabel(FightSession session, CombatantState actor, FightAction action)
        {
            switch (action.Kind)
            {
                case FightActionKind.Attack:
                    return "Attack";
                case FightActionKind.BasicSpell:
                    return "Skill:" + session.BasicSpellNameFor(actor);
                case FightActionKind.Skill:
                    var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
                    return "Skill:" + (option.Skill.Id ?? "");
                case FightActionKind.Item:
                    return "Item:" + action.ItemId;
                default:
                    return "HoldBack";
            }
        }

        private static void DecrementSatchel(List<SatchelStack> satchel, string itemId)
        {
            for (int i = 0; i < satchel.Count; i++)
            {
                if (satchel[i].ItemId != itemId) continue;

                var stack = satchel[i];
                satchel[i] = new SatchelStack(stack.ItemId, stack.DisplayName,
                    System.Math.Max(0, stack.Count - 1), stack.RestoresMana);
                return;
            }
        }
    }
}
