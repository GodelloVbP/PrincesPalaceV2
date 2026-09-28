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
    // THE THREE RULES THAT LIVE OUTSIDE THE ENGINE.
    //
    // The player picks who a gift or a ward lands on; there is no auto-pick
    // in FightSession.Talents.GiftRecipient or ApplyWard. These rules are
    // not wrong to have, they are in the wrong layer if they live there,
    // so they live in Domain/Bot/AllyTargetSelection -- the only caller left
    // that has to choose with no hand on the mouse.
    //
    // PINNED HERE AND NOT IN FightTalentTests, which is the point of the move:
    // a change to how the BOT plays must not be able to reach into what a cast
    // DOES, and a test file per layer is what makes that visible.
    public class BotAllyTargetSelectionTests
    {
        private static CombatantState Hero(string name, int maxMana = 60, int speed = 5) =>
            new CombatantState(name, true, 500, maxMana, 40, speed);

        private static CombatantState Foe() =>
            new CombatantState("Foe", false, 1000, 10, 30, 1);

        private static ResolvedSkill Skill(SkillEffect effect, string name) =>
            new ResolvedSkill("s_" + name, name, "", "hero", 1, effect,
                SkillEntryResolver.DefaultTargetingFor(effect),
                0, 0, false, 100, 0, false, null, SpellPresentation.None, 0);

        private static FightSession Fight(CombatantState[] party, params ResolvedSkill[] skills)
        {
            var kits = new List<PlayerKit>
            {
                new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical),
            };

            var session = new FightSession(new CombatEncounter(party, new[] { Foe() }), kits, null,
                new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();
            return session;
        }

        private static ResourcePool Fury() =>
            new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 15, gainOnDamageTaken: 10)
            {
                ShortTag = "FURY",
                RestoredByManaEffects = false,
            };

        // ---- the legal menu is the player's menu -------------------------------

        [Test]
        public void LegalActions_OffersOneActionPerEligibleAlly()
        {
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");
            var odette = Hero("Odette");
            shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.GiftManaPercent, 40),
            });

            var session = Fight(new[] { shawn, bjorn, odette }, Skill(SkillEffect.GiftMana, "Gift: Mana"));

            var gifts = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>())
                .Where(a => a.Kind == FightActionKind.Skill)
                .ToList();

            Assert.AreEqual(2, gifts.Count, "one per squadmate the gift will accept, and the caster is not one");
            CollectionAssert.AreEquivalent(new[] { bjorn, odette }, gifts.Select(a => a.Target).ToList());
        }

        [Test]
        public void LegalActions_OffersTheCastersOwnPlateForAWard()
        {
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");
            shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
            });

            var session = Fight(new[] { shawn, bjorn }, Skill(SkillEffect.Ward, "Fleece Ward"));

            var wards = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>())
                .Where(a => a.Kind == FightActionKind.Skill)
                .ToList();

            CollectionAssert.AreEquivalent(new[] { shawn, bjorn }, wards.Select(a => a.Target).ToList());
        }

        // ---- and the preference over it ----------------------------------------

        [Test]
        public void GiftMana_PrefersTheEmptiestBar()
        {
            // The engine's old rule, verbatim: a gift is a fixed percentage of
            // the recipient's own maximum, so handing it to whoever is missing
            // the most is the only reading under which the number the caster
            // spent wool for is the number that lands.
            var shawn = Hero("Shawn", speed: 10);
            var full = Hero("Nearly Full");
            full.PrimaryPool.Current = 55;
            var drained = Hero("Drained");
            drained.PrimaryPool.Current = 10;

            var preferred = AllyTargetSelection.Preferred(
                SkillEffect.GiftMana, shawn, new[] { full, drained });

            Assert.AreSame(drained, preferred);
        }

        [Test]
        public void GiftMana_BreaksATieOnPartyOrder()
        {
            // LINQ's OrderByDescending is stable, so two equally empty bars
            // fall back to the order the candidate list arrived in -- which is
            // EligibleAllies' order, which is the field formation.
            var shawn = Hero("Shawn", speed: 10);
            var first = Hero("First");
            first.PrimaryPool.Current = 10;
            var second = Hero("Second");
            second.PrimaryPool.Current = 10;

            Assert.AreSame(first, AllyTargetSelection.Preferred(
                SkillEffect.GiftMana, shawn, new[] { first, second }));
        }

        [Test]
        public void GiftFuryAndGiftHaste_TakeTheFirstLivingAlly()
        {
            // Neither says anything about a resource, so every living ally is
            // a valid recipient and the first one is as good a pick as any.
            // That was the engine's answer and it is kept verbatim.
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");
            bjorn.PrimaryPool = Fury();
            var odette = Hero("Odette");

            var candidates = new[] { bjorn, odette };

            Assert.AreSame(bjorn, AllyTargetSelection.Preferred(SkillEffect.GiftFury, shawn, candidates));
            Assert.AreSame(bjorn, AllyTargetSelection.Preferred(SkillEffect.GiftHaste, shawn, candidates));
        }

        [Test]
        public void Ward_TakesHisOwnBackFirst()
        {
            // The ward was the caster's own for the whole of the talent's
            // life. A bot that started wrapping somebody else by default would
            // be a behaviour change smuggled in under a targeting change.
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");

            Assert.AreSame(shawn, AllyTargetSelection.Preferred(
                SkillEffect.Ward, shawn, new[] { bjorn, shawn }));
        }

        [Test]
        public void Preferred_NeverWidensTheCandidateList()
        {
            // AllyTargeting decides who is ELIGIBLE and this decides who is
            // BEST among them. Handed a list the caster is not on, a ward has
            // to take somebody who IS on it rather than reaching for him.
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");

            Assert.AreSame(bjorn, AllyTargetSelection.Preferred(SkillEffect.Ward, shawn, new[] { bjorn }));
            Assert.IsNull(AllyTargetSelection.Preferred(SkillEffect.Ward, shawn, new CombatantState[0]));
        }

        // ---- narrowing, which is what a thinking policy actually calls ---------

        [Test]
        public void Narrow_LeavesOneActionPerAllyFacingSkillAndTouchesNothingElse()
        {
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");
            var odette = Hero("Odette");
            bjorn.PrimaryPool.Current = 10;
            odette.PrimaryPool.Current = 55;
            shawn.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.GiftManaPercent, 40),
            });

            var session = Fight(new[] { shawn, bjorn, odette }, Skill(SkillEffect.GiftMana, "Gift: Mana"));
            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            var narrowed = AllyTargetSelection.Narrow(session, session.Current, legal);

            var gifts = narrowed.Where(a => a.Kind == FightActionKind.Skill).ToList();
            Assert.AreEqual(1, gifts.Count);
            Assert.AreSame(bjorn, gifts[0].Target, "the emptiest bar");

            Assert.AreEqual(legal.Count(a => a.Kind != FightActionKind.Skill),
                narrowed.Count(a => a.Kind != FightActionKind.Skill),
                "every Attack, Item and Move passes through untouched");
        }

        [Test]
        public void Narrow_ReturnsTheSameListWhenThereIsNothingToNarrow()
        {
            // Every turn of every fight with no ally-facing skill in the kit,
            // which is most of them -- the allocation is worth avoiding and
            // the identity is worth pinning so it stays avoided.
            var shawn = Hero("Shawn", speed: 10);
            var bjorn = Hero("Bjorn");

            var session = Fight(new[] { shawn, bjorn }, Skill(SkillEffect.DamageSingle, "Bolt"));
            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.AreSame(legal, AllyTargetSelection.Narrow(session, session.Current, legal));
        }
    }
}
