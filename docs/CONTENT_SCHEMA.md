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
| `strength` | int | `10` | Base Strength; must be 1-30 (CharacterEntryResolver.MinAbilityScore/MaxAbilityScore). |  |
| `dexterity` | int | `10` | Base Dexterity; must be 1-30 (CharacterEntryResolver.MinAbilityScore/MaxAbilityScore). |  |
| `constitution` | int | `10` | Base Constitution; must be 1-30 (CharacterEntryResolver.MinAbilityScore/MaxAbilityScore). |  |
| `wisdom` | int | `10` | Base Wisdom; must be 1-30 (CharacterEntryResolver.MinAbilityScore/MaxAbilityScore). |  |
| `intelligence` | int | `10` | Base Intelligence; must be 1-30 (CharacterEntryResolver.MinAbilityScore/MaxAbilityScore). |  |
| `charisma` | int | `10` | Base Charisma; must be 1-30 (CharacterEntryResolver.MinAbilityScore/MaxAbilityScore). |  |
| `princesFavor` | int | `0` | This character's luck stat; a separate axis from the six ability scores, 0 is the honest default. |  |
| `portraitPath` | string | `""` | Resources-relative path (no extension) to a head-and-shoulders portrait loaded at runtime, e.g. 'Portraits/sheep'; empty means no art yet and the dossier keeps its armour-stand placeholder. |  |
| `battleSpritePath` | string | `""` | Resources-relative folder of full-body stance art loaded at runtime; empty means no art yet. |  |
| `battleSpriteFacing` | string | `""` | Which way the battle art is drawn in its source file: 'Right' or 'Left'. | Left, Right |
| `plateTheme` | string | `""` | Which UiKit.ButtonTheme this character's fight-HUD cards are coloured with (rim and name), matched case-insensitively. Required: an empty or unknown value refuses the build. | Gold, Crimson, Violet, Blue, Green, Silver |
| `plateArt` | string | `""` | Resources-relative path (no extension) to this character's fight-HUD plate, e.g. 'Plates/pc_sheep'. Required: an empty path, or one that loads nothing, refuses the build. |  |
| `primaryPoolId` | string | `"mana"` | The pools.json id of the resource this character's skills spend; refused unless pools.json defines it, and blank means 'mana'. |  |
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

## pools.json -- `RawPoolEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier a character's primaryPoolId names; unique across every content type. |  |
| `displayName` | string | (none -- required) | The name shown for this pool, e.g. 'Mana'; the character sheet's max-resource row reads it. |  |
| `shortTag` | string | (none -- required) | The short tag drawn on the combat meter, e.g. 'MP'; no whitespace, at most 6 characters. |  |
| `capacityRule` | string | (none -- required) | Required. How capacity is derived: 'WisdomDerived' (the authored capacity is a base every Max Mana source adds to) or 'Fixed' (the authored capacity is the whole number, from every source). | WisdomDerived, Fixed |
| `capacity` | int | `0` | How much this pool holds; must be positive. Under 'WisdomDerived' it is the BASE every Max Mana source adds to, under 'Fixed' it is the entire capacity. |  |
| `gainPerTurn` | int | `0` | Gained at the start of each of the owner's turns; must not be negative. |  |
| `gainOnAttack` | int | `0` | Gained once when the owner performs an action that deals damage -- a plain attack or a damaging skill, once however many targets it hits, and never on a miss or a heal; must not be negative. |  |
| `gainOnDamageTaken` | int | `0` | Gained when the owner takes damage; must not be negative. |  |
| `decayPerIdleTurn` | int | `0` | Lost at the start of an idle turn (see decayUnless); must not be negative. 0 means the pool never decays. |  |
| `decayUnless` | string | `""` | What stops a turn counting as idle: 'Damage' (dealt or taken) or 'AnyAction'. Blank means Damage, and it may only be authored on a pool that actually decays. | Damage, AnyAction |
| `startRule` | string | (none -- required) | Required. What the pool holds at the start of a fight: 'Full', 'Zero', or 'Value' (see startValue). | Full, Zero, Value |
| `startValue` | int | `0` | Only for startRule 'Value': what the pool opens a fight at, 1..capacity. Must be 0 under any other start rule. |  |
| `brightHex` | string | (none -- required) | The meter fill colour, '#RRGGBB' or '#RRGGBBAA'. |  |
| `deepHex` | string | (none -- required) | The meter's dark tone, '#RRGGBB' or '#RRGGBBAA'; its rim and shade are derived from this at 0.70 and 0.44 alpha. |  |
| `textHex` | string | (none -- required) | The colour of the meter's own text, '#RRGGBB' or '#RRGGBBAA'. |  |
| `pulse` | bool | `false` | Whether the meter beats like a heartbeat while the fight runs. |  |
| `allowsSpellBooks` | bool | `true` | Whether a character whose primary pool this is may hold spell books. |  |
| `restoredByManaEffects` | bool | `true` | Whether mana potions, RestorePartyMana and the bot's mana accounting refill this pool. |  |
| `absorbsDamage` | bool | `false` | Whether this pool soaks incoming damage before health, the way a signature resource can. |  |

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

