using System.Collections.Generic;
using System.Text;

namespace PrincesPalace.Domain.Bot
{
    // What one player command did, inside one fight. Fields, not
    // properties, and everything a plain type (string/int/bool) -- this is
    // written straight to JSON by the batch runner (Phase 3), and a getter
    // with logic in it is a trap for a serializer that just walks fields.
    public sealed class TurnTrace
    {
        public string ActorId;
        public string Action;
        public string TargetId;
        public int PartyHpAfter;
        public int EnemyHpAfter;

        // THE POOL TIER THIS COMMAND'S OWN CAST FIRED -- 0 for every command
        // that is not a poolTiers skill's cast (an attack, an item, a move,
        // an untiered skill, and a tiered skill that fired no tier at all),
        // else the fired tier's damage multiplier (2 for Bjorn's Slam x2, 4
        // for x4 -- CombatBeat.PoolTierDamageMultiplier's own values). Read
        // by FightRunner off the beat it drains for THIS command whose Actor
        // is this command's own actor -- see FightRunner.Play. Phase 1 of
        // the fury-tier plan built and reverted this same field
        // (PHASE1_BOT_REPORT.md's own note); this time it stays, so a batch
        // can finally answer the plan's x2/x4 share trip-wire instead of the
        // contaminated damage-magnitude proxy PHASE6_BOT_REPORT.md §3 had to
        // fall back to.
        public float PoolTierFired;

        // THE ACTOR'S OWN PRIMARY POOL (mana, fury, whatever pools.json names
        // it for this character), immediately after this command resolved --
        // CombatantState.CurrentMana, which is PrimaryPool.Current under a
        // friendlier name. Read AFTER FightAction.Apply, the same instant
        // PartyHpAfter/EnemyHpAfter beside it are, so a tier that spent the
        // pool down and a gainOnAttack that refilled part of it on the same
        // command both show up in the one number the command leaves behind.
        public int PrimaryPoolAfter;
    }

    // One fight, start to end.
    public sealed class FightTrace
    {
        public int Step;
        public int Floor;
        public string RoomType;
        public List<string> EnemyIds = new List<string>();
        public int Turns;
        public int PartyHpIn;
        public int PartyHpOut;
        public int DamageDealt;
        public int DamageTaken;
        public List<TurnTrace> TurnTraces = new List<TurnTrace>();
        public bool Won;
        public int PayoutGold;
        public int PayoutExp;

        // ---- event fights and Shawn's own numbers (PLAN_EVENTS_BELL_AND_CARAVAN M8a) ----

        // The EVENT's id when this was an event fight (EncounterRequest.EventId),
        // "" for a room fight.
        public string EventId = "";

        // FightSession.EndReason's name: Defeated / Survived / Fell, or None
        // for a fight the runner stopped (a stall) before it ended.
        public string EndReason = "";

        // FightSession.Round when the fight stopped. For a round-limited fight
        // that Survived this is the limit + 1 (the round that never started).
        public int Rounds;

        // Shawn (kit "sheep"), when fielded: HP as a whole percent of his max
        // entering and leaving the fight, -1 when he was not fielded.
        public int ShawnHpPercentIn = -1;
        public int ShawnHpPercentOut = -1;

        // His Speed as the fight opened (the flock's speed-band split, R7).
        public int ShawnSpeed;

        // Whether he wore a Transform at any point this fight.
        public bool ShawnTransformed;

        // His row of the fight's own ledger (overkill included, the ledger's
        // own rule), and the part of it Toll of the Flock's packets made.
        public int ShawnDamageDealt;
        public int FlockDamage;

        // Names of every party member seen wearing a Transform after a command
        // (FightRunner). ShawnTransformed is read off it.
        public List<string> TransformedActors = new List<string>();
    }

    // One thing the bot put on, between rooms.
    //
    // A separate record rather than a widened RoomTrace field because equipping
    // is not one decision per room: an equip pass walks eight slots for every
    // fielded character and may move nothing or may move six things, and
    // "which item, into which slot, on whom" is the question the report has to
    // be able to ask. Same fields-only, plain-types shape TurnTrace has, and
    // for the same reason -- this is written straight to JSON.
    public sealed class EquipTrace
    {
        public string CharacterId;
        public string ItemId;
        public string Slot;
        public int Plus;
    }

