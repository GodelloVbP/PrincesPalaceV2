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

        // EDITOR/DEV ONLY -- set by QuickFightMenu just before it opens the
        // Fight scene in Play mode, never by ScreenRegistry/SceneBuilder and
        // never serialized on this component. When set, BuildPlaceholderFight
        // fields exactly this one enemy instead of its usual sortOrder pick,
        // which is how "hop straight into a fight against just the giant
        // rat" works without a second generated scene (a full SceneBuilder
        // rebuild reassigns every scene's fileIDs -- too much for a dev
        // convenience). Consumed (cleared) the moment BuildPlaceholderFight
        // reads it, so a forced fight can never survive a scene reload or
        // leak into a normal no-run placeholder.
        //
        // Backed by SessionState, not a plain static field -- entering Play
        // mode runs a domain reload (by default) that resets every static
        // field BEFORE Start() ever runs, which would silently drop a value
        // QuickFightMenu set an instant earlier. SessionState is the
        // standard survives-a-domain-reload channel for exactly this.
        // #if'd out of player builds, where UnityEditor isn't linked; the
        // plain-field fallback there is always null, so BuildPlaceholderFight
        // needs no #if of its own at the call site.
#if UNITY_EDITOR
        private const string DevForcedEnemyIdKey = "PrincesPalace.Dev.ForcedEnemyId";
        internal static string DevForcedEnemyId
        {
            get => UnityEditor.SessionState.GetString(DevForcedEnemyIdKey, "");
            set => UnityEditor.SessionState.SetString(DevForcedEnemyIdKey, value ?? "");
        }
#else
        internal static string DevForcedEnemyId;
#endif

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

            fight.Bind(built.Session, EncounterFor(RunManager.CurrentNode), RunOrchestrator.BuildSatchel());
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

        // The IN-RUN half moved to RunOrchestrator.BuildFight, which is the
        // seam the balance bot builds its fights through too (docs/
        // PLAN_BALANCE_BOT.md F2). What stays here is the half that is only
        // ever a screen's: the placeholder stage, the art filtering, and the
        // wiring in Start above.
        internal FightEncounterAdapter.BuiltFight BuildOpeningFight() =>
            RunManager.HasRun ? RunOrchestrator.BuildFight() : BuildPlaceholderFight();

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
            //
            // DevForcedEnemyId overrides all of that with exactly one named
            // enemy -- see its own field comment. Consumed here, not left for
            // next time.
            List<string> enemies;
            string forcedId = DevForcedEnemyId;
            DevForcedEnemyId = null;
            if (!string.IsNullOrEmpty(forcedId) && ContentDatabase.Enemies.Any(e => e.id == forcedId))
            {
                enemies = new List<string> { forcedId };
            }
            else
            {
                enemies = ContentDatabase.Enemies
                    .Where(HasArt)
                    .OrderBy(e => e.SortOrder)
                    .Take(enemyCount)
                    .Select(e => e.id)
                    .ToList();

                if (enemies.Count == 0)
                {
                    enemies = ContentDatabase.Enemies.Take(enemyCount).Select(e => e.id).ToList();
                }
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
            // reach here -- one can't, and RunOrchestrator.BuildFight is where
            // it goes.
            return FightEncounterAdapter.Build(party, enemies, new SeededRandom((ulong)seed),
                relicIds: null,
                depthStep: 0);
        }

        private static bool HasArt(CharacterDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.data.BattleSpritePath);

        private static bool HasArt(EnemyDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.data.SpritePath);
    
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

        // THE SETTLEMENT LIVES IN RunOrchestrator NOW.
        //
        // Every rule that used to be written out here -- fold the ledger,
        // write HP back, spend second lives, record the room and the boss, end
        // the run on a loss, bank gold and apply experience on a win, clear
        // the room, advance the leg -- moved to RunOrchestrator.SettleFight
        // unchanged, comments and all. The bot has to obey the same rules and
        // a second copy of them would measure itself rather than the game
        // (docs/PLAN_BALANCE_BOT.md F2); FightSettlementTests pins the
        // behaviour through this same door.
        //
        // What is left here is what only a SCREEN needs: the two statics the
        // Reckoning and the defeat screen read back through RewardSource and
        // SettlementSource.
        private void OnFightEnded(bool won)
        {
            var settled = RunOrchestrator.SettleFight(fight.Session, won);

            // ASSIGNED ONLY WHEN THERE IS SOMETHING TO ASSIGN, which is what
            // the two branches this replaces did: a win never touched
            // LastSettlement and a fight that paid nothing never touched
            // LastReward. Writing either unconditionally would blank a value
            // the next screen is about to read.
            if (settled.Reward != null) LastReward = settled.Reward;
            if (settled.RunEnded != null) LastSettlement = settled.RunEnded;
        }

        // What the fight just paid, per character. Held for the rewards screen
        // to read once it exists; until then the levels have still been applied,
        // which is the part that matters to the save.
        public static Domain.Rewards.CombatReward LastReward { get; private set; }

        // What the run that just ended paid out and cost. Read by the defeat
        // screen, which cannot compute it itself: EndRun has already discarded
        // the snapshot by the time anything is drawn.
        public static RunSettlement.Result LastSettlement { get; private set; }

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
            fight.RefreshSatchel(RunOrchestrator.BuildSatchel());
        }
}
}
