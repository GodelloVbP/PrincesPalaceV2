using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Validates characters.json. Same collected-not-first-only reporting as
    // the other resolvers: one build tells you everything wrong with the
    // file, rather than one problem per run.
    public static class CharacterEntryResolver
    {
        // Every character's six ability scores must total exactly this. It is
        // 6 x the neutral score, so an unauthored character (a flat 10 across
        // the board) already satisfies it and derives nothing — which is what
        // lets exactly one character be designed at a time without touching
        // the others.
        //
        // A budget rather than a floor, deliberately: differentiation here is
        // REDISTRIBUTION, not growth. A new character cannot be quietly
        // stronger than the roster by being above average at everything; to
        // be tough it has to be feeble somewhere else.
        //
        // ContentDatabase.ValidateContent asserts the same total against the
        // BUILT assets, which is not redundant — it catches an asset created
        // through the [CreateAssetMenu] hazard CLAUDE.md flags, which never
        // passes through this resolver at all.
        public const int AbilityScoreBudget = 60;

        // How many characters a fresh profile fields, and therefore how many
        // must carry startsInSquad. The stage's own capacity -- there are
        // three player slots and there is no arrangement in which a fourth
        // starting member could be drawn.
        public const int DefaultSquadSize = FightHudSpec.StageSlotsPerSide;

        public static bool TryResolveAll(IReadOnlyList<RawCharacterEntry> entries, out List<ResolvedCharacter> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedCharacter>();
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

            foreach (string duplicateId in resolved.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate character id '{duplicateId}' — every id must be unique, and it is written " +
                           "into save files, so a collision corrupts an existing profile rather than just " +
                           "confusing the roster.");
            }

            // A signature resource id is what CombatantState.Signature keys
            // on; two characters sharing one would make their resources
            // indistinguishable.
            foreach (var duplicate in resolved.Where(c => c.HasSignatureResource)
                         .GroupBy(c => c.SignatureId).Where(g => g.Count() > 1))
            {
                errors.Add($"Characters {string.Join(", ", duplicate.Select(c => c.Id))} all use the signature " +
                           $"resource id '{duplicate.Key}' — one resource per character.");
            }

            // ---- the starting squad, which is a WHOLE-FILE fact ------------
            //
            // Checked here rather than per entry because no single row can be
            // wrong on its own: a character with startsInSquad is fine, and
            // four of them are not. Same shape as the duplicate-id and
            // duplicate-signature rules above it, and reported the same way --
            // every id named, so an author fixes the file in one pass rather
            // than one row per build.
            var starters = resolved.Where(c => c.StartsInSquad).ToList();

            if (starters.Count != DefaultSquadSize)
            {
                errors.Add($"{starters.Count} character(s) set startsInSquad and exactly {DefaultSquadSize} must " +
                           $"({(starters.Count == 0 ? "none" : string.Join(", ", starters.Select(c => c.Id)))}). " +
                           "That flag is what a fresh profile opens with; without exactly three of them the " +
                           "starting squad would fall back to whichever rows happen to sit at the top of the " +
                           "file, which is how appending a character used to make them unfieldable.");
            }

            foreach (var duplicate in starters.GroupBy(c => c.SquadSlot).Where(g => g.Count() > 1))
            {
                errors.Add($"Characters {string.Join(", ", duplicate.Select(c => c.Id))} all claim squadSlot " +
                           $"{duplicate.Key} -- the slots are the squad's ORDER, so two characters in one slot " +
                           "leaves the order decided by file position again, which is the thing this replaces.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawCharacterEntry raw, int index, int sortOrder, out ResolvedCharacter resolvedCharacter, out string error)
        {
            resolvedCharacter = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"characters.json entry #{index + 1}" : $"character '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{label}: id is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required — it is what the player actually reads.";
                return false;
            }

            if (!TryParseEnum<CharacterRole>(raw.role, out var role))
            {
                error = $"{label}: role '{raw.role}' is not a CharacterRole. Valid: {NamesOf<CharacterRole>()}.";
                return false;
            }

            if (!TryParseEnum(raw.attackType, out DamageType attackType, DamageType.Physical))
            {
                error = $"{label}: attackType '{raw.attackType}' is not a DamageType. Valid: {NamesOf<DamageType>()}.";
                return false;
            }

            if (!TryParseEnum(raw.battleSpriteFacing, out SpriteFacing facing, SpriteFacing.Right))
            {
                error = $"{label}: battleSpriteFacing '{raw.battleSpriteFacing}' is not a SpriteFacing. " +
                        $"Valid: {NamesOf<SpriteFacing>()}.";
                return false;
            }

            if (raw.maxHealth <= 0)
            {
                error = $"{label}: maxHealth must be positive (got {raw.maxHealth}) — a character that starts dead " +
                        "would soft-lock the squad it is in.";
                return false;
            }

            if (raw.speed <= 0)
            {
                error = $"{label}: speed must be positive (got {raw.speed}) — TurnOrder would never schedule them.";
                return false;
            }

            var scores = new AbilityScoreBlock(raw.strength, raw.dexterity, raw.constitution,
                raw.wisdom, raw.intelligence, raw.charisma);
            int total = raw.strength + raw.dexterity + raw.constitution + raw.wisdom + raw.intelligence + raw.charisma;
            if (total != AbilityScoreBudget)
            {
                error = $"{label}: ability scores total {total}, but every character must spend exactly " +
                        $"{AbilityScoreBudget} (STR {raw.strength}, DEX {raw.dexterity}, CON {raw.constitution}, " +
                        $"WIS {raw.wisdom}, INT {raw.intelligence}, CHA {raw.charisma}). Differentiation is " +
                        "redistribution, not growth — to be strong somewhere, be weak somewhere else.";
                return false;
            }

            // Two path conventions coexist in this project and they are not
            // interchangeable, so both of these are checked even though a
            // character's two art fields now happen to share one convention:
            // an Assets-relative path is baked into a scene at build time by
            // AssetDatabase, a Resources-relative one (no extension) is
            // Resources.Load'ed at runtime. Swapping them fails SILENTLY — the
            // loader just returns null. portraitPath was the baked kind until
            // 2026-09-06 and is RuntimeLoaded now, which is why the rule lives
            // in a table and not in a sentence here.
            //
            // These two checks used to be written out here, and were the ONLY
            // ones in the codebase: the other seven art fields across six
            // resolvers accepted either convention in any field. The rule moved
            // to ArtPathConvention so there is one statement of it rather than
            // one per resolver that remembered.
            if (!ArtPathConvention.Check(label, "portraitPath", raw.portraitPath, out error)) return false;
            if (!ArtPathConvention.Check(label, "battleSpritePath", raw.battleSpritePath, out error)) return false;

            bool hasSignature = !string.IsNullOrWhiteSpace(raw.signatureId);
            if (hasSignature)
            {
                if (string.IsNullOrWhiteSpace(raw.signatureDisplayName))
                {
                    error = $"{label}: signatureId '{raw.signatureId}' needs a signatureDisplayName — it is drawn " +
                            "on the combat HUD.";
                    return false;
                }

                if (raw.signatureCapacity <= 0)
                {
                    error = $"{label}: signature '{raw.signatureId}' has capacity {raw.signatureCapacity}. An empty " +
                            "signatureId is how a character has NO resource; a zero-capacity one is a resource " +
                            "that can never hold anything.";
                    return false;
                }

                if (raw.signatureGainPerTurn <= 0 && raw.signatureGainOnAttack <= 0 && raw.signatureGainOnDamageTaken <= 0)
                {
                    error = $"{label}: signature '{raw.signatureId}' gains nothing from any of the three triggers, " +
                            "so it would sit at zero for the whole fight.";
                    return false;
                }
            }

            // THE PER-ROW HALF of the starting-squad rule. "How many carry
            // the flag" is a whole-file question and lives in TryResolveAll;
            // "is this row's slot a legal slot" is answerable here, and saying
            // so here names the row rather than the collection.
            if (raw.startsInSquad && (raw.squadSlot < 1 || raw.squadSlot > DefaultSquadSize))
            {
                error = $"{label} sets startsInSquad with squadSlot {raw.squadSlot}; it must be 1-" +
                        $"{DefaultSquadSize}. The slot is the squad's order, so an unset or out-of-range one " +
                        "would put them nowhere in particular.";
                return false;
            }

            if (!raw.startsInSquad && raw.squadSlot != 0)
            {
                error = $"{label} sets squadSlot {raw.squadSlot} without startsInSquad. A slot on a character " +
                        "nobody starts with is a flag somebody meant to set and did not -- refused rather than " +
                        "ignored, because ignoring it is how the roster quietly loses a member.";
                return false;
            }

            resolvedCharacter = new ResolvedCharacter(
                raw.id.Trim(),
                raw.displayName.Trim(),
                role,
                new StatBlock(raw.maxHealth, raw.speed, raw.attack,
                    physicalDefense: raw.physicalDefense, magicalDefense: raw.magicalDefense),
                scores,
                (raw.portraitPath ?? string.Empty).Trim(),
                (raw.battleSpritePath ?? string.Empty).Trim(),
                facing,
                attackType,
                hasSignature ? raw.signatureId.Trim() : string.Empty,
                hasSignature ? (raw.signatureDisplayName ?? string.Empty).Trim() : string.Empty,
                hasSignature ? raw.signatureCapacity : 0,
                hasSignature ? raw.signatureGainPerTurn : 0,
                hasSignature ? raw.signatureGainOnAttack : 0,
                hasSignature ? raw.signatureGainOnDamageTaken : 0,
                hasSignature && raw.signatureAbsorbsDamage,
                raw.princesFavor < 0 ? 0 : raw.princesFavor,
                sortOrder,
                raw.startsInSquad,
                raw.squadSlot);
            error = null;
            return true;
        }

        private static bool TryParseEnum<T>(string raw, out T value) where T : struct, Enum
        {
            return Enum.TryParse(raw, ignoreCase: true, out value) && Enum.IsDefined(typeof(T), value);
        }

        // Empty means "leave it at the sensible default" rather than "invalid"
        // — an unauthored character should not have to spell out Physical.
        private static bool TryParseEnum<T>(string raw, out T value, T fallback) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                value = fallback;
                return true;
            }

            return TryParseEnum(raw, out value);
        }

        private static string NamesOf<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames(typeof(T)));
    }
}
