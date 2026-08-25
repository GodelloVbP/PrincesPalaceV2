using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Turns the human-edited enemies.json into real game data, with clear,
    // collected-not-first-only error messages — this is the one place a
    // typo in a hand-written JSON file needs to fail loudly and specifically
    // rather than silently producing a broken or default-everything
    // monster. Engine-free and unit-tested so the validation rules
    // themselves are provable without needing ContentBuilder or a Unity
    // Editor session at all.
    public static class EnemyEntryResolver
    {
        // Every stat but maxHealth can be left blank and gets scaled off it
        // — chosen so "just say his health is 30" really is enough to get a
        // playable, roughly-balanced monster. Not tuned to reproduce any
        // specific existing enemy's numbers; a fresh monster is expected to
        // get its omitted stats adjusted by hand afterward if the default
        // profile doesn't fit, same as any other first draft.
        private const float AttackPerHealth = 1f / 3f;
        private const float DefensePerHealth = 1f / 12f;
        private const int DefaultSpeed = 8;
        private const float ExpPerHealth = 1.2f;
        private const float ExpPerAttack = 2f;
        private const float CurrencyPerExp = 0.6f;

        // Derived stagger capacity, same "just say his health is 30" spirit
        // as attack/defense above. Calibrated against BreakShield's own
        // depletion rates (1 per ordinary hit, 2 on a weakness match) so a
        // party WITHOUT the right element still breaks a boss in roughly a
        // dozen ordinary hits rather than needing dozens, while a party that
        // found the matchup roughly halves that. At this project's health
        // scale that lands the Dungeon Warden (600 HP) around 15 and the
        // weakest active monster, the Bog Witch (160 HP), around 4 — a real
        // but short fight either way, never a single hit and never a grind.
        private const float BreakShieldPerHealth = 1f / 40f;

        // However low maxHealth is authored, a stagger meter this thin would
        // break on the very first hit and read as broken content rather than
        // as a fast monster. Everything above roughly 80 HP already clears
        // this floor through the formula alone.
        private const int MinimumBreakShieldPoints = 2;

        public static bool TryResolveAll(IReadOnlyList<RawEnemyEntry> entries, out List<ResolvedEnemy> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedEnemy>();
            errors = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, out var single, out string error))
                {
                    resolved.Add(single);
                }
                else
                {
                    errors.Add(error);
                }
            }

            var duplicates = resolved
                .GroupBy(e => e.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (string duplicateId in duplicates)
            {
                errors.Add($"Duplicate enemy id '{duplicateId}' — every id must be unique.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawEnemyEntry raw, int index, int sortOrder, out ResolvedEnemy resolvedEnemy, out string error)
        {
            resolvedEnemy = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"enemies.json entry #{index + 1}" : $"enemy '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{label}: id is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required.";
                return false;
            }

            if (raw.maxHealth <= 0)
            {
                error = $"{label}: maxHealth must be a positive number (got {raw.maxHealth}). " +
                        "This is the one stat every monster must specify — everything else can be left out.";
                return false;
            }

            int attack = raw.attack >= 0 ? raw.attack : (int)MathF.Round(raw.maxHealth * AttackPerHealth);
            int defense = raw.defense >= 0 ? raw.defense : (int)MathF.Round(raw.maxHealth * DefensePerHealth);
            int speed = raw.speed >= 0 ? raw.speed : DefaultSpeed;
            int expReward = raw.expReward >= 0 ? raw.expReward : (int)MathF.Round(raw.maxHealth * ExpPerHealth + attack * ExpPerAttack);
            int currencyReward = raw.currencyReward >= 0 ? raw.currencyReward : (int)MathF.Round(expReward * CurrencyPerExp);

            // 0 is a real, authored value here (no stagger meter at all),
            // distinct from -1 meaning "omitted, derive it" — the same
            // distinction RawEnemyEntry's own comment makes.
            int breakShieldPoints = raw.breakShieldPoints >= 0
                ? raw.breakShieldPoints
                : Math.Max(MinimumBreakShieldPoints, (int)MathF.Round(raw.maxHealth * BreakShieldPerHealth));

            if (!TryResolveDamageTypes(raw.weakness, label, "weakness", out var weaknesses, out error))
            {
                return false;
            }

            if (!TryResolveDamageTypes(raw.resistance, label, "resistance", out var resistances, out error))
            {
                return false;
            }

            // DERIVATION IS STILL SINGLE-ELEMENT, and stays that way on purpose.
            // It exists so an unauthored monster is not neutral to everything,
            // not to invent a creature's elemental identity -- guessing two
            // weaknesses off an id hash would be twice as much fiction, and
            // twice as much for a real authoring pass to undo.
            bool weaknessWasBlank = string.IsNullOrWhiteSpace(raw.weakness);
            bool resistanceWasBlank = string.IsNullOrWhiteSpace(raw.resistance);
            if (weaknessWasBlank && resistanceWasBlank)
            {
                // Neither given: derive both from the id, distinct by construction.
                DeriveDamageTypePair(raw.id, out var derivedWeakness, out var derivedResistance);
                weaknesses = new List<DamageType> { derivedWeakness };
                resistances = new List<DamageType> { derivedResistance };
            }
            else if (weaknessWasBlank)
            {
                // Only weakness omitted: derive one that isn't among the author's
                // own explicit resistances.
                weaknesses = new List<DamageType> { DeriveSingleDamageType(raw.id, exclude: resistances) };
            }
            else if (resistanceWasBlank)
            {
                resistances = new List<DamageType> { DeriveSingleDamageType(raw.id, exclude: weaknesses) };
            }

            // An element claimed as both is the one thing multi-element
            // affinities cannot resolve for themselves -- CombatMath would score
            // it as a weakness and the resistance would silently never apply.
            // Named in full rather than one at a time, so fixing three of them
            // takes one build instead of three.
            //
            // ASKED OF THE AFFINITY rather than recomputed here. The same
            // question is asked again at load time by ContentDatabase.
            // Validation, and one of the two spellings answering differently is
            // exactly how content passes a build and fails at runtime.
            var affinity = ElementalAffinity.Of(weaknesses, resistances);
            var contradictions = affinity.Contradictions;
            if (contradictions.Count > 0)
            {
                error = $"{label}: {string.Join(" and ", contradictions)} " +
                        $"{(contradictions.Count == 1 ? "is" : "are")} listed as both a weakness and a resistance.";
                return false;
            }

            // Blank means "use the default" (right), but anything non-blank
            // and unrecognised is a hard error rather than a silent fall
            // back to the default: a facing typo renders as a monster
            // fighting with its back turned, which looks like broken art and
            // sends someone into the sprite sheets instead of this file.
            var facing = PrincesPalace.Domain.Stage.SpriteFacing.Right;
            if (!string.IsNullOrWhiteSpace(raw.facing)
                && !PrincesPalace.Domain.Stage.StageFacing.TryParse(raw.facing, out facing))
            {
                error = $"{label}: facing '{raw.facing}' is not recognised — use \"left\" or \"right\".";
                return false;
            }

            if (!TryResolveStatus(raw, label, out var appliesStatus, out int statusMagnitude, out int statusDuration, out error))
            {
                return false;
            }

            // All three are Resources-relative and load at runtime. Checked
            // because the wrong convention returns null rather than throwing,
            // so the enemy just fights as a blank plate with no VFX and nothing
            // anywhere says why.
            if (!ArtPathConvention.Check(label, "spritePath", raw.spritePath, out error)) return false;
            // THE ABILITY LIST, validated here and looked up later.
            //
            // This resolver cannot check that a skill id EXISTS -- skills are a
            // separate catalogue resolved independently, and making one resolver
            // wait on the other would couple two things that have no other
            // reason to know about each other. What it can check is everything
            // that is wrong on its face, and ContentBuilder checks the ids
            // against the real catalogue once both have resolved (the same
            // arrangement talents already use for grantsSkillId).
            var abilities = new List<EnemyAbilityRef>();
            for (int i = 0; i < (raw.abilities?.Length ?? 0); i++)
            {
                var entry = raw.abilities[i];
                string id = (entry?.skillId ?? "").Trim();

                if (string.IsNullOrEmpty(id))
                {
                    error = $"{label}: abilities[{i}] has no skillId.";
                    return false;
                }

                if (entry.weight < 0f)
                {
                    error = $"{label}: abilities[{i}] ('{id}') has a negative weight {entry.weight}.";
                    return false;
                }

                abilities.Add(new EnemyAbilityRef(id, entry.weight));
            }

            // Every weight at zero is a monster authored as able to do nothing
            // -- it would fall back to a basic attack forever with nothing
            // saying why. A SINGLE zero is fine and useful while tuning.
            if (abilities.Count > 0 && abilities.All(a => a.Weight <= 0f))
            {
                error = $"{label}: every ability weight is zero, so none can ever be chosen.";
                return false;
            }

            if (!ArtPathConvention.Check(label, "vfx.path", raw.vfx.path, out error)) return false;
            if (!ArtPathConvention.Check(label, "vfx.sfxPath", raw.vfx.sfxPath, out error)) return false;

            var baseStats = new StatBlock(raw.maxHealth, speed, attack, defense);
            resolvedEnemy = new ResolvedEnemy(raw.id, raw.displayName, baseStats, expReward, currencyReward, raw.isBoss,
                affinity, sortOrder,
                (raw.spritePath ?? string.Empty).Trim(), facing, raw.active,
                (raw.skillName ?? string.Empty).Trim(),
                raw.skillPower < 0f ? DefaultSkillPower : raw.skillPower,
                raw.skillChance < 0f ? DefaultSkillChance : raw.skillChance,
                breakShieldPoints,
                raw.vfx.Copy(),
                abilities, raw.attackWeight < 0f ? 1f : raw.attackWeight,
                appliesStatus, statusMagnitude, statusDuration, raw.avoidsFrontSlot, raw.attackHoldsPosition,
                raw.minFloor, raw.stageScale, raw.slotSpan,
                Combat.Session.StageApproaches.Parse(raw.attackApproach, Combat.Session.StageApproach.Lunge));
            error = null;
            return true;
        }

        // Applied when a monster names a skill but leaves the numbers out.
        private const float DefaultSkillPower = 1.5f;
        private const float DefaultSkillChance = 0.35f;
        private const float DefaultVfxSeconds = 0.6f;
        private const int DefaultVfxImpactFrame = 3;

        // appliesStatus is entirely optional, but once authored, magnitude
        // and duration are both required — mirrors SkillEntryResolver's own
        // TryResolveStatus exactly (see its comment); duplicated rather
        // than shared because each resolver is deliberately self-contained
        // and engine-free, and the two Raw types are unrelated classes.
        private static bool TryResolveStatus(RawEnemyEntry raw, string label,
            out StatusEffectType? appliesStatus, out int magnitude, out int duration, out string error)
        {
            appliesStatus = null;
            magnitude = 0;
            duration = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(raw.appliesStatus))
            {
                return true;
            }

            if (!Enum.TryParse<StatusEffectType>(raw.appliesStatus.Trim(), ignoreCase: true, out var parsed))
            {
                error = $"{label}: appliesStatus '{raw.appliesStatus}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(StatusEffectType)))}.";
                return false;
            }

            if (raw.statusMagnitude <= 0)
            {
                error = $"{label}: appliesStatus is set to {parsed}, so statusMagnitude is required and must be positive.";
                return false;
            }

            if (raw.statusDuration <= 0)
            {
                error = $"{label}: appliesStatus is set to {parsed}, so statusDuration is required and must be positive.";
                return false;
            }

            appliesStatus = parsed;
            magnitude = raw.statusMagnitude;
            duration = raw.statusDuration;
            return true;
        }

        // The keyword for "this monster genuinely has none", as against a blank
        // field meaning "I did not say". See RawEnemyEntry.weakness.
        private const string NoneKeyword = "none";

        // Parses one comma-separated element list.
        //
        // Returns an EMPTY list for a blank field, which the caller reads as
        // "omitted, derive it", and an empty list for "none", which it reads as
        // authored -- the two are told apart at the call site by re-checking the
        // raw string, because the difference is a fact about the JSON rather
        // than about the parsed result.
        //
        // A duplicate inside one list is harmless (the affinity is a set, so
        // "Fire, Fire" is "Fire") and is not worth failing a build over.
        private static bool TryResolveDamageTypes(string value, string label, string fieldName,
                                                  out List<DamageType> result, out string error)
        {
            result = new List<DamageType>();
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true; // resolved later by DeriveDamageTypePair
            }

            if (string.Equals(value.Trim(), NoneKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return true; // authored as none -- see NoneKeyword
            }

            foreach (var piece in value.Split(','))
            {
                string name = piece.Trim();

                // A trailing comma, or "Fire,, Ice". Skipped rather than
                // rejected: nothing about it is ambiguous.
                if (name.Length == 0) continue;

                if (!Enum.TryParse(name, ignoreCase: true, out DamageType parsed))
                {
                    string validOptions = string.Join(", ", Enum.GetNames(typeof(DamageType)));
                    error = $"{label}: {fieldName} '{name}' isn't a valid damage type. " +
                            $"Valid options: {validOptions}, or \"{NoneKeyword}\".";
                    result = null;
                    return false;
                }

                if (!result.Contains(parsed)) result.Add(parsed);
            }

            return true;
        }

        // Deterministic (same id -> same pair, every regeneration) so
        // omitting weakness/resistance doesn't make the build non-
        // reproducible. Guarantees the two values differ.
        // One array, not one per call: Enum.GetValues allocates through
        // reflection and both derivers below run once per authored enemy.
        private static readonly DamageType[] AllDamageTypes =
            (DamageType[])Enum.GetValues(typeof(DamageType));

        private static void DeriveDamageTypePair(string id, out DamageType weakness, out DamageType resistance)
        {
            var values = AllDamageTypes;
            int hash = Math.Abs((id ?? string.Empty).GetHashCode());
            int weaknessIndex = hash % values.Length;
            int resistanceOffset = 1 + hash / values.Length % (values.Length - 1);
            int resistanceIndex = (weaknessIndex + resistanceOffset) % values.Length;
            weakness = values[weaknessIndex];
            resistance = values[resistanceIndex];
        }

        // Same deterministic id-hash approach as DeriveDamageTypePair, for
        // the case where only ONE of weakness/resistance was left blank —
        // picks a value that can't collide with anything the author explicitly
        // listed on the other side.
        //
        // Walks forward until it finds a free element rather than stepping once,
        // because `exclude` is a LIST now: a monster authored as resisting four
        // things has four ways for a single step to land on another collision.
        // Six elements against at most five exclusions means this always
        // terminates with something to return.
        private static DamageType DeriveSingleDamageType(string id, IReadOnlyList<DamageType> exclude)
        {
            var values = AllDamageTypes;
            int hash = Math.Abs((id ?? string.Empty).GetHashCode());
            int index = hash % values.Length;

            for (int step = 0; step < values.Length; step++)
            {
                var candidate = values[(index + step) % values.Length];
                if (!exclude.Contains(candidate)) return candidate;
            }

            // Every element excluded, which content validation would have to
            // have let through. Neutral-ish rather than a throw: the house style
            // is that bad content degrades into the game, not out of it.
            return values[index];
        }
    }
}
