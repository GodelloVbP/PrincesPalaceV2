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
| `epithet` | string | `""` | Optional short line shown under this character's name on the dialogue name plate, e.g. 'Prince the cat'. Max 32 characters; empty means the plate shows no epithet. |  |
| `dialogueBustPath` | string | `""` | Resources-relative FOLDER (no extension) of this character's dialogue busts, one PNG per expression named by Domain.Content.DialogueBust, e.g. 'Portraits/Dialogue/sheep'; empty means no bust art yet and the dialogue stage shows none. |  |
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
| `appliesStatus` | string | `""` | Which StatusEffectType this monster's basic attack or skill applies to whoever it hits, or empty for none. | Poison, Regen, Protect, Vulnerable, Stun, Shielded, Provoked, Empowered, Chilled, Rooted, Marked, Feared, Burn, Thorned |
| `statusMagnitude` | int | `-1` | The magnitude of the applied status; required together with appliesStatus. |  |
| `statusDuration` | int | `-1` | How many of the afflicted combatant's own turns the applied status lasts; required together with appliesStatus. |  |
| `avoidsFrontSlot` | bool | `false` | Whether this monster is never placed in the front stage slot when the room's other picks give an alternative. |  |
| `attackHoldsPosition` | bool | `false` | Whether this monster's plain-attack art is a stationary pose rather than a forward strike. |  |
| `attackApproach` | string | `""` | How this monster's plain attack travels when not holding position: lunge (default), close, or charge. | Hold, Lunge, Close, Charge |
| `stageScale` | float | `0` | A multiplier on this monster's stage size, on top of its slot's own depth scale; 0 means unset and reads as 1. |  |
| `slotSpan` | int | `0` | How many of the stage's positions this monster occupies; 0 means unset and reads as 1. |  |
| `rollable` | bool | `true` | Whether room fights may roll this monster; false keeps it out of the room pool while an event fight can still name it. Distinct from active, which builds no asset at all. |  |
| `rallyPerRound` | RawEnemyRally (below) | (zero -- see RawEnemyRally) | An attack stack this monster gains as each round starts, for the rest of the fight; see RawEnemyRally. Omitted or all zero means none. |  |

## events.json -- `RawEventEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | `""` | Stable identifier; persisted in the run's eventsSeen list so an event shows at most once per run (a mayReturn event: until a finish effect applies). |  |
| `floors` | int[] | `[]` | Floor numbers this event may appear on; empty means every floor. |  |
| `requires` | RawEventRequirement[] (below) | `[]` | Event-level requirements; all must pass for this event to be eligible to be rolled at all. |  |
| `pages` | RawEventPage[] (below) | `[]` | The event's page graph; the first page is where the event opens. |  |
| `backdrop` | string | `""` | The dialogue stage's full-bleed backdrop, Assets-relative with its extension, baked at scene build like a page's artPath; a page's own backdrop overrides it. Empty means EventEntryResolver.DefaultBackdrop (Assets/_Project/Art/Backgrounds/Dungeon.png). Must be filed under Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/<event_id>/. |  |
| `mayReturn` | bool | `false` | When true, opening this event does not mark it seen: it stays eligible for every later Event node this run until a finish effect applies. False (every event before the Bell) marks it seen the moment it opens. A finish effect on an event without mayReturn is refused. |  |
| `speakers` | RawEventSpeaker[] (below) | `[]` | This event's own speakers: people who belong to the event rather than the party (a merchant, say). A line or cast entry may name one by id; they are always present at their own event. |  |
| `fights` | RawEventFight[] (below) | `[]` | Fights this event can start, each named by a fight effect in some outcome. Its result (every enemy down, the round limit reached, or the party down) picks onDefeated, onSurvived or onFell, which apply like any outcome. |  |
| `shelves` | RawEventShelf[] (below) | `[]` | Merchant shelves this event can open (a shelf effect) or hand over (a takeShelf effect). A shelf is the room shop's own roll and buying on a stock that belongs to the event, not the node: rolled once per run, kept across Walk on and later visits, ended by finish. |  |

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

