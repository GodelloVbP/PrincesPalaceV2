using System.Collections.Generic;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Dungeon
{
    // One enemy in the pool a room can draw from, reduced to the three facts
    // the roll actually needs.
    //
    // A projection rather than the EnemyDefinition itself, because that type is
    // a ScriptableObject living in Core and Domain has noEngineReferences. The
    // same reason InventoryOps moved out of Data: a rule that cannot be reached
    // from an EditMode test is a rule nothing checks.
    public readonly struct EnemyCandidate
    {
        public readonly string Id;
        public readonly bool IsBoss;
        public readonly bool AvoidsFrontSlot;

        // See RawEnemyEntry.minFloor. Carried here rather than filtered by the
        // caller so the rule lives with the roll it constrains -- every path
        // into a room goes through Roll, and only one of them builds the pool.
        public readonly int MinFloor;

        public EnemyCandidate(string id, bool isBoss = false, bool avoidsFrontSlot = false, int minFloor = 1)
        {
            Id = id;
            IsBoss = isBoss;
            AvoidsFrontSlot = avoidsFrontSlot;
            MinFloor = minFloor < 1 ? 1 : minFloor;
        }
    }

    // What a room fields, and who from the squad is standing up to meet it.
    //
    // Pure and seeded: the same room in the same run always rolls the same
    // enemies. That property is the whole point of keying the stream to
    // (runSeed, step, nodeId) rather than to a fight counter -- position
    // carries the state, so quitting mid-fight and reloading finds the same
    // monsters rather than a reroll for an easier draw.
    public static class EncounterRoll
    {
        // An elite room fields exactly two. Normal rooms field one or two.
        // Both numbers are v1's, kept rather than re-tuned: this is a port of
        // a rule that was playtested, and changing the shape of encounters
        // while restoring them would make any regression impossible to
        // attribute to one or the other.
        //
        // NEITHER MAY EXCEED THE STAGE, and that ceiling is not stated here --
        // see FightHudSpec.StageSlotsPerSide. Raising either of these past it
        // would field a monster with no slot to stand in, which is invisible
        // and still swinging rather than merely undrawn. Clamped in Roll and
        // asserted, so the two numbers cannot drift apart quietly.
        public const int EliteEnemyCount = 2;
        public const int NormalMinEnemies = 1;
        public const int NormalMaxEnemiesExclusive = 3;

        public sealed class Result
        {
            public IReadOnlyList<string> EnemyIds { get; }
            public bool IsBoss { get; }
            public bool IsElite { get; }

            public Result(IReadOnlyList<string> enemyIds, bool isBoss, bool isElite)
            {
                EnemyIds = enemyIds;
                IsBoss = isBoss;
                IsElite = isElite;
            }
        }

        // declaredBossId is the run's own bossEnemyId -- the enemy the descent
        // was sent to kill. Preferred over a fresh roll so the boss the map
        // promised is the boss that appears, and so RecordBossKill credits the
        // thing the run was actually about.
        // `floor` is the shallowest-first depth band -- see EnemyCandidate.
        // Defaulted to a depth that admits everything, so the existing tests and
        // any caller that does not care about banding read exactly as before.
        public static Result Roll(
            RoomType roomType,
            IReadOnlyList<EnemyCandidate> pool,
            SeededRandom rng,
            string declaredBossId = null,
            int floor = int.MaxValue)
        {
            if (pool == null || pool.Count == 0 || rng == null)
            {
                return new Result(new List<string>(), isBoss: false, isElite: false);
            }

            var bosses = new List<EnemyCandidate>();
            var regulars = new List<EnemyCandidate>();

            foreach (var candidate in pool)
            {
                if (string.IsNullOrEmpty(candidate.Id)) continue;
                if (candidate.MinFloor > floor) continue;
                (candidate.IsBoss ? bosses : regulars).Add(candidate);
            }

            // NOTHING IN BAND IS NOT AN EMPTY ROOM. A content set whose
            // shallowest enemy starts at floor 3 would otherwise field an empty
            // stage on floors 1 and 2, and an empty stage is a soft lock rather
            // than an easy fight. Falling back to the whole pool is the same
            // graceful-degradation posture the boss fallback below takes.
            if (bosses.Count == 0 && regulars.Count == 0)
            {
                foreach (var candidate in pool)
                {
                    if (string.IsNullOrEmpty(candidate.Id)) continue;
                    (candidate.IsBoss ? bosses : regulars).Add(candidate);
                }
            }

            if (roomType == RoomType.Boss && bosses.Count > 0)
            {
                return new Result(new List<string> { ResolveBoss(bosses, rng, declaredBossId) },
                    isBoss: true, isElite: false);
            }

            bool isElite = roomType == RoomType.EliteFight;

            // Falling back to the whole pool when nothing is a regular means a
            // content set of bosses only still fights, rather than presenting
            // an empty stage. Graceful degradation, house style.
            var drawFrom = regulars.Count > 0 ? regulars : new List<EnemyCandidate>(pool);
            if (drawFrom.Count == 0)
            {
                return new Result(new List<string>(), isBoss: false, isElite: isElite);
            }

            int count = isElite
                ? EliteEnemyCount
                : rng.NextInt(NormalMinEnemies, NormalMaxEnemiesExclusive);

            // The stage's capacity is the hard ceiling, whatever the tuning
            // above says. Clamped rather than trusted: this is the line that
            // keeps "how many monsters a room rolls" and "how many the stage
            // can show" from becoming two independent facts.
            if (count > FightHudSpec.StageSlotsPerSide) count = FightHudSpec.StageSlotsPerSide;

            // WITH replacement, deliberately: two of the same monster is a
            // legitimate room and v1 drew this way. Drawing without it would
            // also silently cap a room at the pool size, which is a different
            // rule arriving by accident.
            var picks = new List<EnemyCandidate>();
            for (int i = 0; i < count; i++)
            {
                picks.Add(drawFrom[rng.NextInt(0, drawFrom.Count)]);
            }

            PlaceAvoidingFrontSlot(picks);

            var ids = new List<string>(picks.Count);
            foreach (var pick in picks) ids.Add(pick.Id);

            return new Result(ids, isBoss: false, isElite: isElite);
        }

        // The run's declared boss if it is actually in the pool, else a roll.
        //
        // A declared id that no longer resolves is a content gap, not a reason
        // to field nothing -- the same posture BossIdOf takes on the way back
        // out when a boss fight ends with no declared id.
        private static string ResolveBoss(
            IReadOnlyList<EnemyCandidate> bosses, SeededRandom rng, string declaredBossId)
        {
            if (!string.IsNullOrEmpty(declaredBossId))
            {
                foreach (var boss in bosses)
                {
                    if (boss.Id == declaredBossId) return boss.Id;
                }
            }

            return bosses[rng.NextInt(0, bosses.Count)].Id;
        }

        // Stage slot IS list order -- the adapter appends in order,
        // CombatEncounter preserves it, and the beat player indexes the enemy
        // sprites by it. So reordering here, before anything becomes a
        // combatant, is the only place this preference needs to live.
        //
        // A preference, not a rule: when every pick avoids the front there is
        // nothing sensible to swap with and this is a silent no-op, rather
        // than content authoring being able to deadlock the stage.
        private static void PlaceAvoidingFrontSlot(List<EnemyCandidate> picks)
        {
            if (picks.Count < 2 || !picks[0].AvoidsFrontSlot) return;

            for (int i = 1; i < picks.Count; i++)
            {
                if (!picks[i].AvoidsFrontSlot)
                {
                    (picks[0], picks[i]) = (picks[i], picks[0]);
                    return;
                }
            }
        }

        // Who from the squad can stand up.
        //
        // A character with no health entry has never been hurt this run and
        // fields at full. One recorded at 0 or less was knocked out earlier and
        // sits this fight out -- it is downed, not absent, which is why
        // RewardApplier still pays it and the Reckoning still lists it.
        //
        // Returning an empty list is a squad wipe. The caller decides what that
        // means; this does not silently substitute someone to avoid it, because
        // an unloseable run is exactly the bug AUDIT #12 recorded when a
        // full-HP party was built for a selection that seeded no health at all.
        public static IReadOnlyList<string> FieldableParty(
            IReadOnlyList<string> squadIds,
            IReadOnlyDictionary<string, int> currentHealth)
        {
            var fieldable = new List<string>();
            if (squadIds == null) return fieldable;

            foreach (string id in squadIds)
            {
                if (string.IsNullOrEmpty(id)) continue;

                if (currentHealth != null
                    && currentHealth.TryGetValue(id, out int hp)
                    && hp <= 0)
                {
                    continue;
                }

                fieldable.Add(id);
            }

            // DELIBERATELY UNCAPPED, unlike the enemy roll above.
            //
            // The same ceiling applies -- a hero with no slot fights from off
            // screen exactly as a monster would -- but the fix is not to drop
            // one here. Nobody notices two monsters instead of three; a player
            // whose recruited character silently fails to turn up notices very
            // much, and choosing WHICH of their squad sits out is a design
            // decision, not a clamp.
            //
            // So the mismatch is made unshippable instead: EncounterRollTests
            // holds SaveData's own maximum squad against the stage's slots, and
            // buying the roster past it fails there with somewhere to put the
            // decision.
            return fieldable;
        }
    }
}
