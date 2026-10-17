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

        // One plain sentence on what the effect does; empty where the label
        // says it all.
        public readonly string Detail;

        public RunEffectRow(string label, string value, string detail = "")
        {
            Label = label;
            Value = value;
            Detail = detail;
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

    // What the RUN pane's effects column says: the squad's Favor, the event
    // buffs in force, the run-wide bonus damage, and the second lives left.
    // Pure: Core hands in the run's plain values (RunController).
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

        // The rows, in a fixed order: Favor, the buff kinds as EventBuffs
        // lists them, then bonus damage, then second lives. A kind with
        // nothing active, and a figure at zero, has no row; an empty result
        // is the pane's cue to say so in words.
        //
        // squadFavor is the whole figure the item offers read
        // (ItemOfferRoll.CurrentSquadFavor), event gifts already inside it.
        // The PrincesFavor buff therefore has no row of its own: a second
        // line would show the same gift twice and read as two stats.
        public static List<RunEffectRow> Rows(
            IReadOnlyList<EventBuffEntry> buffs, int legStartStep, int squadFavor,
            int bonusDamagePercent, int secondLivesLeft)
        {
            var rows = new List<RunEffectRow>();

            if (squadFavor > 0)
            {
                rows.Add(new RunEffectRow(
                    UiStrings.RunEffectFavor.Format(),
                    UiStrings.RunEffectFavorAmount.Format(squadFavor),
                    UiStrings.RunEffectFavorDetail.Format()));
            }

            foreach (var def in Buffs)
            {
                if (def.Kind == EventBuffs.PrincesFavor) continue;
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