## level_curve.json -- `RawLevelCurveEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `level` | int | `0` | The character level this row is the cost to ENTER; the table runs from 2 to RewardTrack.MaxLevel with no gaps. |  |
| `cost` | int | `0` | Experience needed to go from the previous level to this one; must be 58-10153 and never lower than the row before it. |  |

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
| `effect` | string | `""` | Which RelicEffect this grants, matched case-insensitively; empty resolves to None. | None, DualWield, MagicalShield, Bloodlust, ChargingCrystal, BallerinasSlippers, TinFoilPipe, ToothedNecklace, BountyHunterContract, SwordInABox, LuckyDeck, DrownedLantern, FirstRune, SaltLedger, LongCount, MagicMarker, JarOfBearUrine, WorldEndersCrown, CursedIdol, AmassingStar, RampagingBullsHorn, IceFingernail, LoadedDice, MonkeyKingsScepter, SparringSaber, SparringBuckler, EssenceSiphon, DisgruntledLackey, InconspicuousKey, DancersAnklet, BerserkersVest, PhoenixEgg, Kinship, BellwethersBell, TollOfTheFlock |
| `iconPath` | string | `""` | Editor-time path to this relic's icon under Assets/_Project/Art/Items/Relics/Processed/; empty falls back to a flat accent-coloured circle. |  |
| `rarity` | string | `""` | How rare the offer is, one of RelicRarity's names; empty means Common. | Common, Uncommon, Rare, UltraRare, Mythic, Godlike |
| `unlockedBy` | string | `""` | The achievement id that must be earned before this relic can be offered; empty means available from the first run. |  |
| `modifiers` | RawRelicModifier[] (below) | `[]` | Numeric stat changes this relic grants; see RawRelicModifier. |  |
| `requiresConvergenceAbility` | bool | `false` | Whether this relic is only ever offered to a party that already has a convergence/ultimate ability. |  |
| `bearer` | string | `""` | Optional character id; when set, only that character gets this relic's effect. Refused together with modifiers, which apply party-wide. |  |
| `draftable` | bool | `true` | Whether this relic can be offered in a relic draft or the shop's relic shelf; false for relics granted another way, such as by an event. |  |
| `vfx` | SpellPresentation (below) | (zero -- see SpellPresentation) | Optional presentation played when this relic's effect fires on its own beat; see SpellPresentation. Omitted plays nothing. |  |

## reward_tracks.json -- `RawRewardTrackEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `characterId` | string | `""` | The character this track belongs to; matches a characters.json id. |  |
| `levels` | RawTrackLevel[] (below) | `[]` | One entry per level from 2 to RewardTrack.MaxLevel, in any order; see RawTrackLevel. |  |

