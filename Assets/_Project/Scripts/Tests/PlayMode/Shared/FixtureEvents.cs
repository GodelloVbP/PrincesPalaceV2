using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // FIXTURE EVENTS, RESOLVED AND RIDING ON THE LOADED CATALOGUE for one
    // test: the raw entries go through the real EventEntryResolver against
    // the built characters' names, and the results are appended to
    // ContentDatabase.Events, which is the loaded List itself. The caller's
    // TearDown must call ContentDatabase.Reset() (TestGlobals.ResetAll does)
    // so the next test reloads from Resources. EventRunBuffTests' shape,
    // shared so a fixture never depends on what events.json authors.
    internal static class FixtureEvents
    {
        internal static List<ResolvedEventDefinition> Append(params RawEventEntry[] raws) =>
            Append(new Dictionary<string, string>(), raws);

        internal static List<ResolvedEventDefinition> Append(Dictionary<string, string> relicNames, params RawEventEntry[] raws)
        {
            var characters = ContentDatabase.Characters
                .Where(c => c != null && c.Data != null)
                .ToDictionary(c => c.Data.Id, c => c.Data.DisplayName);

            bool ok = EventEntryResolver.TryResolveAll(raws.ToList(), characters, new string[0], relicNames,
                out var resolved, out var errors);
            Assert.IsTrue(ok, "fixture events do not resolve: " + string.Join("; ", errors ?? new List<string>()));

            var events = (List<EventDefinition>)ContentDatabase.Events;
            var dataField = typeof(EventDefinition).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(dataField, "EventDefinition no longer has a private 'data' field to seed");
            foreach (var definition in resolved)
            {
                var asset = ScriptableObject.CreateInstance<EventDefinition>();
                dataField.SetValue(asset, definition);
                events.Add(asset);
            }

            return resolved;
        }
    }
}
