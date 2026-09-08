using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // One row of the command submenu, already decided.
    //
    // A MODEL rather than a painting instruction: it says what the row means
    // (this is the name, this is unaffordable) and never how to draw it. That
    // split is what lets every rule below be tested without a scene, and what
    // lets the design preview drive the identical code path with made-up rows --
    // v1's preview built its own, which is how it ended up drawing rows at
    // 8-slot positions while the real menu drew them at N-slot ones.
    public readonly struct SubmenuRow
    {
        public readonly string Name;
        public readonly string Meta;
        public readonly string Cost;

        // Two DIFFERENT reasons a row can be uncastable, kept apart rather than
        // OR'd into one flag: "no mana" and "requirement unmet" render
        // identically today, and the moment anything wants to say WHICH, the
        // distinction has to already exist here.
        public readonly bool CanPay;
        public readonly bool MeetsRequirement;

        // What hovering this row previews on the mana bar.
        public readonly int ManaCost;

        // A THIRD reason, kept apart for the same stated reason as the other
        // two: "no mana" and "not yet" are different sentences, and a row that
        // greyed out without saying which leaves the player checking their mana
        // bar for an answer that is not there.
        public readonly int CooldownRemaining;

        public bool Affordable => CanPay && MeetsRequirement && CooldownRemaining <= 0;

        public SubmenuRow(string name, string meta, string cost, bool canPay, bool meetsRequirement,
                          int manaCost, int cooldownRemaining = 0)
        {
            Name = name;
            Meta = meta;
            Cost = cost;
            CanPay = canPay;
            MeetsRequirement = meetsRequirement;
            CooldownRemaining = cooldownRemaining;
            ManaCost = manaCost;
        }
    }

    // One consumable stack, as the fight sees it. Core owns the inventory and
    // hands these over already resolved -- the satchel lives in RunState and has
    // no business being reachable from combat.
    public readonly struct SatchelStack
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly int Count;
        public readonly bool RestoresMana;

        public SatchelStack(string itemId, string displayName, int count, bool restoresMana)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Count = count;
            RestoresMana = restoresMana;
        }
    }

    // What the detail column is describing.
    public sealed class DetailPanel
    {
        public string Name = "";
        public string Kind = "";
        public string Body = "";
        public readonly List<(string Key, string Value)> Stats = new List<(string, string)>();

        // The skill's damage type ("Fire", "Arcane", ...), or "" for a
        // non-damaging skill -- see FightHudModel.DamageTypeLabel. A
        // DEDICATED field rather than a sixth Stats row: FightMenuStateTests
        // already pins Stats at exactly five entries by index (SCALES is
        // Stats[4]), so a new row would renumber every one of them for
        // whichever screen places this label. The UiKit side decides where
        // (and whether) to show it; this model only has to carry the string.
        public string DamageType = "";
    }

    // The fight HUD's contents, derived from the session and nothing else.
    public static class FightHudModel
    {
        // Column B for the skill branch.
        //
        // UNAFFORDABLE ROWS ARE INCLUDED AND DIMMED. They are the character's
        // own kit; the player should learn what they have rather than watch the
        // list change length as their wool moves, and what stands in the way is
        // a resource that comes back this turn or the next.
        //
        // NOTHING LOCKED REACHES HERE. Two gates upstream see to it: the kit is
        // built from skills whose unlockLevel the character has reached, and
        // SkillOptionsFor drops any whose ability requirements are unmet -- and
        // it filters THERE rather than here on purpose, because an option
        // carries its own index into kit.Skills and the dispatcher reads it, so
        // filtering rows alone would cast a different skill than the one
        // pressed.
        //
        // This used to re-test the requirement anyway and prefix the meta line
        // with LOCKED for a failure that could no longer happen. Removed rather
        // than left as a belt: a second copy of a rule that cannot fire is not
        // a safety net, it is a claim that the list can contain locked entries,
        // and it reads as one to anyone deciding where to add the next gate.
        public static IReadOnlyList<SubmenuRow> SkillRows(FightSession session, CombatantState actor) =>
            SkillRows(session?.SkillOptionsFor(actor), actor);

        // Same rows, built from an OPTION LIST ALREADY IN HAND rather than
        // asking the session for it -- FightController's RefreshMenuChrome
        // computes SkillOptionsFor(actor) once per repaint and hands it
        // straight through here, so this overload is what lets that single
        // computation serve the submenu without a second call to the
        // session hiding inside it. The (session, actor) overload above
        // still exists for callers -- the EditMode tests among them -- who
        // only have the session, and just forwards into this one.
        public static IReadOnlyList<SubmenuRow> SkillRows(IReadOnlyList<ResolvedSkillOption> options, CombatantState actor)
        {
            var rows = new List<SubmenuRow>();
            if (options == null || actor == null) return rows;

            string resourceName = actor.Signature?.DisplayName;

            foreach (var option in options)
            {
                var skill = option.Skill;
                if (!actor.AbilityScores.Meets(RequirementCurve.Apply(skill.Requirements))) continue;

                rows.Add(new SubmenuRow(
                    skill.DisplayName,
                    MetaLine(skill),

                    // THE COST COLUMN SAYS THE WAIT INSTEAD, while there is
                    // one. A row showing "8 MP" that cannot be pressed is
                    // telling the player about the only thing that is NOT
                    // stopping them.
                    option.CooldownRemaining > 0
                        ? CooldownLabel(option.CooldownRemaining)
                        : CostLabel(skill, resourceName),
                    option.Affordable,
                    meetsRequirement: true,
                    skill.ManaCost,
                    cooldownRemaining: option.CooldownRemaining));
            }

            return rows;
        }

        // Column B at Element depth: one row per element the skill offers, in
        // the order it authored them.
        //
        // NO COST TEXT AND ALWAYS AFFORDABLE. The choice is free -- the skill's
        // own cost was already shown on the row that led here, and repeating it
        // four times would read as four prices. Affordability was decided one
        // depth up too: an unaffordable skill's row cannot be pressed, so no
        // element list is ever reached for one.
        //
        // The meta line is the skill's, restated once per row rather than left
        // blank, so the column still says what is being cast while the skill
        // list is folded away behind it.
        public static IReadOnlyList<SubmenuRow> ElementRows(ResolvedSkill skill)
        {
            var rows = new List<SubmenuRow>();
            if (skill == null || !skill.HasElementChoice) return rows;

            foreach (var choice in skill.Elements)
            {
                if (choice == null) continue;

                rows.Add(new SubmenuRow(
                    choice.Type.ToString().ToUpperInvariant(),
                    MetaLine(skill),
                    "",
                    canPay: true,
                    meetsRequirement: true,
                    manaCost: 0));
            }

            return rows;
        }

        // Column B for the item branch. Items never cost mana, so nothing here
        // previews on the bar.
        public static IReadOnlyList<SubmenuRow> ItemRows(IReadOnlyList<SatchelStack> satchel)
        {
            var rows = new List<SubmenuRow>();
            if (satchel == null) return rows;

            foreach (var stack in satchel)
            {
                rows.Add(new SubmenuRow(
                    stack.DisplayName,
                    stack.RestoresMana ? "CONSUMABLE  ·  MANA" : "CONSUMABLE  ·  HEALTH",
                    "x" + stack.Count,
                    canPay: stack.Count > 0,
                    meetsRequirement: true,
                    manaCost: 0));
            }

            return rows;
        }

        // Column B for the move branch. TWO ROWS, ALWAYS BOTH SHOWN, one per
        // direction -- an illegal one is dimmed with its reason in the cost
        // column rather than dropped, because a list whose length changes with
        // the formation is a list the player has to re-read every turn, and
        // "you cannot go forward, you are already in front" is more use than
        // a row that quietly is not there.
        //
        // Row order is FORWARD then BACK, matching the direction the stage
        // draws: index 0 is toward rank 0.
        public static IReadOnlyList<SubmenuRow> MoveRows(FightSession session, CombatantState actor)
        {
            return new List<SubmenuRow>
            {
                MoveRow(session, actor, MoveDirection.Forward, "FORWARD", "MOVE  ·  TOWARD THE FRONT"),
                MoveRow(session, actor, MoveDirection.Back, "BACK", "MOVE  ·  TOWARD THE REAR"),
            };
        }

        private static SubmenuRow MoveRow(FightSession session, CombatantState actor,
            MoveDirection direction, string label, string meta)
        {
            bool legal = session != null && session.CanMove(actor, direction);

            // ENDS THE TURN is the cost, and the row says so -- Move is the
            // only command in the menu that spends a whole turn for no number
            // anywhere, and a blank cost column would read as "free".
            return new SubmenuRow(label, meta, legal ? "ENDS TURN" : "NO ROOM",
                canPay: legal, meetsRequirement: true, manaCost: 0);
        }

        // "DamageSingle" + SingleEnemy -> "DAMAGE  ·  SINGLE".
        //
        // Generated from the skill's own fields rather than authored, so it
        // cannot drift from what the skill actually does -- the same rule the
        // item cards and the equipment summary already follow.
        //
        // WHAT it does and WHO it reaches are two facts, and the enum name
        // conflates them. Printing the enum put "DAMAGESINGLE  ·  SINGLE" on
        // screen: a word no player uses, saying "single" twice. The effect now
        // contributes only the verb and the targeting contributes only the
        // reach, so the two halves stop repeating each other.
        public static string MetaLine(ResolvedSkill skill)
        {
            string reach = skill.Targeting == SkillTargeting.SingleEnemy ? "SINGLE"
                : skill.Targeting == SkillTargeting.Self ? "SELF"
                : "GROUP";
            return VerbFor(skill.Effect) + "  ·  " + reach;
        }

        // What a submenu describes when it is OPEN but nothing is chosen yet.
        //
        // The panel used to fall back to DetailForStrike here, so opening SKILL
        // and choosing nothing described the ATTACK verb -- name, body and all.
        // Not a stale panel: a wrong one, confidently rendered, while the
        // breadcrumb overhead read COMMAND > SKILL.
        public static DetailPanel DetailForNoSelection(MenuBranch branch)
        {
            return new DetailPanel
            {
                Name = branch == MenuBranch.Item ? "Items" : "Skills",
                Kind = branch == MenuBranch.Item ? "SATCHEL" : "REPERTOIRE",
                Body = branch == MenuBranch.Item
                    ? "Choose something to use."
                    : "Choose a skill to see what it does.",
            };
        }

        // The EFFECT row on the skill card, and the reach line beside it.
        //
        // EXHAUSTIVE, with no ToString() fallback. The fallback read fine for
        // half the enum and badly for the rest -- a Gift printed "GIFTMANA"
        // and the Lamb's Wail printed "BUFFPARTY", two identifiers leaking
        // into a player-facing card. Worse, it made the omission invisible:
        // an effect nobody wrote a verb for still rendered something. Throwing
        // is what turns "nobody chose a word for this" into a build the tests
        // stop, which is the only reason every member below has one.
        //
        // Public because SkillEffectBehaviourTests asks it the same question
        // per member that it asks the resolver and the intent badge.
        public static string VerbFor(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.DamageSingle:
                case SkillEffect.DamageAll: return "DAMAGE";
                case SkillEffect.HealSelf:
                case SkillEffect.HealParty: return "HEAL";
                case SkillEffect.RestorePartyMana: return "RESTORE";
                case SkillEffect.Provoke: return "TAUNT";
                case SkillEffect.Transform: return "TRANSFORM";
                case SkillEffect.Ward: return "WARD";
                case SkillEffect.Shatter: return "SHATTER";
                case SkillEffect.BuffParty: return "BUFF";
                case SkillEffect.GiftMana:
                case SkillEffect.GiftFury:
                case SkillEffect.GiftHaste: return "GIFT";
                case SkillEffect.Summon: return "SUMMON";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(effect), effect,
                        "FightHudModel has no EFFECT verb for this effect. Add one -- the card would " +
                        "otherwise print the enum identifier at the player.");
            }
        }

        // Both halves ALWAYS carry their unit.
        //
        // The column read "3", "4", "6 MP + 3", "5 MP", "0 MP" down a single
        // list -- three different grammars, and a bare number that silently
        // meant a different resource than the one beside it. A skill costing
        // nothing says so in words rather than showing "0 MP", which reads as a
        // missing value.
        // "2 TURNS" in the column a cost would occupy. Upper case to match the
        // costs it stands in for -- a row whose cost column suddenly changed
        // case would read as a different KIND of row rather than the same row
        // waiting.
        private static string CooldownLabel(int turns) =>
            turns == 1 ? "1 TURN" : $"{turns} TURNS";

        public static string CostLabel(ResolvedSkill skill, string resourceName)
        {
            string resource = string.IsNullOrWhiteSpace(resourceName) ? "" : " " + resourceName.ToUpperInvariant();

            if (skill.ResourceCost > 0 && skill.ManaCost > 0)
            {
                return $"{skill.ManaCost} MP + {skill.ResourceCost}{resource}";
            }

            if (skill.ResourceCost > 0) return $"{skill.ResourceCost}{resource}";
            return skill.ManaCost > 0 ? skill.ManaCost + " MP" : "FREE";
        }

        // What the POWER stat shows: pre-mitigation damage from the caster's
        // OWN stats/relics/buffs, before the target's armor/resistance/
        // reduction touches it. Was `skill.Power` printed verbatim, which
        // reads as 0 for any skill whose damage comes from `flatAmount`
        // instead of the per-resource-point formula -- mud_burst among them,
        // shown blank despite dealing real damage. FightSession.PreviewSkillPower
        // is the actual pre-mitigation figure for every skill shape (fixed
        // packets, flat, or resource-scaled) with no target and no RNG spent.
        // "-" rather than a printed "0" for the effects SkillResolution.Amount
        // has no number for at all (Provoke, Transform, BuffParty, the three
        // Gifts) -- their preview is a real 0, but showing "0 POWER" on the
        // card reads as "this does nothing" for an ability that redirects
        // aggro, transforms the caster, or hands an ally a status/resource.
        // Same idiom ScalingLabelForSkill below already uses for "no answer
        // here". Ward and Shatter DO have a number (talent-authored; see
        // SkillResolution.Amount) and print it like any other skill.
        private static bool HasNoPreviewablePower(SkillEffect effect) => effect switch
        {
            SkillEffect.Provoke => true,
            SkillEffect.Transform => true,
            SkillEffect.BuffParty => true,
            SkillEffect.GiftMana => true,
            SkillEffect.GiftFury => true,
            SkillEffect.GiftHaste => true,
            _ => false,
        };

        public static string PowerLabel(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            if (HasNoPreviewablePower(skill.Effect)) return "-";
            return session == null ? "0" : session.PreviewSkillPower(actor, skill).ToString();
        }

        public static DetailPanel DetailForSkill(FightSession session, CombatantState actor, ResolvedSkill skill, string resourceName = null)
        {
            var panel = new DetailPanel
            {
                Name = skill.DisplayName,
                Kind = "SKILL",
                Body = skill.Description ?? "",
            };

            // THE COST ROW SAYS THE WAIT INSTEAD, while there is one -- the
            // exact rule the submenu row's own cost column already follows
            // (see SkillRows above). Without this, mud_burst's card showed
            // "8 MP" whether or not it could actually be cast, which is how a
            // relic/skill that genuinely does have a cooldown reads as having
            // none: nothing on this card ever said so.
            int cooldown = session?.CooldownRemaining(actor, skill.Id) ?? 0;
            panel.Stats.Add(("COST", cooldown > 0 ? CooldownLabel(cooldown) : CostLabel(skill, resourceName)));
            panel.Stats.Add(("POWER", PowerLabel(session, actor, skill)));

            // Both of these printed the enum: "SINGLEENEMY" and "DAMAGESINGLE".
            // Same fix as the row's meta line, and it has to be the same words
            // or the panel and the row it describes disagree in front of the
            // player.
            panel.Stats.Add(("TARGET", ReachFor(skill.Targeting)));
            panel.Stats.Add(("EFFECT", VerbFor(skill.Effect)));
            panel.Stats.Add(("SCALES", ScalingLabelForSkill(session, actor, skill)));
            panel.DamageType = DamageTypeLabel(session, actor, skill);
            return panel;
        }

        // The skill's damage type, as a display string: "Fire", "Arcane", a
        // "/"-joined list for a multi-packet spell that authors more than
        // one (frost_flare: Fire and Ice), or "" for a skill that deals no
        // typed damage at all.
        //
        // FIXED-DAMAGE PACKETS ANSWER FROM THE SKILL ALONE -- each one
        // already carries its own authored type (ResolvedSkill.
        // DamageInstances), no caster needed. A non-fixed damaging skill
        // (Power/FlatAmount scaled off Attack) has no authored type of its
        // own -- it rides the CASTER's own attackType at cast time, the
        // identical resolve ScalingLabelForSkill just above already needs a
        // session+actor for, so this reads it the same way rather than
        // inventing a second answer to "what element is this cast".
        public static string DamageTypeLabel(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            if (!skill.IsDamaging) return "";

            // A CHOICE NOT YET MADE LISTS THE CHOICES, joined the same way a
            // multi-packet spell's types already are -- the row is answering
            // "what element is this", and "all four, you pick" is the true
            // answer until one is picked. Once it is, the card is built on
            // AsElement's copy, which offers nothing further and falls through
            // to the fixed-packet branch below with one type in it.
            if (skill.HasElementChoice)
            {
                return string.Join("/", skill.Elements.Where(e => e != null).Select(e => e.Type));
            }

            if (skill.HasFixedDamage)
            {
                var types = skill.DamageInstances.Select(i => i.type).Distinct();
                return string.Join("/", types);
            }

            if (session == null || actor == null) return "";
            var castType = session.ActorAttackType(actor) ?? DamageType.Physical;
            return castType.ToString();
        }

        // Which ability score this skill's damage actually rides, for the
        // SCALES row. "-" for anything that does not scale off an ability
        // score at all -- heals (flat + per-resource-point only, no
        // CombatMath.ScaledAttack term) and fixed-packet spells (their own
        // multiplier comes from the caster's spell TIER, not a grade on any
        // one score) both genuinely have no answer here, and saying so is more
        // honest than guessing.
        private static string ScalingLabelForSkill(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            if (session == null || actor == null) return "-";
            if (skill.HasFixedDamage) return "-";
            if (skill.Effect != SkillEffect.DamageSingle && skill.Effect != SkillEffect.DamageAll) return "-";

            var castType = session.ActorAttackType(actor) ?? DamageType.Physical;
            var axis = skill.ScalingAxis == ScalingAxis.Auto ? ScalingAxes.For(castType) : skill.ScalingAxis;
            var set = axis == ScalingAxis.Weapon ? actor.WeaponScaling
                    : axis == ScalingAxis.Spell ? actor.SkillScaling
                    : ScalingSet.None;
            return ScalingLabel(set);
        }

        // The strongest-graded ability score across all three scaling slots,
        // by SHORT NAME alone -- "STR", not "STR A · DEX C". The detail
        // card's value column is one short line shared with numbers like
        // "108" and "ONE ENEMY"; the full grade breakdown ScalingProfile.
        // Describe() prints belongs on an item tooltip, which has the room.
        public static string ScalingLabel(ScalingSet set)
        {
            AbilityScore? best = null;
            ScalingGrade bestGrade = ScalingGrade.None;

            foreach (AbilityScore score in AbilityScores.All)
            {
                var grade = set.SpellTier[score];
                if (set.MainHand[score] > grade) grade = set.MainHand[score];
                if (set.OffHand[score] > grade) grade = set.OffHand[score];
                if (grade == ScalingGrade.None) continue;

                if (best == null || grade > bestGrade)
                {
                    best = score;
                    bestGrade = grade;
                }
            }

            return best.HasValue ? AbilityScores.ShortName(best.Value) : "-";
        }

        private static string ReachFor(SkillTargeting targeting)
        {
            switch (targeting)
            {
                case SkillTargeting.SingleEnemy: return "ONE ENEMY";
                case SkillTargeting.Self: return "SELF";
                default: return "GROUP";
            }
        }

        // The synthetic entry for ATTACK, which has no authored skill behind it.
        // Reads its numbers off the actor so it describes the swing the player
        // is about to take rather than a generic one.
        //
        // POWER used to print the raw Attack stat, which is not the damage
        // number anywhere else on the sheet reads -- CombatMath scales Attack
        // by weapon scaling first. This is the same pre-mitigation reading
        // PreviewSkillPower gives a cast.
        //
        // BALANCE REDESIGN PHASE 3 (D3): `actor.Attack` IS WeaponPower now
        // for any player with a weapon equipped (FightEncounterAdapter.
        // ToCombatant sets it there, once, at combatant-build time) and the
        // character's own unarmed figure otherwise -- ScaledAttack reads
        // whichever is already sitting on Attack, so this reads WeaponPower
        // x ScalingMultiplier, raw, no defense, exactly the plan's "same
        // intent, right inputs" ask.
        //
        // 2026-08-26 (D1 fix): now calls ComputeAttackDamage directly instead
        // of duplicating Scale(ScaledAttack(...)) by hand -- the two entry
        // points every real swing goes through stopped multiplying by
        // CombatMath.DamageScale (see ComputeAttackDamage's own header), and
        // this readout has to keep reading exactly what a real Strike deals
        // or it goes stale the moment the production formula moves again.
        // `target` is unread by ComputeAttackDamage, so null is safe.
        public static DetailPanel DetailForStrike(CombatantState actor)
        {
            var panel = new DetailPanel
            {
                Name = "Strike",
                Kind = "ATTACK",
                Body = "A plain swing at one enemy in reach.",
            };
            panel.Stats.Add(("COST", "FREE"));
            panel.Stats.Add(("POWER", actor == null ? "0" : CombatMath.ComputeAttackDamage(actor, null).ToString()));
            panel.Stats.Add(("TARGET", "SINGLE"));
            panel.Stats.Add(("EFFECT", "DAMAGE"));
            panel.Stats.Add(("SCALES", actor == null ? "-" : ScalingLabel(actor.WeaponScaling)));
            return panel;
        }

        // ---- status rows -------------------------------------------------------
        //
        // What the badge row above a character (and the enemy plate's own
        // text line) reads on hover -- the party portrait had NOTHING before
        // this (TagLineFor is enemy-only text, no icon at all), so a player
        // could not tell Ballerina's Slippers had fired without opening a
        // menu that says nothing about it either.
        //
        // ONE STRUCT for every surface, replacing the BuffBadge/StatusBadge
        // pair this used to be split across -- see PLAN_STATUS_EFFECT_UI.md
        // section 4/11. Code, Slug, Tooltip and IsPositive are the badge's
        // whole vocabulary; Counter and SortKey are the two facts that used
        // to live only in the caller's head (a -1 meant "draw nothing" in
        // three different ways across BuffBadge/PillCode/EnemyStatusLine,
        // never spelled the same way twice).
        public readonly struct StatusRow
        {
            public readonly string Code;
            public readonly string Slug;
            public readonly string Tooltip;
            public readonly bool IsPositive;

            // -1 means "draw no number" -- a sentinel-duration status
            // (Shielded, Empowered, Marked) or a permanent speed grant
            // (TurnsLeft < 0). See StatusHud.SentinelTurns for the rule.
            public readonly int Counter;

            // Lower sorts first. See StatusHud.SortKeyFor/SpeedSortKey for
            // the tiering this encodes: action restrictions, then immediate-
            // exchange statuses, then everything else by harm/control/
            // benefit bucket and a fixed table position.
            public readonly int SortKey;

            public StatusRow(string code, string slug, string tooltip, bool isPositive, int counter, int sortKey)
            {
                Code = code;
                Slug = slug;
                Tooltip = tooltip;
                IsPositive = isPositive;
                Counter = counter;
                SortKey = sortKey;
            }
        }

        // Every status effect and every relic-granted speed buff/malus
        // currently on ONE combatant, as rows, in section 5's display order.
        // Two different stores (CombatantState.Statuses, FightSession's
        // private speed-buff dictionary) because that split is real -- see
        // FightSession.SpeedBuffs's own header -- so this is where they
        // finally become one list, for the one place a player actually
        // looks: the character they are about to act with. Replaces
        // BuffBadgesFor.
        //
        // SESSION IS OPTIONAL, actor is not (S3's review corrects the old
        // claim here: EnemyStatusLine does not call this at all any more --
        // Phase 3 retired its own status list to BRK only, see that
        // method's own header -- and nothing in this tree currently calls
        // StatusRowsFor with session == null). Kept optional anyway: a null
        // session still costs only the speed-buff half and, as of C2, the
        // Drowned Lantern's own mark below -- actor.Statuses needs no
        // session to read at all -- so a future no-session caller (a
        // preview render, a design tool) degrades rather than throws.
        public static List<StatusRow> StatusRowsFor(FightSession session, CombatantState actor)
        {
            var rows = new List<StatusRow>();
            if (actor == null) return rows;

            foreach (var status in actor.Statuses)
            {
                rows.Add(StatusHud.RowFor(status));
            }

            if (session != null)
            {
                var kit = session.KitFor(actor);
                foreach (var buff in session.ActiveSpeedBuffs(actor))
                {
                    // A STATUS-DRIVEN entry (Chilled, as of Phase D2) is
                    // already shown by the `foreach (var status in actor.
                    // Statuses)` loop above -- StatusHud.RowFor prints its
                    // own Chilled row straight off the ActiveStatus. Showing
                    // it again here as a second, generic speed row would be
                    // the same fact twice; this dictionary entry exists so
                    // the SPEED NUMBER stays correct (TrueBaseSpeed composes
                    // it with every relic buff), not so it gets its own row
                    // on top of the status's own.
                    if (buff.Source is StatusEffectType) continue;

                    string name = null;
                    if (kit != null && buff.Source is RelicEffect relicSource)
                    {
                        foreach (var relic in kit.Relics)
                        {
                            // Equals(...), not ==, now that Source is
                            // `object` -- see FightSession.SpeedBuffs.
                            // ActiveSpeedBuffs' own comment on why `==`
                            // between a boxed enum and `object` would
                            // silently compare by reference instead of by
                            // value.
                            if (relic.Effect == relicSource) { name = relic.DisplayName; break; }
                        }
                    }
                    name ??= buff.Source.ToString();

                    rows.Add(StatusHud.SpeedRow(name, buff.Granted, buff.TurnsLeft));
                }

                // THE DROWNED LANTERN'S OWN MARK (C2's review). This is
                // FightSession.Relics._marked/IsMarked, a completely separate
                // mechanic from StatusEffectType.Marked (Marks.cs) that
                // happens to read as the same word to a player -- see
                // FightSession.Relics.cs's own header on why it is tracked by
                // target rather than as a status. Nothing above this line
                // ever sees it: actor.Statuses has no entry for it at all, so
                // an enemy carrying only a Lantern mark and no Marked status
                // would otherwise show nothing here -- which is exactly the
                // "MK" pill the old EnemyStatusLine used to guarantee, but
                // the badge row's own de-duplication now has to give it a
                // home instead. Skipped when a real StatusEffectType.Marked
                // row already exists (the loop above already added it),
                // since both read as one "Marked" badge to a player and a
                // holder carrying both mechanics at once must never show it
                // twice -- StatusHudCoverageTests pins that exact case.
                bool alreadyMarked = rows.Any(r => r.Slug == StatusHud.SlugFor(StatusEffectType.Marked));
                if (!alreadyMarked && session.IsMarked(actor))
                {
                    // Built from the same StatusHud code/slug/tooltip/sort
                    // key as the status-backed row -- a synthetic ActiveStatus
                    // at the sentinel duration reads as "until it is used",
                    // which is exactly right: a Lantern mark is spent by the
                    // wearer's next attack (MarkBonus), never counted down.
                    rows.Add(StatusHud.RowFor(new ActiveStatus(StatusEffectType.Marked, 0, StatusHud.SentinelTurns)));
                }
            }

            // OrderBy, not List.Sort -- Sort is not stable, and two rows
            // sharing a SortKey (two different speed sources, say) must keep
            // the order they were added in rather than swap places on every
            // repaint.
            return rows.OrderBy(r => r.SortKey).ToList();
        }

        // Real artwork for a status badge, exactly EnemyIntentIcons' shape
        // one shelf over -- ResourceFor is RESOURCES-relative and
        // extension-free for the identical reason: Resources.Load returns
        // null (silently) for an Assets/ path or one carrying ".png", and
        // these are swapped at runtime as a combatant's statuses change.
        //
        // ALL FOURTEEN ship today (S3's review corrects this: the twelve
        // StatusEffectType slugs plus speed/speed_down, every one of them
        // under Assets/_Project/Resources/Status/ -- commissioned from
        // docs/STATUS_ICON_PROMPTS.md and keyed by tools/key_green_screen.py
        // --kit status; make_status_icons.py, the old producer, is retired).
        // The text CODE stays the fallback path regardless -- a future
        // fifteenth status/presentation with no art on disk yet still
        // resolves to a path nothing answers to, and Resources.Load returns
        // null for it exactly like it would for a typo. That null IS the
        // fallback mechanism: the caller (FightController.Hud's PaintBadge)
        // already has an icon-first/glyph-fallback-if-null priority, the
        // same one StageVisuals uses for enemy intents, so a status with no
        // art on disk degrades to its three-letter code with no special-
        // casing anywhere.
        public static class StatusBadgeIcons
        {
            public static string ResourceFor(StatusEffectType kind) => "Status/" + StatusHud.SlugFor(kind);

            // The two speed presentations have no StatusEffectType member
            // (PLAN_STATUS_EFFECT_UI.md section 4 -- a presentation id, not
            // a thirteenth enum member), so they resolve off the row's own
            // Slug instead of a kind. Works for any row, not only speed's:
            // Resources-relative and extension-free the same way the
            // StatusEffectType overload above is.
            public static string ResourceFor(StatusRow row) => "Status/" + row.Slug;
        }

        // ---- enemy plate status line (Phase 3: retired to BRK only) --------
        //
        // Used to join every status into a "PSN·3  ·  VLN·2  ·  +1" line
        // squeezed under the HP bar in 8pt text -- PLAN_STATUS_EFFECT_UI.md
        // section 9 (Phase 3) retires that: the same statuses already read,
        // full-size, off the enemy's own stage row (BuildEnemyStatusRows,
        // FightController.RefreshEnemyStatusRows), and a plate line
        // repeating them was "two surfaces for one fact on a 200x64 card",
        // the plate's own comment's own warning turned against itself.
        //
        // BRK SURVIVES because it has no other surface. BreakShield is not a
        // StatusEffectType -- StatusRowsFor never sees it, so no badge in the
        // stage row represents it -- and it is the one always-shown fact this
        // line used to guarantee could never be bumped into a "+N" overflow.
        // The Tags node this writes into stays in the screen tree for exactly
        // this prefix; see FightScreen.BuildEnemyPlates.
        //
        // `session` is unread now that the status list is gone from THIS
        // line -- it moved, not disappeared. C2's review found the Marked-
        // vs-Marked de-duplication had actually been dropped, not relocated:
        // StatusRowsFor read only actor.Statuses, so an enemy carrying a
        // Drowned Lantern mark and no StatusEffectType.Marked entry showed
        // nothing anywhere at all. StatusRowsFor now reads session.IsMarked
        // itself and appends the row there, which is what makes `session`
        // unread here rather than merely redundant. Kept as a
        // parameter rather than dropped: the call site still has a session
        // in scope, and losing the parameter now would just have to come
        // back the day this line grows a second always-shown fact.
        private const string BrokenHex = "#FFC45A";

        public static string EnemyStatusLine(CombatantState enemy, FightSession session)
        {
            if (enemy == null || enemy.BreakShield == null || !enemy.BreakShield.IsBroken) return "";

            return ItemStatLines.Coloured(BrokenHex, "BRK");
        }

        public static DetailPanel DetailForItem(SatchelStack stack)
        {
            var panel = new DetailPanel
            {
                Name = stack.DisplayName,
                Kind = "ITEM",
                Body = stack.RestoresMana ? "Restores mana." : "Restores health.",
            };
            panel.Stats.Add(("COST", "1"));
            panel.Stats.Add(("POWER", "-"));
            panel.Stats.Add(("TARGET", "SELF"));
            panel.Stats.Add(("EFFECT", stack.RestoresMana ? "MANA" : "HEAL"));
            return panel;
        }

        // The breadcrumb under the verb column. Letterspaced by hand, as v1
        // authored it -- that spacing is typography, not data.
        public static string Breadcrumb(FightMenuState menu)
        {
            if (menu == null || menu.Depth == MenuDepth.Root) return "C O M M A N D";

            string word = menu.Branch == MenuBranch.Skill ? "S K I L L"
                : menu.Branch == MenuBranch.Item ? "I T E M"
                : "A T T A C K";

            string trail = "C O M M A N D  ›  " + word;

            // THE ELEMENT STEP IS SHOWN AT BOTH DEPTHS IT IS TRUE AT: standing
            // on the element list, and standing on the target list having come
            // through it. A breadcrumb that dropped the element on the way to
            // targeting would be the one line on screen still claiming the
            // player has a choice left to make.
            if (menu.Depth == MenuDepth.Element || menu.ChosenElement.HasValue)
            {
                trail += "  ›  E L E M E N T";
            }

            return menu.Depth == MenuDepth.Sub || menu.Depth == MenuDepth.Element
                ? trail
                : trail + "  ›  T A R G E T";
        }

        // How many enemies are still up, for the hint above the plates.
        public static int StandingCount(CombatEncounter encounter) =>
            encounter == null ? 0 : encounter.LivingEnemies.Count();

        // ---- telling two of the same monster apart -------------------------------
        //
        // Three plates reading "Giant Rat", "Giant Rat", "Giant Rat" name the
        // KIND and leave the player to work out which is which from position --
        // which they cannot, because the plates are a list on the right and the
        // rats are a row in the middle, and the two orders are only incidentally
        // the same. It matters the moment one of them is at 4 HP.
        //
        // NUMBERED ONLY WHEN THERE IS SOMETHING TO TELL APART. A lone Bog Witch
        // is "Bog Witch", not "Bog Witch 1" -- the ordinal is information, and
        // adding one where there is no ambiguity is noise that also makes the
        // encounter read as a spawn table.
        //
        // Over the WHOLE list including the dead, so a rat's number does not
        // change when the rat beside it falls. A name that renumbers mid-fight
        // is worse than no name: the player has already learned which one they
        // were hurting.
        public static IReadOnlyList<string> DisplayNames(IReadOnlyList<CombatantState> combatants)
        {
            var names = new List<string>();
            if (combatants == null) return names;

            var totals = new Dictionary<string, int>();
            foreach (var c in combatants)
            {
                if (c == null) continue;
                totals.TryGetValue(c.Name, out int seen);
                totals[c.Name] = seen + 1;
            }

            var used = new Dictionary<string, int>();
            foreach (var c in combatants)
            {
                if (c == null)
                {
                    names.Add("");
                    continue;
                }

                if (totals[c.Name] <= 1)
                {
                    names.Add(c.Name);
                    continue;
                }

                used.TryGetValue(c.Name, out int n);
                used[c.Name] = n + 1;
                names.Add($"{c.Name} {n + 1}");
            }

            return names;
        }

        // One combatant's display name out of the list it belongs to. The plates
        // walk the list and take the whole thing; the nameplate under a figure
        // and the intent tooltip each want one, and both have to agree with the
        // plates or the screen contradicts itself.
        public static string DisplayNameOf(IReadOnlyList<CombatantState> combatants, CombatantState combatant)
        {
            if (combatants == null || combatant == null) return combatant?.Name ?? "";

            var names = DisplayNames(combatants);
            for (int i = 0; i < combatants.Count && i < names.Count; i++)
            {
                if (ReferenceEquals(combatants[i], combatant)) return names[i];
            }

            return combatant.Name;
        }

        // ---- enemy intent -------------------------------------------------------

        // The sentence behind an intent icon.
        //
        // Written HERE, whole, rather than assembled from three labels in the
        // view: the phrasing is content, and splitting it across nodes would put
        // the grammar in the layout. "about" is load-bearing -- the figure
        // excludes variance and any ward the target still holds, so promising an
        // exact number would be a lie the player could catch in one hit.
        public static string IntentTooltip(string enemyName, EnemyIntent intent)
        {
            string verb = intent.IsAttack ? "attack" : intent.Label;

            // A SUMMON HAS NO OBJECT. "will Roar itself" is grammatical and
            // says nothing; what the player needs is that the fight is about to
            // be one monster bigger. Its own sentence rather than a fourth
            // `who` case, because the shape of the sentence is what differs --
            // and BEFORE the switch, which it would otherwise run in full and
            // then throw away.
            if (intent.Kind == EnemyIntentKind.Summon)
            {
                return $"{enemyName} will {verb}, calling in another monster";
            }

            // PHASE D3 FIX: a Rooted enemy with nothing left to cast. Its own
            // sentence, before the switch below, for the same reason Summon's
            // is -- "will Forfeits Turn itself" is what the generic template
            // would produce, and that is not a sentence. FightSession.IntentForfeit
            // is compared against as a sentinel, matching how TelegraphSuffix
            // already compares Label against FightSession.IntentAttack.
            if (intent.Label == FightSession.IntentForfeit)
            {
                return $"{enemyName} has nothing left to cast and will forfeit its turn";
            }

            // WHO, BY SCOPE. A telegraph that names one target for an effect
            // landing on everybody is worse than no telegraph: it is a specific
            // claim, and it is false. See EnemyIntentScope.
            string who;
            switch (intent.Scope)
            {
                case EnemyIntentScope.AllOpponents:
                    who = "your whole party";
                    break;
                case EnemyIntentScope.Self:
                    who = "itself";
                    break;
                case EnemyIntentScope.AllAllies:
                    who = "its allies";
                    break;
                default:
                    who = string.IsNullOrEmpty(intent.TargetName) ? "someone" : intent.TargetName;
                    break;
            }

            string line = $"{enemyName} will {verb} {who}";
            if (intent.ExpectedDamage <= 0) return line;

            // THE SIGN, SAID IN WORDS. ExpectedDamage is a magnitude, and a
            // magnitude with no sign reads as a threat -- "for about 40" under a
            // heal icon is the one sentence that could send a player to kill the
            // wrong monster.
            string what = intent.Heals ? "healing" : "damage";

            // "each" only where there is more than one of them to be each of.
            string spread = intent.Scope == EnemyIntentScope.AllOpponents
                            || intent.Scope == EnemyIntentScope.AllAllies
                ? " each"
                : "";

            return line + $"\nfor about {intent.ExpectedDamage} {what}{spread}";
        }
    }
}
