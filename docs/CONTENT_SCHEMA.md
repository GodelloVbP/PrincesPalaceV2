# Content schema

GENERATED FILE -- do not hand-edit. Produced by ContentSchema.Generate() (Assets/_Project/Scripts/Domain/Content/ContentSchema.cs) from the Raw*Entry types by reflection, and checked byte-for-byte by ContentSchemaTests (Tests/EditMode/Content/ContentSchemaTests.cs). Run `tools/content_schema.ps1` after changing a Raw*Entry field or its [ContentDoc], then commit this file.

Each table is one JSON file under Assets/_Project/ContentData/. `Default` is the field's own C# initialiser -- the value JsonUtility leaves in place when an author omits the key -- not a claim about what the resolver does with it; a resolver-specific rule (a sentinel that means "derive it", a both-fields-or-neither pairing, a value that is only legal for one `effect`) stays in the field's own `_readme` prose, which this file does not replace. `Values` is only populated for a field a resolver parses against an enum, and is read live off that enum, so it cannot go stale the way hand-typed prose can.

## achievements.json -- `RawAchievementEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save data the moment this is earned, so never rename it. |  |
| `displayName` | string | (none -- required) | The name shown for this achievement. |  |
| `description` | string | `""` | Flavor text shown to the player. |  |
| `condition` | string | `""` | Which AchievementCondition this is earned by, matched case-insensitively. | Never, DefeatSpecificBoss, DefeatDistinctBosses, ReachCharacterLevel, ClearRooms, ReachDepth, DealTotalDamage |
| `threshold` | int | `0` | What the condition compares against; meaning depends on the condition, and a counting condition rejects 0. |  |
| `parameter` | string | `""` | The condition's subject when it has one; today only DefeatSpecificBoss uses it, naming a boss enemy id. |  |

## characters.json -- `RawCharacterEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save files and never renamed once used. |  |
| `displayName` | string | (none -- required) | The name shown for this character. |  |
| `role` | string | `""` | Which CharacterRole this is, matched case-insensitively. | Utility, Assassin, CrowdControl, Tank, Support |
| `attackType` | string | `""` | Which DamageType this character's attacks carry; blank means Physical. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `maxHealth` | int | `20` | Base max health, before talents. |  |
| `speed` | int | `10` | Base speed, before talents; drives how often this character acts in the charge-based turn order. |  |
| `attack` | int | `5` | Base attack, before talents. |  |
| `physicalDefense` | int | `24` | Base flat reduction against Physical damage, before talents. |  |
| `magicalDefense` | int | `12` | Base flat reduction against non-Physical damage, before talents. |  |
| `strength` | int | `10` | Base Strength; the six ability scores must total exactly the resolver's budget. |  |
| `dexterity` | int | `10` | Base Dexterity; the six ability scores must total exactly the resolver's budget. |  |
| `constitution` | int | `10` | Base Constitution; the six ability scores must total exactly the resolver's budget. |  |
| `wisdom` | int | `10` | Base Wisdom; the six ability scores must total exactly the resolver's budget. |  |
| `intelligence` | int | `10` | Base Intelligence; the six ability scores must total exactly the resolver's budget. |  |
| `charisma` | int | `10` | Base Charisma; the six ability scores must total exactly the resolver's budget. |  |
| `princesFavor` | int | `0` | This character's luck stat; a separate axis from the six ability scores, 0 is the honest default. |  |
| `portraitPath` | string | `""` | Resources-relative path (no extension) to a head-and-shoulders portrait loaded at runtime, e.g. 'Portraits/sheep'; empty means no art yet and the dossier keeps its armour-stand placeholder. |  |
| `battleSpritePath` | string | `""` | Resources-relative folder of full-body stance art loaded at runtime; empty means no art yet. |  |
| `battleSpriteFacing` | string | `""` | Which way the battle art is drawn in its source file: 'Right' or 'Left'. | Left, Right |
| `signatureId` | string | `""` | The id of this character's private signature resource; empty means the character has none. |  |
| `signatureDisplayName` | string | `""` | The name shown for the signature resource, e.g. 'Wool'. |  |
| `signatureCapacity` | int | `0` | How much signature resource this character can hold. |  |
| `signatureGainPerTurn` | int | `0` | Signature resource gained automatically at the start of each of this character's turns. |  |
| `signatureGainOnAttack` | int | `0` | Signature resource gained when this character attacks. |  |
| `signatureGainOnDamageTaken` | int | `0` | Signature resource gained when this character takes damage. |  |
| `signatureAbsorbsDamage` | bool | `false` | Whether the signature resource also soaks incoming damage before health. |  |
| `startsInSquad` | bool | `false` | Whether a fresh profile fields this character; exactly three characters must set it. |  |
| `squadSlot` | int | `0` | This character's place in the starting squad, 1-3 and unique; only read when startsInSquad is set. |  |

