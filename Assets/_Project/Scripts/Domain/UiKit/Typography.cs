namespace PrincesPalace.Domain.UiKit
{
    // The typography roles a screen picks from, and what each one means --
    // engine-free, so it is unit-testable without a scene and so a screen
    // tree (also engine-free) can name a role without referencing TMPro.
    //
    // Not wired into any Ui.Label/Ui.Button call yet: this is the vocabulary,
    // not the migration. SceneBuilder.FontFor/MaterialFor (in
    // Editor/SceneBuilder/SceneBuilder.Typography.cs) resolve a role to the
    // generated font/material assets for whichever call site opts in.
    public enum TypographyRole
    {
        // Screen/run titles, ceremonial moments (reward reveals, run-end).
        CeremonialTitle,
        // Section and panel headings.
        FunctionalHeading,
        // Button and interactive-control labels.
        ButtonLabel,
        // Paragraph and descriptive body text.
        Body,
        // Numeric/stat readouts -- damage, HP, currency counters.
        TacticalData,
        // Warnings, danger states, low-HP or failure messaging.
        Alert,
    }

    // One role's authored spec: which generated font/material asset it
    // resolves to, its size range at a 1080p reference canvas, and the
    // presentation rules that apply wherever it's used.
    public readonly struct TypographySpec
    {
        public readonly string FontAssetName;
        public readonly string MaterialName;
        public readonly float MinSize1080p;
        public readonly float MaxSize1080p;
        public readonly bool Uppercase;
        // TMP tracking (character spacing) in TMP's own units. Null means
        // "dynamic" -- ButtonLabel's tracking depends on label length/fit
        // rather than being a fixed authored value, so callers compute it
        // rather than reading a constant here.
        public readonly float? Tracking;
        public readonly float LineSpacing;

        public TypographySpec(
            string fontAssetName, string materialName,
            float minSize1080p, float maxSize1080p,
            bool uppercase, float? tracking, float lineSpacing)
        {
            FontAssetName = fontAssetName;
            MaterialName = materialName;
            MinSize1080p = minSize1080p;
            MaxSize1080p = maxSize1080p;
            Uppercase = uppercase;
            Tracking = tracking;
            LineSpacing = lineSpacing;
        }
    }

    public static class Typography
    {
        // Font asset names match TmpBootstrap.Typography.cs's generated
        // asset names exactly -- both sides of that seam are string literals
        // because Domain cannot reference the Editor-only TMP_FontAsset type,
        // and TypographyRoleTests pins the pairing so a rename on either side
        // is caught rather than silently drifting apart.
        public static readonly System.Collections.Generic.IReadOnlyDictionary<TypographyRole, TypographySpec> Specs =
            new System.Collections.Generic.Dictionary<TypographyRole, TypographySpec>
            {
                [TypographyRole.CeremonialTitle] = new TypographySpec(
                    "Cinzel-SemiBold SDF", "CeremonialTitle",
                    minSize1080p: 52f, maxSize1080p: 64f,
                    uppercase: true, tracking: 2f, lineSpacing: 0f),

                [TypographyRole.FunctionalHeading] = new TypographySpec(
                    "SourceSans3-SemiBold SDF", "FunctionalHeading",
                    minSize1080p: 34f, maxSize1080p: 42f,
                    uppercase: true, tracking: 2f, lineSpacing: 0f),

                [TypographyRole.ButtonLabel] = new TypographySpec(
                    "SourceSans3-Bold SDF", "ButtonLabel",
                    minSize1080p: 26f, maxSize1080p: 30f,
                    uppercase: true, tracking: null, lineSpacing: 0f),

                [TypographyRole.Body] = new TypographySpec(
                    "SourceSans3-Regular SDF", "Body",
                    minSize1080p: 20f, maxSize1080p: 22f,
                    uppercase: false, tracking: 0f, lineSpacing: 5f),

                [TypographyRole.TacticalData] = new TypographySpec(
                    "ChakraPetch-Medium SDF", "TacticalData",
                    minSize1080p: 17f, maxSize1080p: 19f,
                    uppercase: false, tracking: 1f, lineSpacing: 0f),

                [TypographyRole.Alert] = new TypographySpec(
                    "SourceSans3-Bold SDF", "Alert",
                    minSize1080p: 22f, maxSize1080p: 26f,
                    uppercase: false, tracking: 1f, lineSpacing: 0f),
            };
    }
}
