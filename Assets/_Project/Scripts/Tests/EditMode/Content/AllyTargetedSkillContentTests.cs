using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // WHICH SHIPPED ROWS ASK THE PLAYER TO PICK A SQUADMATE (AUDIT #147).
    //
    // READ OUT OF skills.json, not asserted against DefaultTargetingFor alone:
    // a test that only called the mapping would pass for a roster with no
    // ally-facing skill in it at all, which is the vacuity guard every lint in
    // this project is required to carry. The point is that the FIVE AUTHORED
    // ROWS reach SingleAlly -- through the default or through an override,
    // whichever the file uses.
    //
    // AND THAT NOTHING ELSE DOES, which is the half that catches the real
    // mistake: a single-ally targeting landing on an effect whose resolve path
    // ignores `target` would open a picker for a cast that then does something
    // else entirely.
    public class AllyTargetedSkillContentTests
    {
        private static string SkillsJson() =>
            File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project", "ContentData", "skills.json"));

        private static SkillTargeting TargetingOf(string skill)
        {
            string authored = (JsonBlocks.String(skill, "targeting") ?? "").Trim();

            var effect = SkillEffect.DamageSingle;
            System.Enum.TryParse(JsonBlocks.String(skill, "effect") ?? "", ignoreCase: true, out effect);

            var targeting = SkillEntryResolver.DefaultTargetingFor(effect);
            if (!string.IsNullOrWhiteSpace(authored))
            {
                System.Enum.TryParse(authored, ignoreCase: true, out targeting);
            }

            return targeting;
        }

        private static Dictionary<string, SkillTargeting> ByIdFromContent()
        {
            var body = SkillsJson();
            var map = new Dictionary<string, SkillTargeting>();

            foreach (string skill in JsonBlocks.ObjectsInArray(body, "skills"))
            {
                string id = JsonBlocks.String(skill, "id");
                if (!string.IsNullOrWhiteSpace(id)) map[id] = TargetingOf(skill);
            }

            return map;
        }

        [Test]
        public void TheWardsAndTheThreeGiftsAskForAnAlly()
        {
            var byId = ByIdFromContent();

            Assert.Greater(byId.Count, 20, "vacuity guard: skills.json was not read at all");

            foreach (string id in new[]
                     {
                         "fleece_ward", "placeholder_brawler_ward", "bulwark", "prism_ward",
                         "gift_mana", "gift_fury", "gift_haste", "mend",
                     })
            {
                Assert.IsTrue(byId.ContainsKey(id), $"skills.json no longer has a row called '{id}'");
                Assert.AreEqual(SkillTargeting.SingleAlly, byId[id],
                    $"'{id}' has to stop for an ally pick -- see AUDIT #147");
            }
        }

        [Test]
        public void NothingElseInContentClaimsSingleAllyTargeting()
        {
            // The resolve path reads `target` for exactly two targetings
            // (SingleEnemy and SingleAlly). A row that declared SingleAlly on
            // an effect resolving off the actor would open the party picker
            // and then ignore whoever was clicked.
            var allowed = new HashSet<string>
            {
                "fleece_ward", "placeholder_brawler_ward", "gift_mana", "gift_fury", "gift_haste",

                // Progression v2 phase 4. Bulwark and Prism Ward are wards
                // like the two above; Mend is the first HealSingle, and
                // ResolveCharacterSkillInner has an arm that reads its
                // target. Tuck In is deliberately NOT here -- it authors
                // targeting Self, because a free action that stops for a
                // pick is a free action that costs a click.
                "bulwark", "prism_ward", "mend",
            };

            var strays = ByIdFromContent()
                .Where(pair => pair.Value == SkillTargeting.SingleAlly && !allowed.Contains(pair.Key))
                .Select(pair => pair.Key)
                .ToList();

            CollectionAssert.IsEmpty(strays,
                "a new single-ally skill is welcome -- add it to this list, and give ResolveCharacterSkillInner " +
                "an arm that actually reads its target");
        }
    }
}
