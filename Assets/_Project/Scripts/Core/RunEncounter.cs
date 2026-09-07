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

            // BANDED BY DEPTH. Without the floor the pool is every non-boss
            // enemy at every depth, so a floor-1 room could field a golem --
            // twelve rounds against a starting party, next to a rat's one.
            var roll = EncounterRoll.Roll(roomType, Pool(), rng, run?.bossEnemyId,
                RunDepth.FloorFor(run?.legStartStep ?? 0));

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
                .Select(e => new EnemyCandidate(e.id, e.Data.IsBoss, e.Data.AvoidsFrontSlot, e.Data.MinFloor, e.Data.SlotSpan))
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
        // Every squad member back to their effective maximum.
        //
        // The WHOLE squad, including anyone knocked out earlier -- that is what
        // makes a rest the answer to a bad fight rather than a small top up for
        // whoever happened to survive it. EncounterRoll.FieldableParty reads
        // these same entries, so a character restored here fields again in the
        // next room.
        //
        // HERE RATHER THAN IN RoomResolver, which is where it used to live, so
        // that a rest room and the end of a leg heal by the same code. Two
        // implementations of "restore the party" is exactly the kind of pair
        // that drifts, and one of them would have been the one nobody tested.
        public static void HealPartyToFull(RunSnapshot run)
        {
            var save = SaveSlotManager.CurrentSave;
            if (run == null || save == null) return;

            run.currentHealth ??= new List<RunHealthEntry>();

            foreach (var character in save.ActiveSquad())
            {
                if (character == null || string.IsNullOrEmpty(character.definitionId)) continue;

                int maxHealth = Content.ContentDatabase.EffectiveStats(character).maxHealth;

                var entry = run.currentHealth.Find(e => e != null && e.characterId == character.definitionId);
                if (entry == null)
                {
                    entry = new RunHealthEntry { characterId = character.definitionId };
                    run.currentHealth.Add(entry);
                }

                entry.hp = maxHealth;
            }

            SaveSlotManager.SaveCurrent();
        }

        // KEEPS A CHARACTER'S CARRIED HEALTH AT THE SAME FRACTION of a maximum
        // that just moved under it.
        //
        // The run stores current health as an ABSOLUTE number per character,
        // and the maximum it is a fraction of is computed from the character --
        // so anything that changes the maximum silently changes the fraction.
        // Equipping a +20 max health item at 50 of 100 left the run holding 50
        // against a new maximum of 120, and the bar grew a tail of empty at the
        // end that no amount of resting could fill in that fight. Taking the
        // item off again did not give it back: the old value simply clamped.
        //
        // THE FRACTION IS THE RIGHT THING TO PRESERVE rather than the delta,
        // and the argument is the equip/unequip cycle rather than taste. Adding
        // the difference to current health on the way in and clamping on the
        // way out is a healing exploit: put the item on at 50/100 for 70/120,
        // take it off for 70 clamped to 100, repeat until full. Scaling both
        // ways is symmetric, so a round trip returns exactly what it took.
        //
        // A DOWNED CHARACTER STAYS DOWN. Zero times any fraction is zero, and
        // the floor at 1 below is applied only to someone who was standing --
        // otherwise a max-health item would be a resurrection.
        public static void ScaleCarriedHealth(Character character, int previousMax)
        {
            if (character == null || previousMax <= 0) return;
            if (string.IsNullOrEmpty(character.definitionId)) return;

            var run = SaveSlotManager.CurrentSave?.activeRun;
            if (run?.currentHealth == null) return;

            var entry = run.currentHealth.Find(
                e => e != null && e.characterId == character.definitionId);

            // No entry means this character is not carrying health -- outside a
            // run, or not yet fielded. There is no fraction to preserve, and
            // the next descent starts them full anyway.
            if (entry == null || entry.hp <= 0) return;

            int newMax = Content.ContentDatabase.EffectiveStats(character).maxHealth;

            // The arithmetic lives in Domain so it can be tested without a
            // save, a run or an engine; this half knows only where the numbers
            // are kept.
            entry.hp = Domain.Progression.CarriedHealth.Rescaled(entry.hp, previousMax, newMax);
        }

        // TAKES THE BUILT FIGHT, not a party and a list of ids, and that is the
        // whole of the fix rather than a tidy-up.
        //
        // This used to be handed the ids the ROLL produced and zip them against
        // the combatants the ADAPTER produced. Those two lists are the same
        // length only while every id resolves: Build skips an unknown id in
        // place, so one squad member whose content was renamed shifts every
        // combatant after them one position left, and the next character walks
        // into the room on the missing one's carried health. Clamping to the
        // shorter list -- which is what the old code did, and what its comment
        // claimed prevented exactly this -- only stops the overrun, not the
        // transposition.
        //
        // Since BuiltFight.PartyIds is built in the same loop as Party, there is
        // no longer a mismatched pair a caller could hand over.
        public static void ApplyStartingHealth(
            FightEncounterAdapter.BuiltFight built,
            IReadOnlyDictionary<string, int> startingHealth)
        {
            if (built?.Party == null || built.PartyIds == null || startingHealth == null) return;

            var party = built.Party;
            var partyIds = built.PartyIds;

            // Still the shorter of the two, as a guard rather than as the
            // correctness argument: the two are built together and cannot
            // disagree, and an index out of range here would be a crash rather
            // than a wrong health bar.
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
