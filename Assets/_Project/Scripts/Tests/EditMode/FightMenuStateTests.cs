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
            Assert.AreEqual(7, menu.ManaPreview);
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
            Assert.AreEqual(0, menu.ManaPreview);
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
            Assert.AreEqual(0, menu.ManaPreview);
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

        private static (FightSession session, CombatantState hero) Fight(params ResolvedSkill[] skills)
        {
            var hero = new CombatantState("Hero", true, 200, 30, 20, 0, 10);
            var foe = new CombatantState("Foe", false, 1000, 10, 5, 0, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0));
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1));
            return (session, hero);
        }

        [Test]
        public void TheBasicSpellGetsARowAfterTheAuthoredSkills()
        {
            // Every character has one and no character has it in their list, so
            // its index is stated once rather than recomputed at each call site.
            var (session, hero) = Fight(Skill("a", "Alpha"), Skill("b", "Beta"));

            var rows = FightHudModel.SkillRows(session, hero);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(FightHudModel.BasicSpellRow(2), rows.Count - 1);
            Assert.IsTrue(rows[2].IsBasicSpell);
            Assert.AreEqual("Spark", rows[2].Name);
        }

        [Test]
        public void TheBasicSpellsCostComesFromTheSameResolutionEveryOtherSkillUses()
        {
            // v1 hand-rolled a separate mana check for this one row. That is
            // exactly the shape that drifts.
            var (session, hero) = Fight();

            var basic = FightHudModel.SkillRows(session, hero).Single();

            Assert.AreEqual(session.BasicSpellManaCostFor(hero), basic.ManaCost);
            Assert.AreEqual(session.CanAffordBasicSpell(hero), basic.CanPay);
        }

        [Test]
        public void AnUnaffordableRowIsShownAndMarked_NeverDropped()
        {
            // The player should learn what their character has rather than watch
            // the list change length as their mana moves.
            var (session, hero) = Fight(Skill("cheap", "Cheap", manaCost: 1), Skill("dear", "Dear", manaCost: 999));
            hero.CurrentMana = 5;

            var rows = FightHudModel.SkillRows(session, hero);

            Assert.AreEqual(3, rows.Count, "both authored skills plus the basic spell");
            Assert.IsTrue(rows[0].Affordable);
            Assert.IsFalse(rows[1].Affordable);
        }

        [Test]
        public void NoMoneyAndNoRequirementAreSeparateFlags()
        {
            // They render identically today. The distinction has to already
            // exist here for anything to ever say which it is.
            var (session, hero) = Fight(Skill("dear", "Dear", manaCost: 999));
            hero.CurrentMana = 0;

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

            Assert.AreEqual("5 MP", FightHudModel.CostLabel(manaOnly, "Wool"));
            Assert.AreEqual("FREE", FightHudModel.CostLabel(free, "Wool"),
                "a skill that costs nothing says so - '0 MP' reads as a missing value");
        }

        [Test]
        public void APacketSpellShowsTheSumOfItsPacketsScaledByTheCastersOwnTier()
        {
            // The packets are the SAME multiplier ResolveDamageInstances
            // itself applies -- Fight()'s spell tier is 1.5x, so 30 Fire and
            // 20 Ice read 45 and 30, not the raw 30/20 authored in content.
            // Showing the raw sum would be exactly the kind of "close but not
            // the real number" the Strike row had before this fix.
            var packets = new[]
            {
                new DamageInstance(DamageType.Fire, 30),
                new DamageInstance(DamageType.Ice, 20),
            };
            var spell = Skill("bolt", "Prismatic Bolt", power: 0, packets: packets);
            var (session, hero) = Fight(spell);

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
            // Hero's Attack (20, scaled) + flatAmount (12), on the x10 scale,
            // with no target so no defense subtracted: (20 + 12) * 10 = 320.
            Assert.AreEqual("320", power);
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
            // dealing real damage. This pins the fix: the CASTER'S OWN Attack,
            // scaled and put on the x10 scale every other damage number uses,
            // with no target and therefore no defense subtracted. Hero's
            // Attack is 20 and this skill spends no signature resource (no
            // Signature is set on the test hero), so `power: 12` never
            // actually contributes -- which is the point: the row now reads
            // what the caster's own stats produce, not the authored constant.
            var skill = Skill("a", "Alpha", power: 12);
            var (session, hero) = Fight(skill);

            Assert.AreEqual("200", FightHudModel.PowerLabel(session, hero, skill));
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
            // POWER is Scale(ScaledAttack(...)), not the raw Attack stat --
            // hero.Attack is 20, and CombatMath puts every damage number on
            // its own x10 scale, so a swing with nothing else in play reads
            // 200, matching what ComputeAttackDamage would produce against a
            // defenseless target.
            var (_, hero) = Fight();

            var panel = FightHudModel.DetailForStrike(hero);

            Assert.AreEqual("Strike", panel.Name);
            Assert.AreEqual(5, panel.Stats.Count, "the column has five fixed rows");
            Assert.AreEqual("200", panel.Stats[1].Value,
                "it describes THIS actor's swing, not a generic one");
        }

        [Test]
        public void ASkillsDetailPanelFillsAllFiveStatRows()
        {
            var skill = Skill("a", "Alpha", manaCost: 5, power: 12);
            var (session, hero) = Fight(skill);

            var panel = FightHudModel.DetailForSkill(session, hero, skill);

            Assert.AreEqual("Alpha", panel.Name);
            Assert.AreEqual(5, panel.Stats.Count);
            CollectionAssert.AreEqual(new[] { "COST", "POWER", "TARGET", "EFFECT", "SCALES" },
                panel.Stats.Select(s => s.Key).ToArray());
        }

        [Test]
        public void ADetailCardOnCooldownShowsThatInsteadOfItsCost()
        {
            // The bug this pins: mud_burst genuinely has a cooldown authored
            // in content, and nothing anywhere on the fight screen ever said
            // so -- the card kept showing its mana cost as though it were
            // freely castable. COST already knows how to say "not yet" for a
            // skill the row model gates the same way (see SkillRows' own
            // comment); this is that same rule reaching the detail card.
            var skill = new ResolvedSkill("cd", "Cooldown Bolt", "Waits between casts.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 5,
                resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 10,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 0, cooldownTurns: 2);
            var (session, hero) = Fight(skill);
            var foe = session.Encounter.Enemies[0];

            var before = FightHudModel.DetailForSkill(session, hero, skill);
            Assert.AreEqual("5 MP", before.Stats[0].Value,
                "fixture: not yet cast, so this should read its plain cost");

            Assert.IsTrue(session.CastSkill(skill, foe), "fixture: the cast itself must succeed to arm the cooldown");

            var after = FightHudModel.DetailForSkill(session, hero, skill);
            Assert.AreNotEqual("5 MP", after.Stats[0].Value,
                "a skill actually on cooldown must not still read its mana cost as though it were castable");
            StringAssert.Contains("TURN", after.Stats[0].Value);
        }

        [Test]
        public void StrikesScalesRowNamesTheStrongestWeaponScalingScore()
        {
            var (_, hero) = Fight();
            hero.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);

            var panel = FightHudModel.DetailForStrike(hero);

            Assert.AreEqual("SCALES", panel.Stats[4].Key);
            Assert.AreEqual("STR", panel.Stats[4].Value);
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

            Assert.AreEqual("-", panel.Stats[4].Value);
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
            var stranger = new CombatantState("Stranger", true, 10, 10, 1, 0, 1);

            var rows = FightHudModel.SkillRows(session, stranger);

            // One row: the basic spell, which every combatant conceptually has
            // even with no kit behind it. Graceful, not empty-and-confusing.
            Assert.AreEqual(1, rows.Count);
            Assert.IsTrue(rows[0].IsBasicSpell);
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

        private static CombatantState Foe(string name) =>
            new CombatantState(name, false, 40, 0, 5, 0, 3);
    }
}
