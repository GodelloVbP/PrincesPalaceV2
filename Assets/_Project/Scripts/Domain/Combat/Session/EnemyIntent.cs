using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // What a monster has committed to doing on its next turn, in enough detail
    // for the player to plan against it.
    //
    // Before this, an intent was a bare string and only skills were telegraphed
    // at all -- a plain attack showed nothing, and NOTHING ever said who was
    // going to be hit or for roughly how much. So the one decision the player
    // actually makes each turn (who needs healing, who can afford to be hit,
    // whether to kill this one first) had to be guessed.
    //
    // The target is committed HERE rather than picked at resolution time, which
    // is the change that makes the display honest: a telegraph that says
    // "targeting Shawn" and then hits someone else is worse than no telegraph.
    // A taunt applied AFTER the telegraph still redirects it, and that is the
    // player overriding a declared plan rather than the plan being a lie.
    public enum EnemyIntentKind
    {
        // A plain swing.
        Attack,

        // An authored skill with no status attached.
        Skill,

        // Skills that additionally inflict something, split out because WHICH
        // status is coming changes the answer more than the damage does.
        Poison,
        Stun,
        Weaken,
        Heal,
        Shield,
    }

    public readonly struct EnemyIntent
    {
        // The label the resolution path compares against to decide whether the
        // monster is using its skill. Kept as the plain skill name, and equal to
        // FightSession.IntentAttack for a plain swing, because that comparison
        // predates this type and re-spelling it here would be a second source of
        // truth for the same fact.
        public readonly string Label;
        public readonly EnemyIntentKind Kind;

        // Who it is aimed at, committed at the same moment as the action.
        //
        // The combatant itself rather than just a name, because RESOLUTION has
        // to honour the same pick the icon showed. Two collections -- one for
        // the display name, one for the reference -- is the drift this project
        // keeps writing rules against, so there is one field and the name is
        // derived from it.
        public readonly CombatantState Target;

        public string TargetName => Target?.Name ?? "";

        // Roughly what it will do, computed WITHOUT variance, without a ward,
        // and without touching the run's SeededRandom -- a preview that consumed
        // a draw would change the fight it is previewing, and this is a seeded
        // game where that would also change every later fight in the run.
        //
        // "Roughly" is not a hedge: variance and wards genuinely move it, so the
        // view says "about".
        public readonly int ExpectedDamage;

        public EnemyIntent(string label, EnemyIntentKind kind, CombatantState target, int expectedDamage)
        {
            Label = label;
            Kind = kind;
            Target = target;
            ExpectedDamage = expectedDamage;
        }

        public bool IsAttack => Kind == EnemyIntentKind.Attack;
    }

    public static class EnemyIntentIcons
    {
        // ONE table, so swapping the whole visual language is one edit.
        //
        // REAL ARTWORK, from game-icons.net (CC BY 3.0 -- see CREDITS.md). The
        // route here is worth recording, because the obvious approach is a dead
        // end: emoji cannot work at all in this project. The shipped font is a
        // STATIC SDF atlas containing only the characters the game already uses
        // -- no U+2694, no arrows, not even '*' or '=' -- and TMP draws a
        // character it has no glyph for as NOTHING. The first cut rendered three
        // invisible badges, which looks exactly like a feature that never
        // shipped.
        //
        // RESOURCES-relative and extension-free, because these are swapped at
        // RUNTIME as intents change. That is the convention ArtPathConvention
        // enforces for content, and it applies here for the same reason:
        // Resources.Load returns null for an Assets/ path or a path carrying
        // ".png", and returns it silently.
        public static string ResourceFor(EnemyIntentKind kind) => "Intent/" + Slug(kind);

        public static string Slug(EnemyIntentKind kind)
        {
            switch (kind)
            {
                case EnemyIntentKind.Attack: return "attack";
                case EnemyIntentKind.Poison: return "poison";
                case EnemyIntentKind.Stun: return "stun";
                case EnemyIntentKind.Weaken: return "weaken";
                case EnemyIntentKind.Heal: return "heal";
                case EnemyIntentKind.Shield: return "shield";
                default: return "skill";
            }
        }

        // Colour carries as much of the meaning as the shape does at badge size.
        public static string TintFor(EnemyIntentKind kind)
        {
            switch (kind)
            {
                case EnemyIntentKind.Attack: return UiKit.FightHudPalette.IntentAttack;
                case EnemyIntentKind.Poison: return UiKit.FightHudPalette.IntentPoison;
                case EnemyIntentKind.Stun: return UiKit.FightHudPalette.IntentStun;
                case EnemyIntentKind.Weaken: return UiKit.FightHudPalette.IntentWeaken;
                case EnemyIntentKind.Heal: return UiKit.FightHudPalette.IntentHeal;
                case EnemyIntentKind.Shield: return UiKit.FightHudPalette.IntentShield;
                default: return UiKit.FightHudPalette.IntentSkill;
            }
        }

        // The three-letter fallback, kept for the case the artwork does not
        // load. Graceful degradation is the house style, and a badge that
        // silently renders nothing is exactly the failure this feature already
        // hit once.
        public static string For(EnemyIntentKind kind)
        {
            switch (kind)
            {
                case EnemyIntentKind.Attack: return "ATK";
                case EnemyIntentKind.Poison: return "PSN";
                case EnemyIntentKind.Stun: return "STN";
                case EnemyIntentKind.Weaken: return "WKN";
                case EnemyIntentKind.Heal: return "HEAL";
                case EnemyIntentKind.Shield: return "DEF";
                default: return "SKL";
            }
        }

        // Which kind an authored enemy skill reads as. Statuses win over the
        // generic Skill because what a monster is about to APPLY changes the
        // player's decision more than the fact that it is a skill at all.
        public static EnemyIntentKind KindFor(bool usingSkill, StatusEffectType? appliesStatus, bool hasStatus)
        {
            if (!usingSkill) return EnemyIntentKind.Attack;
            if (!hasStatus || !appliesStatus.HasValue) return EnemyIntentKind.Skill;

            switch (appliesStatus.Value)
            {
                case StatusEffectType.Poison: return EnemyIntentKind.Poison;
                case StatusEffectType.Stun: return EnemyIntentKind.Stun;
                case StatusEffectType.Vulnerable: return EnemyIntentKind.Weaken;
                case StatusEffectType.Regen: return EnemyIntentKind.Heal;
                case StatusEffectType.Protect: return EnemyIntentKind.Shield;
                default: return EnemyIntentKind.Skill;
            }
        }
    }
}
