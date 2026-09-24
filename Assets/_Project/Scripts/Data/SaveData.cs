using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Economy;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    [Serializable]
    public class SaveData
    {
        // Bump whenever the shape or meaning of a field changes, and add a
        // matching step to Migrate(). Without this, JsonUtility silently
        // fills missing fields with defaults and a stale save loads as
        // half-valid data rather than failing loudly.
        //
        // 1 -> 2: the talent tree reset. Orbs stopped costing talent points
        // and started costing Embers, so every orb on an older save was paid
        // for in a currency that no longer exists. This is the FIRST bump this
        // field has ever taken -- everything before it was additive and
        // handled by clear-as-you-read folds inside Reconcile(), which is the
        // policy the comments below still describe and which still applies to
        // anything that is merely a new field. A change that INVALIDATES data
        // already on disk is the case that policy cannot cover.
        // 2 -> 3: Embers moved from the shared wallet onto the CHARACTER.
        // A single pool meant a character you had never fielded could be
        // kindled to the top of their tree out of embers someone else earned,
        // which is the opposite of what per-character progression is for. The
        // migration hands the whole old pool to the first roster member rather
        // than splitting it, because splitting would silently reduce what any
        // one character can afford and there is no record of who earned what.
        // 3 -> 4: a run keeps nothing. Gear and the pack are cleared when a
        // run ends now, and a save written before that rule is carrying the
        // spoils of runs that ended under the old one -- 41 stockpiled items
        // and three worn pieces on the save that reported this. Cleared once,
        // on the way up, rather than left as a permanent exception to a rule
        // the game otherwise enforces.
        // 4 -> 5: the reward track became per-character content. Removed
        // fields would need no bump -- JsonUtility drops what the shape no
        // longer has -- but `claimedTrackLevel` SURVIVES and would be
        // REINTERPRETED, which is worse than either: a watermark of 40 read
        // against Shawn's new table claims two wool-capacity nodes and +12%
        // Nature that were never applied, while unspentStatPoints still holds
        // what the old table paid. See Migrate's own step for what it does
        // about it.
        // 5 -> 6: progression v2 reset the whole ladder. The level cap came
        // down from 100 to 40, the cost of every level changed (10 was 236 and
        // is now 500; 40 was 30,199 and is now 10,000), and the reward at each
        // level is being reauthored in phase 4. A carried `level` therefore
        // means something different on each side of this bump, and a carried
        // `exp` was earned against income that paid 195,369 for a deep run
        // where it now pays 10,153. There is no honest conversion: a
        // level-preserving migration would have to reconcile old rewards
        // against new nodes, which is more code than the feature. So the
        // ladder is put back to the bottom and everything that is not the
        // ladder is kept. The only saves in existence are developers'
        // (PLAN_PROGRESSION_V2.md §6 says so and is the authority for this
        // being acceptable at all); a shipped game would need the other
        // answer.
        public const int CurrentVersion = 6;

        // Meta-progression: the "extra_recruit_slot" Principality upgrade
        // raises this. Matches the id ContentBuilder authors it under —
        // the same plain-string-content-id convention already used
        // elsewhere in this codebase (e.g. "health_potion"), not a
        // dedicated registry for what is currently a single lookup.
        // Solo (Shawn only) when the two placeholder seats aren't authored —
        // see SquadOfThreeReady below for when this widens to 3 on its own.
        private const int BaseMaxSquadSize = 1;
        private const int TestSquadOfThreeSize = 3;

        // THE single switch, but its DEFAULT is derived rather than hardcoded
        // false. Null (the state every fresh process starts in) means "decide
        // from content": once both bear (Bjorn) and owl (Odette)
        // resolve in ContentDatabase.Characters, a new save fields all three
        // without anyone having to flip anything. Set true/false to override
        // that either way -- BalanceBotRunner forces true regardless of
        // content shape, and a test pinning the solo-default behaviour forces
        // false. A caller that overrides it must set it back to null, since
        // it is static and survives past the call that set it.
        public static bool? TestSquadOfThreeEnabled;

        // Deliberately re-checks content rather than caching: ContentDatabase
        // can be (re)loaded mid-process (Editor domain reload, tests), and a
        // stale "yes" here would field a seat content no longer has.
        //
        // ASKS THE FLAG, NOT TWO IDS. This named bear and owl
        // literally, which made "can we field three" a question about two
        // specific characters rather than about the roster -- rename either
        // and every fresh profile silently drops back to a solo Shawn with
        // nothing failing. characters.json says who starts now
        // (RawCharacterEntry.startsInSquad), and CharacterEntryResolver
        // refuses the file unless exactly three do, so this can simply count.
        private static bool SquadOfThreeReady =>
            DefaultSquadIds().Count >= TestSquadOfThreeSize;

        // THE STARTING SQUAD, IN ITS AUTHORED ORDER. Empty when content
        // carries no flags at all -- an unbuilt or half-built catalogue -- and
        // every caller falls back to roster order there, which is what this
        // replaced and is still a better answer than an empty party.
        //
        // Ordered by squadSlot, then by id so the order is total even in the
        // impossible case of a duplicate slot reaching a built asset (the
        // resolver refuses one, but ContentDatabase can be handed assets a
        // resolver never saw -- CLAUDE.md's [CreateAssetMenu] hazard).
        internal static List<string> DefaultSquadIds() =>
            ContentDatabase.Characters
                .Where(c => c != null && c.Data.StartsInSquad)
                .OrderBy(c => c.Data.SquadSlot)
                .ThenBy(c => c.id, StringComparer.Ordinal)
                .Select(c => c.id)
                .ToList();
        // PUBLIC because the squad's ceiling has to be assertable against the
        // stage's slot count, and a test that wrote "extra_recruit_slot" as a
        // literal would be the drift it is meant to catch.
        public const string ExtraRecruitSlotUpgradeId = "extra_recruit_slot";
        private const int ExtraRecruitSlotBonus = 1;

        public int version = CurrentVersion;

        // THE persistent purse: Gold and Relics. One place all earning and
        // spending goes through, rather than a bare += at every call site —
        // which is how AUDIT.md P0 #1's unbounded money loop happened.
        public Wallet wallet = new Wallet();

        // LEGACY, migration source only. This was the only currency the game
        // had. Reconcile() folds a non-zero value into wallet.gold and clears
        // it, so a save written before Relics existed keeps its money. Kept as
        // a FIELD rather than deleted because deleting it makes JsonUtility
        // drop the value on load, silently robbing every existing save.
        public int currency;

        public int Gold
        {
            get => wallet.gold;
            set => wallet.Set(CurrencyType.Gold, value);
        }


        public int exp;
        public List<Character> roster = new List<Character>();

        // THE PARTY'S SEATS, IN SEAT ORDER, AND AN EMPTY SEAT IS ONE OF THEM.
        //
        // This was a compact list of whoever is fielded, and the compaction
        // was the bug (AUDIT #93): PartyFormation keeps seats by index and
        // leaves a hole where one is vacated, because "POSITIONS ARE
        // MECHANICAL, not cosmetic -- seat 0 is the front rank enemy melee
        // concentrates on". Benching the front-ranker and reloading therefore
        // promoted whoever was in Middle into Front, silently, and combat had
        // already promoted them before any reload because ActiveSquad reads
        // this same list.
        //
        // An empty seat is EmptySeat (the empty string) in place. Trailing
        // empties are not written -- a hole carries positional information
        // only when somebody sits behind it, and keeping the tail would leave
        // a solo save three entries long for one member.
        public List<string> selectedCharacterIds = new List<string>();

        // THE CAP THIS PROFILE HAS ALREADY SEEN, which is what separates "the
        // squad is short because the cap grew" from "the squad is short
        // because the player benched somebody" (AUDIT #118).
        //
        // Reconcile's top-up used to read the two as one: a selection shorter
        // than EffectiveMaxSquadSize() meant seats to fill, so benching a
        // member from the party screen was undone by the next load, which
        // handed them back in the REAR seat because that is where TopUpOrder
        // lands. The count cannot answer the question; only a recorded fact
        // can.
        //
        // ZERO ON EVERY SAVE WRITTEN BEFORE THIS FIELD EXISTED (JsonUtility
        // keeps the initialiser for a missing key), which reads as "this
        // profile has never seen a cap" and so tops up exactly once, exactly
        // as it did before. Reconcile stamps the current cap on the way out,
        // so the second load onwards respects a bench.
        public int squadSizeSeen;

        public List<string> purchasedUpgradeIds = new List<string>();

        // Every boss this profile has ever put down, across all runs.
        //
        // THE ember source. Embers are paid per UNIQUE boss kill, so the answer
        // to "does this kill pay" is "is it already in here" — which is why the
        // list is lifetime and lives on the save rather than on the run. A run
        // knows what it killed; only this knows what was new.
        //
        // Deliberately never cleared. Clearing it would silently re-open a
        // payout the player has already banked and spent, which is the
        // meta-progression equivalent of the infinite money printer.
        public List<string> defeatedBossIds = new List<string>();

        // ---- lifetime totals, across every run ever ---------------------------
        //
        // EndRun replaces the run snapshot wholesale, so anything counted only
        // there is gone the moment a descent ends. That made "clear a hundred
        // rooms" mean a hundred in ONE descent, which nobody will ever do --
        // the achievement was authored, listed, and unreachable.
        //
        // Folded in by RunSettlement, which already runs before EndRun for
        // exactly this class of reason. Achievements read these PLUS the live
        // run, so a total ticks up during a descent rather than only at its
        // end, and cannot double-count once the run is settled and discarded.
        public int lifetimeRoomsCleared;
        public long lifetimeDamageDealt;
        public int lifetimeRunsEnded;

        // A HIGH-WATER MARK, not a sum -- how deep this profile has ever got.
        // Compared against the live run rather than added to it, which is the
        // one place the lifetime/active fold in Achievements.FactsFor differs
        // per field.
        public int lifetimeDeepestStep;

        // TICKED CONTINUOUSLY BY PlaytimeTracker, not measured between saves.
        // A wall-clock timestamp pair (last opened, last saved) would count
        // every minute the game sat alone on a desktop as played; this only
        // grows while a scene that is actually PLAYING is loaded (see the
        // tracker's own header for why the main menu itself does not count).
        //
        // A float rather than the `long` lifetimeDamageDealt uses: seconds
        // over any survivable play history stay well inside a float's exact-
        // integer range, and this is a slot-card display number, never
        // compared for equality or fed into a formula the way damage is.
        public float totalPlaySeconds;

        // relicLoadout WAS HERE, and it is gone (AUDIT #119, owner's call
        // 2026-09-11). A `(characterId, relicId)` map, serialized on every
        // save, null-guarded and pruned on every Reconcile, and NEITHER
        // WRITTEN NOR READ by anything in the tree -- stronger than #50, #87
        // and #112, which are at least written.
        //
        // What actually carries a character's relic is RunSnapshot.relicIds
        // plus FightEncounterAdapter.ResolveRelics, which is run-scoped rather
        // than save-scoped; per-character assignment, which is the only thing
        // this shape was for, exists nowhere and is not on the roadmap. It was
        // written as `{}` on every save, so JsonUtility simply DROPS the
        // unknown key on the next load and nothing is lost -- the same
        // no-migration deletion grantedGold got in the pass that filed #112.
        //
        // Kept as a comment rather than removed outright because the hazard
        // was never the field, it was a future reader seeing a relic loadout
        // on the save and assuming relics were already handled.

        // Items bought in the Divine Principality store with permanent
        // currency. GameplayManager.StartRun() copies these into the fresh
        // RunState.inventory and clears this list — same "granted fresh,
        // not banked" rule RunState already applies to a starting-item
        // talent's grant, so there's only ever one rule for "what's in my
        // inventory this run" regardless of where an item came from.
        public List<InventoryEntry> stockpiledItems = new List<InventoryEntry>();

        // The run in progress, if there is one. Always non-null; ask
        // `activeRun.hasRun`, never `activeRun != null` — see RunSnapshot's
        // own comment for why the discriminator has to be in-band.
        //
        // Purely ADDITIVE, so CurrentVersion does not move: an older save
        // simply has no such field, JsonUtility leaves the initializer's
        // instance in place, and `hasRun` reads false — which is exactly
        // right, since a save written before runs were resumable did not have
        // one. That is the same additive posture Migrate() already documents.
        public RunSnapshot activeRun = new RunSnapshot();

        // ---- event counters (plan contract 9) ---------------------------------
        //
        // PROFILE-SCOPED, not run-scoped, which is the whole reason these are
        // here and not on RunSnapshot: "on the tenth toss" counts across runs,
        // and EndRun replaces the snapshot wholesale. EndRun touches neither
        // this list nor anything else on SaveData it does not name.
        //
        // A {id, value} list rather than a Dictionary because JsonUtility
        // cannot write one -- same shape as RunHealthEntry. Additive, so
        // CurrentVersion does not move: an old save reads this as empty, and
        // an empty list is every counter at 0.
        public List<EventCounterEntry> eventCounters = new List<EventCounterEntry>();

        // 0 for a counter never touched -- the same answer an absent entry
        // and a fresh profile both give, so "has never happened" needs no
        // separate state.
        public int EventCounter(string counterId)
        {
            if (string.IsNullOrEmpty(counterId) || eventCounters == null) return 0;

            foreach (var entry in eventCounters)
            {
                if (entry != null && entry.id == counterId) return entry.value;
            }

            return 0;
        }

        public void AddEventCounter(string counterId, int delta)
        {
            if (string.IsNullOrEmpty(counterId) || delta == 0) return;
            SetEventCounter(counterId, EventCounter(counterId) + delta);
        }

        public void SetEventCounter(string counterId, int value)
        {
            if (string.IsNullOrEmpty(counterId)) return;

            eventCounters ??= new List<EventCounterEntry>();
            var entry = eventCounters.FirstOrDefault(e => e != null && e.id == counterId);
            if (entry == null)
            {
                entry = new EventCounterEntry { id = counterId };
                eventCounters.Add(entry);
            }

            entry.value = value;
        }

        // How many roster members can be active in the squad at once.
        // Character Select is out of the active flow for now (see #21
        // follow-up), so this isn't enforced by picking — it caps how many
        // of the roster get auto-selected in CreateNew()/Reconcile().
        //
        // CAPPED AT THE STAGE, always -- FightHudSpec.StageSlotsPerSide (3)
        // is how many fight-scene slots exist on the player's side of the
        // stage, built once at scene-build time; FieldableParty does not
        // clamp past it on its own (see FightAfterTheEliteTests'
        // TheBiggestSquadTheSaveAllowsStillFitsTheStage, which exists for
        // exactly this reason). Solo base + extra_recruit_slot tops out at 2,
        // safely under 3; squad-of-three base + the same upgrade would
        // reach 4 without this Min, which is exactly the "extra hero fights
        // from off screen" case that test refuses to ship. Capping here
        // rather than widening the stage keeps the upgrade's own promise
        // ("one more seat") honest without a stage that already fits the
        // whole roster having anything left to grow into.
        public int EffectiveMaxSquadSize()
        {
            bool squadOfThree = TestSquadOfThreeEnabled ?? SquadOfThreeReady;
            int baseSize = squadOfThree ? TestSquadOfThreeSize : BaseMaxSquadSize;
            int withUpgrade = baseSize + (purchasedUpgradeIds.Contains(ExtraRecruitSlotUpgradeId) ? ExtraRecruitSlotBonus : 0);
            return Math.Min(withUpgrade, FightHudSpec.StageSlotsPerSide);
        }

        // THE single definition of "who is in the party right now".
        //
        // This existed six times, copy-pasted across GameplayManager,
        // FightController, DungeonController, TalentController,
        // CharacterSheetController and InventoryController — and the copies
        // disagreed. Five fell back to the full roster when the selection was
        // empty; GameplayManager.StartRun did not. That divergence is a real
        // bug, not untidiness (AUDIT.md #12): with an empty selection StartRun
        // seeded currentHealth for nobody while the fight built a full-roster
        // party at full HP, so RunState.IsSquadWiped could never fire and the
        // run became unloseable.
        //
        // Unified on the fallback, because that is what five of the six did
        // and what every screen needs: a save with no selection must still
        // show and field SOMEONE rather than an empty party.
        //
        // Ids that no longer resolve are dropped rather than throwing — a save
        // written before a character was renamed stays loadable, matching
        // Reconcile's posture everywhere else.
        // Embers are held PER CHARACTER, not in the wallet -- wallet.embers is
        // a migration source and nothing live reads it. So "how many embers do
        // I have" is a sum over the roster, and it lives here rather than in
        // whichever screen asked first: the hub had it as a private static, and
        // the system menu's lintel needed the same number.
        public int EmberTotal()
        {
            int total = 0;
            foreach (var character in roster ?? new List<Character>())
            {
                if (character != null) total += character.embers;
            }

            return total;
        }

        // WHAT AN EMPTY SEAT LOOKS LIKE IN selectedCharacterIds, and the one
        // question every reader of that list asks.
        //
        // The empty string rather than null, because JsonUtility writes a null
        // element of a List<string> as "" and reads it back as "" -- a null
        // sentinel would not survive its own round trip, so this is the one
        // the serializer already produces rather than a second one layered on
        // top of it. Both are tolerated on the way in for the same reason
        // every other Reconcile guard is tolerant.
        public const string EmptySeat = "";

        public static bool IsEmptySeat(string id) => string.IsNullOrEmpty(id);

        public List<Character> ActiveSquad()
        {
            // An empty seat resolves to nobody and drops out here, exactly as
            // an id content no longer has always did -- "who is in the party"
            // is a question about members, not about seats.
            var selected = selectedCharacterIds
                .Select(id => IsEmptySeat(id)
                    ? null
                    : roster.FirstOrDefault(c => c != null && c.definitionId == id))
                .Where(c => c != null)
                .ToList();

            return selected.Count > 0 ? selected : new List<Character>(roster);
        }

        // Same resolution, ids only — for callers that just need to ask "is
        // this set of characters wiped" rather than the Character objects.
        // Sharing ActiveSquad's body matters: RunState.IsSquadWiped requires
        // an entry for EVERY id it is given, so an id list built by a
        // different rule than the party itself would silently disagree about
        // who counts.
        public List<string> ActiveSquadIds()
        {
            return ActiveSquad().Select(c => c.definitionId).ToList();
        }

        // Builds a fresh profile with one instance of every authored
        // character. Adding a character to the game is purely a content
        // change — nothing here needs editing.
        public static SaveData CreateNew()
        {
            var data = new SaveData();

            foreach (var definition in ContentDatabase.Characters)
            {
                data.roster.Add(new Character(definition.id));
            }

            // WHO CONTENT SAYS STARTS, not who sorts first.
            //
            // This was `roster.Take(EffectiveMaxSquadSize())` over authored
            // file order, which meant a character appended to the end of
            // characters.json was in the roster and could never be fielded,
            // and one inserted at position three silently benched whoever was
            // there. Neither said anything; the only way to find out was to
            // read this line. characters.json now flags its three starters
            // and CharacterEntryResolver refuses the file unless exactly three
            // do -- so the roster's order is once again just an order.
            //
            // Filtered against the roster rather than trusted: a flagged
            // character with no roster entry is impossible today (the roster
            // IS every authored character, right above) and would otherwise
            // select an id ActiveSquad cannot resolve.
            var starters = DefaultSquadIds()
                .Where(id => data.roster.Any(c => c.definitionId == id))
                .ToList();

            if (starters.Count == 0)
            {
                starters = data.roster.Select(c => c.definitionId).ToList();
            }

            data.selectedCharacterIds = starters.Take(data.EffectiveMaxSquadSize()).ToList();

            // A fresh profile has seen exactly the cap it was built against,
            // so it is already topped up and the first bench sticks.
            data.squadSizeSeen = data.EffectiveMaxSquadSize();

            // The starting kit: one of every item flagged startingStock in
            // items.json. Granted HERE and nowhere else — Reconcile
            // deliberately doesn't re-grant, so this is a set of gear a new
            // player begins with, not a per-load income (the exact shape of
            // the bug AUDIT.md P0 #1 describes for starting_gold_boost).
            foreach (var item in ContentDatabase.StartingStock)
            {
                data.stockpiledItems.Add(new InventoryEntry(item.id, 1));
            }

            return data;
        }

        // Brings an older save up to CurrentVersion. Returns true when the
        // save was usable (possibly after migration), false when it is too
        // old or too damaged to recover and should be replaced.
        public bool Migrate()
        {
            if (version == CurrentVersion)
            {
                Reconcile();
                return true;
            }

            // Version 0 predates the content-driven roster: its characters
            // carried their own names instead of definition ids, so there is
            // nothing reliable to map forward. No saves have shipped, so
            // this is discarded rather than guessed at.
            if (version < 1)
            {
                return false;
            }

            // A save from a newer build than this one: refuse rather than
            // risk interpreting fields we do not understand.
            if (version > CurrentVersion)
            {
                return false;
            }

            // 1 -> 2: the talent reset.
            //
            // Gated on the version the save CAME FROM, not on the version it is
            // going to, and expressed as a range rather than `version == 1`.
            // Both matter. A range keeps this correct when CurrentVersion moves
            // to 3 and a v1 save has to pass through this step on its way
            // there; an equality check would silently skip it and let a v1
            // save arrive in a much later build with its old orbs intact.
            //
            // Ordered BEFORE the stamp below for the same reason -- once
            // `version` is rewritten there is no longer anything to test.
            if (version < 2)
            {
                ResetTalentProgress();
            }

            // 2 -> 3 (the ember move) IS DEFERRED PAST Reconcile, alone
            // among these steps, and the flag is captured here so the
            // gate still reads the version the save CAME FROM -- the same
            // reason the 1 -> 2 step above is expressed as a range rather
            // than an equality.
            //
            // It is the only step that MOVES something rather than clearing
            // it, so it is the only one that cares whether the roster it is
            // moving onto survives. Run here, it handed the whole pooled
            // balance to the first roster member on disk and then Reconcile
            // deleted that member for naming content that no longer exists --
            // which is an ordinary rename, and took the embers with it. An
            // empty roster lost them the same way one step earlier, with the
            // wallet cleared and nobody to have received it.
            bool moveEmbers = version < 3;

            if (version < 4)
            {
                ClearWhatARunShouldNotHaveKept();
            }

            if (version < 5)
            {
                ResetTheRewardTrack();
            }

            if (version < 6)
            {
                ResetTheLadder();
            }

            version = CurrentVersion;
            Reconcile();

            // AFTER Reconcile, which is what guarantees a roster to move them
            // onto: it drops members naming content that is gone and adds one
            // for every authored character, so by here the list is exactly
            // what this build can hold.
            if (moveEmbers) MoveEmbersOntoTheRoster();

            return true;
        }

        // The whole old shared pool to the FIRST roster member, then cleared.
        //
        // Not split evenly: a split silently reduces what any single character
        // can afford, and since nothing recorded who earned the embers there is
        // no honest way to divide them. Giving them to one character is at
        // least a decision somebody can see and undo.
        // The one-time sweep for saves written before "a run keeps nothing".
        //
        // Only ever runs on the way from 3 to 4, so a player who buys something
        // between runs after this keeps it until their next run ends -- which is
        // the rule, not an exception to it. Deliberately NOT put in Reconcile:
        // that runs on every load, and clearing the pack there would delete a
        // purchase before the run it was bought for ever started.
        private void ClearWhatARunShouldNotHaveKept()
        {
            stockpiledItems?.Clear();

            foreach (var character in roster ?? new List<Character>())
            {
                character?.equipment?.Clear();
            }
        }

        // The one-time sweep for saves written before the reward track became
        // per-character content.
        //
        // WHAT THIS IS ACTUALLY FIXING is not the removed fields -- JsonUtility
        // drops `earnedFavor`, `bonusExpPermille` and `bonusMaxHealth` on load
        // with no help from here -- but the one that SURVIVED.
        // `claimedTrackLevel` used to mean "paid this far up ONE shared table
        // of stat points and max health"; it now means "collected this far up
        // THIS CHARACTER'S track", and every reward on that track is summed
        // live against it. A watermark of 40 carried across unread would hand
        // Shawn two wool-capacity nodes and +12% Nature he was never paid,
        // while his unspentStatPoints still holds what the old table gave him.
        // That is a plausible wrong answer, which is the one thing neither of
        // this project's two error postures may return.
        //
        // So the track is put back to waiting and the points it paid are taken
        // back with it: claimedTrackLevel 0, unspentStatPoints 0, and the
        // invested block cleared, since points that came off the old table
        // cannot stay spent once the payment is unwound. `level`, `exp`,
        // `embers`, unlocked talents and equipment are untouched. The player
        // opens the reward screen to a full track of waiting nodes and one
        // collect button -- a free respec of exactly the half the track paid
        // for, which is the right answer to "the track changed underneath
        // you", and it exercises the new claim path from level 1 on first
        // open.
        //
        // ONE CONSEQUENCE WORTH KNOWING BEFORE IT IS REPORTED AS A BUG:
        // invested points are part of the equipment requirement floor
        // (ContentDatabase.ActiveLoadout), so a character whose weapon was
        // liftable only because of them opens the migrated save with that item
        // INERT -- worn, on the paperdoll, contributing nothing -- until the
        // points are re-spent. It is visible (the dossier paints
        // SlotBlockedCaptions) and it is one collect plus a re-spend away,
        // which is the same state Character.Respec already produces on
        // purpose. Nothing here tries to be clever about it.
        private void ResetTheRewardTrack()
        {
            foreach (var character in roster ?? new List<Character>())
            {
                if (character == null) continue;

                character.claimedTrackLevel = 0;
                character.unspentStatPoints = 0;
                character.investedAbilityScores = default;
            }
        }

        // The one-time sweep for saves written before progression v2 reset the
        // ladder, 5 -> 6.
        //
        // EVERYTHING THE LADDER TOUCHES, and nothing else. Level and
        // experience go back to where a new character starts; the track's
        // watermark, the points it paid and the scores those points were spent
        // on go with them, because a point that came off the old table cannot
        // stay spent once the level that paid it is unwound. `embers`,
        // `unlockedTalentIds` and `equipment` are untouched -- none of them was
        // ever priced in experience, and a player who spent an evening on a
        // talent tree should not lose it to a curve retune.
        //
        // OVERLAPS ResetTheRewardTrack ON PURPOSE. A version-4 save runs both,
        // and the three fields they share are set to the same values by each,
        // so the order does not matter and neither does the duplication. The
        // alternative -- a shared helper called from two version gates -- would
        // couple two steps that are only alike by coincidence, and the 4 -> 5
        // step must keep meaning what it meant when it was written even if
        // this one changes.
        //
        // IDEMPOTENT BY THE VERSION STAMP rather than by the arithmetic:
        // Migrate returns at its first line for a save already at
        // CurrentVersion, so a version-6 save loaded twice is read, reconciled
        // and left alone. That is what makes "loading again changes nothing"
        // true, and it is why the stamp is written in the same pass as the
        // reset rather than afterwards.
        private void ResetTheLadder()
        {
            foreach (var character in roster ?? new List<Character>())
            {
                if (character == null) continue;

                character.level = 1;
                character.exp = 0;
                character.claimedTrackLevel = 0;
                character.unspentStatPoints = 0;
                character.investedAbilityScores = default;
            }
        }

        private void MoveEmbersOntoTheRoster()
        {
            int pooled = wallet.embers;
            if (pooled <= 0) return;

            // THE SOURCE IS NOT CLEARED UNTIL SOMEBODY HAS TAKEN IT. The
            // clear used to happen either way, so a roster with nobody in it
            // emptied the wallet into nothing. Its caller now runs this after
            // Reconcile, which should make an empty roster impossible -- this
            // is the guard that keeps "should" from being the only thing
            // standing between a player and their whole ember balance.
            var first = roster?.FirstOrDefault(c => c != null);
            if (first == null) return;

            first.embers += pooled;
            wallet.embers = 0;
        }

        // Locks every orb and zeroes every unspent point.
        //
        // Called only from Migrate's 1 -> 2 step, and deliberately not from
        // Reconcile: Reconcile runs on EVERY load, and a destructive pass
        // living there would erase the tree again on every launch -- silently,
        // and worse each time, since by then the orbs would have been paid for
        // with Embers that are gone too. That distinction (fold-in on every
        // load vs. one-shot on a version change) is the whole reason the
        // version field exists.
        private void ResetTalentProgress()
        {
            if (roster == null)
            {
                return;
            }

            foreach (var character in roster)
            {
                if (character == null)
                {
                    continue;
                }

                character.unlockedTalentIds?.Clear();

                // There is no point balance left to clear: talentPoints was
                // retired once levels stopped granting it. A legacy save still
                // has the key on disk and JsonUtility simply drops it, which
                // is the whole of the cleanup that field now needs.
            }
        }

        // Reconciles a save against currently authored content: adds
        // characters that were introduced since the save was written, drops
        // references to content that no longer exists, and tops up
        // selectedCharacterIds to EffectiveMaxSquadSize().
        //
        // PUBLIC, AND THE REASON IS NOT THE ONE THIS COMMENT USED TO GIVE. It
        // said the visibility existed so that buying extra_recruit_slot could
        // reconcile immediately rather than at the next full reload (AUDIT.md
        // P0 #3). There is no such caller and never was one under this tree:
        // the only production `.Reconcile()` outside Migrate/Load is
        // ProfilePresets.cs, the bot's profile builder, and
        // purchasedUpgradeIds has no production writer at all -- upgrades.json
        // says as much itself ("there is no Principality shop screen yet, so
        // nothing has ever charged anybody 50 for anything"). The described
        // purchase cannot happen, so the justification described a caller that
        // does not exist.
        //
        // What is true today: it is public because the bot builds a profile
        // in memory and has to settle it against content without a load, and
        // because tests reach it directly. If a Principality shop is ever
        // built, calling this after a purchase IS the right move -- the
        // sentence above was a plan, and it is recorded here as one.
        public void Reconcile()
        {
            wallet ??= new Wallet();

            // Fold the pre-Relics single currency into Gold. Runs on every
            // Reconcile rather than behind a version bump: the field is cleared
            // as it is read, so a second pass finds nothing to do — the same
            // additive-migration call SaveData's version comment already makes
            // about every other field added since.
            if (currency != 0)
            {
                wallet.Add(CurrencyType.Gold, currency);
                currency = 0;
            }

            roster ??= new List<Character>();
            selectedCharacterIds ??= new List<string>();
            purchasedUpgradeIds ??= new List<string>();
            stockpiledItems ??= new List<InventoryEntry>();

            stockpiledItems.RemoveAll(entry => entry == null || ContentDatabase.GetItem(entry.itemId) == null);

            // No modifierIds pruning here yet, and that is correct for now --
            // Phase A (this one) adds only the storage, and no path in the
            // game today can write a real id into modifierIds, so there is no
            // content to have gone stale. A populated-but-unresolvable
            // modifierIds list (the future case: a modifier renamed or
            // removed from modifiers.json) is still perfectly safe to load --
            // it just sits there meaning nothing until something reads it --
            // so this does not crash or misbehave either way. Once Phase A2
            // ships modifiers.json, this needs the same tolerant-prune
            // treatment stockpiledItems and character.equipment already get
            // above: drop ids ContentDatabase no longer resolves, same
            // posture, not yet written because there is nothing to prune
            // against.

            // A saved run gets the same tolerant pruning as the stockpile: a
            // content update between quitting and resuming can remove an item
            // or a character the run was carrying, and a run is not worth
            // discarding over one dangling id. The map does not need pruning
            // because it is regenerated rather than stored, and generation
            // reads no content at all.
            activeRun ??= new RunSnapshot();
            activeRun.inventory ??= new List<InventoryEntry>();
            activeRun.currentHealth ??= new List<RunHealthEntry>();
            activeRun.clearedNodeIds ??= new List<int>();
            activeRun.relicIds ??= new List<string>();

            // THE TWO LISTS RunSettlement ACTUALLY READS, and the two this
            // method walked past. Every other run list above is guarded here
            // and pruned somewhere; bossesKilled and ledger were neither, which
            // left the only run-scoped values a load hands straight to a payout
            // as the only ones nothing checked.
            activeRun.bossesKilled ??= new List<string>();
            activeRun.ledger ??= new List<RunLedgerEntry>();
            activeRun.inventory.RemoveAll(entry => entry == null || ContentDatabase.GetItem(entry.itemId) == null);
            activeRun.currentHealth.RemoveAll(entry => entry == null || ContentDatabase.GetCharacter(entry.characterId) == null);

            // THE RUN'S OWN RELICS, which this method pruned everything BUT.
            //
            // save.relicLoadout got this treatment for as long as it existed
            // and the run's list did not -- and the run's is the one that
            // counts, which is most of why the other was deleted (#119).
            // FightEncounterAdapter.ResolveRelics skips an unresolvable id
            // harmlessly, but RunOrchestrator.DraftHasAnotherRound counts
            // relicIds.Count against RelicPool.StartingRelicsPerDescent -- so
            // a relic renamed between quitting and resuming ends the draft a
            // round early, having handed over one fewer relic than it says.
            activeRun.relicIds.RemoveAll(id => ContentDatabase.GetRelic(id) == null);

            // THE RUN'S BOSS KILLS, which are money. RunSettlement pays an
            // ember per boss the profile has never killed and then writes that
            // id into save.defeatedBossIds permanently, where
            // DefeatDistinctBosses counts it. A boss renamed in enemies.json
            // between quitting and resuming would therefore pay for a dead id
            // AND leave it on the lifetime list, while the same boss under its
            // new id pays again -- one rename, two embers, and a distinct-boss
            // count that names something the game no longer has.
            //
            // LOUD, unlike the tolerant prunes above. Dropping a dangling item
            // costs the player nothing they can see; dropping a boss kill takes
            // away a payout they earned, and that should be in the log if
            // anyone ever comes looking for the missing ember.
            foreach (var bossId in activeRun.bossesKilled
                         .Where(id => ContentDatabase.GetEnemy(id) == null)
                         .ToList())
            {
                UnityEngine.Debug.LogWarning(
                    $"[Reconcile] the run says it killed '{bossId}', which is no longer an enemy in content. " +
                    "Dropping the kill rather than paying an ember for an id nothing can name.");

                activeRun.bossesKilled.Remove(bossId);
            }

            ReconcileLearnedSpells(activeRun);
            ReconcileShopStock(activeRun);
            ReconcileOpenEvent(activeRun);

            eventCounters ??= new List<EventCounterEntry>();
            eventCounters.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));

            roster.RemoveAll(c => c == null || ContentDatabase.GetCharacter(c.definitionId) == null);

            foreach (var definition in ContentDatabase.Characters)
            {
                if (roster.All(c => c.definitionId != definition.id))
                {
                    roster.Add(new Character(definition.id));
                }
            }

            foreach (var character in roster)
            {
                character.unlockedTalentIds ??= new List<string>();
                character.unlockedTalentIds.RemoveAll(id => ContentDatabase.GetTalent(id) == null);

                // The OTHER unlocked list, which this loop walked straight
                // past. unlockedSkillIds is the Event room's mage's gift --
                // nothing writes it today (ContentDatabase reads it and no
                // production path fills it), so this is inert, exactly the way
                // the modifierIds prune above says its own plumbing is. It is
                // here so the two lists cannot drift on what "a reference to
                // content that no longer exists" means the day one of them
                // gets a writer.
                character.unlockedSkillIds ??= new List<string>();
                character.unlockedSkillIds.RemoveAll(id => ContentDatabase.GetSkill(id) == null);

                // Strips talents that belong to a DIFFERENT character. Before
                // talents had an owner, every node was available to everyone,
                // so a save written then can legitimately hold one that is now
                // somebody else's — and its bonuses would keep applying
                // through EffectiveStats forever, invisibly, because the node
                // is no longer drawn on that character's tree. Same tolerant
                // posture as the prune above: drop the reference, keep the
                // save loadable.
                character.unlockedTalentIds.RemoveAll(id =>
                {
                    var talent = ContentDatabase.GetTalent(id);
                    return talent != null && !talent.IsAvailableTo(character);
                });

                // Absent in any save written before the paperdoll existed.
                character.equipment ??= new EquipmentLoadout();

                // Drop worn ids that no longer resolve, or that resolve to
                // something no longer wearable (an item demoted from Weapon
                // to Consumable in items.json). Same tolerant posture as the
                // talent prune directly above — but the pieces are handed
                // BACK to the stash rather than deleted, because a slot
                // holding a real item the player earned should not be
                // silently emptied by an authoring change.
                // AT THE PLUS IT LEFT WITH. This used to take the ids-only
                // overload and re-add at the default plus 0, so a renamed
                // content id quietly turned a +5 heirloom into a plain one --
                // with the item count still correct, which is why nothing
                // caught it.
                // AT THE PLUS AND ROLL IT LEFT WITH -- orphan.modifierIds and
                // orphan.riftTier travel back with it for the identical
                // reason orphan.plus does above them. Nothing in Phase A ever
                // puts a real id into modifierIds, so this is inert today;
                // it exists so the plumbing is already correct once Phase A2
                // ships modifiers.json.
                foreach (var orphan in character.equipment.RemoveEntriesWhere(
                             id => ContentDatabase.GetItem(id)?.IsEquippable != true))
                {
                    if (ContentDatabase.GetItem(orphan.itemId) != null)
                    {
                        InventoryOps.Add(stockpiledItems, orphan.itemId, 1, orphan.plus,
                            orphan.modifierIds, orphan.riftTier);
                    }
                }

                // Migrate the single-weapon-slot era (Character.
                // equippedItemId) into the paperdoll's first hand. Runs on
                // every Reconcile rather than gated on a version bump: the
                // field is cleared as it is read, so a second pass finds
                // nothing to do, and no CurrentVersion bump is needed for
                // what is a purely additive change (the same call
                // SaveData's own version comment already makes about the
                // other additive fields).
                if (!string.IsNullOrEmpty(character.equippedItemId))
                {
                    string legacyId = character.equippedItemId;
                    character.equippedItemId = "";

                    if (ContentDatabase.GetItem(legacyId)?.IsEquippable == true
                        && character.equipment.IsEmpty(EquipmentSlot.Weapon1))
                    {
                        character.equipment.Set(EquipmentSlot.Weapon1, legacyId);
                    }
                }
            }

            purchasedUpgradeIds.RemoveAll(id => ContentDatabase.GetUpgrade(id) == null);

            // relicLoadout's null-guard and prune stood here, tolerantly
            // dropping assignments nothing could resolve. Both went with the
            // field (#119): a prune that cannot be observed is upkeep on a
            // fact nothing asks for.

            // AN EMPTY SEAT IS NOT A DANGLING REFERENCE. This drop is for ids
            // naming content that is gone -- a renamed character -- and those
            // still CLOSE UP and get replaced, which is what
            // SaveReconcileRenamedCharacterTests pins and why it is a
            // RemoveAll rather than a blank-in-place: nobody chose that hole,
            // and a squad silently down to two would hide behind
            // ActiveSquad's whole-roster fallback.
            //
            // A seat the PLAYER emptied is the opposite case and is skipped
            // here, or Reconcile would undo every bench on the way past
            // (AUDIT #93).
            int droppedForMissingContent = selectedCharacterIds.RemoveAll(
                id => !IsEmptySeat(id) && ContentDatabase.GetCharacter(id) == null);

            TrimTrailingEmptySeats();

            int effectiveMax = EffectiveMaxSquadSize();

            if (selectedCharacterIds.Count > effectiveMax)
            {
                // Cap raised the wrong way (e.g. an upgrade somehow revoked —
                // not currently possible, just defensive) — trim rather than
                // silently exceeding it.
                selectedCharacterIds = selectedCharacterIds.Take(effectiveMax).ToList();
                TrimTrailingEmptySeats();
            }
            else if (effectiveMax > squadSizeSeen || droppedForMissingContent > 0)
            {
                // ONLY WHEN THE CAP GREW, OR WHEN CONTENT TOOK SOMEBODY AWAY
                // (AUDIT #118).
                //
                // "The squad is short" and "there is a seat to fill" are two
                // different facts, and the count can only ever tell you the
                // first. The player benches one of three, Persist writes two,
                // and this loop used to read that as a vacancy and hand the
                // benched character straight back -- in the REAR seat, because
                // that is where TopUpOrder lands. A visible affordance undone
                // by quitting.
                //
                // squadSizeSeen is the second fact. It is stamped at the
                // bottom of this method, so the growth is measured against the
                // last cap this profile actually reconciled against rather
                // than against the roster or the selection.
                //
                // EXISTING SAVES STILL KEEP THEIR SQUAD, which is what this
                // loop was always for: it only ever ADDS, never reorders and
                // never removes anything the player chose. A profile written
                // while the squad was solo gains the two seats the squad of
                // three opened, once.
                //
                // The top-up order follows content's starting squad first and
                // roster order after it, so a save that is short a member
                // gains the same one a fresh profile would rather than
                // whichever row sorts first. Appended rather than poured into
                // the holes: a hole is a seat the player emptied on purpose,
                // and a wider cap is no reason to fill it.
                foreach (var character in TopUpOrder())
                {
                    if (selectedCharacterIds.Count >= effectiveMax)
                    {
                        break;
                    }

                    if (!selectedCharacterIds.Contains(character.definitionId))
                    {
                        selectedCharacterIds.Add(character.definitionId);
                    }
                }
            }

            squadSizeSeen = effectiveMax;
        }

        // A trailing hole carries no positional information -- there is nobody
        // behind it to promote -- and keeping it would leave a solo save three
        // entries long for one member. Leading and middle holes stay exactly
        // where they are; those are the ones that say who is in the front rank.
        private void TrimTrailingEmptySeats()
        {
            while (selectedCharacterIds.Count > 0
                   && IsEmptySeat(selectedCharacterIds[selectedCharacterIds.Count - 1]))
            {
                selectedCharacterIds.RemoveAt(selectedCharacterIds.Count - 1);
            }
        }

        // The roster, with content's starting squad brought to the front.
        // Every roster member appears exactly once, so a top-up can still
        // reach somebody nobody flagged.
        private List<Character> TopUpOrder()
        {
            var order = new List<Character>();

            foreach (string id in DefaultSquadIds())
            {
                var character = roster.FirstOrDefault(c => c != null && c.definitionId == id);
                if (character != null) order.Add(character);
            }

            foreach (var character in roster)
            {
                if (character != null && !order.Contains(character)) order.Add(character);
            }

            return order;
        }

        // THE SHOP SHELF, RECONCILED THE SAME TOLERANT WAY EVERYTHING ELSE
        // IN Reconcile IS -- a content update between quitting and resuming
        // is not worth discarding a run over (docs/PLAN_SHOP.md §2e).
        //
        // Four rules, and each one is a different kind of broken:
        //
        //  - A contentId that no longer resolves becomes NO OFFER IN PLACE.
        //    Not removed: removal renumbers the indices the screen binds
        //    cards by, so a deleted item would silently shift every card
        //    after it onto the wrong slot.
        //  - A SOLD entry stays sold whatever happened to its content. The
        //    gold was spent; the card is not a refund.
        //  - Stock belonging to a node the party is not standing on is
        //    dropped whole, rerolls and all. It cannot be repaired -- it is
        //    an answer to a question about somewhere else -- and arriving at
        //    a shop rolls a fresh shelf anyway.
        //  - The reroll array is normalised to SectionCount. See
        //    RunSnapshot.shopRerollsUsed for what can make it the wrong
        //    length.
        // Tolerant pruning for docs/PLAN_SHOP.md §1b, same posture
        // stockpiledItems gets above: a content edit under a live run is not
        // worth discarding the run over.
        //
        // CHECKED AGAINST bookTier > 0, not against bookOnly.
        //
        // THE REASON THIS USED TO GIVE IS SPENT. It said "bookOnly stays false
        // on every skill until Phase E's flip (docs/handoffs/shop_v2/
        // GAP_AUDIT.md, Gate 3), so a check against it here would prune every
        // learned spell the moment it was learned". The flip has happened: all
        // four bookTier > 0 skills in skills.json (mud_burst, frost_flare,
        // cinderfault, lightning_bolt -- static_fleece and golden_fleece
        // removed 2026-09-15, AUDIT #150, they failed the universality test
        // docs/SPELL_DESIGN_STANDARD.md states) carry bookOnly: true today,
        // so the two predicates now agree.
        //
        // They agree by COINCIDENCE, which is the thing to know. bookTier is
        // still the right question here -- "is this a skill a book can be" --
        // but ContentDatabase.AvailableSkillsFor gates the KIT on BookOnly, so
        // the prune and the read would disagree the day a bookTier > 0 skill
        // ships with bookOnly: false. Nothing refuses that combination.
        //
        // AND A THIRD KIND OF BROKEN, added with the pool model (plan P6,
        // gate 4): the book is fine, the character is fine, and the two can
        // no longer go together because that character's primary pool stopped
        // reading books between the save and the load. The entry is dropped
        // rather than left to sit inert -- an unreachable slot is exactly the
        // "purchase with no effect" AvailableSkillsFor's own header records.
        //
        // THE BOOK GOES BACK TO THE POOL, not into the bin: nothing about the
        // copy is wrong, only who is holding it, and a different squad member
        // can still place it. Same call ReplaceSpell makes for a displaced
        // book, and the same reasoning (§7.1 point 5).
        //
        // LOUD, because this one is silent otherwise. The other three arms
        // drop content that no longer exists, which the player can see for
        // themselves; this one drops a spell off a character who still has
        // three empty-looking slots.
        private static void ReconcileLearnedSpells(RunSnapshot run)
        {
            run.learnedSpells ??= new List<LearnedSpellEntry>();
            run.unassignedSpellBooks ??= new List<string>();

            run.learnedSpells.RemoveAll(e => e == null || !IsBookEligible(e.skillId)
                || ContentDatabase.GetCharacter(e.characterId) == null);

            foreach (var entry in run.learnedSpells
                         .Where(e => !ContentDatabase.CanHoldSpellBooks(e.characterId))
                         .ToList())
            {
                UnityEngine.Debug.LogWarning(
                    $"[Reconcile] '{entry.characterId}' can no longer carry spell books, so '{entry.skillId}' " +
                    $"has left slot {entry.slot} and gone back to the unplaced pile.");

                run.learnedSpells.Remove(entry);
                run.unassignedSpellBooks.Add(entry.skillId);
            }

            run.unassignedSpellBooks.RemoveAll(id => !IsBookEligible(id));
        }

        private static bool IsBookEligible(string skillId)
        {
            var skill = ContentDatabase.GetSkill(skillId);
            return skill != null && skill.Data.BookTier > 0;
        }

        // The event half of "what the party is standing in", mirroring
        // ReconcileShopStock: null lists from an old save become empty ones,
        // and an event that is not under the party -- or whose content is
        // gone -- is closed rather than trusted. A closed event leaves its
        // room uncleared, which the map already tolerates: the party can
        // walk on from it like from any other current node.
        //
        // A page that no longer exists sends the event back to its start
        // page rather than closing it: the event is still real, and the
        // player loses a page rather than the room.
        private static void ReconcileOpenEvent(RunSnapshot run)
        {
            run.eventsSeen ??= new List<string>();
            run.eventResultEffects ??= new List<PrincesPalace.Domain.Events.EventEffect>();
            run.eventResultEffects.RemoveAll(e => e == null);
            run.eventId ??= "";
            run.eventPageId ??= "";
            run.eventResult ??= "";

            if (string.IsNullOrEmpty(run.eventId)) return;

            var definition = RunOrchestrator.FindEvent(run.eventId);
            if (definition == null || run.eventNodeId != run.currentNodeId || run.eventNodeId < 0)
            {
                RunOrchestrator.CloseEventFields(run);
                return;
            }

            if (!string.IsNullOrEmpty(run.eventPageId) && definition.PageById(run.eventPageId) == null)
            {
                run.eventPageId = definition.StartPage?.Id ?? "";
                run.eventResult = "";
                run.eventResultEffects.Clear();
                if (string.IsNullOrEmpty(run.eventPageId)) RunOrchestrator.CloseEventFields(run);
            }
        }

        private static void ReconcileShopStock(RunSnapshot run)
        {
            run.shopStock ??= new List<ShopStockEntry>();

            if (run.shopRerollsUsed == null || run.shopRerollsUsed.Length != ShopStock.SectionCount)
            {
                var rerolls = new int[ShopStock.SectionCount];
                int carried = Math.Min(run.shopRerollsUsed?.Length ?? 0, rerolls.Length);
                for (int i = 0; i < carried; i++) rerolls[i] = run.shopRerollsUsed[i];
                run.shopRerollsUsed = rerolls;
            }

            if (run.shopNodeId != run.currentNodeId || run.shopNodeId < 0)
            {
                run.shopStock.Clear();
                run.shopNodeId = -1;
                run.shopStockVersion = 0;
                Array.Clear(run.shopRerollsUsed, 0, run.shopRerollsUsed.Length);
                return;
            }

            // A null entry cannot be repaired IN PLACE -- there is nothing on
            // it to say which section or index it was -- and dropping it
            // would renumber the rest, which is the one thing this method is
            // careful not to do. So the shelf goes as a unit and the next
            // arrival rolls a fresh one.
            if (run.shopStock.Any(entry => entry == null))
            {
                run.shopStock.Clear();
                run.shopNodeId = -1;
                run.shopStockVersion = 0;
                Array.Clear(run.shopRerollsUsed, 0, run.shopRerollsUsed.Length);
                return;
            }

            foreach (var entry in run.shopStock)
            {
                if (entry.noOffer || entry.sold || Resolves(entry)) continue;

                entry.noOffer = true;
            }
        }

        // Whether the thing a card is selling still exists.
        private static bool Resolves(ShopStockEntry entry)
        {
            switch (entry.kind)
            {
                case ShopEntryKind.Gear: return ContentDatabase.GetItem(entry.contentId) != null;
                case ShopEntryKind.Relic: return ContentDatabase.GetRelic(entry.contentId) != null;

                // THE SAME PREDICATE THE BOOK SHELF IS ROLLED FROM.
                // IsBookEligible above is what stocks the section and what
                // prunes unassignedSpellBooks; asking it here is what stops a
                // load from disagreeing with the roll about whether a book
                // exists. This used to fall through to `default: return false`
                // -- correct while books were unauthored, and a bug from the
                // moment ShopStock.RollBooks started drawing real candidates:
                // every unsold book card on a reloaded save was stamped NO
                // OFFER in place, permanently.
                case ShopEntryKind.Book: return IsBookEligible(entry.contentId);

                default: return false;
            }
        }
    }

    // One profile counter. See SaveData.eventCounters.
    [Serializable]
    public class EventCounterEntry
    {
        public string id = "";
        public int value;
    }
}
