using System.Collections.Generic;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.UiKit
{
    // One line of the RUN pane's effects column: what is in force, and its
    // figure. The figure is empty for an effect that has none to state.
    public readonly struct RunEffectRow
    {
        public readonly string Label;
        public readonly string Value;

        public RunEffectRow(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    // How one EventBuffs kind reads on the pane. A definition per kind, so a
    // kind with no way to be shown cannot ship: RunEffectRowTests fails on any
    // EventBuffs kind missing from Buffs.
    public readonly struct RunEffectDef
    {
        public readonly string Kind;

        // The label for a buff that lasts one leg, and for one that lasts the
        // run. Equal where the scope is not part of the wording.
        public readonly UiString LegLabel;
        public readonly UiString RunLabel;

        // The figure, given the summed amount of the kind's active buffs; null
        // for a kind whose presence is the whole message.
        public readonly UiString Value;

        public RunEffectDef(string kind, UiString legLabel, UiString runLabel, UiString value = default)
        {
            Kind = kind;
            LegLabel = legLabel;
            RunLabel = runLabel;
            Value = value;
        }
    }

    // What the RUN pane's effects column says: the event buffs in force, the
    // run-wide bonus damage, and the second lives left. Pure: Core hands in
    // the run's plain values (RunController).
    public static class RunEffectRows
    {
        public static readonly IReadOnlyList<RunEffectDef> Buffs = new[]
        {
            new RunEffectDef(EventBuffs.PrincesFavor,
                UiStrings.RunEffectFavor, UiStrings.RunEffectFavor, UiStrings.RunEffectFavorAmount),
            new RunEffectDef(EventBuffs.FillSpecialPool,
                UiStrings.RunEffectFillLeg, UiStrings.RunEffectFillRun),
        };

        public static RunEffectDef? DefFor(string kind)
        {
            foreach (var def in Buffs)
            {
                if (def.Kind == kind) return def;
            }

            return null;
        }

        // The rows, in a fixed order: the buff kinds as EventBuffs lists them,
        // then bonus damage, then second lives. A kind with nothing active,
        // and a figure at zero, has no row; an empty result is the pane's cue
        // to say so in words.
        public static List<RunEffectRow> Rows(
            IReadOnlyList<EventBuffEntry> buffs, int legStartStep, int bonusDamagePercent, int secondLivesLeft)
        {
            var rows = new List<RunEffectRow>();

            foreach (var def in Buffs)
            {
                if (!EventBuffs.AnyActive(buffs, def.Kind, legStartStep)) continue;

                bool wholeRun = false;
                foreach (var buff in EventBuffs.Active(buffs, def.Kind, legStartStep))
                {
                    if (buff.legStartStep == EventBuffs.WholeRun) wholeRun = true;
                }

                string label = (wholeRun ? def.RunLabel : def.LegLabel).Format();
                string value = def.Value.IsValid
                    ? def.Value.Format(EventBuffs.ActiveAmount(buffs, def.Kind, legStartStep))
                    : "";
                rows.Add(new RunEffectRow(label, value));
            }

            if (bonusDamagePercent > 0)
            {
                rows.Add(new RunEffectRow(
                    UiStrings.RunEffectBonusDamage.Format(),
                    UiStrings.RunEffectBonusDamageAmount.Format(bonusDamagePercent)));
            }

            if (secondLivesLeft > 0)
            {
                rows.Add(new RunEffectRow(
                    UiStrings.RunEffectSecondLives.Format(),
                    UiStrings.RunEffectSecondLivesAmount.Format(secondLivesLeft)));
            }

            return rows;
        }
    }
}
