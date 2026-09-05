using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // MostRecentSlot, which is what lets Continue exist at all.
    //
    // PLAIN [Test], not [UnityTest] -- this is file I/O with no scene and no
    // frame to wait a tick for, and it lives in the PlayMode assembly only
    // because that is the one with visibility onto Core (SaveSystem, SaveData):
    // Domain.Tests deliberately cannot see Core at all, the same boundary
    // EquipmentLoadoutTests' own header explains for the opposite direction.
    //
    // File-backed, like SaveSlotFlowTests, and for the same reason: this is a
    // question about mtimes on disk, and a throwaway root is the only way to
    // ask it without touching the player's real saves.
    public class SaveSystemTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-save-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void NoSlotsAtAll_HasNoMostRecentSlot()
        {
            Assert.AreEqual(-1, SaveSystem.MostRecentSlot());
        }

        [Test]
        public void OneSlot_IsItsOwnMostRecent()
        {
            SaveSystem.Save(SaveData.CreateNew(), 2);
            Assert.AreEqual(2, SaveSystem.MostRecentSlot());
        }

        // THE WHOLE POINT: the slot written LAST wins, not the lowest index and
        // not the order slots happen to be checked in.
        [Test]
        public void TheLatestWriteWins_RegardlessOfSlotIndex()
        {
            SaveSystem.Save(SaveData.CreateNew(), 3);

            // File.GetLastWriteTimeUtc's resolution is coarser than a tight
            // loop on some filesystems -- without a real gap, two writes in the
            // same tick could tie and this would pass by accident.
            Thread.Sleep(20);

            SaveSystem.Save(SaveData.CreateNew(), 0);

            Assert.AreEqual(0, SaveSystem.MostRecentSlot(),
                "slot 0 was written after slot 3 and should be the one Continue offers");
        }

        // Re-saving an EXISTING slot moves it back to the front, which is what
        // makes this the right signal for "what was I just playing" rather than
        // "what did I create first".
        [Test]
        public void ResavingAnOlderSlot_MakesItTheMostRecentAgain()
        {
            SaveSystem.Save(SaveData.CreateNew(), 1);
            Thread.Sleep(20);
            SaveSystem.Save(SaveData.CreateNew(), 4);
            Thread.Sleep(20);

            SaveSystem.Save(SaveData.CreateNew(), 1);

            Assert.AreEqual(1, SaveSystem.MostRecentSlot());
        }

        [Test]
        public void ADeletedSlot_IsNeverOfferedAsTheMostRecent()
        {
            SaveSystem.Save(SaveData.CreateNew(), 0);
            Thread.Sleep(20);
            SaveSystem.Save(SaveData.CreateNew(), 1);

            SaveSystem.DeleteSlot(1);

            Assert.AreEqual(0, SaveSystem.MostRecentSlot(),
                "the deleted slot was the most recent by mtime, but it no longer exists");
        }
    }
}
