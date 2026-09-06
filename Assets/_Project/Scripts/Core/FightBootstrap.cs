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
        //
        // TWO MORE KEYS BESIDE IT, and they belong here rather than anywhere
        // nearer the content because they are the same kind of thing: a
        // preview's opinion about ONE fight, consumed the instant it is read,
        // never written to a record, a save or an asset. tools/preview.ps1 sets
        // them through Editor/PreviewRequestWatcher.cs.
        //
        //   DevForcedFormation -- "full" fields three copies of the forced mob
        //     instead of one, so a summon has a slot to fail on and an
        //     all-target ability has something to hit.
        //   DevForcedEnemyScript -- hands the session an EnemyShowcase, which
        //     makes the mob take its authored abilities in order instead of
        //     rolling. See that class's header.
        //   DevForcedSkillId -- tools/preview.ps1 -Spell. The skill is
        //     appended to the preview's own kit (FightEncounterAdapter.Build's
        //     previewExtraSkillIds), so unlock level and book ownership are
        //     bypassed WITHOUT the content record or a save being touched, and
        //     PreviewFight decides who casts it and what the stage has to look
        //     like for the cast to be visible.
        //   DevForcedSquad -- tools/preview.ps1 -Character. A comma-separated
        //     party for this one fight, in place of the art-filtered pick
        //     below.
        //   DevForcedFirstAction -- a skill id the player's first turn casts
        //     by itself, through the same buttons a hand would press
        //     (FightController.ForceFirstAction). Separate from
        //     DevForcedSkillId because character mode sets it to a skill the
        //     kit already has, and spell mode sets both to the same id.
#if UNITY_EDITOR
        private const string DevForcedEnemyIdKey = "PrincesPalace.Dev.ForcedEnemyId";
        private const string DevForcedFormationKey = "PrincesPalace.Dev.ForcedFormation";
        private const string DevForcedEnemyScriptKey = "PrincesPalace.Dev.ForcedEnemyScript";
        private const string DevForcedSkillIdKey = "PrincesPalace.Dev.ForcedSkillId";
        private const string DevForcedSquadKey = "PrincesPalace.Dev.ForcedSquad";
        private const string DevForcedFirstActionKey = "PrincesPalace.Dev.ForcedFirstAction";

        internal static string DevForcedEnemyId
        {
            get => UnityEditor.SessionState.GetString(DevForcedEnemyIdKey, "");
            set => UnityEditor.SessionState.SetString(DevForcedEnemyIdKey, value ?? "");
        }

        internal static string DevForcedFormation
        {
            get => UnityEditor.SessionState.GetString(DevForcedFormationKey, "");
            set => UnityEditor.SessionState.SetString(DevForcedFormationKey, value ?? "");
        }

        internal static bool DevForcedEnemyScript
        {
            get => UnityEditor.SessionState.GetBool(DevForcedEnemyScriptKey, false);
            set => UnityEditor.SessionState.SetBool(DevForcedEnemyScriptKey, value);
        }

        internal static string DevForcedSkillId
        {
            get => UnityEditor.SessionState.GetString(DevForcedSkillIdKey, "");
            set => UnityEditor.SessionState.SetString(DevForcedSkillIdKey, value ?? "");
        }

        internal static string DevForcedSquad
        {
            get => UnityEditor.SessionState.GetString(DevForcedSquadKey, "");
            set => UnityEditor.SessionState.SetString(DevForcedSquadKey, value ?? "");
        }

        internal static string DevForcedFirstAction
        {
            get => UnityEditor.SessionState.GetString(DevForcedFirstActionKey, "");
            set => UnityEditor.SessionState.SetString(DevForcedFirstActionKey, value ?? "");
        }
#else
        internal static string DevForcedEnemyId;
        internal static string DevForcedFormation;
        internal static bool DevForcedEnemyScript;
        internal static string DevForcedSkillId;
        internal static string DevForcedSquad;
        internal static string DevForcedFirstAction;
