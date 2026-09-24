using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Content
{
    public static partial class ContentDatabase
    {
        // The exact-total ability-score budget (used to be 60, asserted here
        // as well as in CharacterEntryResolver because this sees the WHOLE
        // catalogue at once where the resolver sees one file) was REMOVED
        // 2026-09-07 on the owner's call — Shawn/Bjorn/Odette shipped at
        // 66/64/62 as authored. See characters.json's _readme and
        // CharacterEntryResolver's header for the full record; restoring
        // `total == 60` in both places is how the rule comes back if a
        // future character turns out to need it.
        //
        // Wide enough for real characterisation, narrow enough that the
        // derived bonuses stay inside sane ranges. A 0 would mean a
        // character with negative Attack before gear.
        private const int MinAbilityScore = 3;
        private const int MaxAbilityScore = 20;

        // THE CHECKS NO SINGLE RESOLVER CAN MAKE, and only those.
        //
        // Every entry reaching this point has already been through its own
        // Domain resolver, which rejected the empty ids, the unparseable
        // enums and the out-of-range numbers -- ContentBuilder is the only
        // thing in the project that constructs a *Definition, and it writes
        // nothing at all for a file whose resolver refused it. So a per-type
        // "is this id empty" loop here could only ever fire on input the
        // resolver had already accepted, which is to say never. Those loops
        // are gone.
        //
        // What is left is the half a resolver structurally cannot do, because
        // it sees ONE file: ids unique ACROSS catalogues (a relic sharing an
        // id with an item resolves a save to the wrong definition), a talent
        // naming an item or skill that does not exist, a relic naming an
        // achievement, a skill owned by no character, an enemy weak to and
        // resistant to the same type, and the ability-score reachability
        // ceiling. Returns one message per problem found; an empty list means
        // the catalogues agree with each other.
        public static List<string> ValidateContent()
        {
            EnsureLoaded();
            var errors = new List<string>();
            // ONE SWEEP FOR ID COLLISIONS, across every catalogue at once.
            //
            // This was eight `CheckId(x.id, "Kind")` calls threaded through
            // eight per-type loops, and the empty-id half of it was
            // unreachable (see the header). What survives is the half that is
            // not: `seen` is SHARED, so this is the only place in the project
            // that can notice a relic and an item claiming the same id -- and
            // ids are what saves store, so that collision surfaces later as a
            // save resolving to the wrong definition. AUDIT.md #20's blind
            // spot was exactly this check missing for one type; stating it
            // once over a list of catalogues is what stops the next type
            // added from being forgotten the same way.
            var seen = new HashSet<string>();
            foreach (var (kind, ids) in new (string, IEnumerable<string>)[]
                     {
                         ("Character", _characters.Select(x => x.id)),
                         ("Talent", _talents.Select(x => x.id)),
                         ("Upgrade", _upgrades.Select(x => x.id)),
                         ("Relic", _relics.Select(x => x.id)),
                         ("Modifier", _modifiers.Select(x => x.id)),
                         ("Enemy", _enemies.Select(x => x.id)),
                         ("Item", _items.Select(x => x.id)),
                         ("Skill", _skills.Select(x => x.id)),

                         // Pools join the sweep like every other catalogue.
                         // There is deliberately NO second "a character
                         // names a known pool" check beside it: pools are
                         // built before characters and a failed pool build
                         // writes no characters at all, so a check here
                         // could only ever fire on input
                         // CharacterEntryResolver had already accepted --
                         // the unreachable shape this file's header says was
                         // deleted from the codebase.
                         ("Pool", _pools.Select(x => x.id)),
                         ("Event", _events.Select(x => x.id)),
                     })
            {
                foreach (string id in ids)
                {
                    if (!seen.Add(id))
                    {
                        errors.Add($"Duplicate id '{id}' (on a {kind}) — every id must be unique across all content types.");
                    }
                }
            }

            foreach (var character in _characters)
            {
                if (character.Data.BaseStats.maxHealth <= 0)
                {
                    errors.Add($"Character '{character.id}' has non-positive baseStats.maxHealth ({character.Data.BaseStats.maxHealth}).");
                }

                // No total-budget check any more (see the header comment
                // above) -- what is left is the per-score sanity floor,
                // which is independent of any budget and never depended on
                // one: AbilityDerivation never divides BY a score, so a
                // score of 0 would not throw, it would just derive a large
                // negative bonus that ContentDatabase.Effective.cs then
                // floors rather than crashing on.
                var scores = character.Data.AbilityScores;
                foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
                {
                    int value = scores[score];
                    if (value < MinAbilityScore || value > MaxAbilityScore)
                    {
                        errors.Add($"Character '{character.id}' has {score} {value}, outside the authorable range {MinAbilityScore}-{MaxAbilityScore}.");
                    }
                }

                // THE PLATE HAS TO ACTUALLY LOAD, which is a check no
                // resolver can make: CharacterEntryResolver sees the string
                // and refuses an empty one or one written the wrong
                // convention (ArtPathConvention), but it cannot open a file.
                // This can, and the failure it catches is the silent one --
                // Resources.Load<Sprite> returns null for a typo, for a PNG
                // that imported as a plain Texture2D, and for a file nobody
                // committed, with no error anywhere. The plate is the
                // character's whole identity on the fight column, so the
                // consequence is a blank strip in the HUD rather than a
                // missing decoration, and it is worth a refused build.
                string plateArt = character.Data.PlateArt;
                if (!string.IsNullOrWhiteSpace(plateArt)
                    && UnityEngine.Resources.Load<UnityEngine.Sprite>(plateArt.Trim()) == null)
                {
                    errors.Add($"Character '{character.id}' names plateArt '{plateArt}', which loads nothing. " +
                               "It must be Resources-relative and without an extension (e.g. 'Plates/pc_sheep'), " +
                               "the PNG must sit under Assets/_Project/Resources/, and it must import as a Sprite " +
                               "-- Resources.Load<Sprite> returns null for a plain Texture2D. " +
                               "Run `py tools/normalize_pc_plates.py` to (re)produce the plates.");
                }
            }

            foreach (var enemy in _enemies)
            {
                // AN ABILITY POINTING AT NOTHING SILENTLY DISARMS A MONSTER.
                //
                // The adapter drops an unresolvable id with a warning rather
                // than failing a fight, which is the right call at play time and
                // the wrong place to find out: the symptom is a boss that is
                // merely easier than intended, and nothing about an easier boss
                // looks like a bug. Named here for the same reason the talent
                // check below exists, and caught at build time where a typo is
                // still a typo.
                if (enemy?.Data?.Abilities == null) continue;

                foreach (var ability in enemy.Data.Abilities)
                {
                    if (string.IsNullOrEmpty(ability.SkillId)) continue;

                    if (GetSkill(ability.SkillId) == null)
                    {
                        errors.Add($"Enemy '{enemy.id}' has an ability naming unknown skill id " +
                                   $"'{ability.SkillId}'.");
                    }
                }
            }

            foreach (var talent in _talents)
            {

                // A talent-granted ability that points at nothing puts a
                // button on the combat strip that cannot be pressed —
                // exactly the same failure mode as grantsStartingItemId
                // below, and worth the same named check rather than a null
                // silently reaching FightController's skill strip.
                if (!string.IsNullOrEmpty(talent.Data.GrantsSkillId))
                {
                    var granted = GetSkill(talent.Data.GrantsSkillId);
                    if (granted == null)
                    {
                        errors.Add($"Talent '{talent.id}' grants unknown skill id '{talent.Data.GrantsSkillId}'.");
                    }
                    else if (!talent.IsSharedByEveryCharacter && granted.Data.CharacterId != talent.Data.CharacterId)
                    {
                        // Caught for real: the Fragile Lamb's ward ability was
                        // first authored as "ward", which the OWL already
                        // owned. TalentGrantedSkillsFor's runtime owner check
                        // refused it — correctly — so the node simply granted
                        // nothing, the ability never appeared, and the only
                        // symptom was a button that was not there. An id
                        // collision between two characters' kits is a typo,
                        // and it should fail the content build rather than
                        // quietly delete a talent's whole payload.
                        errors.Add($"Talent '{talent.id}' belongs to '{talent.Data.CharacterId}' but grants skill " +
                                   $"'{talent.Data.GrantsSkillId}', which belongs to '{granted.Data.CharacterId}'. " +
                                   "A character cannot hand out another character's kit.");
                    }
                }

                // RequirementResolver's greatest-fixpoint guarantee (order
                // independent, unique largest legal set — see its own
                // header comment) only holds when every ability-score BONUS
                // is non-negative: a negative one could make REMOVING an
                // item help a different item's requirement, which breaks
                // the monotonicity the whole algorithm rests on.
                foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
                {
                    if (talent.Data.AbilityScoreBonus[score] < 0)
                    {
                        errors.Add($"Talent '{talent.id}' has a negative {score} bonus ({talent.Data.AbilityScoreBonus[score]}) — ability-score bonuses must never be negative, or RequirementResolver's fixpoint is no longer guaranteed to converge on the same set regardless of order.");
                    }
                }

                if (!string.IsNullOrEmpty(talent.Data.GrantsStartingItemId) && GetItem(talent.Data.GrantsStartingItemId) == null)
                {
                    errors.Add($"Talent '{talent.id}' grants unknown item id '{talent.Data.GrantsStartingItemId}'.");
                }

                // A talent owned by a character who does not exist can never
                // be taken by anyone, so it is dead content that still shows
                // up in the global Talents list. Mirrors the same check on
                // skills and on grantsStartingItemId.
                if (!talent.IsSharedByEveryCharacter && GetCharacter(talent.Data.CharacterId) == null)
                {
                    errors.Add($"Talent '{talent.id}' belongs to unknown character id '{talent.Data.CharacterId}'.");
                }

                // The one invariant the talent PANEL depends on: two nodes
                // visible to the same character may not occupy one grid cell,
                // because the grid renders one button per cell. Checked per
                // viewer, since two characters' private trees may share a cell
                // — only one of them is ever on screen.
                foreach (var character in _characters)
                {
                    if (!talent.IsAvailableTo(new Character(character.id)))
                    {
                        continue;
                    }

                    var sameCell = _talents.Where(other =>
                        other != talent
                        && other.Data.Column == talent.Data.Column
                        && other.Data.Row == talent.Data.Row
                        && other.IsAvailableTo(new Character(character.id))).ToList();

                    if (sameCell.Count > 0)
                    {
                        errors.Add($"'{character.id}' sees both '{talent.id}' and '{sameCell[0].id}' at grid cell " +
                                   $"({talent.Data.Column},{talent.Data.Row}) — one cell renders one node.");
                    }
                }
            }

            // The negative-cost sweep that used to sit here is gone, and its
            // absence is not a loss of coverage: upgrades are authored in
            // upgrades.json now and UpgradeEntryResolver refuses a negative
            // cost at BUILD time, where the bad row never becomes an asset at
            // all. This check ran against the generated asset, which is one
            // step too late to be the one that stops it.
            //
            // WHAT REPLACES IT IS THE CHECK A RESOLVER CANNOT MAKE. Exactly one
            // upgrade id is named in code -- SaveData.ExtraRecruitSlotUpgradeId
            // -- and it is what makes the squad's fourth slot reachable at all.
            // A resolver reading upgrades.json has no way to know that; it
            // would happily accept a renamed row. The whole-catalogue view is
            // the only place the constant and the content can be put beside
            // each other, which is the same argument the ability-score budget
            // above is re-asserted on.
            if (_upgrades.All(u => u == null || u.id != SaveData.ExtraRecruitSlotUpgradeId))
            {
                errors.Add($"No upgrade has id '{SaveData.ExtraRecruitSlotUpgradeId}', which " +
                           "SaveData.ExtraRecruitSlotUpgradeId names and SaveData.SquadSize reads to grant the " +
                           "fourth squad slot. Renaming that row in upgrades.json does not break the build " +
                           "anywhere else -- it just makes an upgrade nobody can ever benefit from.");
            }

            foreach (var relic in _relics)
            {
                if (string.IsNullOrWhiteSpace(relic.Data.DisplayName))
                {
                    errors.Add($"Relic '{relic.id}' has no displayName; the Relics screen would show a blank row.");
                }
            }

            foreach (var modifier in _modifiers)
            {
                if (string.IsNullOrWhiteSpace(modifier.Data.DisplayName))
                {
                    errors.Add($"Modifier '{modifier.id}' has no displayName; a tooltip line would show a blank row.");
                }
            }

            foreach (var enemy in _enemies)
            {
                if (enemy.Data.BaseStats.maxHealth <= 0)
                {
                    errors.Add($"Enemy '{enemy.id}' has non-positive baseStats.maxHealth ({enemy.Data.BaseStats.maxHealth}).");
                }

                // An element on BOTH lists, now that each is a list. Still an
                // error rather than a precedence rule: CombatMath scores a
                // weakness first, so the resistance would be authored, shown in
                // the glossary, and never once apply.
                var contradictions = enemy.Data.Affinity.Contradictions;
                if (contradictions.Count > 0)
                {
                    errors.Add($"Enemy '{enemy.id}' lists {string.Join(" and ", contradictions)} " +
                               "as both a weakness and a resistance.");
                }
            }

            // AN ACHIEVEMENT'S BOSS PARAMETER, CHECKED AGAINST THE ENEMY
            // CATALOGUE. AchievementEntryResolver already refused a blank
            // parameter on DefeatSpecificBoss; what it cannot do is know
            // whether the id it was handed is a real enemy, let alone one
            // flagged isBoss -- it sees achievements.json alone. The actual
            // comparison lives in AchievementProgress so it stays a pure,
            // literal-testable function; this is only the wiring that hands
            // it the real catalogue.
            var enemyIsBossById = _enemies.ToDictionary(e => e.id, e => e.Data.IsBoss);
            foreach (var achievement in _achievements)
            {
                if (achievement.Data.Condition != AchievementCondition.DefeatSpecificBoss)
                {
                    continue;
                }

                string error = AchievementProgress.ValidateDefeatSpecificBossParameter(
                    achievement.id, achievement.Data.Parameter, enemyIsBossById);
                if (error != null)
                {
                    errors.Add(error);
                }
            }

            // Precomputed ONCE for the unreachable-requirement check below:
            // the single best ability-score bonus any item accepted by each
            // paperdoll slot grants, and the sum of every talent's own
            // bonus (a character could eventually unlock all of them). This
            // is a GENEROUS reachability ceiling, not an exact one — it
            // ignores the talent point cap and per-character trees — on
            // purpose, so it only ever fires on a requirement no real
            // combination of content could ever satisfy, never on one that
            // is merely tight.
            var bestBonusPerSlot = new Dictionary<EquipmentSlot, AbilityScoreBlock>();
            foreach (var slot in EquipmentSlots.All)
            {
                var best = AbilityScoreBlock.Zero;
                foreach (var candidate in _items)
                {
                    if (!candidate.IsEquippable || !EquipmentSlots.Accepts(slot, candidate.equipSlot))
                    {
                        continue;
                    }

                    foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
                    {
                        if (candidate.abilityScoreBonus[score] > best[score])
                        {
                            best = best.With(score, candidate.abilityScoreBonus[score]);
                        }
                    }
                }

                bestBonusPerSlot[slot] = best;
            }

            var talentBonusSum = AbilityScoreBlock.Zero;
            foreach (var talent in _talents)
            {
                foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
                {
                    if (talent.Data.AbilityScoreBonus[score] > 0)
                    {
                        talentBonusSum = talentBonusSum.With(score, talentBonusSum[score] + talent.Data.AbilityScoreBonus[score]);
                    }
                }
            }

            foreach (var item in _items)
            {
                if (item.cost < 0)
                {
                    errors.Add($"Item '{item.id}' has a negative cost ({item.cost}).");
                }

                // A worn item that grants nothing occupies a slot and does
                // nothing — the paperdoll would happily accept it and the
                // player would never see a number move. ItemEntryResolver
                // already rejects this at authoring time; asserting it here
                // too is what makes it hold for an asset created any other
                // way (the [CreateAssetMenu] hazard in CLAUDE.md).
                if (item.IsEquippable
                    && item.TotalStatBonus.Equals(StatBlock.Zero)
                    && item.abilityScoreBonus.Equals(AbilityScoreBlock.Zero))
                {
                    errors.Add($"Item '{item.id}' is equippable but grants no stats at all.");
                }

                if (item.kind == ItemKind.Weapon && !EquipmentSlots.IsWeaponSlot(item.equipSlot))
                {
                    errors.Add($"Weapon '{item.id}' is assigned to the {item.equipSlot} slot — a Weapon can only go in a hand.");
                }

                foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
                {
                    // Same non-negative-bonus rule as talents, above.
                    if (item.abilityScoreBonus[score] < 0)
                    {
                        errors.Add($"Item '{item.id}' has a negative {score} bonus ({item.abilityScoreBonus[score]}) — ability-score bonuses must never be negative, or RequirementResolver's fixpoint is no longer guaranteed to converge on the same set regardless of order.");
                    }

                    int required = item.requirements[score];
                    if (required <= 0)
                    {
                        continue;
                    }

                    int ceiling = MaxAbilityScore + talentBonusSum[score];
                    foreach (var slot in EquipmentSlots.All)
                    {
                        if (slot == item.equipSlot)
                        {
                            continue; // an item never satisfies its own requirement, and cannot share its own slot with anything else either
                        }

                        ceiling += bestBonusPerSlot[slot][score];
                    }

                    if (required > ceiling)
                    {
                        errors.Add($"Item '{item.id}' requires {required} {score}, but the most any character could ever reach — best base score, every OTHER slot's single best item, and every talent — is {ceiling}. This requirement can never be met.");
                    }
                }
            }

            foreach (var skill in _skills)
            {
                // Mirrors the grantsStartingItemId check: a skill owned by a
                // character who does not exist can never be pressed, and the
                // typo is invisible until someone wonders where the button
                // went.
                // A MONSTER MAY OWN A SKILL TOO, and the owner still has to
                // exist. The rule this is loosening is "a skill with no owner
                // would be offered to the whole roster" -- an enemy id is an
                // owner, and AvailableSkillsFor matches characterId against a
                // CHARACTER's definitionId, so a skill owned by 'golem' can
                // never reach a player's button strip.
                //
                // Checked against both catalogues rather than skipped for
                // anything unrecognised: the whole value of this check is that
                // a typo'd owner is invisible until someone wonders where the
                // button went, and that is exactly as true for a monster.
                bool ownedByCharacter = GetCharacter(skill.Data.CharacterId) != null;
                bool ownedByEnemy = GetEnemy(skill.Data.CharacterId) != null;

                if (!ownedByCharacter && !ownedByEnemy)
                {
                    errors.Add($"Skill '{skill.id}' belongs to unknown owner id '{skill.Data.CharacterId}'. " +
                               "It must name a character or an enemy.");
                }

                // THE TWO FACTS HAVE TO AGREE. playerSelectable exists because
                // the resolver cannot see this catalogue; this is the other end
                // of that trade, and without it the flag is an unchecked claim.
                //
                // Both directions are wrong in their own way: a selectable
                // skill owned by a monster can never be pressed by anyone, and
                // a non-selectable one owned by a character is a button the
                // player has silently lost.
                else if (skill.Data.PlayerSelectable && ownedByEnemy)
                {
                    errors.Add($"Skill '{skill.id}' is player-selectable but belongs to enemy " +
                               $"'{skill.Data.CharacterId}', so no character can ever be offered it. " +
                               "Set playerSelectable false, or give it a character owner.");
                }
                // A PLACEHOLDER IS THE ONE LEGITIMATE CASE of a character's
                // own skill that never reaches their strip, and it is that
                // BY CONSTRUCTION: SkillEntryResolver FORCES PlayerSelectable
                // false for one regardless of what the row says, because a
                // stand-in with no effect authored would be a button that
                // costs a turn and does nothing. Shawn's level-30 node names
                // one (placeholder_shawn_capstone) so the shape of his track
                // is real before the owner has designed its ability --
                // collecting it hands over a skill that correctly does not
                // appear. Refusing that here would mean the choice was
                // between an undesigned button in the fight and no node on
                // the rail.
                else if (!skill.Data.PlayerSelectable && ownedByCharacter && !skill.Data.Placeholder)
                {
                    errors.Add($"Skill '{skill.id}' belongs to character '{skill.Data.CharacterId}' but is " +
                               "not player-selectable, so it will never appear on their strip.");
                }

                // A book-only skill's unlockLevel is int.MaxValue by
                // construction (SkillEntryResolver) -- not a violation of
                // "1 or higher", the carve-out below is what stops this row
                // reading it as one. The second check is the load-time twin
                // of the resolver's own authoring-time refusal: content is
                // checked at both moments, and this is the one that would
                // catch a hand-edited asset the resolver never saw.
                // ONE WAY TO LEARN A SKILL. The resolver's own rule, mirrored
                // for the loaded catalogue -- see SkillEntryResolver's
                // StartingKitUnlockLevel/GrantedElsewhereUnlockLevel for why
                // there are exactly two legal values and nothing between
                // them. Reads the same two constants rather than retyping
                // the numbers, so the two doors cannot drift apart.
                if (!skill.Data.BookOnly
                    && skill.Data.UnlockLevel != SkillEntryResolver.StartingKitUnlockLevel
                    && skill.Data.UnlockLevel != SkillEntryResolver.GrantedElsewhereUnlockLevel)
                {
                    errors.Add($"Skill '{skill.id}' unlocks at level {skill.Data.UnlockLevel}. A skill is either " +
                               $"part of the starting kit ({SkillEntryResolver.StartingKitUnlockLevel}) or handed " +
                               $"over by a reward track or a talent ({SkillEntryResolver.GrantedElsewhereUnlockLevel}) " +
                               "-- the levelling ladder in between is gone.");
                }

                if (skill.Data.BookOnly && skill.Data.UnlockLevel != int.MaxValue)
                {
                    errors.Add($"Skill '{skill.id}' is bookOnly but its unlockLevel is {skill.Data.UnlockLevel}, not " +
                               "int.MaxValue -- bookOnly and unlockLevel cannot both be authored.");
                }

                if (skill.Data.BookTier < 0)
                {
                    errors.Add($"Skill '{skill.id}' has a negative bookTier ({skill.Data.BookTier}). 0 means not " +
                               "book-eligible.");
                }

                // A free skill that touches a health or mana bar strictly
                // dominates every other action. A free skill that only
                // rearranges the board does not — Provoke deals nothing,
                // heals nobody, and buys the right to choose who swings at
                // you in exchange for the whole turn. Same carve-out, same
                // reasoning, as SkillEntryResolver's copy of this rule; both
                // exist because content is checked at authoring time AND
                // after generation, and neither is redundant.
                // AND, LIKE THAT COPY, ONLY WHERE SOMEBODY CHOOSES IT. The
                // argument is about a player weighing this action against
                // another; a monster's abilities are drawn by weight and it has
                // neither mana nor wool to spend either way.
                //
                // AND ONLY WHERE THE OWNER OPENS THE FIGHT ABLE TO PAY. This
                // half is easier to state here than in the resolver, which
                // cannot see a catalogue: a pool authored `startRule: Zero`
                // holds nothing on turn one, so its holder owning no free
                // action cannot act at all on the turn the fight starts.
                // Bjorn's `fury` is the shipped case and his slam and brace
                // are the shipped skills; the same rule, phrased against the
                // same fact, lives in SkillEntryResolver, whose comment
                // records why it outlives the unauthored Fury prices.
                bool ownerOpensEmpty =
                    PrimaryPoolOf(skill.Data.CharacterId)?.StartRule == PoolStartRule.Zero;

                if (skill.Data.PlayerSelectable && skill.Data.ManaCost == 0 && !skill.Data.CostsResource
                    && skill.Data.Effect != SkillEffect.Provoke && !ownerOpensEmpty)
                {
                    errors.Add($"Skill '{skill.id}' costs nothing at all, so it strictly dominates every other action.");
                }

                // A character can only spend a resource they have. This is
                // the cross-content check that catches authoring a Wool cost
                // onto somebody who has no Wool.
                if (skill.Data.CostsResource)
                {
                    var owner = GetCharacter(skill.Data.CharacterId);
                    if (owner != null && !owner.Data.HasSignatureResource)
                    {
                        errors.Add($"Skill '{skill.id}' costs a signature resource, but '{skill.Data.CharacterId}' has none.");
                    }
                }

                // The load-time twin of SkillEntryResolver's own cross-
                // catalogue check (its comment records why it cannot make
                // this call itself) -- checked here as well because content
                // is validated at both moments and an asset built by hand
                // under Resources/Content never passed through the resolver
                // at all. A poolTiers skill spends the OWNER'S PRIMARY pool,
                // not a signature resource, so the earlier CostsResource
                // check above cannot stand in for this one -- Bjorn's Fury
                // IS his primary pool.
                if (skill.Data.HasPoolTiers && PrimaryPoolOf(skill.Data.CharacterId) == null)
                {
                    errors.Add($"Skill '{skill.id}' authors poolTiers, but '{skill.Data.CharacterId}' has no " +
                               "primary pool to spend from.");
                }
            }

            var seenSpellTierLevels = new HashSet<int>();
            foreach (var tier in _spellTiers)
            {
                if (!seenSpellTierLevels.Add(tier.Data.Level))
                {
                    errors.Add($"Duplicate spell tier for level {tier.Data.Level} — every level must appear at most once.");
                }

                if (tier.Data.ManaCost <= 0)
                {
                    errors.Add($"Spell tier level {tier.Data.Level} has non-positive manaCost ({tier.Data.ManaCost}).");
                }

                if (tier.Data.PowerMultiplier <= 0f)
                {
                    errors.Add($"Spell tier level {tier.Data.Level} has non-positive powerMultiplier ({tier.Data.PowerMultiplier}).");
                }

                // The level-1 tier is what GetSpellTierForLevel(1) has to
                // resolve to for a brand new character — gating it on a
                // requirement would leave Skill with no mana cost or
                // damage to fall back on for anyone who has not yet met it.
                if (tier.Data.Level == 1 && !tier.Data.Requirements.Equals(AbilityScoreBlock.Zero))
                {
                    errors.Add($"Spell tier level 1 has a non-zero requirement ({tier.Data.Requirements}) — the level-1 tier must always be usable, since a fresh character has to have SOME spell tier available from the very first fight.");
                }
            }

            // Character.level always starts at 1 (see Character.cs) — with
            // no level-1 tier, GetSpellTierForLevel(1) returns null for
            // every fresh character, and Skill would have no mana cost or
            // damage to fall back on.
            if (_spellTiers.Count > 0 && !seenSpellTierLevels.Contains(1))
            {
                errors.Add("No spell tier defined for level 1 — every fresh character starts at level 1 and needs one.");
            }

            // A reward track's UnlockSkill node that points at nothing is the
            // same failure the talent arm above exists for: a reward the
            // player collects, is captioned for, and never receives, with no
            // symptom but an absence.
            //
            // THE FIRST HALF OF THAT ARM AND DELIBERATELY NOT ITS SECOND
            // (docs/archive/PLAN_REWARD_TRACKS.md §3h). The talent check also refuses
            // a skill belonging to another character; this one must not, and
            // the next reader restoring the symmetry is exactly what this
            // paragraph is here to stop. A talent belongs to a character and
            // could name somebody else's skill by typo; a TRACK IS the
            // character, so naming a skill on it has already said whose it
            // is -- and every book-only spell in the game is authored
            // characterId "sheep", so an ownership test would refuse Odette
            // the two her track is built around.
            //
            // Here as well as in RewardTrackEntryResolver because this
            // validates the LOADED catalogue: an asset created by hand under
            // Resources/Content never passed through a resolver at all.
            //
            // The three rules left are the resolver's own, mirrored for the
            // same reason: a skillId that names a real skill, no signature
            // reward on a character with no signature resource, and
            // FuryStartOfFight refused on anyone but a positive-amount,
            // Zero-start pool owner (RewardTrackEntryResolver's rule 6).
            // (The old "filler element must be one the character can deal
            // at level 1" mirror went with the filler mix itself --
            // progression v2 phase 4, see RawTrackLevel.)
            foreach (var track in _rewardTracks)
            {
                var owner = GetCharacter(track.Data.CharacterId);
                bool hasSignatureResource = owner != null && owner.Data.HasSignatureResource;

                // THE RESOLVER'S OWN RULE 6, mirrored for the same reason as
                // the signature-resource check just above: this validates
                // the LOADED catalogue, which a hand-authored asset can
                // reach without ever passing through RewardTrackEntryResolver.
                bool primaryPoolStartsZero = owner != null
                    && PrimaryPoolFor(owner.Data.PrimaryPoolId)?.Data?.StartRule == PoolStartRule.Zero;

                foreach (var level in track.Data.Levels)
                {
                    // The same "resolves to a real skill, nothing about
                    // whose it is" check as UnlockSkill's, extended to the
                    // three Skill*Delta kinds' own skillId -- §3h again, a
                    // track IS the character.
                    bool needsSkillId = level.Reward == TrackReward.UnlockSkill
                        || level.Reward == TrackReward.SkillCostDelta
                        || level.Reward == TrackReward.SkillFlatDelta
                        || level.Reward == TrackReward.SkillPowerDelta;

                    if (needsSkillId && (string.IsNullOrEmpty(level.SkillId) || GetSkill(level.SkillId) == null))
                    {
                        errors.Add($"Reward track '{track.Data.CharacterId}' level {level.Level} ({level.Reward}) " +
                                   $"names unknown skill id '{level.SkillId}'.");
                    }

                    if (RewardTrack.IsSignatureReward(level.Reward) && !hasSignatureResource)
                    {
                        errors.Add($"Reward track '{track.Data.CharacterId}' level {level.Level} authors " +
                                   $"{level.Reward}, but '{track.Data.CharacterId}' has no signature resource.");
                    }

                    if (level.Reward == TrackReward.FuryStartOfFight && (!primaryPoolStartsZero || level.Amount <= 0))
                    {
                        errors.Add($"Reward track '{track.Data.CharacterId}' level {level.Level} authors " +
                                   "FuryStartOfFight, but it is only legal on a character whose primary pool " +
                                   "starts at Zero, with an amount above 0.");
                    }
                }

                // The node-kind rules, mirrored from the resolver for the
                // loaded-catalogue path -- see RewardTrackNodeValidation's
                // own header for why this is one function called twice
                // rather than the same rules typed twice.
                errors.AddRange(RewardTrackNodeValidation.Validate($"Reward track '{track.Data.CharacterId}'",
                    RewardTrackDefinition.From(track.Data)));
            }

            return errors;
        }
    }
}
