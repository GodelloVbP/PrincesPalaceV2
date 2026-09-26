using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

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

            // Fails loudly rather than skipping (AUDIT #46). If the preview
            // has since learned all seven, trim Unsupported and this test with
            // it; if content dropped them, the by-name refusal is unread.
            Assert.IsNotNull(candidate,
                "no authored skill carries one of the effects the preview refuses, so this test has stopped " +
                "covering the by-name refusal -- update Unsupported to match PreviewFight, or retire the test");

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

        // ---- which element gets cast -----------------------------------------
        //
        // BUILT BY HAND rather than reached through content, and deliberately:
        // the rule is "the element the author asked for, else the first that
        // draws", and every interesting case of it (an element with no art
        // sitting first, an ask for one further down the list, an ask for one
        // that is not there) is a shape content might or might not happen to
        // have this month. A fixture that had to hope prismatic_orb still
        // authors four elements would go quiet the day it authored three.
        // ForSpell against real content is exercised once, below.

        private static SpellPresentation Drawn() =>
            new SpellPresentation { path = "Spells/anything" };

        private static ResolvedSkill Elemental() =>
            new ResolvedSkill
            {
                Id = "hand_built_orb",
                Elements = new[]
                {
                    new ElementChoice(DamageType.Earth),
                    new ElementChoice(DamageType.Water, Drawn()),
                    new ElementChoice(DamageType.Wind, Drawn()),
                },
            };

        [Test]
        public void WithNoElementAskedForTheFirstOneThatDrawsIsCast()
        {
            var chosen = PreviewFight.PreviewElementOf(Elemental(), null);

            Assert.AreEqual(DamageType.Water, chosen.Type,
                "Earth is first in authored order and draws nothing, so the rule that survives -Element " +
                "is still 'the first element that draws anything'.");
            Assert.IsNull(PreviewFight.ElementRefusal(Elemental(), null),
                "asking for no element in particular cannot be a refusal.");
        }

        [Test]
        public void TheElementAskedForIsTheOneCastEvenWhenItIsNotTheFirstThatDraws()
        {
            Assert.AreEqual(DamageType.Wind, PreviewFight.PreviewElementOf(Elemental(), "Wind").Type);

            // CASE-INSENSITIVELY, because the command line is typed by a human
            // and DamageType's own casing is an implementation detail of C#.
            Assert.AreEqual(DamageType.Wind, PreviewFight.PreviewElementOf(Elemental(), "wind").Type);

            // AND AN ELEMENT THAT DRAWS NOTHING IS STILL CASTABLE. "Does Earth
            // have art yet" is a real question and refusing it would make the
            // flag unable to answer it.
            Assert.AreEqual(DamageType.Earth, PreviewFight.PreviewElementOf(Elemental(), "Earth").Type);
        }

        [Test]
        public void AnElementTheSkillDoesNotOfferIsRefusedNamingTheOnesItDoes()
        {
            string refusal = PreviewFight.ElementRefusal(Elemental(), "Shadow");

            Assert.IsNotNull(refusal, "an element the skill does not offer was accepted.");
            StringAssert.Contains("Shadow", refusal);
            StringAssert.Contains("hand_built_orb", refusal);
            StringAssert.Contains("Earth, Water, Wind", refusal,
                "the refusal has to LIST what is on offer -- an author who typed the wrong element needs " +
                "the right one, not the news that theirs was wrong.");
        }

        [Test]
        public void AskingASkillWithNoElementsForOneIsItsOwnRefusal()
        {
            var plain = new ResolvedSkill { Id = "headbutt" };

            string refusal = PreviewFight.ElementRefusal(plain, "Fire");

            Assert.IsNotNull(refusal);
            StringAssert.Contains("offers no element choice at all", refusal,
                "'it does not offer that one' and 'it offers none' send an author to two different places.");
            StringAssert.Contains("headbutt", refusal);
        }

        // THE PLAN, over real content, so the wiring between the refusal above
        // and ForSpell is not left to inspection. The id is whatever content
        // currently offers a choice; the assertion is about the plan, not the
        // orb.
        [Test]
        public void ForSpellCarriesTheAskedForElementOntoThePlanAndRefusesAnUnofferedOne()
        {
            var elemental = ContentDatabase.Skills
                .FirstOrDefault(s => s?.Data != null && s.Data.HasElementChoice);

            Assert.IsNotNull(elemental,
                "no skill in content offers an element choice any more, so -Element has nothing to pick " +
                "from and this pin covers nothing. Point it at the current example rather than leaving " +
                "it passing on an empty set.");

            string offered = elemental.Data.Elements[elemental.Data.Elements.Length - 1].Type.ToString();

            var plan = PreviewFight.ForSpell(elemental.id, offered.ToLowerInvariant());
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Assert.AreEqual(offered, plan.Element,
                "the plan carries the DamageType's own spelling, not whatever case was typed.");
            Assert.IsTrue(plan.Notes.Any(n => n.Contains(offered)),
                "casting a chosen element rather than the first that draws is an accommodation, and every " +
                "accommodation a preview makes is listed in Notes.");

            var refused = PreviewFight.ForSpell(elemental.id, "NotAnElementAnySkillOffers");
            Assert.IsFalse(refused.Ok);
            StringAssert.Contains("NotAnElementAnySkillOffers", refused.Refusal);
            StringAssert.Contains(offered, refused.Refusal);
        }

        [Test]
        public void WithNoElementAskedForThePlanNamesNoneAndTheOldRuleStands()
        {
            var elemental = ContentDatabase.Skills
                .FirstOrDefault(s => s?.Data != null && s.Data.HasElementChoice);

            Assert.IsNotNull(elemental, "no skill in content offers an element choice any more.");

            var plan = PreviewFight.ForSpell(elemental.id);

            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Assert.IsNull(plan.Element,
                "an absent -Element must leave the plan silent about which element is cast, so the forced " +
                "press and the capture's timing both fall back to PreviewElementOf's own rule.");
            Assert.AreEqual(PreviewFight.PreviewElementOf(elemental.Data),
                PreviewFight.PreviewElementOf(elemental.Data, plan.Element),
                "with nothing asked for, the two overloads have to agree -- they are what the controller " +
                "and the capture fixture each call.");
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

        // ---- the two kinds added 2026-09-25, against real content -------------
        //
        // PreviewStageTests (dotnet) pins the rules with hand-built skills;
        // these pin that the shipped rows reach them through ForSpell.

        [Test]
        public void ThornTitheIsStagedAgainstOneEnemyWithShawnAlone()
        {
            var plan = PreviewFight.ForSpell("thorn_tithe");

            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Assert.AreEqual(SkillEffect.Afflict, plan.Skill.Effect);
            Assert.AreEqual(PreviewFight.FormationLone, plan.Formation);
            CollectionAssert.AreEqual(new[] { "sheep" }, plan.Party);
        }

        // The bear (speed 8, dexterity 12: 9) stands beside Shawn (10); the
        // owl (11, dexterity 12: 12) would open the player side and is passed
        // over. Literal, off characters.json as of 2026-09-25 -- a speed
        // retune that changes who is slower than Shawn changes this line.
        [Test]
        public void PalacePassageIsStagedWithShawnAndASlowerSquadmate()
        {
            var plan = PreviewFight.ForSpell("palace_passage");

            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Assert.AreEqual(SkillEffect.SwapAllies, plan.Skill.Effect);
            Assert.AreEqual(PreviewFight.FormationLone, plan.Formation);
            CollectionAssert.AreEqual(new[] { "sheep", "bear" }, plan.Party);
        }
    }
}
