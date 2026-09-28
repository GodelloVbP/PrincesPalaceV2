using System.Collections.Generic;
using PrincesPalace.Domain.Relics;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // Three relics, one descent, choose.
    //
    // An overlay on the hub rather than a screen of its own, for the same
    // reason the character sheet is one: the hub is where a run starts, and
    // the gate is still standing behind this when it closes. It also means the
    // draft cannot be skipped by navigating past it -- the Descend button is
    // the only way out and it is the thing that starts the run.
    //
    // THREE CARDS, FIXED. The count is RelicPool.OfferCount rather than a
    // number typed here, so the tree, the controller and the count audit read
    // the same one and a pool change cannot leave a fourth card orphaned.
    public sealed class RelicDraftScreen
    {
        // VIOLET, 3:4 KIT CONTAINER. The authored 380x460 (aspect 0.826) was
        // 40% off the kit's 3:4 aspect and had to move; WHICH AXIS MOVES has
        // been the same call twice, and the answer is still width. Height is
        // what the frame's title/subtitle/Descend column above and below the
        // card row is budgeted against (see the frame's own 1500x1000
        // comment), so changing width is the change that does not reopen that
        // budget.
        //
        // 345: at the art's true 0.75 aspect, a 460-tall card is 345 wide
        // (345/460 = 0.75 exactly). Three of them at a CardGap pitch reach
        // +-557.5, still well inside the frame's own +-697.5 content bound,
        // and every child inside the card (the 230-wide body being the
        // widest) has room to spare.
        public const float CardWidth = 345f; // was 270, was 380
        public const float CardHeight = 460f;
        private const float CardGap = 40f;

        public UiNode Root;
        public NodeRef Frame;
        public NodeRef EmptyHint;
        public NodeRef DescendButton;

        // Level 70 of the reward track turns the draft from three random cards
        // into the whole eligible pool, which is 13 relics -- more than a row
        // of three can hold, so it pages. Hidden until then.
        public NodeRef PrevPageButton;
        public NodeRef NextPageButton;
        public NodeRef PageLabel;

        // BAKED, not painted -- the same starburst the Reckoning's item offers
        // wear behind their icon. Copied wholesale rather than re-derived: see
        // ReckoningScreen.BurstKey for why the generated one won and the
        // painted attempt did not.
        public const string BurstKey = "proc:rarity_burst";

        public List<NodeRef> Cards = new List<NodeRef>();
        public List<NodeRef> CardNames = new List<NodeRef>();
        public List<NodeRef> CardRarities = new List<NodeRef>();
        public List<NodeRef> CardBodies = new List<NodeRef>();
        public List<NodeRef> CardIcons = new List<NodeRef>();
        public List<NodeRef> CardSelections = new List<NodeRef>();
        public List<NodeRef> CardHalos = new List<NodeRef>();
        public List<NodeRef> CardBursts = new List<NodeRef>();

        public static RelicDraftScreen Build()
        {
            var screen = new RelicDraftScreen();

            var inside = new List<UiNode>
            {
                Ui.Label("DraftTitle", UiStrings.DraftTitle, new UiVec(1200f, 60f), 38, "#F2DB9E",
                    Place.At(0f, 350f)).AsDecor().Styled(TypographyRole.CeremonialTitle),
                Ui.Label("DraftSubtitle", UiStrings.DraftSubtitle, new UiVec(1000f, 36f), 19, "#B8A8D9",
                    Place.At(0f, 300f)).AsDecor().Styled(TypographyRole.Body),
            };

            // Laid out from the centre so the row stays centred whatever the
            // count is -- two cards from a thin pool must not sit left-aligned
            // with a hole where the third would be.
            float pitch = CardWidth + CardGap;
            float firstX = -(RelicPool.OfferCount - 1) * pitch * 0.5f;

            for (int i = 0; i < RelicPool.OfferCount; i++)
            {
                inside.Add(screen.BuildCard(i, firstX + i * pitch));
            }

            // Shown when the unlocked pool is empty -- which cannot happen with
            // the current content, and would still be silent if it ever did.
            var empty = Ui.Label("DraftEmptyHint", UiStrings.DraftNoRelics, new UiVec(900f, 44f), 22,
                    "#7E6E9E", Place.At(0f, 0f))
                .AsDecor()
                .Styled(TypographyRole.Body)
                .Inactive();
            screen.EmptyHint = empty;
            inside.Add(empty);

            // PAGING, for an offer wider than the card row can show at once.
            //
            // Dormant today -- nothing currently offers more than three -- but
            // the tree keeps the controls rather than removing them: the card
            // row is three wide and emitted once at build time, so the whole
            // pool still cannot be shown at once should a future reward widen
            // it again, and a 13-card grid would be a second card design
            // competing with the one that already reads well. Paging reuses
            // the cards exactly as they are and costs two arrows.
            //
            // Outside the card row, not over it: three 380-wide cards at a 420
            // pitch reach x +/-610. Pulled in from +/-680 to +/-660 when the
            // frame gained real border art: the container's own 3.5%
            // left/right inset caps content at +/-697.5 (half of the 1500-
            // wide frame's own content bound), and 680+30 (this button's own
            // half-width) reached 710 -- 12.5px past it.
            //
            // The counter sits between the cards (which stop at y -210) and
            // Descend (at -350), which is why its y is its own rather than the
            // arrows'.
            var pager = Ui.Pager("DraftPrevPage", Place.At(-660f, 20f),
                "DraftNextPage", Place.At(660f, 20f), new UiVec(60f, 60f), 22,
                "DraftPageLabel", UiString.Runtime, Place.At(0f, -280f),
                new UiVec(300f, 30f), 18, "#B8A8D9");
            pager.Prev.Inactive();
            pager.Next.Inactive();
            pager.Label.AsDecor().Styled(TypographyRole.TacticalData).Inactive();

            screen.PrevPageButton = pager.Prev;
            screen.NextPageButton = pager.Next;
            screen.PageLabel = pager.Label;
            inside.Add(pager.Prev);
            inside.Add(pager.Next);
            inside.Add(pager.Label);

            // Always pressable. A draft the player cannot leave is worse than a
            // draft they decline, and an empty pool has to have an exit.
            // GOLD: the one recommended action on this screen -- take the
            // offer (or knowingly decline it) and move on. It is the only way
            // out, which is exactly what Gold is reserved for.
            var descend = Ui.Button("DraftDescendButton", UiStrings.DraftDescend,
                new UiVec(320f, 64f), 24, Place.At(0f, -350f))
                .Themed(ButtonTheme.Gold);
            screen.DescendButton = descend;
            inside.Add(descend);

            // THE SYSTEM MENU'S OWN FRAME IDIOM, not a kit Container: a flat
            // near-black Solid ground plus a hairline Rim reads as the
            // kit's UI chrome, where a leather Container plate reads as a
            // game OBJECT sitting behind the menu. SystemMenuScreen and
            // ShopScreen use the same idiom for the same reason (see Ui.
            // SystemMenuPane's own header).
            //
            // PanelFill/PanelRim are SystemMenuScreen's own two constants
            // (its PanelFill/PanelRim fields) copied rather than shared,
            // because nothing before this needed a Container-idiom frame's
            // fill/rim colours outside the file that owns its own theme --
            // ShopScreen's version of the same shape uses its own CardFill/
            // BorderQuiet pair instead, which is the same "the shell doesn't
            // export its colours" precedent already in the kit.
            //
            // FrameSize AT 1500x1000: not a Container, so no aspect has to
            // match the kit's measured 3:2 -- every child's placement (card
            // row, pager, Descend) is measured against this exact size.
            const string PanelFill = "#1A1024F5"; // SystemMenuScreen.PanelFill (PanelViolet at 96%)
            const string PanelRim = FightHudPalette.BorderGold; // SystemMenuScreen.PanelRim
            var frameSize = new UiVec(1500f, 1000f);

            var frameChildren = new List<UiNode> { Ui.Solid("DraftFrameFill", PanelFill, frameSize, Place.At(0f, 0f)).AsDecor() };
            frameChildren.AddRange(inside);
            // LAST, drawn on top of everything it encloses -- same reason
            // SystemMenuScreen's own Rim call is last (its own comment: "the
            // whole fix for the sides disappearing along the tab bar").
            frameChildren.AddRange(Ui.Rim("DraftFrame", frameSize, PanelRim));

            var frame = Ui.Panel("DraftFrame", Place.At(0f, 0f), UiSize.Fixed(frameSize), frameChildren);
            screen.Frame = frame;

            var content = Ui.Panel("DraftContent", Place.At(0f, 0f), UiSize.Fill, frame);
            screen.Root = Ui.Modal("RelicDraftPanel", "#0A0614F0", content).Inactive();
            return screen;
        }

        // Copied from ReckoningScreen.BuildOffer -- the item-reward card at the
        // end of a fight -- rather than the draft's own earlier card design.
        // Easy-paste on purpose, per the ask: halo, burst, icon, name and a
        // meta line, chromeless, no backing plate. One thing added back on
        // top of that copy: the description line the reward card has no room
        // for, because a relic is chosen for its EFFECT and the offer card's
        // layout alone left that unreadable until Descend.
        private UiNode BuildCard(int index, float x)
        {
            // THE SAME GLOW LOOK ThemedButtonState's own focus state uses
            // (proc:radial_glow, tinted per theme, alpha 0 at idle -- see
            // Ui.BuildVisuals), rather than a bespoke Solid ring: the
            // selection is the one layer the reward card has no equivalent
            // for -- an offer there is taken on click and gone, but a draft
            // round can be reconsidered before Descend, so "which one is
            // currently chosen" still needs its own channel. Violet, matching
            // the frame's own theme. Sized like the halo/burst behind the
            // icon (deliberately larger than the card, bleeding past its
            // edge) rather than an inset ring, so the glow reads as light
            // coming off the card instead of a border competing with the
            // rarity burst.
            var selection = Ui.Sprite($"DraftCard{index}Selection", "proc:radial_glow",
                    Place.Stretch(-24f, -24f, -24f, -24f), UiSize.Fill)
                .Coloured(Ui.ThemeGlowHex(ButtonTheme.Violet))
                .AsDecor()
                .AllowOverflow("the glow sits outside its card on every side, same as ThemedButtonState's own focus glow - that bleed IS the selection signal");

            // Icon geometry scaled from the offer card's 200x250-in-400 to this
            // card's 460 height, same top/bottom-margin shape.
            const float IconTop = 170f;
            const float IconBottom = -30f;
            const float IconCentre = (IconTop + IconBottom) * 0.5f;

            var halo = Ui.Sprite($"DraftCard{index}Halo", "proc:radial_glow",
                    Place.At(0f, IconCentre), UiSize.Fixed(300f, 300f))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the halo is deliberately larger than the icon it sits behind - that bleed IS the rarity signal");

            var burst = Ui.Sprite($"DraftCard{index}Burst", BurstKey,
                    Place.At(0f, IconCentre), UiSize.Fixed(310f, 310f))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the burst is deliberately larger than the icon it sits behind - that bleed IS the rarity signal");

            var icon = Ui.Sprite($"DraftCard{index}Icon", null,
                    Place.At(0f, IconCentre), UiSize.Fixed(200f, IconTop - IconBottom))
                .AsDecor();

            // 230, DOWN FROM 340: the card was narrowed from 380 to clear the
            // Violet 3:4 container's own aspect (see CardWidth's own comment)
            // and 230 was what the 270-wide version's 234.9px content width
            // left. The card is 345 wide now, so the container's 6.9%-a-side
            // inset leaves 297.4 -- 230 has 33px a side rather than 5, which
            // is slack it does not have to spend.
            var name = Ui.Label($"DraftCard{index}Name", UiString.Runtime, new UiVec(230f, 60f), 24,
                    "#EDE6FF", Place.At(0f, IconBottom - 6f - 33f))
                .AsDecor();

            const float RarityY = IconBottom - 6f - 66f - 4f - 13f;

            var rarity = Ui.Label($"DraftCard{index}Rarity", UiStrings.DraftRarity, new UiVec(230f, 30f), 16,
                    "#B8A8D9", Place.At(0f, RarityY))
                .AsDecor();

            // Below the rarity line, in the gap the reward card's shorter card
            // doesn't have: rarity's box bottoms out at RarityY - 15, so this
            // starts 8px under that.
            //
            // 290x58: the container's bottom inset gives a 204.7px content
            // bound on a 460-tall card, and a wider, shorter box clears it
            // -- 290 wide (the card is 345 wide) fits more characters per
            // line than a taller box would, and at 58 tall it sits exactly
            // 8px under the rarity line with no pull-up needed, clearing
            // the content bound by 4.7px.
            var body = Ui.Label($"DraftCard{index}Body", UiString.Runtime, new UiVec(290f, 58f), 14,
                    "#9C8FC4", Place.At(0f, RarityY - 15f - 8f - 29f))
                .AsDecor();

            // THE FRAME, nested inside the clickable button rather than
            // replacing it: a card still has to take the click Choose()
            // listens for, and Ui.Container's own factory returns a plain
            // (non-Button) Panel, so the frame is declared as a Sprite-
            // backed CHILD of the NoChrome button instead of swapping the
            // button out for one. Same size as the button's own hit box, so
            // the painted border reads flush with the clickable area.
            var frame = Ui.Container($"DraftCard{index}Frame", ButtonTheme.Violet, ContainerRatio.ThreeByFour,
                Place.At(0f, 0f), new UiVec(CardWidth, CardHeight));
            Ui.ContainerContent(frame, ContainerRatio.ThreeByFour, $"DraftCard{index}FrameContent",
                selection, halo, burst, icon, name, rarity, body);

            var card = Ui.Button($"DraftCard{index}", UiString.Runtime,
                    new UiVec(CardWidth, CardHeight), 12, Place.At(x, 20f))
                .NoChrome();
            card.Children.Add(frame);

            Cards.Add(card);
            CardSelections.Add(selection);
            CardHalos.Add(halo);
            CardBursts.Add(burst);
            CardIcons.Add(icon);
            CardNames.Add(name);
            CardRarities.Add(rarity);
            CardBodies.Add(body);
            return card;
        }
    }
}
