using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The per-actor melee clips: a character or monster may name its own swing
    // and impact, and anyone without one leaves the beat's fields empty so
    // Core falls back to the shared ContactCues default. Every path is a
    // literal.
    public class MeleeSoundOverrideTests
    {
        private static ResolvedEnemy Golem() =>
            new ResolvedEnemy("golem", "Golem", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

        private static FightSession Fight(PlayerKit kit, ResolvedEnemy enemy, out CombatantState monster)
        {
            var hero = new CombatantState("Hero", true, 500, 20, 20, 10);
            monster = new CombatantState("Golem", false, 1000, 10, 30, 9);
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(enemy, false) }, new SeededRandom(7)) { DamageVarianceRange = 0f };
            session.Begin();
            return session;
        }

        // The hero swings first (speed 10 against 9); the monster replies inside
        // the same call, so one exchange records a beat for each side.
        private static IReadOnlyList<CombatBeat> Exchange(PlayerKit kit, ResolvedEnemy enemy)
        {
            var session = Fight(kit, enemy, out var monster);
            session.ExecuteAttack(monster);
            return session.DrainBeats();
        }

        private static CombatBeat HeroBeat(PlayerKit kit) =>
            Exchange(kit, Golem()).First(b => b.IsAction && b.Actor.IsPlayerSide);

        private static CombatBeat MonsterBeat(ResolvedEnemy enemy) =>
            Exchange(new PlayerKit("x", CharacterRole.Tank, null, null, null), enemy)
                .First(b => b.IsAction && !b.Actor.IsPlayerSide);

        [Test]
        public void ACharacterWithBothClipsCarriesBothOnItsBeat()
        {
            var beat = HeroBeat(new PlayerKit("sheep", CharacterRole.Tank, null, null, null,
                swingSfxPath: "Audio/Sfx/Shawn/shawn_attack_swipe",
                impactSfxPath: "Audio/Sfx/Shawn/shawn_attack_impact"));

            Assert.AreEqual("Audio/Sfx/Shawn/shawn_attack_swipe", beat.ActorSwingSfx);
            Assert.AreEqual("Audio/Sfx/Shawn/shawn_attack_impact", beat.ActorImpactSfx);
        }

        [Test]
        public void ACharacterWithOnlyAnImpactLeavesTheSwingEmptyForTheDefault()
        {
            var beat = HeroBeat(new PlayerKit("owl", CharacterRole.Support, null, null, null,
                impactSfxPath: "Audio/Sfx/Odette/odette_attack_impact"));

            Assert.AreEqual("", beat.ActorSwingSfx);
            Assert.AreEqual("Audio/Sfx/Odette/odette_attack_impact", beat.ActorImpactSfx);
        }

        [Test]
        public void ACharacterWithNoClipsLeavesBothEmpty()
        {
            var beat = HeroBeat(new PlayerKit("bear", CharacterRole.Tank, null, null, null));

            Assert.AreEqual("", beat.ActorSwingSfx);
            Assert.AreEqual("", beat.ActorImpactSfx);
        }

        [Test]
        public void AMonsterCanNameItsOwnClipsThroughTheSameFields()
        {
            var golem = Golem();
            golem.SwingSfxPath = "Audio/Sfx/golem_swing";
            golem.ImpactSfxPath = "Audio/Sfx/golem_impact";

            var beat = MonsterBeat(golem);

            Assert.AreEqual("Audio/Sfx/golem_swing", beat.ActorSwingSfx);
            Assert.AreEqual("Audio/Sfx/golem_impact", beat.ActorImpactSfx);
        }

        [Test]
        public void AMonsterWithNoClipsLeavesBothEmpty()
        {
            var beat = MonsterBeat(Golem());

            Assert.AreEqual("", beat.ActorSwingSfx);
            Assert.AreEqual("", beat.ActorImpactSfx);
        }

        [Test]
        public void AnExtensionOnAMeleeClipPathIsRefused()
        {
            Assert.IsFalse(ArtPathConvention.Check("row", "swingSfxPath", "Audio/Sfx/x.wav", out var error));
            StringAssert.Contains("swingSfxPath", error);
        }

        // ---- the authored rows ---------------------------------------------------

        [Test]
        public void ShawnHasSwingAndImpactAndOdetteHasImpactOnly()
        {
            string sheep = CharacterRow("sheep");
            string owl = CharacterRow("owl");

            StringAssert.Contains("\"swingSfxPath\": \"Audio/Sfx/Shawn/shawn_attack_swipe\"", sheep);
            StringAssert.Contains("\"impactSfxPath\": \"Audio/Sfx/Shawn/shawn_attack_impact\"", sheep);
            StringAssert.DoesNotContain("swingSfxPath", owl);
            StringAssert.Contains("\"impactSfxPath\": \"Audio/Sfx/Odette/odette_attack_impact\"", owl);
            StringAssert.DoesNotContain("SfxPath", CharacterRow("bear"));
        }

        [Test]
        public void TheImportedSpellCuesAreWiredToTheirSpells()
        {
            string json = File.ReadAllText(ContentPath("skills.json"));
            var mud = JsonBlocks.ObjectsInArray(json, "skills").Where(s =>
                JsonBlocks.String(s, "id") == "mud_burst" || JsonBlocks.String(s, "id") == "bog_mud_burst").ToList();

            Assert.AreEqual(2, mud.Count);
            foreach (var entry in mud)
            {
                StringAssert.Contains("\"castSfxPath\": \"Audio/Sfx/mud_burst_startup\"", entry);
                StringAssert.Contains("\"sfxPath\": \"Audio/Sfx/mud_burst_impact\"", entry);
            }

            StringAssert.Contains("\"castSfxPath\": \"Audio/Sfx/prismatic_orb_water_startup\"", json);
        }

        [Test]
        public void TheRubberduckBlowCuesAreWiredToTheirSpells()
        {
            string json = File.ReadAllText(ContentPath("skills.json"));
            var expected = new Dictionary<string, string>
            {
                ["velvet_shackles"] = "Audio/Sfx/velvet_shackles_impact",
                ["dark_chains"] = "Audio/Sfx/dark_chains_impact",
                ["boulder_slam"] = "Audio/Sfx/boulder_slam_impact",
                ["trunk_slam"] = "Audio/Sfx/trunk_slam_impact",
                ["bellwether_scratch"] = "Audio/Sfx/bellwether_scratch_impact",
                ["censer_of_embers"] = "Audio/Sfx/censer_of_embers_impact",
            };
            var skills = JsonBlocks.ObjectsInArray(json, "skills");
            foreach (var pair in expected)
            {
                var entry = skills.Single(s => JsonBlocks.String(s, "id") == pair.Key);
                StringAssert.Contains("\"sfxPath\": \"" + pair.Value + "\"", entry);
            }
        }

        [Test]
        public void EveryAuthoredSoundPathNamesAFileUnderResources()
        {
            var missing = new List<string>();
            foreach (var file in new[] { "skills.json", "characters.json", "enemies.json" })
            {
                string json = File.ReadAllText(ContentPath(file));
                foreach (Match m in Regex.Matches(json,
                    "\"(?:sfxPath|castSfxPath|swingSfxPath|impactSfxPath)\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string stem = Path.Combine(ResourcesRoot(),
                        m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
                    bool found = new[] { ".wav", ".mp3", ".ogg" }.Any(ext => File.Exists(stem + ext));
                    if (!found) missing.Add(file + ": " + m.Groups[1].Value);
                }
            }

            CollectionAssert.IsEmpty(missing);
        }

        private static string CharacterRow(string id)
        {
            string json = File.ReadAllText(ContentPath("characters.json"));
            string row = JsonBlocks.ObjectsInArray(json, "characters")
                .FirstOrDefault(c => JsonBlocks.String(c, "id") == id);
            Assert.IsNotNull(row, "characters.json has no " + id + " row");
            return row;
        }

        private static string ProjectRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/ContentData from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project");
        }

        private static string ContentPath(string file) => Path.Combine(ProjectRoot(), "ContentData", file);

        private static string ResourcesRoot() => Path.Combine(ProjectRoot(), "Resources");
    }
}
