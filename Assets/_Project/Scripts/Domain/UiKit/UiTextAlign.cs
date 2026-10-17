namespace PrincesPalace.Domain.UiKit
{
    // Where a label's text sits inside the rect it was given.
    //
    // Centre is the default, in both axes. The reward track needs more: its
    // focus card sets a kicker hard left against a rule and a level numeral
    // hard right on the same line, and its node captions hang from a fixed
    // BOTTOM edge so a four-line reward name grows upward, away from the
    // disc, instead of centring itself over it and closing the gap.
    //
    // Centring those would be a different design, not a smaller one: a
    // centred kicker floats in the middle of its column, and a centred
    // caption block moves its last line whenever the reward's name wraps.
    //
    // NOT A FULL CROSS PRODUCT of the nine TMP alignments. These are the six
    // that a screen has asked for; the rest are cheap to add and nobody should
    // add one speculatively. TopLeft serves the fight's detail column, 500px
    // tall: a description centred in a box that tall floats in the middle of
    // it and moves every time the text wraps to another line.
    //
    // It is a separate enum from UiAlign, which is the cross-axis alignment
    // of CHILDREN inside a flow container -- one is
    // about boxes, this is about glyphs, and one enum serving both would read
    // as though a Column could align its text.
    public enum UiTextAlign
    {
        Centre,
        Left,
        Right,
        Bottom,
        BottomLeft,
        TopLeft,
    }
}
