using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PrincesPalace.Domain.Content
{
    // Generates docs/CONTENT_SCHEMA.md from the Raw*Entry types themselves,
    // so the field list committed there cannot drift from what the
    // resolvers actually read -- the failure mode this replaces is exactly
    // the one the content-chain audit found: 82 of 176 fields undocumented
    // in the hand-written _readme strings, and five readme claims stale
    // against the code (enemies.json still naming six DamageTypes after
    // af1ef62 added five more, skills.json's effect list missing Transform
    // and Summon).
    //
    // ENGINE-FREE, BCL ONLY, same rule as everything else in Domain -- this
    // is what lets ContentSchemaTests (Tests/EditMode) run it under
    // `dotnet test` without booting Unity. See tools/content_schema.ps1 for
    // how the committed file gets regenerated.
    public static class ContentSchema
    {
        // Which JSON file under Assets/_Project/ContentData/ each Raw*Entry
        // type is authored into, in the order the tables are emitted --
        // alphabetical by filename, matching the ContentData folder listing.
        // ADD A ROW HERE when a new content type is added; there is nothing
        // that reflects this mapping automatically; a new Raw*Entry with no
        // row here would silently print no table for its file.
        private static readonly (string JsonFile, Type RawType)[] FileMap =
        {
            ("achievements.json", typeof(RawAchievementEntry)),
            ("characters.json", typeof(RawCharacterEntry)),
            ("enemies.json", typeof(RawEnemyEntry)),
            ("items.json", typeof(RawItemEntry)),
            ("itemsets.json", typeof(RawItemSetEntry)),
            ("level_curve.json", typeof(RawLevelCurveEntry)),
            ("modifiers.json", typeof(RawModifierEntry)),
            ("pools.json", typeof(RawPoolEntry)),
            ("relics.json", typeof(RawRelicEntry)),
            ("reward_tracks.json", typeof(RawRewardTrackEntry)),
            ("skills.json", typeof(RawSkillEntry)),
            ("spells.json", typeof(RawSpellTierEntry)),
            ("talents.json", typeof(RawTalentEntry)),
            ("upgrades.json", typeof(RawUpgradeEntry)),
            ("weapons.json", typeof(RawWeaponEntry)),
        };

        // Which enum a field's authored STRING is checked against by its
        // resolver. JsonUtility can only write an enum as its ordinal (see
        // RawSkillEntry.approach's own comment for why that is dangerous),
        // so every field below is a plain string on the Raw type and an enum
        // only after a resolver has parsed it -- reflection over the Raw
        // type alone cannot recover that relationship, so this table states
        // it by hand. What it does NOT hand-state is the valid VALUES: those
        // are read live off each enum with Enum.GetNames, so a member added
        // to (say) DamageType shows up here on the next regenerate with
        // nothing in this file touched.
        private static readonly Dictionary<(Type Owner, string Field), Type> EnumBackedFields =
            new Dictionary<(Type, string), Type>
            {
                [(typeof(RawSkillEntry), nameof(RawSkillEntry.effect))] = typeof(Combat.SkillEffect),
                [(typeof(RawSkillEntry), nameof(RawSkillEntry.targeting))] = typeof(Combat.SkillTargeting),
                [(typeof(RawSkillEntry), nameof(RawSkillEntry.scalingAxis))] = typeof(Combat.ScalingAxis),
                [(typeof(RawSkillEntry), nameof(RawSkillEntry.appliesStatus))] = typeof(Combat.StatusEffectType),
                [(typeof(RawSkillEntry), nameof(RawSkillEntry.approach))] = typeof(Combat.Session.StageApproach),
                [(typeof(RawDamageInstance), nameof(RawDamageInstance.type))] = typeof(Stats.DamageType),
                [(typeof(RawEnemyEntry), nameof(RawEnemyEntry.attackType))] = typeof(Stats.DamageType),
                [(typeof(RawEnemyEntry), nameof(RawEnemyEntry.weakness))] = typeof(Stats.DamageType),
                [(typeof(RawEnemyEntry), nameof(RawEnemyEntry.resistance))] = typeof(Stats.DamageType),
                [(typeof(RawEnemyEntry), nameof(RawEnemyEntry.appliesStatus))] = typeof(Combat.StatusEffectType),
                [(typeof(RawEnemyEntry), nameof(RawEnemyEntry.facing))] = typeof(Stage.SpriteFacing),
                [(typeof(RawEnemyEntry), nameof(RawEnemyEntry.attackApproach))] = typeof(Combat.Session.StageApproach),
                [(typeof(RawCharacterEntry), nameof(RawCharacterEntry.role))] = typeof(CharacterRole),
                [(typeof(RawCharacterEntry), nameof(RawCharacterEntry.attackType))] = typeof(Stats.DamageType),
                [(typeof(RawCharacterEntry), nameof(RawCharacterEntry.battleSpriteFacing))] = typeof(Stage.SpriteFacing),
                [(typeof(RawCharacterEntry), nameof(RawCharacterEntry.plateTheme))] = typeof(UiKit.ButtonTheme),
                [(typeof(RawPoolEntry), nameof(RawPoolEntry.capacityRule))] = typeof(PoolCapacityRule),
                [(typeof(RawPoolEntry), nameof(RawPoolEntry.startRule))] = typeof(PoolStartRule),
                [(typeof(RawPoolEntry), nameof(RawPoolEntry.decayUnless))] = typeof(PoolDecayTrigger),
                [(typeof(RawItemEntry), nameof(RawItemEntry.kind))] = typeof(ResolvedItemKind),
                [(typeof(RawItemEntry), nameof(RawItemEntry.effect))] = typeof(ResolvedItemEffect),
                [(typeof(RawRelicEntry), nameof(RawRelicEntry.effect))] = typeof(RelicEffect),
                [(typeof(RawRelicEntry), nameof(RawRelicEntry.rarity))] = typeof(RelicRarity),
                [(typeof(RawRelicModifier), nameof(RawRelicModifier.type))] = typeof(RelicModifierType),
                [(typeof(RawRelicModifier), nameof(RawRelicModifier.damageType))] = typeof(Stats.DamageType),
                [(typeof(RawModifierEffect), nameof(RawModifierEffect.type))] = typeof(Combat.ModifierEffectType),
                [(typeof(RawModifierEffect), nameof(RawModifierEffect.damageType))] = typeof(Stats.DamageType),
                [(typeof(RawTalentEffect), nameof(RawTalentEffect.type))] = typeof(Combat.TalentEffectType),
                [(typeof(RawAchievementEntry), nameof(RawAchievementEntry.condition))] = typeof(AchievementCondition),
                [(typeof(RawTrackLevel), nameof(RawTrackLevel.reward))] = typeof(Progression.TrackReward),
                [(typeof(RawTrackLevel), nameof(RawTrackLevel.against))] = typeof(Stats.DamageType),
                [(typeof(RawTrackLevel), nameof(RawTrackLevel.resource))] = typeof(Progression.TrackResourceTarget),
                [(typeof(RawTrackLevel), nameof(RawTrackLevel.identityKind))] = typeof(Progression.TrackIdentityKind),

                // A presentation's own word, and the seven a layer is authored
                // in. `place` is here for its CLOSED half only -- its open
                // 'layer:<id>' form has no enum member that could spell it and
                // lives in the field's own [ContentDoc] instead, because this
                // column is Enum.GetNames and cannot document an open
                // vocabulary.
                [(typeof(SpellPresentation), nameof(SpellPresentation.anchor))] = typeof(SpellAnchor),
                [(typeof(SpellLayer), nameof(SpellLayer.render))] = typeof(SpellRender),
                [(typeof(SpellLayer), nameof(SpellLayer.place))] = typeof(SpellPlace),
                [(typeof(SpellLayer), nameof(SpellLayer.at))] = typeof(SpellCue),
                [(typeof(SpellLayer), nameof(SpellLayer.until))] = typeof(SpellEnd),
                [(typeof(SpellLayer), nameof(SpellLayer.facing))] = typeof(SpellFacing),
                [(typeof(SpellLayer), nameof(SpellLayer.sort))] = typeof(SpellSort),
                [(typeof(SpellLayer), nameof(SpellLayer.align))] = typeof(SpellAlign),
            };

        private static readonly StringComparer NameOrder = StringComparer.Ordinal;

        public static string Generate()
        {
            var nested = DiscoverNestedTypes();

            var sb = new StringBuilder();
            sb.Append("# Content schema\n\n");
            sb.Append("GENERATED FILE -- do not hand-edit. Produced by ContentSchema.Generate() ");
            sb.Append("(Assets/_Project/Scripts/Domain/Content/ContentSchema.cs) from the Raw*Entry ");
            sb.Append("types by reflection, and checked byte-for-byte by ContentSchemaTests ");
            sb.Append("(Tests/EditMode/Content/ContentSchemaTests.cs). Run `tools/content_schema.ps1` after ");
            sb.Append("changing a Raw*Entry field or its [ContentDoc], then commit this file.\n\n");
            sb.Append("Each table is one JSON file under Assets/_Project/ContentData/. `Default` is ");
            sb.Append("the field's own C# initialiser -- the value JsonUtility leaves in place when an ");
            sb.Append("author omits the key -- not a claim about what the resolver does with it; a ");
            sb.Append("resolver-specific rule (a sentinel that means \"derive it\", a both-fields-or-");
            sb.Append("neither pairing, a value that is only legal for one `effect`) stays in the ");
            sb.Append("field's own `_readme` prose, which this file does not replace. `Values` is only ");
            sb.Append("populated for a field a resolver parses against an enum, and is read live off ");
            sb.Append("that enum, so it cannot go stale the way hand-typed prose can.\n\n");

            foreach (var (jsonFile, rawType) in FileMap)
            {
                sb.Append("## ").Append(jsonFile).Append(" -- `").Append(rawType.Name).Append("`\n\n");
                AppendTable(sb, rawType, nested);
                sb.Append('\n');
            }

            if (nested.Count > 0)
            {
                sb.Append("## Shared value types\n\n");
                sb.Append("Referenced from a field above (an array element or a nested block, such as ");
                sb.Append("`vfx`); documented once here rather than once per use.\n\n");

                foreach (Type t in nested)
                {
                    sb.Append("### `").Append(t.Name).Append("`\n\n");
                    AppendTable(sb, t, nested);
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        // BFS outward from the 11 root types over every field whose type (or
        // array element type) is itself a [Serializable] class in this same
        // namespace -- SpellPresentation, RawDamageInstance, RawEnemyAbility,
        // RawRelicModifier, RawTalentEffect, RawModifierEffect, RawSetPiece
        // all get found this way, with nothing here naming them by hand. A
        // shared, general-purpose type from another namespace (StatBlock,
        // AbilityScoreBlock, Combat.TransformGrant) is deliberately NOT
        // expanded -- it is documented elsewhere, and expanding it here would
        // make this generator responsible for a struct it does not own.
        private static SortedSet<Type> DiscoverNestedTypes()
        {
            var result = new SortedSet<Type>(Comparer<Type>.Create((a, b) => NameOrder.Compare(a.Name, b.Name)));
            var visited = new HashSet<Type>(FileMap.Select(m => m.RawType));
            var queue = new Queue<Type>(FileMap.Select(m => m.RawType));

            while (queue.Count > 0)
            {
                Type t = queue.Dequeue();
                foreach (FieldInfo f in InstanceFields(t))
                {
                    Type elem = ElementType(f.FieldType);
                    if (!elem.IsClass || elem.Namespace != "PrincesPalace.Domain.Content") continue;
                    if (!visited.Add(elem)) continue;

                    result.Add(elem);
                    queue.Enqueue(elem);
                }
            }

            return result;
        }

        private static void AppendTable(StringBuilder sb, Type type, ISet<Type> nestedTypes)
        {
            sb.Append("| Field | Type | Default | Description | Values |\n");
            sb.Append("|---|---|---|---|---|\n");

            object instance = Activator.CreateInstance(type);
            foreach (FieldInfo f in InstanceFields(type))
            {
                string typeName = FriendlyTypeName(f.FieldType, nestedTypes);
                string def = FormatDefault(f.GetValue(instance));
                var doc = f.GetCustomAttribute<ContentDocAttribute>();
                string desc = Escape(doc != null ? doc.Description : "");
                string values = "";
                if (EnumBackedFields.TryGetValue((type, f.Name), out Type enumType))
                {
                    values = string.Join(", ", Enum.GetNames(enumType));
                }

                sb.Append("| `").Append(f.Name).Append("` | ").Append(typeName).Append(" | ")
                  .Append(def).Append(" | ").Append(desc).Append(" | ").Append(values).Append(" |\n");
            }
        }

        // [NonSerialized] IS NOT CONTENT, so it is not in the schema and does
        // not need a [ContentDoc]. A field the serialiser never fills cannot
        // be written in a JSON file, so documenting it would describe a knob
        // an author does not have -- RawSkillEntry.physicalMoveOmitted is a
        // parse-time stamp, not a row's field. The test that enforces the
        // [ContentDoc] rule reads this same helper, so the two cannot drift.
        public static IEnumerable<FieldInfo> InstanceFields(Type t) =>
            t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
             .Where(f => !f.IsNotSerialized);

        private static Type ElementType(Type t) => t.IsArray ? t.GetElementType() : t;

        private static string FriendlyTypeName(Type t, ISet<Type> nestedTypes)
        {
            bool isArray = t.IsArray;
            Type elem = ElementType(t);
            string baseName = BuiltinName(elem) ?? elem.Name;
            string full = isArray ? baseName + "[]" : baseName;
            if (nestedTypes.Contains(elem)) full += " (below)";
            return full;
        }

        private static string BuiltinName(Type t)
        {
            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            return null;
        }

        // The field's own C# initialiser, read off a freshly constructed
        // instance -- the Raw types' initialisers ARE the defaults
        // (JsonUtility only overwrites a field the author actually wrote),
        // so this cannot drift from what the resolver sees on an omitted
        // field the way a hand-typed "-1" in prose could.
        private static string FormatDefault(object value)
        {
            switch (value)
            {
                case null:
                    return "(none -- required)";
                case string s:
                    return s.Length == 0 ? "`\"\"`" : "`\"" + Escape(s) + "\"`";
                case bool b:
                    return b ? "`true`" : "`false`";
                case float f:
                    return "`" + f.ToString(CultureInfo.InvariantCulture) + "`";
                case int i:
                    return "`" + i.ToString(CultureInfo.InvariantCulture) + "`";
                case Array arr:
                    return arr.Length == 0 ? "`[]`" : "(authored default)";
                default:
                    // A struct block (StatBlock, AbilityScoreBlock) or a
                    // Domain class from another namespace (TransformGrant):
                    // zero-valued by construction, and its own fields are
                    // documented where that type is defined, not here.
                    return "(zero -- see " + value.GetType().Name + ")";
            }
        }

        // Markdown table cells break on an unescaped pipe; nothing in this
        // project's content prose uses one today, but a future field
        // description quoting a "<a> | <b>" example should not silently
        // corrupt the table it lands in.
        private static string Escape(string s) => (s ?? "").Replace("|", "\\|");
    }
}
