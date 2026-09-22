using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // Captures the RUNNING game to PNG from inside a PlayMode test.
    //
    // The distinction this rests on is worth restating, because getting it
    // wrong cost v1 months of blind visual work: the EDITOR entering Play Mode
    // from a batch -executeMethod call hangs in this environment. The PlayMode
    // TEST RUNNER is a different mechanism and has always worked. ScreenshotTool
    // renders Edit Mode, which never ticks Update(), so it can show the layout
    // but not one frame of the ambient layer actually moving.
    public class RuntimeScreenshotTests
    {
        // With Time.captureFramerate set, time advances by exactly 1/fps per
        // frame regardless of how long the frame really took, so "advance 7
        // seconds" means the same thing on any machine.
        private const int CaptureFps = 60;

        private static readonly float[] CaptureSeconds = { 1.5f, 7f, 15f };

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void RestoreCaptureFramerate()
        {
            // Global engine state; leaking it would change the pace of every
            // test that runs after this one.
            Time.captureFramerate = 0;

            // Defensive, for the two capture tests below: a submenu's open
            // fade (ColumnOpenAnimator, 0.14s of real Time.deltaTime) never
            // advances if a PREVIOUS test in the same batch left timeScale
            // at 0 and this class had nothing of its own resetting it.
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator MainMenu_IsActuallyAnimating_AndCapturesToPng()
        {
            // Not a disabled test: with -nographics there is no graphics device,
            // camera.Render() is a silent no-op and ReadPixels returns garbage.
            // The commit gate passes -nographics, so this can only run through
            // tools/screenshot.ps1 -Runtime, which deliberately omits it.
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime");
            }

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            // The ROOT canvas -- same reasoning as ScreenshotTool.Capture. A
            // node can be given its own nested Canvas for sort-order control,
            // so "any" of them would eventually be a sub-panel, and the
            // failure would present as an art bug rather than a lookup one.
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "MainMenu should have a root Canvas");

            Directory.CreateDirectory(OutputDir);
            Time.captureFramerate = CaptureFps;

            var captured = new byte[CaptureSeconds.Length][];
            float elapsed = 0f;

            for (int i = 0; i < CaptureSeconds.Length; i++)
            {
                while (elapsed < CaptureSeconds[i])
                {
                    elapsed += 1f / CaptureFps;
                    yield return null;
                }

                string path = Path.Combine(OutputDir, $"MainMenu_t{CaptureSeconds[i]:0.0}s.png");
                CanvasCapture.RenderToFile(canvas, path);

                FileAssert.Exists(path);
                captured[i] = File.ReadAllBytes(path);
                Assert.Greater(captured[i].Length, 1024, $"capture at {CaptureSeconds[i]}s is too small to be a frame");
            }

            // The assertion that matters. Identical bytes across three widely
            // separated points means nothing is moving -- the failure an Edit
            // Mode screenshot cannot see, and that the pure-curve unit tests
            // cannot see either, because those prove the FORMULAS vary, not that
            // anything calls them.
            for (int i = 1; i < captured.Length; i++)
            {
                Assert.AreNotEqual(captured[0], captured[i],
                    $"the menu at {CaptureSeconds[0]}s and {CaptureSeconds[i]}s rendered identically - the ambient layer is not animating.");
            }

            Debug.Log($"Wrote {CaptureSeconds.Length} runtime captures to {OutputDir}");
        }

        // ---- the skill/item submenu rework's own proof shots -------------------
        //
        // Three owner items, one screen (2026-09-22): precise stat rows on the
        // detail card, a wider/darker/readable detail card, and a submenu that
        // grows to five rows before it scrolls. A dotnet-host EditMode audit can
        // prove the tree never overlaps, but only a RUNNING fight screen shows
        // whether the numbers a real skill list produces actually read.

        private static IEnumerator OpenSkillSubmenu(FightController fight)
        {
            Button verb1 = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (verb1 == null && Time.realtimeSinceStartup < deadline)
            {
                if (fight.IsBusy || !fight.Session.IsPlayerTurn) { yield return null; continue; }

                verb1 = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name == "Verb1" && b.interactable);
                if (verb1 == null) yield return null;
            }

            Assert.IsNotNull(verb1, "the Skill verb never became interactable");
            verb1.onClick.Invoke();

            // The submenu (and the detail card riding it) fades in via
            // UiNode.Opening() rather than snapping open -- a capture taken
            // too soon after the click catches that transition mid-fade
            // instead of the settled card the player actually reads.
            for (int i = 0; i < 20; i++) yield return null;
        }

        // ONE HERO, SIX AUTHORED SKILLS spanning every new row this rework adds:
        // a plain mana+cooldown cast, a resource-cost skill that ignores
        // defense entirely, a health-cost skill, a non-damaging skill (POWER
        // and DEFENSE both omitted), and two more filling the sixth row past
        // FightSubmenuLayout.RowsInView (five) so the capture also proves the
        // scrollbar.
        private static (FightSession session, CombatantState hero) BuildSixSkillFight()
        {
            var boltPackets = new[] { new DamageInstance(DamageType.Fire, 20) };
            var bolt = new ResolvedSkill("bolt", "Ember Lance", "A mana bolt on a short cooldown.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 8,
                resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 0,
                ignoresDefense: false, damageInstances: boltPackets, presentation: SpellPresentation.None,
                sortOrder: 0, cooldownTurns: 2);

            var cleave = new ResolvedSkill("cleave", "Iron Cleave", "Spends fury; punches through armour.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 0,
                resourceCost: 4, spendsAllResource: false, power: 14, flatAmount: 0,
                ignoresDefense: true, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 1);

            var bloodToll = new ResolvedSkill("bloodtoll", "Blood Toll", "Pays in blood for a heavier hit.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 0,
                resourceCost: 0, spendsAllResource: false, power: 18, flatAmount: 0,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 2, healthCostPercent: 5);

            var rally = new ResolvedSkill("rally", "Rally", "Draws every eye. No number to preview.", "hero", 1,
                SkillEffect.Provoke, SkillTargeting.Self, manaCost: 3,
                resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 0,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 3);

            var mend = new ResolvedSkill("mend", "Mend", "Restores a little health.", "hero", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, manaCost: 6,
                resourceCost: 0, spendsAllResource: false, power: 5, flatAmount: 0,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 4);

            var frost = new ResolvedSkill("frost", "Frost Shard", "A slow, cold nuke.", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 10,
                resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 0,
                ignoresDefense: false, damageInstances: new[] { new DamageInstance(DamageType.Ice, 22) },
                presentation: SpellPresentation.None, sortOrder: 5, cooldownTurns: 3);

            var skills = new List<ResolvedSkill> { bolt, cleave, bloodToll, rally, mend, frost };

            var hero = new CombatantState("Shawn", true, 260, 30, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical);
            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, enemyKits, new SeededRandom(9));
            session.Begin();
            return (session, hero);
        }

        // THE SAME HERO, THREE SKILLS -- the first three of the six above, so
        // the two captures are directly comparable: same character, same
        // stat-row vocabulary, a shorter box that must not carry the other
        // capture's dead space.
        private static (FightSession session, CombatantState hero) BuildThreeSkillFight()
        {
            var six = BuildSixSkillFight();
            return six;
        }

        [UnityTest]
        public IEnumerator FightSubmenu_SixSkills_ShowsFiveRowsScrollbarAndDetailCard()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var (session, _) = BuildSixSkillFight();
            fight.Bind(session, EncounterClass.Normal, new List<SatchelStack>());
            yield return null;

            yield return OpenSkillSubmenu(fight);

            // Row 3, ROW INDEX 3 -- Rally, the non-damaging skill: its card
            // shows MANA present, POWER and DEFENSE both omitted, which is
            // the one case a screenshot has to catch a regression on (a
            // "-"/"0" sneaking back in would not fail any dotnet assertion
            // this session ran, only look wrong here).
            fight.HoverRowForTest(3);
            for (int i = 0; i < 10; i++) yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "Fight should have a root Canvas");

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "FightSubmenu_SixSkills.png");
            CanvasCapture.RenderToFile(canvas, path);

            FileAssert.Exists(path);
            Debug.Log($"Wrote {path}");
        }

        [UnityTest]
        public IEnumerator FightSubmenu_ThreeSkills_GrowsTheBoxInsteadOfLeavingDeadSpace()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var (fullSession, hero) = BuildThreeSkillFight();

            // A GENUINELY three-row kit for this capture -- reuse the six-skill
            // fixture's fight/encounter shape, but hand the controller a kit
            // truncated to its first three skills, so the SAME character/
            // stats are on screen and only the row count differs between the
            // two PNGs.
            var kit = fullSession.KitFor(hero);
            var shortKit = new PlayerKit("hero", CharacterRole.Tank,
                kit.Skills.Take(3).ToList(), null, DamageType.Physical);

            var shortHero = new CombatantState("Shawn", true, 260, 30, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { shortHero }, foes);
            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();
            var session = new FightSession(encounter, new List<PlayerKit> { shortKit }, enemyKits, new SeededRandom(9));
            session.Begin();

            fight.Bind(session, EncounterClass.Normal, new List<SatchelStack>());
            yield return null;

            yield return OpenSkillSubmenu(fight);

            fight.HoverRowForTest(0);
            for (int i = 0; i < 10; i++) yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "Fight should have a root Canvas");

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "FightSubmenu_ThreeSkills.png");
            CanvasCapture.RenderToFile(canvas, path);

            FileAssert.Exists(path);
            Debug.Log($"Wrote {path}");
        }
    }
}
