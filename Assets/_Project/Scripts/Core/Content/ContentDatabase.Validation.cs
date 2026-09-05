using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Content
{
    public static partial class ContentDatabase
    {
        // Referenced, not re-declared: this used to be a second literal 60
        // with a "must match ContentBuilder.AbilityScoreBudget" comment
        // holding the two together by hand. Now that characters are authored
        // in characters.json, the resolver that validates that file owns the
        // number and this reads it.
        //
        // Still asserted here as well as in the resolver, which is not
        // redundant: an asset created through the [CreateAssetMenu] hazard
        // CLAUDE.md flags never passes through the resolver at all.
        private const int AbilityScoreBudget = Domain.Content.CharacterEntryResolver.AbilityScoreBudget;

        // Wide enough for real characterisation, narrow enough that the
        // derived bonuses stay inside sane ranges. A 0 would mean a
        // character with negative Attack before gear.
        private const int MinAbilityScore = 3;
        private const int MaxAbilityScore = 20;

        // Checks the kind of mistakes that are easy to make when hand-adding
        // a new definition and that nothing else catches until something
        // breaks at runtime: a duplicate/empty id, a 0-HP combatant, a talent
        // that grants an item id that doesn't exist, an enemy that's weak to
        // and resistant to the same type (an authoring slip, not a valid
        // design — see CombatMath.EffectivenessMultiplier's own comment on
        // why that case still needs a defined answer rather than being
        // rejected outright there). Returns one message per problem found;
        // an empty list means content is internally consistent. Does not
        // check cross-content references outside what's listed above — this
        // is a floor, not exhaustive validation.
        public static List<string> ValidateContent()
        {
            EnsureLoaded();
            var errors = new List<string>();
            var seenIds = new HashSet<string>();

            void CheckId(string id, string kind)
            {
                if (string.IsNullOrEmpty(id))
                {
                    errors.Add($"{kind} has an empty id.");
                }
                else if (!seenIds.Add(id))
                {
                    errors.Add($"Duplicate id '{id}' (on a {kind}) — every id must be unique across all content types.");
                }
            }

            foreach (var character in _characters)
            {
                CheckId(character.id, "Character");
                if (character.baseStats.maxHealth <= 0)
                {
                    errors.Add($"Character '{character.id}' has non-positive baseStats.maxHealth ({character.baseStats.maxHealth}).");
                }

                // Ability scores are a fixed budget, not a free stat line.
                // AbilityDerivation turns them into real combat numbers, so
                // without this a new character could be above average at
                // everything and simply outclass the roster. To be tough
                // somewhere you have to be feeble somewhere else.
                var scores = character.baseAbilityScores;
                int total = scores.strength + scores.dexterity + scores.constitution
                            + scores.wisdom + scores.intelligence + scores.charisma;
                if (total != AbilityScoreBudget)
                {
                    errors.Add($"Character '{character.id}' ability scores total {total}, but every character must spend exactly {AbilityScoreBudget} ({scores}).");
                }

                foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
                {
                    int value = scores[score];
                    if (value < MinAbilityScore || value > MaxAbilityScore)
                    {
                        errors.Add($"Character '{character.id}' has {score} {value}, outside the authorable range {MinAbilityScore}-{MaxAbilityScore}.");
                    }
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
                if (enemy?.abilities == null) continue;

                foreach (var ability in enemy.abilities)
                {
                    if (ability == null || string.IsNullOrEmpty(ability.skillId)) continue;

                    if (GetSkill(ability.skillId) == null)
                    {
                        errors.Add($"Enemy '{enemy.id}' has an ability naming unknown skill id " +
                                   $"'{ability.skillId}'.");
                    }
                }
            }

            foreach (var talent in _talents)
            {
                CheckId(talent.id, "Talent");

                // A talent-granted ability that points at nothing puts a
                // button on the combat strip that cannot be pressed —
                // exactly the same failure mode as grantsStartingItemId
                // below, and worth the same named check rather than a null
                // silently reaching FightController's skill strip.
                if (!string.IsNullOrEmpty(talent.grantsSkillId))
                {
                    var granted = GetSkill(talent.grantsSkillId);
                    if (granted == null)
                    {
                        errors.Add($"Talent '{talent.id}' grants unknown skill id '{talent.grantsSkillId}'.");
                    }
                    else if (!talent.IsSharedByEveryCharacter && granted.data.CharacterId != talent.characterId)
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
                        errors.Add($"Talent '{talent.id}' belongs to '{talent.characterId}' but grants skill " +
                                   $"'{talent.grantsSkillId}', which belongs to '{granted.data.CharacterId}'. " +
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
                    if (talent.abilityScoreBonus[score] < 0)
                    {
                        errors.Add($"Talent '{talent.id}' has a negative {score} bonus ({talent.abilityScoreBonus[score]}) — ability-score bonuses must never be negative, or RequirementResolver's fixpoint is no longer guaranteed to converge on the same set regardless of order.");
                    }
                }

                if (!string.IsNullOrEmpty(talent.grantsStartingItemId) && GetItem(talent.grantsStartingItemId) == null)
                {
                    errors.Add($"Talent '{talent.id}' grants unknown item id '{talent.grantsStartingItemId}'.");
                }

                // A talent owned by a character who does not exist can never
                // be taken by anyone, so it is dead content that still shows
                // up in the global Talents list. Mirrors the same check on
                // skills and on grantsStartingItemId.
                if (!talent.IsSharedByEveryCharacter && GetCharacter(talent.characterId) == null)
                {
                    errors.Add($"Talent '{talent.id}' belongs to unknown character id '{talent.characterId}'.");
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
                        && other.column == talent.column
                        && other.row == talent.row
                        && other.IsAvailableTo(new Character(character.id))).ToList();

                    if (sameCell.Count > 0)
                    {
                        errors.Add($"'{character.id}' sees both '{talent.id}' and '{sameCell[0].id}' at grid cell " +
                                   $"({talent.column},{talent.row}) — one cell renders one node.");
                    }
                }
            }

            foreach (var upgrade in _upgrades)
            {
                CheckId(upgrade.id, "Upgrade");
                if (upgrade.cost < 0)
                {
                    errors.Add($"Upgrade '{upgrade.id}' has a negative cost ({upgrade.cost}).");
                }
            }

            // Relics were the one content type this function had never seen
            // (AUDIT.md #20's blind spot, reopened on a type added after the
            // finding was written). The id check matters more than it looks:
            // `seenIds` is shared across every type above, so until relics
            // were walked here, a relic sharing an id with an item or a talent
            // went undetected — and ids are what saves store, so the collision
            // surfaces later as a save resolving to the wrong definition.
            foreach (var relic in _relics)
            {
                CheckId(relic.id, "Relic");
                if (string.IsNullOrWhiteSpace(relic.displayName))
                {
                    errors.Add($"Relic '{relic.id}' has no displayName; the Relics screen would show a blank row.");
                }
            }

            foreach (var modifier in _modifiers)
            {
                CheckId(modifier.id, "Modifier");
                if (string.IsNullOrWhiteSpace(modifier.displayName))
                {
                    errors.Add($"Modifier '{modifier.id}' has no displayName; a tooltip line would show a blank row.");
                }
            }

            foreach (var enemy in _enemies)
            {
                CheckId(enemy.id, "Enemy");
                if (enemy.baseStats.maxHealth <= 0)
                {
                    errors.Add($"Enemy '{enemy.id}' has non-positive baseStats.maxHealth ({enemy.baseStats.maxHealth}).");
                }

                // An element on BOTH lists, now that each is a list. Still an
                // error rather than a precedence rule: CombatMath scores a
                // weakness first, so the resistance would be authored, shown in
                // the glossary, and never once apply.
                var contradictions = enemy.Affinity.Contradictions;
                if (contradictions.Count > 0)
                {
                    errors.Add($"Enemy '{enemy.id}' lists {string.Join(" and ", contradictions)} " +
                               "as both a weakness and a resistance.");
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
                    if (talent.abilityScoreBonus[score] > 0)
                    {
                        talentBonusSum = talentBonusSum.With(score, talentBonusSum[score] + talent.abilityScoreBonus[score]);
                    }
                }
            }

            foreach (var item in _items)
            {
                CheckId(item.id, "Item");
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
                CheckId(skill.id, "Skill");

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
                bool ownedByCharacter = GetCharacter(skill.data.CharacterId) != null;
                bool ownedByEnemy = GetEnemy(skill.data.CharacterId) != null;

                if (!ownedByCharacter && !ownedByEnemy)
                {
                    errors.Add($"Skill '{skill.id}' belongs to unknown owner id '{skill.data.CharacterId}'. " +
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
                else if (skill.data.PlayerSelectable && ownedByEnemy)
                {
                    errors.Add($"Skill '{skill.id}' is player-selectable but belongs to enemy " +
                               $"'{skill.data.CharacterId}', so no character can ever be offered it. " +
                               "Set playerSelectable false, or give it a character owner.");
                }
                else if (!skill.data.PlayerSelectable && ownedByCharacter)
                {
                    errors.Add($"Skill '{skill.id}' belongs to character '{skill.data.CharacterId}' but is " +
                               "not player-selectable, so it will never appear on their strip.");
                }

                // A book-only skill's unlockLevel is int.MaxValue by
                // construction (SkillEntryResolver) -- not a violation of
                // "1 or higher", the carve-out below is what stops this row
                // reading it as one. The second check is the load-time twin
                // of the resolver's own authoring-time refusal: content is
                // checked at both moments, and this is the one that would
                // catch a hand-edited asset the resolver never saw.
                if (!skill.data.BookOnly && skill.data.UnlockLevel < 1)
                {
                    errors.Add($"Skill '{skill.id}' unlocks at level {skill.data.UnlockLevel}; characters start at level 1.");
                }

                if (skill.data.BookOnly && skill.data.UnlockLevel != int.MaxValue)
                {
                    errors.Add($"Skill '{skill.id}' is bookOnly but its unlockLevel is {skill.data.UnlockLevel}, not " +
                               "int.MaxValue -- bookOnly and unlockLevel cannot both be authored.");
                }

                if (skill.data.BookTier < 0)
                {
                    errors.Add($"Skill '{skill.id}' has a negative bookTier ({skill.data.BookTier}). 0 means not " +
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
                if (skill.data.PlayerSelectable && skill.data.ManaCost == 0 && !skill.data.CostsResource
                    && skill.data.Effect != SkillEffect.Provoke)
                {
                    errors.Add($"Skill '{skill.id}' costs nothing at all, so it strictly dominates every other action.");
                }

                // A character can only spend a resource they have. This is
                // the cross-content check that catches authoring a Wool cost
                // onto somebody who has no Wool.
                if (skill.data.CostsResource)
                {
                    var owner = GetCharacter(skill.data.CharacterId);
                    if (owner != null && !owner.HasSignatureResource)
                    {
                        errors.Add($"Skill '{skill.id}' costs a signature resource, but '{skill.data.CharacterId}' has none.");
                    }
                }
            }

            var seenSpellTierLevels = new HashSet<int>();
            foreach (var tier in _spellTiers)
            {
                if (!seenSpellTierLevels.Add(tier.level))
                {
                    errors.Add($"Duplicate spell tier for level {tier.level} — every level must appear at most once.");
                }

                if (tier.manaCost <= 0)
                {
                    errors.Add($"Spell tier level {tier.level} has non-positive manaCost ({tier.manaCost}).");
                }

                if (tier.powerMultiplier <= 0f)
                {
                    errors.Add($"Spell tier level {tier.level} has non-positive powerMultiplier ({tier.powerMultiplier}).");
                }

                // The level-1 tier is what GetSpellTierForLevel(1) has to
                // resolve to for a brand new character — gating it on a
                // requirement would leave Skill with no mana cost or
                // damage to fall back on for anyone who has not yet met it.
                if (tier.level == 1 && !tier.requirements.Equals(AbilityScoreBlock.Zero))
                {
                    errors.Add($"Spell tier level 1 has a non-zero requirement ({tier.requirements}) — the level-1 tier must always be usable, since a fresh character has to have SOME spell tier available from the very first fight.");
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

            return errors;
        }
    }
}
