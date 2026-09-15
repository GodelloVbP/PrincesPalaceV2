using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // WHICH TITLE A CHARACTER WEARS, and that the choice survives a save.
    //
    // Progression v2 §4: "Titles share one display slot: the newest is shown,
    // earlier ones selectable in the hub roster". Phase 5 built the roster's
    // picker (PartyController.CycleTitle); this pins the model under it -- the
    // default, the choice, the round-trip, and the one way the stored figure
    // can go stale.
    //
    // THE TITLES ARE READ OUT OF CONTENT, never typed as literals. Which levels
    // carry a Title is reward_tracks.json's business and RewardTrackContentPin
    // Tests already pins it level by level; a hardcoded 39 here would fail for
    // an unrelated reauthoring and would prove nothing about selection.
    //
    // PlayMode, not EditMode: Character, SaveData and CharacterIdentity are all
    // Core types and the EditMode assembly references only Domain.
    public class TitleSelectionTests
    {
        // A roster character with the whole track collected, which is the only
        // state in which there is more than one title to choose between.
        private static Character FullyCollected()
        {
            var save = SaveData.CreateNew();
            var character = save.roster[0];

            character.level = RewardTrack.MaxLevel;
            character.claimedTrackLevel = RewardTrack.MaxLevel;

            return character;
        }

        private static CharacterIdentityItem[] TitlesOf(Character character) =>
            CharacterIdentity.CollectedFor(character)
                .Where(item => item.Kind == TrackIdentityKind.Title)
                .ToArray();

        [Test]
        public void AFullyCollectedTrackCarriesMoreThanOneTitle()
        {
            var titles = TitlesOf(FullyCollected());

            Assert.Greater(titles.Length, 1,
                "fixture: the shipped track no longer authors two or more Title nodes, so there is " +
                "nothing for a picker to pick between and every test below is vacuous");
        }

        // THE DEFAULT IS THE NEWEST, which is what §4 promises and what a
        // player who never opens the picker sees.
        [Test]
        public void NeverChoosingShowsTheNewestTitle()
        {
            var character = FullyCollected();
            var titles = TitlesOf(character);

            Assert.AreEqual(0, character.selectedTitleTrackLevel, "fixture: a fresh save has no choice stored");

            var shown = CharacterIdentity.SelectedTitleFor(character);
            Assert.IsNotNull(shown);
            Assert.AreEqual(titles.Last().Level, shown.Value.Level,
                "the default is not the highest-level Title collected");
        }

        [Test]
        public void ChoosingAnOlderTitleShowsThatOneInstead()
        {
            var character = FullyCollected();
            var oldest = TitlesOf(character).First();

            character.SelectTitle(oldest.Level);

            var shown = CharacterIdentity.SelectedTitleFor(character);
            Assert.IsNotNull(shown);
            Assert.AreEqual(oldest.Level, shown.Value.Level);
            Assert.AreEqual(oldest.Value, shown.Value.Value);
        }

        // THE ROUND TRIP, through the same JsonUtility every save file goes
        // through. selectedTitleTrackLevel was added without a version bump on
        // the grounds that it is purely additive and zero already reads as
        // "never chosen" (Character's own header) -- which is only true if the
        // field actually serialises, and nothing asserted that it did.
        [Test]
        public void TheChoiceSurvivesASaveAndALoad()
        {
            var save = SaveData.CreateNew();
            var character = save.roster[0];
            character.level = RewardTrack.MaxLevel;
            character.claimedTrackLevel = RewardTrack.MaxLevel;

            var chosen = TitlesOf(character).First();
            character.SelectTitle(chosen.Level);

            var reloaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            var after = reloaded.roster[0];

            Assert.AreEqual(chosen.Level, after.selectedTitleTrackLevel,
                "the stored choice did not survive the round trip");

            var shown = CharacterIdentity.SelectedTitleFor(after);
            Assert.IsNotNull(shown);
            Assert.AreEqual(chosen.Value, shown.Value.Value);
        }

        // A STORED CHOICE THAT NO LONGER NAMES A COLLECTED TITLE falls back to
        // the newest rather than showing nothing.
        //
        // Reachable two ways that are not hypothetical: a version-6 reset puts
        // claimedTrackLevel back to 0 while leaving this field alone (it is not
        // part of the ladder), and a reauthored track can move a Title off the
        // level a save recorded. Either way the answer has to be a title the
        // character actually has.
        [Test]
        public void AChoiceThatIsNoLongerCollectedFallsBackToTheNewest()
        {
            var character = FullyCollected();
            var titles = TitlesOf(character);

            character.SelectTitle(titles.First().Level);

            // Roll the watermark back past the chosen one, which is what a
            // reset does. Everything above the first Title is uncollected now.
            character.claimedTrackLevel = titles.First().Level - 1;

            Assert.IsEmpty(TitlesOf(character), "fixture: rolling the watermark back left titles collected");
            Assert.IsNull(CharacterIdentity.SelectedTitleFor(character),
                "a character with nothing collected showed a title anyway");

            // And with SOME collected but not the chosen one, the newest of
            // what is left wins rather than the stale figure being honoured.
            character.claimedTrackLevel = titles[titles.Length - 2].Level;
            character.SelectTitle(titles.Last().Level);

            var shown = CharacterIdentity.SelectedTitleFor(character);
            Assert.IsNotNull(shown);
            Assert.AreEqual(titles[titles.Length - 2].Level, shown.Value.Level,
                "a stored choice above the watermark was honoured rather than falling back");
        }
    }
}
