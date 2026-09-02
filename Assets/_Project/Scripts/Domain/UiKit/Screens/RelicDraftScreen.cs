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
        public const float CardWidth = 380f;
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

            // PAGING, for level 70's "choose your starting relics".
            //
            // The card row is three wide and emitted once at build time, so the
            // whole pool cannot be shown at once -- and a 13-card grid would be
            // a second card design competing with the one that already reads
            // well. Paging reuses the cards exactly as they are and costs two
            // arrows.
            //
            // Outside the card row, not over it: three 380-wide cards at a 420
            // pitch reach x +/-610, and the frame's half-width is 750.
            var prevPage = Ui.Button("DraftPrevPage", UiStrings.TalentPrev,
                    new UiVec(60f, 60f), 22, Place.At(-680f, 20f))
                .Inactive();
            var nextPage = Ui.Button("DraftNextPage", UiStrings.TalentNext,
                    new UiVec(60f, 60f), 22, Place.At(680f, 20f))
                .Inactive();

            // Between the cards (which stop at y -210) and Descend (at -350).
            var pageLabel = Ui.Label("DraftPageLabel", UiString.Runtime, new UiVec(300f, 30f), 18,
                    "#B8A8D9", Place.At(0f, -280f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData)
                .Inactive();

            screen.PrevPageButton = prevPage;
            screen.NextPageButton = nextPage;
            screen.PageLabel = pageLabel;
            inside.Add(prevPage);
            inside.Add(nextPage);
            inside.Add(pageLabel);

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

            var frame = Ui.Panel("DraftFrame", Place.At(0f, 0f), UiSize.Fixed(1500f, 820f), inside)
                .Coloured("#241736F5");
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
            // The selection ring is the one layer the reward card has no
            // equivalent for -- an offer there is taken on click and gone, but
            // a draft round can be reconsidered before Descend, so "which one
            // is currently chosen" still needs its own channel. Kept as a
            // sibling ring for the same reason it always was: a colour on the
            // card itself would fight the rarity read the burst now carries.
            var selection = Ui.Solid($"DraftCard{index}Selection", "#F2DB9E00",
                    Place.Stretch(-6f, -6f, -6f, -6f), UiSize.Fill)
                .AsDecor()
                .AllowOverflow("the ring sits 6px OUTSIDE its card on every side - that overhang is the whole signal, since a border drawn inside the card would be competing with the card's own edge rather than framing it");

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

            var name = Ui.Label($"DraftCard{index}Name", UiString.Runtime, new UiVec(340f, 60f), 24,
                    "#EDE6FF", Place.At(0f, IconBottom - 6f - 33f))
                .AsDecor();

            const float RarityY = IconBottom - 6f - 66f - 4f - 13f;

            var rarity = Ui.Label($"DraftCard{index}Rarity", UiStrings.DraftRarity, new UiVec(340f, 30f), 16,
                    "#B8A8D9", Place.At(0f, RarityY))
                .AsDecor();

            // Below the rarity line, in the gap the reward card's shorter card
            // doesn't have: rarity's box bottoms out at RarityY - 15, so this
            // starts 8px under that and runs to within 8px of the card's own
            // bottom edge (-CardHeight/2 = -230).
            var body = Ui.Label($"DraftCard{index}Body", UiString.Runtime, new UiVec(340f, 76f), 14,
                    "#9C8FC4", Place.At(0f, RarityY - 15f - 8f - 38f))
                .AsDecor();

            var card = Ui.Button($"DraftCard{index}", UiString.Runtime,
                    new UiVec(CardWidth, CardHeight), 12, Place.At(x, 20f))
                .NoChrome();

            foreach (var layer in new[] { selection, halo, burst, icon, name, rarity, body })
            {
                card.Children.Add(layer);
            }

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