## skills.json -- `RawSkillEntry`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | (none -- required) | Stable identifier; written into save files and never renamed once used. |  |
| `displayName` | string | (none -- required) | The name shown on the skill's own button. |  |
| `description` | string | `""` | Flavor text shown to the player; read by no formula. |  |
| `iconPath` | string | `""` | Editor-time path to this skill's icon (Assets/_Project/Art/...); empty means no art, and the slot hides rather than showing a placeholder. Used today only by bookOnly skills, whose spell-book art carries the glyph baked in. |  |
| `characterId` | string | `""` | The character this skill belongs to; required so it is never offered to everyone. |  |
| `unlockLevel` | int | `-1` | The character level this skill becomes available at; see notes for the bookOnly exception. |  |
| `effect` | string | `""` | Which SkillEffect this casts, matched case-insensitively. | DamageSingle, DamageAll, HealSelf, HealParty, RestorePartyMana, Provoke, Transform, Ward, Shatter, BuffParty, GiftMana, GiftFury, GiftHaste, Summon, HealSingle, Reclaim, Hasten, SwapAllies, Afflict, Enthrall |
| `targeting` | string | `""` | Which SkillTargeting this hits; defaults to whatever the effect implies. | SingleEnemy, AllEnemies, Self, Party, SingleAlly |
| `manaCost` | int | `-1` | Mana spent to cast; a skill must cost this and/or resourceCost. |  |
| `resourceCost` | int | `-1` | How much of the owner's signature resource a cast consumes. |  |
| `spendsAllResource` | bool | `false` | Whether the cast takes the caster's entire signature resource instead of resourceCost. |  |
| `resourceSpendCap` | int | `0` | A ceiling on how much a spendsAllResource cast takes; 0 means no ceiling. Meaningless without spendsAllResource. |  |
| `spendsAllPrimary` | bool | `false` | Whether the cast takes the caster's entire PRIMARY pool (Fury, mana) instead of manaCost, with manaCost as the minimum. |  |
| `percentOfMaxHealthPerPoint` | int | `0` | Percent of the caster's own max health added to a heal per point of the pool actually spent; 0 means none. |  |
| `percentOfCasterMaxHealth` | int | `0` | Percent of the caster's own max health added to a Ward's shield pool, flat; 0 means none. Ward only. |  |
| `wardTurns` | int | `0` | How many of the wearer's own turns a Ward stands before expiring; 0 means the default of one. |  |
| `freeAction` | bool | `false` | Whether casting this does not end the caster's turn; at most one free action per turn. |  |
| `power` | int | `-1` | Added per point of resource actually spent when casting. |  |
| `flatAmount` | int | `-1` | A flat contribution before scaling: the whole amount for a heal, an offset on top of Attack for damage. |  |
| `ignoresDefense` | bool | `false` | Whether this skill's damage skips the target's Defense entirely. |  |
| `damageInstances` | RawDamageInstance[] (below) | `[]` | Fixed, typed damage packets that replace the Attack/power formula entirely; each is checked against weakness and resistance on its own. |  |
| `cooldownTurns` | int | `0` | How many of the caster's own turns must pass before this skill can be cast again; 0 means no cooldown. |  |
| `playerSelectable` | bool | `true` | Whether a player ever picks this from a menu, as against a monster-only skill drawn by weighted chance. |  |
| `vfx` | SpellPresentation (below) | (zero -- see SpellPresentation) | How the skill looks and sounds when it resolves; see SpellPresentation. |  |
| `appliesStatus` | string | `""` | Which StatusEffectType this skill applies on landing, or empty for none. | Poison, Regen, Protect, Vulnerable, Stun, Shielded, Provoked, Empowered, Chilled, Rooted, Marked, Feared, Burn, Thorned |
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
| `physicalMove` | bool | `false` | Whether this action is a physical move (a swing, charge or lunge) that Rooted forbids; required on every damage row. |  |
| `reachSlots` | int[] | `[]` | Which 1-based positions in the enemy line this SingleEnemy skill may target; empty means anywhere. |  |
| `elements` | RawElementChoice[] (below) | `[]` | Elements the player chooses between before targeting; each retypes every authored damage packet. |  |
| `poolTiers` | RawPoolTier[] (below) | `[]` | Ascending fractions of the owner's primary pool this skill can spend for extra damage; the highest tier the caster can afford fires automatically. |  |
| `placeholder` | bool | `false` | Whether this skill's design has not been written yet; never player-selectable and never drawn by a monster, and may author no effect fields at all. |  |
| `placeholderNote` | string | `""` | Required together with placeholder: why this skill exists undesigned. |  |
| `healthCostPercent` | int | `0` | Percent of the caster's own max health paid as a cost, alongside manaCost; 0 means none. Rounds up. |  |
| `requiresStatus` | string | `""` | A StatusEffectType the target must already carry, or the cast is refused before anything is paid; empty means no requirement. |  |
| `consumesStatus` | string | `""` | A StatusEffectType a landed hit consumes off the target; required together with damageInstancesIfConsumed. |  |
| `damageInstancesIfConsumed` | RawDamageInstance[] (below) | `[]` | The packet list used instead of damageInstances when the target carries consumesStatus; required together with consumesStatus. |  |
| `detonationPercent` | int | `0` | The premium percent a Reclaim effect's detonation is marked up by; required and positive on a Reclaim row, meaningless elsewhere. |  |
| `detonationSplit` | string[] | `[]` | The ordered DamageType names a Reclaim effect's consumed total is split across, odd point to the first; required together with detonationPercent. |  |
| `advanceSlots` | int | `0` | How many places earlier in the turn queue this skill moves its target; required and positive on a Hasten row, meaningless elsewhere. |  |

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

