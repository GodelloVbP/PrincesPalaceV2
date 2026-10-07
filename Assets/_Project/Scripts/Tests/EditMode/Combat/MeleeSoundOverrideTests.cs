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

            var orb = JsonBlocks.ObjectsInArray(json, "skills").Single(s => JsonBlocks.String(s, "id") == "prismatic_orb");
            foreach (var path in new[]
            {
                "\"sfxPath\": \"Audio/Sfx/prismatic_orb_earth_impact\"",
                "\"sfxPath\": \"Audio/Sfx/prismatic_orb_fire_impact\"",
                "\"sfxPath\": \"Audio/Sfx/prismatic_orb_wind_impact\"",
                "\"castSfxPath\": \"Audio/Sfx/prismatic_orb_water_cast\"",
                "\"sfxPath\": \"Audio/Sfx/prismatic_orb_water_impact\"",
            })
                StringAssert.Contains(path, orb);
        }

        [Test]
        public void TheSpellPackCuesAreWiredToTheirSpells()
        {
            string json = File.ReadAllText(ContentPath("skills.json"));
            var impact = new Dictionary<string, string>
            {
                ["winters_rebuke"] = "winters_rebuke_impact",
                ["vipers_bite"] = "vipers_bite_impact",
                ["spore_cloud"] = "spore_cloud_impact",
                ["ashen_reckoning"] = "ashen_reckoning_impact",
                ["censer_of_embers"] = "censer_of_embers_impact",
                ["blackglass_spear"] = "blackglass_spear_impact",
                ["death_knell"] = "death_knell_impact",
                ["court_of_whispers"] = "court_of_whispers_impact",
                ["gale_scythe"] = "gale_scythe_impact",
                ["thorn_tithe"] = "thorn_tithe_impact",
                ["cinderfault"] = "cinderfault_eruption_impact",
                ["crownfall"] = "crownfall_impact",
                ["borrowed_moment"] = "borrowed_moment_impact",
                ["gilded_aegis"] = "gilded_aegis_impact",
                ["palace_passage"] = "palace_passage_impact",
            };
            var skills = JsonBlocks.ObjectsInArray(json, "skills");
            foreach (var pair in impact)
            {
                var entry = skills.Single(s => JsonBlocks.String(s, "id") == pair.Key);
                StringAssert.Contains("\"sfxPath\": \"Audio/Sfx/" + pair.Value + "\"", entry);
            }

            StringAssert.Contains("\"castSfxPath\": \"Audio/Sfx/gale_scythe_cast\"",
                skills.Single(s => JsonBlocks.String(s, "id") == "gale_scythe"));

        }

        // Each cast cue is the tail of a riser, cut to the seconds between the
        // beat opening and the blow (a Hold beat with no windup pose:
        // FightBeatPlayer plays castSfxPath, then waits hitCueSeconds). A cue
        // longer than that spends its peak after the impact; shorter, it ends
        // in silence before it. Seconds are literals, per tools/make_spell_cast_cues.py.
        [Test]
        public void TheSpellCastCuesFitTheirBlows()
        {
            string json = File.ReadAllText(ContentPath("skills.json"));
            var skills = JsonBlocks.ObjectsInArray(json, "skills");
            var single = new (string spell, string cue, float seconds)[]
            {
                ("winters_rebuke", "winters_rebuke_cast", 0.34f),
                ("vipers_bite", "vipers_bite_cast", 0.40f),
                ("spore_cloud", "spore_cloud_cast", 0.35f),
                ("ashen_reckoning", "ashen_reckoning_cast", 0.60f),
                ("censer_of_embers", "censer_of_embers_cast", 1.00f),
                ("blackglass_spear", "blackglass_spear_cast", 0.70f),
                ("death_knell", "death_knell_cast", 0.50f),
                ("court_of_whispers", "court_of_whispers_cast", 0.70f),
                ("palace_passage", "palace_passage_cast", 0.30f),
                ("thorn_tithe", "thorn_tithe_cast", 0.40f),
                ("crownfall", "crownfall_cast", 0.34f),
                ("borrowed_moment", "borrowed_moment_cast", 0.30f),
                ("velvet_shackles", "velvet_shackles_cast", 0.40f),
                ("gilded_aegis", "gilded_aegis_cast", 0.30f),
                ("cinderfault", "cinderfault_startup", 0.2167f),
            };
            foreach (var (spell, cue, seconds) in single)
            {
                var entry = skills.Single(s => JsonBlocks.String(s, "id") == spell);
                StringAssert.Contains("\"castSfxPath\": \"Audio/Sfx/" + cue + "\"", entry);
                var hit = Regex.Match(entry, "\"hitCueSeconds\": ([0-9.]+)");
                Assert.IsTrue(hit.Success, spell + " has no hitCueSeconds");
                Assert.AreEqual(seconds, float.Parse(hit.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 0.001f, spell + " hitCueSeconds");
                AssertWavSeconds(cue, seconds);
            }

            // The orb's elements share one skill row, so the pairing is checked
            // against the impact path each cast sits beside.
            var orb = new (string element, float seconds)[]
            {
                ("earth", 0.3467f), ("fire", 0.4167f), ("wind", 0.4167f),
            };
            foreach (var (element, seconds) in orb)
            {
                StringAssert.IsMatch(
                    "\"sfxPath\": \"Audio/Sfx/prismatic_orb_" + element + "_impact\",[ \r\n]+\"castSfxPath\": \"Audio/Sfx/prismatic_orb_" + element + "_cast\"",
                    json);
                AssertWavSeconds("prismatic_orb_" + element + "_cast", seconds);
            }
        }

        private static void AssertWavSeconds(string cue, float expected)
        {
            string path = Path.Combine(ResourcesRoot(), "Audio", "Sfx", cue + ".wav");
            Assert.IsTrue(File.Exists(path), cue + ".wav is missing");
            // 44-byte header, 16-bit mono at 44.1 kHz: what the cutting tool writes.
            float seconds = (new FileInfo(path).Length - 44) / 2f / 44100f;
            Assert.AreEqual(expected, seconds, 0.002f, cue + " length");
        }

        // The censer's 1.0 s riser peaks on the blow: the loudest 50 ms window of the
        // recut clip ends inside its last 0.15 s, and the first half is quieter than the last.
        [Test]
        public void TheCenserCastRiserPeaksOnTheBlow()
        {
            string path = Path.Combine(ResourcesRoot(), "Audio", "Sfx", "censer_of_embers_cast.wav");
            var bytes = File.ReadAllBytes(path);
            int count = (bytes.Length - 44) / 2;
            var x = new double[count];
            for (int i = 0; i < count; i++) x[i] = System.BitConverter.ToInt16(bytes, 44 + 2 * i) / 32768.0;

            int win = 2205; // 50 ms
            int best = 0;
            double bestRms = -1;
            for (int start = 0; start + win <= count; start += 441)
            {
                double sum = 0;
                for (int i = start; i < start + win; i++) sum += x[i] * x[i];
                double rms = System.Math.Sqrt(sum / win);
                if (rms > bestRms) { bestRms = rms; best = start + win; }
            }

            Assert.GreaterOrEqual(best, count - (int)(0.15 * 44100), "the loudest 50 ms ends in the last 0.15 s");

            double first = 0, last = 0;
            for (int i = 0; i < count / 2; i++) first += x[i] * x[i];
            for (int i = count / 2; i < count; i++) last += x[i] * x[i];
            Assert.Greater(last, first * 2.0, "it builds");
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

        // The Earth orb's blow cue starts ON its transient. The source clip had a
        // 0.25 s crackle before the strike, which fired 0.25 s after the contact
        // beat; its sibling elements all peak inside their first 90 ms.
        [Test]
        public void ThePrismaticOrbImpactsStartOnTheirTransient()
        {
            foreach (var element in new[] { "earth", "fire", "wind", "water" })
            {
                var samples = ReadMono16("prismatic_orb_" + element + "_impact");
                int limit = 44100 * 15 / 100; // 150 ms; water, the slowest, peaks at 136 ms
                int peakAt = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (System.Math.Abs((int)samples[i]) > System.Math.Abs((int)samples[peakAt])) peakAt = i;
                }
                Assert.Less(peakAt, limit, element + " peaks " + peakAt / 44100f + " s in");
            }
        }

        // An impact cue fires on the payoff frame, so its loudest sample belongs at
        // the clip's start: 50 ms is 3 frames at 60 fps. Thorn Tithe's and Borrowed
        // Moment's clips peaked 0.15 s and 0.23 s in (9 and 14 frames late).
        [Test]
        public void TheRitualImpactsStartOnTheirTransient()
        {
            foreach (var cue in new[] { "thorn_tithe_impact", "borrowed_moment_impact", "gilded_aegis_impact" })
            {
                var samples = ReadMono16(cue);
                int peakAt = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (System.Math.Abs((int)samples[i]) > System.Math.Abs((int)samples[peakAt])) peakAt = i;
                }
                Assert.Less(peakAt, 44100 * 5 / 100, cue + " peaks " + peakAt / 44100f + " s in");
            }
        }

        // A cast riser's loudest sample sits in its last 60 ms, so the swell
        // resolves on the blow; a riser trimmed short would lose its crack.
        [Test]
        public void TheRitualRisersPeakJustBeforeTheirBlow()
        {
            foreach (var cue in new[] { "velvet_shackles_cast", "gilded_aegis_cast", "crownfall_cast" })
            {
                var samples = ReadMono16(cue);
                int peakAt = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (System.Math.Abs((int)samples[i]) > System.Math.Abs((int)samples[peakAt])) peakAt = i;
                }
                Assert.Greater(peakAt, samples.Length - 44100 * 6 / 100, cue + " peaks " + peakAt / 44100f + " s in");
            }
        }

        [Test]
        public void TheNewRitualCuesHaveALevelEntry()
        {
            string levels = File.ReadAllText(Path.Combine(ResourcesRoot(), "Audio", "audio_levels.json"));
            foreach (var cue in new[] { "velvet_shackles_cast", "gilded_aegis_cast", "gilded_aegis_impact" })
            {
                StringAssert.Contains("\"path\": \"Sfx/" + cue + "\"", levels);
            }
        }

        // Status cues: six takes (three per family), one entry each in the gain table.
        [Test]
        public void TheStatusCuesExistAndHaveALevelEntry()
        {
            string levels = File.ReadAllText(Path.Combine(ResourcesRoot(), "Audio", "audio_levels.json"));
            foreach (var cue in new[]
            {
                "status_ward_1", "status_ward_2", "status_ward_3",
                "status_regen_1", "status_regen_2", "status_regen_3",
            })
            {
                AssertWavSeconds(cue, 1.2f);
                StringAssert.Contains("\"path\": \"Sfx/" + cue + "\"", levels);
            }
        }

        private static short[] ReadMono16(string cue)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(ResourcesRoot(), "Audio", "Sfx", cue + ".wav"));
            var samples = new short[(bytes.Length - 44) / 2];
            System.Buffer.BlockCopy(bytes, 44, samples, 0, samples.Length * 2);
            return samples;
        }

        private static string ContentPath(string file) => Path.Combine(ProjectRoot(), "ContentData", file);

        private static string ResourcesRoot() => Path.Combine(ProjectRoot(), "Resources");
    }
}
