using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Economy;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Relics;
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
        public const int CurrentVersion = 4;

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
        // from content": once both placeholder_brawler and placeholder_caster
        // resolve in ContentDatabase.Characters, a new save fields all three
        // without anyone having to flip anything. Set true/false to override
        // that either way -- BalanceBotRunner forces true regardless of
        // content shape, and a test pinning the solo-default behaviour forces
        // false. A caller that overrides it must set it back to null, since
        // it is static and survives past the call that set it.
        public static bool? TestSquadOfThreeEnabled;

        // Deliberately re-checks content rather than caching: ContentDatabase
        // can be (re)loaded mid-process (Editor domain reload, tests), and a
        // stale "yes" here would field a placeholder seat content no longer
        // has.
        private static bool SquadOfThreeReady =>
            ContentDatabase.Characters.Any(c => c.id == "placeholder_brawler")
            && ContentDatabase.Characters.Any(c => c.id == "placeholder_caster");
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
        public List<string> selectedCharacterIds = new List<string>();
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

        // Which relic each character is carrying — RelicDefinition content
        // (a run-long combat effect), NOT the `Relics` currency above. Named
        // after its TYPE rather than the concept, unlike every other field
        // here (compare `equipment` on Character) — the natural name
        // `relics` would sit one line of intent away from `Relics` the
        // currency, and that collision was worth avoiding rather than living
        // with.
        public RelicLoadout relicLoadout = new RelicLoadout();

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

        public List<Character> ActiveSquad()
        {
            var selected = selectedCharacterIds
                .Select(id => roster.FirstOrDefault(c => c != null && c.definitionId == id))
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

            data.selectedCharacterIds = data.roster.Take(data.EffectiveMaxSquadSize()).Select(c => c.definitionId).ToList();

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

            if (version < 3)
            {
                MoveEmbersOntoTheRoster();
            }

            if (version < 4)
            {
                ClearWhatARunShouldNotHaveKept();
            }

            version = CurrentVersion;
            Reconcile();
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

        private void MoveEmbersOntoTheRoster()
        {
            int pooled = wallet.embers;
            if (pooled <= 0) return;

            var first = roster?.FirstOrDefault(c => c != null);
            if (first != null) first.embers += pooled;

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
        // selectedCharacterIds to EffectiveMaxSquadSize(). Public (not just
        // called from Migrate/Load) specifically so a purchase that changes
        // EffectiveMaxSquadSize — buying extra_recruit_slot — can call this
        // immediately instead of only taking effect on the next full
        // reload. AUDIT.md P0 #3: this was previously private, and nothing
        // outside Migrate/Load ever called it, so a purchased upgrade
        // showed "(Owned)" while doing nothing for the rest of the session.
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
            activeRun.inventory.RemoveAll(entry => entry == null || ContentDatabase.GetItem(entry.itemId) == null);
            activeRun.currentHealth.RemoveAll(entry => entry == null || ContentDatabase.GetCharacter(entry.characterId) == null);

            ReconcileShopStock(activeRun);

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

            // Same tolerant posture as everything else in this method: drop
            // an assignment pointing at a relic that got renamed/removed, or
            // at a character no longer in the roster, rather than letting a
            // stale reference sit there or fail loudly. No "give it back"
            // step needed — an unassigned relic just becomes available
            // again, unlike an orphaned equipped item, which is why this is
            // one line rather than the stash-return block equipment needed.
            relicLoadout ??= new RelicLoadout();
            relicLoadout.RemoveWhere((characterId, relicId) =>
                ContentDatabase.GetRelic(relicId) == null || roster.All(c => c.definitionId != characterId));

            selectedCharacterIds.RemoveAll(id => ContentDatabase.GetCharacter(id) == null);
            int effectiveMax = EffectiveMaxSquadSize();

            if (selectedCharacterIds.Count > effectiveMax)
            {
                // Cap raised the wrong way (e.g. an upgrade somehow revoked —
                // not currently possible, just defensive) — trim rather than
                // silently exceeding it.
                selectedCharacterIds = selectedCharacterIds.Take(effectiveMax).ToList();
            }
            else if (selectedCharacterIds.Count < effectiveMax)
            {
                // Tops up newly-available slots (a new character added to
                // the roster, or extra_recruit_slot just purchased) without
                // disturbing whatever was already selected — this is what
                // lets a save transition cleanly instead of only working out
                // right for a brand new one.
                foreach (var character in roster)
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

        // Whether the thing a card is selling still exists. Books answer
        // false until gate 3 authors them, which is the same answer their
        // NO OFFER placeholder already gives -- so nothing changes shape when
        // they arrive.
        private static bool Resolves(ShopStockEntry entry)
        {
            switch (entry.kind)
            {
                case ShopEntryKind.Gear: return ContentDatabase.GetItem(entry.contentId) != null;
                case ShopEntryKind.Relic: return ContentDatabase.GetRelic(entry.contentId) != null;
                default: return false;
            }
        }
    }
}