### `EventRoundOverlay`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `path` | string | `""` | The overlay image, Assets-relative with its extension, baked at scene build; filed in this event's own Assets/_Project/Art/Events/<event_id>/. Empty means no overlay. |  |
| `fromScale` | float | `1` | The overlay's scale at the first round; above 0. |  |
| `toScale` | float | `1` | The overlay's scale at the last round of the limit; above 0. |  |
| `pivotX` | float | `0.5` | The point of the overlay image that stays put while it scales, as a fraction of the image's width from its left edge; 0-1, default 0.5. |  |
| `pivotY` | float | `0.5` | The same point, as a fraction of the image's height from its BOTTOM edge; 0-1, default 0.5. |  |
| `anchorX` | float | `0.5` | Where on the fight frame that point sits, as a fraction of the frame's width from its left edge; 0-1, default 0.5. The backdrop fills the frame, so a point on the painting is a fraction of the frame. |  |
| `anchorY` | float | `0.5` | Where on the fight frame that point sits, as a fraction of the frame's height from its BOTTOM edge; 0-1, default 0.5. |  |
| `tint` | string | `""` | A colour token '#RRGGBB' or '#RRGGBBAA' the overlay is multiplied by; its alpha is how much of the backdrop shows through. Empty draws the image as painted. Refused without a path. |  |

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

### `RawEnemyRally`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `attackPercentPerStack` | int | `0` | Attack percent each stack adds (8 = +8%); above 0 when maxStacks is set. |  |
| `maxStacks` | int | `0` | The most stacks the rally reaches; at least 1 when attackPercentPerStack is set. |  |

### `RawEventCastMember`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `character` | string | `""` | The character id (characters.json) or event speaker id (the event's speakers[]) this row places; must speak on this page. |  |
| `side` | string | `""` | Which screen edge the bust stands against: left or right (case-insensitive). A right-side bust is mirrored so it faces inward. | Left, Right |

### `RawEventChoice`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `text` | string | `""` | The choice's own button text; capped at EventEntryResolver.MaxChoiceTextLength characters. |  |
| `requires` | RawEventRequirement[] (below) | `[]` | Requirements gating this choice; a choice with none is always selectable. |  |
| `hiddenUntilMet` | bool | `false` | When true, this choice is hidden entirely (not shown locked) until its requirements pass. |  |
| `effects` | RawEventEffect[] (below) | `[]` | Effects applied immediately when this choice is picked, before an outcome is chosen. A gold spend here implies its own gold requirement -- do not author one by hand. |  |
| `outcomes` | RawEventOutcome[] (below) | `[]` | Ordered outcomes; the first whose requirements pass wins. |  |

### `RawEventEffect`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `kind` | string | `""` | Which EventEffectKind this is: gold, healPercent, damagePercent, exp, item, counter, relic, princesFavor, fillSpecialPool, fight, finish, shelf or takeShelf. | Gold, HealPercent, DamagePercent, Exp, Item, Counter, Relic, PrincesFavor, FillSpecialPool, Fight, Finish, Shelf, TakeShelf |
| `amount` | int | `0` | The amount this effect changes: gold (+ gain/- spend), heal/damage percent (1-100), exp, a counter delta, or princesFavor's run-long bonus (> 0). fillSpecialPool takes exactly 1; relic ignores it; fight, finish and shelf refuse it; takeShelf reads it as how many unsold cards are lost first (0 or more). |  |
| `item` | string | `""` | The item id granted; required by item. |  |
| `counter` | string | `""` | The counter id this effect changes; required by counter. |  |
| `character` | string | `""` | Optional on healPercent and exp: that one character id instead of the whole squad. healPercent never revives (a member at 0 HP stays at 0); exp goes to that member alone rather than being split. Refused on any other kind. |  |
| `relic` | string | `""` | The relic id (relics.json) added to the run; required by relic. Already held is a no-op. |  |
| `fight` | string | `""` | fight only, required: the id of one of this event's own fights[] to start. Allowed only in an outcome's effects, at most one per outcome, and that outcome's goTo must be empty -- the fight's result outcome decides where the event goes next. |  |
| `shelf` | string | `""` | shelf and takeShelf only, required: the id of one of this event's own shelves[]. shelf opens it (the stock is rolled on the first shelf or takeShelf of the run and kept until finish); takeShelf hands the party its unsold cards, amount of them lost first, fakes staying fake. |  |
| `reveal` | bool | `false` | shelf only: when true this visit marks the shelf's fakes, and the mark stays with the stock for every later visit and reload. Refused on any other kind. |  |

