using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // Caravan fakes in a fight and on the body (docs/PLAN_EVENTS_BELL_AND_
    // CARAVAN.md 1.5, M4). A fake consumable is used up for nothing and costs
    // the turn; the bot's local satchel spends exactly the stack it picked;
    // fake gear counts down one completed fight at a time and breaks on the
    // third. Fixture instances throughout -- the caravan that sells them is M5.
    public class FakeItemFightTests
    {
        private const string FakeLine = "This is of such poor quality... it's a fake.";

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(int foeHealth = 5000)
        {
            var hero = new CombatantState("Shawn", true, 300, 30, 20, 10);
            // Speed 9 against 10, so the monster replies to every action and
            // "the turn was spent" is observable.
            var foe = new CombatantState("Rat", false, foeHealth, 10, 5, 9);

            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null);

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(4))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foe);
        }

        private static ItemInstance Lot(string id, string lot, bool fake) =>
            new ItemInstance(id, provenance: new Provenance(lot, fake));

        // ---- the fake consumable --------------------------------------------------------

        [Test]
        public void AFakePotion_IsUsed_HealsNothing_AndSaysItIsAFake()
        {
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;

            bool used = session.UseConsumable("Health Potion", 60, restoresMana: false, isFake: true);
            var beats = session.DrainBeats();

            Assert.IsTrue(used, "true is what tells the caller to spend it");
            Assert.AreEqual(100, beats.First().Snapshot[hero].Health);
            var lines = beats.SelectMany(b => b.Messages).ToList();
            CollectionAssert.Contains(lines, "Shawn uses Health Potion.");
            CollectionAssert.Contains(lines, FakeLine);
        }

        [Test]
        public void AFakePotion_CostsTheTurn()
        {
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;

            session.UseConsumable("Health Potion", 60, restoresMana: false, isFake: true);

            Assert.IsTrue(session.DrainBeats().Any(b => b.IsAction && !b.Actor.IsPlayerSide),
                "the monster got its turn, so the fake cost one");
        }

        [Test]
        public void AFakeManaPotion_OnAPoolThatRefusesMana_IsRefusedExactlyAsAGenuineOne()
        {
            var (session, hero, _) = Fight();
            var fury = new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 15, gainOnDamageTaken: 10);
            fury.RestoredByManaEffects = false;
            hero.PrimaryPool = fury;

            Assert.IsFalse(session.UseConsumable("Mana Potion", 40, restoresMana: true, isFake: true));
            Assert.IsFalse(session.DrainBeats().Any(), "no beat, no line, no turn");
        }

        // ---- the bot's local satchel ------------------------------------------------------

        // Picks the fake if it is on offer, then any other item, then swings.
        private sealed class FakeFirst : IFightPolicy
        {
            public FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal,
                SeededRandom rng)
            {
                var items = legal.Where(a => a.Kind == FightActionKind.Item).ToList();
                var fake = items.Where(a => a.ItemInstance != null && a.ItemInstance.IsFake).ToList();
                if (fake.Count > 0) return fake[0];
                if (items.Count > 0) return items[0];
                return legal.First(a => a.Kind == FightActionKind.Attack);
            }
        }

        [Test]
        public void TheBot_SpendsExactlyThePickedStack_FakeThenGenuine_EachOnce()
        {
            var (session, hero, _) = Fight(foeHealth: 200);
            hero.CurrentHealth = 100;
            var satchel = new List<SatchelStack>
            {
                new SatchelStack("health_potion", "Health Potion", 1, false, Lot("health_potion", "lot-g", fake: false)),
                new SatchelStack("health_potion", "Health Potion", 1, false, Lot("health_potion", "lot-f", fake: true)),
            };
            var spent = new List<ItemInstance>();

            var hits = FightRunner.Play(session, new FakeFirst(), satchel, new SeededRandom(9), new FightTrace(),
                spent.Add);

            Assert.AreEqual(2, spent.Count, "one press per stack, and no more");
            Assert.AreEqual("lot-f", spent[0].Lot);
            Assert.IsTrue(spent[0].IsFake);
            Assert.AreEqual("lot-g", spent[1].Lot);
            Assert.IsFalse(spent[1].IsFake);
            Assert.IsEmpty(hits, string.Join("; ", hits.Select(h => h.Name + ": " + h.Detail)));
        }

        // ---- fake gear wearing out ----------------------------------------------------

        [Test]
        public void AWornFake_BreaksOnItsThirdCompletedFight_NotItsSecond()
        {
            var worn = new EquipmentLoadout();
            worn.Put(EquipmentSlot.Torso, new ItemInstance("leather_torso_p0", provenance: FakeWear.NewFakeGear("lot-a")));

            Assert.IsEmpty(FakeWear.WearOneFight(worn));
            Assert.AreEqual(2, worn.GetInstance(EquipmentSlot.Torso).FightsLeft);

            Assert.IsEmpty(FakeWear.WearOneFight(worn));
            Assert.AreEqual(1, worn.GetInstance(EquipmentSlot.Torso).FightsLeft);
            Assert.AreEqual("leather_torso_p0", worn.Get(EquipmentSlot.Torso), "still worn after the second");

            var broken = FakeWear.WearOneFight(worn);

            Assert.AreEqual(1, broken.Count);
            Assert.AreEqual("leather_torso_p0", broken[0].ItemId);
            Assert.AreEqual("lot-a", broken[0].Lot);
            Assert.AreEqual("", worn.Get(EquipmentSlot.Torso), "gone, not returned anywhere");
        }

        [Test]
        public void WearingOut_LeavesGenuineGearAlone()
        {
            var worn = new EquipmentLoadout();
            worn.Put(EquipmentSlot.Torso, new ItemInstance("leather_torso_p0", provenance: new Provenance("lot-b")));
            worn.Set(EquipmentSlot.Head, "leather_coif_p0", plus: 2);

            for (int i = 0; i < 5; i++) Assert.IsEmpty(FakeWear.WearOneFight(worn));

            Assert.AreEqual("leather_torso_p0", worn.Get(EquipmentSlot.Torso));
            Assert.AreEqual(0, worn.GetInstance(EquipmentSlot.Torso).FightsLeft);
            Assert.AreEqual(2, worn.GetPlus(EquipmentSlot.Head));
        }

        [Test]
        public void AWornOutFake_IsGearOnly()
        {
            Assert.IsTrue(FakeWear.IsWornOut(new Provenance("lot-a", fake: true, fightsLeft: 0), isGear: true));
            Assert.IsFalse(FakeWear.IsWornOut(new Provenance("lot-a", fake: true, fightsLeft: 1), isGear: true));
            Assert.IsFalse(FakeWear.IsWornOut(new Provenance("lot-a", fake: true, fightsLeft: 0), isGear: false),
                "a fake consumable carries no countdown");
            Assert.IsFalse(FakeWear.IsWornOut(new Provenance("lot-a", fake: false, fightsLeft: 0), isGear: true));
        }

        [Test]
        public void TheBreakNotice_ReadsAsTheMerchantSaysIt()
        {
            Assert.AreEqual("Ragged Leather Torso falls apart.", FakeWear.BreakLine("Ragged Leather Torso"));
            Assert.AreEqual("No refunds.", FakeWear.NoRefundsLine);
        }
    }
}