    // One item on the end-of-fight reward screen, with the axes that decide
    // it rather than just the id -- the shop-balance batch (plan §7 extension)
    // cares whether tier/plus/rift-tier/modifier-count are climbing the way
    // RarityTable/LootLadder intend, not which flavor of sword landed.
    // Fields-only, same reasoning as TurnTrace/EquipTrace above.
    public sealed class OfferEntry
    {
        public string ItemId;
        public int Tier;
        public int Plus;

        // (int)Domain.Content.RiftTier, 0..3. Stored as a plain int for the
        // same reason EquipmentSlotEntry.riftTier is: a JSON number needs no
        // enum on the reading side.
        public int RiftTier;
        public int ModifierCount;
    }

    // One room, fight or otherwise.
    public sealed class RoomTrace
    {
        public int Step;
        public int Floor;
        public int NodeId;
        public string RoomType;
        public List<string> OfferItemIds = new List<string>();
        public int PickedIndex;

        // Per-offer tier/plus/riftTier/modifierCount, index-aligned with
        // OfferItemIds. OfferItemIds stays rather than being replaced --
        // existing readers (bot_merge.py's itemPickRate/itemEquipRate) key
        // off it by id alone and do not need the extra axes.
        public List<OfferEntry> Offers = new List<OfferEntry>();

        // The squad's Prince's Favor at the moment this room's offer was
        // rolled (ItemOfferRoll.CurrentSquadFavor()), and the encounter class
        // RunOrchestrator.RollOffers actually used to roll it -- "Normal" or
        // "Elite" exactly as that method computes them, not a class this room
        // arguably deserves. Empty string for a room that made no offer.
        public int Favor;
        public string EncounterClass = "";

        // What the equip pass after this room put on. Empty for most rooms,
        // which is the honest answer: a player does not re-dress after every
        // fight either.
        public List<EquipTrace> Equipped = new List<EquipTrace>();

        // ---- gold, and what the shop did with it -----------------------------
        //
        // GoldOnArrival IS RECORDED FOR EVERY ROOM, not only for shops, and
        // that is the point of it. §2a priced the shop off medians over WON
        // fights and lifetime cumulative gold; neither is what a player
        // actually holds when a door opens (§7.1 point 1). Capturing it at
        // every step costs one int and answers the question at every depth
        // rather than only where a shop happened to generate.
        //
        // Read BEFORE the room resolves, so a treasure room's stash is not
        // already in it.
        public int GoldOnArrival;

        // Zero for every room that is not a shop. GoldSpent is what left the
        // purse here (purchases and rerolls both), and GoldOnLeave is what
        // remained -- kept as two numbers rather than one difference so a
        // sale, which moves gold the other way, cannot hide inside a
        // subtraction.
        public int GoldSpent;
        public int GoldOnLeave;

        // Indexed by ShopStock's section constants. Sized by the runner, so
        // a section added later widens these without a shape change here.
        public int[] PurchasesBySection = new int[0];
        public int[] RerollsBySection = new int[0];

        // The shelf as it stood when the visit ENDED -- what was offered and
        // what was taken. "Rerolls followed by no purchase in that section"
        // and "arrived with less than the cheapest card" are both read off
        // this plus the counters above.
        public List<ShopOfferTrace> ShopOffers = new List<ShopOfferTrace>();

        // EVERY CHOICE THE POLICY MADE, IN ORDER, including the ones that
        // were refused and the leave that ended the visit.
        //
        // The counters above cannot express order and were never meant to:
        // "sold a duplicate, then bought the helm it was worse than" and
        // "bought the helm, then sold the duplicate" are the same two
        // counters and two different decisions. They also cannot express a
        // refusal at all -- a purchase the purse turned down leaves no mark
        // on PurchasesBySection, so a policy repeatedly asking for something
        // it cannot have would read as a quiet visit.
        public List<ShopChoiceTrace> ShopChoices = new List<ShopChoiceTrace>();

