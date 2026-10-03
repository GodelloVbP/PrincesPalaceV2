using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // EVERYTHING THE STATUS BADGE ROW READS, as plain data, at one instant.
    //
    // A beat records one of these per combatant (Vitals.Statuses) instead of
    // the formatted rows: recording is bookkeeping the bot and balance runs pay
    // for on every beat, while the badges are only ever painted by a HUD. The
    // HUD turns a snapshot into rows (FightHudModel.StatusRowsFrom) from this
    // data alone, never from the live session -- a live read at paint time
    // would show a status a later beat of the same round applied.
    //
    // Capture is the one place that reads the combatant and the session;
    // FightHudModel.StatusRowsFrom is the one place that formats.
    public sealed class StatusSnapshot
    {
        public readonly struct SpeedGrant
        {
            public readonly string SourceName;
            public readonly int Granted;
            public readonly int TurnsLeft;

            public SpeedGrant(string sourceName, int granted, int turnsLeft)
            {
                SourceName = sourceName;
                Granted = granted;
                TurnsLeft = turnsLeft;
            }
        }

        // One summary per distinct status type, wards excluded, in the order
        // the types first appear on the combatant.
        public readonly IReadOnlyList<StatusEffects.StatusSummary> Statuses;
        public readonly StatusEffects.WardSummary Wards;

        public readonly bool Transformed;
        public readonly string TransformName;
        public readonly int TransformTurns;
        public readonly bool TransformPermanent;
        // Set when a pool keeps the form alive (Berserk's Fury); null otherwise.
        public readonly string TransformUpkeepPool;

        public readonly bool Silenced;
        public readonly int SilenceTurns;
        public readonly bool Disarmed;
        public readonly int DisarmPercent;
        public readonly int DisarmTurns;

        public readonly int PermanentPhysicalDefenseShred;

        // Session facts. All zero/empty when the capture had no session.
        public readonly int RallyStacks;
        public readonly int RallyAttackPercent;
        public readonly IReadOnlyList<SpeedGrant> SpeedGrants;

        // The Drowned Lantern's mark (FightSession.IsMarked), which is not a
        // status on the combatant.
        public readonly bool LanternMarked;

        public StatusSnapshot(IReadOnlyList<StatusEffects.StatusSummary> statuses, StatusEffects.WardSummary wards,
                              bool transformed, string transformName, int transformTurns, bool transformPermanent,
                              string transformUpkeepPool, bool silenced, int silenceTurns, bool disarmed,
                              int disarmPercent, int disarmTurns, int permanentPhysicalDefenseShred,
                              int rallyStacks, int rallyAttackPercent, IReadOnlyList<SpeedGrant> speedGrants,
                              bool lanternMarked)
        {
            Statuses = statuses;
            Wards = wards;
            Transformed = transformed;
            TransformName = transformName;
            TransformTurns = transformTurns;
            TransformPermanent = transformPermanent;
            TransformUpkeepPool = transformUpkeepPool;
            Silenced = silenced;
            SilenceTurns = silenceTurns;
            Disarmed = disarmed;
            DisarmPercent = disarmPercent;
            DisarmTurns = disarmTurns;
            PermanentPhysicalDefenseShred = permanentPhysicalDefenseShred;
            RallyStacks = rallyStacks;
            RallyAttackPercent = rallyAttackPercent;
            SpeedGrants = speedGrants;
            LanternMarked = lanternMarked;
        }

        // Every status type on the combatant at this instant, wards as Shielded.
        public IEnumerable<StatusEffectType> Types()
        {
            foreach (var summary in Statuses) yield return summary.Type;
            if (Wards.Any) yield return StatusEffectType.Shielded;
        }

        public static StatusSnapshot Capture(FightSession session, CombatantState actor)
        {
            var statuses = new List<StatusEffects.StatusSummary>();
            var seen = new HashSet<StatusEffectType>();
            foreach (var status in actor.Statuses)
            {
                if (status.Type == StatusEffectType.Shielded) continue;
                if (!seen.Add(status.Type)) continue;
                statuses.Add(StatusEffects.SummariseStatus(actor, status.Type));
            }

            var transformation = actor.Transformation;
            var suppression = actor.Suppression;

            int rallyStacks = 0, rallyPercent = 0;
            var speed = new List<SpeedGrant>();
            bool marked = false;
            if (session != null)
            {
                rallyStacks = session.RallyStacksOf(actor);
                if (rallyStacks > 0) rallyPercent = session.RallyAttackPercentOf(actor);

                var kit = session.KitFor(actor);
                foreach (var buff in session.ActiveSpeedBuffs(actor))
                {
                    // A status-driven entry already has its own summary above.
                    if (buff.Source is StatusEffectType) continue;
                    speed.Add(new SpeedGrant(FightHudModel.SpeedSourceName(kit, buff.Source), buff.Granted,
                        buff.TurnsLeft));
                }

                marked = session.IsMarked(actor);
            }

            return new StatusSnapshot(
                statuses, StatusEffects.SummariseWards(actor),
                transformation != null, transformation?.DisplayName, transformation?.TurnsRemaining ?? 0,
                transformation != null && transformation.IsPermanent,
                transformation != null && transformation.PrimaryDrainPerTurn > 0 ? actor.PrimaryPool?.DisplayName : null,
                suppression.IsSilenced, suppression.IsSilenced ? suppression.Silence.TurnsRemaining : 0,
                suppression.IsDisarmed, suppression.IsDisarmed ? suppression.DisarmPercent : 0,
                suppression.IsDisarmed ? suppression.Disarm.TurnsRemaining : 0,
                actor.PermanentPhysicalDefenseShred, rallyStacks, rallyPercent, speed, marked);
        }
    }
}
