using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Combat.Session
{
    // One status as the Glossary lists it: what the fight's badge is called, its
    // code, and what it does with the number left open.
    public readonly struct StatusDefinition
    {
        // The badge's resource name (Resources/Status/<slug>) and the row's id.
        public readonly string Slug;
        public readonly string Code;
        public readonly string Name;

        // Holder-relative, as the badge colours it: an enemy's own Poison is
        // still a detriment.
        public readonly bool Positive;

        // The clause the fight's tooltip shows after "NAME -- ", with "X" where
        // the tooltip has the live number.
        public readonly string Effect;

        // A sentence on how applications combine or how long it lasts, when
        // the badge's own tooltip says so. Empty otherwise.
        public readonly string Note;

        public StatusDefinition(string slug, string code, string name, bool positive, string effect,
                                string note = "")
        {
            Slug = slug;
            Code = code;
            Name = name;
            Positive = positive;
            Effect = effect;
            Note = note ?? "";
        }

        // "X damage each turn start.": the tooltip's clause as a sentence. The
        // glossary shows the name as the entry's heading, so the body does not
        // repeat it; the fight's tooltip keeps its own "Name -- clause" shape in
        // StatusHud.
        public string Description =>
            char.ToUpperInvariant(Effect[0]) + Effect.Substring(1) + "." + (Note.Length > 0 ? " " + Note : "");
    }

    // EVERY STATUS A BADGE CAN SHOW, as one list -- the source the Glossary's
    // Statuses category reads instead of copying strings.
    //
    // The engine statuses come from StatusEffectType itself, so a status added
    // to the enum arrives here (and StatusHud.EffectPhrase throws until it has
    // wording, which StatusDefinitionsTests turns into a failing test rather
    // than a blank row). The statuses with no enum member -- speed, the
    // transformation, rally and the two suppression windows -- are the
    // presentation ids StatusHud already names, and are listed by hand here
    // because nothing enumerates them; StatusDefinitionsTests pins that every
    // slug is one a badge can draw.
    public static class StatusDefinitions
    {
        // Alphabetical, the order a reference is looked up in. The fight's own
        // badge order is a priority (StatusHud.SortKeyFor), which is the wrong
        // question for a list you read.
        public static readonly IReadOnlyList<StatusDefinition> All = Build();

        private const string X = "X";

        private static IReadOnlyList<StatusDefinition> Build()
        {
            var list = new List<StatusDefinition>();

            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                list.Add(new StatusDefinition(
                    StatusHud.SlugFor(type),
                    StatusHud.CodeFor(type),
                    StatusHud.NameFor(type),
                    StatusHud.IsPositive(type),
                    StatusHud.EffectPhrase(type, X),
                    StatusHud.StacksInTooltip(type) ? "Separate applications add together." : ""));
            }

            list.Add(new StatusDefinition(StatusHud.SpeedUpSlug, StatusHud.SpeedUpCode, "Speed up", true,
                StatusHud.SpeedEffect(true, X)));
            list.Add(new StatusDefinition(StatusHud.SpeedDownSlug, StatusHud.SpeedDownCode, "Speed down", false,
                StatusHud.SpeedEffect(false, X)));
            list.Add(new StatusDefinition(StatusHud.TransformSlug, StatusHud.TransformCode, "Transformed", true,
                StatusHud.TransformedEffect));
            list.Add(new StatusDefinition(StatusHud.RallySlug, StatusHud.RallyCode, "Rallied", true,
                StatusHud.RallyEffect(X), "It lasts the rest of the fight and grows each round."));
            list.Add(new StatusDefinition(StatusHud.SilenceSlug, StatusHud.SilenceCode, "Silenced", false,
                StatusHud.SilenceEffect));
            list.Add(new StatusDefinition(StatusHud.DisarmSlug, StatusHud.DisarmCode, "Disarmed", false,
                StatusHud.DisarmEffect(X)));

            return list.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        }
    }
}
