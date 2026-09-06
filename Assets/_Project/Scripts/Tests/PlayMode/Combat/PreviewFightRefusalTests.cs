using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // THE REFUSAL LADDER, which was the one part of the preview loop with no
    // test at all.
    //
    // PreviewFight's whole posture is "an unsupported setup is REPORTED BY NAME
    // AND REFUSED, never approximated" -- so the sentence it prints IS the
    // deliverable. Nothing checked that any of them still name what they claim
    // to: a refusal that came out as "REFUSED: " with an empty reason, or that
    // stopped naming the effect, would look exactly like a working refusal from
    // every side except the author's.
    //
    // PlayMode rather than EditMode because PreviewFight lives in Core and the
    // EditMode assembly references Domain only.
    //
    // Two rungs are driven through ChooseCaster with a hand-built roster, and
    // that is why it takes one: against the content in this repo they cannot be
    // reached at all (Shawn carries Wool, and the roster is never empty), so
    // the alternative was to leave two authored messages permanently unread.
    public class PreviewFightRefusalTests
    {
        // The seven the preview will not stand a fight up for, per
        // PreviewFight's own header. Listed here rather than reached through
        // the private array so the two can disagree -- if one of these becomes
        // supported, the sweep below simply finds no candidate and says so.
        private static readonly SkillEffect[] Unsupported =
        {
            SkillEffect.Ward,
            SkillEffect.Shatter,
            SkillEffect.GiftFury,
            SkillEffect.GiftHaste,
            SkillEffect.GiftMana,
            SkillEffect.Provoke,
            SkillEffect.RestorePartyMana,
        };

        private static ResolvedCharacter Character(string id, string signatureId = "", string art = "")
            => new ResolvedCharacter
            {
                Id = id,
                DisplayName = id,
                SignatureId = signatureId,
                SignatureDisplayName = signatureId,
                SignatureCapacity = signatureId.Length > 0 ? 10 : 0,
                BattleSpritePath = art,
            };

        private static ResolvedSkill Skill(string id, string characterId, int resourceCost = 0)
            => new ResolvedSkill { Id = id, CharacterId = characterId, ResourceCost = resourceCost };

        // ---- what the content database can provoke ---------------------------

        [Test]
        public void AnUnsupportedEffectIsRefusedByName()
        {
            var candidate = ContentDatabase.Skills
                .Where(s => s != null && s.Data != null)
                .FirstOrDefault(s => Unsupported.Contains(s.Data.Effect));

            if (candidate == null)
            {
                Assert.Ignore("no authored skill carries one of the seven effects the preview refuses, " +
                              "so there is nothing in content to refuse.");
            }

            var plan = PreviewFight.ForSpell(candidate.id);

            Assert.IsFalse(plan.Ok, $"'{candidate.id}' resolves as {candidate.Data.Effect}, which the preview " +
                                    "does not support, but the plan came back buildable.");
            StringAssert.Contains(candidate.id, plan.Refusal);
            StringAssert.Contains(candidate.Data.Effect.ToString(), plan.Refusal);

            // The list of what WOULD work, in the same breath as what did not.
            StringAssert.Contains(SkillEffect.DamageSingle.ToString(), plan.Refusal);
            StringAssert.Contains("REFUSED", PreviewFight.Describe(plan));
        }

        // An id that is not in content is the likeliest thing an author gets
        // wrong (a typo, or a row added without a rebuild), so the refusal has
        // to name the FILE they should be looking in -- not merely say no.
        [Test]
        public void AnUnknownSkillIdIsRefusedNamingTheFileToCheck()
        {
            var plan = PreviewFight.ForSpell("no_such_skill_preview_refusal_test");

            Assert.IsFalse(plan.Ok);
            StringAssert.Contains("no_such_skill_preview_refusal_test", plan.Refusal);
            StringAssert.Contains("ContentData/skills.json", plan.Refusal);
            StringAssert.Contains("rebuild content", plan.Refusal);
        }

        [Test]
        public void AnUnknownCharacterIdIsRefusedNamingTheFileToCheck()
        {
            var plan = PreviewFight.ForCharacter("no_such_character_preview_refusal_test");

            Assert.IsFalse(plan.Ok);
            StringAssert.Contains("no_such_character_preview_refusal_test", plan.Refusal);
            StringAssert.Contains("ContentData/characters.json", plan.Refusal);
        }

        // ---- the two rungs content cannot reach ------------------------------

        [Test]
        public void NoRosterCharacterCarriesTheResourceItSpends()
        {
            var plan = new PreviewFight.Plan();
            var roster = new List<ResolvedCharacter> { Character("penniless") };

            // The skill's owner is deliberately NOT in the roster: rung 1
            // returns the owner when it can find them, so the resource rung is
            // only reachable for a skill whose author is missing.
            string caster = PreviewFight.ChooseCaster(Skill("wool_spender", "sheep", resourceCost: 3), roster, plan);

            Assert.IsNull(caster);
            Assert.IsFalse(plan.Ok);
            StringAssert.Contains("no character in content carries resource", plan.Refusal);
            StringAssert.Contains("wool_spender", plan.Refusal);
            StringAssert.Contains("3 point(s)", plan.Refusal);
            StringAssert.Contains("Author a character with that signature resource", plan.Refusal);

            // NAMED AS "(any signature resource)", NOT AS WOOL, and that is the
            // message as it can actually be reached rather than as it reads.
            // The branch that names a specific resource asks the AUTHORED
            // OWNER which one it means -- and the owner being absent from the
            // roster is the only way to arrive here at all, so the lookup can
            // never answer. Pinned as-is rather than quietly deleted: whether
            // to drop the branch or to answer it from characters.json instead
            // is a decision, not a tidy-up.
            StringAssert.Contains("(any signature resource)", plan.Refusal);
        }

        [Test]
        public void AnEmptyRosterIsRefusedRatherThanCastByNobody()
        {
            var plan = new PreviewFight.Plan();

            string caster = PreviewFight.ChooseCaster(
                Skill("orphan_bolt", ""), new List<ResolvedCharacter>(), plan);

            Assert.IsNull(caster);
            Assert.IsFalse(plan.Ok);
            StringAssert.Contains("no characters in the content database at all", plan.Refusal);
        }

        // ---- and the rungs that do NOT refuse, so the ladder is not just a
        //      list of ways to say no ------------------------------------------

        [Test]
        public void TheAuthoredOwnerCastsTheirOwnSkill()
        {
            var plan = new PreviewFight.Plan();
            var roster = new List<ResolvedCharacter> { Character("owl"), Character("sheep", "wool") };

            Assert.AreEqual("sheep", PreviewFight.ChooseCaster(Skill("shear", "sheep", 3), roster, plan));
            Assert.IsTrue(plan.Ok);
        }

        [Test]
        public void SomebodyWhoCanPayCastsAnOrphanedResourceSkill()
        {
            var plan = new PreviewFight.Plan();
            var roster = new List<ResolvedCharacter> { Character("owl"), Character("sheep", "wool") };

            Assert.AreEqual("sheep", PreviewFight.ChooseCaster(Skill("book_spell", "gone", 3), roster, plan));
            Assert.IsTrue(plan.Ok);
            Assert.IsTrue(plan.Notes.Any(n => n.Contains("wool")),
                "the stand-in caster is an accommodation and every accommodation is listed in Notes.");
        }

        [Test]
        public void TheRosterLeadWithArtCastsWhenNothingElseDecides()
        {
            var plan = new PreviewFight.Plan();
            var roster = new List<ResolvedCharacter>
            {
                Character("artless"),
                Character("drawn", art: "Characters/drawn"),
            };

            Assert.AreEqual("drawn", PreviewFight.ChooseCaster(Skill("free_bolt", ""), roster, plan));
            Assert.IsTrue(plan.Ok);
            Assert.IsTrue(plan.Notes.Any(n => n.Contains("roster lead")));
        }
    }
}