        // ---- spell acquisition (docs/PLAN_SHOP.md §1g/§2g, gate 3) --------------
        //
        // Recorded for EVERY ROOM, same reasoning GoldOnArrival's own header
        // gives: the gate-3 numbers ("P(a fielded character has learned a
        // first spell) by step 8/16/24/32") ask about a specific depth, and a
        // single end-of-run reading cannot answer a question about the
        // middle of the run. Read AFTER pending assignments are resolved for
        // this room, so a book bought or dropped here that got placed
        // immediately already counts.
        public int LearnedSpellCountAfterRoom;
        public int UnassignedSpellBookCountAfterRoom;

        // What the bot actually did with each pending book this room, in
        // order -- the acquisition-loop analogue of ShopChoices above.
        public List<SpellAssignmentTrace> SpellAssignments = new List<SpellAssignmentTrace>();

        // The event opened in this room ("" when none did), and whether it was
        // the bot's -ForceEvent rather than the room's own roll.
        public string EventId = "";
        public bool EventForced;
    }

    // One ChooseSpellAssignment answer and what the orchestrator did with
    // it. Flat strings and ints, same posture as ShopChoiceTrace.
    public sealed class SpellAssignmentTrace
    {
        public string SkillId = "";
        public bool Assigned;
        public string CharacterId = "";
        public int Slot = -1;

        // ShopResult.Outcome's name when Assigned is true; "Skip" otherwise
        // -- a policy declining is not a refusal, so it gets its own word
        // rather than borrowing ShopOutcome.Refused for a choice nothing
        // refused.
        public string Outcome = "";
    }

    // One ChooseShop answer and what the orchestrator did with it. Flat
    // strings and ints for the reason ShopOfferTrace is: it is written once
    // and read by a Python merge.
    public sealed class ShopChoiceTrace
    {
        // ShopChoiceKind's name.
        public string Kind = "";

        // -1 where the kind does not use it.
        public int Section = -1;
        public int Index = -1;

        // Signed the way the purse moved, straight off ShopResult: negative
        // for a purchase or a reroll, positive for a sale, zero for a
        // refusal or a leave.
        public int GoldDelta;

        // ShopOutcome's name, or "Leave". A visit that ended because the
        // shelf refused reads differently from one that ended because the
        // policy was done, and only this column can tell them apart.
        public string Outcome = "";

        // ShopRefusal's name, "None" when nothing was refused.
        public string Refusal = "None";
    }

    // One shop card, as the trace records it. Deliberately flat strings and
    // ints rather than the live ShopStockEntry: a trace row is written once
    // and read by a Python merge, so it carries what a report needs and not
    // an object that can still change.
    public sealed class ShopOfferTrace
    {
        public string Kind = "";
        public string ContentId = "";
        public int Price;
        public bool Sold;
    }

    // One whole run, one archetype, one profile, one seed.
    public sealed class RunTrace
    {
        public ulong Seed;
        public string Archetype;
        public string Profile;
        public List<FightTrace> Fights = new List<FightTrace>();
        public List<RoomTrace> Rooms = new List<RoomTrace>();
        public int DeathStep;
        public string DeathCause = "";
        public bool Capped;

        // WHAT THE PARTY DIED WEARING, item ids across the whole fielded
        // squad, ordinal-sorted so two runs that arrived at the same loadout by
        // different routes read as the same set.
        //
        // On the trace rather than computed in the runner, unlike party max HP
        // and the relic rounds: a loadout is a DECISION the run made and the
        // trace is the record of decisions. It is also the only place it can be
        // read at all -- RunManager.EndRun strips both gear and bag, so
        // anything asked afterwards reports an empty paperdoll for every run,
        // which is every run.
        public List<string> WornAtDeath = new List<string>();

        // The one equip pass that happens before any room does: the party
        // dressing itself out of whatever the profile's starting stock holds.
        // Not a RoomTrace, because there is no room yet -- and folding it into
        // the first room's would make "what did the run pick up and put on"
        // unanswerable for that room.
        public List<EquipTrace> EquippedAtStart = new List<EquipTrace>();

        // The fielded squad's total level when the run ended. Summed rather
        // than per character because only Shawn is ever really fielded today,
        // and a sum degrades correctly to "his level" while a first-member read
        // would quietly start lying the day a second character is real.
        public int LevelAtDeath;

