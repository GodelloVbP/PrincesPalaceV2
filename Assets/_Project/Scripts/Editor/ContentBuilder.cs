using System.Collections.Generic;
using System.IO;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using UnityEditor;
using UnityEngine;

// Generates the default content assets. Content lives under Resources so
// ContentDatabase can load it without any scene wiring; regenerating is
// destructive by design, so hand-authored assets belong in a folder this
// tool does not own once real content starts landing.
public static class ContentBuilder
{
    private const string ContentRoot = "Assets/_Project/Resources/Content";
    private const string CharactersPath = ContentRoot + "/Characters";
    private const string TalentsPath = ContentRoot + "/Talents";
    private const string UpgradesPath = ContentRoot + "/Upgrades";
    private const string EnemiesPath = ContentRoot + "/Enemies";
    private const string ItemsPath = ContentRoot + "/Items";
    private const string SpellTiersPath = ContentRoot + "/SpellTiers";
    private const string SkillsPath = ContentRoot + "/Skills";
    private const string RelicsPath = ContentRoot + "/Relics";
    private const string AchievementsPath = ContentRoot + "/Achievements";
    private const string ModifiersPath = ContentRoot + "/Modifiers";
    private const string RewardTracksPath = ContentRoot + "/RewardTracks";

    // The talent grid's shape is not declared here, and must not be written
    // out here either. The tree's size has one home -- TalentPage.PathCount
    // for its width, TalentSkeleton.SlotCount for its depth -- and
    // TalentEntryResolver derives the authorable bounds from those rather than
    // restating them.
    //
    // Said as a prohibition because the version of this comment that DID write
    // them out said "column 0-11, row 0-5" long after the tree became three
    // paths of twenty-one slots, which is a confident wrong answer sitting in
    // the first file anyone adding content opens.

    // The stamp file every freshness check reads. Under ContentRoot on
    // purpose: RecreateFolder(ContentRoot) deletes the whole tree first, so a
    // build that dies before it gets here leaves NO stamp rather than a stamp
    // describing a catalogue that is no longer on disk.
    private const string StampPath = ContentRoot + "/content_stamp.json";

    // Folder leaf -> the asset names actually written into it this run, and
    // the labels of the types that failed to produce anything.
    //
    // ACCUMULATED AT THE POINT OF WRITING, never restated. The stamp used to
    // be conceivable as a second list built beside the builders, and a second
    // list is a list that drifts -- this project has the scars (the scene-set
    // guard that named four of five scenes, the per-field asset copy that
    // dropped `transform`). Every CreateAsset in this file goes through
    // CreateContentAsset below, so the stamp cannot describe anything but what
    // was written.
    private static readonly SortedDictionary<string, List<string>> WrittenByFolder =
        new SortedDictionary<string, List<string>>(System.StringComparer.Ordinal);
    private static readonly List<string> FailedTypes = new List<string>();

