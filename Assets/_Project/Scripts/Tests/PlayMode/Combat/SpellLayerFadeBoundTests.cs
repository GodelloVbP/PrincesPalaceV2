using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // THE ONE CLAIM SpellLayerRules.MaxFadeSeconds MAKES ABOUT THE BEAT, as an
    // assertion rather than as prose in a comment.
    //
    // Domain is engine-free and cannot see FightBeatPlayer's constants, so the
    // ceiling on a layer's ending is a literal there with its derivation
    // written beside it -- which is a restated value, and
    // docs/CODE_STANDARDS.md section 9 says the strongest form available for
    // one of those is a test that fails when the claim stops being true.
    //
    // The claim is a bracket, and both ends carry weight:
    //
    //   LONGER than a beat's hold, because a tail outliving the beat that cast
    //   it is the whole point of removing the beat-end flush. A ceiling below
    //   the hold would make "the tail genuinely outlives the beat" unauthorable.
    //
    //   SHORTER than hold plus gap, because a fade still running when the beat
    //   AFTER the next one opens has stopped being attributable to a cast by
    //   eye -- at which point it reads as a rendering fault rather than as the
    //   end of something.
    //
    // A PlayMode fixture with no scene: it needs Core's constants and nothing
    // else, which is exactly what EditMode cannot reach.
    public class SpellLayerFadeBoundTests
    {
        [Test]
        public void ALayersEndingMayOutliveItsBeatButNotTheBeatAfterTheNextOne()
        {
            Assert.Greater(SpellLayerRules.MaxFadeSeconds, FightBeatPlayer.BeatHoldSeconds,
                "a fade capped at or below the beat hold could never outlive the beat that cast it, which " +
                "is the behaviour the no-flush contract exists to allow");

            Assert.Less(SpellLayerRules.MaxFadeSeconds,
                FightBeatPlayer.BeatHoldSeconds + FightBeatPlayer.BeatGapSeconds,
                "a fade may not still be running when the beat after the next one opens -- past that point " +
                "nobody watching can tell which cast it belonged to");
        }
    }
}
