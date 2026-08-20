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

        BuildCharacters();
        BuildTalents();
        BuildUpgrades();
        BuildEnemies();
        BuildItems();
        BuildSpellTiers();
        BuildSkills();

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

    // Authored in Assets/_Project/ContentData/characters.json now, not
    // here. Same pattern as BuildEnemies/BuildRelics/BuildSkills: the
    // Domain-layer CharacterEntryResolver validates, and this is the thin
    // Editor-only glue.
    //
    // Characters were the last content type still written as C# object
    // initializers (Upgrades is the only one left), which meant the roster
    // could not be touched without a recompile and its design rationale
    // lived in code comments rather than beside the data. That prose moved
    // to characters.json's _readme, where the person editing the numbers is
    // actually looking.
    private static void BuildCharacters()
    {
        const string jsonPath = "Assets/_Project/ContentData/characters.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildCharacters: no file at '{jsonPath}' — no characters were created.");
            return;
        }

        var file = JsonUtility.FromJson<RawCharacterFile>(File.ReadAllText(jsonPath));
        if (!CharacterEntryResolver.TryResolveAll(file.characters, out var resolved, out var errors))
        {
            Debug.LogError($"BuildCharacters: {jsonPath} has {errors.Count} problem(s) — no characters were created:\n" +
                            string.Join("\n", errors));
            return;
        }

        foreach (var character in resolved)
        {
            var asset = ScriptableObject.CreateInstance<CharacterDefinition>();
            asset.id = character.Id;
            asset.displayName = character.DisplayName;
            asset.role = character.Role;
            asset.baseStats = character.BaseStats;
            // Set EXPLICITLY even when unauthored, rather than leaning on
            // CharacterDefinition's field initializer. The initializer is
            // what every existing asset silently inherited, which meant the
            // authored value and the default lived in two different files
            // and only agreed by luck.
            asset.baseAbilityScores = character.AbilityScores;
            asset.portraitPath = character.PortraitPath;
            asset.attackType = character.AttackType;
            asset.battleSpritePath = character.BattleSpritePath;
            asset.battleSpriteFacing = character.BattleSpriteFacing;
            asset.signatureResourceId = character.SignatureId;
            asset.signatureResourceDisplayName = character.SignatureDisplayName;
            asset.signatureResourceCapacity = character.SignatureCapacity;
            asset.signatureGainPerTurn = character.SignatureGainPerTurn;
            asset.signatureGainOnAttack = character.SignatureGainOnAttack;
            asset.signatureGainOnDamageTaken = character.SignatureGainOnDamageTaken;
            asset.signatureAbsorbsDamage = character.SignatureAbsorbsDamage;
            asset.princesFavor = character.PrincesFavor;
            asset.sortOrder = character.SortOrder;
            AssetDatabase.CreateAsset(asset, $"{CharactersPath}/{character.Id}.asset");
        }
    }


    // Authored in Assets/_Project/ContentData/talents.json now, not here.
    // Same pattern as BuildEnemies/BuildItems/BuildSpellTiers/BuildSkills:
    // the Domain-layer TalentEntryResolver validates, and this is the thin
    // Editor-only glue.
    //
    // What this replaces is worth recording. The tree used to be generated by
    // a nested loop as 30 nodes literally named "Talent 1".."Talent 30", all
    // costing 1, with stat bonuses derived from `column % 4` — so columns 4
    // and 5 silently duplicated columns 0 and 1. Every character saw the same
    // 30 placeholder nodes and could take any of them.
    private static void BuildTalents()
    {
        const string jsonPath = "Assets/_Project/ContentData/talents.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildTalents: no file at '{jsonPath}' — no talents were created.");
            return;
        }

        var file = JsonUtility.FromJson<RawTalentFile>(File.ReadAllText(jsonPath));
        if (!TalentEntryResolver.TryResolveAll(file.talents, out var resolved, out var errors))
        {
            Debug.LogError($"BuildTalents: {jsonPath} has {errors.Count} problem(s) — no talents were created:\n" +
                            string.Join("\n", errors));
            return;
        }

        // Two passes, for the same reason the old loop needed them:
        // prerequisites are asset REFERENCES, so every asset has to exist
        // before any of them can be wired.
        var created = new Dictionary<string, TalentDefinition>();
        foreach (var talent in resolved)
        {
            var asset = ScriptableObject.CreateInstance<TalentDefinition>();
            asset.id = talent.Id;
            asset.displayName = talent.DisplayName;
            asset.description = talent.Description;
            asset.characterId = talent.CharacterId;
            asset.column = talent.Column;
            asset.row = talent.Row;
            asset.statBonus = talent.StatBonus;
            asset.abilityScoreBonus = talent.AbilityScoreBonus;
            asset.maxManaBonus = talent.MaxManaBonus;
            asset.skillManaCostReduction = talent.SkillManaCostReduction;
            asset.signatureCapacityBonus = talent.SignatureCapacityBonus;
            asset.signaturePerTurnBonus = talent.SignaturePerTurnBonus;
            asset.grantsStartingItemId = talent.GrantsStartingItemId;
            asset.iconPath = talent.IconPath;
            asset.minSpent = talent.MinSpent;
            asset.grantsSkillId = talent.GrantsSkillId;
            asset.effects = talent.Effects
                .Select(e => new TalentDefinition.TalentEffectEntry
                {
                    type = e.Type,
                    magnitude = e.Magnitude,
                    threshold = e.Threshold,
                })
                .ToArray();
            AssetDatabase.CreateAsset(asset, $"{TalentsPath}/{talent.Id}.asset");
            created[talent.Id] = asset;
        }

        foreach (var talent in resolved)
        {
            if (talent.Prerequisites.Count == 0)
            {
                continue;
            }

            var asset = created[talent.Id];
            asset.prerequisites = talent.Prerequisites.Select(id => created[id]).ToArray();
            EditorUtility.SetDirty(asset);
        }
    }

    // One pass, unlike BuildTalents — a relic has no cross-references to
    // wire up in a second pass; RelicEntryResolver validates everything
    // (a known id, a known effect, no two relics sharing an effect) up front.
    private static void BuildRelics(System.Collections.Generic.IReadOnlyCollection<string> achievementIds)
    {
        const string jsonPath = "Assets/_Project/ContentData/relics.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildRelics: no file at '{jsonPath}' — no relics were created.");
            return;
        }

        var file = JsonUtility.FromJson<RawRelicFile>(File.ReadAllText(jsonPath));
        if (!RelicEntryResolver.TryResolveAll(file.relics, achievementIds, out var resolved, out var errors))
        {
            Debug.LogError($"BuildRelics: {jsonPath} has {errors.Count} problem(s) — no relics were created:\n" +
                            string.Join("\n", errors));
            return;
        }

        foreach (var relic in resolved)
        {
            var asset = ScriptableObject.CreateInstance<RelicDefinition>();
            asset.id = relic.Id;
            asset.displayName = relic.DisplayName;
            asset.description = relic.Description;
            asset.effect = relic.Effect;
            asset.sortOrder = relic.SortOrder;
            asset.iconPath = relic.IconPath;
            asset.rarity = relic.Rarity;
            asset.unlockedBy = relic.UnlockedBy;
            asset.modifiers = relic.Modifiers
                .Select(m => new RelicModifierEntry { type = m.Type, amount = m.Amount })
                .ToArray();
            AssetDatabase.CreateAsset(asset, $"{RelicsPath}/{relic.Id}.asset");
        }
    }

    // Returns the ids it created, because BuildRelics validates against them.
    private static System.Collections.Generic.IReadOnlyCollection<string> BuildAchievements()
    {
        const string jsonPath = "Assets/_Project/ContentData/achievements.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildAchievements: no file at '{jsonPath}' -- no achievements were created.");
            return new string[0];
        }

        var file = JsonUtility.FromJson<RawAchievementFile>(File.ReadAllText(jsonPath));
        if (!AchievementEntryResolver.TryResolveAll(file.achievements, out var resolved, out var errors))
        {
            Debug.LogError($"BuildAchievements: {jsonPath} has {errors.Count} problem(s) -- no achievements were created:\n" +
                           string.Join("\n", errors));
            return new string[0];
        }

        var ids = new System.Collections.Generic.List<string>();
        foreach (var achievement in resolved)
        {
            var asset = ScriptableObject.CreateInstance<AchievementDefinition>();
            asset.id = achievement.Id;
            asset.displayName = achievement.DisplayName;
            asset.description = achievement.Description;
            asset.condition = achievement.Condition;
            asset.threshold = achievement.Threshold;
            asset.parameter = achievement.Parameter;
            asset.sortOrder = achievement.SortOrder;
            AssetDatabase.CreateAsset(asset, $"{AchievementsPath}/{achievement.Id}.asset");
            ids.Add(achievement.Id);
        }

        return ids;
    }

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
    // golem/wolf also punish. warden (the only boss, fought in every
    // dungeon clear) deliberately resists Fire — the one type no character
    //'s attackType used at the time — after Poison here once silently
    // gutted the Assassin's execute bonus in every single boss fight.
    private static void BuildEnemies()
    {
        const string jsonPath = "Assets/_Project/ContentData/enemies.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildEnemies: no file at '{jsonPath}' — no enemies were created.");
            return;
        }

        var file = JsonUtility.FromJson<RawEnemyFile>(File.ReadAllText(jsonPath));
        if (!EnemyEntryResolver.TryResolveAll(file.enemies, out var resolved, out var errors))
        {
            Debug.LogError($"BuildEnemies: {jsonPath} has {errors.Count} problem(s) — no enemies were created:\n" +
                            string.Join("\n", errors));
            return;
        }

        foreach (var enemy in resolved)
        {
            // Benched monsters are validated like every other entry — a typo
            // in a disabled one still fails the build rather than lying in
            // wait until it is switched back on — but no asset is written,
            // so ContentDatabase never sees them and they cannot spawn.
            if (!enemy.Active)
            {
                continue;
            }

            var asset = ScriptableObject.CreateInstance<EnemyDefinition>();
            asset.id = enemy.Id;
            asset.displayName = enemy.DisplayName;
            asset.baseStats = enemy.BaseStats;
            asset.expReward = enemy.ExpReward;
            asset.currencyReward = enemy.CurrencyReward;
            asset.isBoss = enemy.IsBoss;
            asset.minFloor = enemy.MinFloor;
            asset.weakness = enemy.Weakness;
            asset.resistance = enemy.Resistance;
            asset.spritePath = enemy.SpritePath;
            asset.facing = enemy.Facing;
            asset.skillName = enemy.SkillName;
            asset.skillPower = enemy.SkillPower;
            asset.skillChance = enemy.SkillChance;
            asset.sortOrder = enemy.SortOrder;
            asset.breakShieldPoints = enemy.BreakShieldPoints;
            asset.vfxPath = enemy.VfxPath;
            asset.vfxSeconds = enemy.VfxSeconds;
            asset.vfxImpactFrame = enemy.VfxImpactFrame;
            asset.sfxPath = enemy.SfxPath;
            asset.hasStatus = enemy.AppliesStatus.HasValue;
            if (enemy.AppliesStatus.HasValue)
            {
                asset.appliesStatus = enemy.AppliesStatus.Value;
            }
            asset.statusMagnitude = enemy.StatusMagnitude;
            asset.statusDuration = enemy.StatusDuration;
            asset.avoidsFrontSlot = enemy.AvoidsFrontSlot;
            asset.attackHoldsPosition = enemy.AttackHoldsPosition;
            AssetDatabase.CreateAsset(asset, $"{EnemiesPath}/{enemy.Id}.asset");
        }
    }

    // Same pattern as BuildEnemies: authored outside this file (see
    // Assets/_Project/ContentData/spells.json), validated and resolved by
    // the Domain-layer SpellTierEntryResolver, this method is just the
    // thin Editor-only glue that turns the result into assets.
    private static void BuildSpellTiers()
    {
        const string jsonPath = "Assets/_Project/ContentData/spells.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildSpellTiers: no file at '{jsonPath}' — no spell tiers were created.");
            return;
        }

        var file = JsonUtility.FromJson<RawSpellTierFile>(File.ReadAllText(jsonPath));
        if (!SpellTierEntryResolver.TryResolveAll(file.tiers, out var resolved, out var errors))
        {
            Debug.LogError($"BuildSpellTiers: {jsonPath} has {errors.Count} problem(s) — no spell tiers were created:\n" +
                            string.Join("\n", errors));
            return;
        }

        foreach (var tier in resolved)
        {
            var asset = ScriptableObject.CreateInstance<SpellTierDefinition>();
            asset.level = tier.Level;
            asset.displayName = tier.DisplayName;
            asset.manaCost = tier.ManaCost;
            asset.powerMultiplier = tier.PowerMultiplier;
            asset.scaling = tier.Scaling;
            asset.requirements = tier.Requirements;
            asset.sortOrder = tier.SortOrder;
            AssetDatabase.CreateAsset(asset, $"{SpellTiersPath}/level_{tier.Level}.asset");
        }
    }

    // Same pattern as BuildEnemies/BuildSpellTiers/BuildItems: authored in
    // Assets/_Project/ContentData/skills.json, validated by the Domain-layer
    // SkillEntryResolver, and this is just the thin Editor-only glue.
    //
    // No enum mapping step here, unlike BuildItems: SkillEffect and
    // SkillTargeting live in Domain and are used directly on both sides.
    private static void BuildSkills()
    {
        const string jsonPath = "Assets/_Project/ContentData/skills.json";
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"BuildSkills: no file at '{jsonPath}' — no skills were created.");
            return;
        }

        var file = JsonUtility.FromJson<RawSkillFile>(File.ReadAllText(jsonPath));
        if (!SkillEntryResolver.TryResolveAll(file.skills, out var resolved, out var errors))
        {
            Debug.LogError($"BuildSkills: {jsonPath} has {errors.Count} problem(s) — no skills were created:\n" +
                            string.Join("\n", errors));
            return;
        }

        foreach (var skill in resolved)
        {
            var asset = ScriptableObject.CreateInstance<SkillDefinition>();
            asset.id = skill.Id;
            asset.displayName = skill.DisplayName;
            asset.description = skill.Description;
            asset.characterId = skill.CharacterId;
            asset.unlockLevel = skill.UnlockLevel;
            asset.effect = skill.Effect;
            asset.targeting = skill.Targeting;
            asset.manaCost = skill.ManaCost;
            asset.resourceCost = skill.ResourceCost;
            asset.spendsAllResource = skill.SpendsAllResource;
            asset.power = skill.Power;
            asset.flatAmount = skill.FlatAmount;
            asset.ignoresDefense = skill.IgnoresDefense;
            asset.damageInstances = skill.DamageInstances;
            asset.vfxPath = skill.VfxPath;
            asset.vfxSeconds = skill.VfxSeconds;
            asset.vfxImpactFrame = skill.VfxImpactFrame;
            asset.sfxPath = skill.SfxPath;
            asset.sortOrder = skill.SortOrder;
            asset.requirements = skill.Requirements;
            asset.scalingAxis = skill.ScalingAxis;
            asset.queuePushSlots = skill.QueuePushSlots;
            asset.transform = skill.Transform;
            asset.hasStatus = skill.AppliesStatus.HasValue;
            if (skill.AppliesStatus.HasValue)
            {
                asset.appliesStatus = skill.AppliesStatus.Value;
                asset.statusMagnitude = skill.StatusMagnitude;
                asset.statusDuration = skill.StatusDuration;
            }

            AssetDatabase.CreateAsset(asset, $"{SkillsPath}/{skill.Id}.asset");
        }
    }

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
