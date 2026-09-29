using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE PHYSICAL-MOVE CLASSIFICATION, AS SHIPPED (plan 1.10, milestone D).
    //
    // Two rules, and they fail differently on purpose:
    //
    //   1. every DamageSingle/DamageAll row STATES physicalMove -- the content
    //      lint, caught here as well as at the content build, because a row
    //      that silently defaults to "not a move" is a sword swing a rooted
    //      monster goes on making and nothing looks wrong;
    //   2. every row's classification is the one the plan's audit table names,
    //      pinned as a literal id->bool list. Two of the calls are judgement
    //      rather than derivation and are the reason the list is written out
    //      rather than computed: `boulder_slam` is physical although the golem
    //      authors no approach, and `spore_cloud` is not although the treant's
    //      other ability is.
    //
    // READ OFF THE AUTHORED TEXT, through JsonBlocks, for two reasons. The
    // explicitness question cannot be asked of a parsed RawSkillEntry at all
    // -- a bool has no spare value for "absent", which is the whole reason
    // ContentBuilder stamps physicalMoveOmitted from a probe parse -- and
    // reading the text keeps this file in the fast dotnet host rather than
    // needing Unity's JsonUtility.
    public class PhysicalMoveAuditTests
    {
        // THE VACUITY GUARD (.claude/rules/tests.md). 23 rows land on the
        // damage pipeline today -- 22 that author `effect` and
        // `placeholder_shawn_capstone`, which authors none and so takes
        // SkillEffect's own default of DamageSingle, exactly as the resolver
        // reads it. A scan that found none would pass rule 1 for a catalogue
        // with no classification in it at all, and a path change that silently
        // pointed this at the wrong file is exactly the failure a bare "no
        // offenders" assertion cannot tell from success.
        private const int MinimumDamageRowsExpected = 23;

        // Every id in skills.json and what it must classify as. The 36 rows
        // that existed before milestone D are the plan's own audit table
        // (1.10) transcribed; the ten spell-expansion rows are all casts, as
        // the plan states for all thirteen.
        private static readonly Dictionary<string, bool> AuditedTable = new Dictionary<string, bool>
        {
            // Shawn's kit. The three that move the body are the three the
            // sheep throws himself at something with.
            { "shear", true },
            { "battering_ram", true },
            { "headbutt", true },
            { "woolgathering", false },
            { "provoke", false },
            { "black_ram_mode", false },
            { "fleece_ward", false },
            { "shatter", false },
            { "wail", false },
            { "gift_mana", false },
            { "gift_fury", false },
            { "gift_haste", false },
            { "tuck_in", false },
            { "placeholder_shawn_capstone", false },

            // The four original books, and Odette's kit. All casts.
            { "mud_burst", false },
            { "frost_flare", false },
            { "cinderfault", false },
            { "lightning_bolt", false },
            { "prismatic_orb", false },
            { "mend", false },
            { "prism_ward", false },

            // Bjorn. A brawler's damage is all body.
            { "placeholder_brawler_slam", true },
            { "rampage", true },
            { "hack", true },
            { "headsplitter", true },
            { "berserk", false },
            { "placeholder_brawler_provoke", false },
            { "placeholder_brawler_ward", false },
            { "second_wind", false },
            { "gorge", true },
            { "unbroken", false },
            { "bjorn_cursed_blood", false },
            { "hold_the_line", false },
            { "plant_the_shield", false },
            { "shield_bash", true },

            // The nine authored enemy abilities. boulder_slam is the
            // judgement call on the true side -- a thrown boulder is the golem
            // moving to act, although it authors no approach -- and
            // spore_cloud is the one on the false side, a cloud rather than a
            // charge, although the treant's other ability is a lunge.
            { "boulder_slam", true },
            { "overhead_slam", true },
            { "grapple", true },
            { "barrel_roll", true },
            { "trunk_slam", true },
            { "roar", false },
            { "shell_up", false },
            { "spore_cloud", false },
            { "bog_mud_burst", false },

            // The Bellwether's kit (PLAN_BELLWETHER_KIT M5): the rake is a
            // lunge, so a rooted Bellwether cannot scratch; the chains and the
            // knell are casts it throws from where it stands.
            { "bellwether_scratch", true },
            { "dark_chains", false },
            { "death_knell", false },

            // The spell expansion. All thirteen are casts; these are the
            // twelve landed through milestone E, plus Court in F.
            { "gilded_aegis", false },
            { "winters_rebuke", false },
            { "vipers_bite", false },
            { "crownfall", false },
            { "ashen_reckoning", false },
            { "blackglass_spear", false },
            { "borrowed_moment", false },
            { "gale_scythe", false },
            { "palace_passage", false },
            { "velvet_shackles", false },
            { "censer_of_embers", false },
            { "thorn_tithe", false },
            { "court_of_whispers", false },
        };

        private static List<string> SkillBlocks() =>
            JsonBlocks.ObjectsInArray(
                File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project", "ContentData", "skills.json")),
                "skills");

        private static bool IsDamageRow(string block)
        {
            System.Enum.TryParse(JsonBlocks.String(block, "effect") ?? "", ignoreCase: true, out SkillEffect effect);
            return SkillEffects.IsDamagePipeline(effect);
        }

        [Test]
        public void EveryDamageSkillInContentStatesPhysicalMoveExplicitly()
        {
            int damageRows = 0;
            var silent = new List<string>();

            foreach (string block in SkillBlocks())
            {
                if (!IsDamageRow(block)) continue;

                damageRows++;
                if (!JsonBlocks.HasKey(block, "physicalMove"))
                {
                    silent.Add(JsonBlocks.String(block, "id") ?? "(no id)");
                }
            }

            Assert.GreaterOrEqual(damageRows, MinimumDamageRowsExpected,
                $"Only {damageRows} damage rows were scanned, against {MinimumDamageRowsExpected} authored -- " +
                "the scan is not seeing skills.json, so this rule would pass an entirely unclassified catalogue.");

            CollectionAssert.IsEmpty(silent,
                "Every damage row must state physicalMove -- true for a swing, charge, lunge or thrown body " +
                "blow, false for a cast. It cannot be derived from the damage element, and Rooted refuses " +
                "exactly the rows that say true. Silent: " + string.Join(", ", silent));
        }

        [Test]
        public void TheClassificationOfEverySkillMatchesTheAuditedTable()
        {
            var fromContent = new Dictionary<string, bool>();
            foreach (string block in SkillBlocks())
            {
                string id = JsonBlocks.String(block, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    fromContent[id] = JsonBlocks.Bool(block, "physicalMove") ?? false;
                }
            }

            CollectionAssert.AreEquivalent(AuditedTable.Keys, fromContent.Keys,
                "skills.json and the audited table name different skills. A row added without a line in " +
                "this table is a row nobody has classified; a line left behind names a row that is gone.");

            var wrong = fromContent
                .Where(row => AuditedTable[row.Key] != row.Value)
                .Select(row => $"{row.Key}: content says {row.Value}, the audit says {AuditedTable[row.Key]}")
                .ToList();

            CollectionAssert.IsEmpty(wrong,
                "The audited classification is the contract Rooted reads. " + string.Join("; ", wrong));
        }

        // THE OTHER HALF OF THE CLASSIFICATION, and it has no content row to
        // carry it: every combatant's plain attack. Stated as a constant so
        // the claim has one place to be read, and pinned here so it cannot be
        // flipped to make a test pass -- a shackled actor that can still swing
        // is the whole spell failing.
        [Test]
        public void ThePlainAttackIsPhysicalWithNoRowToSaySo()
        {
            Assert.IsTrue(CombatActions.PlainAttackIsPhysicalMove,
                "a swing is a physical move by construction; there is no skills.json row for it");
        }
    }
}
