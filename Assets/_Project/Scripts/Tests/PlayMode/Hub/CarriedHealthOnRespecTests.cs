using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // ANYTHING THAT MOVES A MAXIMUM HAS TO RESCALE WHAT IS CARRIED AGAINST IT.
    //
    // RunEncounter.ScaleCarriedHealth states the rule as widely as it can be
    // stated: "The run stores current health as an ABSOLUTE number per
    // character, and the maximum it is a fraction of is computed from the
    // character -- so anything that changes the maximum silently changes the
    // fraction." Three things in the tree move a max health, and only one of
    // them obeyed: EquipmentOps.Equip/Unequip and the reward track's claim.
    //
    // TalentOps.Kindle and Character.Respec did not. A respec is the reachable
    // half: it clears unlockedTalentIds AND investedAbilityScores, and invested
    // Constitution is 20 max health a point through AbilityDerivation. So a
    // character who had spent points into Constitution and then respecced kept
    // the absolute number against a maximum that had just dropped -- clamped by
    // the bar rather than scaled, which is the healing-exploit's mirror image.
    //
    // PlayMode: ScaleCarriedHealth reads SaveSlotManager and ContentDatabase,
    // both Core.
    public class CarriedHealthOnRespecTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-respec-hp-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // The numbers, stated rather than recomputed:
        //
        //   Shawn's authored maxHealth is 200 and his Constitution is 14
        //   (characters.json). AbilityDerivation gives +20 max health per point
        //   above the neutral 10, so his effective maximum is 200 + 80 = 280.
        //   One invested point takes Constitution to 15 and the maximum to 300.
        //
        // Both are asserted below as FIXTURE checks, so this test says "content
        // moved" rather than "the rescale broke" if either changes.
        private const int ShawnsMaxWithNothingInvested = 280;
        private const int ShawnsMaxWithOnePointInConstitution = 300;

        [Test]
        public void RespeccingKeepsTheCarriedFractionRatherThanTheAbsoluteNumber()
        {
            var character = new Character("sheep");
            character.unspentStatPoints = 1;
            Assert.IsTrue(character.Invest(AbilityScore.Constitution),
                "fixture: the point did not go in");

            Assert.AreEqual(ShawnsMaxWithOnePointInConstitution,
                Content.ContentDatabase.EffectiveStats(character).maxHealth,
                "fixture: content moved -- Shawn's base maxHealth or Constitution is not what this pins");

            // Half health, carried by a run.
            RunManager.StartRun(4242UL);
            var run = RunManager.Run;
            run.currentHealth.Add(new RunHealthEntry { characterId = "sheep", hp = 150 });

            TalentOps.Respec(character, embersSpent: 0);

            Assert.AreEqual(ShawnsMaxWithNothingInvested,
                Content.ContentDatabase.EffectiveStats(character).maxHealth,
                "fixture: the respec did not give the point back");

            // 150 of 300 rescaled onto 280, rounded rather than truncated:
            // (150 * 280 + 150) / 300 = 42150 / 300 = 140.
            Assert.AreEqual(140, run.currentHealth[0].hp,
                "the run kept 150 against a maximum that had just dropped to 280");
        }

        [Test]
        public void ADownedCharacterIsNotRevivedByARespec()
        {
            // Zero times any fraction is zero, and the floor of 1 in
            // CarriedHealth.Rescaled applies only to somebody who was standing.
            // Asserted here as well as in CarriedHealthTests because this is a
            // NEW caller of that rule and "a respec resurrected the party" is
            // the shape of bug a wrapper introduces.
            var character = new Character("sheep");
            character.unspentStatPoints = 1;
            character.Invest(AbilityScore.Constitution);

            RunManager.StartRun(4242UL);
            var run = RunManager.Run;
            run.currentHealth.Add(new RunHealthEntry { characterId = "sheep", hp = 0 });

            TalentOps.Respec(character, embersSpent: 0);

            Assert.AreEqual(0, run.currentHealth[0].hp, "a respec stood a downed character back up");
        }

        [Test]
        public void ARespecStillHandsBackBothCurrencies()
        {
            // The wrapper must not change what a respec IS. Character.Respec's
            // own contract: every ember spent, every stat point placed.
            var character = new Character("sheep");
            character.unspentStatPoints = 2;
            character.Invest(AbilityScore.Constitution);
            character.Invest(AbilityScore.Strength);
            character.unlockedTalentIds.Add("some_talent");
            character.embers = 0;

            var refund = TalentOps.Respec(character, embersSpent: 7);

            Assert.AreEqual(7, refund.Embers);
            Assert.AreEqual(2, refund.StatPoints);
            Assert.AreEqual(7, character.embers);
            Assert.AreEqual(2, character.unspentStatPoints);
            Assert.IsEmpty(character.unlockedTalentIds);
        }

        // THE FIFTH MOVER OF A MAXIMUM, and the one a player reaches from
        // inside a descent.
        //
        // 33d88bcc closed this class for three of them (equip, unequip, the
        // respec) and RewardTrackController.Input.Claim for a fourth. The
        // dossier's "+" was left: CharacterDossierController.Spend calls
        // character.Invest, saves and repaints, with no photograph of the
        // maximum and no rescale after it. Invested Constitution pays 20 max
        // health a point through AbilityDerivation, so the maximum moves by 20
        // and the run keeps holding the old absolute.
        //
        // AND IT IS REACHABLE MID-DESCENT ON PURPOSE. Spend guards on
        // lockedForFight only, while Refund beside it also guards on
        // InDescent -- the asymmetry is deliberate (a placed point is a
        // decision you may make in the field, taking one back is not), which
        // is what makes this a live bug rather than an unreachable one.
        //
        // DRIVEN THROUGH THE SCREEN'S OWN BUTTON rather than through the new
        // wrapper directly: the wrapper existing proves nothing if the dossier
        // does not call it, and "the dossier does not call it" was the bug.
        //
        // The numbers, stated rather than recomputed: 280 with nothing
        // invested, 300 with one point in Constitution (both pinned as fixture
        // checks above), and 140 of 280 carried into the descent. Rescaled:
        // (140 * 300 + 140) / 280 = 42140 / 280 = 150.
        [UnityTest]
        public IEnumerator InvestingAPointKeepsTheCarriedFraction()
        {
            Navigation.LoadOverride = _ => { };

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the hub has no dossier controller");

            var character = SquadFixture.FirstLiveMember();
            character.unspentStatPoints = 1;
            character.investedAbilityScores = default;

            Assert.AreEqual(ShawnsMaxWithNothingInvested,
                Content.ContentDatabase.EffectiveStats(character).maxHealth,
                "fixture: the character on screen is not the one these literals were read off");

            RunManager.StartRun(4242UL);
            RunManager.Run.currentHealth.Add(new RunHealthEntry
            {
                characterId = character.definitionId,
                hp = 140,
            });

            dossier.Refresh();
            yield return null;

            // THE CELL IS NOT THE SCORE -- the six are sorted highest-first per
            // character, so which cell holds Constitution is a fact about
            // Shawn's stat line rather than a constant. Read the labels and
            // press the one that says so.
            int cell = Enumerable.Range(0, 6).First(i =>
                TextNamed(menu.gameObject, $"DossierAttrKey{i}").text.Trim().ToUpperInvariant()
                    .StartsWith("CON"));

            ButtonNamed(menu.gameObject, $"DossierAttrPlus{cell}").onClick.Invoke();
            yield return null;

            character = SquadFixture.FirstLiveMember();
            Assert.AreEqual(1, character.investedAbilityScores[AbilityScore.Constitution],
                "fixture: the plus did not put a point into Constitution");
            Assert.AreEqual(ShawnsMaxWithOnePointInConstitution,
                Content.ContentDatabase.EffectiveStats(character).maxHealth,
                "fixture: the point went in but the maximum did not move");

            Assert.AreEqual(150, RunManager.Run.currentHealth[0].hp,
                "the run kept 140 against a maximum that had just risen to 300, which is the " +
                "permanently-empty tail RunEncounter.ScaleCarriedHealth's header describes");
        }

        private static Button ButtonNamed(GameObject scope, string name)
        {
            var button = scope.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"the dossier has no '{name}'");
            return button;
        }

        private static TMP_Text TextNamed(GameObject scope, string name)
        {
            var label = scope.GetComponentsInChildren<TMP_Text>(includeInactive: true)
                .FirstOrDefault(t => t.name == name);
            Assert.IsNotNull(label, $"the dossier has no '{name}'");
            return label;
        }

        // The other half of finding 6, which cannot be driven today.
        //
        // TalentOps.Kindle now rides the same measure/rescale pair this file's
        // first test pins, but nothing can PROVE it from the outside: a talent
        // moves max health only through TalentDefinition.StatBonus, and not one
        // of the 43 rows in talents.json carries a maxHealth bonus -- all 43
        // author behavioural `effects` alone. So the hole was latent and the fix
        // is latent with it.
        //
        // Ignored rather than deleted, in the shape AUDIT #93 and #97 already
        // use: the day a "+N max health" talent is authored, un-ignore this and
        // it is the test that says the wrapper works. Kindling it at 50 of 100
        // against a new maximum of 120 must leave the run holding 60, not 50.
        [Test]
        [Ignore("No talent in talents.json carries a maxHealth StatBonus, so kindling " +
                "cannot move a maximum today. Un-ignore when one is authored.")]
        public void TalentKindlingKeepsTheCarriedFraction()
        {
            Assert.Fail("needs a talent that grants max health");
        }
    }
}
