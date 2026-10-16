using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's cues (docs/PLAN_REWARD_SCREENS.md C11): checked by CLIP,
    // not by enum, so a cue pointing at the button click or the refusal buzz
    // fails here even though its name is its own.
    public class ReckoningSoundTests
    {
        private static readonly Sound[] Reckoning =
        {
            Sound.CardDeal, Sound.CacheOpen, Sound.CardFlip, Sound.LegendaryBell, Sound.CoinTick,
            Sound.PotionLand, Sound.BookLand, Sound.Take, Sound.HoneScrape, Sound.EmberSnuff, Sound.XpHum,
            Sound.LevelBell, Sound.Fell, Sound.Endured, Sound.EmberLight, Sound.ChevronNote, Sound.BossPeak,
            Sound.RevealRiser, Sound.RevealRing, Sound.RevealImpact, Sound.RevealRumble,
        };

        [Test]
        public void NoReckoningCue_BorrowsTheButtonClickOrTheRefusalBuzz()
        {
            Assert.AreEqual(21, Reckoning.Length, "vacuity guard: the cue list");
            foreach (var cue in Reckoning)
            {
                string path = SoundLibrary.PathOf(cue);
                Assert.IsNotNull(path, cue + " has no clip");
                Assert.AreNotEqual("Audio/Sfx/button click", path, cue + " is the button click");
                Assert.AreNotEqual("Audio/Sfx/click_fail", path, cue + " is the refusal buzz");
            }
        }

        [Test]
        public void EveryReckoningCue_IsItsOwnAudibleClip()
        {
            var paths = Reckoning.Select(SoundLibrary.PathOf).ToList();
            CollectionAssert.AllItemsAreUnique(paths, "two moments share one clip");
            foreach (var cue in Reckoning)
            {
                var clip = Resources.Load<AudioClip>(SoundLibrary.PathOf(cue));
                Assert.IsNotNull(clip, cue + " has no clip on disk");
                Assert.Greater(clip.length, 0.1f, cue + " is silent or empty");
            }
        }

        // ---- the board's and the outcome's cues fire once (C11, R5) -------------------
        //
        // Each moment R5 wired, played through the real controller in the Fight
        // scene and counted through SoundController.Observer.

        private string _root;
        private ReckoningController _reckoning;
        private readonly List<Sound> _heard = new List<Sound>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reck-sound-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
            RunManager.StartRun(4242);
            ReckoningController.SpeedMultiplier = 40f;
            _heard.Clear();
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            ReckoningController.SpeedMultiplier = 1f;
            SoundController.Observer = null;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator Play(ReckoningRequest request)
        {
            yield return SharedScene.Ensure("Fight");
            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            _reckoning.gameObject.SetActive(false);

            SoundController.Observer = _heard.Add;
            _reckoning.Show(request);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_reckoning.BoardSettled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_reckoning.BoardSettled, "the board never settled");
        }

        private int Heard(Sound sound) => _heard.Count(s => s == sound);

        [UnityTest]
        public IEnumerator TheBoard_HumsOnce_RingsOnceForOneLevel_AndEachHaulItemLandsOnce()
        {
            var reward = new CombatReward { GoldGained = 10 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 2, 20, 3, 50, 100, 130, expToNextBefore: 100));
            reward.Characters.Add(new CharacterReward("owl", "Odette", 2, 20, 2, 50, 100, 30));
            reward.Prizes.Add(new PaidPrize(PrizeKind.Consumable, PrizeKind.Consumable, 1, new[] { "test_potion" }));
            reward.Prizes.Add(new PaidPrize(PrizeKind.Book, PrizeKind.Book, 0, new[] { "test_book" }));
            yield return Play(ReckoningRequest.Full(reward, new List<PendingPage>()));

            Assert.AreEqual(1, Heard(Sound.XpHum), "one hum for the board's fills");
            Assert.AreEqual(1, Heard(Sound.LevelBell), "one bell for the one level crossed");
            Assert.AreEqual(1, Heard(Sound.PotionLand), "the one potion lands once");
            Assert.AreEqual(1, Heard(Sound.BookLand), "the one book lands once");
            Assert.AreEqual(0, Heard(Sound.Fell) + Heard(Sound.Endured), "a paid fight has no outcome note");
        }

        [UnityTest]
        public IEnumerator Fell_ItsLowNoteFiresOnce() => TheOutcomeNoteFiresOnce(FightEnding.Fell, Sound.Fell);

        [UnityTest]
        public IEnumerator Endured_ItsDroneFiresOnce() => TheOutcomeNoteFiresOnce(FightEnding.Endured, Sound.Endured);

        private IEnumerator TheOutcomeNoteFiresOnce(FightEnding ending, Sound note)
        {
            var tally = new List<TallyMember> { new TallyMember("sheep", "Shawn") };
            yield return Play(ReckoningRequest.Outcome(ending, new List<string>(), new List<string>(), tally,
                new CombatLedger()));

            Assert.AreEqual(1, Heard(note));
            Assert.AreEqual(0, Heard(note == Sound.Fell ? Sound.Endured : Sound.Fell));
            Assert.AreEqual(0, Heard(Sound.XpHum), "the outcome form has no rings to fill");
            Assert.AreEqual(0, Heard(Sound.PotionLand) + Heard(Sound.BookLand), "and no haul");
        }

        [UnityTest]
        public IEnumerator APaidNothingVictory_SoundsNoNote()
        {
            var tally = new List<TallyMember> { new TallyMember("sheep", "Shawn") };
            yield return Play(ReckoningRequest.Outcome(FightEnding.Victory, new List<string>(), new List<string>(), tally,
                new CombatLedger()));

            Assert.AreEqual(0, Heard(Sound.Fell) + Heard(Sound.Endured));
        }

        // Cut by tools/make_reckoning_cues.py; only CacheOpen reuses a shipped clip.
        [Test]
        public void ReckoningCues_LiveInTheirOwnFolder_ExceptCacheOpen()
        {
            foreach (var cue in Reckoning.Where(c => c != Sound.CacheOpen))
                StringAssert.StartsWith("Audio/Sfx/Reckoning/", SoundLibrary.PathOf(cue), cue.ToString());
            Assert.AreEqual("Audio/Sfx/open_the_pack", SoundLibrary.PathOf(Sound.CacheOpen));
        }
    }
}
