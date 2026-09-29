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

        private static ResolvedPool Fury() =>
            Pools().SingleOrDefault(p => p.Id == "fury")
            ?? throw new AssertionException("pools.json has no 'fury' row; Bjorn's primaryPoolId names it, " +
                                            "so the character catalogue cannot build without one.");

        [Test]
        public void TwoPoolsShip()
        {
            // ORDER IS FILE ORDER and it is asserted, not just membership: a
            // pool's SortOrder is its position here, and the meter, the sheet
            // row and the cost label all read a pool by id rather than by
            // index -- so the value of pinning the order is that an author
            // moving a row sees this test rather than a silent reshuffle.
            CollectionAssert.AreEqual(new[] { "mana", "fury" }, Pools().Select(p => p.Id).ToList());
        }

        [Test]
        public void FuryIsARageBarRatherThanABudget()
        {
            var fury = Fury();

            Assert.AreEqual("Fury", fury.DisplayName);
            Assert.AreEqual("FURY", fury.ShortTag);

            // FIXED IS THE HALF THAT MAKES "0..100, ALWAYS" TRUE. Every Max
            // Mana source in the game -- the Wisdom bonus, talents, relics,
            // the reward track, FlatMaxManaBonus -- adds to a WisdomDerived
            // pool and to nothing else, so 100 is the whole number from every
            // source and stays 100 for a Bjorn wearing every relic in the
            // game. The consequence, accepted rather than hidden: a Max Mana
            // talent or relic is a dead pick for him.
            Assert.AreEqual(PoolCapacityRule.Fixed, fury.CapacityRule);
            Assert.AreEqual(100, fury.Capacity);

            // THE SHAPE, and it is the opposite of mana's. Mana opens full
            // and only goes down; Fury opens at nothing and is earned by
            // fighting -- so Bjorn's worst turn is his first and his best is
            // his last, the arc Shawn's Wool already draws.
            Assert.AreEqual(PoolStartRule.Zero, fury.StartRule);
            Assert.AreEqual(0, fury.StartValue);

            // NOTHING FROM THE POOL ITSELF: the flat gains are 0 because each
            // of Bjorn's three root talents is a Fury engine that replaces
            // them (the Sentinel per hit taken, the Einherjar per damaging
            // action, the Juggernaut per turn). A Bjorn with no root earns
            // no Fury from swinging or being hit; the root is free and the
            // pick is mandatory.
            Assert.AreEqual(0, fury.GainPerTurn);
            Assert.AreEqual(0, fury.GainOnAttack);
            Assert.AreEqual(0, fury.GainOnDamageTaken);

            // AND IT DRAINS IF HE DOES NEITHER. Damage is the narrow reading
            // of "idle" (a turn spent on Provoke, an item or a Move decays);
            // the owner can widen it to AnyAction by editing this one field,
            // which is why DecayUnless is an enum and not a bool.
            Assert.AreEqual(10, fury.DecayPerIdleTurn);
            Assert.AreEqual(PoolDecayTrigger.Damage, fury.DecayUnless);

            // The three switches that make Fury exotic rather than mana. A
            // mana potion must not be a rage potion (RestoredByManaEffects),
            // the meter beats (Pulse), and Bjorn carries no spell books --
            // the fact five gates in Core read through SpellBooks.CanHold.
            Assert.IsTrue(fury.Pulse);
            Assert.IsFalse(fury.AllowsSpellBooks);
            Assert.IsFalse(fury.RestoredByManaEffects);
            Assert.IsFalse(fury.AbsorbsDamage);
        }

        [Test]
        public void FuryIsOrangeAndNothingElseOnTheRosterIs()
        {
            // THE HEXES AS LITERALS, and the same reasoning
            // PartyFormationCaptureTests' fixture row gives: the HUD test
            // hands a fixture pool to a live combatant and asserts these
            // three tokens, so if the shipped row ever disagreed with the
            // fixture the capture would be photographing a colour nobody
            // ships. This is the other end of that pair.
            var fury = Fury();

            Assert.AreEqual("#FF8A3A", fury.BrightHex);
            Assert.AreEqual("#8E3A12", fury.DeepHex);
            Assert.AreEqual("#FFD2B0", fury.TextHex);

            Assert.AreNotEqual(Mana().BrightHex, fury.BrightHex,
                "the two shipped meters draw in the same colour, so the bar tells the player nothing");
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

            // 30 IS NOW THE ONLY COPY. It used to duplicate
            // GameplayConstants.DefaultMaxMana; phase B deleted that constant
            // (and the class it was alone in) once every reader started
            // building its pool from this row, so this file is where the
            // number lives and this literal is what a silent edit fails
            // against.
            Assert.AreEqual(30, mana.Capacity);

            // Mana is a budget: it opens full and only goes down.
            Assert.AreEqual(PoolStartRule.Full, mana.StartRule);
            Assert.AreEqual(0, mana.StartValue);

            // gainPerTurn 0 does NOT mean mana stops regenerating: for a
            // WisdomDerived pool the authored number is a BASE and the
            // derived manaRegen stat (Wisdom + gear + talents + track) is
            // added on top of it (ContentDatabase.BuildPrimaryPool). Every
            // point of mana's regen is derived, so the base is 0 -- and
            // authoring a number here would ADD to what a character already
            // regenerates rather than replace it.
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
