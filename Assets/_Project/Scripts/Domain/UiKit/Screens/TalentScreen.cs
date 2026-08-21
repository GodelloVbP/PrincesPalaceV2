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
        // Keyed by tools/key_green_screen.py out of the raw generations beside
        // them; the Processed copies are what the game loads.
        public const string OrbUnlitKey = "UI/TalentTree/Processed/orb_unlit.png";
        public const string OrbLitKey = "UI/TalentTree/Processed/orb_lit.png";

        // Soft along its short axis, so a limb fades at its sides rather than
        // ending on a hard band.
        public const string EdgeStripeKey = "proc:soft_edge_stripe";

        // A DEAD CONDUIT, A WARM HALO, A WHITE-HOT CORE.
        //
        // Not bark. v1's kit was painted as gnarled branches and
        // branch_tile_set is still sitting in Art/UI/TalentTree unreferenced
        // because of it -- these limbs are lines carrying current, so an unlit
        // one is a cold wire rather than a piece of wood, and everything that
        // happens to it is light.
        private const string EdgeDim = "#33263D";
        private const string EdgeGlow = "#FF91455A";
        private const string EdgeCore = "#FFD98C";
        private const string EdgeSpark = "#FFEBC0E6";

        public const string BackgroundKey = "Assets/_Project/Art/Backgrounds/Divine_principality_nebula.png";

        // One orb per slot per path. Declared rather than pooled: the skeleton
        // is a fixed shape, so every position is known at build time and the
        // audit can check all of them.
        // static readonly rather than const, because the skeleton derives its
        // own slot count from the rows it is made of now. Nothing needs this
        // at compile time -- it sizes arrays and bounds loops, both of which
        // are happy with a value read at type-init.
        public static readonly int OrbCount = TalentSkeleton.SlotCount;

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
        public NodeRef RespecButton;

        // Indexed [path * OrbCount + slot]. One flat list rather than a list of
        // lists, because the wiring binds arrays and E4 counts them.
        public List<NodeRef> Orbs = new List<NodeRef>();
        public List<NodeRef> OrbGlows = new List<NodeRef>();

        // The reveal: a mask per orb, holding the LIT medallion. The mask is
        // what grows when a talent is kindled; the orb inside it is held at
        // full size and full strength the whole way, which is the difference
        // between something arriving and something switching on.
        public List<NodeRef> OrbReveals = new List<NodeRef>();
        public List<NodeRef> Edges = new List<NodeRef>();

        // The lit half of each edge, in the SAME order as Edges. Switched off
        // at build time and raised by the controller for the connections the
        // player has actually earned -- so an unspent tree draws as dim limbs
        // and investing is what lights the path behind it.
        public List<NodeRef> EdgeGlows = new List<NodeRef>();

        // Which slot each edge arrives at, parallel to Edges. The controller
        // lights an edge when its CHILD is invested: the parent necessarily
        // already is, since that is what the prerequisite means, so the child
        // alone answers it and nothing has to re-walk the skeleton at runtime.
        public List<int> EdgeChildSlots = new List<int>();

        // The two animated layers, and each edge's own length. The components
        // that drive them live in Core -- Domain cannot name a MonoBehaviour --
        // so the wiring step attaches them, and the spark is TOLD its travel
        // rather than measuring a parent whose rect is a cosmetic width.
        public List<NodeRef> EdgeCores = new List<NodeRef>();
        public List<NodeRef> EdgeSparks = new List<NodeRef>();
        public List<float> EdgeLengths = new List<float>();

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

            // RESPEC, level 20 of the reward track. Bottom-left, mirroring Back
            // top-left at the same size -- the two are the screen's only
            // whole-screen actions, as against Kindle which acts on the one orb
            // that is selected.
            //
            // No confirmation step, and that is a decision rather than an
            // omission. A respec here is FREE and gives back exactly what was
            // spent, so pressing it by accident costs the arrangement of a
            // build and nothing else -- re-kindling the same orbs costs the
            // same embers that just came back. The hold-to-confirm the abandon
            // button uses is for a run that cannot be got back.
            // INACTIVE in the tree. TalentController.Refresh shows it only for
            // a squad that has reached level 20 -- but a node that starts
            // active is visible for the frame before any controller paints, so
            // an unearned reward flashes on screen every time the screen opens.
            // Same posture as the dossier's "+" buttons and the draft's empty
            // hint.
            var respec = Ui.Button("TalentRespecButton", UiStrings.TalentRespec, new UiVec(220f, 60f), 16,
                    Place.At(-830f, -420f))
                .Inactive();
            screen.RespecButton = respec;

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

            screen.Root = Ui.Panel("TalentPanel", UiSize.Fill,
                Ui.Sprite("TalentBackground", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor(),
                sky,
                Ui.Column("TalentHeading", Place.At(0f, 431f), spacing: 0f, UiAlign.Centre).AsDecor(),
                characterName, pathName, embers,
                prevPath, nextPath, prevCharacter, nextCharacter,
                detailPlate, invest, back, respec);

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
            ConstellationLayout.StarX(TalentSkeleton.DxSlot[slot], TalentSkeleton.Depth[slot]),
            ConstellationLayout.StarY(TalentSkeleton.Depth[slot]));

        private UiNode BuildOrb(int path, int slot)
        {
            var at = PositionOf(slot);

            // SIZED BY ROLE, which is the design's own rule and the thing this
            // screen had lost: every orb was one size, so the capstone at the
            // top of an eight-tier climb looked exactly like the first chain
            // node above the root.
            float size = ConstellationLayout.OrbSize(TalentSkeleton.Kind[slot]);

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
            // THE PAINTED MEDALLION, which has been sitting in Art/UI/TalentTree
            // unreferenced since the art was keyed.
            //
            // It was proc:solid_circle -- a flat baked disc, chosen when this
            // screen had no orb art to reach for. It does: orb_unlit is the
            // locked/available medallion and orb_lit the invested one, both
            // styled after the glowing orbs on the Hub's own Talents building,
            // which is the reference the whole kit was generated against.
            //
            // UNLIT IS THE BUILT STATE. The controller swaps to orb_lit on the
            // ones the player owns, so a scene opened with nothing invested --
            // which is every screenshot and every fresh save -- draws the tree
            // dark, and lighting up is something the player does.
            orb.SpriteKey = OrbUnlitKey;

            // THE LIT MEDALLION, held at full size inside a mask that is not.
            //
            // orb_lit has been declared and unreferenced since the art was
            // keyed -- the screen tinted the single unlit sprite for all three
            // states instead. This is what it was for.
            var lit = Ui.Sprite($"Orb{path}_{slot}Lit", OrbLitKey,
                    new UiVec(size, size), Place.At(0f, 0f))
                .AsDecor();

            // Clipping, so shrinking this reveals a SLICE of the orb inside
            // rather than a smaller orb. Built switched off and sized to the
            // node's full diameter -- TalentNodeInvestReveal reads that size in
            // Awake, which is why it is authored here rather than restated as a
            // constant over there: the diameter depends on the slot's role.
            var reveal = Ui.Panel($"Orb{path}_{slot}Reveal", Place.At(0f, 0f),
                    UiSize.Fixed(size, size), lit)
                .Clipping()
                .Inactive()
                .AsDecor();

            // AFTER the glow, so the lit orb draws over the halo rather than
            // under it.
            orb.Children.Add(glow);
            orb.Children.Add(reveal);

            Orbs.Add(orb);
            OrbGlows.Add(glow);
            OrbReveals.Add(reveal);
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

            // TEN WIDE, not three. A 3px hairline reads as a wiring diagram;
            // v1's edges are limbs with a lit crack down them, and the width is
            // most of what makes a connection look grown rather than drawn.
            var edge = Ui.Solid($"Edge{path}_{parent}_{slot}", EdgeDim,
                    Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f),
                    UiSize.Fixed(length, ConstellationLayout.EdgeWidth))
                .Rotated(angle)
                .AsDecor()
                .AllowOverflow("a rotated edge's axis-aligned box is wider than the line inside it");

            // THE LIT LAYER, a sibling rather than a child.
            //
            // v1 hangs the core off the glow so it inherits the rotation for
            // free, and that is right there -- but here the glow is what the
            // controller switches, and a switched-off parent takes its children
            // with it whether or not that was meant. Sibling and child are the
            // same picture; only one of them can be reasoned about.
            var glow = Ui.Sprite($"Edge{path}_{parent}_{slot}Glow", EdgeStripeKey,
                    new UiVec(length, ConstellationLayout.EdgeGlowWidth),
                    Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f))
                .Coloured(EdgeGlow)
                .Rotated(angle)
                .Inactive()
                .AsDecor()
                .AllowOverflow("a soft halo is meant to bleed past the limb - that bleed is the light");

            var core = Ui.Sprite($"Edge{path}_{parent}_{slot}Core", EdgeStripeKey,
                    new UiVec(length, ConstellationLayout.EdgeCoreWidth),
                    Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f))
                .Coloured(EdgeCore)
                .Rotated(angle)
                // NOT Inactive. It is a CHILD of the glow, which is the thing
                // the controller switches, so it comes and goes with its
                // parent -- and switching it off here as well meant it never
                // came back on at all. A lit edge showed its halo and no crack,
                // and the crackle rode an object that never ran.
                .AsDecor()
                .AllowOverflow("a rotated edge's axis-aligned box is wider than the line inside it");

            // THE DOT THAT RUNS THE LINE. A child of the glow like the core,
            // so it inherits the rotation and can travel along its own local X
            // -- and so an unlit edge has no spark for free, because a
            // switched-off glow takes its children with it.
            //
            // Placed at the far end to start; TalentEdgeSpark moves it from
            // there. Authored somewhere real rather than at zero so the audit
            // measures it where it will actually be seen.
            var spark = Ui.Sprite($"Edge{path}_{parent}_{slot}Spark", "proc:radial_glow",
                    new UiVec(ConstellationLayout.EdgeSparkSize, ConstellationLayout.EdgeSparkSize),
                    Place.At(-length * 0.5f, 0f))
                .Coloured(EdgeSpark)
                .AsDecor()
                .AllowOverflow("the spark sits ON the edge's end and is wider than the line - it is meant to spill past it");

            glow.Children.Add(core);
            glow.Children.Add(spark);

            Edges.Add(edge);
            EdgeGlows.Add(glow);
            EdgeChildSlots.Add(slot);
            EdgeCores.Add(core);
            EdgeSparks.Add(spark);
            EdgeLengths.Add(length);

            return Ui.Panel($"Edge{path}_{parent}_{slot}Group", Place.At(0f, 0f), UiSize.Fill,
                    edge, glow)
                .AsDecor();
        }
    }
}
