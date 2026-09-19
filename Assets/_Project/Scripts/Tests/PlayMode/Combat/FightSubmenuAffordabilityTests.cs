using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Finding 3 (code review): RefreshSubmenu used to call
    // ThemedButtonState.SetMenuState BEFORE setting the row Button's own
    // interactable flag. SetMenuState ends in Refresh(), which reads
    // Button.interactable right then to choose the plate tint -- so a row
    // that just went unaffordable this repaint still painted off LAST
    // repaint's interactable value, one frame behind row.Affordable.
    public class FightSubmenuAffordabilityTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void RestoreBeatSpeed() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        // The exact colour ThemedButtonState paints an unaffordable row --
        // its own private DisabledTint, pinned here as a literal rather than
        // read off the component (CLAUDE.md gotcha 5: a test must not
        // recompute the production value it is checking).
        private static readonly Color DisabledTint = new Color(0.45f, 0.45f, 0.45f, 1f);

        [UnityTest]
        public IEnumerator ASkillThatBecomesUnaffordableShowsTheDisabledPlateInTheSameRepaint()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 5, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);

            // One authored skill costing all 5 of hero's mana -- affordable at
            // full mana, not once it is drained.
            var skill = new ResolvedSkill("s", "Spellblade", "", "shawn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, manaCost: 5, resourceCost: 0, spendsAllResource: false,
                power: 1, flatAmount: 0, ignoresDefense: false, damageInstances: null,
                presentation: SpellPresentation.None, sortOrder: 0);

            var kit = new PlayerKit("shawn", CharacterRole.Tank, new List<ResolvedSkill> { skill }, null, null);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal, new List<SatchelStack>());
            yield return null;

            // Verb1 is SKILL -- OnVerbPressed's own switch: 0 ATTACK, 1 SKILL,
            // 2 ITEM, 3 MOVE (see FightSubmenuScrollTests for the same
            // convention on Verb2/ITEM).
            Click("Verb1");
            yield return null;

            var row = Named("CharacterSkill0");
            Assert.IsNotNull(row, "the skill submenu did not open its first row");
            var themed = row.GetComponent<ThemedButtonState>();
            Assert.IsNotNull(themed, "the submenu row is not a themed button");

            Assert.IsTrue(row.GetComponent<Button>().interactable,
                "fixture: the skill must start affordable at full mana");
            Assert.AreNotEqual(DisabledTint, themed.Plate.color,
                "fixture: an affordable row must not already read as disabled");

            // Drain the mana out from under the row with no other state
            // change, then repaint EXACTLY ONCE -- the ordering bug means a
            // single repaint is not enough to even START the fade toward the
            // right target; it needs a SECOND, unrelated repaint (another
            // hover, another beat) before the plate ever begins to darken.
            hero.PrimaryPool.Current = 0;
            _fight.RefreshUi();

            // ThemedButtonState fades the plate over FadeSeconds (0.12s)
            // rather than snapping it -- give that ONE repaint's fade time to
            // finish, and nothing else, before reading the settled colour.
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.IsFalse(row.GetComponent<Button>().interactable,
                "the row must have gone non-interactable off this one repaint");
            Assert.AreEqual(DisabledTint, themed.Plate.color,
                "the plate must settle on the disabled tint from the ONE repaint that made the row " +
                "unaffordable -- not still fading toward last repaint's (still-affordable) target");
        }

        // ---- the item branch asks about the drinker, not about nobody -----------
        //
        // a4c85461 gave FightHudModel.ItemRows the drinker's pool and gave the
        // hover panel and the press the same predicate; the CONTROLLER kept
        // calling ItemRows(_satchel) with no actor, so the row a hand actually
        // presses was still the one reader out of three asking the wrong
        // question. FightConsumableTests pins the model; this pins the seam,
        // which is the half that was broken.
        //
        // A FURY pool, not just any refusing pool: Bjorn is the shipped case
        // (pools.json's fury row, restoredByManaEffects false), and it is his
        // turn that makes a Mana Draught inert.
        private static ResourcePool FuryPool()
        {
            var pool = new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 15, gainOnDamageTaken: 10);
            pool.ShortTag = "FURY";
            pool.RestoredByManaEffects = false;
            return pool;
        }

        [UnityTest]
        public IEnumerator AManaPotionsRowIsGreyedForTheFuryHolderWhoseTurnItIs()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            // Speed 10 against the foe's 4, same as the fixture above, so the
            // acting character when the menu opens is unambiguously Bjorn.
            var bjorn = new CombatantState("Bjorn", true, 300, FuryPool(), 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { bjorn }, foes);

            var kit = new PlayerKit("bear", CharacterRole.Tank, new List<ResolvedSkill>(), null, null);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9));
            session.Begin();

            // Row 0 is the potion his pool refuses; row 1 is the control. A
            // fixture holding only the mana potion would pass just as well if
            // the seam greyed the whole satchel.
            var satchel = new List<SatchelStack>
            {
                new SatchelStack("ether", "Mana Draught", 3, restoresMana: true),
                new SatchelStack("salve", "Field Salve", 2, restoresMana: false),
            };

            _fight.Bind(session, EncounterClass.Normal, satchel);
            yield return null;

            Assert.AreSame(bjorn, _fight.ActingCharacterForTest(),
                "fixture: the fury holder must be the one acting when the item list opens");

            // Verb2 is ITEM -- OnVerbPressed's own switch: 0 ATTACK, 1 SKILL,
            // 2 ITEM, 3 MOVE.
            Click("Verb2");
            yield return null;

            var rows = _fight.CurrentRowsForTest();
            Assert.AreEqual(2, rows.Count, "the item list did not open on the fixture satchel");

            Assert.AreEqual("CONSUMABLE  ·  NO EFFECT", rows[0].Meta,
                "the mana potion's row must say what it would do for a Fury holder, which is nothing");
            Assert.IsFalse(rows[0].CanPay,
                "a potion that would move no bar must not be payable -- pressing it spends the turn " +
                "and deletes the potion from the save");

            Assert.AreEqual("CONSUMABLE  ·  HEALTH", rows[1].Meta,
                "control: a health potion is unaffected by whose pool is asking");
            Assert.IsTrue(rows[1].CanPay, "control: the health potion must stay payable");

            // And the same fact through the scene, which is what a hand meets.
            Assert.IsFalse(Named("CharacterSkill0").GetComponent<Button>().interactable,
                "the mana potion's row must be non-interactable, not merely labelled NO EFFECT");
            Assert.IsTrue(Named("CharacterSkill1").GetComponent<Button>().interactable,
                "control: the health potion's row must stay pressable");
        }

        // ---- the SPELL submenu's row art (owner, 2026-09-19) --------------------
        //
        // "Art for spell book is not shown in the select spell screen." fe40bd36
        // baked shop.skillArt and controller.icons (shop, dossier) off the real
        // ContentDatabase.Skills but never reached this row pool -- FightController
        // had no submenuMarks/skillArt fields, WireFight bound nothing, and
        // RefreshSubmenu painted only names. The join key this fixture actually
        // exercises is a skill's Id: fight.skillArt is baked in ScreenRegistry.
        // Fight() from the REAL skills.json at scene-build time, so a fixture
        // skill only needs to reuse one of the four authored book ids
        // (mud_burst/frost_flare/cinderfault/lightning_bolt) to land a hit --
        // its own constructor fields are otherwise irrelevant to the lookup.
        [UnityTest]
        public IEnumerator ARowWithBookArtShowsItsMarkAndARowWithoutStaysInactive()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var withArt = new ResolvedSkill("mud_burst", "Mud Burst", "", "shawn", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 5, resourceCost: 0,
                spendsAllResource: false, power: 1, flatAmount: 0, ignoresDefense: false,
                damageInstances: null, presentation: SpellPresentation.None, sortOrder: 0);

            // No skills.json row owns this id, so ItemIcons.Find must miss --
            // the control that proves a mark can also stay off.
            var withoutArt = new ResolvedSkill("no_such_skill_art", "Plain Strike", "", "shawn", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 3, resourceCost: 0,
                spendsAllResource: false, power: 1, flatAmount: 0, ignoresDefense: false,
                damageInstances: null, presentation: SpellPresentation.None, sortOrder: 1);

            var hero = new CombatantState("Shawn", true, 300, 5, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);

            // kit.Skills order is row order (SkillOptionsFor's own header: a
            // Select((s, i) => ...) over kit.Skills with no reorder), so
            // withArt lands on row 0 and withoutArt on row 1 deterministically.
            var kit = new PlayerKit("shawn", CharacterRole.Tank,
                new List<ResolvedSkill> { withArt, withoutArt }, null, null);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal, new List<SatchelStack>());
            yield return null;

            Click("Verb1");
            yield return null;

            var mark0 = Named("CharacterSkill0Mark")?.GetComponent<Image>();
            var mark1 = Named("CharacterSkill1Mark")?.GetComponent<Image>();
            Assert.IsNotNull(mark0, "row 0's mark node was not found -- FightScreen.BuildSubmenuColumn's own name");
            Assert.IsNotNull(mark1, "row 1's mark node was not found");

            Assert.IsTrue(mark0.enabled, "mud_burst has authored art in skills.json and must show its mark");
            Assert.IsNotNull(mark0.sprite, "the mark must actually carry the book's sprite, not just be enabled");

            Assert.IsFalse(mark1.enabled,
                "a skill with no authored iconPath must leave its mark inactive, not show a stale sprite");
        }

        // Depth change reuses the SAME pooled row nodes for a different list
        // (BuildSubmenuColumn's own header) -- an element choice is not a
        // skill, so opening one must not leave the skill row's mark showing
        // through it.
        [UnityTest]
        public IEnumerator TheElementListDoesNotInheritTheSkillRowsMark()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var dualElement = new ResolvedSkill("mud_burst", "Mud Burst", "", "shawn", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 5, resourceCost: 0,
                spendsAllResource: false, power: 1, flatAmount: 0, ignoresDefense: false,
                damageInstances: null, presentation: SpellPresentation.None, sortOrder: 0,
                elements: new[]
                {
                    new ElementChoice { Type = DamageType.Fire },
                    new ElementChoice { Type = DamageType.Nature },
                });

            var hero = new CombatantState("Shawn", true, 300, 5, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);

            var kit = new PlayerKit("shawn", CharacterRole.Tank,
                new List<ResolvedSkill> { dualElement }, null, null);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal, new List<SatchelStack>());
            yield return null;

            Click("Verb1");
            yield return null;

            var skillMark0 = Named("CharacterSkill0Mark")?.GetComponent<Image>();
            Assert.IsNotNull(skillMark0);
            Assert.IsTrue(skillMark0.enabled, "fixture: the skill row must show its mark before the element press");

            // Same pooled node, now serving the element list this skill opens.
            Click("CharacterSkill0");
            yield return null;

            var elementMark0 = Named("CharacterSkill0Mark")?.GetComponent<Image>();
            Assert.IsNotNull(elementMark0, "the element list reuses the same pooled mark node");
            Assert.IsFalse(elementMark0.enabled,
                "an element choice is not a skill -- the row pool must not still show the skill's mark " +
                "it was carrying one depth up");
        }
    }
}