## reward_tracks.json -- `RawRewardTrackEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `characterId` | string | `""` | The character this track belongs to; matches a characters.json id. |  |
| `milestones` | RawTrackMilestone[] (below) | `[]` | The track's twelve milestone rewards, one per fixed milestone level; see RawTrackMilestone. |  |
| `filler` | RawTrackFiller[] (below) | `[]` | The track's filler reward mix, spread evenly across its 87 non-milestone levels; see RawTrackFiller. |  |

## skills.json -- `RawSkillEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save files and never renamed once used. |  |
| `displayName` | string | (none -- required) | The name shown on the skill's own button. |  |
| `description` | string | `""` | Flavor text shown to the player; read by no formula. |  |
| `characterId` | string | `""` | The character this skill belongs to; required so it is never offered to everyone. |  |
| `unlockLevel` | int | `-1` | The character level this skill becomes available at; see notes for the bookOnly exception. |  |
| `effect` | string | `""` | Which SkillEffect this casts, matched case-insensitively. | DamageSingle, DamageAll, HealSelf, HealParty, RestorePartyMana, Provoke, Transform, Ward, Shatter, BuffParty, GiftMana, GiftFury, GiftHaste, Summon |
| `targeting` | string | `""` | Which SkillTargeting this hits; defaults to whatever the effect implies. | SingleEnemy, AllEnemies, Self, Party, SingleAlly |
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
| `approachStance` | string | `""` | The pose worn while the caster travels to its target (Close's walk-in, Lunge/Charge's crossing); empty means the strike pose is worn throughout. Ignored on a Hold approach, which has no travel. |  |
| `windupStance` | string | `""` | The pose held through the wind-up, between arrival and impact; empty means the strike pose is worn throughout. On a Hold or Close approach this buys the beat a wind-up wait it would not otherwise have. |  |
| `approach` | string | `""` | How the caster gets to what it is hitting: hold, lunge, or close; empty means hold. | Hold, Lunge, Close, Charge |
| `shake` | float | `0` | How hard this skill kicks the stage on its own account, 0..1; 0 means whatever the damage was worth. |  |
| `summonEnemyId` | string | `""` | The enemy id this skill calls onto the caster's own side, for a Summon effect. |  |
| `summonCap` | int | `-1` | The most living copies of summonEnemyId allowed on the caster's side before this skill stops being drawn. |  |
| `bookOnly` | bool | `false` | Whether this skill is learned from a shop book rather than by levelling. |  |
| `bookTier` | int | `0` | The shop's price band (1-4) for this spell as a book; 0 means not book-eligible. |  |
| `meleeReach` | bool | `false` | Whether the front-rank melee-reach rule applies to this SingleEnemy skill. |  |
| `reachSlots` | int[] | `[]` | Which 1-based positions in the enemy line this SingleEnemy skill may target; empty means anywhere. |  |
| `elements` | RawElementChoice[] (below) | `[]` | Elements the player chooses between before targeting; each retypes every authored damage packet. |  |
| `poolTiers` | RawPoolTier[] (below) | `[]` | Ascending fractions of the owner's primary pool this skill can spend for extra damage; the highest tier the caster can afford fires automatically. |  |

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

### `RawElementChoice`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `type` | string | `""` | A DamageType name this choice retypes the skill's packets to, matched case-insensitively ('Frost' is accepted for Ice). |  |
| `vfx` | SpellPresentation (below) | (zero -- see SpellPresentation) | How this element's cast looks and sounds; omitted, the skill's own vfx plays for every element. |  |

### `RawEnemyAbility`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `skillId` | string | `""` | A skill id from skills.json this monster may draw. |  |
| `weight` | float | `1` | The relative likelihood this ability is chosen; 0 means authored but never drawn unless every entry is 0. |  |

### `RawModifierEffect`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `type` | string | `""` | Which Domain.Combat.ModifierEffectType this rule grants, matched case-insensitively. | None, ElementalDamageOnHitPercent, ElementalDamagePercent, TypedResistanceFlat, FlatSpeedBonus, LifestealPercent, GuaranteedFirstAction, FlatPhysicalDamageReduction, BreakShieldDepletionResistPercent, OnKillSplashPercent, PushBackOnHitChancePercent, FlatMaxManaBonus, FlatManaRegenBonus, NextSkillManaDiscountPercent, ManaToWardOnTurnStartPercent, FortunateFavorBonusFlat, DodgeRating, ChilledOnHitChancePercent, RootChancePercent |
| `magnitude` | int | `0` | The rule's UNSCALED base magnitude; the fight reads base x TierMultiplier x RiftMultiplier. |  |
| `threshold` | int | `0` | The rule's threshold; meaning depends on type. |  |
| `damageType` | string | `""` | A DamageType name (or 'magical') this rule targets; required by TypedResistanceFlat only. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |

