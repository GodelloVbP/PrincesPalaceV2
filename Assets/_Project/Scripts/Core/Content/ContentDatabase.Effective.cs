using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Content
{
    public static partial class ContentDatabase
    {
        // How much raw damage ONE point of a signature resource soaks.
        //
        // Deliberately NOT CombatMath.DamageScale, which is what it was when
        // the x10 health rescale landed. Tying the two together looked tidy
        // and was quietly catastrophic: at 10-per-point, a mere 2 points of
        // Wool completely absorbed a Bog Witch's 20-damage attack, while
        // Shawn gains 6 a round (3 attacking, 1 taking the hit, 2 at
        // turn start) against a drain of only 2 — so Wool climbed to its cap
        // by round 4 and he became flatly immune to that enemy's entire kit,
        // Hex Bolt included.
        //
        // Lowering the GAIN rates cannot fix that, which is worth recording
        // because it is the obvious first instinct: at 10-per-point, full
        // immunity to a 20-damage hit begins at TWO points, and every
        // non-degenerate gain rate reaches two points within a round or so.
        // The absorb rate is the only lever with any authority here.
        //
        // At 2, a 20-damage hit needs 10 of Shawn's 16 points to fully stop,
        // so ordinary attacks always get some damage through, bigger hits
        // get proportionally more through, and the fleece reads as real
        // armour rather than a toggle between "irrelevant" and "invulnerable".
        //
        // HALVED TO 1 alongside CombatMath.DamageScale's own halving (10->5).
        // Deliberately uncoupled from that constant does not mean immune to
        // it: this is calibrated against the SIZE of a typical hit, and every
        // hit in the game just got half as big. Leaving this at 2 would have
        // reproduced the exact failure this comment already records, from the
        // other direction -- a 10-damage hit needing only 5 points to fully
        // stop instead of 10, i.e. Wool absorbing twice as much fight as
        // before for the same investment.
        private const int SignatureAbsorbPerPoint = 1;

        // The settled result of resolving which of a character's worn items
        // are actually contributing their bonus right now — see
        // RequirementResolver. Entries rather than bare ids or
        // ItemDefinitions alone, because a caller (the equipment screen's
        // greyed-out inert slots, a future hover panel) needs both the
        // instance's own plus and the item it resolves to.
        public readonly struct ActiveLoadoutResult
        {
            public readonly AbilityScoreBlock Scores;
            public readonly IReadOnlyList<(EquipmentSlotEntry Entry, ItemDefinition Item)> LiveEntries;
            public readonly IReadOnlyList<(EquipmentSlotEntry Entry, ItemDefinition Item)> InertEntries;

            public ActiveLoadoutResult(AbilityScoreBlock scores,
                IReadOnlyList<(EquipmentSlotEntry, ItemDefinition)> liveEntries,
                IReadOnlyList<(EquipmentSlotEntry, ItemDefinition)> inertEntries)
            {
                Scores = scores;
                LiveEntries = liveEntries;
                InertEntries = inertEntries;
            }

            public bool IsLive(EquipmentSlot slot)
            {
                foreach (var (entry, _) in LiveEntries)
                {
                    if (entry.slot == slot)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Which of a character's worn items are actually contributing their
        // bonus right now, and the ability scores that live set produces —
        // resolved ONCE via RequirementResolver's fixpoint (see its own
        // header comment) rather than trusted item by item. This is the
        // single source EffectiveStats/EffectiveAbilityScores/
        // EffectiveSkillScaling/EquippedWeapon all read from now, so an
        // inert item is inert everywhere at once rather than in whichever
        // of those someone remembered to gate.
        //
        // No cache: the call graph has EffectiveStats at 8 production call
        // sites and EffectiveAbilityScores at 4, none inside an Update(),
        // and a full resolve is at most 8 candidates — a handful of
        // AbilityScoreBlock comparisons, not a hot path.
        public static ActiveLoadoutResult ActiveLoadout(Character character)
        {
            EnsureLoaded();

            var definition = character != null ? GetCharacter(character.definitionId) : null;
            var floor = definition == null ? AbilityScoreBlock.Zero : definition.Data.AbilityScores;

            if (character != null)
            {
                foreach (string talentId in character.unlockedTalentIds)
                {
                    var talent = GetTalent(talentId);
                    if (talent != null)
                    {
                        floor += talent.Data.AbilityScoreBonus;
                    }
                }

                // Stat points placed during this run, folded in beside the
                // talent bonuses because they are the same KIND of thing: a
                // permanent-for-now property of the character rather than
                // something a piece of gear lends them.
                //
                // Part of the FLOOR specifically, which means an invested
                // point can satisfy a weapon's requirement. That is the
                // intended reading -- levelling into Strength should let you
                // lift the sword you could not lift at level one -- and it is
                // safe for RequirementResolver's fixpoint because the addition
                // is non-negative, the same condition ValidateContent already
                // enforces on talent bonuses for exactly this reason.
                floor += character.investedAbilityScores;
            }

            var bySlot = new Dictionary<EquipmentSlot, (EquipmentSlotEntry Entry, ItemDefinition Item)>();
            var candidates = new List<RequirementCandidate>();
            if (character?.equipment != null)
            {
                foreach (var entry in character.equipment.EquippedEntries())
                {
                    var item = GetItem(entry.itemId);
                    if (item == null || !item.IsEquippable)
                    {
                        continue;
                    }

                    bySlot[entry.slot] = (entry, item);
                    // Ability scores are not scaled by plus (see
                    // AbilityScoreBonusAt's own comment); the requirement IS
                    // scaled, by the global tuning knob, at this exact read
                    // point — RequirementCurve's whole reason for existing.
                    candidates.Add(new RequirementCandidate(entry.slot, item.AbilityScoreBonusAt(entry.plus), RequirementCurve.ApplyGear(item.requirements)));
                }
            }

            var resolution = RequirementResolver.Resolve(floor, candidates);
            var live = resolution.LiveSlots.Select(s => bySlot[s]).ToList();
            var inert = resolution.InertSlots.Select(s => bySlot[s]).ToList();

            return new ActiveLoadoutResult(resolution.Scores.ClampedAtLeast(0), live, inert);
        }

        // A character's stats including every talent they've unlocked.
        // Unknown talent ids are ignored rather than throwing, so a save
        // referencing content that was later removed still loads.
        public static StatBlock EffectiveStats(Character character)
        {
            EnsureLoaded();

            var definition = GetCharacter(character.definitionId);
            var total = definition == null ? StatBlock.Zero : definition.Data.BaseStats;

            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = GetTalent(talentId);
                if (talent != null)
                {
                    total += talent.Data.StatBonus;
                }
            }

            // Everything worn, on top of talents — but only what ActiveLoadout
            // settled as LIVE. An inert item occupies its slot and shows on
            // the paperdoll, but contributes nothing, same as if it were not
            // worn at all. This is the one place plus reaches combat stats.
            var loadout = ActiveLoadout(character);
            foreach (var (entry, item) in loadout.LiveEntries)
            {
                var bonus = item.StatBonusAt(entry.plus);

                // PHASE 3 of the balance redesign (D3): gear no longer grants
                // flat Attack. A weapon's own Attack now enters combat as
                // WeaponPower, at the FightEncounterAdapter seam, computed
                // from the weapon's own attackAtTier and hone alone -- never
                // summed with a character's base Attack or with anything
                // else worn. Zeroed HERE rather than at the source (item.
                // StatBonusAt/attackBonus) so a weapon's DEFINITION still
                // honestly reports what it grants -- the store and the
                // tooltip still want that number -- while EffectiveStats,
                // the one place real combat actually reads, stops treating
                // it as a swing contribution. This
                // also quietly retires the handful of hand-authored
                // "attack N" lines still sitting in itemsets.json (Phase 4
                // rewrites that schema; until then they are authored but
                // inert, which is the correct reading now that armour is
                // not supposed to swing for you).
                bonus.attack = 0;
                total += bonus;
            }

            // Ability scores, LAST — the SAME resolve ActiveLoadout already
            // ran, not a second one (EffectiveAbilityScores would otherwise
            // run RequirementResolver's fixpoint again for one caller).
            total += AbilityDerivation.DerivedStats(loadout.Scores);

            // The reward track's collected nodes, flat and on top of all of
            // it. Here rather than in AbilityDerivation because neither is
            // derived from anything -- both are quantities the track handed
            // over, and folding max health into Constitution would make each
            // node worth 20 health or 1 depending on where the player had
            // spent unrelated points, which is a reward nobody can plan
            // around.
            //
            // READ LIVE off the watermark rather than off a stored total (§2):
            // a stored copy is one retune away from disagreeing with the
            // definition, and there is nothing to spend here, so there is
            // nothing storage buys.
            if (character != null)
            {
                var track = RewardTracks.For(character);
                total.maxHealth += track.CollectedTotal(TrackReward.MaxHealth, character.claimedTrackLevel);
                total.manaRegen += track.CollectedTotal(TrackReward.ManaRegen, character.claimedTrackLevel);
            }

            // THE single clamp point (see AbilityDerivation's own header --
            // every derivation above this is deliberately signed and
            // unclamped). Floor 0 everywhere, EXCEPT two stats that must
            // never reach it: a combatant with 0 max health is already
            // dead before the fight starts, and 0 Speed cannot take a turn
            // at all (SpeedScale divides by it downstream). Neither is a
            // build the game should ever produce, even off a very low
            // CON/DEX spread stacked with the worst gear on offer, so both
            // get their own floor of 1 on top of the general one.
            var clamped = total.ClampedAtLeast(0);
            clamped.maxHealth = Mathf.Max(1, clamped.maxHealth);
            clamped.speed = Mathf.Max(1, clamped.speed);
            return clamped;
        }

        // A fresh signature resource for this character, or null if they have
        // none. Built per fight — the resource starts empty every time, so
        // there is nothing to carry between encounters and nothing to
        // serialise. Mana already fully refills each fight; a resource whose
        // whole premise is "compounds WITHIN a fight" persisting across them
        // would be the odd one out.
        public static ResourcePool BuildSignatureResource(Character character)
        {
            EnsureLoaded();

            var definition = GetCharacter(character.definitionId);
            if (definition == null || !definition.Data.HasSignatureResource)
            {
                return null;
            }

            // Charisma moves the per-turn rate only. Capacity and the
            // situational gains stay authored, so a high-CHA build fills
            // faster without also getting a deeper tank for free.
            int perTurn = definition.Data.SignatureGainPerTurn
                          + AbilityDerivation.SignatureGainBonus(EffectiveAbilityScores(character));
            int capacity = definition.Data.SignatureCapacity;

            // Talents deepen the fleece and speed it up. Same tolerant lookup
            // as everywhere else — an unlocked id that no longer resolves is
            // ignored rather than throwing.
            //
            // Neither field is used by Shawn's reworked tree, which raises
            // neither capacity nor the flat rate: a deeper pool and a faster
            // trickle are exactly the commensurable "+N to a number" nodes
            // the rework exists to delete, and the three engines have taken
            // over the generation question entirely. The fields stay because
            // four other characters' 252 talents still speak this vocabulary
            // and because a future signature resource may well want them.
            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = GetTalent(talentId);
                if (talent != null)
                {
                    capacity += talent.Data.SignatureCapacityBonus;
                    perTurn += talent.Data.SignaturePerTurnBonus;
                }
            }

            // The reward track's three signature terms, beside the talent ones
            // and summed the same way -- docs/PLAN_REWARD_TRACKS.md §2's
            // read-site table. Every kind ACCUMULATES: an authored capacity
            // step is written "+5", never "15", so there is one summation rule
            // and no pair of kinds where getting it the wrong way round is
            // silent.
            //
            // The on-attack gain is deliberately absent: no reward kind grants
            // it, so there is nothing to add and a term reading a total that is
            // always zero would only look like coverage.
            var track = RewardTracks.For(character);
            int claimed = character.claimedTrackLevel;

            capacity += track.CollectedTotal(TrackReward.SignatureCapacity, claimed);
            perTurn += track.CollectedTotal(TrackReward.SignatureGainPerTurn, claimed);
            int onDamageTaken = definition.Data.SignatureGainOnDamageTaken
                                + track.CollectedTotal(TrackReward.SignatureGainOnDamageTaken, claimed);

            // OR'd, never replaced. A character authored to absorb already
            // does; the track can only turn it on for one that does not, and
            // a collected node cannot un-author the flag.
            //
            // HasUnlocked, NOT CollectedTotal(...) > 0. The plan's read-site
            // table says the latter and it cannot work: SignatureAbsorbs is a
            // capability with no number, so its authored Amount is 0 and the
            // sum is 0 however many of them are collected. HasUnlocked asks
            // the question the entry actually answers -- is there such a node
            // at or below the watermark -- and reads the watermark rather than
            // `level`, which is what makes it a COLLECTED reward.
            bool absorbs = definition.Data.SignatureAbsorbsDamage
                           || track.HasUnlocked(TrackReward.SignatureAbsorbs, claimed);

            return new ResourcePool(
                definition.Data.SignatureId,
                string.IsNullOrWhiteSpace(definition.Data.SignatureDisplayName)
                    ? definition.Data.SignatureId
                    : definition.Data.SignatureDisplayName,
                capacity,
                Mathf.Max(0, perTurn),
                Mathf.Max(0, definition.Data.SignatureGainOnAttack),
                Mathf.Max(0, onDamageTaken),
                SignatureAbsorbPerPoint,
                absorbs);
        }

        // Every triggered/conditional rule this character's unlocked talents
        // contribute, flattened into the shape combat reads.
        //
        // Resolved once per fight (FightController.Encounter) rather than
        // per swing — see TalentEffectSet's own header. Unknown ids are
        // skipped rather than throwing, the same tolerance every other
        // Effective* method here already has, so a save referencing content
        // that was later removed still loads.
        public static TalentEffectSet TalentEffects(Character character)
        {
            EnsureLoaded();

            if (character == null)
            {
                return TalentEffectSet.Empty;
            }

            List<TalentEffect> found = null;
            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = GetTalent(talentId);
                if (talent == null || talent.Data.Effects == null || talent.Data.Effects.Length == 0)
                {
                    continue;
                }

                found = found ?? new List<TalentEffect>();
                found.AddRange(talent.Data.Effects);
            }

            return found == null ? TalentEffectSet.Empty : new TalentEffectSet(found);
        }

        // Every rule this character's equipped items' ROLLED MODIFIERS
        // contribute — AND the elemental damage their reward track has paid —
        // flattened into the shape combat reads. The gear-side mirror of
        // TalentEffects just above, same "resolved once per fight, unknown ids
        // skipped" reasoning.
        //
        // THE TRACK TERM IS NOT GEAR, and this method is where it lands anyway
        // rather than getting a seam of its own: ModifierEffectSet is the
        // shape combat already reads elemental percentages out of, and a
        // second set threaded to the same place would be two mechanisms for
        // one thing. Its other production caller, ItemOfferRoll, asks only
        // .Best(FortunateFavorBonusFlat) and cannot be disturbed by an
        // appended elemental effect.
        //
        // PHASE A2: real working code, not a stub, even though it returns
        // ModifierEffectSet.Empty for every character in every real save
        // today — see ModifierEffectSetAlwaysEmptyForRealCharactersTests.
        // Phase A1 already gave EquipmentSlotEntry.modifierIds a real (if
        // always-empty-today) list to walk, and Phase A3's roll has nothing
        // to change here when it lands: it only starts populating
        // modifierIds with real ids, which this method already reads.
        //
        // READS ActiveLoadout, NOT the raw equipment walk TalentEffects has
        // no equivalent of. A worn item whose ability-score requirement is
        // unmet already contributes no stats (see ActiveLoadout's own
        // header, "an inert item is inert everywhere at once") — a modifier
        // riding an inert item granting its effect anyway would be exactly
        // the class of bug that comment was written to prevent, so this
        // reads LiveEntries specifically rather than EquippedEntries().
        public static ModifierEffectSet ModifierEffects(Character character)
        {
            EnsureLoaded();

            if (character == null)
            {
                return ModifierEffectSet.Empty;
            }

            List<ModifierEffect> found = null;
            foreach (var (entry, item) in ActiveLoadout(character).LiveEntries)
            {
                foreach (var scaled in ScaledModifierEffectsForItem(item.tier, (RiftTier)entry.riftTier, entry.modifierIds))
                {
                    found = found ?? new List<ModifierEffect>();
                    found.Add(scaled.Effect);
                }
            }

            // THE REWARD TRACK'S ELEMENTAL NODES, one effect per element,
            // ROUTED BY THE CHARACTER'S OWN attackType (docs/PLAN_REWARD_
            // TRACKS.md §2, "the routing rule"). Exactly one of the two hooks
            // per element, never both:
            //
            // - SAME ELEMENT as their attackType -> ElementalDamageOnHitPercent,
            //   the existing on-hit rider, whose same-element branch multiplies
            //   the hit's own landed figure. That is every landed hit, swing
            //   and cast alike, and it is the only hook a plain swing reaches
            //   at all.
            // - ANY OTHER ELEMENT -> ElementalDamagePercent, the packet hook,
            //   which reads a damage instance's own type inside
            //   ResolveDamageInstances. A foreign element only exists on a
            //   spell packet in the first place.
            //
            // Getting this wrong is invisible rather than broken, which is why
            // it is spelled out: routing everything through the packet hook
            // pays a Nature line on a character whose whole kit authors one
            // damageInstances entry precisely nothing, and routing everything
            // through the rider pays a Fire line on an Arcane caster a flat
            // few points of Attack that has nothing to do with her Firebolt.
            //
            // A character we cannot identify falls through to the packet hook,
            // which is the inert direction: an unknown attackType must not
            // accidentally match and double a line's value.
            var own = GetCharacter(character.definitionId)?.Data.AttackType;
            var track = RewardTracks.For(character);

            foreach (var elemental in track.CollectedElementalTotals(character.claimedTrackLevel))
            {
                DamageType type = elemental.Key;
                int n = elemental.Value;
                if (n <= 0) continue;

                found = found ?? new List<ModifierEffect>();
                found.Add(new ModifierEffect(
                    own == type
                        ? ModifierEffectType.ElementalDamageOnHitPercent
                        : ModifierEffectType.ElementalDamagePercent,
                    n, against: type));
            }

            return found == null ? ModifierEffectSet.Empty : new ModifierEffectSet(found);
        }

        // ONE ITEM INSTANCE's rolled modifiers, scaled -- the shared seam
        // ModifierEffects(character) above and the UI layer's tooltip text
        // (Core.ItemDescription.ModifierLines) both walk through, so there is
        // exactly one place `base x TierMultiplier(itemTier) x
        // RiftMultiplier(riftTier)` is ever computed (see ModifierMagnitude's
        // own header). A character-wide caller sums this across every worn
        // slot into one flattened ModifierEffectSet; a UI caller wants each
        // effect kept beside the MODIFIER it came from (which ModifierEffects'
        // flattened bag deliberately does not preserve — see
        // ModifierEffectSet's own "MAX, NOT SUM" header), which is why this
        // yields the pair rather than the bare effect.
        //
        // PUBLIC as ModifierEffectsForItem below, for exactly one item at a
        // time with no CombatantState/Character in scope at all — a Reckoning
        // offer or an unequipped bag stack has never been "worn" and has no
        // ActiveLoadout entry to walk.
        private static IEnumerable<(ModifierDefinition Modifier, ModifierEffect Effect)> ScaledModifierEffectsForItem(
            int itemTier, RiftTier riftTier, IReadOnlyList<string> modifierIds)
        {
            if (modifierIds == null || modifierIds.Count == 0)
            {
                yield break;
            }

            double scale = ModifierMagnitude.Scale(itemTier, riftTier);

            foreach (string modifierId in modifierIds)
            {
                var modifier = GetModifier(modifierId);
                if (modifier == null)
                {
                    continue;
                }

                foreach (var raw in modifier.Data.Effects)
                {
                    // Threshold/Against/AgainstMagical pass through UNSCALED
                    // -- Threshold is a health-gate percentage, not a power
                    // number, and Against/AgainstMagical are selectors, not
                    // magnitudes. Only Magnitude itself rides the tier/rift
                    // curve.
                    int scaledMagnitude = Rounding.AwayFromZero((float)(raw.Magnitude * scale));
                    yield return (modifier, new ModifierEffect(raw.Type, scaledMagnitude, raw.Threshold, raw.Against, raw.AgainstMagical));
                }
            }
        }

        // The UI-facing door into ScaledModifierEffectsForItem above — every
        // rolled modifier ONE item copy carries, each already scaled to that
        // copy's own tier and rolled RiftTier, kept paired with the
        // ModifierDefinition it came from so a caller can group by
        // displayName (Core.ItemDescription.ModifierLines does exactly this).
        // Never recomputes the tier/rift formula itself — see this method's
        // private helper for why that would be the one thing never allowed to
        // have two copies.
        public static IReadOnlyList<(ModifierDefinition Modifier, ModifierEffect Effect)> ModifierEffectsForItem(
            int itemTier, RiftTier riftTier, IReadOnlyList<string> modifierIds)
        {
            EnsureLoaded();
            return ScaledModifierEffectsForItem(itemTier, riftTier, modifierIds).ToList();
        }

        // The skills a character's unlocked talents have put on their combat
        // strip, in the same authored order the level-unlocked ones use.
        //
        // Separate from AvailableSkillsFor's level ladder rather than merged
        // into it, because the two answer different questions and both
        // answers are wanted separately: "what has levelling given them" is
        // still what the character sheet wants to show, while the fight
        // strip wants the union. AvailableSkillsFor returns the union.
        public static IReadOnlyList<SkillDefinition> TalentGrantedSkillsFor(Character character)
        {
            EnsureLoaded();

            if (character == null)
            {
                return new List<SkillDefinition>();
            }

            var granted = new List<SkillDefinition>();
            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = GetTalent(talentId);
                if (talent == null || string.IsNullOrEmpty(talent.Data.GrantsSkillId))
                {
                    continue;
                }

                var skill = GetSkill(talent.Data.GrantsSkillId);

                // Owner-checked. A talent may only grant its OWN character's
                // skill: AvailableSkillsFor filters the level ladder by
                // characterId, and a granted skill bypassing that filter is
                // the one route by which Shawn could end up holding the
                // owl's kit. Content validation names the same mistake at
                // build time; this is the runtime half of it.
                if (skill != null && skill.Data.CharacterId == character.definitionId && !granted.Contains(skill))
                {
                    granted.Add(skill);
                }
            }

            return granted;
        }

        // The ItemDefinition in a character's Weapon 1 hand, or null if that
        // hand is empty / the id no longer resolves / the id points at
        // something that isn't actually a Weapon / it is worn but INERT.
        //
        // Active-aware — the one behavioural change ActiveLoadout brought to
        // an existing method. FightController.Encounter reads this
        // specifically to decide WeaponScaling, so an inert main-hand
        // weapon returning here anyway would let a requirement the resolver
        // already rejected still amplify the basic Attack.
        public static ItemDefinition EquippedWeapon(Character character)
        {
            EnsureLoaded();

            if (character?.equipment == null)
            {
                return null;
            }

            if (!ActiveLoadout(character).IsLive(EquipmentSlot.Weapon1))
            {
                return null;
            }

            var item = GetItem(character.equipment.Get(EquipmentSlot.Weapon1));
            return item != null && item.kind == ItemKind.Weapon ? item : null;
        }

        // THE damage number -- balance redesign Phase 3 (D3). The honed
        // WeaponPower of whatever is LIVE in the main hand right now, or
        // null when the hand is empty / inert / holds something that is not
        // a Weapon -- see EquippedWeapon's own header for why "live" matters
        // here, and ItemDefinition.WeaponPowerAt for the formula itself.
        //
        // Null, not zero, on purpose: FightEncounterAdapter reads null as
        // "fall back to the character's own authored Attack, unmultiplied"
        // (the unarmed case) rather than as "this character hits for
        // nothing", which a 0 would silently read as if the caller forgot
        // to check it.
        //
        // Reads the SAME ActiveLoadout resolve every other Effective*
        // reader here shares, rather than a second independent walk of
        // equipment.
        public static int? EquippedWeaponPower(Character character)
        {
            EnsureLoaded();

            if (character?.equipment == null)
            {
                return null;
            }

            foreach (var (entry, item) in ActiveLoadout(character).LiveEntries)
            {
                if (entry.slot == EquipmentSlot.Weapon1 && item.kind == ItemKind.Weapon)
                {
                    return item.WeaponPowerAt(entry.plus);
                }
            }

            return null;
        }

        // A character's ability scores including every talent they've
        // unlocked and every LIVE piece of worn gear — a one-line delegate
        // onto ActiveLoadout now, which is where the actual resolve lives.
        public static AbilityScoreBlock EffectiveAbilityScores(Character character)
        {
            return ActiveLoadout(character).Scores;
        }

        // THE POOL ROW A CHARACTER'S primaryPoolId NAMES, and the one place
        // that lookup is written. Falls back to the mana row when the id
        // resolves to nothing -- which the content build already refuses, so
        // reaching the fallback means the catalogue was swapped out from
        // under a save rather than that an author typed the wrong thing.
        // Graceful degradation, the house posture: a save whose pool went
        // missing plays as mana instead of throwing.
        public static PoolDefinition PrimaryPoolFor(string primaryPoolId)
        {
            EnsureLoaded();

            var named = string.IsNullOrWhiteSpace(primaryPoolId) ? null : GetPool(primaryPoolId);
            return named ?? GetPool(ManaPoolId);
        }

        // THE SAME LOOKUP FROM THE OTHER END: a character id, not a pool id.
        // Callers that start from a character were writing the two-step out
        // (CharacterDossierController, ItemDescription); stating it once is
        // what keeps a later change to the fallback from reaching some of
        // them and not others.
        public static ResolvedPool PrimaryPoolOf(string characterId)
        {
            var definition = GetCharacter(characterId);
            return PrimaryPoolFor(definition?.Data?.PrimaryPoolId)?.Data;
        }

        // WHETHER THIS CHARACTER CAN CARRY A SPELL BOOK AT ALL (plan P6), and
        // the only place a character id becomes that answer. The rule itself
        // is SpellBooks.CanHold; this is the id -> pool step in front of it,
        // written once so the shop, the dossier, the orchestrator and the save
        // reconcile cannot come to different conclusions about the same
        // character.
        public static bool CanHoldSpellBooks(string characterId) =>
            SpellBooks.CanHold(PrimaryPoolOf(characterId));

        // A character's max primary-pool capacity, every source summed.
        //
        // THE BASE IS THE ROW, not a constant: GameplayConstants.
        // DefaultMaxMana was the other copy of pools.json's `capacity: 30`
        // and is gone with this phase, so there is exactly one place the
        // number 30 is written and it is the file an author edits.
        //
        // EVERY BONUS IS WisdomDerived-ONLY (plan P3, amended by attack point
        // 3). For a Fixed pool the authored capacity is the WHOLE number from
        // every source -- that is the difference between the two rules and
        // the entire reason capacityRule is an enum. The consequence, priced
        // in rather than hidden: a Max Mana talent or relic is a dead pick
        // for a Fixed pool's owner, and the sheet says so by dropping the
        // Wisdom link (SheetStats.FedBy).
        //
        // NO RELICS AND NO GEAR HERE -- both need the drafted relic list and
        // the resolved modifier set, which only the encounter adapter holds.
        // This is the sheet's number and the base BuildPrimaryPool finishes;
        // passing a null modifier list is the identity case of
        // RelicModifiers.Apply, so the two share one rule rather than two.
        public static int EffectiveMaxMana(Character character)
        {
            EnsureLoaded();

            var definition = GetCharacter(character.definitionId);
            var pool = PrimaryPoolFor(definition?.Data?.PrimaryPoolId);
            if (pool?.Data == null) return 0;

            return PoolPrecedence.Capacity(pool.Data, MaxManaBonuses(character), null, 0);
        }

        // EVERY ADDITIVE Max Mana TERM A CHARACTER CARRIES, summed. Whether
        // any of it counts is PoolPrecedence's decision, not this method's --
        // the sum is the same arithmetic for both rules and only one of them
        // spends it.
        private static int MaxManaBonuses(Character character)
        {
            int total = 0;

            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = GetTalent(talentId);
                if (talent != null)
                {
                    total += talent.Data.MaxManaBonus;
                }
            }

            total += AbilityDerivation.MaxManaBonus(EffectiveAbilityScores(character));

            // The reward track's max-mana nodes, read live off the watermark
            // (§2). Reaches the fight through FightEncounterAdapter, the same
            // one seam every other mana term already goes through.
            total += RewardTracks.For(character)
                .CollectedTotal(TrackReward.MaxMana, character.claimedTrackLevel);

            return total;
        }

        // THE WHOLE CAPACITY CHAIN, ENDED. One statement of precedence for
        // the primary pool, in the order the numbers actually apply:
        //
        //   1. the row's authored capacity           (always)
        //   2. talents, Wisdom, the reward track     (WisdomDerived only)
        //   3. RelicStat.MaxMana                     (WisdomDerived only)
        //   4. ModifierEffectType.FlatMaxManaBonus   (WisdomDerived only)
        //
        // Steps 1-2 are EffectiveMaxMana; 3 and 4 are here because they need
        // the drafted relics and the resolved gear the adapter holds. The
        // arithmetic is unchanged from what FightEncounterAdapter did inline
        // before this phase -- relic percent/flat first, then the gear
        // modifier on top -- so a Runic wearer's number is the same number.
        //
        // GAIN PER TURN TAKES THE SAME SHAPE as capacity, and for the same
        // reason: the row's authored gainPerTurn is a BASE, and for a
        // WisdomDerived pool EffectiveStats.manaRegen (Wisdom + gear +
        // talents + track) and FlatManaRegenBonus are added on top; for a
        // Fixed pool the authored number is the whole income. Mana authors 0
        // because every point of its regen is derived -- which is why the row
        // reading 0 does NOT mean mana stopped regenerating. manaRegen is
        // passed in rather than re-derived because the adapter has already
        // resolved the stat block.
        public static ResourcePool BuildPrimaryPool(Character character,
                                                    IReadOnlyList<RelicModifier> modifiers,
                                                    ModifierEffectSet modifierEffects,
                                                    int manaRegen)
        {
            EnsureLoaded();

            var definition = GetCharacter(character.definitionId);
            var pool = PrimaryPoolFor(definition?.Data?.PrimaryPoolId);
            if (pool?.Data == null) return null;

            var effects = modifierEffects ?? ModifierEffectSet.Empty;

            int capacity = PoolPrecedence.Capacity(pool.Data, MaxManaBonuses(character), modifiers,
                effects.Best(ModifierEffectType.FlatMaxManaBonus));
            int gainPerTurn = PoolPrecedence.GainPerTurn(pool.Data,
                manaRegen + effects.Best(ModifierEffectType.FlatManaRegenBonus));

            return new ResourcePool(pool.Data, capacity, gainPerTurn);
        }

        // The same chain for the two paths that have a DEFINITION and no
        // save: the tooling party (opening the Fight scene directly) and
        // every enemy. Neither has talents, a reward track or gear, so only
        // the ability-score term and the relic modifiers survive -- and an
        // enemy has neither of those either, which is why it passes neutral
        // scores and a null modifier list and gets exactly the row's
        // authored capacity, as it always did.
        public static ResourcePool BuildPrimaryPool(string primaryPoolId,
                                                    AbilityScoreBlock scores,
                                                    IReadOnlyList<RelicModifier> modifiers,
                                                    int manaRegen)
        {
            EnsureLoaded();

            var pool = PrimaryPoolFor(primaryPoolId);
            if (pool?.Data == null) return null;

            return new ResourcePool(pool.Data,
                PoolPrecedence.Capacity(pool.Data, AbilityDerivation.MaxManaBonus(scores), modifiers, 0),
                PoolPrecedence.GainPerTurn(pool.Data, manaRegen));
        }

        // A character's actual Skill mana cost including their level's
        // spell tier and every talent they've unlocked. Floored at 1 rather
        // than 0 — Skill should stay a real resource decision, never a
        // second free Attack.
        public static int EffectiveSkillManaCost(Character character)
        {
            EnsureLoaded();

            var tier = GetSpellTierForLevel(character.level, EffectiveAbilityScores(character));
            int total = tier != null ? tier.Data.ManaCost : CombatMath.SkillManaCost;

            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = GetTalent(talentId);
                if (talent != null)
                {
                    total -= talent.Data.SkillManaCostReduction;
                }
            }

            return Mathf.Max(1, total);
        }

        // A character's Skill power multiplier from their level's spell tier
        // ALONE — talents don't affect this today, only the mana side of the
        // economy (see EffectiveSkillManaCost / maxManaBonus). Falls back to
        // the same baseline CombatMath.SkillManaCost implies (level 1's 1.5x)
        // if no spell tiers loaded at all.
        //
        // Ability scores are DELIBERATELY NOT folded in here any more — see
        // AbilityDerivation's own header for why a flat per-point bonus
        // blended into this number was a second, uncoordinated answer to a
        // question EffectiveSkillScaling below already answers. This is now
        // exactly as level-only as EffectiveStats' weapon-agnostic Attack is:
        // the scaling happens once, downstream, in CombatMath.
        // ComputeSkillDamage, multiplying THIS value by SkillScaling's own
        // multiplier — never blended into it beforehand.
        public static float EffectiveSkillPowerMultiplier(Character character)
        {
            EnsureLoaded();

            var tier = GetSpellTierForLevel(character.level, EffectiveAbilityScores(character));
            return tier != null ? tier.Data.PowerMultiplier : 1.5f;
        }

        // What a character's PLAIN SWING rides: the main-hand weapon's own
        // `scaling` grade, and nothing else. A swing is one weapon (see
        // ScalingSet's own header) so, unlike EffectiveSkillScaling below,
        // the off hand never contributes here even when one is worn.
        //
        // FOUND MISSING, not designed this way. CombatantState.WeaponScaling/
        // SkillScaling existed, CombatMath/SkillResolution already read them,
        // and an extensive EditMode suite already pinned the arithmetic --
        // but nothing in the real adapter path (FightEncounterAdapter.
        // ToCombatant) ever WROTE them, so every real fight built its
        // combatants with ScalingSet.None regardless of what was equipped.
        // A Strength build and a Dexterity build swung for identical damage
        // off identical Attack, because the one thing meant to tell them
        // apart was never plugged in.
        public static ScalingSet EffectiveWeaponScaling(Character character)
        {
            EnsureLoaded();

            var loadout = ActiveLoadout(character);
            var mainHand = ScalingProfile.None;
            foreach (var (entry, item) in loadout.LiveEntries)
            {
                if (entry.slot == EquipmentSlot.Weapon1)
                {
                    mainHand = item.scaling;
                    break;
                }
            }

            return new ScalingSet(ScalingProfile.None, mainHand, ScalingProfile.None);
        }

        // Every axis a character's Skill rides right now: the spell tier
        // their level grants, PLUS whatever spellScaling their weapons
        // contribute — a staff in the main hand, an off-hand item, or both.
        //
        // A separate SET from the weapon's own basic-Attack scaling on
        // purpose. A Strength build swings hard and casts weakly, an
        // Intelligence build the reverse, and the same character cannot be
        // excellent at both — which is what makes finding the right spell as
        // interesting as finding the right sword. Off hand contributes here
        // (see ScalingSet's own off-hand rule) even though it never
        // contributes to WeaponScaling: a swing is one weapon, a cast is not.
        //
        // Reads LIVE entries only, off the SAME ActiveLoadout resolve this
        // also needs for the tier check — an inert staff contributes
        // nothing here either, same as it contributes no stat bonus.
        public static ScalingSet EffectiveSkillScaling(Character character)
        {
            EnsureLoaded();

            var loadout = ActiveLoadout(character);
            var tier = GetSpellTierForLevel(character.level, loadout.Scores);
            var spellTierProfile = tier != null ? tier.Data.Scaling : ScalingProfile.None;

            var mainHand = ScalingProfile.None;
            var offHand = ScalingProfile.None;
            foreach (var (entry, item) in loadout.LiveEntries)
            {
                if (entry.slot == EquipmentSlot.Weapon1)
                {
                    mainHand = item.spellScaling;
                }
                else if (entry.slot == EquipmentSlot.Weapon2)
                {
                    offHand = item.spellScaling;
                }
            }

            return new ScalingSet(spellTierProfile, mainHand, offHand);
        }

        // The display name of the spell a character's Skill action actually
        // casts right now — "Spark" at level 1, up to "Ascendance" at level
        // 9 with the default spells.json. Falls back to "Skill" so combat
        // text never shows an empty name if content failed to load.
        public static string EffectiveSkillDisplayName(Character character)
        {
            EnsureLoaded();

            var tier = GetSpellTierForLevel(character.level, EffectiveAbilityScores(character));
            return tier != null ? tier.Data.DisplayName : "Skill";
        }
    }
}