## enemies.json -- `RawEnemyEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; used to derive an unauthored weakness/resistance pair and matched against monster references elsewhere in content. |  |
| `displayName` | string | (none -- required) | The name shown for this monster in the fight UI. |  |
| `maxHealth` | int | `-1` | The monster's maximum health. |  |
| `attack` | int | `-1` | The monster's base attack stat. |  |
| `physicalDefense` | int | `-1` | Flat damage reduction against Physical attacks; -1 derives it from maxHealth. |  |
| `magicalDefense` | int | `-1` | Flat damage reduction against non-Physical attacks; -1 derives it from maxHealth. |  |
| `speed` | int | `-1` | The monster's speed, which drives how often it acts in the charge-based turn order. |  |
| `expReward` | int | `-1` | Experience granted to the party on defeating this monster. |  |
| `currencyReward` | int | `-1` | Currency granted to the party on defeating this monster. |  |
| `isBoss` | bool | `false` | Whether this monster only ever appears in the dungeon's final room. |  |
| `minFloor` | int | `0` | The shallowest floor this enemy may be drawn on; 0 or absent means floor 1. |  |
| `attackType` | string | `""` | The DamageType this monster's attacks and abilities carry; blank means Physical. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `weakness` | string | `""` | A comma-separated list of DamageType names this monster takes +50% from; blank derives one from the id, 'none' means genuinely no weakness. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `resistance` | string | `""` | A comma-separated list of DamageType names this monster takes -50% from; blank derives one from the id, 'none' means genuinely no resistance. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `breakShieldPoints` | int | `-1` | Stagger meter capacity; -1 derives it from maxHealth, 0 means this monster has no stagger meter at all. |  |
| `spritePath` | string | `""` | Resources-relative folder of this monster's stance sprites; empty means no art yet. |  |
| `skillName` | string | `""` | The legacy single second action's telegraph name; superseded by abilities for anything new. |  |
| `skillPower` | float | `-1` | The legacy second action's multiplier on this monster's own basic attack. |  |
| `skillChance` | float | `-1` | The legacy second action's odds of being picked on any given turn. |  |
| `abilities` | RawEnemyAbility[] (below) | `[]` | This monster's real skills (skills.json ids) with relative selection weights; replaces skillName/skillPower/skillChance when non-empty. |  |
| `attackWeight` | float | `1` | The relative weight of the monster's plain attack against its ability weights; 0 removes plain attacks entirely. |  |
| `active` | bool | `true` | Whether this monster can actually spawn; false benches the entry without deleting it. |  |
| `facing` | string | `"right"` | Which way this monster's art is drawn in its source file, so the stage knows whether to mirror it. | Left, Right |
| `vfx` | SpellPresentation (below) | (zero -- see SpellPresentation) | VFX played over the target when this monster's skill lands; see SpellPresentation. |  |
| `appliesStatus` | string | `""` | Which StatusEffectType this monster's basic attack or skill applies to whoever it hits, or empty for none. | Poison, Regen, Protect, Vulnerable, Stun, Shielded, Provoked, Empowered, Chilled, Rooted, Marked, Feared |
| `statusMagnitude` | int | `-1` | The magnitude of the applied status; required together with appliesStatus. |  |
| `statusDuration` | int | `-1` | How many of the afflicted combatant's own turns the applied status lasts; required together with appliesStatus. |  |
| `avoidsFrontSlot` | bool | `false` | Whether this monster is never placed in the front stage slot when the room's other picks give an alternative. |  |
| `attackHoldsPosition` | bool | `false` | Whether this monster's plain-attack art is a stationary pose rather than a forward strike. |  |
| `attackApproach` | string | `""` | How this monster's plain attack travels when not holding position: lunge (default), close, or charge. | Hold, Lunge, Close, Charge |
| `stageScale` | float | `0` | A multiplier on this monster's stage size, on top of its slot's own depth scale; 0 means unset and reads as 1. |  |
| `slotSpan` | int | `0` | How many of the stage's positions this monster occupies; 0 means unset and reads as 1. |  |