#endif

        // How many copies of a forced mob "-Formation full" fields. The stage's
        // own capacity, so every slot is occupied and nothing is hidden.
        private const int FullFormationCount = 3;

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

            // AFTER Bind, because the controller resets its menu inside it and
            // a queued action set beforehand would be thrown away with the rest
            // of the previous fight's state. Consumed on read, like every other
            // DevForced key -- one preview, one fight.
            string firstAction = DevForcedFirstAction;
            DevForcedFirstAction = null;
            if (!string.IsNullOrEmpty(firstAction)) fight.ForceFirstAction(firstAction);

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
            // roster have sheets and the rest do not. That pick moved to
            // PreviewFight.EnemiesWithArt, which is the one place both this
            // route and the headless capture read it from -- a photograph of a
            // different formation than the one -Launch fields is evidence
            // about nothing.
            //
            // DevForcedEnemyId overrides all of that with exactly one named
            // enemy -- see its own field comment. Consumed here, not left for
            // next time.
            List<string> enemies;
            string forcedId = DevForcedEnemyId;
            string formation = DevForcedFormation;
            bool showcase = DevForcedEnemyScript;
            string forcedSkill = DevForcedSkillId;
            string forcedSquad = DevForcedSquad;

            // ALL FIVE CONSUMED TOGETHER, and before anything can throw. They
            // are one preview's opinion about one fight; a leftover key is a
            // scene reload later showing a fight nobody asked for.
            DevForcedEnemyId = null;
            DevForcedFormation = null;
            DevForcedEnemyScript = false;
            DevForcedSkillId = null;
            DevForcedSquad = null;

            // THE SPELL PREVIEW DECIDES THE REST OF THE FIGHT, not the author.
            // Who can cast it, how many enemies it needs to be visible against,
            // whether the party has to be hurt first -- all of it falls out of
            // the skill, so PreviewFight answers once and both this path and
            // the headless capture read the same answer.
            List<string> previewSkills = null;
            PreviewFight.Plan plan = null;

            if (!string.IsNullOrEmpty(forcedSkill))
            {
                plan = PreviewFight.ForSpell(forcedSkill);
                Debug.Log("[FightBootstrap] spell preview: " + PreviewFight.Describe(plan));

                // REFUSED MEANS REFUSED. Building the fight anyway would put a
                // stage in front of the author that does not show what they
                // asked for -- and they would believe it, because it looks
                // exactly like one that does.
                if (!plan.Ok) return null;

                previewSkills = new List<string> { forcedSkill };
                party = new List<string> { plan.CasterId };
                if (string.IsNullOrEmpty(formation)) formation = plan.Formation;
            }
            else if (!string.IsNullOrEmpty(forcedSquad))
            {
                var squad = forcedSquad.Split(',')
                    .Select(id => id.Trim())
                    .Where(id => id.Length > 0 && ContentDatabase.Characters.Any(c => c != null && c.id == id))
                    .ToList();

                if (squad.Count > 0) party = squad;
                else Debug.LogWarning($"[FightBootstrap] forced squad '{forcedSquad}' names nobody in content; " +
                                      "the usual placeholder party stands instead.");
            }

            if (!string.IsNullOrEmpty(forcedId) && ContentDatabase.Enemies.Any(e => e.id == forcedId))
            {
                int copies = formation == "full" ? FullFormationCount : 1;
                enemies = Enumerable.Repeat(forcedId, copies).ToList();
            }
            else
            {
                // THE FORMATION REACHES THIS BRANCH TOO, and it has to: a
                // spell preview forces no enemy id at all (it does not care
                // which monster it lands on) but a Summon still needs a free
                // slot and an all-target cast still needs more than one thing
                // to hit. Empty formation -- every non-preview fight -- keeps
                // the authored enemyCount.
                enemies = PreviewFight.EnemiesWithArt(PreviewFight.EnemyCountFor(formation, enemyCount));
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
            var built = FightEncounterAdapter.Build(party, enemies, new SeededRandom((ulong)seed),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: previewSkills);

            // AFTER the build and only for a preview. The session is the right
            // owner (the showcase has to survive every turn of one fight and
            // die with it), and the adapter is deliberately not told a preview
            // exists.
            // AFTER the build, because the preview's accommodations are made
            // to the CombatantState the build produced -- see PreviewFight.
            if (plan != null && built != null)
            {
                PreviewFight.Prepare(built, plan);
                Debug.Log("[FightBootstrap] spell preview, prepared: " + PreviewFight.Describe(plan));
            }

            if (showcase && built != null)
            {
                built.Session.Showcase = new EnemyShowcase(Debug.Log);
                Debug.Log($"[FightBootstrap] showcase mode: '{forcedId}' will take its authored abilities in " +
                          "order, then swing plainly. Any it cannot play this turn is named and skipped.");
            }

            return built;
        }

        private static bool HasArt(CharacterDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition.Data.BattleSpritePath);

    
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
