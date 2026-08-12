using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // Starts a fight when the Fight scene opens.
    //
    // A PLACEHOLDER FOR THE RUN, and it says so rather than pretending
    // otherwise. Once a descent exists it will hand the room's roster over and
    // this picks nothing; until then, opening the scene has to produce a
    // playable fight or the screen cannot be looked at at all.
    //
    // The ids are read from content rather than hardcoded, so adding a monster
    // to enemies.json is enough to see it on the stage.
    public class FightBootstrap : MonoBehaviour
    {
        [SerializeField] internal FightController fight;

        // Fixed by default. A fight that reshuffles every time the scene
        // reloads cannot be compared against the last screenshot, and this
        // screen is verified by looking at it.
        [SerializeField] internal int seed = 20260810;

        // How many monsters to field. Three is the stage's own capacity
        // (FightHudSpec.StageSlotsPerSide) and shows every slot at once.
        [SerializeField] internal int enemyCount = 3;

        private void Start()
        {
            if (fight == null) fight = GetComponent<FightController>();
            if (fight == null || fight.HasSession) return;

            var built = BuildOpeningFight();
            if (built == null)
            {
                // No content: the scene still loads and the HUD still paints its
                // static half. Graceful degradation, house style -- an empty
                // stage beats a scene that throws on open.
                Debug.LogWarning("[FightBootstrap] No characters or enemies in content; the stage stays empty.");
                return;
            }

            // The room's depth rides the session, so the payout is scaled by
            // where the fight HAPPENED rather than by wherever the run has moved
            // to by the time it settles.
            built.Session.DepthStep = RunManager.HasRun ? RunManager.Run.step : 0;

            fight.Bind(built.Session, EncounterFor(RunManager.CurrentNode), BuildSatchel());
            fight.ItemUsed += OnItemUsed;
            fight.BindPartyArt(built.Party, built.PartyArt);

            // Banked the moment the last beat has PLAYED, not when the player
            // dismisses the screen.
            //
            // This used to wait for the dismissal, on the reasoning that
            // crediting a payout before the player has seen it makes the number
            // arrive from nowhere. The Reckoning inverts that: it is the thing
            // that shows them the number, and it cannot show what has not been
            // computed. The player still sees the credit and the screen at the
            // same instant -- only the internal ordering moved.
            fight.FightEnded += OnFightEnded;

            // Read straight AFTER OnFightEnded has run, which is what populates
            // it. A Func rather than the value, because at subscription time
            // the fight has not happened yet.
            fight.RewardSource = () => LastReward;
            fight.SettlementSource = () => LastSettlement;
        }

        internal FightEncounterAdapter.BuiltFight BuildOpeningFight()
        {
            // The first character WITH BATTLE ART, not simply the first. Most of
            // the roster has no sprite authored yet, and this bootstrap exists so
            // the screen can be looked at -- a party member who renders as a
            // fallback plate defeats its entire purpose.
            var party = ContentDatabase.Characters
                .Where(HasArt)
                .Take(1)
                .Select(c => c.id)
                .ToList();

            if (party.Count == 0)
            {
                party = ContentDatabase.Characters.Take(1).Select(c => c.id).ToList();
            }

            // Same rule for the monsters, and for the same reason: three of the
            // roster have sheets and the rest do not.
            //
            // Ordered by sortOrder ONLY as a tie-break. It is unset across the
            // whole of enemies.json today, so every value is 0 and this is really
            // Resources.LoadAll's incidental order -- which is exactly the trap
            // CLAUDE.md gotcha 4 names. Filtering on ART first is what makes the
            // selection stable regardless, because only three qualify.
            var enemies = ContentDatabase.Enemies
                .Where(HasArt)
                .OrderBy(e => e.sortOrder)
                .Take(enemyCount)
                .Select(e => e.id)
                .ToList();

            if (enemies.Count == 0)
            {
                enemies = ContentDatabase.Enemies.Take(enemyCount).Select(e => e.id).ToList();
            }

            if (party.Count == 0 || enemies.Count == 0) return null;

            // The RUN'S relics, so the thing the player drafted at the gate
            // actually reaches the fight. Without this the draft wrote to a
            // field nothing read.
            return FightEncounterAdapter.Build(party, enemies, new SeededRandom((ulong)seed),
                relicIds: RunManager.HasRun ? RunManager.Run.relicIds : null);
        }

        private static bool HasArt(CharacterDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.battleSpritePath);

        private static bool HasArt(EnemyDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.spritePath);
    
        // Which backdrop and which reward multiplier. Reads the ROOM, so an
        // elite room is elite everywhere at once rather than in each place that
        // happens to ask.
        private static EncounterClass EncounterFor(DescentNode node)
        {
            if (node == null) return EncounterClass.Normal;

            switch (node.Type)
            {
                case RoomType.Boss: return EncounterClass.Boss;
                case RoomType.EliteFight: return EncounterClass.Elite;
                default: return EncounterClass.Normal;
            }
        }

        private void OnFightEnded(bool won)
        {
            if (!RunManager.HasRun) return;

            var run = RunManager.Run;
            var session = fight.Session;

            // FOLDED BEFORE THE WIN CHECK. What a character did in the fight
            // that killed them is part of the run -- dropping it would make the
            // death screen under-report the most dramatic fight in it, which is
            // the one fight the player most wants described.
            RunLedger.Fold(run, session?.Ledger);

            var payout = won ? session?.Payout : null;
            RunLedger.RecordRoom(run, won,
                payout?.Gold ?? 0,
                won ? (payout?.Experience ?? 0) : 0,
                run?.step ?? 0);

            // A boss goes on the run's list the moment it dies. Whether it PAYS
            // is settled at the end of the run against the save's lifetime
            // list, because only that knows whether this was the first time.
            if (won && session != null && session.IsBossFight)
            {
                RunLedger.RecordBossKill(run, BossIdOf(session));
            }

            if (!won)
            {
                // A loss ends the RUN, not just the fight. Anything else would
                // let a player retry the same room until it went their way,
                // which is the whole tension a roguelike is built on.
                //
                // EndRun settles before it discards, and hands back what it
                // paid -- which is the only surviving record of the run by the
                // time the defeat screen draws.
                LastSettlement = RunManager.EndRun();
                return;
            }

            if (payout.HasValue)
            {
                // GOLD to the run, EXPERIENCE to the characters. Two different
                // owners with two different lifetimes: the run's gold is spent
                // inside the run and lost with it, while a level survives.
                RunManager.BankPayout(payout.Value.Gold);
                LastReward = RewardApplier.Apply(payout.Value, FieldedIds());

                // The fight's own counters, carried onto the reward so the
                // Reckoning's tally tab has something to read. Without this the
                // ledger existed, was folded into the run, and was visible only
                // after you died.
                if (session?.Ledger != null) LastReward.Ledger = session.Ledger;
            }

            RunManager.ClearCurrentRoom();

            // Out of rooms means the LEG ended, not the run. A leg is eight
            // steps and finishes on whatever the curve forces -- an elite at
            // step 8, a boss at 16 -- so ending the run here would stop every
            // descent at the first elite.
            //
            // Where to go NEXT is no longer decided here: the map screen is
            // where the player chooses, and this only opens the next leg when
            // there is nothing left to choose between.
            if (RunManager.LegIsOver()) RunManager.AdvanceLeg();
        }

        // What the fight just paid, per character. Held for the rewards screen
        // to read once it exists; until then the levels have still been applied,
        // which is the part that matters to the save.
        public static Domain.Rewards.CombatReward LastReward { get; private set; }

        // What the run that just ended paid out and cost. Read by the defeat
        // screen, which cannot compute it itself: EndRun has already discarded
        // the snapshot by the time anything is drawn.
        public static RunSettlement.Result LastSettlement { get; private set; }

        // Which boss died. The run records the enemy it was sent to kill rather
        // than whatever happened to be standing there, so a boss room with
        // adds cannot pay out twice or pay for the wrong thing.
        private static string BossIdOf(Domain.Combat.Session.FightSession session)
        {
            string declared = RunManager.Run?.bossEnemyId;
            if (!string.IsNullOrEmpty(declared)) return declared;

            // A boss fight with no declared id is a content gap, not a reason
            // to lose the kill: fall back to the enemy that was actually there.
            return session.Encounter.Enemies
                .Select(session.SourceFor)
                .FirstOrDefault(k => k?.Source != null && k.Source.IsBoss)?.Source.Id;
        }

        // Who actually stood on the stage. A squad member left out of the
        // encounter (at 0 HP when it was built) is downed rather than absent.
        private System.Collections.Generic.IReadOnlyList<string> FieldedIds()
        {
            var session = fight.Session;
            if (session == null) return new System.Collections.Generic.List<string>();

            return session.Encounter.PlayerParty
                .Select(session.KitFor)
                .Where(k => k != null)
                .Select(k => k.Id)
                .ToList();
        }

        // The satchel, from the stash.
        //
        // stockpiledItems is the single live inventory for now -- the same list
        // the character overlay reads -- so a potion bought between runs is a
        // potion available in the next fight, and using one is visible on both
        // screens because there is only one list.
        private static IReadOnlyList<SatchelStack> BuildSatchel()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return new List<SatchelStack>();

            return save.stockpiledItems
                .Where(e => e != null && e.count > 0)
                .Select(e => new { Entry = e, Item = ContentDatabase.GetItem(e.itemId) })
                .Where(x => x.Item != null && x.Item.kind == ItemKind.Consumable)
                .Select(x => new SatchelStack(
                    x.Item.id, x.Item.displayName, x.Entry.count,
                    x.Item.effect == ItemEffect.RestoreMana))
                .ToList();
        }

        // Resolving the effect is Core's job -- the session is told what
        // happened, not what the item was, because ItemEffect is content and
        // the session is Domain.
        private void OnItemUsed(string itemId)
        {
            var save = SaveSlotManager.CurrentSave;
            var item = ContentDatabase.GetItem(itemId);
            if (save == null || item == null) return;

            fight.Session?.UseConsumable(item.displayName, item.amount,
                item.effect == ItemEffect.RestoreMana);

            // Spent from the stash and written immediately, then the column is
            // handed the new counts -- otherwise it keeps showing what the
            // fight opened with.
            InventoryOps.TryRemove(save.stockpiledItems, itemId);
            SaveSlotManager.SaveCurrent();
            fight.RefreshSatchel(BuildSatchel());
        }
}
}