    [MenuItem("Prince's Palace/Build Default Content")]
    public static void BuildDefaultContent()
    {
        WrittenByFolder.Clear();
        FailedTypes.Clear();

        // PHASE TIMINGS, permanently, for the same reason GenerationRun.Mark
        // exists: "the content build is slow" was unactionable until the phases
        // were stamped, and the answer was the per-asset import, not the
        // resolvers. NOT prefixed "[ContentBuilder]" -- that exact string is
        // run_tests_parallel.ps1's failure grep, so a timing line wearing it
        // would fail every build it measured.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var last = System.TimeSpan.Zero;
        void Mark(string what)
        {
            var now = watch.Elapsed;
            Debug.Log($"[ContentTiming] {what}: {(now - last).TotalSeconds:N1}s (total {now.TotalSeconds:N1}s)");
            last = now;
        }

        RecreateFolder(ContentRoot);
        EnsureFolder(CharactersPath);
        EnsureFolder(TalentsPath);
        EnsureFolder(UpgradesPath);
        EnsureFolder(EnemiesPath);
        EnsureFolder(ItemsPath);
        EnsureFolder(SpellTiersPath);
        EnsureFolder(SkillsPath);
        EnsureFolder(RelicsPath);
        EnsureFolder(AchievementsPath);
        EnsureFolder(ModifiersPath);
        EnsureFolder(RewardTracksPath);
        Mark("folders");

        // ONE IMPORT PASS FOR THE WHOLE CATALOGUE, not one per asset.
        //
        // AssetDatabase.CreateAsset imports what it just wrote before it
        // returns, and this method calls it ~950 times (192 content entries
        // plus the expanded weapon and armour-set families). Measured on
        // 2026-09-05: 50.8s warm for the whole method, of which the writes
        // were 48.6s. Bracketing them tells the AssetDatabase to defer every
        // import to the StopAssetEditing below, so the catalogue is imported
        // once. Same assets, same order, same GUID churn -- only the number of
        // import cycles changes.
        //
        // The folder creation above stays OUTSIDE the bracket deliberately:
        // AssetDatabase.IsValidFolder/CreateFolder are the database's own view
        // of the tree, and asking it to create a folder while it is not
        // importing is asking for a folder that is not there yet when
        // CreateAsset needs it.
        //
        // try/finally, not a bare pair: a resolver that throws inside here
        // would otherwise leave the AssetDatabase paused for the rest of the
        // process, and every later Editor operation -- including the ones that
        // report the failure -- would see a database that never refreshes.
        AssetDatabase.StartAssetEditing();
        try
        {
            var characters = BuildCharacters();
            BuildTalents();
            BuildUpgrades();
            BuildEnemies();
            BuildItems();
            BuildSpellTiers();
            var skills = BuildSkills();
            BuildModifiers();

            // ACHIEVEMENTS BEFORE RELICS, and the order is load-bearing: relics
            // are validated against the achievement ids this returns, so building
            // them the other way round would validate against nothing and let a
            // typo'd gate through.
            var achievementIds = BuildAchievements();
            BuildRelics(achievementIds);

            // REWARD TRACKS AFTER CHARACTERS AND SKILLS, same reason: a
            // track's rules 4/5 and its UnlockSkill/signature captions are
            // validated against what those two already resolved -- see
            // docs/PLAN_REWARD_TRACKS.md §4's touch-point table.
            BuildRewardTracks(characters, skills);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        Mark("write assets");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Mark("import");

        // Runs only after every Build* call above has already completed, so
        // a failure here means the generated content itself is wrong (a
        // duplicate id, a 0-HP enemy, an authoring slip like weakness ==
        // resistance) — not that the process was interrupted partway
        // through. ContentDatabase.Reset() forces a real re-read of what
        // was just written rather than trusting whatever state it was
        // already in in this process.
        ContentDatabase.Reset();
        var validationErrors = ContentDatabase.ValidateContent();
        Mark("validate");
        if (validationErrors.Count > 0)
        {
            foreach (string error in validationErrors)
            {
                Debug.LogError($"[ContentBuilder] {error}");
            }

            throw new System.Exception(
                $"ContentBuilder generated {validationErrors.Count} invalid content item(s) — see the errors above. " +
                "Fix the authoring bug in ContentBuilder's Build*() methods and rebuild.");
        }

        Debug.Log($"Default content generated under {ContentRoot}");

        WriteStamp();
        Mark("stamp");

        // The completion sentinel run_tests_parallel.ps1's gate requires.
        // Reachable only past every throw above, which is the entire point: a
        // build that dies partway leaves the PREVIOUS content on disk, where
        // nothing about it looks wrong and the suite tests it happily. This
        // line is the only evidence that what is on disk came from THIS run.
        Debug.Log("BUILD-COMPLETE: ContentBuilder");
    }

    // THE SHAPE EIGHT OF THE ELEVEN CONTENT TYPES SHARE, written once.
    //
    // Read the JSON, hand the raw entries to the type's own resolver, and
    // write one asset per resolved record. Every one of those steps used to be
    // copied per type -- same File.Exists guard, same "no X were created"
    // wording, same TryResolveAll signature, same CreateInstance/CreateAsset
    // loop -- and the only line that differed was the per-field copy the
    // Resolved* collapse already deleted. What is left over eight types is
    // four lambdas apiece.
    //
    // THE THREE THAT STAY BESPOKE do real work here rather than restating a
    // pattern: weapons expand one authored family into N items, item sets
    // expand one set into N pieces (both then colliding against items.json's
    // own output in a shared folder), and items map the Domain-side
    // ResolvedItemKind onto the Unity-side ItemKind. A hook for each would be
    // a generalisation over three one-off jobs.
    //
    // Returns the resolved records so a caller that needs them -- achievements,
    // whose ids gate the relics built after -- can read them without a second
    // pass over the assets.
    private delegate bool ResolveAll<TRaw, TResolved>(
        IReadOnlyList<TRaw> entries, out List<TResolved> resolved, out List<string> errors);

    private static IReadOnlyList<TResolved> Build<TRaw, TResolved, TDef>(
        string label,
        string jsonPath,
        string folder,
        string noun,
        System.Func<string, TRaw[]> entriesOf,
        ResolveAll<TRaw, TResolved> resolve,
        System.Action<TDef, TResolved> store,
        System.Func<TResolved, string> assetName,
        System.Func<TResolved, bool> include = null)
        where TDef : ScriptableObject
    {
        var none = new List<TResolved>();

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"{label}: no file at '{jsonPath}' -- no {noun} were created.");
            RecordFailure(label);
            return none;
        }

