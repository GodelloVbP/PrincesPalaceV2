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
    // TWO PATHS, and which one runs depends on whether a descent is happening.
    //
    // With a run, the room decides: RunEncounter fields the whole surviving
    // squad against enemies rolled from the fight stream for this node. That
    // is the real game, and it is what the header below used to promise.
    //
    // Without one, the placeholder still stands -- opening the Fight scene
    // directly has to produce a playable, LOOK-THE-SAME-EVERY-TIME fight or
    // screenshot.ps1 cannot verify this screen and PlayMode tests have no
    // stage to assert against. That is why the fixed seed survives: it is the
    // tooling's fight, not the game's.
    public class FightBootstrap : MonoBehaviour
    {
        [SerializeField] internal FightController fight;

        // Fixed by default. A fight that reshuffles every time the scene
        // reloads cannot be compared against the last screenshot, and this
        // screen is verified by looking at it. Used by the NO-RUN path only.
        [SerializeField] internal int seed = 20260810;

        // How many monsters to field. Three is the stage's own capacity
        // (FightHudSpec.StageSlotsPerSide) and shows every slot at once.
        // No-run path only; in a run the count comes from the room.
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

            // OPEN THE FIGHT. Every test in the project called this and nothing
            // in the game did -- 36 call sites, all of them in Tests/ -- so the
            // opening sequence its own docstring describes ("any monsters faster
            // than the whole party take their opening swings") never ran for a
            // player.
            //
            // AutoResolveEnemyTurns has exactly two callers: this, and the path
            // that runs AFTER the player acts. So enemies only ever moved in
            // reply to a move. TurnOrder.Start seeds charge from initiative and
            // gives turn one to the highest-initiative combatant -- and when
            // that was an enemy, nothing existed to resolve its turn. It held
            // the turn forever: monsters standing still, no intent icons
            // (Bind's telegraph is gated on IsPlayerTurn), every verb disabled
            // (CanAct requires IsPlayerTurn), no exception, and the stranded-
            // turn watchdog silent because _isBusy was never set.
            //
            // Reported as "every game after the first elite, none of the buttons
            // respond". Floor 1's monsters are slower than the party, so the
            // player almost always opened and the deadlock could not happen;
            // leg 2's pool admits faster ones. bffe4c5 added the watchdog and
            // four tests for this symptom and recorded "WHAT I HAVE NOT DONE IS
            // REPRODUCE IT" -- because every one of those tests calls Begin by
            // hand, and so repairs the exact state the game was leaving broken.
            //
            // BEFORE Bind, because Bind paints the HUD and telegraphs the first
            // turn from a turn state it assumes is already settled.
            built.Session.Begin();

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

        internal FightEncounterAdapter.BuiltFight BuildOpeningFight() =>
            RunManager.HasRun ? BuildRoomFight() : BuildPlaceholderFight();

        // The real thing: this room, this squad, this run's seed.
        private FightEncounterAdapter.BuiltFight BuildRoomFight()
        {
            var run = RunManager.Run;

            // Entry is the only non-fight room that can reach this path, and
            // only via a direct scene load. Treating an unknown room as a
            // normal fight beats refusing to build one, for the same reason the
            // no-content case degrades rather than throwing.
            var roomType = RunManager.CurrentNode?.Type ?? RoomType.Fight;

            var roster = RunEncounter.For(SaveSlotManager.CurrentSave, run, roomType);
            if (roster.IsEmpty)
            {
                // An empty party here is a squad wipe that should have ended
                // the run before the map ever offered this room. Saying so is
                // worth more than an empty stage that looks like a render bug.
                Debug.LogWarning(
                    $"[FightBootstrap] Room {roomType} fielded {roster.PartyIds?.Count ?? 0} party " +
                    $"and {roster.EnemyIds?.Count ?? 0} enemies; the stage stays empty.");
                return null;
            }

            // isBoss/isElite ACTUALLY PASSED, which they were not before.
            // FightSession took its defaults, so IsBossFight was false in every
            // fight the game could reach -- and OnFightEnded gates
            // RecordBossKill on it, so no boss kill was ever recorded and the
            // run settled without paying for any of them. EnemyKit took the
            // same false for isElite, so elite rooms fielded ordinary kits.
            var built = FightEncounterAdapter.Build(
                roster.PartyIds, roster.EnemyIds, roster.Rng,
                isBoss: roster.IsBoss,
                isElite: roster.IsElite,
                relicIds: run.relicIds,
                depthStep: run.step,
                // What they are WEARING, which is the difference between a
                // character built from their save and one built from the
                // content that named them. Without this the roster was right
                // and every one of them fought at base stats -- full plate and
                // nothing swung identically.
                partyCharacters: SaveSlotManager.CurrentSave?.ActiveSquad());

            if (built == null) return null;

            // Damage taken in earlier rooms, carried in. Applied after the
            // build because the adapter constructs from definitions and knows
            // nothing about a descent.
            RunEncounter.ApplyStartingHealth(built.Party, roster.PartyIds, roster.StartingHealth);
            return built;
        }

        // The tooling's fight. Unchanged, and deliberately still art-filtered:
        // its whole purpose is a stage that can be LOOKED at, which a party of
        // fallback plates defeats.
        private FightEncounterAdapter.BuiltFight BuildPlaceholderFight()
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
            // DEPTH GOES IN AT BUILD TIME, not after it.
            //
            // Session.DepthStep is assigned by the caller once the fight
            // exists, which is soon enough for the payout and far too late for
            // the enemies — they are fully constructed by then. That gap is
            // how DifficultyCurve came to be scaling rewards and nothing else
            // while its own header claimed it scaled enemy stats.
            // No relics and no depth: this path only runs when there is no run
            // to have drafted any or to be at a depth. Those two arguments used
            // to read the run defensively, which read as though a run could
            // reach here -- one can't, and BuildRoomFight is where it goes.
            return FightEncounterAdapter.Build(party, enemies, new SeededRandom((ulong)seed),
                relicIds: null,
                depthStep: 0);
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

            // HP CARRIED FORWARD, also before the win check, and for a related
            // reason: a loss ends the run through EndRun below, and the defeat
            // screen reports on the squad that just died. Writing health back
            // only on a win would leave that screen reading whatever the party
            // had walked IN with.
            //
            // This is the other half of ApplyStartingHealth. Nothing in v2
            // wrote party health back before it, so every room opened at full
            // regardless of what the last one cost -- which also left Rest
            // rooms with nothing to restore even once they resolve again.
            RunEncounter.WriteBackHealth(run, session);

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
