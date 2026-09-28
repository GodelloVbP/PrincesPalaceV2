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

        // ANOTHER BODY ON THE FIELD, which is not a kind of damage and is the
        // reason it needs its own badge. Every other kind above answers "what
        // is about to happen to someone"; this one answers "the shape of this
        // fight is about to change", and it read as the generic Skill icon --
        // the badge that means "something is coming" -- on the one turn where
        // knowing what is coming would change whether the player spends their
        // burst now or saves it for the two enemies about to exist.
        Summon,

        // THE BELLWETHER'S VOCABULARY (PLAN_BELLWETHER_KIT 3.8), each derived
        // from the skill like the rest: Bleed, a skill that applies Bleed;
        // Pull, a Reposition ("you are about to be moved"); Knell, a
        // seat-sized hit ("where you stand decides how much").
        Bleed,
        Pull,
        Knell,
    }

    // WHO IT LANDS ON, which is the half of a telegraph that changes the
    // player's decision most.
    //
    // "It will hit Shawn for 40" and "it will heal its whole side for 40" are
    // opposite problems and used to be indistinguishable, because a monster
    // could only ever do one thing: damage the person it was pointed at. Now
    // that a monster casts real skills, the scope has to be said or the
    // telegraph is worse than none -- it would name a single target for an
    // effect that hits everybody.
    public enum EnemyIntentScope
    {
        // The one combatant named in the intent.
        One,

        // Everyone on the player's side.
        AllOpponents,

        // The monster itself.
        Self,

        // Every monster still standing.
        AllAllies,
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

        // WHICH ABILITY, by index into the kit's pool.
        //
        // Resolution used to recover this by comparing the intent's Label back
        // against the monster's single skill name. That worked while there was
        // exactly one skill and its name was therefore unique. With a weighted
        // pool two abilities may share a display name -- two flavours of the
        // same spell, a placeholder authored twice -- and a comparison on text
        // would resolve the wrong one while the telegraph looked right.
        //
        // -1 means a plain swing declared by something with no pool at all.
        public readonly int AbilityIndex;

        // Who it lands on, and whether the number is a wound or a mend. Both
        // are needed before the tooltip can phrase a sentence that is true:
        // ExpectedDamage is a magnitude, and a magnitude with no sign reads as
        // a threat even when it is a heal.
        public readonly EnemyIntentScope Scope;
        public readonly bool Heals;

        // THE NEXT STEP OF A SCHEDULED SEQUENCE ("Death Knell"), or null. Set
        // at commitment; the plate line and tooltip say "<Then> next".
        public readonly string Then;

        // ---- read live (FightSession.IntentDetailFor), never stored --------
        //
        // A seat-sized hit's expected damage for each seat, front first, or
        // null for any other intent; ExpectedDamage is then the entry for
        // TargetSeat, the seat the target stands in NOW.
        public readonly int[] DamageBySeat;
        public readonly int TargetSeat;

        // The shown number is at or above the target's current health.
        public readonly bool IsLethal;

        public EnemyIntent(string label, EnemyIntentKind kind, CombatantState target, int expectedDamage,
                           int abilityIndex = -1, EnemyIntentScope scope = EnemyIntentScope.One,
                           bool heals = false, string then = null)
            : this(label, kind, target, expectedDamage, abilityIndex, scope, heals, then, null, -1, false)
        {
        }

        private EnemyIntent(string label, EnemyIntentKind kind, CombatantState target, int expectedDamage,
                            int abilityIndex, EnemyIntentScope scope, bool heals, string then,
                            int[] damageBySeat, int targetSeat, bool isLethal)
        {
            Label = label;
            Kind = kind;
            Target = target;
            ExpectedDamage = expectedDamage;
            AbilityIndex = abilityIndex;
            Scope = scope;
            Heals = heals;
            Then = string.IsNullOrEmpty(then) ? null : then;
            DamageBySeat = damageBySeat;
            TargetSeat = targetSeat;
            IsLethal = isLethal;
        }

        // The same commitment with its live reading filled in.
        public EnemyIntent Live(int expectedDamage, int[] damageBySeat, int targetSeat, bool isLethal) =>
            new EnemyIntent(Label, Kind, Target, expectedDamage, AbilityIndex, Scope, Heals, Then,
                damageBySeat, targetSeat, isLethal);

        public bool IsAttack => Kind == EnemyIntentKind.Attack;

        // "Step back": a seat behind the one the target stands in takes less.
        public bool StepBackIsSafer
        {
            get
            {
                if (DamageBySeat == null || TargetSeat < 0 || TargetSeat >= DamageBySeat.Length) return false;
                for (int s = TargetSeat + 1; s < DamageBySeat.Length; s++)
                {
                    if (DamageBySeat[s] < DamageBySeat[TargetSeat]) return true;
                }

                return false;
            }
        }
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

        // WHO AN EFFECT LANDS ON, from the effect alone.
        //
        // Read from the SkillEffect rather than stored per ability, because it
        // is not an authoring choice: DamageAll hits everyone opposite by
        // definition, and an author who could disagree with that could make the
        // telegraph lie.
        public static EnemyIntentScope ScopeFor(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.DamageAll:
                case SkillEffect.Enthrall:
                    return EnemyIntentScope.AllOpponents;

                case SkillEffect.HealSelf:

                // A summon does nothing to anyone standing opposite. Left on
                // the default One, the telegraph named a party member for an
                // action that never touches them -- "will Roar Shawn" -- which
                // is the specific-and-false claim this enum exists to stop.
                case SkillEffect.Summon:
                    return EnemyIntentScope.Self;

                case SkillEffect.HealParty:
                case SkillEffect.RestorePartyMana:
                    return EnemyIntentScope.AllAllies;

                default:
                    return EnemyIntentScope.One;
            }
        }

        public static bool HealsFor(SkillEffect effect) =>
            effect == SkillEffect.HealSelf || effect == SkillEffect.HealParty;

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
                case EnemyIntentKind.Summon: return "summon";
                case EnemyIntentKind.Bleed: return "bleed";
                case EnemyIntentKind.Pull: return "pull";
                case EnemyIntentKind.Knell: return "knell";
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
                case EnemyIntentKind.Summon: return UiKit.FightHudPalette.IntentSummon;
                case EnemyIntentKind.Bleed: return UiKit.FightHudPalette.IntentBleed;
                case EnemyIntentKind.Pull: return UiKit.FightHudPalette.IntentPull;
                case EnemyIntentKind.Knell: return UiKit.FightHudPalette.IntentKnell;
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

                // "CALL", not "SUM" — three letters that read as arithmetic on
                // a badge full of other three-letter abbreviations.
                case EnemyIntentKind.Summon: return "CALL";
                case EnemyIntentKind.Bleed: return "BLD";
                case EnemyIntentKind.Pull: return "PULL";
                case EnemyIntentKind.Knell: return "KNL";
                default: return "SKL";
            }
        }

        // Which kind an authored enemy skill reads as. Statuses win over the
        // generic Skill because what a monster is about to APPLY changes the
        // player's decision more than the fact that it is a skill at all.
        // THE EFFECT FIRST, then the status.
        //
        // KindFor below reads only the status a monster applies, which was the
        // whole vocabulary while a monster's "skill" was a scaled attack that
        // might poison. A real skill says what it does in its own Effect, and a
        // heal with no Regen attached would otherwise telegraph as a generic
        // "skill" -- the icon that means "something is coming" on the one turn
        // the player most needs to know it is not coming for them.
        // A real skill's kind, from the skill itself. The seat-sized hit wins
        // over its effect and status: "where you stand decides" is the one
        // fact the player must act on, whatever else rides the hit.
        public static EnemyIntentKind KindFor(Content.ResolvedSkill skill)
        {
            if (skill == null) return EnemyIntentKind.Skill;
            if (skill.HasDamageBySeat && !HealsFor(skill.Effect)) return EnemyIntentKind.Knell;
            return KindFor(skill.Effect, skill.AppliesStatus, skill.AppliesStatus.HasValue);
        }

        public static EnemyIntentKind KindFor(SkillEffect effect, StatusEffectType? appliesStatus, bool hasStatus)
        {
            switch (effect)
            {
                case SkillEffect.HealSelf:
                case SkillEffect.HealParty:
                    return EnemyIntentKind.Heal;

                // Before the status check below, like the heals are, and for
                // the same reason: a summon that also applied a status would
                // otherwise telegraph the status and hide the summon, which is
                // the larger of the two facts by a distance.
                case SkillEffect.Summon:
                    return EnemyIntentKind.Summon;

                // Being moved is the larger fact than any rider on the move.
                case SkillEffect.Reposition:
                    return EnemyIntentKind.Pull;
            }

            return KindFor(true, appliesStatus, hasStatus);
        }

        public static EnemyIntentKind KindFor(bool usingSkill, StatusEffectType? appliesStatus, bool hasStatus)
        {
            if (!usingSkill) return EnemyIntentKind.Attack;
            if (!hasStatus || !appliesStatus.HasValue) return EnemyIntentKind.Skill;

            switch (appliesStatus.Value)
            {
                case StatusEffectType.Poison: return EnemyIntentKind.Poison;
                case StatusEffectType.Stun: return EnemyIntentKind.Stun;
                // Feared reads as the same "your turn is not going to go the
                // way you planned" warning Stun already carries -- there is
                // no dedicated fear icon, and Stun's is the closer lie than
                // the generic Skill fallback would be.
                case StatusEffectType.Feared: return EnemyIntentKind.Stun;
                case StatusEffectType.Vulnerable: return EnemyIntentKind.Weaken;
                case StatusEffectType.Regen: return EnemyIntentKind.Heal;
                case StatusEffectType.Protect: return EnemyIntentKind.Shield;
                case StatusEffectType.Bleed: return EnemyIntentKind.Bleed;
                default: return EnemyIntentKind.Skill;
            }
        }
    }
}
