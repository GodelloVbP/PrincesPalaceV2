using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Preview;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // WHAT STAGE A SPELL PREVIEW STANDS UP, pinned with hand-built skills and
    // rosters so it runs under the dotnet host.
    //
    // PreviewFightRefusalTests (PlayMode) covers PreviewFight.ForSpell against
    // real content, which needs Unity's ContentDatabase. This covers the part
    // that follows from the skill alone -- the supported list, the formation,
    // the party size and who fills it -- which moved to Domain as PreviewStage
    // exactly so these literals could be checked without a Unity boot.
    //
    // Afflict and SwapAllies are the two effects that let
    // thorn_tithe and palace_passage be previewed and captured.
    public class PreviewStageTests
    {
        private static ResolvedSkill Skill(string id, SkillEffect effect, SkillTargeting targeting) =>
            new ResolvedSkill { Id = id, CharacterId = "sheep", Effect = effect, Targeting = targeting };

        // Speed as FightEncounterAdapter builds it: the row's speed plus
        // (dexterity - 10) / 2. Dexterity 10 is neutral, so `speed` is the
        // whole figure unless `dexterity` says otherwise.
        private static ResolvedCharacter Character(string id, int speed, int dexterity = 10, bool art = true) =>
            new ResolvedCharacter
            {
                Id = id,
                DisplayName = id,
                BaseStats = new StatBlock(maxHealth: 50, speed: speed, attack: 5),
                AbilityScores = new AbilityScoreBlock(10, dexterity, 10, 10, 10, 10),
                BattleSpritePath = art ? "Characters/" + id : "",
            };

        // ---- Afflict -------------------------------------------------------------

        [Test]
        public void AnAfflictIsStagedAgainstOneEnemyWithTheCasterAlone()
        {
            var stage = PreviewStage.For(Skill("thorn_fixture", SkillEffect.Afflict, SkillTargeting.SingleEnemy));

            Assert.IsTrue(stage.Ok, stage.Refusal);

            // One enemy, so the harness's first-living-enemy press lands on
            // enemy slot 0 -- the only one there is.
            Assert.IsFalse(stage.FullFormation, "an Afflict is single-target; it gets the lone formation");
            Assert.AreEqual(1, stage.PartySize);
            Assert.IsFalse(stage.PartyStartsWounded, "an Afflict heals nothing, so the party opens whole");

            var squad = PreviewStage.Squad(stage, "sheep", new[] { Character("sheep", 10), Character("bear", 8) });
            CollectionAssert.AreEqual(new[] { "sheep" }, squad);
        }

        // ---- SwapAllies ----------------------------------------------------------

        [Test]
        public void ASwapIsStagedWithTwoOnThePlayerSide()
        {
            var stage = PreviewStage.For(Skill("passage_fixture", SkillEffect.SwapAllies, SkillTargeting.SingleAlly));

            Assert.IsTrue(stage.Ok, stage.Refusal);
            Assert.AreEqual(2, stage.PartySize, "Palace Passage picks two allies, so two have to stand");
            Assert.IsFalse(stage.FullFormation);
            Assert.IsFalse(stage.PartyStartsWounded);
        }

        [Test]
        public void TheSwapSquadIsTheCasterInSlotZeroAndASlowerSquadmateInSlotOne()
        {
            var stage = PreviewStage.For(Skill("passage_fixture", SkillEffect.SwapAllies, SkillTargeting.SingleAlly));

            // Roster order puts the faster owl (11 + (12-10)/2 = 12) ahead of
            // the bear (8 + 1 = 9). The owl would take the first player turn
            // from a speed-10 caster, so the bear stands instead.
            var roster = new[]
            {
                Character("sheep", 10),
                Character("owl", 11, dexterity: 12),
                Character("bear", 8, dexterity: 12),
            };

            var squad = PreviewStage.Squad(stage, "sheep", roster);

            Assert.IsNotNull(squad, stage.Refusal);
            Assert.IsTrue(stage.Ok, stage.Refusal);

            // The harness confirms ally picks in party order, so these are
            // the two picks: slot 0 (the caster) trades with slot 1.
            CollectionAssert.AreEqual(new[] { "sheep", "bear" }, squad);
        }

        [Test]
        public void ASquadmateAsFastAsTheCasterStandsBecauseTheTieGoesToTheCaster()
        {
            var stage = PreviewStage.For(Skill("passage_fixture", SkillEffect.SwapAllies, SkillTargeting.SingleAlly));

            var squad = PreviewStage.Squad(stage, "sheep", new[] { Character("sheep", 10), Character("twin", 10) });

            CollectionAssert.AreEqual(new[] { "sheep", "twin" }, squad);
        }

        [Test]
        public void ASquadmateWithArtIsPreferredOverAFallbackPlate()
        {
            var stage = PreviewStage.For(Skill("passage_fixture", SkillEffect.SwapAllies, SkillTargeting.SingleAlly));

            var roster = new[]
            {
                Character("sheep", 10),
                Character("plate", 8, art: false),
                Character("bear", 8),
            };

            CollectionAssert.AreEqual(new[] { "sheep", "bear" }, PreviewStage.Squad(stage, "sheep", roster));
        }

        [Test]
        public void ASwapWithNoSquadmateSlowEnoughIsRefusedNamingTheCaster()
        {
            var stage = PreviewStage.For(Skill("passage_fixture", SkillEffect.SwapAllies, SkillTargeting.SingleAlly));

            var squad = PreviewStage.Squad(stage, "sheep", new[] { Character("sheep", 10), Character("owl", 11) });

            Assert.IsNull(squad);
            Assert.IsFalse(stage.Ok);
            StringAssert.Contains("2 on the player side", stage.Refusal);
            StringAssert.Contains("'sheep'", stage.Refusal);
        }

        // ---- the existing kinds, unchanged by the move to Domain -----------------

        [Test]
        public void AnAllTargetCastGetsTheFullFormation()
        {
            var stage = PreviewStage.For(Skill("aoe_fixture", SkillEffect.DamageAll, SkillTargeting.AllEnemies));

            Assert.IsTrue(stage.Ok, stage.Refusal);
            Assert.IsTrue(stage.FullFormation);
            Assert.AreEqual(1, stage.PartySize);
        }

        [Test]
        public void ASingleAllyHealStandsTheCasterAloneAndWounded()
        {
            var stage = PreviewStage.For(Skill("mend_fixture", SkillEffect.HealSingle, SkillTargeting.SingleAlly));

            Assert.IsTrue(stage.Ok, stage.Refusal);
            Assert.AreEqual(1, stage.PartySize, "a heal picks one ally, and the caster is one");
            Assert.IsTrue(stage.PartyStartsWounded);
        }

        [Test]
        public void ASummonGetsTheLoneFormation()
        {
            var stage = PreviewStage.For(Skill("roar_fixture", SkillEffect.Summon, SkillTargeting.SingleEnemy));

            Assert.IsTrue(stage.Ok, stage.Refusal);
            Assert.IsFalse(stage.FullFormation);
        }

        [Test]
        public void AnUnsupportedEffectIsRefusedByNameListingTheNewKinds()
        {
            var stage = PreviewStage.For(Skill("reclaim_fixture", SkillEffect.Reclaim, SkillTargeting.SingleEnemy));

            Assert.IsFalse(stage.Ok);
            StringAssert.Contains("'reclaim_fixture'", stage.Refusal);
            StringAssert.Contains("Reclaim", stage.Refusal);
            StringAssert.Contains("Afflict", stage.Refusal);
            StringAssert.Contains("SwapAllies", stage.Refusal);
        }

        [Test]
        public void TheSupportedListIsElevenEffects()
        {
            CollectionAssert.AreEquivalent(new List<SkillEffect>
            {
                SkillEffect.DamageSingle, SkillEffect.DamageAll, SkillEffect.HealSelf, SkillEffect.HealParty,
                SkillEffect.HealSingle, SkillEffect.BuffParty, SkillEffect.Summon, SkillEffect.Transform,
                SkillEffect.Enthrall, SkillEffect.Afflict, SkillEffect.SwapAllies,
            }, PreviewStage.Supported);
        }
    }
}
