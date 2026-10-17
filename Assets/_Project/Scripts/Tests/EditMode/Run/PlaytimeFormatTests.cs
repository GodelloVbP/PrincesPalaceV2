using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // "3h 12m" on a slot card, and every boundary a hand-checked string is
    // likely to be one digit wrong at.
    public class PlaytimeFormatTests
    {
        // Rows read: seconds, expected text.
        [TestCase(0f, "<1m")]
        [TestCase(59f, "<1m")]
        [TestCase(60f, "1m")]
        [TestCase(41f * 60f, "41m")]
        [TestCase(59f * 60f + 59f, "59m")]
        // THE BOUNDARY. 59:59.9 must not round up into "1h 0m" -- this is
        // floored, not rounded, so a slot never claims an hour it has not
        // actually reached.
        [TestCase(59f * 60f + 59.9f, "59m")]
        [TestCase(3600f, "1h 0m")]
        [TestCase(3f * 3600f + 12f * 60f, "3h 12m")]
        // Minutes never overflow into a bigger number than 59 -- the hour
        // carries instead, the same way a clock does.
        [TestCase(2f * 3600f, "2h 0m")]
        // Never a real input, but a corrupted or hand-edited save is not
        // this method's job to validate -- it degrades rather than throws.
        [TestCase(-5f, "<1m")]
        public void DescribeReadsEveryBoundary(float seconds, string expected)
        {
            Assert.AreEqual(expected, PlaytimeFormat.Describe(seconds));
        }
    }
}
