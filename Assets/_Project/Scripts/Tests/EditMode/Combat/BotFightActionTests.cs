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
        public void LegalActions_SoloPartyOneOnOne_OffersAttackAndMoveBackOnly()
        {
            // THE NEVER-EMPTY GUARANTEE, at its narrowest: Attack on the front
            // enemy is always legal. A one-member party used to have no Move
            // at all; with field seats (PLAN_BELLWETHER_KIT 1.1) it can step
            // back into the empty middle, and still not forward off the front.
            var (session, hero, foe) = OneOnOne();

            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Attack && a.Target == foe),
                "a lone reachable foe must offer an Attack");
            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Move && a.MoveDirection == MoveDirection.Back),
                "the empty middle seat is a legal step back");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Move && a.MoveDirection == MoveDirection.Forward),
                "nothing is in front of the front seat");
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

        // ---- a skill that asks which element it is -----------------------------

        // FOUR ELEMENTS TIMES TWO REACHABLE ENEMIES IS EIGHT DIFFERENT
        // COMMANDS, and they have to be eight entries: a policy weighing
        // "orb the beetle" against "orb the rat" is already weighing a target,
        // and which element it carries is the same kind of decision.
        [Test]
        public void LegalActions_AnElementSkill_OffersOnePerElementPerTarget()
        {
            var (session, foes) = WithKit(Orb(), foeCount: 2);

            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());
            var casts = legal.Where(a => a.Kind == FightActionKind.Skill).ToList();

            Assert.AreEqual(8, casts.Count, "four elements against two reachable enemies");
            foreach (var foe in foes)
            {
                CollectionAssert.AreEquivalent(
                    new DamageType?[] { DamageType.Earth, DamageType.Water, DamageType.Fire, DamageType.Wind },
                    casts.Where(a => a.Target == foe).Select(a => a.Element).ToList());
            }
        }

        // A CAST WITH NO ELEMENT IS NOT ON THE MENU for a skill that asks for
        // one: FightSession refuses it, so a policy that picked it would burn a
        // turn on a refusal that spends nothing and never advances the fight.
        [Test]
        public void LegalActions_AnElementSkill_NeverOffersAnElementlessCast()
        {
            var (session, _) = WithKit(Orb(), foeCount: 1);

            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Skill && a.Element == null));
        }

        // And a kit that asks nothing has exactly the list it always had --
        // one cast per reachable enemy, carrying no element.
        [Test]
        public void LegalActions_APlainSkill_IsUnchangedEntryForEntry()
        {
            var (session, foes) = WithKit(PlainBolt(), foeCount: 2);

            var casts = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>())
                .Where(a => a.Kind == FightActionKind.Skill)
                .ToList();

            Assert.AreEqual(2, casts.Count);
            CollectionAssert.AreEquivalent(foes, casts.Select(a => a.Target).ToList());
            Assert.IsTrue(casts.All(a => a.Element == null));
        }

        [Test]
        public void Apply_AnElementSkill_CastsItAsTheElementTheActionNames()
        {
            var (session, foes) = WithKit(Orb(), foeCount: 1);
            int before = foes[0].CurrentHealth;

            FightAction.Apply(session, new FightAction(
                FightActionKind.Skill, foes[0], skillIndex: 0, element: DamageType.Fire));

            Assert.Less(foes[0].CurrentHealth, before,
                "an element that never reached CastSkill would be refused, and the cast would land nothing");
            StringAssert.Contains("Fire",
                string.Join(" ", session.DrainBeats().SelectMany(b => b.Messages)),
                "the log names the element the cast actually carried");
        }

        // The debug form has to show it, or two of the eight actions above read
        // identically in a trace.
        [Test]
        public void ToString_NamesTheElementWhenThereIsOne()
        {
            var foe = Fighter("Foe", false);

            StringAssert.Contains("Fire", new FightAction(
                FightActionKind.Skill, foe, skillIndex: 0, element: DamageType.Fire).ToString());
            StringAssert.DoesNotContain(":", new FightAction(
                FightActionKind.Skill, foe, skillIndex: 0).ToString(),
                "a skill that asks nothing keeps the shape it had");
        }

        // ---- fixtures for the two skills above ---------------------------------

        private static ResolvedSkill Resolve(RawSkillEntry raw)
        {
            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        private static ResolvedSkill Orb() => Resolve(new RawSkillEntry
        {
            id = "prismatic_orb", displayName = "Prismatic Orb", characterId = "owl",
            effect = "DamageSingle", manaCost = 8,
            damageInstances = new[] { new RawDamageInstance { type = "Earth", amount = 16 } },
            elements = new[]
            {
                new RawElementChoice { type = "Earth" },
                new RawElementChoice { type = "Water" },
                new RawElementChoice { type = "Fire" },
                new RawElementChoice { type = "Wind" },
            },
        });

        private static ResolvedSkill PlainBolt() => Resolve(new RawSkillEntry
        {
            id = "plain_bolt", displayName = "Plain Bolt", characterId = "owl",
            effect = "DamageSingle", manaCost = 8,
            damageInstances = new[] { new RawDamageInstance { type = "Arcane", amount = 16 } },
        });

        // One caster with `skill` and nothing else, against `foeCount` enemies
        // with no reach restriction between them, so the legal list is exactly
        // the fan-out being counted.
        private static (FightSession session, List<CombatantState> foes) WithKit(
            ResolvedSkill skill, int foeCount)
        {
            var hero = new CombatantState("Odette", true, 500, 999, 20, 10);
            var foes = Enumerable.Range(0, foeCount)
                .Select(i => new CombatantState($"Foe{i}", false, 500, 0, 1, 1))
                .ToList();

            var kit = new PlayerKit("owl", CharacterRole.Utility,
                new List<ResolvedSkill> { skill }, new List<ResolvedRelic>(), null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, foes.ToArray()),
                new List<PlayerKit> { kit }, null, new SeededRandom(7)) { DamageVarianceRange = 0f };
            session.Begin();

            return (session, foes);
        }
    }
}
