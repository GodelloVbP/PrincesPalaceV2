using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
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
        // by weapon scaling first and puts the result on the same x10 scale
        // every other damage number is on. Scale(ScaledAttack(...)) with no
        // defense term is exactly ComputeAttackDamage's own formula minus the
        // target's EffectiveDefense, i.e. the same pre-mitigation reading
        // PreviewSkillPower gives a cast.
        public static DetailPanel DetailForStrike(CombatantState actor)
        {
            var panel = new DetailPanel
            {
                Name = "Strike",
                Kind = "ATTACK",
                Body = "A plain swing at one enemy in reach.",
            };
            panel.Stats.Add(("COST", "FREE"));
            panel.Stats.Add(("POWER", actor == null ? "0" : CombatMath.Scale(CombatMath.ScaledAttack(actor, actor.WeaponScaling, 1f)).ToString()));
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

            public BuffBadge(string glyph, string tooltip, bool isPositive)
            {
                Glyph = glyph;
                Tooltip = tooltip;
                IsPositive = isPositive;
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
                string name = null;
                if (kit != null)
                {
                    foreach (var relic in kit.Relics)
                    {
                        if (relic.Effect == buff.Source) { name = relic.DisplayName; break; }
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
                default:
                    return new BuffBadge("?", status.Type.ToString(), true);
            }
        }

        private static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")} left";

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

            // A SUMMON HAS NO OBJECT. "will Roar itself" is grammatical and
            // says nothing; what the player needs is that the fight is about to
            // be one monster bigger. Its own sentence rather than a fourth
            // `who` case, because the shape of the sentence is what differs.
            if (intent.Kind == EnemyIntentKind.Summon)
            {
                return $"{enemyName} will {verb}, calling in another monster";
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