## items.json -- `RawItemEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save files and inventory entries. |  |
| `displayName` | string | (none -- required) | The name shown for this item. |  |
| `description` | string | `""` | Flavor text shown to the player. |  |
| `kind` | string | `""` | Which ItemKind this is: Consumable (default), Weapon, or Equipment, case-insensitive. | Consumable, Weapon, Equipment |
| `effect` | string | `""` | For a Consumable, which effect it triggers: Heal (default) or RestoreMana. | Heal, RestoreMana |
| `amount` | int | `-1` | For a Consumable, how much the effect heals or restores. |  |
| `attackBonus` | int | `-1` | For a Weapon, added to the equipping character's Attack; required for a Weapon. |  |
| `slot` | string | `""` | Which paperdoll slot this is worn in; required for Equipment, optional (defaults to Weapon1) for a Weapon. |  |
| `statBonus` | StatBlock | (zero -- see StatBlock) | Combat stats granted for as long as this Equipment/Weapon is worn. |  |
| `abilityScoreBonus` | AbilityScoreBlock | (zero -- see AbilityScoreBlock) | Ability scores granted for as long as this Equipment/Weapon is worn. |  |
| `requires` | string[] | `[]` | '<ability score> <amount>' lines gating whether this item counts as worn at all. |  |
| `startingStock` | bool | `false` | Whether one is granted, once, into a brand new profile's starting stash. |  |
| `cost` | int | `-1` | Permanent-currency Store price; Weapons/Equipment default to 0 and are not sold. |  |
| `iconPath` | string | `""` | Editor-time path to this item's icon. |  |

## itemsets.json -- `RawItemSetEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; generated item ids are built from it and item ids live in save files. |  |
| `displayName` | string | (none -- required) | What the material is called in item names, e.g. 'Leather' gives 'Hardened Leather Coif'. |  |
| `description` | string | `""` | Flavor text shown to the player. |  |
| `tierAdjectives` | string[] | `[]` | One adjective per tier, lowest first; the last covers every tier past the list's end. |  |
| `maxTier` | int | `-1` | The highest tier this set goes to; every piece is generated at tier 0..maxTier. |  |
| `cost` | int | `-1` | Gold cost of the tier-0 piece. |  |
| `costPerTier` | int | `-1` | Gold added per tier above 0. |  |
| `sortOrder` | int | `-1` | Sort position among item sets. |  |
| `styleWeights` | string[] | `[]` | '<ability score> <hundredths>' lines: this material's per-score share of the ability-score half of the budget, e.g. 'strength 40' is a weight of 0.40. |  |
| `statProfile` | string[] | `[]` | '<combat stat> <percent>' lines spending the combat-stat half of the budget; the five lines must sum to exactly 100. |  |
| `pieces` | RawSetPiece[] (below) | `[]` | This set's pieces; see RawSetPiece. |  |

