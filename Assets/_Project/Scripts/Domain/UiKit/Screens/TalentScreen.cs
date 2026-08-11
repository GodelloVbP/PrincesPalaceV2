using System.Collections.Generic;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The constellations: three skies per character, one orb per talent.
    //
    // Every position comes from ConstellationLayout and TalentSkeleton, which
    // are both already tested -- so this file declares STRUCTURE and nothing
    // else. The three pages are laid out side by side a full screen apart and
    // slid between, which is what makes them read as three PLACES rather than
    // three tabs.
    public sealed class TalentScreen
    {
        public const string BackgroundKey = "Assets/_Project/Art/Backgrounds/Divine_principality_nebula.png";

        // One orb per slot per path. Declared rather than pooled: the skeleton
        // is a fixed shape, so every position is known at build time and the
        // audit can check all of them.
        public const int OrbCount = TalentSkeleton.SlotCount;

        public UiNode Root;

        public NodeRef Sky;
        public NodeRef CharacterName;
        public NodeRef EmberCount;
        public NodeRef PathName;
        public NodeRef PrevPathButton;
        public NodeRef NextPathButton;
        public NodeRef PrevCharacterButton;
        public NodeRef NextCharacterButton;
        public NodeRef BackButton;

        public NodeRef DetailPlate;
        public NodeRef DetailName;
        public NodeRef DetailBody;
        public NodeRef InvestButton;
        public NodeRef InvestLabel;

        // Indexed [path * OrbCount + slot]. One flat list rather than a list of
        // lists, because the wiring binds arrays and E4 counts them.
        public List<NodeRef> Orbs = new List<NodeRef>();
        public List<NodeRef> OrbGlows = new List<NodeRef>();
        public List<NodeRef> Edges = new List<NodeRef>();

        public static int OrbIndex(int path, int slot) => path * OrbCount + slot;

        public static TalentScreen Build()
        {
            var screen = new TalentScreen();
            var pages = new List<UiNode>();

            for (int path = 0; path < TalentPage.PathCount; path++)
            {
                pages.Add(screen.BuildPage(path));
            }

            // The sky holds all three pages side by side and SLIDES. Its own
            // width is one page: the others hang off the edges, which is exactly
            // the overflow the paging is made of.
            var sky = Ui.Panel("TalentSky", Place.At(0f, 40f),
                    UiSize.Fixed(ConstellationLayout.SkyWidth, ConstellationLayout.SkyHeight), pages)
                .AllowOverflow("the other two constellations hang off this page's edges - that overhang IS the paging")
                .AllowOverlap("three full-size pages share one coordinate frame; exactly one is ever on screen");
            screen.Sky = sky;

            var characterName = Ui.Label("TalentCharacterName", UiString.Runtime, new UiVec(420f, 52f), 30,
                "#EDE6FF", Place.At(0f, 452f)).AsDecor();
            var pathName = Ui.Label("TalentPathName", UiString.Runtime, new UiVec(520f, 36f), 20,
                "#B8A8D9", Place.At(0f, 398f)).AsDecor();
            var embers = Ui.Label("TalentEmberCount", UiStrings.TalentEmbers, new UiVec(300f, 44f), 22,
                "#F2DB9E", Place.At(700f, 452f)).AsDecor();

            screen.CharacterName = characterName;
            screen.PathName = pathName;
            screen.EmberCount = embers;

            // Paging arrows flank the sky at its own edges, so they read as
            // belonging to it rather than to the screen.
            var prevPath = Ui.Button("PrevPathButton", UiStrings.TalentPrev, new UiVec(72f, 120f), 34,
                Place.At(-(ConstellationLayout.SkyWidth * 0.5f + 70f), 40f));
            var nextPath = Ui.Button("NextPathButton", UiStrings.TalentNext, new UiVec(72f, 120f), 34,
                Place.At(ConstellationLayout.SkyWidth * 0.5f + 70f, 40f));
            screen.PrevPathButton = prevPath;
            screen.NextPathButton = nextPath;

            // Character switching sits with the NAME it changes, not with the
            // path arrows -- two pairs of arrows doing different things at the
            // same size in the same place is the fastest way to make a screen
            // feel arbitrary.
            var prevCharacter = Ui.Button("PrevCharacterButton", UiStrings.TalentPrev, new UiVec(56f, 52f), 24,
                Place.At(-270f, 452f));
            var nextCharacter = Ui.Button("NextCharacterButton", UiStrings.TalentNext, new UiVec(56f, 52f), 24,
                Place.At(270f, 452f));
            screen.PrevCharacterButton = prevCharacter;
            screen.NextCharacterButton = nextCharacter;

            // LOCAL to the plate, not to the screen. These are its children, so
            // an absolute -392 put them a third of a screen below the plate that
            // is supposed to contain them.
            var detailName = Ui.Label("TalentDetailName", UiString.Runtime, new UiVec(520f, 40f), 24,
                "#EDE6FF", Place.At(0f, 38f));
            var detailBody = Ui.Label("TalentDetailBody", UiString.Runtime, new UiVec(760f, 72f), 17,
                "#B8A8D9", Place.At(0f, -22f));

            // "Caption", NOT "Label" -- UiEmitter names every button's own
            // generated text "<button>Label", so this collided with the one it
            // makes for InvestButton. The wiring bound this node and painted
            // the refusal text onto it correctly; anything looking the node up
            // by name got the emitter's empty one instead. See UiAudit A4b.
            var investLabel = Ui.Label("InvestButtonCaption", UiStrings.TalentInvest, new UiVec(260f, 40f), 20,
                "#F2DB9E", Place.At(0f, 0f));
            var invest = Ui.Button("InvestButton", UiString.Runtime, new UiVec(300f, 66f), 20,
                Place.At(640f, -420f));
            invest.Children.Add(investLabel);

            screen.DetailName = detailName;
            screen.DetailBody = detailBody;
            screen.InvestButton = invest;
            screen.InvestLabel = investLabel;

            var back = Ui.Button("TalentBackButton", UiStrings.TalentBack, new UiVec(220f, 60f), 16,
                Place.At(-830f, 480f));
            screen.BackButton = back;

            // Hidden until an orb is picked, rather than sitting there empty.
            //
            // A 880x150 coloured slab with nothing written on it is the single
            // loudest thing on a screen whose whole subject is a dim sky, and it
            // is the state the screen OPENS in. The plate is the frame for an
            // answer; with no question asked there is nothing to frame.
            var detailPlate = Ui.Panel("TalentDetailPlate", Place.At(0f, -420f), UiSize.Fixed(880f, 150f),
                    detailName, detailBody)
                .Coloured("#2C1C42E0")
                .AsDecor()
                .Inactive();
            screen.DetailPlate = detailPlate;

            screen.Root = Ui.Panel("TalentPanel", UiSize.Fixed(1920f, 1080f),
                Ui.Sprite("TalentBackground", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor(),
                sky,
                Ui.Column("TalentHeading", Place.At(0f, 431f), spacing: 0f, UiAlign.Centre).AsDecor(),
                characterName, pathName, embers,
                prevPath, nextPath, prevCharacter, nextCharacter,
                detailPlate, invest, back);

            return screen;
        }

        private UiNode BuildPage(int path)
        {
            var children = new List<UiNode>();

            // EDGES FIRST, so every orb draws over the lines that reach it --
            // declaration order is painter's order and a line across an orb
            // reads as a crack in it.
            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                foreach (int parent in TalentSkeleton.Parents[slot])
                {
                    children.Add(BuildEdge(path, slot, parent));
                }
            }

            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                children.Add(BuildOrb(path, slot));
            }

            return Ui.Panel($"TalentPage{path}", Place.At(ConstellationLayout.PageX(path, 0), 0f),
                    UiSize.Fixed(ConstellationLayout.SkyWidth, ConstellationLayout.SkyHeight), children)
                .AllowOverflow("orbs and their glows sit on the sky's own edges; the constellation is not a boxed diagram")
                .AllowOverlap("edges pass beneath the orbs they connect, which is what makes them read as connections");
        }

        private static UiVec PositionOf(int slot) => new UiVec(
            ConstellationLayout.StarX(TalentSkeleton.DxSlot[slot], TalentPage.MaxAbsDx),
            ConstellationLayout.StarY(TalentSkeleton.Depth[slot], TalentPage.DepthCount));

        private UiNode BuildOrb(int path, int slot)
        {
            var at = PositionOf(slot);
            float size = ConstellationLayout.StarSize;

            // The glow is a SEPARATE node behind the orb rather than a tint on
            // it, because the two say different things at once: the orb says
            // what kind of talent this is, the glow says whether it is yours.
            var glow = Ui.Sprite($"Orb{path}_{slot}Glow", "proc:radial_glow",
                    new UiVec(size * 2.4f, size * 2.4f), Place.At(0f, 0f))
                .Coloured("#F2DB9E00")
                .AllowOverflow("an unlit orb's glow is meant to bleed past it - that bleed is the whole signal")
                .AsDecor();

            var orb = Ui.Button($"Orb{path}_{slot}", UiString.Runtime, new UiVec(size, size), 12,
                    Place.At(at.X, at.Y))
                .AllowOverlap("a constellation's stars share their light - the glows are 2.4x the orb and reaching a neighbour is the effect, not a collision")
                .AllowOverflow("ConstellationLayout puts the first and last rows ON the sky's edges, so half an orb hangs over them by construction");
            // A DISC, not a glow.
            //
            // This read "proc:radial_glow" -- the same asset as its own glow
            // child, untinted. So an orb had no body and no rim: it was a soft
            // white smudge sitting inside a slightly larger soft white smudge,
            // and a constellation of them read as smears rather than as stars
            // you could aim at. solid_circle is what the baker made for exactly
            // this, and it lets the three state colours below actually show.
            orb.SpriteKey = "proc:solid_circle";
            orb.Children.Add(glow);

            Orbs.Add(orb);
            OrbGlows.Add(glow);
            return orb;
        }

        private UiNode BuildEdge(int path, int slot, int parent)
        {
            var a = PositionOf(parent);
            var b = PositionOf(slot);

            // A thin quad stretched between the two, rotated to face along the
            // line. One node per connection rather than a line renderer: the
            // emitter builds Images, and a rotated quad is a line.
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI);

            var edge = Ui.Solid($"Edge{path}_{parent}_{slot}", "#6B5B9E66",
                    Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f), UiSize.Fixed(length, 3f))
                .Rotated(angle)
                .AsDecor()
                .AllowOverflow("a rotated edge's axis-aligned box is wider than the line inside it");

            Edges.Add(edge);
            return edge;
        }
    }
}
