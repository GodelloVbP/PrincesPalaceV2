using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Content
{
    // Validates characters.json. Same collected-not-first-only reporting as
    // the other resolvers: one build tells you everything wrong with the
    // file, rather than one problem per run.
    public static class CharacterEntryResolver
    {
        // Every character's six ability scores used to have to total exactly
        // 60 (6 x the neutral score) — a budget, not a floor, so
        // differentiation was REDISTRIBUTION rather than growth: a character
        // could not be quietly stronger than the roster by being above
        // average at everything, because to be tough somewhere it had to be
        // feeble somewhere else.
        //
        // REMOVED 2026-09-07 ON THE OWNER'S CALL: Shawn/Bjorn/Odette shipped
        // with totals of 66/64/62 as authored, and the owner chose to ship
        // them rather than trim them back onto the old budget. See
        // characters.json's _readme for the full record. If a future
        // character turns out to be a flatly stronger pick than the roster
        // for having no real weakness, that is the failure mode the budget
        // existed to prevent — restoring a `total == 60` check here and in
        // ContentDatabase.ValidateContent is the fix.
        //
        // Each score alone is still checked below (1-30, refusing 0 or
        // negative) — that sanity floor is independent of any budget and
        // stayed.
        public const int MinAbilityScore = 1;
        public const int MaxAbilityScore = 30;

        // The dialogue name plate's epithet line -- "Prince the cat" sits
        // next to a name, not in a paragraph, so this is the same shape of
        // cap PoolEntryResolver.MaxShortTagLength is for a meter row: the
        // field fits what draws it or it is refused rather than silently
        // clipped or overflowing.
        public const int MaxEpithetLength = 32;

        // How many characters a fresh profile fields, and therefore how many
        // must carry startsInSquad. The stage's own capacity -- there are
        // three player slots and there is no arrangement in which a fourth
        // starting member could be drawn.
        public const int DefaultSquadSize = FightHudSpec.StageSlotsPerSide;

        // TAKES THE KNOWN POOL IDS, the way RelicEntryResolver takes the
        // known achievement ids: ContentBuilder resolves pools.json FIRST
        // and hands the real ids down, so the only source of truth for what
        // resources exist is the file that defines them rather than a
        // constant here that content has to match by hand.
        //
        // REQUIRED, not an optional parameter defaulting to null. Every
        // caller either has the pool ids or is a fixture that can pass the
        // one shipped id in a literal; an optional parameter would be a hole
        // a later caller falls into silently, and the failure would be a
        // primaryPoolId typo shipping as a character whose resource does not
        // exist.
        public static bool TryResolveAll(IReadOnlyList<RawCharacterEntry> entries, IReadOnlyCollection<string> knownPoolIds,
            out List<ResolvedCharacter> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedCharacter>();
            errors = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, knownPoolIds, out var single, out string error))
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

            // A signature resource id is what CombatantState.SignaturePool keys
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

        private static bool TryResolveOne(RawCharacterEntry raw, int index, int sortOrder,
            IReadOnlyCollection<string> knownPoolIds, out ResolvedCharacter resolvedCharacter, out string error)
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

            // REQUIRED, the two-argument overload -- exactly as role above.
            // Empty used to fall back to Blue, which was fine while the theme
            // only picked one of six near-identical dark kit frames. It is
            // the character's IDENTITY COLOUR now (PcTheme): the rim and the
            // name on every HUD card that stands for them, and Blue is
            // Odette's. An unauthored row would not look unthemed, it would
            // look like Odette, which is the plausible-wrong-answer this
            // codebase refuses (docs/CODE_STANDARDS.md Sec5).
            if (!TryParseEnum(raw.plateTheme, out ButtonTheme plateTheme))
            {
                error = $"{label}: plateTheme '{raw.plateTheme}' is required and must name a ButtonTheme " +
                        $"— it is this character's identity colour on every HUD card. " +
                        $"Valid: {NamesOf<ButtonTheme>()}.";
                return false;
            }

            // WHICH RESOURCE THIS CHARACTER'S SKILLS SPEND, checked against
            // the pools the build actually produced rather than against a
            // list here -- see TryResolveAll's header. Blank resolves to
            // "mana" (the field's own default, restated so a row that
            // explicitly writes "" behaves like one that omits the key), and
            // an unknown id is refused naming the ids that do exist, because
            // the alternative is a character whose meter has no colour, no
            // capacity and no name.
            string primaryPoolId = string.IsNullOrWhiteSpace(raw.primaryPoolId) ? "mana" : raw.primaryPoolId.Trim();
            if (knownPoolIds == null || !knownPoolIds.Contains(primaryPoolId))
            {
                string known = knownPoolIds == null || knownPoolIds.Count == 0
                    ? "(none -- pools.json resolved nothing)"
                    : string.Join(", ", knownPoolIds);
                error = $"{label}: primaryPoolId '{primaryPoolId}' is not a pool in pools.json. Known: {known}.";
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

            // THE OTHER THREE BASE STATS, AND FAVOR (AUDIT #145 B8).
            // maxHealth and speed above were refused at <= 0; these were not
            // checked at all, so a negative defense validated and reached
            // StatBlock, and a negative princesFavor was clamped to 0 without
            // a word -- an authored number silently replaced. 0 stays legal
            // for all four: no base attack or no defense is a real (if odd)
            // character, and 0 favor is the field's own default.
            foreach (var (name, value) in new (string, int)[]
                     {
                         ("attack", raw.attack), ("physicalDefense", raw.physicalDefense),
                         ("magicalDefense", raw.magicalDefense), ("princesFavor", raw.princesFavor),
                     })
            {
                if (value < 0)
                {
                    error = $"{label}: {name} cannot be negative (got {value}).";
                    return false;
                }
            }

            var scores = new AbilityScoreBlock(raw.strength, raw.dexterity, raw.constitution,
                raw.wisdom, raw.intelligence, raw.charisma);

            // No total-budget check any more (see the header comment above),
            // but a score still has to be a plausible number on its own.
            // AbilityDerivation never divides BY a score (only by fixed
            // constants), so 0 or a negative score would not throw — it
            // would quietly derive a large negative bonus (e.g. CON 0 is
            // (0-10)*20 = -200 max health) that ContentDatabase.Effective.cs
            // then floors to 1 HP / 0 defense / 0 mana rather than crashing.
            // That is a character silently reduced to barely-functional
            // rather than a caught authoring mistake, which is exactly the
            // failure mode this refuses instead.
            foreach (var (name, value) in new (string, int)[]
                     {
                         ("strength", raw.strength), ("dexterity", raw.dexterity),
                         ("constitution", raw.constitution), ("wisdom", raw.wisdom),
                         ("intelligence", raw.intelligence), ("charisma", raw.charisma),
                     })
            {
                if (value < MinAbilityScore || value > MaxAbilityScore)
                {
                    error = $"{label}: {name} is {value}, outside the authorable range " +
                            $"{MinAbilityScore}-{MaxAbilityScore}.";
                    return false;
                }
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
            if (!ArtPathConvention.Check(label, "plateArt", raw.plateArt, out error)) return false;
            if (!ArtPathConvention.Check(label, "dialogueBustPath", raw.dialogueBustPath, out error)) return false;

            // REQUIRED, and checked AFTER the convention so a row that
            // authored the path the wrong way round hears which mistake it
            // made rather than "you did not author one". Art is optional
            // almost everywhere in this project (ArtPathConvention.Check
            // passes an empty value on purpose); this is one of the two
            // exceptions on a character row, because the fight HUD's plate
            // column has no fallback drawing to fall back TO -- a plateless
            // character would be an empty rectangle where the other two have
            // a face.
            if (string.IsNullOrWhiteSpace(raw.plateArt))
            {
                error = $"{label}: plateArt is required -- it is this character's own fight-HUD plate, " +
                        "the surface their name, meters and status badges are drawn on. Write it " +
                        "Resources-relative and without an extension, e.g. 'Plates/pc_sheep'; " +
                        "tools/normalize_pc_plates.py is what produces the file.";
                return false;
            }

            // OPTIONAL, unlike plateTheme and plateArt above it: an
            // unauthored epithet just means the name plate shows no gold
            // line under the name, which is a valid look and not a hole in
            // the row. Trimmed before the cap is measured, so leading or
            // trailing whitespace an author left in cannot push a line over
            // by characters nobody can see.
            string epithet = (raw.epithet ?? string.Empty).Trim();
            if (epithet.Length > MaxEpithetLength)
            {
                error = $"{label}: epithet '{epithet}' is {epithet.Length} characters and the name plate fits " +
                        $"{MaxEpithetLength}.";
                return false;
            }

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
                raw.princesFavor,
                sortOrder,
                raw.startsInSquad,
                raw.squadSlot,
                plateTheme,
                primaryPoolId,
                raw.plateArt.Trim(),
                epithet,
                (raw.dialogueBustPath ?? string.Empty).Trim());
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
