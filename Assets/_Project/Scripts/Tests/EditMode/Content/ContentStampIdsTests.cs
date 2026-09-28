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
    // What is compared: for each of the twelve folders a build writes, the ids
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
    //
    // TURNED BACK ON. Phase 2 of progression v2 cut RewardTrack.MaxLevel to
    // 40 while the authored tracks were still the hundred-level content, so
    // reward_tracks.json stopped resolving and every test here -- all three
    // walk every content type through ResolvedByFolder -- failed at the same
    // first line. That was a class-level, dated, greppable disable rather
    // than a skip keyed to content shape (.claude/rules/tests.md "Writing tests"'s distinction),
    // and phase 4, which rewrote the three tracks, is what removes it.
    public class ContentStampIdsTests
    {
        private const string Fix =
            "\n\nRun: powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1" +
            "\n(or tools/preview.ps1 -Build, which picks that route or the open-Editor one for you).";

        // Twelve folders today. Eight is the same floor ContentFreshnessTests
        // uses: below it, the comparison is describing something other than
        // this catalogue and every agreement it reports is an accident.
        private const int MinimumTypesCompared = 8;

        private delegate bool ResolveAll<TRaw, TResolved>(
            IReadOnlyList<TRaw> entries, out List<TResolved> resolved, out List<string> errors);

        // RepoRoot/DataPath/ParseFile live on Shared/ContentDataFiles --
        // RewardTrackContentPinTests uses the same trio.
        private static ContentStamp Stamp()
        {
            string path = Path.Combine(ContentDataFiles.RepoRoot(), "Assets", "_Project", "Resources", "Content", "content_stamp.json");

            Assert.IsTrue(File.Exists(path),
                "There is no Resources/Content/content_stamp.json, so no content build has completed against " +
                "this tree and there is nothing to compare the resolvers against." + Fix);

            return ContentStamp.Parse(File.ReadAllText(path));
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
                "items.json", ContentDataFiles.ParseFile<RawItemFile>(ContentDataFiles.DataPath("items.json")).items,
                ItemEntryResolver.TryResolveAll, item => item.Id));

            Add(Resolve<RawItemSetEntry, ResolvedSetPiece>(
                "itemsets.json", ContentDataFiles.ParseFile<RawItemSetFile>(ContentDataFiles.DataPath("itemsets.json")).sets,
                ItemSetEntryResolver.TryResolveAll, piece => piece.Id));

            Add(Resolve<RawWeaponEntry, ResolvedWeapon>(
                "weapons.json", ContentDataFiles.ParseFile<RawWeaponFile>(ContentDataFiles.DataPath("weapons.json")).families,
                WeaponEntryResolver.TryResolveAll, weapon => weapon.Id));

            return ids;
        }

        // Pools before characters, because a character's primaryPoolId is
        // validated against these -- the same ordering ContentBuilder calls
        // load-bearing, mirrored rather than assumed.
        private static List<ResolvedPool> ResolvePools() =>
            ResolveAllOf<RawPoolEntry, ResolvedPool>(
                "pools.json", ContentDataFiles.ParseFile<RawPoolFile>(ContentDataFiles.DataPath("pools.json")).pools,
                PoolEntryResolver.TryResolveAll);

        private static List<string> PoolIds() => ResolvePools().Select(pool => pool.Id).ToList();

        // The skill resolver with the two OWNER-pool facts already bound, the
        // same shape ResolveCharacters uses for the pool ids and for the same
        // reason: skills.json stopped resolving against itself alone the
        // moment a rule turned on the pool its owner carries. Bjorn's slam
        // and brace are free because `fury` starts at zero, and a resolve
        // that was not told so refuses the file outright. PoolOwnership is
        // the one derivation, shared with ContentBuilder.
        private static List<ResolvedSkill> ResolveSkills()
        {
            var pools = ResolvePools();
            var characters = ResolveCharacters();
            var bookRefusers = PoolOwnership.BookRefusers(pools, characters);
            var zeroStartOwners = PoolOwnership.ZeroStartOwners(pools, characters);
            var primaryPoolOwners = PoolOwnership.PrimaryPoolOwners(pools, characters);

            bool Resolver(IReadOnlyList<RawSkillEntry> entries, out List<ResolvedSkill> resolved,
                          out List<string> errors) =>
                SkillEntryResolver.TryResolveAll(entries, bookRefusers, zeroStartOwners, primaryPoolOwners, out resolved, out errors);

            return ResolveAllOf<RawSkillEntry, ResolvedSkill>(
                "skills.json", ContentDataFiles.ParseFile<RawSkillFile>(ContentDataFiles.DataPath("skills.json")).skills,
                Resolver);
        }

        // The character resolver with the real pool ids already bound, so
        // both of the two places that resolve characters here go through one
        // statement of "which pools exist" rather than two.
        private static List<ResolvedCharacter> ResolveCharacters()
        {
            var poolIds = PoolIds();

            bool Resolver(IReadOnlyList<RawCharacterEntry> entries, out List<ResolvedCharacter> resolved,
                          out List<string> errors) =>
                CharacterEntryResolver.TryResolveAll(entries, poolIds, out resolved, out errors);

            return ResolveAllOf<RawCharacterEntry, ResolvedCharacter>(
                "characters.json", ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters,
                Resolver);
        }

        // Achievements first, because relics are validated against their ids
        // -- the same ordering ContentBuilder calls load-bearing.
        private static List<string> AchievementIds() =>
            Resolve<RawAchievementEntry, ResolvedAchievement>(
                "achievements.json", ContentDataFiles.ParseFile<RawAchievementFile>(ContentDataFiles.DataPath("achievements.json")).achievements,
                AchievementEntryResolver.TryResolveAll, achievement => achievement.Id);

        private static List<string> RelicIds()
        {
            var achievementIds = AchievementIds();
            var characterIds = ResolveCharacters().Select(c => c.Id).ToList();

            bool Resolver(IReadOnlyList<RawRelicEntry> entries, out List<ResolvedRelic> resolved,
                          out List<string> errors) =>
                RelicEntryResolver.TryResolveAll(entries, achievementIds, characterIds, out resolved, out errors);

            return Resolve<RawRelicEntry, ResolvedRelic>(
                "relics.json", ContentDataFiles.ParseFile<RawRelicFile>(ContentDataFiles.DataPath("relics.json")).relics,
                Resolver, relic => relic.Id);
        }

        // Characters and skills first, because a reward track is validated
        // against both -- the same ordering ContentBuilder.BuildRewardTracks
        // depends on, and the same context map it assembles.
        //
        // BUILD THE REAL CONTEXT, not an empty one -- an empty tracks array
        // has nothing to validate, but a track that IS authored validates
        // against a character absent from the map as a blank context (see
        // RewardTrackCharacterContext's own header), so a stub map here
        // would silently refuse every rule 4/5 and skillId check rather than
        // exercising them.
        //
        // ONE HALF DIFFERS BETWEEN THE TWO MAPS, deliberately, and it is the
        // rule that turns on: Level1DamageTypes
        // is the character's OWN kit, while SkillDisplayNames is the WHOLE
        // catalogue, because a track may grant a skill authored to somebody
        // else and every book-only spell in the game is authored to "sheep".
        private static List<string> RewardTrackIds()
        {
            var characters = ResolveCharacters();
            var skills = ResolveSkills();

            var contexts = RewardTrackCharacterContext.BuildAll(ResolvePools(), characters, skills);

            bool Resolver(IReadOnlyList<RawRewardTrackEntry> entries, out List<ResolvedRewardTrack> resolved,
                          out List<string> errors) =>
                RewardTrackEntryResolver.TryResolveAll(entries, contexts, out resolved, out errors);

            return Resolve<RawRewardTrackEntry, ResolvedRewardTrack>(
                "reward_tracks.json", ContentDataFiles.ParseFile<RawRewardTrackFile>(ContentDataFiles.DataPath("reward_tracks.json")).tracks,
                Resolver, track => track.CharacterId);
        }

        // Characters (for the display-name map an inParty/memberLevel/ability
        // requirement's reason text is baked with) and items (for an `item`
        // effect's id) first -- the same ordering ContentBuilder.BuildEvents
        // depends on.
        private static List<string> EventIds()
        {
            var characterDisplayNames = ResolveCharacters().ToDictionary(c => c.Id, c => c.DisplayName);
            var itemIds = ItemIds();
            var relicDisplayNames = RelicIds().ToDictionary(id => id, id => id);

            // Enemies too: an event fight names them and counts their
            // slotSpan (ContentBuilder.BuildEnemies hands the same map on).
            var enemySlotSpans = ResolveAllOf<RawEnemyEntry, ResolvedEnemy>(
                    "enemies.json", ContentDataFiles.ParseFile<RawEnemyFile>(ContentDataFiles.DataPath("enemies.json")).enemies,
                    EnemyEntryResolver.TryResolveAll)
                .Where(enemy => enemy.Active)
                .ToDictionary(enemy => enemy.Id, enemy => enemy.SlotSpan);

            bool Resolver(IReadOnlyList<RawEventEntry> entries, out List<ResolvedEventDefinition> resolved,
                          out List<string> errors) =>
                EventEntryResolver.TryResolveAll(entries, characterDisplayNames, itemIds, relicDisplayNames,
                    enemySlotSpans, out resolved, out errors);

            return Resolve<RawEventEntry, ResolvedEventDefinition>(
                "events.json", ContentDataFiles.ParseFile<RawEventFile>(ContentDataFiles.DataPath("events.json")).events,
                Resolver, evt => evt.Id);
        }

        // Resolve as above, but keeping the RECORDS rather than their ids --
        // the cross-catalogue context a reward track is validated against
        // needs fields, not names.
        private static List<TResolved> ResolveAllOf<TRaw, TResolved>(
            string label, TRaw[] entries, ResolveAll<TRaw, TResolved> resolve)
        {
            Assert.IsNotNull(entries, $"{label}: the JSON parsed to nothing.");

            bool ok = resolve(entries, out var resolved, out var errors);
            Assert.IsTrue(ok, $"{label}: {string.Join("; ", errors.Take(5))}");

            return resolved;
        }

        private static Dictionary<string, List<string>> ResolvedByFolder() =>
            new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["Pools"] = PoolIds(),

                ["Characters"] = ResolveCharacters().Select(character => character.Id).ToList(),

                ["Talents"] = Resolve<RawTalentEntry, ResolvedTalent>(
                    "talents.json", ContentDataFiles.ParseFile<RawTalentFile>(ContentDataFiles.DataPath("talents.json")).talents,
                    TalentEntryResolver.TryResolveAll, talent => talent.Id),

                ["Upgrades"] = Resolve<RawUpgradeEntry, ResolvedUpgrade>(
                    "upgrades.json", ContentDataFiles.ParseFile<RawUpgradeFile>(ContentDataFiles.DataPath("upgrades.json")).upgrades,
                    UpgradeEntryResolver.TryResolveAll, upgrade => upgrade.Id),

                // BENCHED MONSTERS ARE VALIDATED AND NOT WRITTEN. The
                // inclusion rule is ContentBuilder's own `include: enemy =>
                // enemy.Active`, mirrored rather than assumed: a stamp that
                // listed an inactive mob would mean ContentDatabase can spawn
                // something the author switched off.
                ["Enemies"] = Resolve<RawEnemyEntry, ResolvedEnemy>(
                    "enemies.json", ContentDataFiles.ParseFile<RawEnemyFile>(ContentDataFiles.DataPath("enemies.json")).enemies,
                    EnemyEntryResolver.TryResolveAll, enemy => enemy.Id, include: enemy => enemy.Active),

                ["Items"] = ItemIds(),

                // A tier has no id of its own; the asset NAME is what the
                // stamp records and what ContentDatabase browses.
                ["SpellTiers"] = Resolve<RawSpellTierEntry, ResolvedSpellTier>(
                    "spells.json", ContentDataFiles.ParseFile<RawSpellTierFile>(ContentDataFiles.DataPath("spells.json")).tiers,
                    SpellTierEntryResolver.TryResolveAll, tier => $"level_{tier.Level}"),

                ["Skills"] = ResolveSkills().Select(skill => skill.Id).ToList(),

                ["Modifiers"] = Resolve<RawModifierEntry, ResolvedModifier>(
                    "modifiers.json", ContentDataFiles.ParseFile<RawModifierFile>(ContentDataFiles.DataPath("modifiers.json")).modifiers,
                    ModifierEntryResolver.TryResolveAll, modifier => modifier.Id),

                ["Achievements"] = AchievementIds(),
                ["Relics"] = RelicIds(),
                ["RewardTracks"] = RewardTrackIds(),
                ["Events"] = EventIds(),

                // A cost row has no id of its own either; the asset NAME is
                // what the stamp records, zero-padded so the folder reads in
                // order to a human -- ContentBuilder.BuildLevelCurve's own
                // `level_{row.Level:D2}`.
                ["LevelCurve"] = Resolve<RawLevelCurveEntry, ResolvedLevelCost>(
                    "level_curve.json", ContentDataFiles.ParseFile<RawLevelCurveFile>(ContentDataFiles.DataPath("level_curve.json")).levels,
                    LevelCurveEntryResolver.TryResolveAll, row => $"level_{row.Level:D2}"),
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

        // Twelve folders are mirrored above and a build writes twelve. A type added
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
