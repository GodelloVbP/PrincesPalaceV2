using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // DOES THE STAMP NAME THE THINGS THE CURRENT JSON ACTUALLY PRODUCES?
    //
    // ContentFreshnessTests beside this file asks four questions about the
    // stamp -- is it there, do its inputs hash the same, is every asset it
    // names on disk, is anything on disk it does not name -- and every one of
    // them takes the stamp's id list on trust. So a build that wrote the wrong
    // ids wrote a stamp that agrees with itself perfectly: the assets exist,
    // nothing is stray, the hash matches, and the catalogue is a catalogue of
    // something else. The plan this work came from promised exactly this check
    // and it was never written.
    //
    // What is compared: for each of the ten folders a build writes, the ids
    // the REAL resolvers produce from the CURRENT ContentData/*.json against
    // the ids the stamp lists. A difference is named, both ways round.
    //
    // TWO PARSERS, ONE ASSERTION. The Raw*Entry types are plain [Serializable]
    // classes with public fields, which both Unity's JsonUtility and
    // System.Text.Json read the same way -- and neither host has the other's
    // serializer (Unity ships no System.Text.Json; the dotnet host has no
    // engine). Rather than exclude this file from the fast loop the way the
    // three JsonUtility pins are excluded, it uses whichever parser its host
    // has. The risk that buys is real and worth naming: the two could disagree
    // about some field, and then one host would be green for a reason the
    // other is not. It is bounded by what is asserted here -- ids, which are
    // plain strings -- and by the full gate compiling the Unity side.
    //
    // WHAT THIS DOES NOT COVER. The three bespoke Items generators
    // (items.json, itemsets.json, weapons.json) are mirrored, including their
    // first-writer-wins collision rule, because they all land in one folder;
    // that mirror is the one piece of ContentBuilder logic restated here, and
    // it is restated rather than skipped because Items is 646 of the 811
    // assets and skipping it would leave the vacuity guard as the only thing
    // holding the check up.
    public class ContentStampIdsTests
    {
        private const string Fix =
            "\n\nRun: powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1" +
            "\n(or tools/preview.ps1 -Build, which picks that route or the open-Editor one for you).";

        // Ten folders today. Eight is the same floor ContentFreshnessTests
        // uses: below it, the comparison is describing something other than
        // this catalogue and every agreement it reports is an accident.
        private const int MinimumTypesCompared = 8;

        private delegate bool ResolveAll<TRaw, TResolved>(
            IReadOnlyList<TRaw> entries, out List<TResolved> resolved, out List<string> errors);

        private static string RepoRoot() => RepoTree.Root();

        private static string DataPath(string file) =>
            Path.Combine(RepoRoot(), "Assets", "_Project", "ContentData", file);

        private static ContentStamp Stamp()
        {
            string path = Path.Combine(RepoRoot(), "Assets", "_Project", "Resources", "Content", "content_stamp.json");

            Assert.IsTrue(File.Exists(path),
                "There is no Resources/Content/content_stamp.json, so no content build has completed against " +
                "this tree and there is nothing to compare the resolvers against." + Fix);

            return ContentStamp.Parse(File.ReadAllText(path));
        }

        // The one line that differs between the two hosts. UNITY_5_3_OR_NEWER
        // is defined by every Unity since 5.3 and by nothing else, so the
        // dotnet host takes the other branch without needing a define of its
        // own.
        private static T ParseFile<T>(string path)
        {
            string json = File.ReadAllText(path);
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(json);
#else
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions
            {
                // Fields, because every Raw*Entry member is one.
                //
                // CASE-SENSITIVE, deliberately, and it is not a preference:
                // JsonUtility matches a JSON key to a field name exactly, so
                // case-insensitivity here would accept files Unity's parser
                // does not. It also breaks outright -- SpellPresentation has
                // both a `anchor` field and an `Anchor` property, which
                // System.Text.Json reports as a name collision the moment
                // names stop being case-sensitive.
                IncludeFields = true,
            });
#endif
        }

        // Resolve one type the way ContentBuilder.Build<> does: read the file,
        // hand the raw entries to the type's own resolver, refuse the WHOLE
        // type if any entry fails, and apply the type's inclusion rule.
        private static List<string> Resolve<TRaw, TResolved>(
            string label,
            TRaw[] entries,
            ResolveAll<TRaw, TResolved> resolve,
            Func<TResolved, string> assetName,
            Func<TResolved, bool> include = null)
        {
            Assert.IsNotNull(entries, $"{label}: the JSON parsed to nothing, so this comparison would be vacuous.");

            bool ok = resolve(entries ?? Array.Empty<TRaw>(), out var resolved, out var errors);

            Assert.IsTrue(ok,
                $"{label}: the authored JSON does not resolve, so no content build can have written the stamp " +
                $"beside it -- {string.Join("; ", errors.Take(5))}");

            return resolved
                .Where(record => include == null || include(record))
                .Select(assetName)
                .ToList();
        }

        // THE ITEMS FOLDER, which is three generators writing into one place.
        //
        // ContentBuilder writes items.json's output first, then the armour
        // sets, then the weapons, and each of the last two SKIPS an id whose
        // asset file already exists (logging an error rather than
        // overwriting). First writer wins, in that order -- mirrored here
        // rather than guessed at.
        private static List<string> ItemIds()
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(IEnumerable<string> more)
            {
                foreach (string id in more)
                {
                    if (seen.Add(id)) ids.Add(id);
                }
            }

            Add(Resolve<RawItemEntry, ResolvedItem>(
                "items.json", ParseFile<RawItemFile>(DataPath("items.json")).items,
                ItemEntryResolver.TryResolveAll, item => item.Id));

            Add(Resolve<RawItemSetEntry, ResolvedSetPiece>(
                "itemsets.json", ParseFile<RawItemSetFile>(DataPath("itemsets.json")).sets,
                ItemSetEntryResolver.TryResolveAll, piece => piece.Id));

            Add(Resolve<RawWeaponEntry, ResolvedWeapon>(
                "weapons.json", ParseFile<RawWeaponFile>(DataPath("weapons.json")).families,
                WeaponEntryResolver.TryResolveAll, weapon => weapon.Id));

            return ids;
        }

        // Achievements first, because relics are validated against their ids
        // -- the same ordering ContentBuilder calls load-bearing.
        private static List<string> AchievementIds() =>
            Resolve<RawAchievementEntry, ResolvedAchievement>(
                "achievements.json", ParseFile<RawAchievementFile>(DataPath("achievements.json")).achievements,
                AchievementEntryResolver.TryResolveAll, achievement => achievement.Id);

        private static List<string> RelicIds()
        {
            var achievementIds = AchievementIds();

            bool Resolver(IReadOnlyList<RawRelicEntry> entries, out List<ResolvedRelic> resolved,
                          out List<string> errors) =>
                RelicEntryResolver.TryResolveAll(entries, achievementIds, out resolved, out errors);

            return Resolve<RawRelicEntry, ResolvedRelic>(
                "relics.json", ParseFile<RawRelicFile>(DataPath("relics.json")).relics,
                Resolver, relic => relic.Id);
        }

        private static Dictionary<string, List<string>> ResolvedByFolder() =>
            new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["Characters"] = Resolve<RawCharacterEntry, ResolvedCharacter>(
                    "characters.json", ParseFile<RawCharacterFile>(DataPath("characters.json")).characters,
                    CharacterEntryResolver.TryResolveAll, character => character.Id),

                ["Talents"] = Resolve<RawTalentEntry, ResolvedTalent>(
                    "talents.json", ParseFile<RawTalentFile>(DataPath("talents.json")).talents,
                    TalentEntryResolver.TryResolveAll, talent => talent.Id),

                ["Upgrades"] = Resolve<RawUpgradeEntry, ResolvedUpgrade>(
                    "upgrades.json", ParseFile<RawUpgradeFile>(DataPath("upgrades.json")).upgrades,
                    UpgradeEntryResolver.TryResolveAll, upgrade => upgrade.Id),

                // BENCHED MONSTERS ARE VALIDATED AND NOT WRITTEN. The
                // inclusion rule is ContentBuilder's own `include: enemy =>
                // enemy.Active`, mirrored rather than assumed: a stamp that
                // listed an inactive mob would mean ContentDatabase can spawn
                // something the author switched off.
                ["Enemies"] = Resolve<RawEnemyEntry, ResolvedEnemy>(
                    "enemies.json", ParseFile<RawEnemyFile>(DataPath("enemies.json")).enemies,
                    EnemyEntryResolver.TryResolveAll, enemy => enemy.Id, include: enemy => enemy.Active),

                ["Items"] = ItemIds(),

                // A tier has no id of its own; the asset NAME is what the
                // stamp records and what ContentDatabase browses.
                ["SpellTiers"] = Resolve<RawSpellTierEntry, ResolvedSpellTier>(
                    "spells.json", ParseFile<RawSpellTierFile>(DataPath("spells.json")).tiers,
                    SpellTierEntryResolver.TryResolveAll, tier => $"level_{tier.Level}"),

                ["Skills"] = Resolve<RawSkillEntry, ResolvedSkill>(
                    "skills.json", ParseFile<RawSkillFile>(DataPath("skills.json")).skills,
                    SkillEntryResolver.TryResolveAll, skill => skill.Id),

                ["Modifiers"] = Resolve<RawModifierEntry, ResolvedModifier>(
                    "modifiers.json", ParseFile<RawModifierFile>(DataPath("modifiers.json")).modifiers,
                    ModifierEntryResolver.TryResolveAll, modifier => modifier.Id),

                ["Achievements"] = AchievementIds(),
                ["Relics"] = RelicIds(),
            };

        [Test]
        public void EveryTypeStampsExactlyTheIdsItsResolverProduces()
        {
            var stamp = Stamp();
            var resolved = ResolvedByFolder();
            var complaints = new List<string>();

            foreach (var pair in resolved)
            {
                if (!stamp.IdsByFolder.TryGetValue(pair.Key, out var stamped))
                {
                    complaints.Add($"{pair.Key}: the stamp lists no such folder, but the current JSON resolves " +
                                   $"{pair.Value.Count} of them.");
                    continue;
                }

                var expected = pair.Value.OrderBy(id => id, StringComparer.Ordinal).ToList();
                var actual = stamped.OrderBy(id => id, StringComparer.Ordinal).ToList();

                var missing = expected.Except(actual, StringComparer.Ordinal).ToList();
                var extra = actual.Except(expected, StringComparer.Ordinal).ToList();

                if (missing.Count > 0)
                {
                    complaints.Add($"{pair.Key}: the JSON resolves {missing.Count} id(s) the stamp does not list " +
                                   $"-- {string.Join(", ", missing.Take(10))}");
                }

                if (extra.Count > 0)
                {
                    complaints.Add($"{pair.Key}: the stamp lists {extra.Count} id(s) the JSON does not resolve " +
                                   $"-- {string.Join(", ", extra.Take(10))}");
                }

                if (missing.Count == 0 && extra.Count == 0 && expected.Count != actual.Count)
                {
                    complaints.Add($"{pair.Key}: the same ids, {expected.Count} resolved against {actual.Count} " +
                                   "stamped, so one of them is written twice.");
                }
            }

            CollectionAssert.IsEmpty(complaints,
                "The generated content tree was not built from the ContentData/*.json files on disk now -- the " +
                "ids differ, per type, below. Every other freshness check reads the stamp's own id list and so " +
                "agrees with whatever it says:\n  " + string.Join("\n  ", complaints) + Fix);
        }

        // The vacuity guard, and it is not decoration: every assertion above
        // is a comparison between two lists, and two empty lists are equal.
        [Test]
        public void EnoughTypesAreComparedForTheComparisonToMeanSomething()
        {
            var resolved = ResolvedByFolder();

            Assert.GreaterOrEqual(resolved.Count, MinimumTypesCompared,
                $"only {resolved.Count} content type(s) are mirrored here, so the check above covers less of the " +
                "catalogue than it appears to.");

            var empty = resolved.Where(p => p.Value.Count == 0).Select(p => p.Key).ToList();

            CollectionAssert.IsEmpty(empty,
                $"{empty.Count} type(s) resolved to no ids at all, so their comparison passes against anything " +
                "the stamp says -- " + string.Join(", ", empty));
        }

        // Ten folders are mirrored above and a build writes ten. A type added
        // to ContentBuilder without a row here would be a type this check
        // silently stops covering, and the stamp is the only place that says
        // how many there are.
        [Test]
        public void NoFolderInTheStampIsLeftUnmirrored()
        {
            var stamp = Stamp();
            var unmirrored = stamp.IdsByFolder.Keys
                .Except(ResolvedByFolder().Keys, StringComparer.Ordinal)
                .ToList();

            CollectionAssert.IsEmpty(unmirrored,
                $"{unmirrored.Count} folder(s) the last build wrote are not mirrored by this test, so nothing " +
                "compares their ids against the JSON they came from -- " + string.Join(", ", unmirrored) +
                ". Add them to ResolvedByFolder, or say in a comment why they cannot be mirrored.");
        }
    }
}
