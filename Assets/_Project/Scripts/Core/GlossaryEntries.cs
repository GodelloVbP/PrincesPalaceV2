using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Glossary;

namespace PrincesPalace
{
    // Turns content into glossary rows.
    //
    // THE ONLY PLACE that knows what a relic is, versus what a monster is. The
    // screen builds a rail from the enum and a list from whatever this returns,
    // so adding a category is a value in GlossaryCategory plus one case here --
    // never a new screen, never a new controller.
    //
    // Core rather than Domain because every source is a ScriptableObject.
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
                    bool locked = !r.IsUnlockedFromTheStart && !earned.Contains(r.unlockedBy);
                    return new GlossaryEntry(
                        r.id, r.displayName,
                        RelicRarityNames.Of(r.rarity),
                        r.description,
                        locked,
                        locked ? AchievementName(r.unlockedBy) : "",
                        r.id);
                })
                .ToList();
        }

        private static List<GlossaryEntry> Monsters() =>
            ContentDatabase.Enemies
                .Where(e => e != null)
                .Select(e => new GlossaryEntry(
                    e.id, e.displayName,
                    // Off baseStats, which is where an enemy's numbers actually
                    // live -- there are no maxHealth/attack fields on the
                    // definition itself.
                    $"{e.baseStats.maxHealth} HP  ·  {e.baseStats.attack} ATK{(e.isBoss ? "  ·  BOSS" : "")}",
                    // Enemies carry no authored prose, so the row says what it
                    // actually knows rather than showing an empty plate.
                    $"Weak to {e.weakness}. Resists {e.resistance}.",
                    iconId: e.id))
                .ToList();

        private static List<GlossaryEntry> Spells() =>
            ContentDatabase.Skills
                .Where(s => s != null)
                .Select(s => new GlossaryEntry(
                    s.id, s.displayName,
                    s.manaCost > 0 ? $"{s.manaCost} MANA" : "NO COST",
                    s.description,
                    iconId: s.id))
                .ToList();

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
                    t.id, t.displayName,
                    t.characterId,
                    t.description,
                    iconId: t.id))
                .ToList();

        // Deeds are listed whether or not they are done, and the LOCK here
        // means "not achieved" rather than "hidden" -- a player has to be able
        // to read what is left to do.
        private static List<GlossaryEntry> Deeds(AchievementFacts facts)
        {
            return ContentDatabase.Achievements
                .Where(a => a != null)
                .Select(a =>
                {
                    bool earned = AchievementProgress.IsEarned(a.ToResolved(), facts);
                    return new GlossaryEntry(
                        a.id, a.displayName,
                        earned ? "EARNED" : "NOT YET",
                        a.description,
                        locked: !earned,
                        lockedBy: "",
                        iconId: a.id);
                })
                .ToList();
        }

        private static string AchievementName(string achievementId)
        {
            var definition = ContentDatabase.Achievements
                .FirstOrDefault(a => a != null && a.id == achievementId);

            return definition == null ? achievementId : definition.displayName;
        }

    }
}
