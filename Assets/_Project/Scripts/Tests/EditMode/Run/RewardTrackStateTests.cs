using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // The reward track's four states, out of two integers.
    //
    // Pure arithmetic over `level` and `claimedTrackLevel`, which is why it is
    // pinnable from EditMode at all -- the screen that draws it needs a scene
    // and a save, and none of what decides the colour does.
    public class RewardTrackStateTests
    {
        // ---- the four states ------------------------------------------------

        [Test]
        public void ALevelAboveThePlayerIsStillToCome()
        {
            Assert.AreEqual(TrackNodeState.ToCome, RewardTrack.StateOf(48, 47, 47));
        }

        [Test]
        public void AReachedAndPaidLevelIsCollected()
        {
            Assert.AreEqual(TrackNodeState.Collected, RewardTrack.StateOf(40, 47, 47));
        }

        [Test]
        public void AReachedAndUnpaidLevelIsWaiting()
        {
            Assert.AreEqual(TrackNodeState.Waiting, RewardTrack.StateOf(40, 47, 12));
        }

        // THE ORDERING DECISION, and the reason it is a test rather than a
        // comment. The player's own node is the one thing on a hundred-node
        // rail they have to find at a glance, so it draws pale even when its
        // own reward is uncollected.
        [Test]
        public void ThePlayersOwnNodeWinsOverWaiting()
        {
            Assert.AreEqual(TrackNodeState.Here, RewardTrack.StateOf(47, 47, 12));
            Assert.AreEqual(TrackNodeState.Here, RewardTrack.StateOf(47, 47, 47));
        }

        // And nothing is hidden by that, because the pulse and the claim ask a
        // different question. A node that answered Here and was therefore
        // treated as not-waiting would be exactly the node most likely to be
        // holding an uncollected reward.
        [Test]
        public void ThePlayersOwnNodeCanStillBeWaiting()
        {
            Assert.IsTrue(RewardTrack.IsWaiting(47, 47, 12));
            Assert.IsFalse(RewardTrack.IsWaiting(47, 47, 47));
        }

        [Test]
        public void NothingAboveThePlayerIsEverWaiting()
        {
            Assert.IsFalse(RewardTrack.IsWaiting(48, 47, 12));
        }

        // Level 1 is where a character starts, not somewhere they arrive, so it
        // pays nothing and can never be owed.
        [Test]
        public void TheStartingLevelIsNeverWaiting()
        {
            Assert.IsFalse(RewardTrack.IsWaiting(1, 47, 0));
        }

        // ---- how many are owed ------------------------------------------------

        [Test]
        public void EveryLevelBetweenTheWatermarkAndTheLevelIsOwed()
        {
            // 2 through 30 inclusive: twenty-nine of them.
            Assert.AreEqual(29, RewardTrack.UnclaimedCount(30, 1));
            Assert.AreEqual(0, RewardTrack.UnclaimedCount(30, 30));
        }

        // A ZERO WATERMARK OWES THE SAME AS A ONE, which is not obvious and is
        // the reason the count clamps rather than subtracting.
        //
        // Zero is what a character migrated from before the track existed
        // carries, and it is tempting to read as "thirty levels owed". It is
        // twenty-nine: level 1 is where a character starts rather than
        // somewhere they arrive, so it pays nothing and can never be owed. A
        // collect button reading "COLLECT 30 REWARDS" and then handing over
        // twenty-nine would be the visible half of that mistake.
        [Test]
        public void AZeroWatermarkOwesNoMoreThanAOne()
        {
            Assert.AreEqual(RewardTrack.UnclaimedCount(30, 1), RewardTrack.UnclaimedCount(30, 0));
            Assert.AreEqual(29, RewardTrack.UnclaimedCount(30, 0));
        }

        // A save can hold anything, including a watermark past the level. "You
        // are owed nothing" is the right answer -- a negative count would
        // pluralise a collect button backwards and, worse, would make
        // level > claimed read as true somewhere downstream.
        [Test]
        public void AWatermarkAheadOfTheLevelOwesNothingRatherThanANegative()
        {
            Assert.AreEqual(0, RewardTrack.UnclaimedCount(10, 40));
        }

        [Test]
        public void TheCountIsCappedAtTheEndOfTheTrack()
        {
            // 39: levels 2 through 40, the whole track, however absurd the
            // level on the save is.
            Assert.AreEqual(39, RewardTrack.UnclaimedCount(400, 1));
        }

        // ---- the whole rail agrees with itself ----------------------------------

        // Every node is in exactly one state, and Waiting is exactly the run
        // between the watermark and the level. A hole in that run would mean
        // claimedTrackLevel had stopped being a single integer, which is the
        // property the whole sequential-claim design rests on.
        [Test]
        public void WaitingIsOneUnbrokenRunAboveTheWatermark()
        {
            const int Level = 31;
            const int Claimed = 12;

            int waiting = 0;
            bool ended = false;

            for (int level = 2; level <= RewardTrack.MaxLevel; level++)
            {
                bool isWaiting = RewardTrack.IsWaiting(level, Level, Claimed);

                if (isWaiting)
                {
                    Assert.IsFalse(ended, $"level {level} is waiting after the run had already ended");
                    waiting++;
                }
                else if (waiting > 0)
                {
                    ended = true;
                }
            }

            Assert.AreEqual(19, waiting, "13 through 31 is nineteen levels");
        }
    }
}
