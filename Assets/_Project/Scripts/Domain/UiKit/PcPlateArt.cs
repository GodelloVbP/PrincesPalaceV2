namespace PrincesPalace.Domain.UiKit
{
    // THE MEASURED SHAPE OF THE PER-PC FIGHT-HUD PLATE, and only the shape.
    //
    // WHICH character wears WHICH plate is CONTENT, not code: characters.json
    // carries a `plateArt` per row (Resources-relative, no extension --
    // ArtPathConvention.RuntimeLoaded), the resolver refuses a row without
    // one, and FightController loads it at runtime through PcPlateSprites. So
    // a fourth playable character costs a PNG and a line of JSON, with no
    // scene rebuild and nothing added here. This file answers the other
    // question -- "how big is a plate and where may text go on it" -- which
    // is a fact about the ART and identical for every character precisely
    // because tools/normalize_pc_plates.py makes it so.
    //
    // EVERY NUMBER BELOW IS PASTED FROM `py tools/normalize_pc_plates.py`,
    // which prints a C#-pasteable block, and re-measured independently by
    // UiKitVisiblePadTests / PcPlateArtTests against the committed PNGs. The
    // same measured-vs-literal arrangement ContainerArt/ButtonPlateArt use,
    // for the same reason: a literal typed by hand is the thing that drifts.
    public static class PcPlateArt
    {
        // width / height of the shared 2048x365 canvas the three plates are
        // normalised onto. Shawn's own delivered aspect, which is the
        // smallest of the three -- see the tool's header for why the target
        // has to be the smallest and why the other two lose WIDTH rather than
        // gaining height.
        public const float Aspect = 5.6111f;

        // ZERO, on all four edges, and that is a property worth having.
        // The six-theme kit's PNGs carry a transparent halo left over from
        // sheet-splicing, so a screen flushing a kit container's bottom edge
        // against a neighbour has to correct for paint that stops short of
        // the rect (ContainerArt.VisiblePad's whole reason for existing).
        // These plates are cropped to their own alpha bounding box, so rect
        // bottom IS painted bottom and Ui.CentreYForVisibleBottom's pad
        // argument is 0. Measured: every edge of every plate at 0px except
        // bear's left at 1px, which is the LANCZOS resample's own edge ramp.
        public static ContentInsetFrac VisiblePad =>
            new ContentInsetFrac(left: 0f, right: 0f, top: 0f, bottom: 0f);

        // THE SAFE INTERIOR: inside the lighter rim stroke and clear of the
        // rounded corner arc. Worst per side across the three plates, as a
        // fraction of the canvas -- L 60px, T 10px, R 60px, B 11px of
        // 2048x365. Sheep carries the thickest top/bottom rim (10/11 against
        // bear and owl's 2/3); bear and owl the widest corner arc.
        public static ContentInsetFrac Inset =>
            new ContentInsetFrac(left: 0.0293f, right: 0.0293f, top: 0.0274f, bottom: 0.0301f);

        // HOW MUCH OF THE RIGHT END THE EMBOSSED HEAD OWNS, as a fraction of
        // the plate's width. No label, meter or badge may enter it -- a name
        // running under a bear's muzzle is the one way this layout can look
        // broken while every rect in it is legal.
        //
        // The widest of the three by measurement: sheep 0.1904 (the ears),
        // bear 0.1816, owl 0.1431. Taken as the max rather than per-character
        // because the three plates are ONE layout -- a content box that
        // changed width with the occupant would move every row on the card
        // the moment the party order changed.
        public const float HeadZoneFrac = 0.1904f;

        // The acting-highlight decal (tools/normalize_pc_plates.py writes it
        // beside the plates): the reference silhouette's edge, crisp at the
        // outline and falling off outward into a halo, white so an Image tint
        // can paint it any PC colour.
        //
        // WHY A DECAL AND NOT FOUR RIM SOLIDS. The plate's corner radius
        // measures ~14% of its height -- an 11px arc at the HUD's 452x81 --
        // and a rectangle of four 1px Solids cannot follow it: each corner
        // would stick a square spur out of a rounded plate. Ui.Rim is right
        // for a flat OutlineBox card and wrong for a shaped one.
        public const string GlowKey = "Assets/_Project/Resources/Plates/pc_plate_glow.png";

        // How far the halo reaches past the plate, as a fraction of the
        // plate's HEIGHT -- the same 0.10 the tool padded the decal's canvas
        // by, so the decal is drawn at exactly the size it was authored for
        // and the halo is neither squashed nor cropped.
        public const float GlowPadFrac = 0.10f;

        // WHAT AN UNREFRESHED SCENE SHOWS, in stack order (bottom plate
        // first). The controller overwrites all three from content on the
        // first repaint; these exist so a freshly built scene, and every
        // Edit-Mode screenshot of one, reads as the roster that actually
        // ships rather than as three copies of one animal.
        //
        // The one content assumption in this file, stated rather than
        // implied: Shawn, Bjorn and Odette in squadSlot order. A fourth
        // character does not need a line here -- nothing reads this array at
        // runtime.
        public static readonly string[] BakedDefaults =
        {
            "Assets/_Project/Resources/Plates/pc_sheep.png",
            "Assets/_Project/Resources/Plates/pc_bear.png",
            "Assets/_Project/Resources/Plates/pc_owl.png",
        };

        // The Resources-relative path a `plateArt` value looks like, for the
        // resolver's error message and for the tests that pin the three
        // shipped rows. Not used to BUILD a path -- content owns that.
        public const string ResourceRoot = "Plates/";

        // THE ENEMY PLATE'S OWN HEAD ZONE -- same idea as HeadZoneFrac above
        // (isolate the head so a small badge reads as a face rather than a
        // smear), a DIFFERENT axis because the source art is a different
        // shape. A PC plate is a wide banner with the head embossed into its
        // right end, pre-authored that way by normalize_pc_plates.py, so
        // HeadZoneFrac reserves a fraction of WIDTH and nothing at runtime
        // has to crop anything. An enemy's idle stance is a tall, standing
        // full-body pose with no such banner behind it -- StanceSpriteFor
        // hands back whatever the content pipeline authored, unnormalised --
        // so the icon has to crop the TOP fraction of the sprite's HEIGHT
        // instead, at paint time (FightController.Hud.RefreshEnemyPlates),
        // to get the same "mostly head" reading out of a 34x34 badge that
        // the party column gets for free from its art.
        //
        // NOT MEASURED like HeadZoneFrac is: there is no per-enemy plate
        // asset to run a measuring tool against, only the same idle sprite
        // the stage already draws at full size. 0.35 is a judgement call --
        // enough of a standing figure's top to carry head, ears/horns and
        // some shoulder for a creature whose head alone would be a handful
        // of pixels, not so much that the icon reads as a body again. Every
        // enemy shares one constant for the reason PcPlateArt.HeadZoneFrac
        // takes the WORST case across three PCs rather than one per
        // character: a badge that changed crop with its occupant would be a
        // second rule for the player to learn per monster.
        //
        // THE FALLBACK SINCE 2026-09-26, and measured on the FIGURE, not the
        // canvas: the fraction of the idle still's OPAQUE height a square
        // icon crop takes from the top of the figure (Domain.Stage.
        // EnemyIconCrop). An actor whose manifest entry authors a `head` box
        // uses that instead -- the beetle's head is out front, not on top.
        public const float EnemyIconHeadZoneFrac = 0.35f;
    }
}
