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

        // WHAT A SAVE IS WORTH ONCE IT IS WRITTEN, in the two backends that
        // are supposed to answer identically.
        //
        // SaveSystem's own header states the contract: "a caller cannot alias
        // the object it saved and no code can come to depend on in-memory mode
        // handing back a live reference." Load holds up its end -- it hands
        // back a JsonUtility round-trip in both modes. Save does not. The
        // in-memory branch is `Memory[slot] = data`, which stores the caller's
        // live object, so the store IS the caller's object and every later
        // mutation of it is retroactively saved.
        //
        // The disk pin below passes and is the reference behaviour. The
        // in-memory one asserts the same thing and is parked, because closing
        // the gap costs the serialize the mode exists to skip -- 28 writes a
        // run, which is the number BotPhaseTimers was added to measure.

        [Test]
        public void OnDisk_AMutationAfterTheSaveIsNotInTheSave()
        {
            var data = SaveData.CreateNew();
            data.Gold = 100;
            SaveSystem.Save(data, 0);

            // No second Save. On disk this cannot reach the file, because the
            // file was serialized at the moment of the write.
            //
            // Gold, not `currency`. The legacy field is a migration source that
            // Reconcile folds into wallet.gold and then zeroes, so a test that
            // wrote it would read 0 back in both backends and prove nothing.
            data.Gold = 999;

            Assert.AreEqual(100, SaveSystem.Load(0).Gold,
                "the disk backend re-read a value that was never written to it.");
        }

        [Test]
        [Ignore("owner's call: in-memory Save stores the caller's live SaveData rather than a copy, so an " +
                "unsaved mutation persists in memory and not on disk. Copying on write would restore parity " +
                "at the cost of the JsonUtility.ToJson the mode exists to skip, on the write path a bot run " +
                "takes 28 times. Un-ignore after deciding; the disk half above already passes.")]
        public void InMemory_AMutationAfterTheSaveIsNotInTheSaveEither()
        {
            SaveSystem.InMemory = true;
            try
            {
                SaveSystem.ClearMemory();

                var data = SaveData.CreateNew();
                data.Gold = 100;
                SaveSystem.Save(data, 0);

                data.Gold = 999;

                Assert.AreEqual(100, SaveSystem.Load(0).Gold,
                    "in-memory Save aliased the caller's object, so a mutation that was never saved came " +
                    "back out of the store. A bot run measuring this game is measuring a different one.");
            }
            finally
            {
                SaveSystem.ClearMemory();
                SaveSystem.InMemory = false;
            }
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
