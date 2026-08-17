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

        public bool Affordable => CanPay && MeetsRequirement;

        public SubmenuRow(string name, string meta, string cost, bool canPay, bool meetsRequirement,
                          int manaCost, bool isBasicSpell = false)
        {
            Name = name;
            Meta = meta;
            Cost = cost;
            CanPay = canPay;
            MeetsRequirement = meetsRequirement;
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
        // UNAFFORDABLE ROWS ARE INCLUDED, marked, never dropped -- the player
        // should learn what their character has rather than watch the list
        // change length as their mana moves.
        public static IReadOnlyList<SubmenuRow> SkillRows(FightSession session, CombatantState actor)
        {
            var rows = new List<SubmenuRow>();
            if (session == null || actor == null) return rows;

            string resourceName = actor.Signature?.DisplayName;

            foreach (var option in session.SkillOptionsFor(actor))
            {
                var skill = option.Skill;
                bool meets = actor.AbilityScores.Meets(RequirementCurve.Apply(skill.Requirements));

                rows.Add(new SubmenuRow(
                    skill.DisplayName,
                    meets ? MetaLine(skill) : LockedPrefix + MetaLine(skill),
                    CostLabel(skill, resourceName),
                    option.Affordable,
                    meets,
                    skill.ManaCost));
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

        // Marks a row the character is not yet ALLOWED to use, as opposed to one
        // they merely cannot pay for right now.
        //
        // Both dim, and before this nothing told them apart: five greyed skills
        // above a full mana bar read as a bug. Affordability is already carried
        // by the cost's own colour, so the word only has to cover the other one.
        public const string LockedPrefix = "LOCKED  ·  ";

        // Both halves ALWAYS carry their unit.
        //
        // The column read "3", "4", "6 MP + 3", "5 MP", "0 MP" down a single
        // list -- three different grammars, and a bare number that silently
        // meant a different resource than the one beside it. A skill costing
        // nothing says so in words rather than showing "0 MP", which reads as a
        // missing value.
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

        // What the POWER stat shows.
        //
        // `Power` (per-point-of-resource scaling) reads as 0 for a skill with
        // authored damage packets -- those replace the attack-scaled formula
        // with a fixed number entirely, so Power never applies and is never
        // authored on them either. The sum of the packets is the honest "how
        // hard does this hit" for those; every other skill keeps showing Power.
        public static string PowerLabel(ResolvedSkill skill)
        {
            if (!skill.HasFixedDamage) return skill.Power.ToString();

            int total = 0;
            foreach (var instance in skill.DamageInstances) total += instance.amount;
            return total.ToString();
        }

        public static DetailPanel DetailForSkill(ResolvedSkill skill, string resourceName = null)
        {
            var panel = new DetailPanel
            {
                Name = skill.DisplayName,
                Kind = "SKILL",
                Body = skill.Description ?? "",
            };
            panel.Stats.Add(("COST", CostLabel(skill, resourceName)));
            panel.Stats.Add(("POWER", PowerLabel(skill)));

            // Both of these printed the enum: "SINGLEENEMY" and "DAMAGESINGLE".
            // Same fix as the row's meta line, and it has to be the same words
            // or the panel and the row it describes disagree in front of the
            // player.
            panel.Stats.Add(("TARGET", ReachFor(skill.Targeting)));
            panel.Stats.Add(("EFFECT", VerbFor(skill.Effect)));
            return panel;
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
        public static DetailPanel DetailForStrike(CombatantState actor)
        {
            var panel = new DetailPanel
            {
                Name = "Strike",
                Kind = "ATTACK",
                Body = "A plain swing at one enemy in reach.",
            };
            panel.Stats.Add(("COST", "FREE"));
            panel.Stats.Add(("POWER", actor == null ? "0" : actor.Attack.ToString()));
            panel.Stats.Add(("TARGET", "SINGLE"));
            panel.Stats.Add(("EFFECT", "DAMAGE"));
            return panel;
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
            string who = string.IsNullOrEmpty(intent.TargetName) ? "someone" : intent.TargetName;

            string line = $"{enemyName} will {verb} {who}";
            return intent.ExpectedDamage > 0
                ? line + $"\nfor about {intent.ExpectedDamage} damage"
                : line;
        }
    }
}
