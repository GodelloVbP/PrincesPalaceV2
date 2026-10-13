using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    public class EffectCatalogTests
    {
        private static EffectKind[] Kinds() =>
            Enum.GetValues(typeof(EffectKind)).Cast<EffectKind>().Where(k => k != EffectKind.None).ToArray();

        [Test]
        public void EveryKind_HasACatalogRow()
        {
            var missing = Kinds().Where(k => !EffectCatalog.Has(k)).ToList();
            Assert.IsEmpty(missing, "EffectKind members with no EffectCatalog row");
            Assert.IsFalse(EffectCatalog.Has(EffectKind.None), "None is never authored, so it has no row");
        }

        // A kind nothing reads is a content row that does nothing in play.
        // The scan looks for `EffectKind.<Name>` in runtime source outside the
        // two files that only declare the vocabulary; a stat kind is read by
        // its catalog row's EffectStat, so it needs no name elsewhere.
        [Test]
        public void EveryNonStatKind_IsReadSomewhere()
        {
            string scripts = Path.Combine(RepoTree.Root(), "Assets", "_Project", "Scripts");
            var sources = new[] { "Domain", "Core" }
                .SelectMany(d => Directory.GetFiles(Path.Combine(scripts, d), "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.EndsWith("EffectKind.cs") && !f.EndsWith("EffectCatalog.cs"))
                .ToList();
            Assert.GreaterOrEqual(sources.Count, 200, "the scan found too few source files to mean anything");

            string all = string.Join("\n", sources.Select(File.ReadAllText));

            var unread = Kinds()
                .Where(k => EffectCatalog.Of(k).Stat == EffectStat.None)
                .Where(k => !Regex.IsMatch(all, @"EffectKind\." + k + @"\b"))
                .ToList();

            Assert.IsEmpty(unread, "non-stat EffectKind members no runtime code names");
        }

        [Test]
        public void AffixKinds_HaveAPool_AndOtherKindsDoNot()
        {
            foreach (var info in EffectCatalog.All)
            {
                bool affix = info.Allows(EffectHolder.Affix);
                Assert.AreEqual(affix, info.Pool != AffixPool.None, $"{info.Kind}: an affix kind needs a pool, nothing else has one");
            }
        }
    }
}
