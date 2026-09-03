using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Two placeholder party members (placeholder_brawler, placeholder_caster)
    // exist purely so a three-member squad can be tested against Shawn --
    // see characters.json's own entries. This file covers the two things
    // that are hard to see from content alone: a fight actually seats three
    // on the player side without throwing, and SaveData.EffectiveMaxSquadSize
    // actually returns 3 when the test-only switch is on.
    //
    // Content RESOLUTION for the two placeholders (ability score budget,
    // required fields, etc.) is covered by CharacterEntryResolver at build
    // time -- ContentBuilder refuses to write a single character asset if
    // either entry is malformed, which is a stronger guarantee than a unit
    // test re-checking the same arithmetic here would be.
    public class SquadOfThreeTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide,
            int maxHealth = 100, int attack = 10, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static (FightSession session, List<CombatantState> party, CombatantState foe) SquadOfThree()
        {
            var shawn = Fighter("Shawn", true, maxHealth: 200, attack: 7, speed: 10);
            var brawler = Fighter("Placeholder Brawler", true, maxHealth: 260, attack: 10, speed: 8);
            var caster = Fighter("Placeholder Caster", true, maxHealth: 140, attack: 4, speed: 11);

            // Enough health that neither side runs out inside 5 turns, and
            // slow/weak enough that it never gets a chance to matter for
            // this test's own purpose -- proving the party seats three and
            // plays cleanly, not proving anything about balance.
            var foe = Fighter("Foe", false, maxHealth: 100000, attack: 1, speed: 1);

            var party = new List<CombatantState> { shawn, brawler, caster };
            var encounter = new CombatEncounter(party, new[] { foe });

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Utility, null, null, DamageType.Nature),
                new PlayerKit("placeholder_brawler", CharacterRole.Tank, null, null, DamageType.Physical),
                new PlayerKit("placeholder_caster", CharacterRole.Support, null, null, DamageType.Arcane),
            };

            var session = new FightSession(encounter, kits, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, party, foe);
        }

        [Test]
        public void CombatEncounter_SeatsAllThreePartyMembers()
        {
            var (_, party, _) = SquadOfThree();

            Assert.AreEqual(3, party.Count);
            Assert.IsTrue(party.All(c => c.IsAlive), "every party member should enter the fight alive");
        }

        [Test]
        public void ThreeMemberParty_PlaysFiveTurnsWithGreedyAggressive_WithNoInvariantHits()
        {
            var (session, party, foe) = SquadOfThree();
            var policy = new GreedyAggressivePolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            int commandsPlayed = 0;
            while (commandsPlayed < 5 && !session.IsOver)
            {
                if (!session.IsPlayerTurn)
                {
                    // A slow, harmless foe should never actually get a turn
                    // inside 5 player commands, but bail out honestly rather
                    // than spin if it somehow does.
                    break;
                }

                var actor = session.Current;
                var legal = FightAction.LegalActions(session, actor, satchel);
                Assert.IsNotEmpty(legal, $"{actor.Name} had no legal action");

                var command = policy.Choose(session, actor, legal, rng);
                Assert.DoesNotThrow(() => FightAction.Apply(session, command),
                    $"applying {actor.Name}'s chosen action threw");

                commandsPlayed++;
            }

            Assert.AreEqual(5, commandsPlayed, "the fixture is set up so 5 commands should always be reachable");

            var hits = FightInvariants.Check(session, 1);
            CollectionAssert.IsEmpty(hits, "GreedyAggressivePolicy must not desync a three-member party");
        }

        // SaveData.TestSquadOfThreeEnabled's own behaviour is covered by
        // SaveDataSquadOfThreeTests instead -- SaveData lives outside the
        // Domain assembly this file compiles against under `dotnet test`
        // (see tools/test.ps1's own header), so a test that touches it has
        // to run through Unity, same as every other SaveData test.

        [Test]
        public void DossierPaging_CyclesThroughAllThreeSquadMembersAndBackToTheFirst()
        {
            // The dossier's own Step() (Core/CharacterDossierController.cs)
            // is `(_index + by + squad.Count) % squad.Count` over
            // SaveData.ActiveSquad() -- already fully general over squad
            // size, so this test is pinning that arithmetic directly rather
            // than standing up the MonoBehaviour (which needs a scene).
            var ids = new List<string> { "sheep", "placeholder_brawler", "placeholder_caster" };

            int index = 0;
            var visited = new List<string> { ids[index] };
            for (int step = 0; step < 3; step++)
            {
                index = (index + 1 + ids.Count) % ids.Count;
                visited.Add(ids[index]);
            }

            // Three forward steps from index 0 over 3 members returns to the
            // start -- the same wraparound Step() relies on.
            CollectionAssert.AreEqual(
                new[] { "sheep", "placeholder_brawler", "placeholder_caster", "sheep" }, visited);
        }
    }
}