## modifiers.json -- `RawModifierEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save data on a rolled item instance. |  |
| `displayName` | string | (none -- required) | The name shown for this modifier ('Rift affix'). |  |
| `description` | string | `""` | Flavor/rules text shown to the player. |  |
| `effects` | RawModifierEffect[] (below) | `[]` | The single rule this modifier grants; more than one entry is rejected. |  |

## relics.json -- `RawRelicEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save data via unlock/achievement references. |  |
| `displayName` | string | (none -- required) | The name shown for this relic when offered. |  |
| `description` | string | `""` | Flavor/rules text shown to the player. |  |
| `effect` | string | `""` | Which RelicEffect this grants, matched case-insensitively; empty resolves to None. | None, DualWield, MagicalShield, Bloodlust, ChargingCrystal, BallerinasSlippers, TinFoilPipe, ToothedNecklace, BountyHunterContract, SwordInABox, LuckyDeck, DrownedLantern, FirstRune, SaltLedger, LongCount, MagicMarker, JarOfBearUrine, WorldEndersCrown, CursedIdol, AmassingStar, RampagingBullsHorn, IceFingernail, LoadedDice, MonkeyKingsScepter, SparringSaber, SparringBuckler, EssenceSiphon, DisgruntledLackey, InconspicuousKey, DancersAnklet, BerserkersVest, PhoenixEgg |
| `iconPath` | string | `""` | Editor-time path to this relic's icon under Assets/_Project/Art/Items/Relics/Processed/; empty falls back to a flat accent-coloured circle. |  |
| `rarity` | string | `""` | How rare the offer is, one of RelicRarity's names; empty means Common. | Common, Uncommon, Rare, UltraRare, Mythic, Godlike |
| `unlockedBy` | string | `""` | The achievement id that must be earned before this relic can be offered; empty means available from the first run. |  |
| `modifiers` | RawRelicModifier[] (below) | `[]` | Numeric stat changes this relic grants; see RawRelicModifier. |  |
| `requiresConvergenceAbility` | bool | `false` | Whether this relic is only ever offered to a party that already has a convergence/ultimate ability. |  |

