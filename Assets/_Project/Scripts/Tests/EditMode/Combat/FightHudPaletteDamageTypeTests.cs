using System;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Every DamageType member has its OWN colour token, pinned by literal --
    // not by calling ForDamageType and comparing it to itself, which would
    // pass even if the switch mapped every member to the same string. See
    // CLAUDE.md gotcha 5 on why a recomputed expectation is a
    // tautology.
    public class FightHudPaletteDamageTypeTests
    {
        // The complete set this test knows about. A DamageType member absent
        // from here fails EveryCurrentMemberIsPinnedHere below, which is what
        // catches a seventh element added to the enum without anyone giving
        // it a deliberate colour.
        private static readonly Dictionary<DamageType, string> Expected = new Dictionary<DamageType, string>
        {
            { DamageType.Physical, "#ED423DFF" },
            { DamageType.Fire, "#FF6A2AFF" },
            { DamageType.Ice, "#9FD8F5FF" },
            { DamageType.Nature, "#5FA24AFF" },
            { DamageType.Poison, "#A8E63CFF" },
            { DamageType.Arcane, "#C69AF1FF" },
            { DamageType.Earth, "#C98A3AFF" },
            { DamageType.Water, "#3F9BE8FF" },
            { DamageType.Wind, "#A8F0E0FF" },
            { DamageType.Lightning, "#F5F06AFF" },
            { DamageType.Void, "#C22FB0FF" },
        };

        [Test]
        public void EveryPinnedMemberMapsToItsOwnToken()
        {
            foreach (var pair in Expected)
            {
                Assert.AreEqual(pair.Value, FightHudPalette.ForDamageType(pair.Key),
                    $"{pair.Key}'s popup/label colour");
            }
        }

        // No two DISTINCT DamageType members share a token -- a copy-paste
        // that gave two elements the same hex would pass the test above (both
        // sides just checked against themselves) while still failing the
        // player, who would see two "different" elements pop in one colour.
        [Test]
        public void NoTwoDamageTypesShareAToken()
        {
            var seen = new HashSet<string>();
            foreach (var pair in Expected)
            {
                Assert.IsTrue(seen.Add(pair.Value),
                    $"{pair.Key} reuses a token another DamageType already claims");
            }
        }

        // THE COVERAGE CHECK. Walks the live enum rather than a fixed list,
        // so a member added later is caught here rather than silently
        // falling through ForDamageType's own default to Physical's colour.
        [Test]
        public void EveryCurrentMemberIsPinnedHere()
        {
            foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
            {
                Assert.IsTrue(Expected.ContainsKey(type),
                    $"{type} has no pinned expectation in this test -- give it a deliberate colour above, " +
                    "not FightHudPalette.ForDamageType's Physical fallback for an unrecognised member.");
            }

            Assert.AreEqual(Enum.GetValues(typeof(DamageType)).Length, Expected.Count,
                "this test pins a DamageType member that no longer exists on the enum");
        }

        // The fallback ITSELF, exercised the only way it can be without a
        // seventh enum member: an out-of-range cast. Proves the "unknown"
        // path lands on Physical's own token rather than on null or a blank
        // string, which is what would have painted a popup white.
        [Test]
        public void AnUnrecognisedValueFallsBackToPhysicalNotWhite()
        {
            var unrecognised = (DamageType)(-1);
            Assert.AreEqual(FightHudPalette.DamageTypePhysical, FightHudPalette.ForDamageType(unrecognised));
        }
    }
}
