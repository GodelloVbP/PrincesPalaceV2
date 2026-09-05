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

    [MenuItem("Prince's Palace/Build Default Content")]
    public static void BuildDefaultContent()
    {
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

        BuildCharacters();
        BuildTalents();
        BuildUpgrades();
        BuildEnemies();
        BuildItems();
        BuildSpellTiers();
        BuildSkills();
        BuildModifiers();

        // ACHIEVEMENTS BEFORE RELICS, and the order is load-bearing: relics
        // are validated against the achievement ids this returns, so building
        // them the other way round would validate against nothing and let a
        // typo'd gate through.
        var achievementIds = BuildAchievements();
        BuildRelics(achievementIds);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Runs only after every Build* call above has already completed, so
        // a failure here means the generated content itself is wrong (a
        // duplicate id, a 0-HP enemy, an authoring slip like weakness ==
        // resistance) — not that the process was interrupted partway
        // through. ContentDatabase.Reset() forces a real re-read of what
        // was just written rather than trusting whatever state it was
        // already in in this process.
        ContentDatabase.Reset();
        var validationErrors = ContentDatabase.ValidateContent();
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
            AssetDatabase.CreateAsset(asset, $"{folder}/{assetName(record)}.asset");
            written++;
        }

        Debug.Log($"{label}: generated {written} {noun} from {entries.Length} entr(y/ies).");
        return resolved;
    }

    // Characters were the last content type still written as C# object
    // initializers (Upgrades is the only one left), which meant the roster
    // could not be touched without a recompile and its design rationale
    // lived in code comments rather than beside the data. That prose moved
    // to characters.json's _readme, where the person editing the numbers is
    // actually looking.
    private static void BuildCharacters() =>
        Build<RawCharacterEntry, ResolvedCharacter, CharacterDefinition>(
            "BuildCharacters", "Assets/_Project/ContentData/characters.json", CharactersPath, "characters",
            json => JsonUtility.FromJson<RawCharacterFile>(json).characters,
            CharacterEntryResolver.TryResolveAll,
            (asset, character) => asset.data = character,
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
            (asset, talent) => asset.data = talent,
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
            (asset, relic) => asset.data = relic,
            relic => relic.Id);
    }

    // Returns the ids it created, because BuildRelics validates against them.
    private static IReadOnlyCollection<string> BuildAchievements() =>
        Build<RawAchievementEntry, ResolvedAchievement, AchievementDefinition>(
            "BuildAchievements", "Assets/_Project/ContentData/achievements.json", AchievementsPath, "achievements",
            json => JsonUtility.FromJson<RawAchievementFile>(json).achievements,
            AchievementEntryResolver.TryResolveAll,
            (asset, achievement) => asset.data = achievement,
            achievement => achievement.Id)
        .Select(achievement => achievement.Id)
        .ToList();

    private static void BuildUpgrades()
    {
        CreateUpgrade("extra_recruit_slot", "Extra Recruit Slot", "Bring an additional character on every run.", 50, 0);
        CreateUpgrade("starting_gold_boost", "Starting Gold Boost", "Begin each run with extra gold.", 30, 1, startingGoldBonus: 50);
    }

    private static void CreateUpgrade(string id, string displayName, string description, int cost, int sortOrder, int startingGoldBonus = 0)
    {
        var asset = ScriptableObject.CreateInstance<UpgradeDefinition>();
        asset.id = id;
        asset.displayName = displayName;
        asset.description = description;
        asset.cost = cost;
        asset.sortOrder = sortOrder;
        asset.startingGoldBonus = startingGoldBonus;
        AssetDatabase.CreateAsset(asset, $"{UpgradesPath}/{id}.asset");
    }

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
            (asset, enemy) => asset.data = enemy,
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
            (asset, tier) => asset.data = tier,

            // BY LEVEL, not by id -- a tier has no id of its own, and the
            // filename is what ContentDatabase browses.
            tier => $"level_{tier.Level}");

    // No enum mapping step here, unlike BuildItems: SkillEffect and
    // SkillTargeting live in Domain and are used directly on both sides.
    private static void BuildSkills() =>
        Build<RawSkillEntry, ResolvedSkill, SkillDefinition>(
            "BuildSkills", "Assets/_Project/ContentData/skills.json", SkillsPath, "skills",
            json => JsonUtility.FromJson<RawSkillFile>(json).skills,
            SkillEntryResolver.TryResolveAll,

            // ONE ASSIGNMENT, not thirty-four. The asset stores the resolved
            // value itself, so there is no per-field copy here to forget a
            // line of -- which is what dropped `transform` and then
            // `bookOnly`/`bookTier` on the way back out. See SkillDefinition.
            (asset, skill) => asset.data = skill,
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
            return;
        }

        var file = JsonUtility.FromJson<RawItemFile>(File.ReadAllText(jsonPath));
        if (!ItemEntryResolver.TryResolveAll(file.items, out var resolved, out var errors))
        {
            Debug.LogError($"BuildItems: {jsonPath} has {errors.Count} problem(s) — no items were created:\n" +
                            string.Join("\n", errors));
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
            AssetDatabase.CreateAsset(asset, $"{ItemsPath}/{item.Id}.asset");
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
            return;
        }

        var file = JsonUtility.FromJson<RawWeaponFile>(File.ReadAllText(jsonPath));
        if (!WeaponEntryResolver.TryResolveAll(file.families, out var resolved, out var errors))
        {
            Debug.LogError($"BuildWeapons: {jsonPath} has {errors.Count} problem(s) — no weapons were created:\n" +
                            string.Join("\n", errors));
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
            AssetDatabase.CreateAsset(asset, assetPath);
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
            (asset, modifier) => asset.data = modifier,
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
            return;
        }

        var file = JsonUtility.FromJson<RawItemSetFile>(File.ReadAllText(jsonPath));
        if (!ItemSetEntryResolver.TryResolveAll(file.sets, out var resolved, out var errors))
        {
            Debug.LogError($"BuildItemSets: {jsonPath} has {errors.Count} problem(s) — no armour sets were created:\n" +
                            string.Join("\n", errors));
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
            AssetDatabase.CreateAsset(asset, assetPath);
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
