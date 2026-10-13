using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace
{
    // What happens once when a realm opens (docs/PLAN_RUN_MAP.md sections 2-4):
    // its profile is resolved, its map generated, and every fight node's
    // strength weighed against the party standing at the door. All three are
    // written to the run and frozen for the realm.
    //
    // Called from RunManager.StartRun and AdvanceLeg, from the v8 -> v9 save
    // migration, and lazily by RunManager.Map for a snapshot that never went
    // through either (realmOpenedAt != legStartStep). It does not persist: each
    // caller owns its write.
    public static class RealmOpening
    {
        public static RealmMap Open(SaveData save, RunSnapshot run)
        {
            if (run == null) return null;

            run.realmProfile = RealmProfiles.ForRealm(run.legStartStep, run.restBeforeBoss);
            run.realmOpenedAt = run.legStartStep;

            var map = MapOf(run);
            run.nodeStrength = Weigh(save, run, map, out double party);

            Debug.Log($"[RealmOpening] floor {RunDepth.FloorFor(run.legStartStep)}: party weight P = {party:F0} " +
                      $"(EV sheet reference {NodeStrength.ReferencePartyWeight:F0}); strength 1/2/3 = " +
                      $"{run.nodeStrength.Count(e => e.strength == 1)}/{run.nodeStrength.Count(e => e.strength == 2)}/" +
                      $"{run.nodeStrength.Count(e => e.strength == 3)}");
            return map;
        }

        // The realm the saved profile describes. No content read: the profile
        // is data, which is what lets a save regenerate its realm exactly.
        public static RealmMap MapOf(RunSnapshot run) =>
            RealmMapGenerator.GenerateFor(run.runSeed, run.legStartStep, run.realmProfile);

        // Strength for every fight and anchor, from the preview roll the fight
        // itself will make (RunEncounter.Preview) at the node's own step.
        private static List<NodeStrengthEntry> Weigh(SaveData save, RunSnapshot run, RealmMap map, out double party)
        {
            party = NodeStrength.Weight(FieldedBodies(save, run));
            var entries = new List<NodeStrengthEntry>();

            foreach (var node in map.Nodes)
            {
                if (node.Type != RoomType.Fight && node.Type != RoomType.EliteFight) continue;

                var roll = RunEncounter.Preview(run, node);
                int step = run.legStartStep + node.Offset;
                var bodies = roll.EnemyIds
                    .Select(id => ContentDatabase.GetEnemy(id)?.Data)
                    .Where(enemy => enemy != null)
                    .Select(enemy => FightEncounterAdapter.EnemyBody(enemy, step, roll.IsElite));

                entries.Add(new NodeStrengthEntry(node.Id, NodeStrength.Of(NodeStrength.Weight(bodies), party)));
            }

            return entries;
        }

        // The squad members a fight at the door would field.
        private static IEnumerable<FighterBody> FieldedBodies(SaveData save, RunSnapshot run)
        {
            if (save == null) yield break;

            var fielded = new HashSet<string>(RunEncounter.FightersFor(save, run, null));
            foreach (var character in save.ActiveSquad())
            {
                if (character == null || !fielded.Contains(character.definitionId)) continue;
                yield return FightEncounterAdapter.PartyBody(character);
            }
        }

        // The frozen strength of a node, 0 when it has none.
        public static int StrengthOf(RunSnapshot run, int nodeId) =>
            NodeStrengthEntry.Find(run?.nodeStrength, nodeId);
    }
}
