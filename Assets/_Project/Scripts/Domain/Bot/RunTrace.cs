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

    // One room, fight or otherwise.
    public sealed class RoomTrace
    {
        public int Step;
        public int NodeId;
        public string RoomType;
        public List<string> OfferItemIds = new List<string>();
        public int PickedIndex;

        // What the equip pass after this room put on. Empty for most rooms,
        // which is the honest answer: a player does not re-dress after every
        // fight either.
        public List<EquipTrace> Equipped = new List<EquipTrace>();
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
                      .Append('/').Append(turn.PartyHpAfter).Append('/').Append(turn.EnemyHpAfter).Append('|');
                }
                sb.Append(';');
            }

            foreach (var room in Rooms)
            {
                sb.Append(room.Step).Append(',').Append(room.NodeId).Append(',').Append(room.RoomType).Append(',');
                foreach (var id in room.OfferItemIds) sb.Append(id).Append('+');
                sb.Append(',').Append(room.PickedIndex).Append(',');
                foreach (var worn in room.Equipped)
                {
                    sb.Append(worn.CharacterId).Append('/').Append(worn.ItemId).Append('/')
                      .Append(worn.Slot).Append('/').Append(worn.Plus).Append('|');
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