## skills.json -- `RawSkillEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save files and never renamed once used. |  |
| `displayName` | string | (none -- required) | The name shown on the skill's own button. |  |
| `description` | string | `""` | Flavor text shown to the player; read by no formula. |  |
| `characterId` | string | `""` | The character this skill belongs to; required so it is never offered to everyone. |  |
| `unlockLevel` | int | `-1` | The character level this skill becomes available at; see notes for the bookOnly exception. |  |
| `effect` | string | `""` | Which SkillEffect this casts, matched case-insensitively. | DamageSingle, DamageAll, HealSelf, HealParty, RestorePartyMana, Provoke, Transform, Ward, Shatter, BuffParty, GiftMana, GiftFury, GiftHaste, Summon |
| `targeting` | string | `""` | Which SkillTargeting this hits; defaults to whatever the effect implies. | SingleEnemy, AllEnemies, Self, Party |
| `manaCost` | int | `-1` | Mana spent to cast; a skill must cost this and/or resourceCost. |  |
| `resourceCost` | int | `-1` | How much of the owner's signature resource a cast consumes. |  |
| `spendsAllResource` | bool | `false` | Whether the cast takes the caster's entire signature resource instead of resourceCost. |  |
| `power` | int | `-1` | Added per point of resource actually spent when casting. |  |
| `flatAmount` | int | `-1` | A flat contribution before scaling: the whole amount for a heal, an offset on top of Attack for damage. |  |
| `ignoresDefense` | bool | `false` | Whether this skill's damage skips the target's Defense entirely. |  |
| `damageInstances` | RawDamageInstance[] (below) | `[]` | Fixed, typed damage packets that replace the Attack/power formula entirely; each is checked against weakness and resistance on its own. |  |
| `cooldownTurns` | int | `0` | How many of the caster's own turns must pass before this skill can be cast again; 0 means no cooldown. |  |
| `playerSelectable` | bool | `true` | Whether a player ever picks this from a menu, as against a monster-only skill drawn by weighted chance. |  |
| `vfx` | SpellPresentation (below) | (zero -- see SpellPresentation) | How the skill looks and sounds when it resolves; see SpellPresentation. |  |
| `appliesStatus` | string | `""` | Which StatusEffectType this skill applies on landing, or empty for none. | Poison, Regen, Protect, Vulnerable, Stun, Shielded, Provoked, Empowered, Chilled, Rooted, Marked, Feared |
| `statusMagnitude` | int | `-1` | The magnitude of the applied status; required together with appliesStatus. |  |
| `statusDuration` | int | `-1` | How many of the afflicted combatant's own turns the applied status lasts; required together with appliesStatus. |  |
| `requires` | string[] | `[]` | '<ability score> <amount>' lines gating whether this skill can be cast at all. |  |
| `scalingAxis` | string | `""` | Which axis this skill's damage rides: Auto (derive from the caster's own attackType), Weapon, Spell, or None. | Auto, Weapon, Spell, None |
| `queuePushSlots` | int | `0` | How many places later in the turn queue this skill knocks its target; 0 means it does not touch the queue. |  |
| `transform` | TransformGrant | (zero -- see TransformGrant) | What the caster transforms into, for a Transform effect; rejected if authored on any other effect. |  |
| `stance` | string | `""` | Which of the caster's own stance folders plays while this skill resolves; empty means the default cast pose. |  |
| `approach` | string | `""` | How the caster gets to what it is hitting: hold, lunge, or close; empty means hold. | Hold, Lunge, Close, Charge |
| `shake` | float | `0` | How hard this skill kicks the stage on its own account, 0..1; 0 means whatever the damage was worth. |  |
| `summonEnemyId` | string | `""` | The enemy id this skill calls onto the caster's own side, for a Summon effect. |  |
| `summonCap` | int | `-1` | The most living copies of summonEnemyId allowed on the caster's side before this skill stops being drawn. |  |
| `bookOnly` | bool | `false` | Whether this skill is learned from a shop book rather than by levelling. |  |
| `bookTier` | int | `0` | The shop's price band (1-4) for this spell as a book; 0 means not book-eligible. |  |
| `meleeReach` | bool | `false` | Whether the front-rank melee-reach rule applies to this SingleEnemy skill. |  |

## spells.json -- `RawSpellTierEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `level` | int | `0` | The character level this tier applies from; the highest tier not exceeding the caster's level is used. |  |
| `displayName` | string | (none -- required) | The name shown for the Skill action at this tier. |  |
| `manaCost` | int | `0` | Mana cost of the Skill action at this tier. |  |
| `powerMultiplier` | float | `0` | The multiplier applied to the Skill action's power at this tier. |  |
| `scalesWith` | string[] | `[]` | '<ability score> <grade>' lines this tier's Skill scales on, alongside powerMultiplier. |  |
| `requires` | string[] | `[]` | '<ability score> <amount>' lines gating whether this tier is the one used; unmet falls back to the highest tier that is met. |  |

