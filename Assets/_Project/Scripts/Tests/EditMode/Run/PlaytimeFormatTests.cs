using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // "3h 12m" on a slot card, and every boundary a hand-checked string is
    // likely to be one digit wrong at.
    public class PlaytimeFormatTests
    {
        [Test]
        public void UnderAMinute_ReadsAsLessThanOne()
        {
            Assert.AreEqual("<1m", PlaytimeFormat.Describe(0f));
            Assert.AreEqual("<1m", PlaytimeFormat.Describe(59f));
        }

        [Test]
        public void ExactlyAMinute_IsOneMinute()
        {
            Assert.AreEqual("1m", PlaytimeFormat.Describe(60f));
        }

        [Test]
        public void UnderAnHour_ShowsMinutesOnly()
        {
            Assert.AreEqual("41m", PlaytimeFormat.Describe(41f * 60f));
            Assert.AreEqual("59m", PlaytimeFormat.Describe(59f * 60f + 59f));
        }

        // THE BOUNDARY. 59:59.9 must not round up into "1h 0m" -- this is
        // floored, not rounded, so a slot never claims an hour it has not
        // actually reached.
        [Test]
        public void FiftyNineMinutesFiftyNineSeconds_IsStillUnderAnHour()
        {
            Assert.AreEqual("59m", PlaytimeFormat.Describe(59f * 60f + 59.9f));
        }

        [Test]
        public void AnHourExactly_ShowsZeroMinutesRatherThanDroppingTheHour()
        {
            Assert.AreEqual("1h 0m", PlaytimeFormat.Describe(3600f));
        }

        [Test]
        public void HoursAndMinutesBothShow()
        {
            Assert.AreEqual("3h 12m", PlaytimeFormat.Describe(3f * 3600f + 12f * 60f));
        }

        // Minutes never overflow into a bigger number than 59 -- the hour
        // carries instead, the same way a clock does.
        [Test]
        public void MinutesRemainderNeverReachesSixty()
        {
            Assert.AreEqual("2h 0m", PlaytimeFormat.Describe(2f * 3600f));
        }

        [Test]
        public void NegativeSeconds_ClampsToZeroRatherThanThrowing()
        {
            // Never a real input, but a corrupted or hand-edited save is not
            // this method's job to validate -- it degrades rather than throws.
            Assert.AreEqual("<1m", PlaytimeFormat.Describe(-5f));
        }
    }
}