### `RawEventFight`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | `""` | Stable id within this event, named by a fight effect. |  |
| `enemies` | string[] | `[]` | Enemy ids (enemies.json, active) in stage order; at least one, and their slotSpans may not add up past the stage's three slots. |  |
| `elite` | bool | `false` | True fights at the elite class (payout multiplier and default backdrop); false at the normal class. |  |
| `party` | string[] | `[]` | Character ids who fight, overriding the normal fieldable squad; empty means the normal squad. Members not listed sit out untouched. The choice starting it should require one of them alive. |  |
| `surviveRounds` | int | `0` | 0 means no round limit. Above 0, reaching round surviveRounds + 1 with someone standing ends the fight as Survived; onSurvived is then required. |  |
| `roundLabel` | string | `""` | The round counter's word ('Toll'), capped at EventEntryResolver.MaxRoundLabelLength characters. Only read with a round limit, so refused without one; empty shows the default. |  |
| `onLoss` | string | `""` | What losing does, one of EventFightLoss: endRun (empty; the run ends, as in a room) or wake (the run continues and the fallen fighters stand at 1 HP). onFell is refused on endRun and required on wake. | EndRun, Wake |
| `pays` | bool | `true` | True pays out like a room fight (gold, spell drop, the Reckoning); false pays nothing and ends on Continue. |  |
| `backdrop` | string | `""` | The fight's backdrop, Assets-relative with its extension, baked at scene build. Empty uses the class's own. Filed under Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/<event_id>/. |  |
| `roundSfx` | string | `""` | Resources-relative sound (no extension) played as each round starts. Only read with a round limit, so refused without one. |  |
| `ambience` | string | `""` | Resources-relative sound (no extension) looped for the whole fight; empty plays none. |  |
| `roundOverlay` | EventRoundOverlay (below) | (zero -- see EventRoundOverlay) | An image laid over the stage that steps from fromScale to toScale across the round limit; see EventRoundOverlay. Only read with a round limit, so a path without one is refused. |  |
| `onDefeated` | RawEventOutcome (below) | (zero -- see RawEventOutcome) | The outcome when every enemy is down; required. An outcome row without requires (refused here), whose goTo is a page or Leave. |  |
| `onSurvived` | RawEventOutcome (below) | (zero -- see RawEventOutcome) | The outcome when the round limit is reached with someone standing; required when surviveRounds > 0 and refused otherwise. |  |
| `onFell` | RawEventOutcome (below) | (zero -- see RawEventOutcome) | The outcome when the fighters fall; required on wake, refused on endRun (the run is over). |  |

### `RawEventLine`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `speaker` | string | `""` | Who says this: a character id (characters.json), one of this event's own speakers[] ids, or the literal 'narration' (case-insensitive) for an unvoiced line with no bust and no name plate. |  |
| `expression` | string | `""` | The speaker's face for this line. A party character takes a DialogueExpression, empty meaning neutral; an event speaker takes one of its own declared expressions, empty meaning the first it declares. Refused on narration. | Neutral, Happy, Annoyed, Nervous, Sad, Surprised, Entranced |
| `text` | string | `""` | The line's text; at most EventEntryResolver.MaxLineLength characters, tags included. The only markup allowed is <i>...</i> (lowercase, balanced, not nested); any other tag, <b> included, is refused. |  |

