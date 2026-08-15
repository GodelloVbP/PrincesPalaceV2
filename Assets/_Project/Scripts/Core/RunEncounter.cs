using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // What the room in front of the party actually contains.
    //
    // The plumbing half of EncounterRoll: this reads ContentDatabase and the
    // save, and the rule it calls is pure Domain so an EditMode test can reach
    // it. Nothing here decides anything -- every choice lives in EncounterRoll.
    //
    // This replaces FightBootstrap's placeholder selection, which fielded the
    // first character with art against the first three enemies with art at a
    // fixed seed. That was correct for what it was (the screen had to be
    // openable before a descent existed) and became wrong the moment one did:
    // every battle in every run was the same hero against the same three
    // monsters, with only the difficulty curve varying by depth.
    public static class RunEncounter
    {
        public sealed class Roster
        {
            public IReadOnlyList<string> PartyIds { get; set; }
            public IReadOnlyList<string> EnemyIds { get; set; }

            // Per-character HP carried in from earlier fights this run. Absent
            // means untouched, which the adapter's full-health default already
            // gets right -- so this only ever needs to say who is hurt.
            public IReadOnlyDictionary<string, int> StartingHealth { get; set; }

            public bool IsBoss { get; set; }
            public bool IsElite { get; set; }

            // The SAME stream the roll drew from, handed on to the fight.
            //
            // Not a second stream opened from the same key: that would start
            // combat on the draws the roster selection just made, so the first
            // swing of every fight would correlate with which monsters turned
            // up in it. v1 opened one _rng in BuildEncounter and the fight
            // continued from it, and this keeps that.
            public SeededRandom Rng { get; set; }

            // A squad wipe, or content with nothing to fight. The caller shows
            // an empty stage rather than throwing: CombatEncounter's
            // constructor refuses an empty side, and a throw here unwinds
            // through OnEnable leaving a HUD whose buttons resolve into
            // nothing.
            public bool IsEmpty => PartyIds == null || PartyIds.Count == 0
                || EnemyIds == null || EnemyIds.Count == 0;
        }

        public static Roster For(SaveData save, RunSnapshot run, RoomType roomType)
        {
            var health = HealthByCharacter(run);

            var party = EncounterRoll.FieldableParty(
                save != null ? save.ActiveSquadIds() : null, health);

            // Keyed to WHERE the fight is, not to how many came before it.
            // Position carries the state, so the same room in the same run
            // fields the same enemies across a quit and reload -- which is what
            // stops a mid-fight quit being a reroll for an easier draw.
            var rng = run != null
                ? RngStreams.Open(run.runSeed, RngStreams.Fight, run.step, run.currentNodeId)
                : null;

            var roll = EncounterRoll.Roll(roomType, Pool(), rng, run?.bossEnemyId);

            return new Roster
            {
                PartyIds = party,
                EnemyIds = roll.EnemyIds,
                StartingHealth = health,
                IsBoss = roll.IsBoss,
                IsElite = roll.IsElite,
                Rng = rng,
            };
        }

        // Every authored enemy, projected down to what the roll needs.
        //
        // NOT filtered on having art, which is what the placeholder did. A
        // monster with no sprite draws as the fallback plate -- deliberately,
        // and the fallback exists precisely so content can outrun art. Keeping
        // the filter would have meant enemies.json quietly deciding the
        // encounter table by which sheets happened to be finished.
        private static IReadOnlyList<EnemyCandidate> Pool() =>
            ContentDatabase.Enemies
                .Where(e => e != null && !string.IsNullOrEmpty(e.id))
                .Select(e => new EnemyCandidate(e.id, e.isBoss, e.avoidsFrontSlot))
                .ToList();

        private static Dictionary<string, int> HealthByCharacter(RunSnapshot run)
        {
            var health = new Dictionary<string, int>();
            if (run?.currentHealth == null) return health;

            foreach (var entry in run.currentHealth)
            {
                if (entry == null || string.IsNullOrEmpty(entry.characterId)) continue;
                health[entry.characterId] = entry.hp;
            }

            return health;
        }

        // Carries this fight's damage into the next room.
        //
        // Called on the way OUT of a fight, and it is the half that makes the
        // half above mean anything: without it every room opened at full health
        // and a Rest room would have nothing to restore. Nothing in v2 wrote
        // party HP back at all before this.
        //
        // Only combatants the session can name are written. One it cannot is a
        // party member with no kit, which should not happen -- and if it does,
        // leaving the previous entry alone is safer than recording a health
        // value against the wrong character.
        public static void WriteBackHealth(RunSnapshot run, FightSession session)
        {
            if (run == null || session?.Encounter?.PlayerParty == null) return;

            run.currentHealth ??= new List<RunHealthEntry>();

            foreach (var combatant in session.Encounter.PlayerParty)
            {
                string id = session.KitFor(combatant)?.Id;
                if (string.IsNullOrEmpty(id)) continue;

                var entry = run.currentHealth.FirstOrDefault(e => e != null && e.characterId == id);
                if (entry == null)
                {
                    entry = new RunHealthEntry { characterId = id };
                    run.currentHealth.Add(entry);
                }

                entry.hp = combatant.CurrentHealth;
            }
        }

        // Applies the carried-in health to a freshly built party.
        //
        // After the adapter rather than inside it: the adapter builds a
        // combatant from a definition and knows nothing about a run, and
        // threading run state through it would put the descent inside the one
        // seam that is currently reusable by tests and the screenshot path
        // alike.
        public static void ApplyStartingHealth(
            IReadOnlyList<CombatantState> party,
            IReadOnlyList<string> partyIds,
            IReadOnlyDictionary<string, int> startingHealth)
        {
            if (party == null || partyIds == null || startingHealth == null) return;

            // The adapter skips ids it cannot resolve, so the two lists agree
            // only when every id resolved. Zipping the shorter of the two keeps
            // a content gap from writing one character's health onto another.
            int count = party.Count < partyIds.Count ? party.Count : partyIds.Count;

            for (int i = 0; i < count; i++)
            {
                if (!startingHealth.TryGetValue(partyIds[i], out int hp)) continue;
                if (hp <= 0) continue;

                party[i].CurrentHealth = hp < party[i].MaxHealth ? hp : party[i].MaxHealth;
            }
        }
    }
}
