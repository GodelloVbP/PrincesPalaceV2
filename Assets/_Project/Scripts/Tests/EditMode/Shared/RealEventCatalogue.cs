using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE REAL events.json, RESOLVED AGAINST EVERY CATALOGUE IT CAN NAME:
    // characters, items, relics and the active enemies with their slotSpans,
    // in ContentBuilder's order (relics and enemies build before events).
    //
    // Each fixture that read the real file used to hand the resolver its own
    // partial catalogue ("kinship" only, no enemies), which held only while no
    // event named anything else. The Bell (M7a) is the first event with a
    // fight and a second relic, and every one of those partial catalogues
    // refused it at once. One loader, so the next event that names a new id
    // breaks nothing here.
    internal static class RealEventCatalogue
    {
        internal static Dictionary<string, string> CharacterNames() =>
            ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters
                .ToDictionary(c => c.id, c => c.displayName);

        internal static List<string> ItemIds() =>
            ContentDataFiles.ParseFile<RawItemFile>(ContentDataFiles.DataPath("items.json")).items
                .Select(i => i.id).ToList();

        internal static List<ResolvedRelic> Relics()
        {
            var achievements = ContentDataFiles.ParseFile<RawAchievementFile>(ContentDataFiles.DataPath("achievements.json"))
                .achievements.Select(a => a.id).ToList();
            var raw = ContentDataFiles.ParseFile<RawRelicFile>(ContentDataFiles.DataPath("relics.json")).relics;

            bool ok = RelicEntryResolver.TryResolveAll(raw, achievements, CharacterNames().Keys.ToList(),
                out var resolved, out var errors);
            Assert.IsTrue(ok, "relics.json: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        internal static List<ResolvedEnemy> Enemies()
        {
            var raw = ContentDataFiles.ParseFile<RawEnemyFile>(ContentDataFiles.DataPath("enemies.json")).enemies;
            bool ok = EnemyEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "enemies.json: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        internal static List<ResolvedEventDefinition> Events()
        {
            var relicNames = Relics().ToDictionary(r => r.Id, r => r.DisplayName);
            var enemySlotSpans = Enemies().Where(e => e.Active).ToDictionary(e => e.Id, e => e.SlotSpan);
            var raw = ContentDataFiles.ParseFile<RawEventFile>(ContentDataFiles.DataPath("events.json")).events;

            bool ok = EventEntryResolver.TryResolveAll(raw, CharacterNames(), ItemIds(), relicNames, enemySlotSpans,
                out var resolved, out var errors);
            Assert.IsTrue(ok, "events.json: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }
    }
}
