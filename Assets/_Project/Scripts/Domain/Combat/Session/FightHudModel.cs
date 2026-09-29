using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

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

        // A FOURTH, same rule again (plan 1.10, milestone D): a rooted
        // character's charge is not unaffordable and not on cooldown, and
        // telling them either would send them to look at the wrong bar.
        public readonly bool Restricted;

        public bool Affordable => CanPay && MeetsRequirement && CooldownRemaining <= 0 && !Restricted;

        public SubmenuRow(string name, string meta, string cost, bool canPay, bool meetsRequirement,
                          int manaCost, int cooldownRemaining = 0, bool restricted = false)
        {
            Name = name;
            Meta = meta;
            Cost = cost;
            CanPay = canPay;
            MeetsRequirement = meetsRequirement;
            CooldownRemaining = cooldownRemaining;
            ManaCost = manaCost;
            Restricted = restricted;
        }
    }

    // One consumable stack, as the fight sees it. Core owns the inventory and
    // hands these over already resolved -- the satchel lives in RunState and has
    // no business being reachable from combat.
    //
    // CARRIES THE STACK'S WHOLE COPY (ItemInstance), not just its id. A bag can
    // hold two stacks of one potion -- an ordinary one and a caravan lot, which
    // may be fake -- and a press has to spend the stack that was pressed:
    // spending "a potion" by id would let a genuine one stand in for a fake or
    // the other way round (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.3). Nothing on
    // screen reads IsFake; only the session's use of it does.
    public readonly struct SatchelStack
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly int Count;
        public readonly bool RestoresMana;
        public readonly ItemInstance Instance;

        public SatchelStack(string itemId, string displayName, int count, bool restoresMana,
            ItemInstance instance = null)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Count = count;
            RestoresMana = restoresMana;
            Instance = instance ?? new ItemInstance(itemId);
        }

        public bool IsFake => Instance?.IsFake == true;

        public SatchelStack WithCount(int count) =>
            new SatchelStack(ItemId, DisplayName, count, RestoresMana, Instance);
    }

    // WHICH ICON A DetailIcon ROW PAINTS. Enum-keyed rather than a raw
    // sprite-path string (owner's ask, 2026-09-23 playtest) so the UI side
    // decides the ONE resource key per member in one place (FightController.
    // Hud.cs's DetailIconResourcePath) instead of a string minted fresh at
    // every call site that could misspell or drift from the art on disk.
    public enum DetailIconKind
    {
        Mana,
        Cost,
        Cooldown,
        Element,
        Defense,
        Reach,
        AreaOfEffect,
        Scaling,
    }

    // One icon cell on the detail card: WHICH icon (Kind, further narrowed
    // by IconKey for the members that cover more than one glyph -- Element
    // has eleven, Defense three, Reach two, Scaling six), and the short
    // text beside it where one exists ("11", "100", "STR"), "" otherwise.
    public readonly struct DetailIcon
    {
        public readonly DetailIconKind Kind;
        public readonly string IconKey;
        public readonly string Value;

        public DetailIcon(DetailIconKind kind, string iconKey, string value)
        {
            Kind = kind;
            IconKey = iconKey;
            Value = value;
        }
    }

    // What the detail column is describing.
    public sealed class DetailPanel
    {
        public string Name = "";
        public string Kind = "";
        public string Body = "";
        public readonly List<(string Key, string Value)> Stats = new List<(string, string)>();

        // THE CARD'S ACTUAL ROWS NOW (2026-09-23 icon rework, playtest ask
        // "replace the text rows with icons"). Stats/Body above are left
        // populated exactly as before -- FightMenuStateTests and every other
        // existing reader still gets the same strings -- this is additive:
        // FightScreen/FightController paint FROM Icons and stop painting the
        // Stats/Body nodes, rather than the model losing a field a different
        // reader still wants. Compact, same "omit rather than pad" rule
        // Stats already followed: a fact that does not apply for this
        // skill/spell is not in the list at all.
        public readonly List<DetailIcon> Icons = new List<DetailIcon>();

        // The skill's damage type ("Fire", "Arcane", ...), or "" for a
        // non-damaging skill -- see FightHudModel.DamageTypeLabel. A
        // DEDICATED field rather than a sixth Stats row: FightMenuStateTests
        // already pins Stats at exactly five entries by index (SCALES is
        // Stats[4]), so a new row would renumber every one of them for
        // whichever screen places this label. The UiKit side decides where
        // (and whether) to show it; this model only has to carry the string.
        public string DamageType = "";

        // THE ONE FACT THE ICON ROWS CANNOT CARRY, printed after Kind on the
        // card's kind line (FightHudModel.DetailKindLine). The icon card
        // renders only Name, the kind line and Icons -- Body and Stats are
        // no longer drawn -- so anything a player has to read that has no
        // icon lives here: a skill's or Strike's power (the Element icon is
        // the hero beside the title and shows no value) and an item's effect,
        // including the refusal "No effect on Fury" that tells the player a
        // potion would waste the turn. Blank means the kind line is Kind alone.
        public string Note = "";
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

            string resourceName = actor.SignaturePool?.DisplayName;

            foreach (var option in options)
            {
                // NO SECOND REQUIREMENT TEST HERE, which is what the header
                // above says and what this loop did anyway until 2026-09-11.
                // The copy it kept was worse than the LOCKED prefix that
                // header describes removing: it DROPPED the row. Row index is
                // the index into the option list for the submenu, the detail
                // card and the cast alike (FightSession's own comment: "row
                // position and skill index disagreeing ... casts a different
                // skill than the one that was pressed"), so a dropped row
                // desyncs all three. Byte-identical to the Where clause that
                // built the list, and fed the same actor, so it could never
                // fire -- which made it purely a claim that the list can
                // contain locked entries.
                var skill = option.Skill;

                // WHAT THE ROW SAYS IT WILL DO RIGHT NOW, for a poolTiers
                // skill -- "Slam" under the first tier, "Slam x2"/"Slam x4"
                // once the actor can afford one. Previewed off the actor's
                // CURRENT (unspent) primary pool, the same reading the press
                // itself will pick a beat later -- see PoolTierResolution.Pick.
                string rowName = skill.HasPoolTiers
                    ? PoolTierResolution.Label(skill.DisplayName, PoolTierResolution.Pick(actor.PrimaryPool, skill.PoolTiers))
                    : skill.DisplayName;

                rows.Add(new SubmenuRow(
                    rowName,
                    MetaLine(skill),

                    // THE COST COLUMN SAYS THE WAIT INSTEAD, while there is
                    // one. A row showing "8 MP" that cannot be pressed is
                    // telling the player about the only thing that is NOT
                    // stopping them.
                    // THE COST COLUMN SAYS "ROOTED" AHEAD OF EITHER (plan
                    // 1.10). A shackled row is not waiting and is not
                    // unaffordable; showing "8 MP" or "2 turns" on it names
                    // something that is not what is stopping the player, which
                    // is the exact complaint the cooldown arm below was added
                    // to answer.
                    option.Restricted
                        ? "Rooted"
                        : option.CooldownRemaining > 0
                            ? CooldownLabel(option.CooldownRemaining)
                            : CostLabel(skill, resourceName, PrimaryTagOf(actor)),
                    option.Affordable,
                    meetsRequirement: true,
                    skill.ManaCost,
                    cooldownRemaining: option.CooldownRemaining,
                    restricted: option.Restricted));
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
        //
        // TAKES THE DRINKER'S POOL for the same reason DetailForItem does, and
        // this is the reader that was missing. The hover panel already said "No
        // effect on Fury." over a row whose own meta read MANA and whose canPay
        // was true -- so the panel was honest and the thing a hand actually
        // presses was not, and the press went through: potion gone from the
        // save, turn spent, bar unmoved. One predicate, three readers.
        //
        // The pool is OPTIONAL and null reads as mana-shaped, matching
        // DetailForItem exactly: a caller with no actor in hand gets the rows
        // it has always got rather than a hedge.
        public static IReadOnlyList<SubmenuRow> ItemRows(
            IReadOnlyList<SatchelStack> satchel, ResourcePool primaryPool = null)
        {
            var rows = new List<SubmenuRow>();
            if (satchel == null) return rows;

            foreach (var stack in satchel)
            {
                bool refuses = ItemRefusedBy(stack.RestoresMana, primaryPool);

                rows.Add(new SubmenuRow(
                    stack.DisplayName,
                    refuses ? "CONSUMABLE  ·  NO EFFECT"
                        : stack.RestoresMana ? "CONSUMABLE  ·  MANA"
                        : "CONSUMABLE  ·  HEALTH",
                    "x" + stack.Count,

                    // The COUNT is still shown, and still true. What "cannot
                    // pay" means here is "this would do nothing", not "you have
                    // none" -- a greyed row over an x3 is the honest reading of
                    // a satchel holding three potions this character cannot use.
                    canPay: stack.Count > 0 && !refuses,
                    meetsRequirement: true,
                    manaCost: 0));
            }

            return rows;
        }

        // WOULD THIS ITEM DO NOTHING FOR THIS POOL. The one predicate behind
        // the row, the hover panel and the press (FightSession.UseConsumable),
        // so the three cannot answer differently -- which is exactly what they
        // did while the row had no pool to ask about.
        //
        // A null pool reads as mana-shaped: see ItemRows' own header.
        public static bool ItemRefusedBy(bool restoresMana, ResourcePool primaryPool) =>
            restoresMana && primaryPool != null && !primaryPool.RestoredByManaEffects;

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

        // THE COST COLUMN NAMES WHAT IS STOPPING THE MOVE: a rooted actor or
        // a rooted occupant is not "no room" -- there is a seat, and the root
        // is what holds (the same reason the skill rows say "Rooted" ahead of
        // a cost). Public so a test can read the caption without a row.
        public static string MoveCostCaption(FightSession session, CombatantState actor, MoveDirection direction)
        {
            var outcome = session == null ? PlaceOutcome.NotOnTheField : session.MoveOutcome(actor, direction);
            switch (outcome)
            {
                case PlaceOutcome.Placed: return UiStrings.MoveCostEndsTurn.Template;
                case PlaceOutcome.MemberRooted:
                case PlaceOutcome.OccupantRooted: return UiStrings.MoveCostRooted.Template;
                default: return UiStrings.MoveCostNoRoom.Template;
            }
        }

        private static SubmenuRow MoveRow(FightSession session, CombatantState actor,
            MoveDirection direction, string label, string meta)
        {
            bool legal = session != null && session.CanMove(actor, direction);

            // ENDS THE TURN is the cost, and the row says so -- Move is the
            // only command in the menu that spends a whole turn for no number
            // anywhere, and a blank cost column would read as "free".
            return new SubmenuRow(label, meta, MoveCostCaption(session, actor, direction),
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
                : skill.Targeting == SkillTargeting.SingleAlly ? "ALLY"
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
                case SkillEffect.HealParty:
                // Mend reads HEAL like the other two. The row already says
                // WHO through its targeting (the player is sent to the party
                // plates for a pick), so a third verb would be the same word
                // twice.
                case SkillEffect.HealSingle: return "HEAL";
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
                // Ashen Reckoning: no packet of its own, but it IS damage --
                // the same word DamageSingle/DamageAll already read as, off a
                // total the cast computes rather than authors.
                case SkillEffect.Reclaim: return "DAMAGE";
                // The two milestone C spells that move something other than a
                // health bar. HASTEN is the turn order; SWAP is the field
                // line. Deliberately two words rather than one shared
                // "MOVE" -- the verb row already has a MOVE and it means the
                // other one of these two, which is precisely the confusion
                // one shared word would create.
                case SkillEffect.Hasten: return "HASTEN";
                case SkillEffect.SwapAllies: return "SWAP";
                // Not "ROOT", although Velvet Shackles is the only user today:
                // the verb names the RESOLUTION SHAPE, and the next two
                // Afflicts (Censer of Embers, Thorn Tithe) land Burn and
                // Thorned. A word taken from this row's status would be a lie
                // on the next row that shares the arm.
                case SkillEffect.Afflict: return "AFFLICT";
                case SkillEffect.Enthrall: return "ENTHRALL";
                // Dark Chains and any later pull or knockback: the target is
                // put in a seat. Not "MOVE" or "SWAP" for the reason Hasten's
                // own line gives.
                case SkillEffect.Reposition: return "PULL";
                // Unbroken and Cursed Blood each open a window on the caster.
                // Named for what the player gets, not for the seam under it.
                case SkillEffect.Unbroken: return "ENDURE";
                case SkillEffect.CursedBlood: return "CURSE";
                case SkillEffect.PlantShield: return "PLANT";
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
            turns == FightSession.SpentForFight ? "USED"
            : turns == 1 ? "1 TURN" : $"{turns} TURNS";

        // BOTH TAGS COME FROM THE CASTER, not from this file. "MP" used to be
        // a literal here, which was true only for as long as every caster
        // spent mana: `manaCost` means "spent from the primary pool", so the
        // number's unit is that pool's own shortTag. Prints "6 MP" for
        // everyone shipped today, and does so by reading the row rather than
        // by agreeing with it.
        //
        // STILL COMBINED, for the SUBMENU ROW alone -- a row is one line of
        // text ("6 MP + 3 WOOL"), and splitting it the way the detail card's
        // rework below did would cost the row a second line it has no room
        // for. The detail card builds its own MANA and COST rows from
        // ManaCostLabel/ResourceOrHealthCostLabel instead of this.
        public static string CostLabel(ResolvedSkill skill, string resourceName, string primaryTag)
        {
            string resource = string.IsNullOrWhiteSpace(resourceName) ? "" : " " + resourceName.ToUpperInvariant();
            string primary = string.IsNullOrWhiteSpace(primaryTag) ? "" : " " + primaryTag.ToUpperInvariant();

            if (skill.ResourceCost > 0 && skill.ManaCost > 0)
            {
                return $"{skill.ManaCost}{primary} + {skill.ResourceCost}{resource}";
            }

            if (skill.ResourceCost > 0) return $"{skill.ResourceCost}{resource}";
            return skill.ManaCost > 0 ? skill.ManaCost + primary : "FREE";
        }

        // MANA ALONE, for the detail card's own MANA row (2026-09-22 rework)
        // -- resource and health payments moved to their own
        // ResourceOrHealthCostLabel row below, because "5 MP + 3 WOOL"
        // sharing one value string is exactly
        // the "two facts, one line" packing the owner's rework asked to stop
        // doing. "" (never "FREE") when the skill spends no mana at all --
        // FREE was true when this was the ONLY cost row a skill could ever
        // show; now an empty MANA row simply does not apply, and the row
        // is hidden rather than printed, same as every other inapplicable
        // stat on this card.
        public static string ManaCostLabel(ResolvedSkill skill, string primaryTag)
        {
            if (skill.ManaCost <= 0) return "";

            string primary = string.IsNullOrWhiteSpace(primaryTag) ? "" : " " + primaryTag.ToUpperInvariant();
            return skill.ManaCost + primary;
        }

        // The OTHER payment a skill can ask for: its signature resource, or
        // (Blackglass Spear's shape) a slice of the caster's own health.
        // MUTUALLY EXCLUSIVE in every skill authored today -- nothing both
        // spends a resource and pays in blood -- so one row states whichever
        // applies rather than guessing which to prefer if a future skill
        // ever authored both.
        //
        // HealthCost.AmountFor is the SAME ceiling-rounded HP figure the
        // actual payment charges (HealthCost.Pay), not healthCostPercent
        // restated raw -- a player comparing "11 HP" here against the
        // number their health bar drops by after casting must see the same
        // number, not a percentage they would have to do the arithmetic on
        // themselves.
        public static string ResourceOrHealthCostLabel(ResolvedSkill skill, CombatantState actor, string resourceName)
        {
            if (skill.ResourceCost > 0)
            {
                string resource = string.IsNullOrWhiteSpace(resourceName) ? "" : " " + resourceName.ToUpperInvariant();
                return skill.ResourceCost + resource;
            }

            if (skill.HealthCostPercent > 0)
            {
                int amount = HealthCost.AmountFor(actor?.MaxHealth ?? 0, skill.HealthCostPercent);
                return amount > 0 ? amount + " HP" : "";
            }

            return "";
        }

        // The COOLDOWN row: the authored length, ALWAYS, for any skill that
        // has one -- not only while it happens to be waiting. A card that
        // only ever mentioned a cooldown once the skill was already
        // unusable told the player nothing about a skill they had not cast
        // yet; this is the fact a player needs BEFORE they commit to a
        // resource-hungry skill with a long wait behind it, not just after.
        //
        // REMAINING JOINS IT, not replaces it, while the skill is actually
        // cooling -- the two facts (how long it waits, how much longer THIS
        // time) are both true at once and the old card could only ever say
        // one, which is what "instead of its cost" in the test this
        // replaces used to mean: the wait and the mana cost fought over the
        // same one line. They have their own rows now, so neither has to
        // give way to the other.
        // A once-per-fight skill has no turns to count: it says so, and says
        // "used" once it has been cast.
        private static string CooldownRowFor(CombatantState actor, ResolvedSkill skill, int remaining)
        {
            if (!skill.OncePerFight) return CooldownRowLabel(FightSession.CooldownTurnsFor(actor, skill), remaining);

            return remaining == FightSession.SpentForFight ? "USED" : "ONCE PER FIGHT";
        }

        public static string CooldownRowLabel(int cooldownTurns, int remaining)
        {
            if (cooldownTurns <= 0) return "";

            string authored = CooldownLabel(cooldownTurns);
            return remaining > 0 ? $"{authored} ({CooldownLabel(remaining)} LEFT)" : authored;
        }

        // THE COOLDOWN ICON'S OWN VALUE, compact rather than CooldownRowLabel's
        // "2 TURNS (1 TURN LEFT)" -- an icon row's value sits beside a small
        // badge, not a whole sentence, the same reason ScalingLabel prints
        // "STR" rather than a full grade breakdown (that method's own
        // header). Same two inputs, same two facts CooldownRowLabel states
        // (the authored length, and how much longer THIS time), just typeset
        // for the smaller space: "3T" while ready, "2/3" while cooling --
        // remaining first, since that is the number a player mid-cooldown
        // actually needs.
        public static string CooldownIconValue(int cooldownTurns, int remaining)
        {
            if (cooldownTurns <= 0) return "";
            return remaining > 0 ? $"{remaining}/{cooldownTurns}" : $"{cooldownTurns}T";
        }

        // What the cost column prints beside a primary-pool number. Falls
        // back to nothing rather than to "MP" when there is no actor: a card
        // built with no caster in hand cannot know the unit, and a guessed
        // unit is worse than a bare number.
        private static string PrimaryTagOf(CombatantState actor) => actor?.PrimaryPool?.ShortTag ?? "";

        // What the POWER stat shows: pre-mitigation damage from the caster's
        // OWN stats/relics/buffs, before the target's armor/resistance/
        // reduction touches it. Was `skill.Power` printed verbatim, which
        // reads as 0 for any skill whose damage comes from `flatAmount`
        // instead of the per-resource-point formula -- mud_burst among them,
        // shown blank despite dealing real damage. FightSession.PreviewSkillPower
        // is the actual pre-mitigation figure for every skill shape (fixed
        // packets, flat, or resource-scaled) with no target and no RNG spent.
        // NOTHING for the effects SkillResolution.Amount has no number for at
        // all (Provoke, Transform, Summon, the Gifts, SwapAllies, Reposition,
        // ...) -- their preview is a real 0, but "0 POWER" on the card reads
        // as "this does nothing" for an ability that redirects aggro, moves
        // an ally, or adds a monster to the field. Which effects have a
        // number is SkillEffects.HasMagnitude, an allow-list: this used to be
        // a private deny-list here, and every effect appended after it
        // (Palace Passage's SwapAllies among them) printed "0 POWER" until
        // someone noticed. Ward and Shatter DO have a number (talent-authored;
        // see SkillResolution.Amount) and print it like any other skill.
        //
        // "" (never "0" or "-"): the rework's rule ("a stat that does not
        // apply is omitted, never shown as - or 0") means the whole POWER row
        // is absent, so this returns "" and lets DetailForSkill leave it out.
        public static string PowerLabel(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            if (!SkillEffects.HasMagnitude(skill.Effect)) return "";
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

            // COMPACT, IN THIS ORDER -- MANA, COST, COOLDOWN, POWER, DEFENSE
            // ONLY WHEN THEY APPLY, then TARGET/EFFECT/SCALES always. A stat
            // that does not apply is not added at all (2026-09-22 rework
            // #2), rather than added with an empty value at a fixed slot --
            // a skill that skips four of the five conditional rows gets a
            // compact five-row card, not a five-row gap in an eight-row one.
            // FightController.Hud.cs's RefreshDetail paints panel.Stats[i]
            // into physical row i for i in 0..Count-1 and hides the rest, so
            // WHICH physical row ends up labelled POWER now varies with what
            // else is present -- RefreshDetailDamageType finds that row by
            // key, at runtime, rather than assuming a fixed index.
            int cooldownRemaining = session?.CooldownRemaining(actor, skill.Id) ?? 0;
            string primaryTag = PrimaryTagOf(actor);

            AddIfApplicable(panel.Stats, "MANA", ManaCostLabel(skill, primaryTag));
            AddIfApplicable(panel.Stats, "COST", ResourceOrHealthCostLabel(skill, actor, resourceName));
            AddIfApplicable(panel.Stats, "COOLDOWN", CooldownRowFor(actor, skill, cooldownRemaining));
            AddIfApplicable(panel.Stats, "POWER", PowerLabel(session, actor, skill));
            AddIfApplicable(panel.Stats, "DEFENSE", DefenseLabel(session, actor, skill));

            // Both of these printed the enum: "SINGLEENEMY" and "DAMAGESINGLE".
            // Same fix as the row's meta line, and it has to be the same words
            // or the panel and the row it describes disagree in front of the
            // player. ALWAYS ADDED -- TARGET/EFFECT/SCALES are not part of
            // the owner's "omit if inapplicable" rule (that rule names power,
            // mana, health/resource cost, cooldown and the defense
            // interaction specifically); SCALES keeps its own pre-existing
            // "-" convention for a skill with no scaling formula.
            panel.Stats.Add(("TARGET", skill.Effect == SkillEffect.Enthrall
                ? "ALL ENEMIES; BOSSES DELAYED"
                : ReachFor(skill.Targeting)));
            panel.Stats.Add(("EFFECT", VerbFor(skill.Effect)));
            panel.Stats.Add(("SCALES", ScalingLabelForSkill(session, actor, skill)));
            panel.DamageType = DamageTypeLabel(session, actor, skill);
            FillDetailIcons(panel, session, actor, skill, resourceName, cooldownRemaining);
            panel.Note = SkillNote(PowerLabel(session, actor, skill), skill.FreeAction);
            return panel;
        }

        // What the kind line says after "SKILL": "45 POWER" for a skill with a
        // number; for one without, "FREE ACTION" when that is what it is --
        // the one fact about a utility skill that changes how a turn is
        // planned, and which the card otherwise only states in prose -- and
        // nothing at all otherwise. Never both: the kind line is audited
        // against DetailKindSkill's sample width, and a damaging free action
        // (none is authored) would say so in its description.
        public static string SkillNote(string power, bool freeAction)
        {
            if (!string.IsNullOrEmpty(power)) return $"{power} POWER";
            return freeAction ? "FREE ACTION" : "";
        }

        // What the card's kind line prints: Kind, then the panel's Note when
        // it has one. See DetailPanel.Note for why the note exists at all.
        public static string DetailKindLine(DetailPanel panel)
        {
            if (panel == null) return "";
            return string.IsNullOrEmpty(panel.Note) ? panel.Kind : $"{panel.Kind}  \u00B7  {panel.Note}";
        }

        // THE ICON ROWS (2026-09-23 icon rework, coordinator pass 2). Every
        // fact the old compact Stats row pool carried now has an icon: MANA,
        // COST (resource/health -- dropping this pass 1 was a real
        // regression, the coordinator's own call), COOLDOWN (same), Element
        // (carrying POWER's value), Defense, Reach, AreaOfEffect, Scaling.
        //
        // ONE ICON, COMPACT -- the same "omit rather than pad" rule Stats
        // already followed above: a skill with no scaling axis (a fixed-
        // damage spell) adds no Scaling row at all rather than a "-" icon.
        private static void FillDetailIcons(DetailPanel panel, FightSession session, CombatantState actor,
            ResolvedSkill skill, string resourceName, int cooldownRemaining)
        {
            string mana = ManaCostLabel(skill, PrimaryTagOf(actor));
            if (!string.IsNullOrEmpty(mana))
            {
                panel.Icons.Add(new DetailIcon(DetailIconKind.Mana, "mana", mana));
            }

            string cost = ResourceOrHealthCostLabel(skill, actor, resourceName);
            if (!string.IsNullOrEmpty(cost))
            {
                panel.Icons.Add(new DetailIcon(DetailIconKind.Cost, "cost", cost));
            }

            string cooldown = skill.OncePerFight
                ? (cooldownRemaining == FightSession.SpentForFight ? "USED" : "1x")
                : CooldownIconValue(FightSession.CooldownTurnsFor(actor, skill), cooldownRemaining);
            if (!string.IsNullOrEmpty(cooldown))
            {
                panel.Icons.Add(new DetailIcon(DetailIconKind.Cooldown, "cooldown", cooldown));
            }

            var types = ResolvedDamageTypesFor(session, actor, skill);
            string power = PowerLabel(session, actor, skill);
            if (types.Count > 0 && !string.IsNullOrEmpty(power))
            {
                // THE FIRST TYPE, not every one -- a multi-packet spell
                // (frost_flare: Fire and Ice) still shows one element icon,
                // the same simplification DamageTypeLabel's own "/"-joined
                // text made bearable to read; the full list stays reachable
                // through panel.DamageType for a reader that wants it.
                panel.Icons.Add(new DetailIcon(DetailIconKind.Element, ElementIconKey(types[0]), power));
            }

            if (skill.IsDamaging)
            {
                string defenseKey = skill.IgnoresDefense ? "ignored"
                    : types.Count == 0 ? null
                    : types.Any(CombatMath.IsPhysical) && types.Any(t => !CombatMath.IsPhysical(t)) ? "mixed"
                    : types.Any(CombatMath.IsPhysical) ? "physical" : "magical";

                // "mixed" has no single glyph (a skill mixing physical and
                // magical packets in one cast is rare enough this rework
                // does not author a third shield for it) -- the row is
                // omitted rather than shown against the wrong icon, same
                // "omit what cannot be shown honestly" rule as everywhere
                // else on this card.
                if (defenseKey != null && defenseKey != "mixed")
                {
                    panel.Icons.Add(new DetailIcon(DetailIconKind.Defense, defenseKey, ""));
                }
            }

            if (skill.Targeting != SkillTargeting.Self)
            {
                string reachKey = ReachIconKey(skill.Reach);
                if (reachKey != null) panel.Icons.Add(new DetailIcon(DetailIconKind.Reach, reachKey, ""));

                string aoeKey = skill.Targeting == SkillTargeting.SingleEnemy
                    || skill.Targeting == SkillTargeting.SingleAlly
                    ? "single" : "aoe";
                panel.Icons.Add(new DetailIcon(DetailIconKind.AreaOfEffect, aoeKey, ""));
            }

            string scaling = ScalingLabelForSkill(session, actor, skill);
            if (scaling != "-")
            {
                panel.Icons.Add(new DetailIcon(DetailIconKind.Scaling, scaling.ToLowerInvariant(), scaling));
            }
        }

        // "any" when every rank is reachable, "front" when only the front
        // rank is, and NO ICON otherwise. Only those two pictures exist, and
        // an authored back-ranks reach (reachSlots [2,3]) used to fall
        // through to "front" -- the opposite of what the skill does. Omitted
        // is the card's own rule for a fact it cannot state honestly.
        public static string ReachIconKey(Reach reach)
        {
            if (reach.Kind == ReachKind.Any) return "any";

            int all = (1 << Reach.MaxRanks) - 1;
            if ((reach.RankMask & all) == all) return "any";
            if ((reach.RankMask & all) == 1) return "front";
            return null;
        }

        // The Element icon's key: exactly DamageType's own member name,
        // lowercased -- the one spelling both this method and the generated
        // Icons/Ability/element_*.png files (tools/make_detail_card_icons.py)
        // agree on, so a twelfth DamageType only ever needs new art, never a
        // new mapping line here.
        private static string ElementIconKey(DamageType type) => type.ToString().ToLowerInvariant();

        // Adds (key, value) only when value is non-empty -- the single seam
        // every compacted stat (MANA, COST, COOLDOWN, POWER, DEFENSE) goes
        // through, so "omit rather than print a dash or a zero" is stated
        // once rather than re-decided at each call site.
        private static void AddIfApplicable(List<(string Key, string Value)> stats, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) stats.Add((key, value));
        }

        // The damage type(s) this cast actually carries, resolved the ONE
        // way every reader of "what element is this" needs -- DamageTypeLabel
        // (the POWER row's own element tag) and DefenseLabel (which broad
        // defense answers it) used to each re-derive this from the skill by
        // hand and could disagree about it by construction. Empty for a
        // non-damaging skill, and for a non-fixed damaging skill with no
        // actor/session in hand to read the caster's attackType from.
        private static IReadOnlyList<DamageType> ResolvedDamageTypesFor(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            if (!skill.IsDamaging) return System.Array.Empty<DamageType>();

            // A CHOICE NOT YET MADE LISTS THE CHOICES, same as DamageTypeLabel
            // always has -- see that method's own header, kept here verbatim
            // now that both readers share this resolve.
            if (skill.HasElementChoice)
            {
                return skill.Elements.Where(e => e != null).Select(e => e.Type).ToArray();
            }

            if (skill.HasFixedDamage)
            {
                return skill.DamageInstances.Select(i => i.type).Distinct().ToArray();
            }

            // A skill that types its own damage answers without a caster.
            if (skill.OwnDamageType.HasValue) return new[] { skill.OwnDamageType.Value };

            if (session == null || actor == null) return System.Array.Empty<DamageType>();
            return new[] { session.CastTypeOf(actor, skill) };
        }

        // The skill's damage type, as a display string: "Fire", "Arcane", a
        // "/"-joined list for a multi-packet spell that authors more than
        // one (frost_flare: Fire and Ice), or "" for a skill that deals no
        // typed damage at all.
        public static string DamageTypeLabel(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            var types = ResolvedDamageTypesFor(session, actor, skill);
            return types.Count == 0 ? "" : string.Join("/", types);
        }

        // The DEFENSE row: which of the target's two broad stats actually
        // answers this cast (CombatMath.IsPhysical draws the same Physical-
        // or-everything-else line DamageTypeLabel's own header already
        // describes), and the one rider that matters more than either --
        // IgnoresDefense means neither one touches it at all, which is a
        // different fact than "which one", not a third value squeezed onto
        // the same line as one of the other two.
        //
        // ArmorPenetration/talent-driven shred (the "ARM" badge FightHudModel's
        // StatusRowsFor already paints, see its own header) is NOT read
        // here -- that is a property of the ATTACKER across every swing they
        // throw, permanent for the fight, and belongs on the character's own
        // status row rather than restated on every one of their skills'
        // cards. IgnoresDefense is the only defense-interaction rider a
        // SKILL itself authors (ResolvedSkill.IgnoresDefense), so it is the
        // only one this row states.
        private static string DefenseLabel(FightSession session, CombatantState actor, ResolvedSkill skill)
        {
            if (!skill.IsDamaging) return "";
            if (skill.IgnoresDefense) return "IGNORES DEFENSE";

            var types = ResolvedDamageTypesFor(session, actor, skill);
            if (types.Count == 0) return "";

            bool anyPhysical = types.Any(CombatMath.IsPhysical);
            bool anyMagical = types.Any(t => !CombatMath.IsPhysical(t));
            if (anyPhysical && anyMagical) return "PHYSICAL/MAGICAL";
            return anyPhysical ? "PHYSICAL" : "MAGICAL";
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

            // Sized off the target's max health, so no ability score rides it.
            if (skill.HasDamageBySeat) return "-";

            var castType = session.CastTypeOf(actor, skill);
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
                // The SIBLING CASE, and it has to be worded as distinctly as
                // the enemy one: a card reading "ONE ENEMY" over a skill that
                // lights up the party plates is the detail column and the
                // picker disagreeing in front of the player.
                case SkillTargeting.SingleAlly: return "ONE ALLY";
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

            // SAME CANONICAL ORDER DetailForSkill fills, compacted the same
            // way -- a plain swing costs nothing and waits on nothing, so
            // MANA/COST/COOLDOWN are simply not added (never "FREE"; see
            // AddIfApplicable's own header for why omission is the
            // convention now). POWER and DEFENSE always apply to a Strike.
            AddIfApplicable(panel.Stats, "POWER",
                actor == null ? "" : CombatMath.ComputeAttackDamage(actor, null).ToString());
            panel.Stats.Add(("DEFENSE", "PHYSICAL"));
            panel.Stats.Add(("TARGET", "SINGLE"));
            panel.Stats.Add(("EFFECT", "DAMAGE"));
            panel.Stats.Add(("SCALES", actor == null ? "-" : ScalingLabel(actor.WeaponScaling)));

            // THE SAME FACTS AS ICONS -- the card draws nothing else, and a
            // Strike with an empty Icons list painted as a bare title. No
            // Element icon: the swing's damage type is the actor's weapon's,
            // which this signature has no session to ask, so its power rides
            // the kind line instead of a hero icon that might name the wrong
            // element.
            panel.Icons.Add(new DetailIcon(DetailIconKind.Defense, "physical", ""));
            panel.Icons.Add(new DetailIcon(DetailIconKind.AreaOfEffect, "single", ""));
            string scaling = actor == null ? "-" : ScalingLabel(actor.WeaponScaling);
            if (scaling != "-")
            {
                panel.Icons.Add(new DetailIcon(DetailIconKind.Scaling, scaling.ToLowerInvariant(), scaling));
            }

            panel.Note = SkillNote(actor == null ? "" : CombatMath.ComputeAttackDamage(actor, null).ToString(), freeAction: false);
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

            // WHAT THE BADGE PRINTS when the number is not a turn count: a
            // stack count reads "x3", never a bare 3 beside the "3 turns left"
            // every other badge means (StatusHud.RallyRow). Null means print
            // Counter as the turn count it is.
            public readonly string CounterText;

            public StatusRow(string code, string slug, string tooltip, bool isPositive, int counter, int sortKey,
                             string counterText = null)
            {
                Code = code;
                Slug = slug;
                Tooltip = tooltip;
                IsPositive = isPositive;
                Counter = counter;
                SortKey = sortKey;
                CounterText = counterText;
            }

            // The digits the badge draws, when it draws any.
            public string CounterLabel => CounterText ?? Counter.ToString();
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
        // ---- the knell's floor figures (PLAN_BELLWETHER_KIT M5) -----------------
        //
        // While a seat-sized hit (the Death Knell) is a committed intent, what
        // it would deal its target at EACH party seat, for the numbers drawn on
        // the floor where a figure in that seat stands: lethal at or above the
        // target's current health, survivable below it, SAFE at a 0 seat. The
        // table is IntentDetailFor's own DamageBySeat -- the live reading the
        // badge and tooltip use -- so it is re-read after every Move, Passage
        // or hit; IsTargetSeat marks where the target stands NOW.
        //
        // `readable` is the badge's own rule (the player's turn is the one on
        // screen, nothing is playing): false, or no such intent, is all hidden.
        public enum SeatFigureStyle { Hidden, Lethal, Survivable, Safe }

        public readonly struct SeatFigure
        {
            public readonly SeatFigureStyle Style;
            public readonly string Text;
            public readonly bool IsTargetSeat;

            public SeatFigure(SeatFigureStyle style, string text, bool isTargetSeat)
            {
                Style = style;
                Text = text;
                IsTargetSeat = isTargetSeat;
            }

            public bool Shown => Style != SeatFigureStyle.Hidden;
        }

        public const string SeatFigureSafeText = "SAFE";

        public static SeatFigure[] SeatFiguresFor(FightSession session, bool readable)
        {
            var figures = new SeatFigure[CombatEncounter.SeatsPerSide];
            if (session == null || !readable) return figures;

            foreach (var enemy in session.Encounter.LivingEnemies)
            {
                var detail = session.IntentDetailFor(enemy);
                if (!detail.HasValue) continue;

                var intent = detail.Value;
                var target = intent.Target;
                if (intent.DamageBySeat == null || target == null || !target.IsAlive || !target.IsPlayerSide) continue;

                for (int seat = 0; seat < figures.Length && seat < intent.DamageBySeat.Length; seat++)
                {
                    int amount = intent.DamageBySeat[seat];
                    var style = amount <= 0 ? SeatFigureStyle.Safe
                        : amount >= target.CurrentHealth ? SeatFigureStyle.Lethal
                        : SeatFigureStyle.Survivable;
                    figures[seat] = new SeatFigure(style,
                        amount <= 0 ? SeatFigureSafeText : amount.ToString(), seat == intent.TargetSeat);
                }

                return figures;
            }

            return figures;
        }

        public static List<StatusRow> StatusRowsFor(FightSession session, CombatantState actor)
        {
            var rows = new List<StatusRow>();
            if (actor == null) return rows;

            // ONE BADGE PER TYPE, not one per entry. Wards stacked first (the
            // shield model, StatusEffects' own WARDS header) and were
            // special-cased below; every other status joined them on
            // 2026-09-20, so the de-duplication is now the rule rather than the
            // exception. A combatant carrying three poisons draws one PSN pill
            // whose tooltip says what all three are doing -- three pills each
            // telling a third of the story is the failure this closes.
            var summarised = new HashSet<StatusEffectType>();
            foreach (var status in actor.Statuses)
            {
                // Wards keep their own summary: their badge answers "how much
                // is left", not "how much longer", and NeverExpires has no
                // analogue for anything else.
                if (status.Type == StatusEffectType.Shielded) continue;
                if (!summarised.Add(status.Type)) continue;

                rows.Add(StatusHud.RowFor(StatusEffects.SummariseStatus(actor, status.Type)));
            }

            var wards = StatusEffects.SummariseWards(actor);
            if (wards.Any)
            {
                rows.Add(StatusHud.WardRow(wards));
            }

            // A TRANSFORMATION IS A STATUS -- 2026-09-09, the HUD-column pass.
            // It used to be a 36px panel of its own (TransformStrip) fused
            // above the party plate, which meant it could only ever be shown
            // for whoever was acting: a transformed ally sitting in the roster
            // showed nothing at all. Read off the actor, needing no session,
            // so it lands in the same half of this method actor.Statuses does
            // and a no-session caller degrades identically.
            if (actor.Transformation != null)
            {
                rows.Add(StatusHud.TransformRow(actor.Transformation.DisplayName,
                    actor.Transformation.TurnsRemaining, actor.Transformation.IsPermanent,
                    actor.Transformation.PrimaryDrainPerTurn > 0 ? actor.PrimaryPool?.DisplayName : null));
            }

            // SILENCE AND DISARM are engine windows on the combatant, not
            // statuses (Suppression.cs); they read off the actor like the
            // transformation above, so a no-session caller shows them too.
            if (actor.Suppression.IsSilenced)
                rows.Add(StatusHud.SilenceRow(actor.Suppression.Silence.TurnsRemaining));
            if (actor.Suppression.IsDisarmed)
                rows.Add(StatusHud.DisarmRow(actor.Suppression.DisarmPercent, actor.Suppression.Disarm.TurnsRemaining));

            // THE RALLY (PLAN_BELLWETHER_KIT 1.7 / 3.9): a fight-long stack
            // count, so the badge reads "x3" rather than a turn count.
            if (session != null)
            {
                int rally = session.RallyStacksOf(actor);
                if (rally > 0) rows.Add(StatusHud.RallyRow(rally, session.RallyAttackPercentOf(actor)));
            }

            if (actor.PermanentPhysicalDefenseShred > 0)
            {
                rows.Add(new StatusRow(
                    "ARM", "armor_shred",
                    $"-{actor.PermanentPhysicalDefenseShred} Physical Defense for this fight.",
                    isPositive: false, counter: -1, sortKey: 45));
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

        // WHAT THE SAME TAGS NODE SHOWS WHEN THERE IS NO BRK TO SHOW --
        // playtest 2026-09-23: a shield was carried by the plate's own ward
        // segment (SetWardFill) with no number anywhere on the card saying
        // how much. The Tags node (see EnemyStatusLine's own header, "BRK
        // survives because it has no other surface") is the one slot this
        // row has spare -- BRK is a boss-only, narrow-window state, so
        // sharing it with ward rather than opening a second box is the same
        // "one surface per fact" call Phase 3 already made here.
        //
        // BRK WINS THE SLOT when both are true at once (an elite mid-break
        // with a shield up is the only way that happens) -- a broken boss is
        // the rarer and more urgent fact, and losing the ward NUMBER for
        // that one window is not losing the ward ITSELF: the segment on the
        // bar still shows it, only the "+N" beside it does not, until BRK
        // clears. Graceful degradation on a slot conflict, not silence.
        public static string EnemyPlateTagLine(CombatantState enemy, FightSession session, int wardPoints)
        {
            string brk = EnemyStatusLine(enemy, session);
            if (brk.Length > 0) return brk;

            return wardPoints > 0 ? ItemStatLines.Coloured(FightHudPalette.WardText, $"+{wardPoints}") : "";
        }

        // WHAT THIS ITEM WOULD DO FOR THE CHARACTER HOLDING IT, which for a
        // mana potion is now a question about their pool rather than a
        // constant.
        //
        // CombatMath.RestoreMana already refuses a pool whose row says
        // restoredByManaEffects is false and returns 0 (phase B), so an
        // affordance reading "Restores mana." over a bar the potion cannot
        // touch would be the card and the effect disagreeing -- the exact
        // split that predicate exists to close. One predicate, two readers.
        //
        // The pool is OPTIONAL and null reads as mana-shaped: the callers
        // with no actor in hand (a detail panel built before the turn
        // resolves) get the sentence they have always got rather than a
        // hedge.
        public static DetailPanel DetailForItem(SatchelStack stack, ResourcePool primaryPool = null)
        {
            bool refuses = ItemRefusedBy(stack.RestoresMana, primaryPool);

            string body;
            if (!stack.RestoresMana) body = "Restores health.";
            else if (refuses) body = $"No effect on {PoolName(primaryPool)}.";
            else body = "Restores mana.";

            var panel = new DetailPanel
            {
                Name = stack.DisplayName,
                Kind = "ITEM",
                Body = body,
            };

            // SAME CANONICAL ORDER, compacted -- an item spends a charge,
            // not mana, a resource or a cooldown wait, and previews no power
            // or defense interaction, so MANA/COOLDOWN/POWER/DEFENSE are not
            // added at all. SCALES never applied to an item even before this
            // rework (no row existed for it); it stays that way.
            panel.Stats.Add(("COST", "1"));
            panel.Stats.Add(("TARGET", "SELF"));

            // "-", not "MANA", when the pool refuses it. The EFFECT cell is
            // the same claim the body makes in one word, so leaving it
            // reading MANA would put the lie back one column over.
            panel.Stats.Add(("EFFECT", !stack.RestoresMana ? "HEAL" : refuses ? "-" : "MANA"));

            // The effect sentence on the kind line, in the line's own caps:
            // "ITEM  .  NO EFFECT ON FURY" is the warning that keeps a turn
            // from being spent on a potion that does nothing.
            panel.Note = body.TrimEnd('.').ToUpperInvariant();
            return panel;
        }

        private static string PoolName(ResourcePool pool) =>
            string.IsNullOrWhiteSpace(pool?.DisplayName) ? "this resource" : pool.DisplayName;

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

            // A SEAT-SIZED HIT is said seat by seat, because where the target
            // stands is the whole decision (PLAN_BELLWETHER_KIT 1.5): "Death
            // Knell on Shawn: about 610 at the front (lethal), 180 in the
            // middle, none at the rear. Step back." Derived from the intent's
            // live table, never authored.
            if (intent.DamageBySeat != null)
            {
                return KnellSentence(intent) + ThenLine(intent);
            }

            string line = $"{enemyName} will {verb} {who}";
            if (intent.ExpectedDamage <= 0) return line + ThenLine(intent);

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

            // An authored crit is guaranteed, so the telegraph says so: the
            // figure already includes the multiplier (EnemyIntent.WillCrit).
            string crit = intent.WillCrit ? " (critical)" : "";
            string lethal = intent.IsLethal ? " (lethal)" : "";
            return line + $"\nfor about {intent.ExpectedDamage} {what}{spread}{crit}{lethal}" + ThenLine(intent);
        }

        // "\nDeath Knell next" for a scheduled step with one to come.
        private static string ThenLine(EnemyIntent intent) =>
            string.IsNullOrEmpty(intent.Then) ? "" : $"\n{intent.Then} next";

        private static string KnellSentence(EnemyIntent intent)
        {
            var parts = new List<string>();
            int health = intent.Target != null ? intent.Target.CurrentHealth : 0;
            bool said = false;

            for (int seat = 0; seat < intent.DamageBySeat.Length; seat++)
            {
                int amount = intent.DamageBySeat[seat];
                string at = seat == PrincesPalace.Domain.Party.PartySeat.Front ? "at the front"
                    : seat == PrincesPalace.Domain.Party.PartySeat.Middle ? "in the middle"
                    : "at the rear";

                if (amount <= 0)
                {
                    parts.Add($"none {at}");
                    continue;
                }

                // "about" once, on the first figure: the whole row is a centre.
                string about = said ? "" : "about ";
                said = true;
                parts.Add($"{about}{amount} {at}{(amount >= health ? " (lethal)" : "")}");
            }

            string who = string.IsNullOrEmpty(intent.TargetName) ? "someone" : intent.TargetName;
            string sentence = $"{intent.Label} on {who}: {string.Join(", ", parts)}.";
            return intent.StepBackIsSafer ? sentence + " Step back." : sentence;
        }
    }
}