## talents.json -- `RawTalentEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save files as an unlocked-talent id. |  |
| `displayName` | string | (none -- required) | The name shown on this talent's node. |  |
| `description` | string | `""` | Flavor/rules text shown to the player. |  |
| `characterId` | string | `""` | The one character who can take this talent, or empty for a node every character shares. |  |
| `column` | int | `0` | Which of the 3 paths (0-2) this talent sits in. |  |
| `row` | int | `0` | The slot index (0-20) into that path's fixed skeleton. |  |
| `prerequisites` | string[] | `[]` | Ids of talents that unlock this one; a chain node names one, a convergence node names all of its parents (any one invested is enough). |  |
| `minSpent` | int | `0` | Requires at least this many points already spent in this talent's own path before it can be taken; 0 means no gate. |  |
| `statBonus` | StatBlock | (zero -- see StatBlock) | Combat stats granted while this talent is unlocked. |  |
| `abilityScoreBonus` | AbilityScoreBlock | (zero -- see AbilityScoreBlock) | Ability scores granted while this talent is unlocked. |  |
| `maxManaBonus` | int | `0` | Maximum mana granted while this talent is unlocked. |  |
| `skillManaCostReduction` | int | `0` | Percent reduction to skill mana costs granted while this talent is unlocked. |  |
| `signatureCapacityBonus` | int | `0` | Signature-resource capacity granted while this talent is unlocked. |  |
| `signaturePerTurnBonus` | int | `0` | Signature resource gained per turn, granted while this talent is unlocked. |  |
| `grantsStartingItemId` | string | `""` | An items.json id granted once into the owner's stash when this talent is taken. |  |
| `effects` | RawTalentEffect[] (below) | `[]` | Non-numeric rules this talent grants; see RawTalentEffect. |  |
| `grantsSkillId` | string | `""` | A skills.json id this talent adds to the owner's combat strip. |  |
| `iconPath` | string | `""` | Editor-time path to this talent's archetype glyph; empty is a supported state, most talents share a small set of archetype icons. |  |

## upgrades.json -- `RawUpgradeEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save data as a purchase, so never rename it after a save exists. |  |
| `displayName` | string | (none -- required) | The name shown in the Principality shop. |  |
| `description` | string | `""` | Flavor text shown under the name; empty is allowed but reads as an unfinished row. |  |
| `cost` | int | `50` | Cost in Principality currency; must not be negative, and 0 means free rather than unbuyable. |  |
| `startingGoldBonus` | int | `0` | In-run currency granted at the start of every run, if this upgrade grants that; 0 for upgrades that do not. |  |

## weapons.json -- `RawWeaponEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; generated item ids are built from it and item ids live in save files. |  |
| `displayName` | string | (none -- required) | The noun in every generated name for this family, e.g. 'Sword' gives 'Keen Sword'. |  |
| `description` | string | `""` | Flavor text shown to the player. |  |
| `maxTier` | int | `-1` | The highest tier this family goes to; every tier 0..maxTier generates one weapon. |  |
| `cost` | int | `-1` | Gold cost of the tier-0 weapon. |  |
| `costPerTier` | int | `-1` | Gold added per tier above 0. |  |
| `sortOrder` | int | `-1` | Sort position among weapon families. |  |
| `slot` | string | `"Weapon"` | Which hand slot this family is worn in; 'Weapon' is the alias for Weapon1. |  |
| `attackAtZero` | int | `-1` | Attack at tier 0, interpolated up to attackAtMax. |  |
| `attackAtMax` | int | `-1` | Attack at maxTier, interpolated down to attackAtZero. |  |
| `iconSheet` | string | `""` | Folder of per-level art sliced by tools/slice_item_sheet.py; used when the family has per-tier art. |  |
| `iconLevels` | int | `0` | How many levels the icon sheet holds. |  |
| `iconPath` | string | `""` | A single flat image for the whole family, used only when there is no iconSheet. |  |
| `tierAdjectives` | string[] | `[]` | One adjective per tier, lowest first, e.g. 'Worn Sword' through 'Sovereign Sword'; the last covers every tier past the list's end. |  |
| `primary` | string | (none -- required) | The one ability score this family's Attack scales on by default, e.g. Sword->STR. |  |
| `primaryAtZero` | string | `""` | The scaling grade this family reaches at tier 0. |  |
| `primaryAtMax` | string | `""` | The scaling grade this family reaches at maxTier; may not be lower than primaryAtZero. |  |
| `secondary` | string | `""` | An optional second ability score this family also scales on. |  |
| `secondaryAtZero` | string | `""` | The secondary scaling grade at tier 0. |  |
| `secondaryAtMax` | string | `""` | The secondary scaling grade at maxTier. |  |
| `alsoScalesWith` | string[] | `[]` | Flat '<score> <grade>' lines this family also rides, unmoved by tier. |  |
| `spellScalesWith` | string[] | `[]` | Flat '<score> <grade>' lines for what this weapon contributes to Skill/spell power, unmoved by tier. |  |
| `requiresAtZero` | string[] | `[]` | '<ability score> <amount>' lines gating whether the tier-0 weapon counts as worn. |  |
| `requiresAtMax` | string[] | `[]` | '<ability score> <amount>' lines gating whether the maxTier weapon counts as worn, interpolated between with requiresAtZero. |  |

