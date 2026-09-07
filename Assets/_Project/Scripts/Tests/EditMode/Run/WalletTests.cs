using NUnit.Framework;
using PrincesPalace.Domain.Economy;

namespace PrincesPalace.Domain.Tests
{
    public class WalletTests
    {
        [Test]
        public void AFreshWallet_IsEmpty()
        {
            var wallet = new Wallet();

            foreach (CurrencyType type in System.Enum.GetValues(typeof(CurrencyType)))
            {
                Assert.AreEqual(0, wallet.Get(type), $"{type} should start at zero");
            }
        }

        [Test]
        public void EveryCurrencyType_HasAField()
        {
            var wallet = new Wallet();

            // Get/Set throw on an unmapped currency, so adding a value to the
            // enum without wiring it up fails here rather than at runtime in
            // whichever screen happens to read it first.
            foreach (CurrencyType type in System.Enum.GetValues(typeof(CurrencyType)))
            {
                Assert.DoesNotThrow(() => wallet.Set(type, 7), $"{type} has no backing field");
                Assert.AreEqual(7, wallet.Get(type));
            }
        }

        [Test]
        public void Currencies_AreIndependent()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Gold, 10);

            Assert.AreEqual(10, wallet.Get(CurrencyType.Gold));
            Assert.AreEqual(0, wallet.Get(CurrencyType.Embers));
        }

        // An "award" that takes money away is a caller bug, and routing it
        // through Add would hide it.
        [Test]
        public void Add_IgnoresNonPositiveAmounts()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Gold, 10);

            wallet.Add(CurrencyType.Gold, 0);
            wallet.Add(CurrencyType.Gold, -5);

            Assert.AreEqual(10, wallet.Get(CurrencyType.Gold));
        }

        // The rule that AUDIT.md P0 #1 exists for: a purchase either happens
        // in full or not at all, and a balance never goes negative.
        [Test]
        public void TrySpend_IsAllOrNothing_AndNeverGoesNegative()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Gold, 30);

            Assert.IsFalse(wallet.TrySpend(CurrencyType.Gold, 31));
            Assert.AreEqual(30, wallet.Get(CurrencyType.Gold), "A refused purchase must not take anything");

            Assert.IsTrue(wallet.TrySpend(CurrencyType.Gold, 30));
            Assert.AreEqual(0, wallet.Get(CurrencyType.Gold));
        }

        [Test]
        public void TrySpend_RefusesANegativeCost()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Gold, 5);

            Assert.IsFalse(wallet.TrySpend(CurrencyType.Gold, -10), "A negative price would be free money");
            Assert.AreEqual(5, wallet.Get(CurrencyType.Gold));
        }

        // THE OTHER END OF THE SAME CLAMP. Set already refuses to go below
        // zero; without a ceiling, Add wrapped past int.MaxValue into a
        // negative and Set then clamped THAT to zero -- a purse that empties
        // itself on the transaction that should have filled it. Every other
        // curve in this codebase that compounds off a save carries an
        // explicit ceiling for exactly this reason (DifficultyCurve's
        // MaxScaledValue, LevelCurve's MaxCost); the wallet is where the
        // money actually accumulates and had none.
        [Test]
        public void Add_SaturatesRatherThanWrappingPastTheCeiling()
        {
            var wallet = new Wallet();
            wallet.Set(CurrencyType.Gold, int.MaxValue - 10);

            wallet.Add(CurrencyType.Gold, 100);

            Assert.AreEqual(int.MaxValue, wallet.Get(CurrencyType.Gold),
                "An award that overflows must cap the purse, never empty it");
        }

        [Test]
        public void BankFrom_SaturatesRatherThanWrappingPastTheCeiling()
        {
            var save = new Wallet();
            save.Set(CurrencyType.Gold, int.MaxValue - 10);

            var run = new Wallet();
            run.Add(CurrencyType.Gold, 500);

            int banked = save.BankFrom(run, CurrencyType.Gold, CurrencyType.Gold);

            Assert.AreEqual(500, banked, "The run still hands over everything it had");
            Assert.AreEqual(int.MaxValue, save.Get(CurrencyType.Gold));
            Assert.AreEqual(0, run.Get(CurrencyType.Gold));
        }

        [Test]
        public void Set_ClampsAtZero()
        {
            var wallet = new Wallet();
            wallet.Set(CurrencyType.Gold, -50);

            Assert.AreEqual(0, wallet.Get(CurrencyType.Gold));
        }

        [Test]
        public void CanAfford_MatchesWhatTrySpendWillDo()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Embers, 3);

            Assert.IsTrue(wallet.CanAfford(CurrencyType.Embers, 3));
            Assert.IsFalse(wallet.CanAfford(CurrencyType.Embers, 4));
            Assert.IsFalse(wallet.CanAfford(CurrencyType.Embers, -1));
        }

        // Banking is between two wallets because that is the real shape of it:
        // Embers live on the run, Gold lives on the save.
        [Test]
        public void BankFrom_MovesTheSourceCurrencyIntoThisOne_AndEmptiesIt()
        {
            var save = new Wallet();
            var run = new Wallet();
            run.Add(CurrencyType.Embers, 42);

            int banked = save.BankFrom(run, CurrencyType.Embers, CurrencyType.Gold);

            Assert.AreEqual(42, banked);
            Assert.AreEqual(42, save.Get(CurrencyType.Gold));
            Assert.AreEqual(0, run.Get(CurrencyType.Embers), "The run should keep nothing after banking");
        }

        [Test]
        public void BankFrom_AppliesItsRateAndFloors()
        {
            var save = new Wallet();
            var run = new Wallet();
            run.Add(CurrencyType.Embers, 45);

            int banked = save.BankFrom(run, CurrencyType.Embers, CurrencyType.Gold, rate: 0.5f);

            Assert.AreEqual(22, banked, "45 at half rate floors to 22, never rounds up into free money");
            Assert.AreEqual(22, save.Get(CurrencyType.Gold));
        }

        [Test]
        public void BankFrom_AnEmptySource_DoesNothing()
        {
            var save = new Wallet();
            save.Add(CurrencyType.Gold, 10);

            Assert.AreEqual(0, save.BankFrom(new Wallet(), CurrencyType.Embers, CurrencyType.Gold));
            Assert.AreEqual(10, save.Get(CurrencyType.Gold));
        }

        [Test]
        public void BankFrom_ANullSource_IsSafe()
        {
            var save = new Wallet();
            Assert.AreEqual(0, save.BankFrom(null, CurrencyType.Gold, CurrencyType.Gold));
        }

        // WHICH CURRENCY A RUN CAN COST YOU is the whole distinction, so it is
        // asserted rather than left to convention.
        //
        // This inverted when Gold and Embers swapped roles: Gold used to be the
        // safe one and Embers the wager. Now Gold is the run's own stake --
        // earned delving, banked on retreat, lost on a wipe -- and Embers are
        // the boss-paid meta currency written straight to the save.
        [Test]
        public void OnlyGold_IsPutAtRiskByARun()
        {
            Assert.IsFalse(Currencies.IsPersistent(CurrencyType.Gold),
                "A run's Gold is forfeited on defeat, which is what makes retreating a decision.");
            Assert.IsTrue(Currencies.IsPersistent(CurrencyType.Embers),
                "Embers go straight onto the save the moment a boss pays them and are never at risk.");
        }

        [Test]
        public void ThereIsExactlyOneCurrencyARunCanCostYou()
        {
            int safe = 0;
            int atRisk = 0;
            foreach (CurrencyType type in System.Enum.GetValues(typeof(CurrencyType)))
            {
                if (Currencies.IsPersistent(type)) { safe++; } else { atRisk++; }
            }

            // Walked from the enum rather than hard-counted against a list,
            // so removing Relics as a currency showed up here immediately
            // instead of leaving a stale number nobody re-read.
            Assert.AreEqual(1, safe, "Embers survive a run");
            Assert.AreEqual(1, atRisk, "Only the run's Gold is a wager");
        }
    }
}
