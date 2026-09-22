using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The command menu's five edges, and what the HUD reads off them.
    //
    // In v1 both lived on a MonoBehaviour, so the only way to check that BACK
    // out of a target actually lands on the right depth was to load the scene
    // and click. The state machine is small and the rules are all conditional,
    // which is the worst possible combination to leave untested.
    public class FightMenuStateTests
    {
        private static FightMenuState Menu() => new FightMenuState();

        // ---- the graph ---------------------------------------------------------

        [Test]
        public void AFreshMenuIsClosedAtTheRoot()
        {
            var menu = Menu();

            Assert.AreEqual(MenuDepth.Root, menu.Depth);
            Assert.AreEqual(MenuBranch.None, menu.Branch);
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(menu.SubmenuOpen);
            Assert.IsFalse(menu.DetailOpen);
            Assert.AreEqual(-1, menu.ActiveVerbIndex);
        }

        [Test]
        public void AttackSkipsTheSubmenuEntirely()
        {
            // It jumps straight to picking a mark: there is nothing to choose
            // between, so a column with one row in it would be a step that only
            // ever costs a click.
            var menu = Menu();

            menu.OpenAttack();

            Assert.AreEqual(MenuDepth.Target, menu.Depth);
            Assert.AreEqual(MenuBranch.Attack, menu.Branch);
            Assert.IsFalse(menu.SubmenuOpen, "ATTACK has no list");
            Assert.IsTrue(menu.DetailOpen, "but it still describes the swing it is about to take");
        }

        [Test]
        public void ASkillBranchOpensItsColumn()
        {
            var menu = Menu();

            menu.OpenBranch(MenuBranch.Skill);

            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
            Assert.IsTrue(menu.SubmenuOpen);
            Assert.AreEqual(1, menu.ActiveVerbIndex);
        }

        // WHAT REPLACED "the column stays up while targeting from it".
        //
        // That reasoned the player should still see what they picked. They do
        // -- the detail column has it in full and the target prompt names it
        // outright -- and the price of the list saying it a third time was the
        // list sitting over the middle of the battlefield, which is the half of
        // the screen the player has just been told to point at.
        [Test]
        public void TheColumnFoldsOnceSomethingIsPicked()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterTargeting();

            Assert.AreEqual(MenuDepth.Target, menu.Depth);
            Assert.IsFalse(menu.SubmenuOpen,
                "the list is still covering the stage the player is being asked to aim at");

            // AND THE DETAIL COLUMN DOES NOT. It is what carries the choice
            // through targeting now, so folding both would leave the prompt as
            // the only thing naming what is about to happen.
            Assert.IsTrue(menu.DetailOpen);
        }

        [Test]
        public void BackFromTargetingLandsOnTheListItCameFrom()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterTargeting();

            Assert.IsTrue(menu.Back());

            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
            Assert.AreEqual(MenuBranch.Skill, menu.Branch, "still in the skill branch");
        }

        [Test]
        public void BackFromAttacksTargetingGoesStraightToTheRoot()
        {
            // It skipped the submenu on the way in, so it skips it on the way
            // out. Landing on an empty list would be a state the design has no
            // drawing for.
            var menu = Menu();
            menu.OpenAttack();

            Assert.IsTrue(menu.Back());

            Assert.AreEqual(MenuDepth.Root, menu.Depth);
            Assert.AreEqual(MenuBranch.None, menu.Branch);
        }

        // ---- which rack is being pointed at (AUDIT #147) ----------------------

        [Test]
        public void TargetingPointsAtTheEnemiesUnlessItIsToldOtherwise()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterTargeting();

            Assert.AreEqual(TargetSide.Enemies, menu.Side);
            Assert.IsTrue(menu.IsPickingEnemy);
            Assert.IsFalse(menu.IsPickingAlly);
        }

        [Test]
        public void AnAllyPickEntersTheSameDepthOnThePartySide()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, 0);
            menu.EnterTargeting(TargetSide.Allies);

            Assert.AreEqual(MenuDepth.Target, menu.Depth, "the same depth, not a fifth one");
            Assert.AreEqual(TargetSide.Allies, menu.Side);
            Assert.IsTrue(menu.IsPickingAlly);
            Assert.IsFalse(menu.IsPickingEnemy, "exactly one rack is live at a time");
        }

        [Test]
        public void BackFromAnAllyPickLandsOnTheSkillListAndForgetsTheSide()
        {
            // "Back/cancel behaves identically to the enemy flow" is the
            // contract, so this is BackFromTargetingLandsOnTheListItCameFrom
            // with one more assertion: a Side left pointing at the party
            // would light the party plates for a menu that is no longer
            // asking anything.
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(2, 0);
            menu.EnterTargeting(TargetSide.Allies);

            Assert.IsTrue(menu.Back());

            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
            Assert.AreEqual(MenuBranch.Skill, menu.Branch, "still in the skill branch");
            Assert.AreEqual(2, menu.Selection, "and still on the row that was pressed");
            Assert.AreEqual(TargetSide.Enemies, menu.Side);
            Assert.IsFalse(menu.IsPickingAlly);
        }

        [Test]
        public void ResolvingAnAllyPickForgetsTheSideToo()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterTargeting(TargetSide.Allies);

            menu.Reset();

            Assert.AreEqual(TargetSide.Enemies, menu.Side);
            Assert.IsFalse(menu.IsPickingAlly);
        }

        [Test]
        public void AttackAlwaysReturnsTheSideToTheEnemies()
        {
            // The one path back into targeting that takes no side argument.
            // Opening ATTACK after an ally pick was cancelled must not
            // inherit the party rack.
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterTargeting(TargetSide.Allies);

            menu.OpenAttack();

            Assert.AreEqual(TargetSide.Enemies, menu.Side);
            Assert.IsTrue(menu.IsPickingEnemy);
        }

        [Test]
        public void BackFromAListClosesTheBranch()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Item);

            Assert.IsTrue(menu.Back());

            Assert.AreEqual(MenuDepth.Root, menu.Depth);
            Assert.AreEqual(MenuBranch.None, menu.Branch);
            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void BackAtTheRootIsNotConsumed()
        {
            // The caller uses the return value to decide whether ESC was theirs
            // to handle -- at the root it belongs to the pause menu instead.
            Assert.IsFalse(Menu().Back());
        }

        [Test]
        public void TheGraphHasNoWayToReachAListlessSubDepth()
        {
            // Two steps back from anywhere reaches the root and stops. A stack
            // would let it go further; three fields cannot.
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterTargeting();

            menu.Back();
            menu.Back();

            Assert.AreEqual(MenuDepth.Root, menu.Depth);
            Assert.IsFalse(menu.Back());
        }

        // ---- selection ----------------------------------------------------------

        [Test]
        public void SelectingARowPreviewsItsCost()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);

            Assert.IsTrue(menu.Select(2, manaCost: 7));

            Assert.AreEqual(2, menu.Selection);
        }

        [Test]
        public void ReselectingTheSameRowChangesNothing()
        {
            // Hovering across one row fires repeatedly; a redraw per mouse move
            // is what the return value exists to avoid.
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(2, 7);

            Assert.IsFalse(menu.Select(2, 7));
        }

        [Test]
        public void NothingIsSelectableOutsideAList()
        {
            var menu = Menu();
            menu.OpenAttack();

            Assert.IsFalse(menu.Select(0, 5), "ATTACK has no rows to hover");
            Assert.AreEqual(-1, menu.Selection);
        }

        [Test]
        public void OpeningABranchClearsWhateverWasSelectedBefore()
        {
            // Otherwise the detail column would open describing a row from the
            // PREVIOUS branch, which is the wrong answer that looks right.
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(3, 9);

            menu.OpenBranch(MenuBranch.Item);

            Assert.AreEqual(-1, menu.Selection);
        }

        [Test]
        public void ResolvingAnActionClosesEverything()
        {
            // Called on every resolution, which is what makes the branch
            // trustworthy as the sole authority for "is the column up".
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(1, 4);
            menu.EnterTargeting();

            menu.Reset();

            Assert.AreEqual(MenuDepth.Root, menu.Depth);
            Assert.AreEqual(MenuBranch.None, menu.Branch);
            Assert.AreEqual(-1, menu.Selection);
        }

        // ---- what the HUD reads -------------------------------------------------

        [Test]
        public void TheBreadcrumbNamesTheWayIn()
        {
            var menu = Menu();
            Assert.AreEqual("C O M M A N D", FightHudModel.Breadcrumb(menu));

            menu.OpenBranch(MenuBranch.Skill);
            StringAssert.Contains("S K I L L", FightHudModel.Breadcrumb(menu));
            StringAssert.DoesNotContain("T A R G E T", FightHudModel.Breadcrumb(menu));

            menu.EnterTargeting();
            StringAssert.Contains("T A R G E T", FightHudModel.Breadcrumb(menu));
        }

        [Test]
        public void ExactlyOneVerbIsLit()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Item);

            Assert.AreEqual(2, menu.ActiveVerbIndex);

            menu.Back();
            Assert.AreEqual(-1, menu.ActiveVerbIndex, "nothing is lit at the root");
        }

        // ---- the element depth -------------------------------------------------

        // ONLY A SKILL THAT ASKS STOPS HERE, and the CALLER is what decides
        // that -- FightMenuState holds no content and asks no content
        // question. These drive the transitions the controller drives on an
        // element skill's row.
        [Test]
        public void AnElementSkillStopsBetweenTheSkillListAndTheTarget()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(2, 0);
            menu.EnterElementChoice();

            Assert.AreEqual(MenuDepth.Element, menu.Depth);
            Assert.IsTrue(menu.SubmenuOpen, "the column stays up showing a different list");
            Assert.IsFalse(menu.IsTargeting);
            Assert.AreEqual(2, menu.Selection, "the skill row is still the selected skill row");
            Assert.IsNull(menu.ChosenElement);

            menu.ChooseElement(DamageType.Fire);

            Assert.AreEqual(MenuDepth.Target, menu.Depth);
            Assert.AreEqual(DamageType.Fire, menu.ChosenElement);
        }

        // The two indices are kept apart precisely so this holds: four call
        // sites in FightController read Selection to find the selected SKILL,
        // and hovering an element must not move any of them onto another one.
        [Test]
        public void HoveringAnElementRowLeavesTheSkillSelectionAlone()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(3, 0);
            menu.EnterElementChoice();

            Assert.IsTrue(menu.Select(1, 0));
            Assert.AreEqual(3, menu.Selection);
            Assert.AreEqual(1, menu.ElementSelection);
            Assert.AreEqual(1, menu.RowSelection, "the row pool is painting the element list");

            Assert.IsFalse(menu.Select(1, 0), "hovering the row already hovered changes nothing");
        }

        [Test]
        public void BackFromATargetLandsOnTheElementListAndUndoesTheChoice()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, 0);
            menu.EnterElementChoice();
            menu.ChooseElement(DamageType.Water);

            Assert.IsTrue(menu.Back());

            Assert.AreEqual(MenuDepth.Element, menu.Depth);
            Assert.IsNull(menu.ChosenElement,
                "standing on the element list still holding the element just undone would describe a " +
                "decision the player has taken back");
        }

        [Test]
        public void BackFromTheElementListLandsOnTheSkillListWithTheSkillStillLit()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(2, 0);
            menu.EnterElementChoice();
            menu.Select(1, 0);

            Assert.IsTrue(menu.Back());

            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
            Assert.AreEqual(MenuBranch.Skill, menu.Branch);
            Assert.AreEqual(2, menu.Selection);
            Assert.AreEqual(-1, menu.ElementSelection);
            Assert.IsNull(menu.ChosenElement);
        }

        // Every other command's path is what it always was: a plain skill at
        // Target depth backs out to its own list, not to an element list it
        // never visited.
        [Test]
        public void BackFromAPlainSkillsTargetStillLandsOnTheSkillList()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.Select(0, 0);
            menu.EnterTargeting();

            Assert.IsTrue(menu.Back());
            Assert.AreEqual(MenuDepth.Sub, menu.Depth);
        }

        [Test]
        public void ResetForgetsTheChosenElement()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterElementChoice();
            menu.ChooseElement(DamageType.Wind);

            menu.Reset();

            Assert.AreEqual(MenuDepth.Root, menu.Depth);
            Assert.IsNull(menu.ChosenElement);
            Assert.AreEqual(-1, menu.ElementSelection);
        }

        // Opening any branch clears it too -- an element left over from last
        // turn would be handed to a cast that never asked for one, which
        // FightSession refuses outright.
        [Test]
        public void OpeningAnyBranchForgetsTheChosenElement()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterElementChoice();
            menu.ChooseElement(DamageType.Wind);

            menu.OpenBranch(MenuBranch.Item);
            Assert.IsNull(menu.ChosenElement);

            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterElementChoice();
            menu.ChooseElement(DamageType.Wind);
            menu.OpenAttack();
            Assert.IsNull(menu.ChosenElement);
        }

        [Test]
        public void TheBreadcrumbNamesTheElementStepAtBothDepthsItIsTrueAt()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            menu.EnterElementChoice();

            StringAssert.Contains("E L E M E N T", FightHudModel.Breadcrumb(menu));
            StringAssert.DoesNotContain("T A R G E T", FightHudModel.Breadcrumb(menu));

            menu.ChooseElement(DamageType.Fire);

            StringAssert.Contains("E L E M E N T", FightHudModel.Breadcrumb(menu),
                "a breadcrumb that dropped the element on the way to targeting would be the one line " +
                "on screen still claiming the player has a choice left to make");
            StringAssert.Contains("T A R G E T", FightHudModel.Breadcrumb(menu));
        }

        // And nothing else grew a step.
        [Test]
        public void TheBreadcrumbIsUnchangedForASkillThatAsksNothing()
        {
            var menu = Menu();
            menu.OpenBranch(MenuBranch.Skill);
            StringAssert.DoesNotContain("E L E M E N T", FightHudModel.Breadcrumb(menu));

            menu.EnterTargeting();
            StringAssert.DoesNotContain("E L E M E N T", FightHudModel.Breadcrumb(menu));
            StringAssert.Contains("T A R G E T", FightHudModel.Breadcrumb(menu));
        }
    }

    // What the submenu and detail column actually say, derived from a session.
    public class FightHudModelTests
    {
        private static ResolvedSkill Skill(string id, string name, int manaCost = 5,
            SkillEffect effect = SkillEffect.DamageSingle, int power = 12,
            DamageInstance[] packets = null, AbilityScoreBlock requirements = default) =>
            new ResolvedSkill(id, name, "It does a thing.", "hero", 1, effect,
                SkillTargeting.SingleEnemy, manaCost, 0, false, power, 0, false,
                packets, SpellPresentation.None, 0, requirements: requirements);

        private static (FightSession session, CombatantState hero) Fight(params ResolvedSkill[] skills) =>
            FightWithTier(1f, skills);

        // A separate overload rather than an optional parameter after skills
        // -- params must be the last parameter, so a caster's own tier
        // multiplier (PlayerKit.SkillPowerMultiplier, still live for every
        // fixed-damage skill after BasicSpell's removal, docs/PLAN_SHOP.md
        // Gate 4) gets its own entry point instead.
        private static (FightSession session, CombatantState hero) FightWithTier(float skillPowerMultiplier, params ResolvedSkill[] skills)
        {
            var hero = new CombatantState("Hero", true, 200, 30, 20, 10);
            var foe = new CombatantState("Foe", false, 1000, 10, 5, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, null, skillPowerMultiplier: skillPowerMultiplier);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1));
            return (session, hero);
        }

        // BasicSpell (the free "Skill" row every character got after their
        // authored skills, regardless of what they had learned) was removed
        // with docs/PLAN_SHOP.md Gate 4 -- SkillRows no longer appends one,
        // so the two tests that pinned its row and its cost resolution went
        // with it.

        [Test]
        public void AnUnaffordableRowIsShownAndMarked_NeverDropped()
        {
            // The player should learn what their character has rather than watch
            // the list change length as their mana moves.
            var (session, hero) = Fight(Skill("cheap", "Cheap", manaCost: 1), Skill("dear", "Dear", manaCost: 999));
            hero.PrimaryPool.Current = 5;

            var rows = FightHudModel.SkillRows(session, hero);

            Assert.AreEqual(2, rows.Count, "both authored skills");
            Assert.IsTrue(rows[0].Affordable);
            Assert.IsFalse(rows[1].Affordable);
        }

        [Test]
        public void NoMoneyAndNoRequirementAreSeparateFlags()
        {
            // They render identically today. The distinction has to already
            // exist here for anything to ever say which it is.
            var (session, hero) = Fight(Skill("dear", "Dear", manaCost: 999));
            hero.PrimaryPool.Current = 0;

            var row = FightHudModel.SkillRows(session, hero)[0];

            Assert.IsFalse(row.CanPay);
            Assert.IsTrue(row.MeetsRequirement, "the skill has no ability-score requirement authored");
            Assert.IsFalse(row.Affordable);
        }

        [Test]
        public void TheMetaLineIsGeneratedFromTheSkillsOwnFields()
        {
            // So it cannot drift from what the skill actually does.
            //
            // The effect contributes the VERB only. It used to contribute the
            // enum name, which put "DAMAGEALL  ·  SINGLE" on screen: a word no
            // player uses, and -- on a single-target skill -- one that flatly
            // contradicted the reach printed next to it. Targeting is authored
            // separately and is the only thing entitled to say who is hit.
            var skill = Skill("x", "Firestorm", effect: SkillEffect.DamageAll);

            StringAssert.Contains("DAMAGE", FightHudModel.MetaLine(skill));
            StringAssert.DoesNotContain("DAMAGEALL", FightHudModel.MetaLine(skill),
                "the enum name is not a word, and it duplicates the reach beside it");
            StringAssert.Contains("SINGLE", FightHudModel.MetaLine(skill), "targeting is authored separately from the effect");
        }

        // WHAT REPLACED "a locked skill says so rather than just going grey".
        //
        // That was the right fix for the wrong problem. Two different dims did
        // look identical -- cannot afford it now, and not allowed to use it at
        // all -- and prefixing the second with LOCKED separated them. It also
        // kept them in the list, and the list is where the separation stopped
        // mattering: nine entries, two castable, seven greyed, and the seven the
        // player could do nothing about for several levels were most of what
        // they were looking at.
        //
        // A locked skill is not in the list now. The one dim left means exactly
        // one thing, which is what the LOCKED prefix was trying to buy.
        [Test]
        public void ASkillTheCharacterMayNotYetUseIsNotListedAtAll()
        {
            var (session, hero) = Fight(
                Skill("open", "Headbutt"),
                Skill("shut", "Firestorm",
                    requirements: new AbilityScoreBlock(99, 0, 0, 0, 0, 0)));

            var names = FightHudModel.SkillRows(session, hero).Select(r => r.Name).ToList();

            CollectionAssert.Contains(names, "Headbutt");
            CollectionAssert.DoesNotContain(names, "Firestorm",
                "a skill whose requirement the character cannot meet is greyed noise in a list " +
                "they are trying to scan - it appears when it becomes usable");
        }

        [Test]
        public void EveryCostCarriesItsUnit_AndFreeSaysSo()
        {
            // The column read "3", "4", "6 MP + 3", "0 MP" down one list: three
            // grammars, and a bare number silently meaning a different resource
            // than the one above it.
            var manaOnly = Skill("a", "Bolt", manaCost: 5);
            var free = Skill("b", "Shout", manaCost: 0);

            Assert.AreEqual("5 MP", FightHudModel.CostLabel(manaOnly, "Wool", "MP"));
            Assert.AreEqual("FREE", FightHudModel.CostLabel(free, "Wool", "MP"),
                "a skill that costs nothing says so - '0 MP' reads as a missing value");
        }

        // BOTH UNITS COME FROM THE CASTER. "MP" was a literal inside
        // CostLabel until mana became one pool among several; a caster whose
        // primary pool is not mana must not be told their skill costs MP.
        // Pinned as literals rather than rebuilt from the tag, so a change to
        // the format string is a failure here rather than a silent agreement.
        [Test]
        public void TheCostColumnPrintsThePrimaryPoolsOwnTag()
        {
            var manaOnly = Skill("a", "Bolt", manaCost: 6);
            var both = new ResolvedSkill("c", "Shear", "It does a thing.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 6, 3, false, 12, 0, false,
                null, SpellPresentation.None, 0);

            Assert.AreEqual("6 MP", FightHudModel.CostLabel(manaOnly, "Wool", "MP"),
                "every character shipped today spends mana, and the column must still read exactly this");
            Assert.AreEqual("6 FURY", FightHudModel.CostLabel(manaOnly, "Wool", "FURY"));
            Assert.AreEqual("6 FURY + 3 WOOL", FightHudModel.CostLabel(both, "Wool", "FURY"));

            // No caster in hand: a bare number rather than a guessed unit.
            Assert.AreEqual("6", FightHudModel.CostLabel(manaOnly, "", ""));
        }

        [Test]
        public void APacketSpellShowsTheSumOfItsPacketsScaledByTheCastersOwnTier()
        {
            // The packets are the SAME multiplier ResolveDamageInstances
            // itself applies -- this fixture's tier is 1.5x, so 30 Fire and
            // 20 Ice read 45 and 30, not the raw 30/20 authored in content.
            // Showing the raw sum would be exactly the kind of "close but not
            // the real number" the Strike row had before this fix.
            var packets = new[]
            {
                new DamageInstance(DamageType.Fire, 30),
                new DamageInstance(DamageType.Ice, 20),
            };
            var spell = Skill("bolt", "Prismatic Bolt", power: 0, packets: packets);
            var (session, hero) = FightWithTier(1.5f, spell);

            Assert.AreEqual("75", FightHudModel.PowerLabel(session, hero, spell));
        }

        [Test]
        public void AFlatAmountOnlySkillShowsRealPowerNotZero()
        {
            // mud_burst's own shape: no authored `power` (it spends only mana,
            // and power scales per point of a SIGNATURE resource mud_burst
            // doesn't spend, so SkillEntryResolver refuses power > 0 without
            // one), no damageInstances, just a flat amount. The old PowerLabel
            // read `skill.Power` verbatim for anything that wasn't a packet
            // spell -- 0 for this shape, shown as a blank row on a skill that
            // deals real damage. flatAmount alone has to produce a non-zero,
            // pre-mitigation POWER.
            var skill = new ResolvedSkill("mud_burst", "Mud Burst", "Flings mud.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 8,
                resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 12,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None, sortOrder: 0);
            var (session, hero) = Fight(skill);

            string power = FightHudModel.PowerLabel(session, hero, skill);

            Assert.AreNotEqual("0", power, "flatAmount alone must not read as no power");
            // Hero's Attack (20, scaled) + flatAmount (12), raw, with no
            // target so no defense subtracted: 20 + 12 = 32.
            Assert.AreEqual("32", power);
        }

        [Test]
        public void APreviewNeverConsumesTheRunsRandomStream()
        {
            // A card the player is merely LOOKING at must not roll anything --
            // the pre-mitigation preview skips DamagePipeline.AfterDefences
            // entirely (that is where variance/ward would read the RNG), so
            // calling it twice in a row for the same skill must be perfectly
            // stable.
            var packets = new[] { new DamageInstance(DamageType.Fire, 30) };
            var spell = Skill("bolt", "Prismatic Bolt", power: 0, packets: packets);
            var (session, hero) = Fight(spell);

            string first = FightHudModel.PowerLabel(session, hero, spell);
            string second = FightHudModel.PowerLabel(session, hero, spell);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void AnOrdinarySkillShowsPreMitigationDamageNotItsRawPowerField()
        {
            // POWER used to print `skill.Power` verbatim -- for a skill whose
            // damage comes from flatAmount instead (mud_burst's actual shape),
            // that field is 0 and the row reads blank despite the skill
            // dealing real damage. This pins the fix: the CASTER'S OWN
            // Attack, scaled and read raw (no DamageScale, fixed 2026-08-26 --
            // see SkillResolution.Damage's own header), with no target and
            // therefore no defense subtracted. Hero's Attack is 20 and this
            // skill spends no signature resource (no Signature is set on the
            // test hero), so `power: 12` never actually contributes -- which
            // is the point: the row now reads what the caster's own stats
            // produce, not the authored constant.
            var skill = Skill("a", "Alpha", power: 12);
            var (session, hero) = Fight(skill);

            Assert.AreEqual("20", FightHudModel.PowerLabel(session, hero, skill));
        }

        // The HasNoPreviewablePower list is meant to be COMPLETE: every
        // effect SkillResolution.Amount has no number for at all. Summon was
        // missing from it, so a summon's card would have promised "0 POWER"
        // -- which reads as "this does nothing" -- for an ability that makes
        // the fight one monster bigger. Unreachable today (the one authored
        // Summon belongs to an enemy), which is exactly why nothing caught
        // it. "" now, not "-" -- the 2026-09-22 detail-card rework changed
        // the convention for an inapplicable POWER row from a printed dash
        // to an omitted (empty, hidden) one; see PowerLabel's own header.
        [Test]
        public void ASummonHasNoPreviewableNumberEither()
        {
            var summon = Skill("roar", "Roar", effect: SkillEffect.Summon);
            var (session, hero) = Fight(summon);

            Assert.AreEqual("", FightHudModel.PowerLabel(session, hero, summon));
        }

        [Test]
        public void AnEmptySatchelProducesNoRows()
        {
            CollectionAssert.IsEmpty(FightHudModel.ItemRows(new List<SatchelStack>()));
            CollectionAssert.IsEmpty(FightHudModel.ItemRows(null));
        }

        [Test]
        public void AnItemRowCountsRatherThanCosts()
        {
            var rows = FightHudModel.ItemRows(new List<SatchelStack>
            {
                new SatchelStack("potion", "Potion", 3, restoresMana: false),
                new SatchelStack("ether", "Ether", 1, restoresMana: true),
            });

            Assert.AreEqual("x3", rows[0].Cost);
            Assert.AreEqual(0, rows[0].ManaCost, "an item never previews on the mana bar");
            StringAssert.Contains("HEALTH", rows[0].Meta);
            StringAssert.Contains("MANA", rows[1].Meta);
        }

        [Test]
        public void AttackGetsASyntheticDetailEntry()
        {
            // A verb that jumps straight to targeting would otherwise be the one
            // command with nothing to read about it.
            //
            // POWER is CombatMath.ComputeAttackDamage(actor, null), not the
            // raw Attack stat verbatim -- hero.Attack is 20, no grades
            // authored, so neutral WeaponScaling/the unarmed-STR fallback at
            // Strength 10 contributes 0, and the 1.2x basic-attack
            // coefficient is the only thing that moves it: round(20 * 1.0 *
            // 1.2) = 24 (no DamageScale multiplies it any more; fixed
            // 2026-08-26, see ComputeAttackDamage's own header), matching
            // exactly what a real Strike against this hero would deal
            // before mitigation.
            var (_, hero) = Fight();

            var panel = FightHudModel.DetailForStrike(hero);

            Assert.AreEqual("Strike", panel.Name);
            Assert.AreEqual("24", StatValue(panel, "POWER"),
                "it describes THIS actor's swing, not a generic one");
        }

        // Looks a stat up BY KEY, never by index -- panel.Stats is COMPACT
        // now (2026-09-22 rework #2: a stat that does not apply is not added
        // at all, so which physical row anything lands on moves with what
        // else is present). Fails loudly, naming every key actually present,
        // rather than throwing IndexOutOfRange or silently reading the wrong
        // row's value.
        private static string StatValue(DetailPanel panel, string key)
        {
            foreach (var stat in panel.Stats)
            {
                if (stat.Key == key) return stat.Value;
            }

            Assert.Fail($"no \"{key}\" stat on this card. Present: " +
                        string.Join(", ", panel.Stats.Select(s => s.Key)));
            return null;
        }

        private static bool HasStat(DetailPanel panel, string key) => panel.Stats.Any(s => s.Key == key);

        [Test]
        public void ASkillsDetailPanelCompactsAwayWhicheverStatsDontApply()
        {
            // manaCost 5 + a fixed physical packet gives two of the five
            // conditional rows something to say (MANA, DEFENSE) alongside
            // the three that always apply (TARGET, EFFECT, SCALES) -- COST
            // and COOLDOWN don't apply to this particular skill, and the
            // point is that they are simply absent, not present-and-empty
            // in the middle of the list.
            var skill = Skill("a", "Alpha", manaCost: 5, power: 12,
                packets: new[] { new DamageInstance(DamageType.Physical, 12) });
            var (session, hero) = Fight(skill);

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("Alpha", panel.Name);
            CollectionAssert.AreEqual(
                new[] { "MANA", "POWER", "DEFENSE", "TARGET", "EFFECT", "SCALES" },
                panel.Stats.Select(s => s.Key).ToArray(),
                "COST and COOLDOWN don't apply to this skill and must be compacted away, not left empty mid-list");
        }

        [Test]
        public void ADetailCardShowsBothItsCostAndItsCooldownWhileWaiting()
        {
            // The bug this used to pin: mud_burst genuinely has a cooldown
            // authored in content, and nothing anywhere on the fight screen
            // ever said so -- the card kept showing its mana cost as though
            // it were freely castable. The fix used to make COST say the
            // wait INSTEAD of the cost; the 2026-09-22 rework gives cooldown
            // its own row so a player can see both facts (what it costs,
            // when it will be ready again) at once, neither one hiding the
            // other.
            var skill = new ResolvedSkill("cd", "Cooldown Bolt", "Waits between casts.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 5,
                resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 10,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 0, cooldownTurns: 2);
            var (session, hero) = Fight(skill);
            var foe = session.Encounter.Enemies[0];

            var before = FightHudModel.DetailForSkill(session, hero, skill);
            Assert.AreEqual("5 MP", StatValue(before, "MANA"),
                "fixture: not yet cast, so mana always reads");
            Assert.AreEqual("2 TURNS", StatValue(before, "COOLDOWN"),
                "the authored cooldown length reads BEFORE the skill is ever on cooldown, not only once it is");

            Assert.IsTrue(session.CastSkill(skill, foe), "fixture: the cast itself must succeed to arm the cooldown");

            var after = FightHudModel.DetailForSkill(session, hero, skill);
            Assert.AreEqual("5 MP", StatValue(after, "MANA"),
                "the cast still costs the same mana whether or not it happens to be ready");
            StringAssert.Contains("2 TURNS", StatValue(after, "COOLDOWN"));
            StringAssert.Contains("LEFT", StatValue(after, "COOLDOWN"),
                "waiting on it must ALSO say how much longer, not just the authored length again");
        }

        [Test]
        public void StrikesScalesRowNamesTheStrongestWeaponScalingScore()
        {
            var (_, hero) = Fight();
            hero.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);

            var panel = FightHudModel.DetailForStrike(hero);

            Assert.AreEqual("STR", StatValue(panel, "SCALES"));
        }

        [Test]
        public void AHealHasNoScalingToNameEvenWithAGradeOnTheActor()
        {
            // HealSelf's own Amount formula (flatAmount + power*resourceSpent)
            // never touches CombatMath.ScaledAttack at all -- a grade on the
            // actor is real, but irrelevant to what this specific skill does,
            // and claiming otherwise would be worse than saying nothing.
            var heal = Skill("h", "Mend", effect: SkillEffect.HealSelf, power: 5);
            var (session, hero) = Fight(heal);
            hero.SkillScaling = ScalingProfile.None.With(AbilityScore.Wisdom, ScalingGrade.S);

            var panel = FightHudModel.DetailForSkill(session, hero, heal);

            Assert.AreEqual("-", StatValue(panel, "SCALES"));
        }

        [Test]
        public void APowerlessEffectOmitsThePowerRowEntirely()
        {
            // The rework's rule: a stat that does not apply is OMITTED, never
            // shown as "-" or "0" -- POWER used to print "-" for Provoke,
            // Transform and the rest of HasNoPreviewablePower's list. Rework
            // #2 goes further: the row is not merely blanked, it is not in
            // Stats at ALL, so Rally's card is compact rather than an
            // eight-row card with a hole in the middle of it.
            var provoke = Skill("p", "Rally", effect: SkillEffect.Provoke);
            var (session, hero) = Fight(provoke);

            var panel = FightHudModel.DetailForSkill(session, hero, provoke);

            Assert.IsFalse(HasStat(panel, "POWER"), "Rally has nothing to preview; POWER must not appear at all");
            Assert.IsFalse(HasStat(panel, "DEFENSE"), "a non-damaging skill has no defense interaction to state");
            CollectionAssert.AreEqual(new[] { "MANA", "TARGET", "EFFECT", "SCALES" },
                panel.Stats.Select(s => s.Key).ToArray(),
                "a compact four-row card, not an eight-row one with four gaps");
        }

        [Test]
        public void ADamagingSkillsDefenseRowNamesThePhysicalOrMagicalSplit()
        {
            var physical = Skill("a", "Alpha", packets: new[] { new DamageInstance(DamageType.Physical, 10) });
            var magical = Skill("b", "Beta", packets: new[] { new DamageInstance(DamageType.Fire, 10) });
            var (session, hero) = Fight(physical, magical);

            Assert.AreEqual("PHYSICAL", StatValue(FightHudModel.DetailForSkill(session, hero, physical), "DEFENSE"));
            Assert.AreEqual("MAGICAL", StatValue(FightHudModel.DetailForSkill(session, hero, magical), "DEFENSE"));
        }

        [Test]
        public void ASkillThatIgnoresDefenseSaysSoInsteadOfNamingEither()
        {
            var skill = new ResolvedSkill("ig", "Pierce", "Ignores armour entirely.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 5,
                resourceCost: 0, spendsAllResource: false, power: 10, flatAmount: 0,
                ignoresDefense: true, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 0);
            var (session, hero) = Fight(skill);

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("IGNORES DEFENSE", StatValue(panel, "DEFENSE"));
        }

        [Test]
        public void AHealthCostSkillNamesTheActualHpAmountNotThePercent()
        {
            var skill = new ResolvedSkill("hc", "Blood Toll", "Pays in blood.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 0,
                resourceCost: 0, spendsAllResource: false, power: 10, flatAmount: 0,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 0, healthCostPercent: 5);
            var (session, hero) = Fight(skill);
            hero.CurrentHealth = hero.MaxHealth;

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            int expected = HealthCost.AmountFor(hero.MaxHealth, 5);
            Assert.AreEqual($"{expected} HP", StatValue(panel, "COST"));
            Assert.IsFalse(HasStat(panel, "MANA"), "this skill spends no mana; MANA must not appear");
        }

        // ---- DamageType: the skill model exposes it, FightHudModel resolves it --

        [Test]
        public void ADamagingSkillWithNoAuthoredPackets_NamesTheCastersAttackType()
        {
            // Fight()'s kit passes attackType: null (see the helper above),
            // so ActorAttackType(actor) falls through to Physical -- the
            // same fallback ScalingLabelForSkill already relies on for this
            // exact fixture shape.
            var skill = Skill("a", "Alpha");
            var (session, hero) = Fight(skill);

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("Physical", panel.DamageType);
        }

        [Test]
        public void ADamagingSkillWithNoAuthoredPackets_NamesWhicheverElementTheCastersKitDeclares()
        {
            var skill = Skill("a", "Alpha");
            var hero = new CombatantState("Hero", true, 200, 30, 20, 10);
            var foe = new CombatantState("Foe", false, 1000, 10, 5, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { skill }, null, DamageType.Fire);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1));

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("Fire", panel.DamageType);
        }

        [Test]
        public void AFixedDamageSkill_NamesItsOwnAuthoredPacketType_RegardlessOfTheCastersKit()
        {
            var skill = Skill("bolt", "Bolt", packets: new[] { new DamageInstance(DamageType.Arcane, 20) });
            var (session, hero) = Fight(skill);

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("Arcane", panel.DamageType);
        }

        [Test]
        public void AFixedDamageSkillWithTwoDistinctPacketTypes_NamesBoth()
        {
            var skill = Skill("frostflare", "Frostflare", packets: new[]
            {
                new DamageInstance(DamageType.Fire, 10),
                new DamageInstance(DamageType.Ice, 7),
            });
            var (session, hero) = Fight(skill);

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("Fire/Ice", panel.DamageType);
        }

        [Test]
        public void ANonDamagingSkill_NamesNoDamageTypeAtAll()
        {
            var heal = Skill("h", "Mend", effect: SkillEffect.HealSelf, power: 5);
            var (session, hero) = Fight(heal);

            var panel = FightHudModel.DetailForSkill(session, hero, heal);

            Assert.AreEqual("", panel.DamageType);
        }

        [Test]
        public void TheStandingCountIgnoresTheFallen()
        {
            var (session, _) = Fight();
            Assert.AreEqual(1, FightHudModel.StandingCount(session.Encounter));

            session.Encounter.Enemies[0].CurrentHealth = 0;
            Assert.AreEqual(0, FightHudModel.StandingCount(session.Encounter));
        }

        [Test]
        public void AnActorWithNoKitGetsAnEmptyListRatherThanAThrow()
        {
            var (session, _) = Fight();
            var stranger = new CombatantState("Stranger", true, 10, 10, 1, 1);

            var rows = FightHudModel.SkillRows(session, stranger);

            // BasicSpell (docs/PLAN_SHOP.md Gate 4) is gone, so a combatant
            // with no kit behind it now has nothing to offer -- an empty
            // list rather than a throw is still the graceful part.
            Assert.AreEqual(0, rows.Count);
        }

        // ---- telling two of the same monster apart -------------------------------

        [Test]
        public void TwoOfTheSameKindAreNumberedInTheOrderTheyStand()
        {
            var names = FightHudModel.DisplayNames(new[]
            {
                Foe("Giant Rat"), Foe("Bog Witch"), Foe("Giant Rat"),
            });

            CollectionAssert.AreEqual(
                new[] { "Giant Rat 1", "Bog Witch", "Giant Rat 2" }, names);
        }

        // THE ORDINAL IS INFORMATION, so it appears only where there is
        // something to tell apart. Numbering a lone Bog Witch makes the
        // encounter read as a spawn table.
        [Test]
        public void AKindWithOnlyOneOfItIsNotNumbered()
        {
            CollectionAssert.AreEqual(
                new[] { "Bog Witch" }, FightHudModel.DisplayNames(new[] { Foe("Bog Witch") }));
        }

        // OVER THE WHOLE LIST INCLUDING THE DEAD, and this is the one that
        // matters in play. A player learns which rat they have been hurting; if
        // rat 1 falls and rat 2 becomes "Giant Rat 1", the name they learned now
        // belongs to a different animal, at exactly the moment they are counting
        // on it.
        [Test]
        public void ARatKeepsItsNumberWhenTheRatBesideItFalls()
        {
            var first = Foe("Giant Rat");
            var second = Foe("Giant Rat");
            var all = new[] { first, second };

            Assert.AreEqual("Giant Rat 2", FightHudModel.DisplayNameOf(all, second));

            first.CurrentHealth = 0;

            Assert.AreEqual("Giant Rat 2", FightHudModel.DisplayNameOf(all, second),
                "the survivor was renumbered, so the name the player learned now means something else");
        }


        // ---- what an item card promises the character actually holding it ----
        //
        // CombatMath.RestoreMana already refuses a pool whose row says
        // restoredByManaEffects is false (phase B), so the affordance and the
        // effect have to agree or the card is lying. A fixture pool, not the
        // shipped mana row: nothing authors a refusing pool yet, and that is
        // exactly the case this exists to hold.
        private static ResourcePool RefusingPool() =>
            new ResourcePool(FuryRow(), capacity: 100, gainPerTurn: 0);

        private static ResolvedPool FuryRow() =>
            new ResolvedPool(
                "fury", "Fury", "FURY",
                PoolCapacityRule.Fixed, 100,
                0, 15, 10,
                10, PoolDecayTrigger.Damage,
                PoolStartRule.Zero, 0,
                "#FF8A3A", "#8E3A12", "#FFD2B0",
                pulse: true, allowsSpellBooks: false, restoredByManaEffects: false, absorbsDamage: false,
                sortOrder: 1);

        [Test]
        public void AManaPotionSaysSoForAPoolThatAcceptsIt()
        {
            var stack = new SatchelStack("elixir", "Elixir", 2, restoresMana: true);
            var panel = FightHudModel.DetailForItem(stack);

            Assert.AreEqual("Restores mana.", panel.Body);
            CollectionAssert.Contains(panel.Stats, ("EFFECT", "MANA"));
        }

        [Test]
        public void AManaPotionSaysItDoesNothingForAPoolThatRefusesIt()
        {
            var stack = new SatchelStack("elixir", "Elixir", 2, restoresMana: true);
            var panel = FightHudModel.DetailForItem(stack, RefusingPool());

            Assert.AreEqual("No effect on Fury.", panel.Body);

            // THE EFFECT CELL TOO. It is the same claim in one word, so
            // leaving it reading MANA would put the lie one column over from
            // the sentence that just withdrew it.
            CollectionAssert.Contains(panel.Stats, ("EFFECT", "-"));
        }

        [Test]
        public void AHealthPotionIsUnaffectedByWhicheverPoolTheActorHolds()
        {
            var stack = new SatchelStack("potion", "Potion", 2, restoresMana: false);

            foreach (var panel in new[] { FightHudModel.DetailForItem(stack), FightHudModel.DetailForItem(stack, RefusingPool()) })
            {
                Assert.AreEqual("Restores health.", panel.Body);
                CollectionAssert.Contains(panel.Stats, ("EFFECT", "HEAL"));
            }
        }

        private static CombatantState Foe(string name) =>
            new CombatantState(name, false, 40, 0, 5, 3);
    }
}