        // A stable hash of the whole trace, for the determinism check: the
        // same seed/archetype/profile must produce the same hash twice.
        // FNV-1a over a canonical string rather than
        // System.Security.Cryptography -- keeps this file portable without
        // first checking whether that namespace is reachable from a
        // noEngineReferences assembly, and FNV-1a is more than enough
        // collision resistance for "did this batch replay identically".
        public string Hash()
        {
            var sb = new StringBuilder();
            sb.Append(Seed).Append('|').Append(Archetype).Append('|').Append(Profile).Append('|');
            sb.Append(DeathStep).Append('|').Append(DeathCause).Append('|').Append(Capped).Append('|');
            sb.Append(LevelAtDeath).Append('|');
            foreach (var id in WornAtDeath) sb.Append(id).Append('+');
            sb.Append('|');
            foreach (var worn in EquippedAtStart)
            {
                sb.Append(worn.CharacterId).Append('/').Append(worn.ItemId).Append('/')
                  .Append(worn.Slot).Append('/').Append(worn.Plus).Append('|');
            }
            sb.Append(';');

            foreach (var fight in Fights)
            {
                sb.Append(fight.Step).Append(',').Append(fight.Floor).Append(',').Append(fight.RoomType).Append(',');
                foreach (var id in fight.EnemyIds) sb.Append(id).Append('+');
                sb.Append(',').Append(fight.Turns).Append(',').Append(fight.PartyHpIn).Append(',')
                  .Append(fight.PartyHpOut).Append(',').Append(fight.DamageDealt).Append(',')
                  .Append(fight.DamageTaken).Append(',').Append(fight.Won).Append(',')
                  .Append(fight.PayoutGold).Append(',').Append(fight.PayoutExp).Append(';');

                foreach (var turn in fight.TurnTraces)
                {
                    sb.Append(turn.ActorId).Append('/').Append(turn.Action).Append('/').Append(turn.TargetId)
                      .Append('/').Append(turn.PartyHpAfter).Append('/').Append(turn.EnemyHpAfter)
                      .Append('/').Append(turn.PoolTierFired).Append('/').Append(turn.PrimaryPoolAfter).Append('|');
                }
                sb.Append(';');
            }

            foreach (var room in Rooms)
            {
                sb.Append(room.Step).Append(',').Append(room.Floor).Append(',').Append(room.NodeId).Append(',').Append(room.RoomType).Append(',');
                foreach (var id in room.OfferItemIds) sb.Append(id).Append('+');
                sb.Append(',').Append(room.PickedIndex).Append(',');
                sb.Append(room.Favor).Append(',').Append(room.EncounterClass).Append(',');
                foreach (var offer in room.Offers)
                {
                    sb.Append(offer.ItemId).Append('/').Append(offer.Tier).Append('/').Append(offer.Plus)
                      .Append('/').Append(offer.RiftTier).Append('/').Append(offer.ModifierCount).Append('|');
                }
                sb.Append(',');
                foreach (var worn in room.Equipped)
                {
                    sb.Append(worn.CharacterId).Append('/').Append(worn.ItemId).Append('/')
                      .Append(worn.Slot).Append('/').Append(worn.Plus).Append('|');
                }
                sb.Append(',');

                // THE SHOP VISIT IS IN THE HASH, or the determinism check
                // would sign off on a shop that rolled a different shelf or
                // took a different decision on the same seed -- the exact
                // defect the check exists to catch, in the newest code.
                sb.Append(room.GoldOnArrival).Append('/').Append(room.GoldSpent).Append(',');
                foreach (var choice in room.ShopChoices)
                {
                    sb.Append(choice.Kind).Append('/').Append(choice.Section).Append('/')
                      .Append(choice.Index).Append('/').Append(choice.GoldDelta).Append('/')
                      .Append(choice.Outcome).Append('|');
                }
                sb.Append(';');
            }

            return Fnv1a(sb.ToString());
        }

        private static string Fnv1a(string text)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= prime;
            }

            return hash.ToString("x16");
        }
    }
}
