namespace PrincesPalace.Domain.UiKit
{
    // Cross-axis alignment inside a flow container: in a Column this is
    // horizontal, in a Row it is vertical.
    //
    // Start/End are named for the axis direction rather than Left/Top so one
    // enum serves both container kinds. In canvas space (+y UP) a Column's
    // Start is its LEFT edge and a Row's Start is its BOTTOM edge.
    public enum UiAlign
    {
        Start,
        Centre,
        End,
    }
}
