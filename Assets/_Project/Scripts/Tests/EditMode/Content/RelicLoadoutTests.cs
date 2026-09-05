using NUnit.Framework;
using PrincesPalace.Domain.Relics;

namespace PrincesPalace.Domain.Tests
{
    public class RelicLoadoutTests
    {
        [Test]
        public void ANewLoadout_HasNoOneCarryingAnything()
        {
            var loadout = new RelicLoadout();

            Assert.IsTrue(loadout.IsEmpty("sheep"));
            Assert.AreEqual("", loadout.Get("sheep"));
            Assert.AreEqual("", loadout.CharacterHolding("dual_wield"));
        }

        [Test]
        public void Set_AssignsTheRelicToTheCharacter()
        {
            var loadout = new RelicLoadout();

            loadout.Set("sheep", "dual_wield");

            Assert.AreEqual("dual_wield", loadout.Get("sheep"));
            Assert.AreEqual("sheep", loadout.CharacterHolding("dual_wield"));
        }

        // THE rule this class exists for: a relic already in use has to be
        // taken away from its previous holder, not duplicated onto a second
        // character.
        [Test]
        public void AssigningARelicAlreadyHeldByAnotherCharacter_TakesItAwayFromThem()
        {
            var loadout = new RelicLoadout();
            loadout.Set("sheep", "dual_wield");

            loadout.Set("owl", "dual_wield");

            Assert.AreEqual("", loadout.Get("sheep"), "Sheep should have lost the relic to Owl");
            Assert.AreEqual("dual_wield", loadout.Get("owl"));
            Assert.AreEqual("owl", loadout.CharacterHolding("dual_wield"));
        }

        // One slot per character today -- assigning a second relic replaces
        // the first rather than somehow holding both.
        [Test]
        public void AssigningASecondRelicToTheSameCharacter_ReplacesTheFirst()
        {
            var loadout = new RelicLoadout();
            loadout.Set("sheep", "dual_wield");

            loadout.Set("sheep", "bloodlust");

            Assert.AreEqual("bloodlust", loadout.Get("sheep"));
            Assert.AreEqual("", loadout.CharacterHolding("dual_wield"), "The old relic should be free again, not still 'held'");
        }

        [Test]
        public void Set_ReturnsWhatThisCharacterWasCarryingBefore()
        {
            var loadout = new RelicLoadout();
            loadout.Set("sheep", "dual_wield");

            string previous = loadout.Set("sheep", "bloodlust");

            Assert.AreEqual("dual_wield", previous);
        }

        [Test]
        public void ReassigningACharactersOwnCurrentRelic_IsAHarmlessNoOp()
        {
            var loadout = new RelicLoadout();
            loadout.Set("sheep", "dual_wield");

            string previous = loadout.Set("sheep", "dual_wield");

            Assert.AreEqual("dual_wield", previous);
            Assert.AreEqual("dual_wield", loadout.Get("sheep"));
            Assert.AreEqual("sheep", loadout.CharacterHolding("dual_wield"));
        }

        [Test]
        public void Clear_FreesTheRelicForAnyoneElse()
        {
            var loadout = new RelicLoadout();
            loadout.Set("sheep", "dual_wield");

            string previous = loadout.Clear("sheep");

            Assert.AreEqual("dual_wield", previous);
            Assert.IsTrue(loadout.IsEmpty("sheep"));
            Assert.AreEqual("", loadout.CharacterHolding("dual_wield"));
        }

        [Test]
        public void RemoveWhere_DropsOnlyTheEntriesTheCallerRejects()
        {
            var loadout = new RelicLoadout();
            loadout.Set("sheep", "dual_wield");
            loadout.Set("owl", "bloodlust");

            loadout.RemoveWhere((characterId, relicId) => characterId == "sheep");

            Assert.IsTrue(loadout.IsEmpty("sheep"));
            Assert.AreEqual("bloodlust", loadout.Get("owl"), "Owl's entry should survive untouched");
        }
    }
}
