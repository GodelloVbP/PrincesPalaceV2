namespace PrincesPalace.Domain.UiKit
{
    // The one naming convention that spans Domain and Core: a screen declares
    // `public NodeRef DetailBody;` and the controller that consumes it declares
    // `[SerializeField] internal TMP_Text detailBody;`. Same word, and only the
    // first letter's case differs, because that is all C# style asks for.
    //
    // Stated as a function so the build-time binder (UiAutoBind) and a test can
    // read the SAME rule rather than each carrying a copy of it. Nothing here
    // strips a suffix or scores a near-miss: `exitLabels` does not reach
    // `ExitButtons` and is not supposed to. A fuzzy rule is how a rename ends
    // up silently binding a field to the wrong node, which is the failure
    // NodeRef exists to make impossible.
    public static class UiBindingNames
    {
        // The screen member a controller field claims, or the name unchanged if
        // it does not start with a lower-case letter (which no serialized
        // controller field in this project does).
        public static string ScreenMemberFor(string controllerFieldName)
        {
            if (string.IsNullOrEmpty(controllerFieldName)) return controllerFieldName;

            char first = controllerFieldName[0];
            char upper = char.ToUpperInvariant(first);

            return first == upper ? controllerFieldName : upper + controllerFieldName.Substring(1);
        }
    }
}