### `RawEventOutcome`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `requires` | RawEventRequirement[] (below) | `[]` | Requirements gating this outcome; the last outcome in a choice must have none, so a choice can never fall through with nothing to show. |  |
| `effects` | RawEventEffect[] (below) | `[]` | Effects applied when this outcome is chosen, in addition to the choice's own effects. |  |
| `result` | string | `""` | The result text shown after this outcome is chosen, in the body's place; capped at EventEntryResolver.MaxBodyLength characters like the body, or at MaxLineLength when it plays on the dialogue stage (the choice's page or the goTo page has lines). |  |
| `goTo` | string | `""` | The next page's id, or the literal 'Leave' (case-insensitive) to close the event. Must be empty on an outcome that carries a fight effect, and only there: the fight's own onDefeated/onSurvived/onFell outcome goes on from it. |  |

### `RawEventPage`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | `""` | Stable id for this page within its event; targeted by an outcome's goTo. |  |
| `artPath` | string | `""` | This page's art, Assets-relative with its extension (Assets/_Project/Art/Events/<event_id>/<page_id>.png; the build refuses art under Art/Events/ that is not in its own event's folder): baked into the Map scene at build time the way item iconPath is, so a new file needs a scene rebuild. Empty, or a file that is not there, hides the image and keeps the frame. Commission at the size docs/EVENTS.md gives. |  |
| `title` | string | `""` | The page's title, shown above the body; capped at EventEntryResolver.MaxTitleLength characters. |  |
| `body` | string | `""` | The page's body text; capped at EventEntryResolver.MaxBodyLength characters. |  |
| `choices` | RawEventChoice[] (below) | `[]` | Up to 4 choices offered on this page. |  |
| `backdrop` | string | `""` | Optional full-bleed backdrop for this page, Assets-relative with its extension, baked at scene build like artPath. Empty inherits the event's backdrop. Must be filed under Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/<event_id>/. |  |
| `cast` | RawEventCastMember[] (below) | `[]` | Optional side overrides for this page's speakers. A speaker not listed takes a side by first appearance on the page: first distinct speaker left, second right, third left, alternating. Every entry must speak on this page, and a character may be listed once. |  |
| `lines` | RawEventLine[] (below) | `[]` | Dialogue lines played before the choices, in order; at most EventEntryResolver.MaxLinesPerPage. Every non-narration speaker must be guaranteed in the party at this page by requirements on the way in (see docs/EVENTS.md, Dialogue lines). |  |

