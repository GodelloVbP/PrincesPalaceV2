using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // Turns content into glossary rows.
    //
    // THE ONLY PLACE that knows what a relic is, versus what a monster is. The
    // screen builds a rail from the enum and a list from whatever this returns,
    // so adding a category is a value in GlossaryCategory plus one case here --
    // never a new screen, never a new controller.
    //
    // Core rather than Domain because every source but one is a
    // ScriptableObject -- Mechanics() is the exception, and stays here
    // anyway so all six categories are built in the one place this file's
    // own job description promises.
    public static class GlossaryEntries
    {
        // EVERY category in one pass, with the achievement facts gathered
        // ONCE.
        //
        // The controller needs all six regardless -- the rail shows an
        // "unlocked of total" count per category -- and building them one at a
        // time meant Relics and Deeds each re-derived the same AchievementFacts
        // by sweeping the roster and the whole run ledger. Six calls, two full
        // fact gathers, repeated on every category click.
        public static List<List<GlossaryEntry>> All(SaveData save)
        {
            var facts = Achievements.FactsFor(save);
            var earned = Achievements.EarnedIdsFrom(facts);

            var byCategory = new List<List<GlossaryEntry>>();
            foreach (var category in GlossaryCatalog.Categories)
            {
                byCategory.Add(For(category, earned, facts));
            }

            return byCategory;
        }

        private static List<GlossaryEntry> For(GlossaryCategory category,
            HashSet<string> earned, AchievementFacts facts)
        {
            switch (category)
            {
                case GlossaryCategory.Relics: return Relics(earned);
                case GlossaryCategory.Monsters: return Monsters();
                case GlossaryCategory.Spells: return Spells();
                case GlossaryCategory.Items: return Items();
                case GlossaryCategory.Talents: return Talents();
                case GlossaryCategory.Mechanics: return Mechanics();
                case GlossaryCategory.Achievements: return Deeds(facts);
                default: return new List<GlossaryEntry>();
            }
        }

        // Relics are the only category with a real lock today: one is gated on
        // an achievement, and the glossary is where a player finds out that the
        // gate exists at all.
        private static List<GlossaryEntry> Relics(HashSet<string> earned)
        {
            return ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r =>
                {
                    bool locked = !r.Data.IsUnlockedFromTheStart && !earned.Contains(r.Data.UnlockedBy);
                    return new GlossaryEntry(
                        r.id, r.Data.DisplayName,
                        RelicRarityNames.Of(r.Data.Rarity),
                        r.Data.Description,
                        locked,
                        locked ? AchievementName(r.Data.UnlockedBy) : "",
                        r.id);
                })
                .ToList();
        }

        private static List<GlossaryEntry> Monsters() =>
            ContentDatabase.Enemies
                .Where(e => e != null)
                .Select(e => new GlossaryEntry(
                    e.id, e.Data.DisplayName,
                    // Off BaseStats, which is where an enemy's numbers actually
                    // live -- there are no maxHealth/attack fields beside it.
                    $"{e.Data.BaseStats.maxHealth} HP  ·  {e.Data.BaseStats.attack} ATK{(e.Data.IsBoss ? "  ·  BOSS" : "")}",
                    // Enemies carry no authored prose, so the row says what it
                    // actually knows rather than showing an empty plate.
                    AffinityLine(e),
                    iconId: e.id))
                .ToList();

        // "Weak to Fire and Ice. Resists Physical." — and each half disappears
        // rather than reading "Weak to ." when a monster is authored with none.
        // A monster with neither says so outright: a blank description row looks
        // like content that failed to load.
        private static string AffinityLine(EnemyDefinition enemy)
        {
            // READ ONCE EACH. Weaknesses and Resistances materialise a fresh
            // list on every get (see ElementalAffinity.Listed), so asking twice
            // per half built four lists to print two sentences.
            var affinity = enemy.Data.Affinity;
            var weaknesses = affinity.Weaknesses;
            var resistances = affinity.Resistances;
            var parts = new List<string>();

            if (weaknesses.Count > 0)
            {
                parts.Add($"Weak to {Listed(weaknesses)}.");
            }

            if (resistances.Count > 0)
            {
                parts.Add($"Resists {Listed(resistances)}.");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "Takes every element the same way.";
        }

        private static string Listed(IReadOnlyList<DamageType> types)
        {
            if (types.Count == 1) return types[0].ToString();

            return string.Join(", ", types.Take(types.Count - 1)) + " and " + types[types.Count - 1];
        }

        private static List<GlossaryEntry> Spells() =>
            ContentDatabase.Skills
                .Where(s => s != null)
                .Select(s => new GlossaryEntry(
                    s.id, s.Data.DisplayName,
                    SpellCost(s),
                    s.Data.Description,
                    iconId: s.id))
                .ToList();

        // PRICED IN THE CASTER'S OWN RESOURCE, not in "MANA".
        //
        // This was the last literal "MANA" left outside the fight HUD, and the
        // change 52793e1b made everywhere else says why it cannot stay: "BOTH
        // TAGS COME FROM THE CASTER, not from this file. 'MP' used to be a
        // literal here, which was true only for as long as every caster spent
        // mana: `manaCost` means 'spent from the primary pool'." The glossary
        // has the owner in hand and was the one surface that never got the
        // message -- inert today only because Bjorn's slam and brace are
        // manaCost 0, and pools.json's own note promises the balance pass that
        // ends that.
        //
        // FALLS BACK TO A BARE NUMBER on a blank tag, which is the same
        // fallback FightHudModel.CostLabel already uses: a number with no unit
        // is incomplete, a number with the WRONG unit is a lie.
        private static string SpellCost(SkillDefinition skill)
        {
            int cost = skill.Data.ManaCost;
            if (cost <= 0) return "NO COST";

            string tag = ContentDatabase.PrimaryPoolOf(skill.Data.CharacterId)?.ShortTag;
            return string.IsNullOrWhiteSpace(tag) ? cost.ToString() : $"{cost} {tag.ToUpperInvariant()}";
        }

        private static List<GlossaryEntry> Items() =>
            ContentDatabase.Items
                .Where(i => i != null)
                .Select(i => new GlossaryEntry(
                    i.id, i.displayName,
                    $"{i.kind}  ·  TIER {i.tier}",
                    i.description,
                    iconId: i.id))
                .ToList();

        private static List<GlossaryEntry> Talents() =>
            ContentDatabase.Talents
                .Where(t => t != null)
                .Select(t => new GlossaryEntry(
                    t.id, t.Data.DisplayName,
                    t.Data.CharacterId,
                    t.Data.Description,
                    iconId: t.id))
                .ToList();

        // BALANCE REDESIGN PHASE 6 (D7.4): the eight facts a player needs to
        // read the sheet and the item tooltips at all -- what each ability
        // score does, what a Defense number is worth, and what honing buys.
        // Hand-authored rather than swept off a ScriptableObject, because
        // none of these eight are content -- they are the rules content is
        // measured against, and there is no *Definition asset for a rule.
        //
        // The numbers here are lifted straight from AbilityDerivation's own
        // constants and ItemUpgrade's own curve rather than retyped by hand,
        // so a retuned rate updates this page for free instead of silently
        // going stale next to the code it is describing.
        private static List<GlossaryEntry> Mechanics()
        {
            return new List<GlossaryEntry>
            {
                new GlossaryEntry("mech_strength", "Strength", "ABILITY SCORE",
                    "Multiplies STR-graded weapons: every point above 10 hits harder, every point below hits " +
                    "softer. Grants no other stat -- offense lives at the weapon, not the sheet."),
                new GlossaryEntry("mech_dexterity", "Dexterity", "ABILITY SCORE",
                    $"+1 Speed for every {AbilityDerivation.SpeedDivisor} points above 10 (and the same " +
                    "below). Also multiplies DEX-graded weapons, the same way Strength does for STR ones."),
                new GlossaryEntry("mech_constitution", "Constitution", "ABILITY SCORE",
                    $"+{AbilityDerivation.HealthPerPoint} Max Health and +{AbilityDerivation.PhysicalDefensePerPoint} " +
                    "Physical Defense per point above 10 -- and the same amount lost per point below it."),
                new GlossaryEntry("mech_intelligence", "Intelligence", "ABILITY SCORE",
                    "Multiplies INT-graded spells, the same way Strength multiplies STR-graded weapons. Grants " +
                    "no other stat."),
                new GlossaryEntry("mech_wisdom", "Wisdom", "ABILITY SCORE",
                    $"+{AbilityDerivation.ManaPerPoint} Max Mana and +{AbilityDerivation.MagicalDefensePerPoint} " +
                    "Magical Defense per point above 10, and multiplies WIS-graded spells on top."),
                new GlossaryEntry("mech_charisma", "Charisma", "ABILITY SCORE",
                    $"Fills this character's signature resource faster: +1 gain per turn for every " +
                    $"{AbilityDerivation.SignatureGainDivisor} points above 10 (and slower for every " +
                    $"{AbilityDerivation.SignatureGainDivisor} below)."),
                new GlossaryEntry("mech_defense", "Physical & Magical Defense", "MECHANIC",
                    "Defense cuts incoming damage along a curve, not a flat percentage: damage taken is " +
                    "multiplied by 100 / (100 + Defense). " +
                    $"25 Defense blocks {ItemStatLines.DamageReductionPercent(25)}% of a hit, " +
                    $"50 blocks {ItemStatLines.DamageReductionPercent(50)}%, " +
                    $"100 blocks {ItemStatLines.DamageReductionPercent(100)}%, " +
                    $"300 blocks {ItemStatLines.DamageReductionPercent(300)}% -- every extra point buys a " +
                    "little less than the one before it, so no amount of Defense makes a character " +
                    "untouchable."),
                new GlossaryEntry("mech_hone", "Honing", "MECHANIC",
                    $"Honing raises a weapon's damage by {ItemUpgrade.PercentPerPlus}% per plus, up to " +
                    $"+{ItemUpgrade.MaxPlus} -- a fully honed weapon deals {1f + ItemUpgrade.PercentPerPlus * ItemUpgrade.MaxPlus / 100f:0.0}x " +
                    "its base damage. Worn armour hones the same way, for whatever stats it grants."),
            };
        }

        // Deeds are listed whether or not they are done, and the LOCK here
        // means "not achieved" rather than "hidden" -- a player has to be able
        // to read what is left to do.
        private static List<GlossaryEntry> Deeds(AchievementFacts facts)
        {
            return ContentDatabase.Achievements
                .Where(a => a != null)
                .Select(a =>
                {
                    bool earned = AchievementProgress.IsEarned(a.Data, facts);
                    return new GlossaryEntry(
                        a.id, a.Data.DisplayName,
                        earned ? "EARNED" : "NOT YET",
                        a.Data.Description,
                        locked: !earned,
                        lockedBy: "",
                        iconId: a.id);
                })
                .ToList();
        }

        private static string AchievementName(string achievementId)
        {
            var definition = ContentDatabase.GetAchievement(achievementId);

            return definition == null ? achievementId : definition.Data.DisplayName;
        }

    }
}
