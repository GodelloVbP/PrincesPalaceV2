using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // KINSHIP: the first damage packet with amount > 0 that reaches the
    // bearer in a fight is turned aside whole (docs/PLAN_PETTING_ZOO.md,
    // "Kinship").
    //
    // A PACKET, NOT AN ATTACK. The check sits in ApplyAndCountDamage, which
    // every DealDamage call passes through once per packet, so a rider from
    // the same swing (a poison detonation ahead of the hit, an elemental
    // rider after it) is a second packet and lands. What reaches the funnel
    // has already been through DamagePipeline, so a dodged blow and a blow a
    // ward ate in full arrive as nothing and cannot spend it.
    //
    // Its own partial because FightSession.Riders.cs, where Bloodlust's
    // spent-set lives, is owned by the next phase of that plan. Same shape
    // as Bloodlust: one session is one fight, so a per-session set IS the
    // per-fight reset.
    public sealed partial class FightSession
    {
        private readonly HashSet<CombatantState> _kinshipSpent = new HashSet<CombatantState>();

        // True when this packet is cancelled -- and in that case it is also
        // spent and said. Called from ApplyAndCountDamage directly after the
        // pools have heard the blow and BEFORE the Phoenix Egg checks, so a
        // lethal first packet is cancelled rather than hatching the egg, and
        // the egg's once-per-combat lock is never touched.
        //
        // Not on a shell: an already-hatched egg absorbs by its own rule
        // (PhoenixEggAbsorb), and Kinship is left unspent for after it.
        private bool TryKinshipCancel(CombatantState target, int amount)
        {
            if (target == null || amount <= 0 || !target.IsPlayerSide || !target.IsAlive) return false;
            if (target.IsPhoenixEgg) return false;
            if (_kinshipSpent.Contains(target) || !HasRelic(target, RelicEffect.Kinship)) return false;

            _kinshipSpent.Add(target);
            AppendMessage("Kinship turns the blow aside.");
            return true;
        }
    }
}