## Shared value types

Referenced from a field above (an array element or a nested block, such as `vfx`); documented once here rather than once per use.

### `RawDamageInstance`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `type` | string | `""` | A DamageType name this packet is typed as, matched case-insensitively ('Frost' is accepted for Ice). | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `amount` | int | `0` | The flat amount this packet deals, on the same x10 scale as every other damage number. |  |

### `RawEnemyAbility`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `skillId` | string | `""` | A skill id from skills.json this monster may draw. |  |
| `weight` | float | `1` | The relative likelihood this ability is chosen; 0 means authored but never drawn unless every entry is 0. |  |

### `RawModifierEffect`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `type` | string | `""` | Which Domain.Combat.ModifierEffectType this rule grants, matched case-insensitively. | None, ElementalDamageOnHitPercent, TypedResistanceFlat, FlatSpeedBonus, LifestealPercent, GuaranteedFirstAction, FlatPhysicalDamageReduction, BreakShieldDepletionResistPercent, OnKillSplashPercent, PushBackOnHitChancePercent, FlatMaxManaBonus, FlatManaRegenBonus, NextSkillManaDiscountPercent, ManaToWardOnTurnStartPercent, FortunateFavorBonusFlat, DodgeRating, ChilledOnHitChancePercent, RootChancePercent |
| `magnitude` | int | `0` | The rule's UNSCALED base magnitude; the fight reads base x TierMultiplier x RiftMultiplier. |  |
| `threshold` | int | `0` | The rule's threshold; meaning depends on type. |  |
| `damageType` | string | `""` | A DamageType name (or 'magical') this rule targets; required by TypedResistanceFlat only. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |

### `RawRelicModifier`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `type` | string | `""` | Which RelicModifierType this changes, matched case-insensitively. | None, AttackPercent, DefensePercent, MaxHealthPercent, MaxManaPercent, SpeedPercent, ResistanceFlat, AttackFlat, DefenseFlat, MaxHealthFlat, MaxManaFlat, SpeedFlat, ArmorPenetrationFlat, WeaknessDamageBonusPercent, LifestealPercent |
| `amount` | int | `0` | How much this modifier changes the stat, percent or flat depending on type. |  |
| `damageType` | string | `""` | A DamageType name (or 'magical' for every non-Physical type), required by ResistanceFlat only. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |

### `RawSetPiece`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier, unique only within its own set; the generated item id is '<set>_<piece>_p<tier>'. |  |
| `displayName` | string | (none -- required) | The piece's own name, e.g. a Helmet for steel where leather has a Coif. |  |
| `slot` | string | (none -- required) | Which paperdoll slot this piece is worn in. |  |
| `baseStats` | string[] | `[]` | REMOVED from the schema; a piece naming this fails the content build, pointing at statProfile instead. |  |
| `topStats` | string[] | `[]` | REMOVED from the schema; a piece naming this fails the content build, pointing at statProfile instead. |  |
| `requiresAtZero` | string[] | `[]` | '<ability score> <amount>' lines gating whether the tier-0 piece counts as worn. |  |
| `requiresAtMax` | string[] | `[]` | '<ability score> <amount>' lines gating whether the maxTier piece counts as worn, interpolated with requiresAtZero. |  |
| `iconSheet` | string | `""` | Folder of this piece's art, one PNG per item level ('level_1.png' upward); empty means no art yet. |  |
| `iconLevels` | int | `0` | How many levels iconSheet holds; 0 means the standard ten. |  |

