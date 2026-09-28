using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // IGNORE PAIN'S POOL (docs/PLAN_BJORN_CONSTELLATIONS.md section 4, Phase 4
    // row 4c): a percent of every HIT is not taken now but paid as damage over
    // the holder's next `Turns` turn starts, in equal parts.
    //
    // WHERE IT CUTS IN, decided 2026-09-28: AFTER wards and every other
    // defence, BEFORE health. FightSession.LandPacket defers the packet it is
    // handed, and that packet is already past DamagePipeline (armour,
    // resistance, Protect/Vulnerable, Stalwart, the ward pool). So a ward
    // still eats the blow first and whole -- Ignore Pain spreads only what
    // would have reached health -- and deferred damage is never mitigated a
    // second time when it is paid. Kinship and a hatched Phoenix Egg shell
    // are checked before the deferral (a cancelled blow defers nothing).
    //
    // ONLY HITS. A status tick is not a hit and is never deferred, and
    // neither is this pool's own payment -- both land through
    // FightSession.DealStatusTickPacket, which does not defer. That is also
    // what stops a payment re-deferring itself forever.
    //
    // ROUNDING, pinned in DelayedDamagePoolTests:
    //   Defer   -- floor(hit * Percent / 100). A 4-point hit at 20% defers 0;
    //              the pool never takes more than the percent promises.
    //   Payment -- each tranche pays ceil(remaining / turnsLeft) per turn, so
    //              the parts are equal up to one point, the larger ones
    //              first (20 over 3 turns is 7, 7, 6) and nothing is lost.
    //
    // TRANCHES, not one number: each hit spreads over ITS OWN next N turns. A
    // single running total would re-spread an old hit every time a new one
    // arrived and pay it later than promised.
    //
    // HealsReducePool (Ignore Pain T3): a heal first pays down what is
    // pending, oldest tranche first, and only the rest restores health --
    // EXCEPT under Cursed Blood, where FightSession.HealAndCount converts the
    // heal before this pool is ever consulted (HealConversion's header).
    public sealed class DelayedDamagePool
    {
        public int Percent;
        public int Turns;
        public bool HealsReducePool;

        private sealed class Tranche
        {
            public DamageType Type;
            public int Remaining;
            public int TurnsLeft;
        }

        private readonly List<Tranche> _tranches = new List<Tranche>();

        public DelayedDamagePool(int percent, int turns, bool healsReducePool = false)
        {
            Percent = percent;
            Turns = turns;
            HealsReducePool = healsReducePool;
        }

        public int Pending
        {
            get
            {
                int sum = 0;
                foreach (var t in _tranches) sum += t.Remaining;
                return sum;
            }
        }

        // How much of an incoming hit this pool would take. Pure: the preview
        // side of Defer, so a caller can ask without committing.
        public int DeferrableOf(int hit)
        {
            if (hit <= 0 || Percent <= 0 || Turns <= 0) return 0;
            return (int)((long)hit * Math.Min(100, Percent) / 100);
        }

        // Takes this pool's share of a hit into a new tranche and returns it;
        // the caller lands the rest now.
        public int Defer(int hit, DamageType type)
        {
            int deferred = DeferrableOf(hit);
            if (deferred <= 0) return 0;

            _tranches.Add(new Tranche { Type = type, Remaining = deferred, TurnsLeft = Turns });
            return deferred;
        }

        // The holder's turn opened: every tranche pays one installment.
        // Grouped by damage type, in first-deferred order, so the caller can
        // land one packet per type the way a status tick lands one row per
        // status.
        public List<(DamageType Type, int Amount)> TakeDue()
        {
            var due = new List<(DamageType Type, int Amount)>();

            foreach (var t in _tranches)
            {
                int part = (t.Remaining + t.TurnsLeft - 1) / t.TurnsLeft;
                t.Remaining -= part;
                t.TurnsLeft--;

                int at = due.FindIndex(d => d.Type == t.Type);
                if (at < 0) due.Add((t.Type, part));
                else due[at] = (t.Type, due[at].Amount + part);
            }

            _tranches.RemoveAll(t => t.Remaining <= 0 || t.TurnsLeft <= 0);
            due.RemoveAll(d => d.Amount <= 0);
            return due;
        }

        // Ignore Pain T3. Returns how much of `heal` the pool consumed; 0 when
        // the flag is off or nothing is pending. Oldest tranche first -- the
        // pain closest to landing is the pain a heal takes away.
        public int ReduceByHeal(int heal)
        {
            if (!HealsReducePool || heal <= 0) return 0;

            int left = heal;
            foreach (var t in _tranches)
            {
                if (left <= 0) break;
                int take = Math.Min(left, t.Remaining);
                t.Remaining -= take;
                left -= take;
            }

            _tranches.RemoveAll(t => t.Remaining <= 0);
            return heal - left;
        }

        public void Clear() => _tranches.Clear();
    }
}
