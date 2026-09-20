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
    // A PREVIEW MUTATES NOTHING AND LIES ABOUT NOTHING -- two properties, and
    // the second one had been quietly false.
    //
    // PreviewSkillPower's fixed-packet branch multiplied by the caster's tier
    // and spell scaling but omitted ElementalDamagePercent, which
    // ResolveDamageInstances applies. A caster wearing a +Fire item was shown
    // one figure on the power row and dealt another, and nothing compared the
    // two, so the divergence could not be noticed from either side. Found
    // reading the code for plan 1.13 rather than from a bug report, which is
    // why the pin is written as a COMPARISON of the two paths and not as a
    // literal either one could drift away from on its own.
    //
    // The three milestone A spells sit here as the cast set, extended per
    // milestone as the plan's test matrix says.
    public class PreviewPurityTests
    {
        private static CombatantState Hero(int mana = 50) =>
            new CombatantState("Hero", true, 500, mana, 40, 10);

        private static CombatantState Foe(int health = 1000) =>
            new CombatantState("Foe", false, health, 10, 5, 1);

        private static ResolvedSkill Packet(string id, DamageType type, int amount,
            StatusEffectType? status = null, int magnitude = 0, int duration = 0) =>
            new ResolvedSkill(id, id, "", "hero", 1, SkillEffect.DamageSingle, SkillTargeting.SingleEnemy,
                0, 0, false, 0, 0, false,
                new[] { new DamageInstance(type, amount) }, SpellPresentation.None, 0,
                appliesStatus: status, statusMagnitude: magnitude, statusDuration: duration);

        private static ResolvedSkill Aegis() =>
            new ResolvedSkill("gilded_aegis", "Gilded Aegis", "", "hero", 1, SkillEffect.Ward,
                SkillTargeting.SingleAlly, 0, 0, false, 0, 18, false,
                null, SpellPresentation.None, 0, scalingAxis: ScalingAxis.Spell);

        // The cast set for this milestone, named so the vacuity guard below
        // can say what it expected rather than counting whatever it was given.
        private static readonly string[] MilestoneASpellIds =
            { "gilded_aegis", "winters_rebuke", "vipers_bite" };

        private static List<ResolvedSkill> TheThree() => new List<ResolvedSkill>
        {
            Aegis(),
            Packet("winters_rebuke", DamageType.Ice, 6, StatusEffectType.Chilled, 25, 2),
            Packet("vipers_bite", DamageType.Poison, 3, StatusEffectType.Poison, 3, 3),
        };

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            IReadOnlyList<ResolvedSkill> skills, params ModifierEffect[] heroEffects)
        {
            var hero = Hero();
            if (heroEffects.Length > 0) hero.ModifierEffects = new ModifierEffectSet(heroEffects);

            var foe = Foe();
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(7))
            {
                DamageVarianceRange = 0f,
            };
            return (session, hero, foe);
        }

        // THE REPAIR, pinned as a comparison. No literal appears on either
        // side: what is asserted is that the two paths agree, which is the
        // property that was broken and the only one a literal could not
        // protect (a literal would have matched the buggy preview just as
        // happily as the correct one).
        [Test]
        public void APacketPreviewQuotesTheSameFigureResolutionProduces_WithAnElementalRider()
        {
            var skill = Packet("winters_rebuke", DamageType.Ice, 6);
            var rider = new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 50, against: DamageType.Ice);

            var (session, hero, foe) = Fight(new[] { skill }, rider);

            int previewed = session.PreviewSkillPower(hero, skill);

            // The foe has no resistance, no weakness, no ward and no defence
            // term that touches a packet, and variance is off -- so what lands
            // IS the scaled packet.
            foe.MagicalDefense = 0;
            foe.PhysicalDefense = 0;
            int before = foe.CurrentHealth;
            session.CastSkill(0, foe);
            int resolved = before - foe.CurrentHealth;

            Assert.AreEqual(resolved, previewed,
                "the power row and the blow must quote the same number -- the preview branch omitted "
                + "ElementalDamagePercent until 2026-09-20, so a caster with an elemental rider was told "
                + "one figure and dealt another");
        }

        // AND THE RIDER HAS TO MATTER, or the test above passes for the wrong
        // reason on a fixture where the modifier never applied at all.
        [Test]
        public void TheElementalRiderActuallyMovesThePreview()
        {
            var skill = Packet("winters_rebuke", DamageType.Ice, 6);

            var (bare, bareHero, _) = Fight(new[] { skill });
            var (riderSession, riderHero, _) = Fight(new[] { skill },
                new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 50, against: DamageType.Ice));

            Assert.Greater(riderSession.PreviewSkillPower(riderHero, skill),
                bare.PreviewSkillPower(bareHero, skill),
                "vacuity guard: with no visible effect from the rider the parity test above proves nothing");
        }

        // A RIDER AGAINST ANOTHER ELEMENT MUST NOT COUNT. The preview reads
        // the rider per PACKET TYPE, the same way resolution does; a preview
        // that summed every elemental rider would quote a Fire bonus on a
        // Frost spell.
        [Test]
        public void ARiderAgainstAnotherElementLeavesThePacketAlone()
        {
            var skill = Packet("winters_rebuke", DamageType.Ice, 6);

            var (bare, bareHero, _) = Fight(new[] { skill });
            var (wrongElement, wrongHero, _) = Fight(new[] { skill },
                new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 50, against: DamageType.Fire));

            Assert.AreEqual(bare.PreviewSkillPower(bareHero, skill),
                wrongElement.PreviewSkillPower(wrongHero, skill));
        }

        // ---- and it still mutates nothing --------------------------------------

        [Test]
        public void APreviewOfEveryMilestoneASpellLeavesTheBoardAsItFoundIt()
        {
            var skills = TheThree();
            Assert.AreEqual(MilestoneASpellIds.Length, skills.Count,
                "vacuity guard: the cast set and the milestone's spell list have drifted apart");

            var (session, hero, foe) = Fight(skills);

            // A Poison on the board is the thing a careless preview would
            // spend: Viper's Bite's packet is Poison-typed, so a preview that
            // reached AfterDefences would detonate it.
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));
            Marks.Apply(foe, hero);

            int heroMana = hero.CurrentMana;
            int foeHealth = foe.CurrentHealth;
            int foeStatuses = foe.Statuses.Count;
            int poisonTurns = foe.Statuses.Single(s => s.Type == StatusEffectType.Poison).TurnsRemaining;

            foreach (var skill in skills)
            {
                session.PreviewSkillPower(hero, skill);
            }

            Assert.AreEqual(heroMana, hero.CurrentMana, "a preview spent mana");
            Assert.AreEqual(foeHealth, foe.CurrentHealth, "a preview dealt damage");
            Assert.AreEqual(foeStatuses, foe.Statuses.Count, "a preview added or removed a status");
            Assert.AreEqual(poisonTurns,
                foe.Statuses.Single(s => s.Type == StatusEffectType.Poison).TurnsRemaining,
                "a preview detonated or aged the Poison it was only supposed to look at");
            Assert.IsTrue(Marks.IsMarked(foe), "a preview consumed the mark");
        }

        [Test]
        public void APreviewRestoresTheCastersBorrowedAttackBonus()
        {
            var skills = TheThree();
            var (session, hero, _) = Fight(skills);
            hero.BonusAttackPercent = 17;

            foreach (var skill in skills)
            {
                session.PreviewSkillPower(hero, skill);
            }

            Assert.AreEqual(17, hero.BonusAttackPercent,
                "PreviewSkillPower borrows this field and puts it back in a finally -- see its own header");
        }

        // ---- milestone C: the schedule is a board too (plan 1.13) ----------

        private static readonly string[] MilestoneCSpellIds =
            { "borrowed_moment", "gale_scythe", "palace_passage" };

        private static List<ResolvedSkill> TheThreeOfC() => new List<ResolvedSkill>
        {
            new ResolvedSkill("borrowed_moment", "Borrowed Moment", "", "hero", 1, SkillEffect.Hasten,
                SkillTargeting.SingleAlly, 8, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, advanceSlots: 2),
            new ResolvedSkill("gale_scythe", "Gale Scythe", "", "hero", 1, SkillEffect.DamageAll,
                SkillTargeting.AllEnemies, 11, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Wind, 5) }, SpellPresentation.None, 0,
                queuePushSlots: 1),
            new ResolvedSkill("palace_passage", "Palace Passage", "", "hero", 1, SkillEffect.SwapAllies,
                SkillTargeting.SingleAlly, 7, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, freeAction: true),
        };

        [Test]
        public void APreviewOfEveryMilestoneCSpellLeavesTheScheduleAndTheFieldAsItFoundThem()
        {
            // THE NEW SURFACE THIS MILESTONE ADDED to preview purity is the
            // SCHEDULE. Milestone A and B's spells could only have spent a
            // status, a mark or a resource; these three can move an initiative
            // entry and a field slot, and neither of those is anywhere in the
            // list the earlier test checks.
            var skills = TheThreeOfC();
            Assert.AreEqual(MilestoneCSpellIds.Length, skills.Count,
                "vacuity guard: the cast set and the milestone's spell list have drifted apart");

            var (session, hero, foe) = Fight(skills);
            var encounter = session.Encounter;

            float heroCharge = encounter.ChargeOf(hero);
            float foeCharge = encounter.ChargeOf(foe);
            var partyBefore = encounter.PlayerParty.ToList();
            int heroMana = hero.CurrentMana;
            int foeHealth = foe.CurrentHealth;

            foreach (var skill in skills)
            {
                session.PreviewSkillPower(hero, skill);
            }

            Assert.AreEqual(heroCharge, encounter.ChargeOf(hero), "a preview moved the caster's own turn");
            Assert.AreEqual(foeCharge, encounter.ChargeOf(foe), "a preview delayed an enemy");
            CollectionAssert.AreEqual(partyBefore, encounter.PlayerParty.ToList(),
                "a preview swapped two field slots");
            Assert.AreEqual(heroMana, hero.CurrentMana, "a preview spent mana");
            Assert.AreEqual(foeHealth, foe.CurrentHealth, "a preview dealt damage");
        }

        [Test]
        public void TheTrackersAdvanceGhostCommitsNothing()
        {
            // THE ONE PREVIEW IN THIS MILESTONE THAT RUNS THE REAL RULE.
            // PreviewSkillPower above cannot touch the schedule because it
            // never looks at it; UpcomingTurnsPulled has to, because its whole
            // job is showing where a cast would land. What keeps it honest is
            // that the rule runs on Snapshot()'s copy -- see
            // TurnOrderTests.ProjectPulled_LeavesEveryRealChargeUnchanged for
            // the same claim one layer down.
            var hero = Hero();
            var ally = new CombatantState("Ally", true, 500, 50, 40, 4);
            var foe = Foe();
            var encounter = new CombatEncounter(new[] { hero, ally }, new[] { foe });
            new FightSession(encounter, new List<PlayerKit>
            {
                new PlayerKit("hero", CharacterRole.Tank, TheThreeOfC(), null, null),
            }, null, new SeededRandom(7)).Begin();

            float allyCharge = encounter.ChargeOf(ally);
            var plain = encounter.UpcomingTurns(6).ToList();

            var ghost = encounter.UpcomingTurnsPulled(ally, 2, 6);

            Assert.IsNotEmpty(ghost, "fixture: the ghost showed nothing");
            Assert.AreEqual(allyCharge, encounter.ChargeOf(ally), "the ghost committed the advance");
            CollectionAssert.AreEqual(plain, encounter.UpcomingTurns(6).ToList(),
                "the real forecast changed after a hover");
        }

    }
}
