namespace PrincesPalace.Domain.UiKit
{
    // Where a label's text sits inside the rect it was given.
    //
    // EVERY LABEL IN THIS PROJECT WAS CENTRED, in both axes, because the
    // emitter wrote TextAlignmentOptions.Center and nothing could say
    // otherwise. That is the right default and it was the only option, which
    // are different claims -- and the reward track is where the difference
    // became a layout the tree could not express: its focus card sets a kicker
    // hard left against a rule and a level numeral hard right on the same line,
    // and its node captions hang from a fixed BOTTOM edge so a four-line reward
    // name grows upward, away from the disc, instead of centring itself over it
    // and closing the gap.
    //
    // Centring both of those is not a smaller version of the design, it is a
    // different one: a centred kicker floats in the middle of its column, and a
    // centred caption block moves its last line whenever the reward's name
    // wraps.
    //
    // NOT A FULL CROSS PRODUCT of the nine TMP alignments. These are the five
    // that a screen has asked for; the tenth is cheap to add and nobody should
    // add it speculatively. Note it is a separate enum from UiAlign, which is
    // the cross-axis alignment of CHILDREN inside a flow container -- one is
    // about boxes, this is about glyphs, and one enum serving both would read
    // as though a Column could align its text.
    public enum UiTextAlign
    {
        Centre,
        Left,
        Right,
        Bottom,
        BottomLeft,
    }
}