        var entries = entriesOf(File.ReadAllText(jsonPath)) ?? System.Array.Empty<TRaw>();
        if (!resolve(entries, out var resolved, out var errors))
        {
            // NOTHING IS WRITTEN when any entry fails. A partial catalogue
            // looks like content that merely lost a row, which is the quietest
            // possible failure -- so one bad entry fails the whole type.
            Debug.LogError($"{label}: {jsonPath} has {errors.Count} problem(s) -- no {noun} were created:\n" +
                           string.Join("\n", errors));
            RecordFailure(label);
            return none;
        }

        int written = 0;
        foreach (var record in resolved)
        {
            if (include != null && !include(record))
            {
                continue;
            }

            var asset = ScriptableObject.CreateInstance<TDef>();
            store(asset, record);
            CreateContentAsset(asset, $"{folder}/{assetName(record)}.asset");
            written++;
        }

        Debug.Log($"{label}: generated {written} {noun} from {entries.Length} entr(y/ies).");
        return resolved;
    }

    // Characters were the second-to-last content type still written as C#
    // object initializers, which meant the roster could not be touched without
    // a recompile and its design rationale lived in code comments rather than
    // beside the data. That prose moved to characters.json's _readme, where the
    // person editing the numbers is actually looking. Upgrades were the last
    // and went the same way; every type is a JSON file now.
    // Returns the resolved roster, the way BuildAchievements already does
    // for its own caller -- BuildRewardTracks needs each character's
    // AttackType, signature resource and authored roster order to validate
    // and caption a track against.
    private static IReadOnlyList<ResolvedCharacter> BuildCharacters() =>
        Build<RawCharacterEntry, ResolvedCharacter, CharacterDefinition>(
            "BuildCharacters", "Assets/_Project/ContentData/characters.json", CharactersPath, "characters",
            json => JsonUtility.FromJson<RawCharacterFile>(json).characters,
            CharacterEntryResolver.TryResolveAll,
            (asset, character) => asset.SetData(character),
            character => character.Id);

    // What this replaces is worth recording. The tree used to be generated by
    // a nested loop as 30 nodes literally named "Talent 1".."Talent 30", all
    // costing 1, with stat bonuses derived from `column % 4` — so columns 4
    // and 5 silently duplicated columns 0 and 1. Every character saw the same
    // 30 placeholder nodes and could take any of them.
    private static void BuildTalents() =>
        Build<RawTalentEntry, ResolvedTalent, TalentDefinition>(
            "BuildTalents", "Assets/_Project/ContentData/talents.json", TalentsPath, "talents",
            json => JsonUtility.FromJson<RawTalentFile>(json).talents,
            TalentEntryResolver.TryResolveAll,
            (asset, talent) => asset.SetData(talent),
            talent => talent.Id);

    // A relic has no cross-references to wire up; RelicEntryResolver
    // validates everything (a known id, a known effect, no two relics sharing
    // an effect) up front.
    private static void BuildRelics(IReadOnlyCollection<string> achievementIds)
    {
        // A LOCAL FUNCTION, because RelicEntryResolver is the one resolver
        // that takes a second argument: the achievement ids a relic may name
        // as its unlock gate. Closing over them here is what lets relics use
        // the shared Build path rather than a fourth bespoke copy of it.
        bool Resolve(IReadOnlyList<RawRelicEntry> entries, out List<ResolvedRelic> resolved, out List<string> errors) =>
            RelicEntryResolver.TryResolveAll(entries, achievementIds, out resolved, out errors);

        Build<RawRelicEntry, ResolvedRelic, RelicDefinition>(
            "BuildRelics", "Assets/_Project/ContentData/relics.json", RelicsPath, "relics",
            json => JsonUtility.FromJson<RawRelicFile>(json).relics,
            Resolve,
            (asset, relic) => asset.SetData(relic),
            relic => relic.Id);
    }

    // docs/PLAN_REWARD_TRACKS.md P2: the content type only -- nothing reads
    // a RewardTrackDefinitionAsset yet. reward_tracks.json ships with an
    // empty "tracks" array until P6 authors the real Shawn/Odette content,
    // so this legitimately writes zero assets today; Build<>'s own
    // written-count log line says so rather than treating it as a failure.
    //
    // Assembles the per-character cross-catalogue context
    // RewardTrackEntryResolver validates rules 4/5 and captions UnlockSkill
    // against, from the characters and skills this same build already
    // resolved -- see RewardTrackCharacterContext's own header for why nothing
    // here re-reads characters.json or skills.json.
    private static void BuildRewardTracks(IReadOnlyList<ResolvedCharacter> characters, IReadOnlyList<ResolvedSkill> skills)
    {
        var contexts = new Dictionary<string, RewardTrackCharacterContext>();

        // THE WHOLE CATALOGUE, built once and shared by every context: an
        // UnlockSkill node names a skill by id and nothing about whose it is
        // (docs/PLAN_REWARD_TRACKS.md §3f/§3h -- every book-only spell is
        // authored to "sheep", so an own-kit lookup would refuse Odette's own
        // Frost Flare node). Rule 4's Level1DamageTypes below is the opposite
        // and stays per-character: what a character can already DEAL at level
        // 1 is a fact about their own kit.
        var everySkillName = new Dictionary<string, string>();
        foreach (var skill in skills) everySkillName[skill.Id] = skill.DisplayName;

        foreach (var character in characters)
        {
            var ownSkills = skills.Where(s => s.CharacterId == character.Id).ToList();

            var level1Types = new HashSet<DamageType> { character.AttackType };
            foreach (var skill in ownSkills)
            {
                if (skill.UnlockLevel > 1) continue;
                foreach (var instance in skill.DamageInstances) level1Types.Add(instance.type);
            }

            contexts[character.Id] = new RewardTrackCharacterContext
            {
                SortOrder = character.SortOrder,
                AttackType = character.AttackType,
                HasSignatureResource = character.HasSignatureResource,
                SignatureDisplayName = character.SignatureDisplayName,
                Level1DamageTypes = level1Types,
                SkillDisplayNames = everySkillName,
            };
        }

        // A LOCAL FUNCTION, the same shape BuildRelics uses to close over a
        // second resolver argument -- here the per-character context map
        // rather than a flat id set.
        bool Resolve(IReadOnlyList<RawRewardTrackEntry> entries, out List<ResolvedRewardTrack> resolved, out List<string> errors) =>
            RewardTrackEntryResolver.TryResolveAll(entries, contexts, out resolved, out errors);

        Build<RawRewardTrackEntry, ResolvedRewardTrack, RewardTrackDefinitionAsset>(
            "BuildRewardTracks", "Assets/_Project/ContentData/reward_tracks.json", RewardTracksPath, "reward tracks",
            json => JsonUtility.FromJson<RawRewardTrackFile>(json).tracks,
            Resolve,
            (asset, track) => asset.SetData(track),
            track => track.CharacterId);
    }

    // Returns the ids it created, because BuildRelics validates against them.
    private static IReadOnlyCollection<string> BuildAchievements() =>
        Build<RawAchievementEntry, ResolvedAchievement, AchievementDefinition>(
            "BuildAchievements", "Assets/_Project/ContentData/achievements.json", AchievementsPath, "achievements",
            json => JsonUtility.FromJson<RawAchievementFile>(json).achievements,
            AchievementEntryResolver.TryResolveAll,
            (asset, achievement) => asset.SetData(achievement),
            achievement => achievement.Id)
        .Select(achievement => achievement.Id)
        .ToList();

    // The last content type written as C# object initialisers, and the reason
    // it took until now is that it was small enough to keep getting away with
    // it: two rows, six fields, no validation of any of them, and a sortOrder
    // typed by hand as a positional argument next to a cost. Small is what made
    // it the easiest one to leave -- and it still meant the Principality
    // economy could not be touched without a recompile, and that nothing said
    // why 50 and 30.
    private static void BuildUpgrades() =>
        Build<RawUpgradeEntry, ResolvedUpgrade, UpgradeDefinition>(
            "BuildUpgrades", "Assets/_Project/ContentData/upgrades.json", UpgradesPath, "upgrades",
            json => JsonUtility.FromJson<RawUpgradeFile>(json).upgrades,
            UpgradeEntryResolver.TryResolveAll,
            (asset, upgrade) => asset.SetData(upgrade),
            upgrade => upgrade.Id);

    // Weakness/resistance pairs, chosen so every one of the 5 characters'
    // attackType (see BuildCharacters) has at least one enemy it's strong
    // against; only Fire has no matching character yet, deliberately left
    // as headroom for a future fire-themed character or talent rather than
    // stretched to fit one of the existing five. First-pass balance, not
    // exhaustively tuned — see CombatMath.EffectivenessMultiplier.
    // Monsters are the one content type authored outside this file — see
    // Assets/_Project/ContentData/enemies.json. That's the "hand-authored
    // assets belong in a folder this tool does not own" promise this file's
    // own header comment has always made, actually kept for enemies: the
    // JSON isn't touched by RecreateFolder(ContentRoot) above, so adding a
    // monster is "add a line to enemies.json and rebuild," not "write a
    // CreateEnemy call and recompile." EnemyEntryResolver (Domain layer,
    // unit-tested, engine-free) does the actual validation and default-
    // filling; this method is just the thin Editor-only glue that reads the
    // file, calls the resolver, and turns the result into real assets.
    //
    // Design intent behind the current roster, preserved here since JSON
    // has no comments: golem is the tanky/slow end of the spread (high
    // HP+defense, low speed); imp is the glass-cannon end (high attack,
    // zero defense); bog_witch resists the same type Sheep deals, giving
    // Sheep a bad matchup to offset its free win against golem; crystal_bat
    // is brittle but resists Arcane, punishing the same Physical attackers
    // golem/wolf also punish. warden (v1's only boss, since removed; the
    // note stays for the reasoning) fought in every
    // dungeon clear) deliberately resists Fire — the one type no character
    //'s attackType used at the time — after Poison here once silently
    // gutted the Assassin's execute bonus in every single boss fight.
    private static void BuildEnemies() =>
        Build<RawEnemyEntry, ResolvedEnemy, EnemyDefinition>(
            "BuildEnemies", "Assets/_Project/ContentData/enemies.json", EnemiesPath, "enemies",
            json => JsonUtility.FromJson<RawEnemyFile>(json).enemies,
            EnemyEntryResolver.TryResolveAll,
            (asset, enemy) => asset.SetData(enemy),
            enemy => enemy.Id,

            // BENCHED MONSTERS ARE VALIDATED LIKE EVERY OTHER ENTRY -- a typo
            // in a disabled one still fails the build rather than lying in
            // wait until it is switched back on -- but no asset is written, so
            // ContentDatabase never sees them and they cannot spawn.
            include: enemy => enemy.Active);

    private static void BuildSpellTiers() =>
        Build<RawSpellTierEntry, ResolvedSpellTier, SpellTierDefinition>(
            "BuildSpellTiers", "Assets/_Project/ContentData/spells.json", SpellTiersPath, "spell tiers",
            json => JsonUtility.FromJson<RawSpellTierFile>(json).tiers,
            SpellTierEntryResolver.TryResolveAll,
            (asset, tier) => asset.SetData(tier),

            // BY LEVEL, not by id -- a tier has no id of its own, and the
            // filename is what ContentDatabase browses.
            tier => $"level_{tier.Level}");

    // No enum mapping step here, unlike BuildItems: SkillEffect and
    // SkillTargeting live in Domain and are used directly on both sides.
    //
    // Returns the resolved skills -- BuildRewardTracks needs each
    // character's owned skills (id, displayName, unlockLevel,
    // damageInstances) to validate UnlockSkill and rule 4's level-1 element
    // set against.
    private static IReadOnlyList<ResolvedSkill> BuildSkills() =>
        Build<RawSkillEntry, ResolvedSkill, SkillDefinition>(
            "BuildSkills", "Assets/_Project/ContentData/skills.json", SkillsPath, "skills",
            json => JsonUtility.FromJson<RawSkillFile>(json).skills,
            SkillEntryResolver.TryResolveAll,

            // ONE ASSIGNMENT, not thirty-four. The asset stores the resolved
            // value itself, so there is no per-field copy here to forget a
            // line of -- which is what dropped `transform` and then
            // `bookOnly`/`bookTier` on the way back out. See SkillDefinition.
            (asset, skill) => asset.SetData(skill),
            skill => skill.Id);

    // Authored outside this file (Assets/_Project/ContentData/items.json),
    // validated by the Domain-layer ItemEntryResolver — same pattern as
    // BuildEnemies/BuildSpellTiers. The Domain resolver has its own
    // Resolved* enum mirrors because Domain can't reference the Unity-side
    // Content enums; the two are mapped by name here, which is the one
    // place that coupling lives.
    private static void BuildItems()
    {
        const string jsonPath = "Assets/_Project/ContentData/items.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildItems: no file at '{jsonPath}' — no items were created.");
            RecordFailure("BuildItems");
            return;
        }

        var file = JsonUtility.FromJson<RawItemFile>(File.ReadAllText(jsonPath));
        if (!ItemEntryResolver.TryResolveAll(file.items, out var resolved, out var errors))
        {
            Debug.LogError($"BuildItems: {jsonPath} has {errors.Count} problem(s) — no items were created:\n" +
                            string.Join("\n", errors));
            RecordFailure("BuildItems");
            return;
        }

        foreach (var item in resolved)
        {
            var asset = ScriptableObject.CreateInstance<ItemDefinition>();
            asset.id = item.Id;
            asset.displayName = item.DisplayName;
            asset.description = item.Description;
            asset.kind = KindOf(item.Kind);
            asset.effect = item.Effect == ResolvedItemEffect.RestoreMana ? ItemEffect.RestoreMana : ItemEffect.Heal;
            asset.amount = item.Amount;
            asset.attackBonus = item.AttackBonus;
            // EquipmentSlot needs no name-mapping the way ItemKind does — it
            // lives in Domain and both sides use it directly, same as
            // DamageType on EnemyDefinition.
            asset.equipSlot = item.Slot;
            asset.statBonus = item.StatBonus;
            asset.abilityScoreBonus = item.AbilityScoreBonus;
            asset.requirements = item.Requirements;
            asset.startingStock = item.StartingStock;
            asset.cost = item.Cost;
            asset.iconPath = item.IconPath;
            asset.sortOrder = item.SortOrder;
            CreateContentAsset(asset, $"{ItemsPath}/{item.Id}.asset");
        }

        BuildItemSets();
        BuildWeapons();
    }

    // Weapon families, generated from Assets/_Project/ContentData/weapons.json.
    //
    // Same reasoning as BuildItemSets for living inside BuildItems: what it
    // produces IS items, in the same folder, of the same asset type, read back
    // through the same ContentDatabase.Items. Keeping all three generators
    // adjacent is what makes the collision check below meaningful.
    private static void BuildWeapons()
    {
        const string jsonPath = "Assets/_Project/ContentData/weapons.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildWeapons: no file at '{jsonPath}' — no weapons were created.");
            RecordFailure("BuildWeapons");
            return;
        }

        var file = JsonUtility.FromJson<RawWeaponFile>(File.ReadAllText(jsonPath));
        if (!WeaponEntryResolver.TryResolveAll(file.families, out var resolved, out var errors))
        {
            Debug.LogError($"BuildWeapons: {jsonPath} has {errors.Count} problem(s) — no weapons were created:\n" +
                            string.Join("\n", errors));
            RecordFailure("BuildWeapons");
            return;
        }

        // Past the armour sets, so weapons never interleave with them in a
        // browse order; within, it is family, then modifier, then tier.
        const int WeaponSortOffset = 200000;

        foreach (var weapon in resolved)
        {
            string assetPath = $"{ItemsPath}/{weapon.Id}.asset";
            if (File.Exists(assetPath))
            {
                Debug.LogError($"BuildWeapons: '{weapon.Id}' collides with an item already generated from items.json or itemsets.json. " +
                                "Rename the family or the modifier — an item id is written into save files, so two things cannot share one.");
                continue;
            }

            var asset = ScriptableObject.CreateInstance<ItemDefinition>();
            asset.id = weapon.Id;
            asset.displayName = weapon.DisplayName;
            asset.description = weapon.Description;
            asset.kind = ItemKind.Weapon;
            asset.equipSlot = weapon.Slot;
            // A Weapon's flat power rides attackBonus rather than
            // statBonus.attack, because that is the field
            // FightController.RollWeaponDrop tiers the loot table on.
            asset.attackBonus = weapon.AttackBonus;
            asset.scaling = weapon.Scaling;
            asset.spellScaling = weapon.SpellScaling;
            asset.requirements = weapon.Requirements;
            asset.cost = weapon.Cost;
            asset.tier = weapon.Tier;
            asset.weaponFamilyId = weapon.FamilyId;
            asset.iconPath = weapon.IconPath;
            asset.sortOrder = WeaponSortOffset + weapon.SortOrder;
            CreateContentAsset(asset, assetPath);
        }

        Debug.Log($"BuildWeapons: generated {resolved.Count} weapon(s) from {file.families.Length} famil(y/ies).");
    }

    // ITS OWN FOLDER (Resources/Content/Modifiers), unlike BuildWeapons/
    // BuildItemSets — those generate ItemDefinition assets that live beside
    // items.json's own output and need the collision check that implies. A
    // modifier is a different asset type in a different folder with its own
    // id space, so there is nothing for it to collide with.
    private static void BuildModifiers() =>
        Build<RawModifierEntry, ResolvedModifier, ModifierDefinition>(
            "BuildModifiers", "Assets/_Project/ContentData/modifiers.json", ModifiersPath, "modifiers",
            json => JsonUtility.FromJson<RawModifierFile>(json).modifiers,
            ModifierEntryResolver.TryResolveAll,
            (asset, modifier) => asset.SetData(modifier),
            modifier => modifier.Id);

    // Armour sets, generated from Assets/_Project/ContentData/itemsets.json.
    //
    // Called from BuildItems rather than from BuildDefaultContent's list
    // because what it produces IS items — they land in the same folder, are
    // the same asset type, and are read back through the same
    // ContentDatabase.Items. Keeping the two generators adjacent is what
    // makes the duplicate-id check below meaningful: a set piece and a
    // hand-authored item colliding is a real possibility and would otherwise
    // be one silently overwriting the other on disk.
    //
    // Sort order is offset past the hand-authored items so a set never
    // interleaves with them; within the sets it is set, then piece, then
    // tier, which is the order a player would expect to browse.
    private static void BuildItemSets()
    {
        const string jsonPath = "Assets/_Project/ContentData/itemsets.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildItemSets: no file at '{jsonPath}' — no armour sets were created.");
            RecordFailure("BuildItemSets");
            return;
        }

        var file = JsonUtility.FromJson<RawItemSetFile>(File.ReadAllText(jsonPath));
        if (!ItemSetEntryResolver.TryResolveAll(file.sets, out var resolved, out var errors))
        {
            Debug.LogError($"BuildItemSets: {jsonPath} has {errors.Count} problem(s) — no armour sets were created:\n" +
                            string.Join("\n", errors));
            RecordFailure("BuildItemSets");
            return;
        }

        const int SetSortOffset = 100000;

        foreach (var piece in resolved)
        {
            string assetPath = $"{ItemsPath}/{piece.Id}.asset";
            if (File.Exists(assetPath))
            {
                Debug.LogError($"BuildItemSets: '{piece.Id}' collides with an item already generated from items.json. " +
                                "Rename the set or the piece — an item id is written into save files, so two things cannot share one.");
                continue;
            }

            var asset = ScriptableObject.CreateInstance<ItemDefinition>();
            asset.id = piece.Id;
            asset.displayName = piece.DisplayName;
            asset.description = piece.Description;
            asset.kind = ItemKind.Equipment;
            asset.equipSlot = piece.Slot;
            asset.statBonus = piece.StatBonus;
            asset.abilityScoreBonus = piece.AbilityScoreBonus;
            asset.requirements = piece.Requirements;
            asset.cost = piece.Cost;
            asset.setId = piece.SetId;
            asset.tier = piece.Tier;
            asset.iconPath = piece.IconPath;
            asset.sortOrder = SetSortOffset + piece.SortOrder;
            CreateContentAsset(asset, assetPath);
        }

        Debug.Log($"BuildItemSets: generated {resolved.Count} set piece(s) from {file.sets.Length} set(s).");
    }

    // The one place the Domain mirror enum and the Unity-side enum are
    // mapped across. Written as an exhaustive switch rather than a ternary
    // chain so adding a value to either side fails to compile here — the
    // previous `== Weapon ? Weapon : Consumable` shape would have silently
    // turned every new kind into a Consumable.
    private static ItemKind KindOf(ResolvedItemKind kind)
    {
        switch (kind)
        {
            case ResolvedItemKind.Weapon: return ItemKind.Weapon;
            case ResolvedItemKind.Equipment: return ItemKind.Equipment;
            case ResolvedItemKind.Consumable: return ItemKind.Consumable;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "ContentBuilder has no ItemKind wired up for this ResolvedItemKind.");
        }
    }

    // THE ONE PLACE AN ASSET IS CREATED, so the stamp below cannot describe
    // anything the build did not write. Every CreateAsset in this file goes
    // through here; there are five call sites and adding a sixth that does not
    // is the only way to make the stamp lie.
    private static void CreateContentAsset(ScriptableObject asset, string assetPath)
    {
        AssetDatabase.CreateAsset(asset, assetPath);

        string folder = Path.GetFileName(Path.GetDirectoryName(assetPath));
        string name = Path.GetFileNameWithoutExtension(assetPath);

        if (!WrittenByFolder.TryGetValue(folder, out var names))
        {
            names = new List<string>();
            WrittenByFolder[folder] = names;
        }

        names.Add(name);
    }

    // Called from every "no X were created" branch. What it buys is the fourth
    // acceptance case in the plan: a resolver error in one entry makes that
    // whole type write nothing, and without this the build would still finish,
    // still log BUILD-COMPLETE, and still stamp a catalogue silently missing a
    // type. No stamp is the honest outcome -- the freshness check then says
    // "not built", which is exactly what happened.
    private static void RecordFailure(string label)
    {
        if (!FailedTypes.Contains(label)) FailedTypes.Add(label);
    }

    // LAST, past every asset and past validation. A reader that finds a stamp
    // whose hash matches is therefore reading a build that ran to completion,
    // which is the entire claim -- see ContentStamp's own header for what it
    // deliberately does NOT claim.
    private static void WriteStamp()
    {
        if (FailedTypes.Count > 0)
        {
            Debug.LogError($"[ContentBuilder] no stamp written: {string.Join(", ", FailedTypes)} produced nothing. " +
                           "Fix the authoring error above and rebuild -- the generated tree is incomplete until then.");
            return;
        }

        var stamp = new ContentStamp { InputHash = ContentInputHash.Compute(Directory.GetCurrentDirectory()) };
        foreach (var pair in WrittenByFolder)
        {
            stamp.IdsByFolder[pair.Key] = pair.Value;
        }

        File.WriteAllText(StampPath, stamp.Render());
        AssetDatabase.ImportAsset(StampPath);
    }

    private static void RecreateFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        EnsureFolder(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
