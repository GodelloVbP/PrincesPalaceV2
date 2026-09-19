using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // VIPER'S BITE, the one milestone A spell whose whole point is an ordering:
    // the Poison-typed packet detonates what the target already carries, and
    // only then does the spell's own fresh Poison land. Two things could go
    // wrong quietly and both are pinned here -- a bite that detonates one
    // instance of three and leaves the rest ticking, and a bite that re-detonates
    // the Poison it just applied.
    //
    // Driven through CastSkill rather than through the resolution methods,
    // because the ordering being tested IS the dispatcher's (plan appendix B).
    public class VipersBiteTests
    {
        private static CombatantState Hero(string name = "Hero", int health = 500, int mana = 50) =>
            new CombatantState(name, true, health, mana, 40, 10);

        private static CombatantState Foe(string name = "Foe", int health = 1000) =>
            new CombatantState(name, false, health, 10, 5, 1);

        // The content row, as skills.json authors it: one Poison packet of 3,
        // then Poison 3 for 3 ticks on a survivor.
        private static ResolvedSkill Bite() =>
            new ResolvedSkill("vipers_bite", "Viper's Bite", "Detonates existing Poison, then poisons again.",
                "hero", 1, SkillEffect.DamageSingle, SkillTargeting.SingleEnemy,
                7, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Poison, 3) }, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 3, statusDuration: 3);

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            CombatantState foe = null, params ModifierEffect[] foeEffects)
        {
            var hero = Hero();
            foe = foe ?? Foe();
            if (foeEffects.Length > 0) foe.ModifierEffects = new ModifierEffectSet(foeEffects);

            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Bite() }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
            };
            return (session, hero, foe);
        }

        // The packet's own damage, measured against a target carrying nothing,
        // so every figure below is the packet plus a detonation rather than an
        // assumption about what the packet is worth.
        private static int PacketDamage()
        {
            var (session, _, foe) = Fight();
            int before = foe.CurrentHealth;
            session.CastSkill(0, foe);
            return before - foe.CurrentHealth;
        }

        [Test]
        public void ABiteOnAPoisonedTarget_DetonatesOnce_ThenLeavesExactlyOneFreshPoison()
        {
            int packet = PacketDamage();

            var (session, _, foe) = Fight();
            // 4 x 5 = 20, deliberately unlike the spell's own 3 x 3 so a wrong
            // order would read completely differently.
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before - packet - 20, foe.CurrentHealth,
                "the packet plus exactly the old Poison's remaining worth");

            var poison = foe.Statuses.Single(s => s.Type == StatusEffectType.Poison);
            Assert.AreEqual(3, poison.Magnitude, "the spell's own fresh Poison, standing alone");
            Assert.AreEqual(3, poison.TurnsRemaining,
                "nothing merged -- the old entry was gone before this one landed");
        }

        // THE CASE STACKING INTRODUCES. Three instances, one detonation, and
        // nothing left ticking but the fresh one.
        [Test]
        public void ABiteOnATripleStackedTarget_DetonatesAllThree_AndLeavesOneFreshPoison()
        {
            int packet = PacketDamage();

            var (session, _, foe) = Fight();
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));   // 20
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 2, 3));   // 6
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 7, 1));   // 7
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before - packet - 33, foe.CurrentHealth,
                "20 + 6 + 7 -- every instance, not the first one");

            var poison = foe.Statuses.Where(s => s.Type == StatusEffectType.Poison).ToList();
            Assert.AreEqual(1, poison.Count, "the survivors of a partial detonation would show up here");
            Assert.AreEqual(3, poison[0].Magnitude);
            Assert.AreEqual(3, poison[0].TurnsRemaining);
        }

        // RECORDED ONCE, however many instances it ate. Three instances
        // detonated as three DealDamage calls would put the same figure through
        // the ledger three times over, and the total the player is shown would
        // still look plausible.
        [Test]
        public void ABiteOnATripleStackedTarget_RecordsOneDetonationRow()
        {
            int packet = PacketDamage();

            var (session, _, foe) = Fight();
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 2, 3));
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 7, 1));

            session.CastSkill(0, foe);

            // The fixture passes no enemy kits, so the ledger keys this foe by
            // its display name -- LedgerIdOf's documented last fallback.
            var line = session.Ledger.For("Foe");
            Assert.AreEqual(packet + 33, line.DamageTaken,
                "the ledger must carry the packet and ONE detonation, not one row per instance");
        }

        [Test]
        public void ADodgedBite_DetonatesNothing_AndAppliesNothing()
        {
            // No finite dodge rating is a guarantee (CombatMath.DodgePercentFrom),
            // so this is the same absurd figure DodgeCoversEveryDamagePathTests
            // uses to make the roll certain for this seed.
            var (session, _, foe) = Fight(null, new ModifierEffect(ModifierEffectType.DodgeRating, 100_000));
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before, foe.CurrentHealth, "a dodged bite deals nothing and detonates nothing");

            var poison = foe.Statuses.Single(s => s.Type == StatusEffectType.Poison);
            Assert.AreEqual(4, poison.Magnitude, "the old Poison must be untouched");
            Assert.AreEqual(5, poison.TurnsRemaining);
        }

        [Test]
        public void ALethalBite_AppliesNoFreshPoison()
        {
            var (session, _, foe) = Fight(Foe("Frail", health: 2));

            session.CastSkill(0, foe);

            Assert.IsFalse(foe.IsAlive, "fixture check: the bite should have killed it");
            CollectionAssert.IsEmpty(foe.Statuses.Where(s => s.Type == StatusEffectType.Poison).ToList(),
                "a corpse takes no fresh status -- ApplySkillStatus is gated on IsAlive");
        }

        // The tooltip the owner asked for, word for word, read out of
        // skills.json rather than off the fixture above -- asserting the
        // fixture's own string against itself would pass whatever the game
        // actually ships.
        [Test]
        public void TheTooltipInContentSaysExactlyWhatTheOwnerAskedFor()
        {
            string body = System.IO.File.ReadAllText(ContentDataFiles.DataPath("skills.json"));
            var blocks = JsonBlocks.ObjectsInArray(body, "skills");
            Assert.Greater(blocks.Count, 20, "vacuity guard: skills.json was not read at all");

            string bite = blocks.FirstOrDefault(b => JsonBlocks.String(b, "id") == "vipers_bite");
            Assert.IsNotNull(bite, "skills.json no longer has a row called 'vipers_bite'");

            Assert.AreEqual("Detonates existing Poison, then poisons again.",
                JsonBlocks.String(bite, "description"));
        }

        // AND THE OTHER TWO MILESTONE A ROWS ARE THERE, with the fields their
        // mechanics actually depend on. Winter's Rebuke's appliesStatus is the
        // first Chilled any content has ever authored, which is the whole
        // reason plan D6's seam exists.
        [Test]
        public void TheThreeMilestoneASpellsAreAuthored_WithTheFieldsTheirMechanicsNeed()
        {
            string body = System.IO.File.ReadAllText(ContentDataFiles.DataPath("skills.json"));
            var byId = JsonBlocks.ObjectsInArray(body, "skills")
                .Where(b => !string.IsNullOrWhiteSpace(JsonBlocks.String(b, "id")))
                .ToDictionary(b => JsonBlocks.String(b, "id"));

            Assert.Greater(byId.Count, 20, "vacuity guard: skills.json was not read at all");

            foreach (string id in new[] { "gilded_aegis", "winters_rebuke", "vipers_bite" })
            {
                Assert.IsTrue(byId.ContainsKey(id), $"skills.json has no row called '{id}'");
                Assert.AreEqual(true, JsonBlocks.Bool(byId[id], "bookOnly"), $"'{id}' must be a book");
                Assert.Greater(JsonBlocks.Number(byId[id], "bookTier") ?? 0, 0,
                    $"'{id}' must carry a shop tier, or it is a book nothing can sell");
                Assert.AreEqual("sheep", JsonBlocks.String(byId[id], "characterId"));
            }

            Assert.AreEqual("Chilled", JsonBlocks.String(byId["winters_rebuke"], "appliesStatus"),
                "the first content row in the game to author Chilled");
            Assert.AreEqual(2, (int)(JsonBlocks.Number(byId["winters_rebuke"], "statusDuration") ?? 0),
                "two of the target's turns fully affected, on the turn-end clock");
            Assert.AreEqual(2, (int)(JsonBlocks.Number(byId["gilded_aegis"], "wardTurns") ?? 0),
                "two of the wearer's own turns");
        }
    }
}
