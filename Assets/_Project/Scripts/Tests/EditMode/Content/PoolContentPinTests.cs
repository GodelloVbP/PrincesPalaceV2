using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Reads Assets/_Project/ContentData/pools.json from disk through the REAL
    // resolver and pins the shipped `mana` row as literals -- the
    // RewardTrackContentPinTests shape, and for the same reason: this row
    // restates behaviour that used to live in code, and a silent edit to it
    // would change what every character's meter holds without any test
    // noticing.
    //
    // TWO PARSERS, ONE ASSERTION, same as ContentStampIdsTests: no
    // UnityEngine dependency here, so this is a [D] class that runs under
    // plain `dotnet test`, which has no JsonUtility. ContentDataFiles picks
    // whichever parser its host has.
    public class PoolContentPinTests
    {
        private static List<ResolvedPool> Pools()
        {
            var raw = ContentDataFiles.ParseFile<RawPoolFile>(ContentDataFiles.DataPath("pools.json")).pools;
            bool ok = PoolEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "pools.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static ResolvedPool Mana() =>
            Pools().SingleOrDefault(p => p.Id == "mana")
            ?? throw new AssertionException("pools.json has no 'mana' row; every character's primaryPoolId " +
                                            "defaults to it, so the catalogue cannot ship without one.");

        [Test]
        public void ManaIsTheOnlyPoolShippedThisPhase()
        {
            // Fury is phase E. Pinned so the row cannot arrive without
            // somebody reading this test, and the two phases' gates stay
            // separable.
            CollectionAssert.AreEqual(new[] { "mana" }, Pools().Select(p => p.Id).ToList());
        }

        [Test]
        public void ManaRestatesTheBehaviourItReplaces()
        {
            var mana = Mana();

            Assert.AreEqual("Mana", mana.DisplayName);
            Assert.AreEqual("MP", mana.ShortTag);

            // WisdomDerived is mana as it has always worked: this capacity is
            // a BASE that AbilityDerivation's Wisdom bonus, talents, relics,
            // the reward track and FlatMaxManaBonus all add to.
            Assert.AreEqual(PoolCapacityRule.WisdomDerived, mana.CapacityRule);

            // 30 IS A DUPLICATE OF GameplayConstants.DefaultMaxMana, and it
            // is written out as a literal here because that is the whole
            // point of the pin. Domain cannot reference Core, so for this
            // one phase the number exists twice; phase B is what deletes one
            // of the two copies, and this literal is what makes a drift
            // between them fail rather than pass.
            Assert.AreEqual(30, mana.Capacity);

            // Mana is a budget: it opens full and only goes down.
            Assert.AreEqual(PoolStartRule.Full, mana.StartRule);
            Assert.AreEqual(0, mana.StartValue);

            // gainPerTurn 0 does NOT mean mana stops regenerating. Regen is
            // still state.ManaRegen, derived by AbilityDerivation and ticked
            // by FightSession.Riders; phase B moves it into this row, and
            // authoring a gain here now would double it.
            Assert.AreEqual(0, mana.GainPerTurn);
            Assert.AreEqual(0, mana.GainOnAttack);
            Assert.AreEqual(0, mana.GainOnDamageTaken);
            Assert.AreEqual(0, mana.DecayPerIdleTurn);

            // The three switches that make mana mana rather than an exotic
            // resource.
            Assert.IsFalse(mana.Pulse);
            Assert.IsTrue(mana.AllowsSpellBooks);
            Assert.IsTrue(mana.RestoredByManaEffects);
            Assert.IsFalse(mana.AbsorbsDamage);
        }

        [Test]
        public void ManasColoursAreTheOnesTheFightHudAlreadyDraws()
        {
            // The copy cannot drift: both files are in Domain, so a [D] test
            // sees the palette constants and the authored row at once. If
            // the HUD's mana blue is ever retuned, this fails and says which
            // half was not updated.
            var mana = Mana();

            Assert.AreEqual(FightHudPalette.MpBright, mana.BrightHex);
            Assert.AreEqual(FightHudPalette.MpDeep, mana.DeepHex);
            Assert.AreEqual(FightHudPalette.MpText, mana.TextHex);
        }

        [Test]
        public void EveryShippedPoolCarriesTheThreeColoursTheMeterNeeds()
        {
            // The resolver already refuses a bad token; this is the vacuity
            // guard against a future row shipping with a token that parses
            // but is empty of meaning, and against this file quietly
            // testing nothing if pools.json ever resolves to no rows.
            var pools = Pools();
            CollectionAssert.IsNotEmpty(pools, "pools.json resolved to nothing, so every pin here is vacuous");

            foreach (var pool in pools)
            {
                Assert.IsNotEmpty(pool.BrightHex, $"{pool.Id}: brightHex");
                Assert.IsNotEmpty(pool.DeepHex, $"{pool.Id}: deepHex");
                Assert.IsNotEmpty(pool.TextHex, $"{pool.Id}: textHex");
            }
        }
    }
}