### `RawPoolTier`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `spend` | float | `0` | Fraction of the owner's primary pool CAPACITY this tier spends, (0,1]; the pool's own gainOnAttack still fires afterwards as normal. Tiers must be authored in ascending order. |  |
| `damageMultiplier` | float | `0` | How much this tier multiplies the skill's own computed damage by; must be 1 or higher. |  |
| `shake` | float | `0` | A floor on how hard this tier's blow kicks the stage, 0..1, on top of the skill's own shake; 0 means no extra floor. |  |
| `hitStopSeconds` | float | `0` | A floor on this tier's hit-stop, in seconds, clamped to HitStop.MaxSeconds; 0 means no extra floor. |  |

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

### `RawTrackFiller`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `reward` | string | `""` | Which TrackReward this filler row grants, matched case-insensitively against the enum member name. A one-shot capability (an unlock) is refused here -- filler may only be a grant. | None, StatPoint, MaxHealth, Respec, SecondLife, SignatureCapacity, SignatureGainPerTurn, SignatureGainOnDamageTaken, SignatureAbsorbs, ElementalDamagePercent, MaxMana, ManaRegen, UnlockSkill |
| `amount` | int | `0` | The reward's magnitude, paid at every filler level this row places. |  |
| `against` | string | `""` | The DamageType this reward is typed against, matched case-insensitively; only ElementalDamagePercent reads this, empty otherwise. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `count` | int | `0` | How many of the track's 87 filler levels this row occupies. Every row's count in a track must sum to exactly 87. |  |