### `RawEventRequirement`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `kind` | string | `""` | Which EventRequirementKind this is: inParty, memberLevel, ability, counter or gold. | InParty, MemberLevel, Ability, Counter, Gold |
| `character` | string | `""` | The character id this requirement names; required by inParty, an optional narrowing for memberLevel/ability. |  |
| `ability` | string | `""` | Which AbilityScore this checks; required by ability. |  |
| `min` | int | `-1` | The minimum value required; -1 means omitted. Required by memberLevel/ability/gold; optional for counter. |  |
| `max` | int | `-1` | The maximum value allowed; -1 means omitted. Only counter reads this. |  |
| `counter` | string | `""` | The counter id this requirement reads; required by counter. |  |
| `reason` | string | `""` | Optional caption a locked choice shows for this row, replacing the generated one for any kind. Empty means generated (a counter's generated caption is 'Not yet' / 'No longer', never its id). On a choice row it is capped at EventEntryResolver.MaxLockReasonLength characters; event- and outcome-level rows never show it. |  |
| `alive` | bool | `false` | inParty only: when true the character must also be standing (run health above 0); a downed member fails with the caption 'Requires <name> standing'. Refused on any other kind. |  |

### `RawEventShelf`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | `""` | Stable id within this event, named by a shelf or takeShelf effect. |  |
| `priceFactorPercent` | int | `100` | Every card's price as a percent of the room shop's price for the same card, rounded half away from zero, never below 1 gold. 1-100. |  |
| `fakeShare` | int | `0` | How many cards are fake: max(1, round(cards / fakeShare)), so 3 is a third (1, 1, 2, 2 of 3, 4, 5, 6 cards). 0 means none. A fake looks and sells like the genuine card; fake gear falls apart after 3 fights worn, a fake consumable does nothing when used. |  |
| `sections` | string[] | `[]` | Which room-shop shelves this stock rolls, each at most once: 'gear' (the room shop's gear roll: same candidates, tier band and affixes). Books and relics are refused -- they carry no item instance, so a fake could not apply. |  |
| `consumableCount` | int | `0` | How many consumable cards (items.json consumables, drawn without repeats) sit beside the sections' cards; 0 up to ShopStock.ConsumableCount (3). |  |
| `title` | string | `""` | The shelf screen's title in place of the room shop's SHOP, capped like a page title (EventEntryResolver.MaxTitleLength). Empty shows the keeper's name, else SHOP. |  |
| `keeper` | string | `""` | One of this event's own speakers: who keeps the shelf. The shelf screen shows their bust (first declared expression), name and epithet in the panel the room shop gives its relics. Empty leaves that panel with the title and the fakes note only. |  |

### `RawEventSpeaker`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `id` | string | `""` | Stable id within this event, named by a line's speaker or a cast entry. Refused when it is a character id in characters.json or 'narration'. |  |
| `name` | string | `""` | The name plate's name; required. |  |
| `epithet` | string | `""` | The name plate's second line; optional, capped at CharacterEntryResolver.MaxEpithetLength characters like a character's. |  |
| `bustPath` | string | `""` | Resources-relative folder of this speaker's busts, one PNG per declared expression (<bustPath>/<expression>), the same shape as a character's dialogueBustPath. Empty shows the name plate and text with no bust. |  |
| `expressions` | string[] | `[]` | The expressions this speaker has, lowercase letters, digits and underscores (they are bust file names). A line naming one not listed is refused; a line with none takes the first. Empty declares just 'neutral'. |  |

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

### `RawTrackLevel`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `level` | int | `0` | The level this entry pays, from 2 to RewardTrack.MaxLevel; every one of those levels must carry exactly one entry and no entry may name a level outside that range. |  |
| `reward` | string | `""` | Which TrackReward this level grants, matched case-insensitively against the enum member name. | None, StatPoint, MaxHealth, Respec, SecondLife, SignatureCapacity, SignatureGainPerTurn, SignatureGainOnDamageTaken, ElementalDamagePercent, MaxMana, ManaRegen, UnlockSkill, FuryGainOnAttack, FuryStartOfFight, SpellCostDelta, SkillCostDelta, SkillFlatDelta, SignatureAbsorbPerPoint, Identity, SkillPowerDelta, SpellDamagePercent |
| `amount` | int | `0` | The reward's magnitude -- a count for a grant (a stat point, max health), or an unlock's own parameter where it has one (SecondLife's charge count); 0 for an unlock with none (Respec). |  |
| `against` | string | `""` | The DamageType this reward is typed against, matched case-insensitively; only ElementalDamagePercent reads this, empty otherwise. | Physical, Fire, Ice, Nature, Poison, Arcane, Earth, Water, Wind, Lightning, Void |
| `skillId` | string | `""` | The skill id this reward unlocks (UnlockSkill), or the one named skill a SkillCostDelta/SkillFlatDelta/SkillPowerDelta entry adjusts; empty otherwise. |  |
| `resource` | string | `""` | Which TrackResourceTarget a SkillCostDelta entry discounts, matched case-insensitively; only SkillCostDelta reads this, empty otherwise. | Mana, Signature |
| `identityKind` | string | `""` | Which TrackIdentityKind an Identity entry carries, matched case-insensitively; only Identity reads this, empty otherwise. | Title, PlateRim, PortraitFrame, PlateEmboss, VictoryPose, Mastery |
| `value` | string | `""` | The Identity entry's payload -- a title string for Title, or 'silver'/'gold' for PlateRim/PlateEmboss; unused by PortraitFrame/VictoryPose/Mastery and by every non-Identity reward. |  |

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
| `place` | string | `""` | Where it belongs: caster, caster-centre, target, target-centre, formation, sky (in the air halfway from the caster to one struck target, above both), or 'layer:<id>' to ride another layer. No default -- a blank is refused. | Caster, CasterCentre, Target, TargetCentre, Formation, Layer, Sky |
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
| `fit` | string | `""` | What `size` scales against: none (the authored number, verbatim) or target (multiplied by the struck target's visible body on stage -- the larger of its opaque width and height over TargetBody.ReferenceExtent, a front-rank Giant Rat; an emitter scales its particles, speeds and source offset by the same factor). Blank means none. Legal only where a single struck target is resolved: place target, target-centre or sky, or a layer that travels. | None, Target |
| `facing` | string | `""` | Mirroring: auto (take the cast's facing), none (never mirror) or reverse. Blank means auto. | Auto, None, Reverse |
| `sort` | string | `""` | Draw band: ground (behind the racks) or effects (over the HUD, under the damage numbers). Blank means effects. | Ground, Effects |
| `align` | string | `""` | How a formation-placed layer lies on the rank: level (axis-aligned, the default) or span (rotated along the line from the leftmost struck body to the rightmost). Refused on any other placement. | Level, Span |
| `punch` | float | `0` | Extra scale at the layer's opening instant, eased out to nothing over the first fifth of its lifetime; 0 means no punch. 0.2 opens it 20% oversized. |  |
| `glow` | float | `0` | How far above 1 this layer's brightest pixels are pushed so the Bloom override can see them; 0 means draw it flat, as every layer did before. 1.2 roughly doubles the hot core. |  |
| `impactX` | float | `-1` | Where the blow lands inside this layer's frames, as a fraction from the left edge; -1 means unauthored. |  |
| `impactY` | float | `-1` | Where the blow lands inside this layer's frames, as a fraction from the bottom edge; -1 means unauthored. |  |
| `aspect` | float | `0` | Width-over-height of the box; 0 means take the sheet's own frame aspect. |  |
| `travelSeconds` | float | `0` | Seconds this layer takes to cross from its anchor to the target, departure to arrival; 0 means it does not travel. |  |
| `travelDelay` | float | `0` | Seconds after this layer starts before its motion begins; the wind-up held at the caster. |  |
| `orient` | string | `""` | Projectile orientation: none (drawn as painted) or path (turned so artDegrees lies along the flight, with the impact point riding the line from launch to aim). Blank means none. Legal only on a layer that travels. | None, Path |
| `artDegrees` | float | `0` | The direction the drawing points as painted, in degrees counter-clockwise from +x (right); read only by orient 'path'. |  |
| `emitter` | SpellEmitter (below) | (zero -- see SpellEmitter) | Ballistic particle settings; inert unless render is 'emitter'. |  |

### `SpellPresentation`

| Field | Type | Default | Description | Values |
|---|---|---|---|---|
| `path` | string | `""` | Resources-relative folder of this spell's animation frames; empty means no visual. |  |
| `seconds` | float | `0.6` | How long the whole per-target animation takes, in seconds. |  |
| `impactFrame` | int | `3` | Which frame (1-based) the spell actually lands on. |  |
| `anchor` | string | `""` | Where the effect happens: a SpellAnchor name (Target, Caster, ...), parsed case-insensitively; unrecognised falls back to Target. | Target, Caster, TargetCentre, CasterCentre, Travel, TravelCentre |
| `size` | float | `380` | The square box the art is fitted into, in reference-frame units; 0 means the default size. |  |
| `fit` | string | `""` | What `size` scales against, as on a layer: none (verbatim) or target (the struck target's visible body over TargetBody.ReferenceExtent). Blank means none. Refused on a caster anchor, and beside layers. | None, Target |
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

