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

        // The basic spell's row, which is synthesised rather than authored --
        // every character has one and no character has it in their skill list.
        public readonly bool IsBasicSpell;

        // A THIRD reason, kept apart for the same stated reason as the other
        // two: "no mana" and "not yet" are different sentences, and a row that
        // greyed out without saying which leaves the player checking their mana
        // bar for an answer that is not there.
        public readonly int CooldownRemaining;

        public bool Affordable => CanPay && MeetsRequirement && CooldownRemaining <= 0;

        public SubmenuRow(string name, string meta, string cost, bool canPay, bool meetsRequirement,
                          int manaCost, bool isBasicSpell = false, int cooldownRemaining = 0)
        {
            Name = name;
            Meta = meta;
            Cost = cost;
            CanPay = canPay;
            MeetsRequirement = meetsRequirement;
            CooldownRemaining = cooldownRemaining;
            ManaCost = manaCost;
            IsBasicSpell = isBasicSpell;
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
    }

    // The fight HUD's contents, derived from the session and nothing else.
    public static class FightHudModel
    {
        // Where the basic spell sits in the skill branch: after the authored
        // skills, always. Stated once so the row index and the dispatch cannot
        // disagree about which row means "cast the basic spell".
        public static int BasicSpellRow(int authoredSkillCount) => authoredSkillCount;

        public static int SkillRowCount(int authoredSkillCount) => authoredSkillCount + 1;

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
        public static IReadOnlyList<SubmenuRow> SkillRows(FightSession session, CombatantState actor)
        {
            var rows = new List<SubmenuRow>();
            if (session == null || actor == null) return rows;

            string resourceName = actor.Signature?.DisplayName;

            foreach (var option in session.SkillOptionsFor(actor))
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

            // The basic spell's own row. Its cost comes through the same
            // SkillResolution every authored skill uses -- v1 hand-rolled a
            // separate mana check here, which is one of the two bugs that died
            // in the move.
            int basicCost = session.BasicSpellManaCostFor(actor);
            rows.Add(new SubmenuRow(
                session.BasicSpellNameFor(actor),
                "ARCANE  ·  SINGLE",
                basicCost + " MP",
                SkillResolution.CanAfford(actor, basicCost, 0),
                meetsRequirement: true,
                manaCost: basicCost,
                isBasicSpell: true));

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

        private static string VerbFor(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.DamageSingle:
                case SkillEffect.DamageAll: return "DAMAGE";
                case SkillEffect.HealSelf:
                case SkillEffect.HealParty: return "HEAL";
                case SkillEffect.RestorePartyMana: return "RESTORE";
                case SkillEffect.Provoke: return "TAUNT";
                default: return effect.ToString().ToUpperInvariant();
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
        public static string PowerLabel(FightSession session, CombatantState actor, ResolvedSkill skill) =>
            session == null ? "0" : session.PreviewSkillPower(actor, skill).ToString();

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
            return panel;
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

        // ---- buff badges -----------------------------------------------------
        //
        // What the icon row above a character reads on hover -- the party
        // portrait had NOTHING before this (TagLineFor is enemy-only text, no
        // icon at all), so a player could not tell Ballerina's Slippers had
        // fired without opening a menu that says nothing about it either.
        //
        // A GLYPH short enough for a small circle, a TOOLTIP sentence with the
        // real numbers, and whether it reads as helping or hurting -- that is
        // everything the badge needs to know; the icon's actual art (or its
        // fallback tint) is a painting decision, made by whoever reads this.
        public readonly struct BuffBadge
        {
            public readonly string Glyph;
            public readonly string Tooltip;
            public readonly bool IsPositive;

            // Null for badges with no status behind them at all (the relic
            // speed-buff "SPD" entry below) and for every status that has no
            // authored icon -- StatusBadgeIcons.ResourceFor resolves to a
            // real sprite only for Chilled and Rooted today, and
            // Resources.Load returning null for the rest is exactly the
            // icon-first/glyph-fallback path this field exists to drive, the
            // same way EnemyIntentIcons drives the enemy telegraph badges.
            public readonly StatusEffectType? Kind;

            public BuffBadge(string glyph, string tooltip, bool isPositive, StatusEffectType? kind = null)
            {
                Glyph = glyph;
                Tooltip = tooltip;
                IsPositive = isPositive;
                Kind = kind;
            }
        }

        // Every status effect and every relic-granted speed buff/malus
        // currently on ONE combatant, as badges. Two different stores
        // (CombatantState.Statuses, FightSession's private speed-buff
        // dictionary) because that split is real -- see FightSession.
        // SpeedBuffs's own header -- so this is where they finally become one
        // list, for the one place a player actually looks: the character
        // they are about to act with.
        public static List<BuffBadge> BuffBadgesFor(FightSession session, CombatantState actor)
        {
            var badges = new List<BuffBadge>();
            if (session == null || actor == null) return badges;

            foreach (var status in actor.Statuses)
            {
                badges.Add(StatusBadge(status));
            }

            var kit = session.KitFor(actor);
            foreach (var buff in session.ActiveSpeedBuffs(actor))
            {
                // A STATUS-DRIVEN entry (Chilled, as of Phase D2) is already
                // shown by the `foreach (var status in actor.Statuses)` loop
                // above -- StatusBadge prints its own "Chilled: ..." tooltip
                // straight off the ActiveStatus. Showing it again here as a
                // second, generic "SPD" badge would be the same fact twice;
                // this dictionary entry exists so the SPEED NUMBER stays
                // correct (TrueBaseSpeed composes it with every relic buff),
                // not so it gets its own badge on top of the status's own.
                if (buff.Source is StatusEffectType) continue;

                string name = null;
                if (kit != null && buff.Source is RelicEffect relicSource)
                {
                    foreach (var relic in kit.Relics)
                    {
                        // Equals(...), not ==, now that Source is `object` --
                        // see FightSession.SpeedBuffs.ActiveSpeedBuffs' own
                        // comment on why `==` between a boxed enum and
                        // `object` would silently compare by reference
                        // instead of by value.
                        if (relic.Effect == relicSource) { name = relic.DisplayName; break; }
                    }
                }
                name ??= buff.Source.ToString();

                bool positive = buff.Granted > 0;
                string turns = buff.TurnsLeft < 0 ? "for the rest of the fight" : Plural(buff.TurnsLeft, "turn");
                string tooltip = $"{name}: {(positive ? "+" : "")}{buff.Granted} speed, {turns}.";
                badges.Add(new BuffBadge("SPD", tooltip, positive));
            }

            return badges;
        }

        private static BuffBadge StatusBadge(ActiveStatus status)
        {
            string turns = Plural(status.TurnsRemaining, "turn");
            switch (status.Type)
            {
                case StatusEffectType.Poison:
                    return new BuffBadge("PSN", $"Poison: {status.Magnitude} damage at the start of your turn, {turns}.", false);
                case StatusEffectType.Regen:
                    return new BuffBadge("RGN", $"Regen: {status.Magnitude} healing at the start of your turn, {turns}.", true);
                case StatusEffectType.Protect:
                    return new BuffBadge("PRT", $"Protect: incoming damage reduced {status.Magnitude}%, {turns}.", true);
                case StatusEffectType.Vulnerable:
                    return new BuffBadge("VLN", $"Vulnerable: incoming damage increased {status.Magnitude}%, {turns}.", false);
                case StatusEffectType.Stun:
                    return new BuffBadge("STN", "Stunned: this turn is skipped.", false);
                case StatusEffectType.Shielded:
                    return new BuffBadge("SHD", $"Shielded: the next hit taken is reduced {status.Magnitude}%.", true);
                case StatusEffectType.Provoked:
                    return new BuffBadge("PRV", $"Provoked: the next attack must target whoever provoked it, for {status.Magnitude}% less damage to them.", false);
                case StatusEffectType.Empowered:
                    return new BuffBadge("EMP", $"Empowered: the next attack deals {status.Magnitude}% more damage, then it's spent.", true);

                // KEYWORDS, not sentences -- the two statuses with real icon
                // art (StatusBadgeIcons.ResourceFor) also get the terse
                // hover text the designer actually asked for: "a symbol
                // under a character... hovering over this symbol shows what
                // it does (again, in keywords)". The other statuses above
                // keep their existing sentence tooltips; retexting all eight
                // is out of scope for this change.
                //
                // The number is coloured with the SAME hex the affix-text
                // rewrite uses for a stat loss (ItemStatLines.LossHex) --
                // the one rich-text precedent this codebase has, so a
                // player who has already learned "red numbers are bad" from
                // gear tooltips reads the same colour the same way here.
                case StatusEffectType.Chilled:
                    return new BuffBadge("CHL",
                        $"Chilled -- {ItemStatLines.Coloured(ItemStatLines.LossHex, $"-{status.Magnitude}% Speed")}, {PluralTerse(status.TurnsRemaining, "turn")}",
                        false, StatusEffectType.Chilled);
                case StatusEffectType.Rooted:
                    return new BuffBadge("ROT",
                        $"Rooted -- {ItemStatLines.Coloured(ItemStatLines.LossHex, "Skill Only")}, {PluralTerse(status.TurnsRemaining, "turn")}",
                        false, StatusEffectType.Rooted);
                default:
                    return new BuffBadge("?", status.Type.ToString(), true);
            }
        }

        private static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")} left";

        // Same count/pluralisation as Plural, without the trailing "left" --
        // a keyword tooltip reads "2 turns", not "2 turns left, full stop".
        private static string PluralTerse(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

        // Real artwork for a status badge, exactly EnemyIntentIcons' shape
        // one shelf over -- ResourceFor is RESOURCES-relative and
        // extension-free for the identical reason: Resources.Load returns
        // null (silently) for an Assets/ path or one carrying ".png", and
        // these are swapped at runtime as a combatant's statuses change.
        //
        // Only Chilled and Rooted have art today (Status/chilled.png,
        // Status/rooted.png -- see tools/art/make_status_icons.py). Every
        // other status deliberately has NO slug below, so ResourceFor for
        // e.g. Poison resolves to "Status/poison" -- a path nothing on disk
        // answers to -- and Resources.Load returns null for it exactly like
        // it would for a typo. That null IS the fallback mechanism: the
        // caller (FightController.Hud's RefreshPartyBuffs) already has an
        // icon-first/glyph-fallback-if-null priority, the same one
        // StageVisuals uses for enemy intents, so a status with no art on
        // disk degrades to its three-letter glyph with no special-casing
        // anywhere -- the glyph was always the fallback, adding an icon for
        // two kinds just gives the fallback something to fall back FROM.
        public static class StatusBadgeIcons
        {
            public static string ResourceFor(StatusEffectType kind) => "Status/" + Slug(kind);

            private static string Slug(StatusEffectType kind)
            {
                switch (kind)
                {
                    case StatusEffectType.Chilled: return "chilled";
                    case StatusEffectType.Rooted: return "rooted";

                    // No art authored for anything else. The slug still has
                    // to be SOME string (ResourceFor is unconditional so the
                    // caller never has to ask "does this kind even have a
                    // resource path" before loading), but it must never
                    // collide with a real file -- lower-casing the enum name
                    // keeps every future status equally not-found until art
                    // actually lands for it.
                    default: return kind.ToString().ToLowerInvariant();
                }
            }
        }

        // ---- enemy plate status line (v2 rework) ----------------------------
        //
        // Replaces a flat "REELING · POISON · CHILLED · MARKED" join that was
        // never tested against a real fight's worth of statuses -- an elite
        // carrying four statuses plus a break meter had no wrap rule and no
        // reading order. Two fixes: a CAP (three codes, the rest folds into a
        // "+N"), and a PRIORITY order -- harm first, since that is what
        // changes whether the target is worth worrying about.
        //
        // BROKEN IS NOT ONE OF THE THREE. An earlier draft of this pass added
        // it to the same list and let it sort by category with everything
        // else, which meant an ordinary Poison/Vulnerable/Chilled trio could
        // push it into the "+N" overflow -- the one state that must never
        // hide. It is a distinct, always-shown prefix instead, so it can
        // never lose its place to a status.
        private enum PillCategory { Harm, Control, Special, Benefit }

        private static PillCategory CategoryOf(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison:
                case StatusEffectType.Vulnerable:
                case StatusEffectType.Stun:
                    return PillCategory.Harm;
                case StatusEffectType.Provoked:
                case StatusEffectType.Chilled:
                case StatusEffectType.Rooted:
                    return PillCategory.Control;
                case StatusEffectType.Regen:
                case StatusEffectType.Protect:
                case StatusEffectType.Shielded:
                case StatusEffectType.Empowered:
                    return PillCategory.Benefit;
                default:
                    return PillCategory.Special;
            }
        }

        private static string PillCode(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return "PO";
                case StatusEffectType.Regen: return "RG";
                case StatusEffectType.Protect: return "PR";
                case StatusEffectType.Vulnerable: return "VU";
                case StatusEffectType.Stun: return "ST";
                case StatusEffectType.Shielded: return "SH";
                case StatusEffectType.Provoked: return "PV";
                case StatusEffectType.Empowered: return "EM";
                case StatusEffectType.Chilled: return "CH";
                case StatusEffectType.Rooted: return "RO";
                default: return "??";
            }
        }

        private const int EnemyPillCap = 3;

        // The same amber FightHudPalette.TargetAmber already carries, repeated
        // as a literal rather than reaching into UiKit from this Session-layer
        // file for one hex string.
        private const string BrokenHex = "#FFC45A";

        public static string EnemyStatusLine(CombatantState enemy, FightSession session)
        {
            if (enemy == null) return "";

            string broken = enemy.BreakShield != null && enemy.BreakShield.IsBroken
                ? ItemStatLines.Coloured(BrokenHex, "BRK") + "  ·  "
                : "";

            var pills = new List<(string Code, int Turns, PillCategory Category)>();
            foreach (var status in enemy.Statuses)
            {
                pills.Add((PillCode(status.Type), status.TurnsRemaining, CategoryOf(status.Type)));
            }

            // MARKED is not a StatusEffectType entry -- see
            // FightSession.Relics.ApplyMark for why it stays outside that
            // system. Turns 0 suppresses the "·N" suffix, the same way BRK's
            // own prefix carries none -- a mark has no ticking duration.
            if (session != null && session.IsMarked(enemy))
            {
                pills.Add(("MK", 0, PillCategory.Special));
            }

            var ordered = pills.OrderBy(p => p.Category).ToList();
            int overflow = ordered.Count - EnemyPillCap;
            var shown = ordered.Take(EnemyPillCap)
                .Select(p => p.Turns > 0 ? $"{p.Code}·{p.Turns}" : p.Code);

            string line = broken + string.Join("  ·  ", shown);
            if (overflow > 0) line += $"  ·  +{overflow}";
            return line;
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

            return menu.Depth == MenuDepth.Sub
                ? "C O M M A N D  ›  " + word
                : "C O M M A N D  ›  " + word + "  ›  T A R G E T";
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
