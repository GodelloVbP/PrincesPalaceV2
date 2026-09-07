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
    // FightAction.LegalActions -- the menu a policy is asked to pick from.
    public class BotFightActionTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        // A party of any size against any enemies, kits for nobody -- the
        // multi-member shape the Move rows need. OneOnOne below is the same
        // build with one of each and a kit, kept as it was so the tests that
        // did not care about formation read unchanged.
        private static FightSession Party(CombatantState[] party, CombatantState[] foes)
        {
            var session = new FightSession(
                new CombatEncounter(party, foes), null, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return session;
        }

        private static (FightSession session, CombatantState hero, CombatantState foe) OneOnOne(
            int foeHealth = 100, int heroSpeed = 10)
        {
            var hero = Fighter("Hero", true, speed: heroSpeed);
            var foe = Fighter("Foe", false, maxHealth: foeHealth, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foe);
        }

        [Test]
        public void LegalActions_SoloPartyOneOnOne_OffersAttackAndNoMoveAtAll()
        {
            // THE NEVER-EMPTY GUARANTEE, at its narrowest. A one-member party
            // has nowhere to step, so neither Move is legal -- and the list is
            // still non-empty because Attack on the front enemy always is.
            // This is exactly the case Hold Back used to cover for free.
            var (session, hero, foe) = OneOnOne();

            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Attack && a.Target == foe),
                "a lone reachable foe must offer an Attack");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Move),
                "a solo party has no ally to trade places with, in either direction");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Skill),
                "no skills were granted, so none should be offered");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Item),
                "an empty satchel offers nothing");
        }

        [Test]
        public void LegalActions_WithAnAllyBehind_OffersMoveBackOnly()
        {
            var front = Fighter("Front", true, speed: 10);
            var behind = Fighter("Behind", true, speed: 1);
            var foe = Fighter("Foe", false, speed: 1);
            var session = Party(new[] { front, behind }, new[] { foe });

            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.AreSame(front, session.Current, "the fast one opens");
            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Move
                                         && a.MoveDirection == MoveDirection.Back),
                "there is an ally behind to trade places with");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Move
                                          && a.MoveDirection == MoveDirection.Forward),
                "nobody stands in front of the front rank");
        }

        [Test]
        public void LegalActions_WithASatchelStack_OffersItOnlyWhenItsCountIsPositive()
        {
            var (session, hero, foe) = OneOnOne();
            var satchel = new List<SatchelStack>
            {
                new SatchelStack("potion", "Potion", 2, false),
                new SatchelStack("elixir", "Elixir", 0, true),
            };

            var legal = FightAction.LegalActions(session, session.Current, satchel);

            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Item && a.ItemId == "potion"),
                "a stack with count > 0 must be offered");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Item && a.ItemId == "elixir"),
                "a stack with count 0 must not be offered");
        }

        [Test]
        public void Apply_Move_SwapsTheFormationAndEndsTheTurn()
        {
            var front = Fighter("Front", true, speed: 10);
            var behind = Fighter("Behind", true, speed: 1);
            var foe = Fighter("Foe", false, maxHealth: 1000, speed: 1);
            var session = Party(new[] { front, behind }, new[] { foe });

            FightAction.Apply(session, new FightAction(FightActionKind.Move,
                moveDirection: MoveDirection.Back));

            Assert.AreSame(behind, session.Encounter.PlayerParty[0], "the ally took the front slot");
            Assert.AreSame(front, session.Encounter.PlayerParty[1]);
            Assert.AreEqual(1, session.Encounter.LivingRankOf(front), "and the mover is now rank 1");
        }

        [Test]
        public void LastResort_PrefersAttackAndNeverThrows()
        {
            // The two greedy policies both end on this. Hold Back used to be
            // unconditionally legal and they used to end on
            // First(a => a.Kind == HoldBack), which throws the day that stops
            // being true. It has stopped being true.
            var (session, hero, foe) = OneOnOne();
            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.AreEqual(FightActionKind.Attack, FightAction.LastResort(legal).Kind);
            Assert.AreEqual(FightActionKind.Item,
                FightAction.LastResort(new List<FightAction>
                {
                    new FightAction(FightActionKind.Item, hero, itemId: "potion"),
                }).Kind,
                "with no Attack on the menu it takes the first entry rather than throwing");
        }

        [Test]
        public void Apply_Attack_DealsDamageToTheTarget()
        {
            var (session, hero, foe) = OneOnOne();
            int before = foe.CurrentHealth;

            FightAction.Apply(session, new FightAction(FightActionKind.Attack, foe));

            Assert.Less(foe.CurrentHealth, before, "an attack must land some damage on its target");
        }
    }
}