### `RawTalentEffect`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `type` | string | `""` | Which Domain.Combat.TalentEffectType this rule grants, matched case-insensitively. | None, WoolOnHitTaken, WoolPerTurnBelowHealth, WoolWhenWardedAllyHit, WoolPerStatusedEnemy, IgnoreDefensePercent, ShredDefenseOnHit, ProvokedDamageReductionPercent, ProvokeHitsEveryEnemy, WoolPerProvokedEnemy, ExecuteDamageBonusPercent, KillSplashPercentOfAttack, ExtraAttackOnKill, TransformDurationBonus, TransformExtendOnKill, TransformHoldsBelowHealth, DefenseBonusPercentBelowHealth, DamageCapPercentBelowHealth, CheatDeathOncePerFight, HeadbuttCancelsIntent, TransformPushesEveryEnemy, TransformPermanentBelowHealth, WardReductionPercent, WardIsFreeAction, WardAlsoAppliesRegen, WardHealsWhenSpent, WardDamageBonusPerAlly, WardDamageBonusSelf, WardSelfBonusPersistsTurns, WardSpreadsToAllies, WardSpreadsToWholeParty, GiftManaPercent, GiftAttackBonusPercent, GiftAppliesImmediateTurn, ShatterDamagePercentOfAttack, ShatterSelfWardMultiplier, ShatterAppliesVulnerable, WardsNeverExpire |
| `magnitude` | int | `0` | The rule's magnitude; meaning depends on type. |  |
| `threshold` | int | `0` | The rule's threshold; meaning depends on type. |  |

### `SpellPresentation`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `path` | string | `""` | Resources-relative folder of this spell's animation frames; empty means no visual. |  |
| `seconds` | float | `0.6` | How long the whole per-target animation takes, in seconds. |  |
| `impactFrame` | int | `3` | Which frame (1-based) the spell actually lands on. |  |
| `anchor` | string | `""` | Where the effect happens: a SpellAnchor name (Target, Caster, ...), parsed case-insensitively; unrecognised falls back to Target. |  |
| `size` | float | `380` | The square box the art is fitted into, in reference-frame units; 0 means the default size. |  |
| `departFrame` | int | `0` | Which frame (1-based) a from-caster effect leaves on; 0 means from the first frame. |  |
| `impactX` | float | `-1` | Where the blow lands inside the sheet, as a fraction from the left edge; -1 means unauthored (use the measured fallback). |  |
| `impactY` | float | `-1` | Where the blow lands inside the sheet, as a fraction from the bottom edge; -1 means unauthored (use the measured fallback). |  |
| `sfxPath` | string | `""` | Resources-relative path to the sound played the moment the blow lands. |  |
| `groundPath` | string | `""` | Resources-relative folder of the shared ground-layer frames drawn once behind every enemy struck; empty means no ground layer at all. |  |
| `groundSeconds` | float | `0` | How long the ground layer runs, in seconds; 0 falls back to the per-target sequence's own seconds. |  |
| `groundImpactFrame` | int | `0` | Which frame (1-based) the ground layer ruptures on; 0 falls back to the per-target sequence's own impactFrame. |  |
| `groundAspect` | float | `0` | Width-over-height of the ground layer's box; 0 means take the sheet's own frame aspect. |  |
| `groundImpactY` | float | `-1` | Where the ground layer's own ground line sits, as a fraction from the bottom edge; -1 means unauthored. |  |
| `castSfxPath` | string | `""` | Resources-relative path to the sound that runs through the cast, ending before the rupture. |  |

