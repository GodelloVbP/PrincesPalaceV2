using System;

namespace PrincesPalace.Domain.Economy
{
    // Holds an amount per currency and is the ONLY place any of them is added
    // to or spent.
    //
    // Before this, every earn was a bare `+=` and every purchase was an
    // if-then-subtract at the call site. That is how an economy ends up with a
    // negative balance in one place and an unguarded overdraft in another —
    // and AUDIT.md P0 #1 is exactly that class of bug, an unbounded money loop
    // nobody wrote on purpose. One TrySpend, one Add, both clamped.
    //
    // Plain int fields rather than a dictionary: JsonUtility cannot round-trip
    // a Dictionary, and this is serialized into the save. Adding a currency
    // means adding a field and a case, which is three lines and impossible to
    // get subtly wrong.
    [Serializable]
    public class Wallet
    {
        public int gold;
        // LEGACY, migration source only. Embers live on the CHARACTER now --
        // see Character.embers and SaveData's 2 -> 3 step. Kept as a field
        // rather than deleted because deleting it makes JsonUtility drop the
        // value on load, which would silently rob every pre-v3 save of
        // everything it had banked. Nothing live reads it.
        public int embers;

        public int Get(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Gold: return gold;
                case CurrencyType.Embers: return embers;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Wallet has no field for this currency.");
            }
        }

        public void Set(CurrencyType type, int amount)
        {
            int clamped = Math.Max(0, amount);
            switch (type)
            {
                case CurrencyType.Gold: gold = clamped; break;
                case CurrencyType.Embers: embers = clamped; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Wallet has no field for this currency.");
            }
        }

        // Negative and zero amounts are ignored rather than silently
        // subtracting: an "award" that takes money away is a caller bug, and
        // routing it through Add would hide it.
        //
        // SUMMED IN long AND SATURATED, which is the other end of Set's own
        // clamp. `Get(type) + amount` in int wraps NEGATIVE past the ceiling,
        // and Set then clamps that to zero -- so the transaction that should
        // have filled the purse emptied it instead, silently, with no
        // exception and nothing in the log. Not a hypothetical at the depths
        // this game reaches: DifficultyCurve.ScaleReward rides the health
        // curve, so a step-200 fight pays roughly two million times a step-0
        // one (capped per payout at MaxScaledValue, a billion), and the
        // endless descent has no floor that stops a long run getting there.
        // Every other curve that compounds off a save carries an explicit
        // ceiling for this reason -- DifficultyCurve's MaxScaledValue,
        // LevelCurve's MaxCost. The wallet is where it actually accumulates
        // and had none.
        public void Add(CurrencyType type, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            long total = (long)Get(type) + amount;
            Set(type, total > int.MaxValue ? int.MaxValue : (int)total);
        }

        public bool CanAfford(CurrencyType type, int amount)
        {
            return amount >= 0 && Get(type) >= amount;
        }

        // All or nothing, and never below zero. A partial spend would leave the
        // player charged for something they did not get.
        public bool TrySpend(CurrencyType type, int amount)
        {
            if (!CanAfford(type, amount))
            {
                return false;
            }

            Set(type, Get(type) - amount);
            return true;
        }

        // Banks a currency from another wallet into this one — a run's Gold
        // joining the save's on the way home. Returns what arrived, so the
        // caller can report it.
        //
        // Between two WALLETS rather than within one, because that is the real
        // shape of the operation: the run's Gold and the save's Gold are the
        // same currency living on two objects with different lifetimes, and
        // banking is precisely the moment one becomes the other. `from` and
        // `to` are still separate parameters because they were once different
        // currencies (Embers -> Gold) and may be again; passing the same one
        // twice is the normal case now and works, since the source is zeroed
        // before the destination is credited.
        //
        // `rate` is floored, so a future "you keep half" is a parameter rather
        // than a rewrite.
        public int BankFrom(Wallet source, CurrencyType from, CurrencyType to, float rate = 1f)
        {
            if (source == null || rate <= 0f)
            {
                return 0;
            }

            int available = source.Get(from);
            if (available <= 0)
            {
                return 0;
            }

            int banked = (int)Math.Floor(available * (double)rate);
            source.Set(from, 0);
            Add(to, banked);
            return banked;
        }
    }
}
