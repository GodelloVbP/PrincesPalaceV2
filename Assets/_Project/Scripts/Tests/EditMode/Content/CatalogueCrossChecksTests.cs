using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // ContentDatabase.ValidateContent()'s six cross-catalogue id rules, one
    // fixture per rule that breaks exactly that rule (AUDIT #77). Each rule's
    // comparison is a CatalogueCrossChecks predicate over primitives, so the
    // fixture is literals rather than a synthetic catalogue no test assembly
    // could build. The wiring -- that ValidateContent really hands each
    // predicate the loaded content -- is ContentValidationWiringTests'
    // (PlayMode), which runs the whole method over shipped content.
    //
    // Every refusal test pins the message's own distinguishing phrase, not
    // just non-null: a predicate that refused for the wrong reason would
    // otherwise pass.
    public class CatalogueCrossChecksTests
    {
        private static readonly HashSet<string> Characters = new HashSet<string> { "shawn", "odette" };
        private static readonly HashSet<string> Enemies = new HashSet<string> { "giant_rat" };
        private static readonly HashSet<string> Items = new HashSet<string> { "iron_sword" };

        private static readonly Dictionary<string, string> SkillOwners = new Dictionary<string, string>
        {
            ["fleece_ward"] = "shawn",
            ["hoot"] = "odette",
            ["gnaw"] = "giant_rat",
        };

        // ---- 1. ids unique across every catalogue ----

        [Test]
        public void DistinctIdsAcrossCataloguesPass()
        {
            var errors = CatalogueCrossChecks.DuplicateIds(new (string, IEnumerable<string>)[]
            {
                ("Relic", new[] { "bloodlust" }),
                ("Item", new[] { "iron_sword" }),
            });

            Assert.IsEmpty(errors);
        }

        [Test]
        public void AnIdSharedByTwoCataloguesIsRefusedOnTheSecond()
        {
            var errors = CatalogueCrossChecks.DuplicateIds(new (string, IEnumerable<string>)[]
            {
                ("Relic", new[] { "bloodlust" }),
                ("Item", new[] { "iron_sword", "bloodlust" }),
            });

            Assert.AreEqual(1, errors.Count, string.Join(" | ", errors));
            StringAssert.Contains("Duplicate id 'bloodlust' (on a Item)", errors[0]);
        }

        [Test]
        public void AnIdRepeatedInsideOneCatalogueIsRefusedToo()
        {
            var errors = CatalogueCrossChecks.DuplicateIds(new (string, IEnumerable<string>)[]
            {
                ("Skill", new[] { "hoot", "hoot" }),
            });

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("Duplicate id 'hoot' (on a Skill)", errors[0]);
        }

        // ---- 2. an enemy ability names a real skill ----

        [Test]
        public void AnEnemyAbilityNamingARealSkillPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.EnemyAbilitySkill("giant_rat", "gnaw", SkillOwners.Keys));
        }

        [Test]
        public void AnEnemyAbilityNamingNoSkillIsRefused()
        {
            string error = CatalogueCrossChecks.EnemyAbilitySkill("giant_rat", "gnw", SkillOwners.Keys);

            Assert.AreEqual("Enemy 'giant_rat' has an ability naming unknown skill id 'gnw'.", error);
        }

        [Test]
        public void AnEnemyAbilityWithNoSkillIdIsNotThisRulesBusiness()
        {
            Assert.IsNull(CatalogueCrossChecks.EnemyAbilitySkill("giant_rat", "", SkillOwners.Keys));
            Assert.IsNull(CatalogueCrossChecks.EnemyAbilitySkill("giant_rat", null, SkillOwners.Keys));
        }

        // ---- 3. a talent's granted skill exists and is its own character's ----

        [Test]
        public void ATalentGrantingItsOwnCharactersSkillPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.TalentGrantedSkill("woolly", "shawn", false, "fleece_ward", SkillOwners));
        }

        [Test]
        public void ATalentGrantingNoSkillPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.TalentGrantedSkill("hardy", "shawn", false, "", SkillOwners));
        }

        [Test]
        public void ATalentGrantingAnUnknownSkillIsRefused()
        {
            string error = CatalogueCrossChecks.TalentGrantedSkill("woolly", "shawn", false, "fleece_wrd", SkillOwners);

            Assert.AreEqual("Talent 'woolly' grants unknown skill id 'fleece_wrd'.", error);
        }

        [Test]
        public void ATalentGrantingAnotherCharactersSkillIsRefused()
        {
            // The Fragile Lamb case: shawn's talent handing out odette's kit.
            string error = CatalogueCrossChecks.TalentGrantedSkill("fragile_lamb", "shawn", false, "hoot", SkillOwners);

            Assert.IsNotNull(error);
            StringAssert.Contains("belongs to 'shawn' but grants skill 'hoot', which belongs to 'odette'", error);
        }

        [Test]
        public void ASharedTalentMayGrantAnyOwnersSkill()
        {
            Assert.IsNull(CatalogueCrossChecks.TalentGrantedSkill("common_ground", "", true, "hoot", SkillOwners));
        }

        [Test]
        public void ASharedTalentGrantingAnUnknownSkillIsStillRefused()
        {
            string error = CatalogueCrossChecks.TalentGrantedSkill("common_ground", "", true, "nothing", SkillOwners);

            Assert.AreEqual("Talent 'common_ground' grants unknown skill id 'nothing'.", error);
        }

        // ---- 4. a talent's starting item exists ----

        [Test]
        public void ATalentGrantingARealItemPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.TalentStartingItem("armoury", "iron_sword", Items));
            Assert.IsNull(CatalogueCrossChecks.TalentStartingItem("hardy", "", Items));
        }

        [Test]
        public void ATalentGrantingAnUnknownItemIsRefused()
        {
            string error = CatalogueCrossChecks.TalentStartingItem("armoury", "iron_swrd", Items);

            Assert.AreEqual("Talent 'armoury' grants unknown item id 'iron_swrd'.", error);
        }

        // ---- 5. a talent's character exists ----

        [Test]
        public void ATalentOwnedByARealCharacterPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.TalentCharacter("hardy", "shawn", false, Characters));
        }

        [Test]
        public void ATalentOwnedByAnUnknownCharacterIsRefused()
        {
            string error = CatalogueCrossChecks.TalentCharacter("hardy", "shwan", false, Characters);

            Assert.AreEqual("Talent 'hardy' belongs to unknown character id 'shwan'.", error);
        }

        [Test]
        public void ASharedTalentHasNoOwnerToCheck()
        {
            Assert.IsNull(CatalogueCrossChecks.TalentCharacter("common_ground", "", true, Characters));
        }

        // ---- 6. a skill's owner is a character or an enemy ----

        [Test]
        public void ASkillOwnedByACharacterOrAnEnemyPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.SkillOwner("hoot", "odette", Characters, Enemies));
            Assert.IsNull(CatalogueCrossChecks.SkillOwner("gnaw", "giant_rat", Characters, Enemies));
        }

        [Test]
        public void ASkillOwnedByNobodyIsRefused()
        {
            string error = CatalogueCrossChecks.SkillOwner("gnaw", "giant_ratt", Characters, Enemies);

            Assert.IsNotNull(error);
            StringAssert.Contains("Skill 'gnaw' belongs to unknown owner id 'giant_ratt'", error);
        }

        // ---- 7. a skill's stance names a still its owner has (AUDIT #145 B4) ----

        // Literal stand-in for the runtime loader: which stills exist where.
        private static readonly HashSet<string> Stills = new HashSet<string>
        {
            "Characters/sheep/cast", "Characters/sheep_black_ram/victory", "Enemies/beetle/turtle_up",
        };

        private static bool Loads(string folder, string stance) => Stills.Contains($"{folder}/{stance}");

        [Test]
        public void AStanceTheOwnersFolderHasPasses()
        {
            Assert.IsNull(CatalogueCrossChecks.SkillStance("barrel_roll", "stance", "turtle_up",
                new[] { "Enemies/beetle" }, Loads));
        }

        [Test]
        public void AStanceOnlyAFormsFolderHasPasses()
        {
            // black_ram_mode's victory pose is drawn in the form's folder.
            Assert.IsNull(CatalogueCrossChecks.SkillStance("black_ram_mode", "windupStance", "victory",
                new[] { "Characters/sheep", "Characters/sheep_black_ram" }, Loads));
        }

        [Test]
        public void AMisspelledStanceIsRefusedNamingTheFieldAndTheFolders()
        {
            string error = CatalogueCrossChecks.SkillStance("barrel_roll", "approachStance", "turtle_upp",
                new[] { "Enemies/beetle" }, Loads);

            Assert.IsNotNull(error);
            StringAssert.Contains("Skill 'barrel_roll' names approachStance 'turtle_upp', which is not a still in Enemies/beetle",
                error);
        }

        [Test]
        public void NoStanceAuthoredOrNoArtYetIsNotThisRulesBusiness()
        {
            Assert.IsNull(CatalogueCrossChecks.SkillStance("shear", "stance", "", new[] { "Characters/sheep" }, Loads));
            Assert.IsNull(CatalogueCrossChecks.SkillStance("slam", "stance", "slam", new string[0], Loads));
            Assert.IsNull(CatalogueCrossChecks.SkillStance("slam", "stance", "slam", null, Loads));
        }
    }
}
