using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // ONE PREVIEW, ONE FIGHT -- including when the preview is refused.
    //
    // The seven DevForced* keys are a preview's opinion about a single fight,
    // written by tools/preview.ps1 through PreviewRequestWatcher and cleared by
    // FightBootstrap the moment it reads them. Five of them were consumed
    // inside BuildPlaceholderFight and the sixth after the null check on what
    // it returned -- so a REFUSED spell plan (an unsupported effect, an id that
    // is not in content) took the early return with ForcedFirstAction still
    // set, and the next fight opened in that Editor session force-cast a skill
    // nobody had asked for.
    //
    // What makes that worth a test rather than a careful comment: nothing about
    // the leak is visible at the moment it happens. The refusal is printed
    // correctly, the stage is empty as promised, and the wrong fight arrives
    // later, in a different run, with no line naming the cause.
    //
    // Scene-driven rather than a direct call to the bootstrap, because WHERE
    // the consumption happens relative to the early return is the entire
    // subject -- a test that called ConsumeDevForced itself would pass over the
    // bug.
    public class DevForcedPreviewKeyTests
    {
        private const string UnknownSkill = "no_such_skill_devforced_key_test";

        [SetUp]
        public void ClearBefore() => ClearKeys();

        [TearDown]
        public void ClearAfter() => ClearKeys();

        private static void ClearKeys()
        {
            FightBootstrap.DevForcedEnemyId = null;
            FightBootstrap.DevForcedFormation = null;
            FightBootstrap.DevForcedEnemyScript = false;
            FightBootstrap.DevForcedSkillId = null;
            FightBootstrap.DevForcedSquad = null;
            FightBootstrap.DevForcedFirstAction = null;
            FightBootstrap.DevForcedElement = null;
        }

        private static void AssertNothingIsStillSet(string after)
        {
            Assert.IsEmpty(FightBootstrap.DevForcedEnemyId ?? "", $"DevForcedEnemyId survived {after}");
            Assert.IsEmpty(FightBootstrap.DevForcedFormation ?? "", $"DevForcedFormation survived {after}");
            Assert.IsFalse(FightBootstrap.DevForcedEnemyScript, $"DevForcedEnemyScript survived {after}");
            Assert.IsEmpty(FightBootstrap.DevForcedSkillId ?? "", $"DevForcedSkillId survived {after}");
            Assert.IsEmpty(FightBootstrap.DevForcedSquad ?? "", $"DevForcedSquad survived {after}");

            // The one that actually leaked, named last because it is the one
            // whose survival is felt rather than seen: FightController.
            // ForceFirstAction casts it through the same buttons a hand would
            // press, in whatever fight opens next.
            Assert.IsEmpty(FightBootstrap.DevForcedFirstAction ?? "",
                $"DevForcedFirstAction survived {after}, so the next fight this Editor session opens will " +
                "force-cast a skill nobody asked for.");

            Assert.IsEmpty(FightBootstrap.DevForcedElement ?? "", $"DevForcedElement survived {after}");
        }

        private static IEnumerator OpenTheFightScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // The bug, exactly: preview.ps1 -Spell sets ForcedSkillId and
        // ForcedFirstAction to the same id, and the plan is refused.
        [UnityTest]
        public IEnumerator ARefusedSpellPreviewLeavesNoKeyBehind()
        {
            FightBootstrap.DevForcedSkillId = UnknownSkill;
            FightBootstrap.DevForcedFirstAction = UnknownSkill;
            FightBootstrap.DevForcedFormation = PreviewFight.FormationFull;

            yield return OpenTheFightScene();

            AssertNothingIsStillSet("a refused spell preview");
        }

        // And the same for a preview that WORKS, so the fix cannot be "clear
        // the keys on the failure path" -- which would leave the two routes
        // clearing them in two places again.
        [UnityTest]
        public IEnumerator APreviewThatBuildsAFightAlsoLeavesNoKeyBehind()
        {
            var mob = ContentDatabase.Enemies.FirstOrDefault(e => e != null && e.Data != null);
            if (mob == null)
            {
                Assert.Ignore("no enemies in content, so no fight can be forced.");
            }

            FightBootstrap.DevForcedEnemyId = mob.id;
            FightBootstrap.DevForcedEnemyScript = true;
            FightBootstrap.DevForcedFormation = PreviewFight.FormationLone;

            yield return OpenTheFightScene();

            AssertNothingIsStillSet("a preview that built its fight");
        }

        // THE WIRE VALUE, PINNED. Everything above compares DevForcedFormation
        // against PreviewFight's own consts, which would still pass if both
        // silently changed together -- tools/preview.ps1 sets this key with a
        // plain string literal (it is PowerShell; it cannot reference a C#
        // const), so one side of that handshake has to be a literal or a
        // rename on this side breaks -Formation full without any test noticing.
        [Test]
        public void TheFullFormationConstIsTheStringToolsPreviewPs1ActuallySends()
        {
            Assert.AreEqual("full", PreviewFight.FormationFull);
        }
    }
}