### `RawTrackMilestone`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `level` | int | `0` | The milestone level this entry lands on; must be one of the track's twelve fixed milestone levels (10, 20, 25, 30, 40, 45, 50, 60, 70, 80, 90, 100), and every one of the twelve must be named exactly once. |  |
| `reward` | string | `""` | Which TrackReward this milestone grants, matched case-insensitively against the enum member name. | None, StatPoint, MaxHealth, Respec, SecondLife, SignatureCapacity, SignatureGainPerTurn, SignatureGainOnDamageTaken, SignatureAbsorbs, ElementalDamagePercent, MaxMana, ManaRegen, UnlockSkill |
| `amount` | int | `0` | The reward's magnitude -- a count for a grant (a stat point, max health), or an unlock's own parameter where it has one (SecondLife's charge count); 0 for an unlock with none (Respec). |  |
| `against` | string | `""` | The DamageType this reward is typed against, matched case-insensitively; only ElementalDamagePercent reads this, empty otherwise. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `skillId` | string | `""` | The skill id this reward unlocks; only UnlockSkill reads this, empty otherwise. |  |

### `SpellEmitter`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `path` | string | `""` | Resources-relative folder of particle stills; each particle picks one by hash, so a folder of one selects that one. |  |
| `rate` | float | `0` | Particles emitted per second while the window is open. |  |
| `burst` | int | `0` | Particles emitted the instant the window opens. |  |
| `window` | float | `0` | Seconds of emission; 0 means burst only. |  |
| `sourceDx` | float | `0` | Offset from the source anchor that particles are born at, in reference-frame units. |  |
| `sourceDy` | float | `0` | Offset from the source anchor that particles are born at, in reference-frame units. |  |
| `spreadDegrees` | float | `0` | Full cone width of the emission spread, in degrees. |  |
| `aimDegrees` | float | `0` | Centre of the emission cone, in degrees; 0 means along the cast's facing. |  |
| `speedMin` | float | `0` | Slowest initial speed, in reference-frame units per second. |  |
| `speedMax` | float | `0` | Fastest initial speed, in reference-frame units per second. |  |
| `inherit` | float | `0` | Fraction of the source's forward velocity a particle carries away, 0..1. |  |
| `drag` | float | `0` | Linear drag per second. |  |
| `gravity` | float | `0` | Acceleration in reference-frame units per second squared; negative falls. |  |
| `lifeMin` | float | `0` | Shortest particle lifetime, in seconds. |  |
| `lifeMax` | float | `0` | Longest particle lifetime, in seconds; also what an emitter layer's own lifetime adds to its window. |  |
| `sizeMin` | float | `1` | Smallest particle scale. |  |
| `sizeMax` | float | `1` | Largest particle scale. |  |
| `spinMin` | float | `0` | Slowest spin, in degrees per second; may be negative. |  |
| `spinMax` | float | `0` | Fastest spin, in degrees per second. |  |
| `fadeFrom` | float | `1` | Fraction of life at which alpha starts falling, 0..1. |  |
| `endScale` | float | `1` | Scale at the end of life, relative to the particle's own. |  |
| `seed` | int | `0` | Random seed; 0 derives one from the cast so two casts differ, non-zero repeats exactly. |  |
| `weights` | float[] | (none -- required) | Per-cell pick weight, one per still in emitter.path's folder in file order; blank means uniform. Each entry must be finite and >= 0, and at least one must be > 0. Length must equal the folder's own frame count. |  |

### `SpellLayer`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | `""` | Stable name for this layer; required only when another layer references it through place 'layer:<id>'. |  |
| `render` | string | `""` | What draws: sprite (an animated folder), still (one frame of a folder) or emitter (ballistic particles). No default -- a blank is refused. | Sprite, Still, Emitter |
| `place` | string | `""` | Where it belongs: caster, caster-centre, target, target-centre, formation, or 'layer:<id>' to ride another layer. No default -- a blank is refused. | Caster, CasterCentre, Target, TargetCentre, Formation, Layer |
| `follow` | bool | `false` | Re-read the anchor every tick (true) rather than sampling its position once when the layer opens (false). |  |
| `at` | string | `""` | When it starts: release (the beat opens), arrival (the cast's projectile lands) or hit (the authoritative impact cue). Blank means release. | Release, Arrival, Hit |
| `offset` | float | `0` | Seconds added to `at`. |  |
| `path` | string | `""` | Resources-relative FOLDER of frames, for sprite and still alike; a still draws that folder's startFrame. |  |
| `seconds` | float | `0` | Total playback length in seconds; 0 derives it from fps and the folder's frame count. |  |
| `fps` | float | `0` | Frames per second; 0 means fit the whole folder into `seconds`, which is what every pre-layer block becomes. |  |
| `startFrame` | int | `0` | Which frame the layer starts on, counting from 1; 0 means frame 1. |  |
| `until` | string | `""` | End policy: once, loop or hold. Blank means once. A travelling layer ends at its arrival whatever this says. | Once, Loop, Hold |
| `fade` | float | `0` | Seconds of alpha ramp-out after the layer's end; 0 means cut. Capped at SpellLayerRules.MaxFadeSeconds. |  |
| `dx` | float | `0` | Local offset from the anchor, in reference-frame units. |  |
| `dy` | float | `0` | Local offset from the anchor, in reference-frame units. |  |
| `scale` | float | `1` | Uniform scale applied on top of the fitted box. |  |
| `size` | float | `0` | The square box the art is fitted into; 0 means SpellPresentation.DefaultSize. Ignored by place 'formation', which measures its own span. |  |
| `facing` | string | `""` | Mirroring: auto (take the cast's facing), none (never mirror) or reverse. Blank means auto. | Auto, None, Reverse |
| `sort` | string | `""` | Draw band: ground (behind the racks) or effects (over the HUD, under the damage numbers). Blank means effects. | Ground, Effects |
| `impactX` | float | `-1` | Where the blow lands inside this layer's frames, as a fraction from the left edge; -1 means unauthored. |  |
| `impactY` | float | `-1` | Where the blow lands inside this layer's frames, as a fraction from the bottom edge; -1 means unauthored. |  |
| `aspect` | float | `0` | Width-over-height of the box; 0 means take the sheet's own frame aspect. |  |
| `travelSeconds` | float | `0` | Seconds this layer takes to cross from its anchor to the target, departure to arrival; 0 means it does not travel. |  |
| `travelDelay` | float | `0` | Seconds after this layer starts before its motion begins; the wind-up held at the caster. |  |
| `emitter` | SpellEmitter (below) | (zero -- see SpellEmitter) | Ballistic particle settings; inert unless render is 'emitter'. |  |

### `SpellPresentation`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `path` | string | `""` | Resources-relative folder of this spell's animation frames; empty means no visual. |  |
| `seconds` | float | `0.6` | How long the whole per-target animation takes, in seconds. |  |
| `impactFrame` | int | `3` | Which frame (1-based) the spell actually lands on. |  |
| `anchor` | string | `""` | Where the effect happens: a SpellAnchor name (Target, Caster, ...), parsed case-insensitively; unrecognised falls back to Target. | Target, Caster, TargetCentre, CasterCentre, Travel, TravelCentre |
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
| `layers` | SpellLayer[] (below) | `[]` | Ordered layers this spell draws; empty means the single-block fields above are used as-is. |  |
| `layerFormat` | int | `0` | Which revision of the layer vocabulary this block was authored against; 0 means the pre-layer format. |  |
| `hitCueSeconds` | float | `0` | Seconds after the cast opens that the blow lands; the one authoritative impact cue. Ignored for a pre-layer block, which derives the cue from impactFrame. |  |

