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

        public List<NodeRef> Cards = new List<NodeRef>();
        public List<NodeRef> CardNames = new List<NodeRef>();
        public List<NodeRef> CardRarities = new List<NodeRef>();
        public List<NodeRef> CardBodies = new List<NodeRef>();
        public List<NodeRef> CardIcons = new List<NodeRef>();
        public List<NodeRef> CardSelections = new List<NodeRef>();

        public static RelicDraftScreen Build()
        {
            var screen = new RelicDraftScreen();

            var inside = new List<UiNode>
            {
                Ui.Label("DraftTitle", UiStrings.DraftTitle, new UiVec(1200f, 60f), 38, "#F2DB9E",
                    Place.At(0f, 350f)).AsDecor(),
                Ui.Label("DraftSubtitle", UiStrings.DraftSubtitle, new UiVec(1000f, 36f), 19, "#B8A8D9",
                    Place.At(0f, 300f)).AsDecor(),
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
                .Inactive();

            screen.PrevPageButton = prevPage;
            screen.NextPageButton = nextPage;
            screen.PageLabel = pageLabel;
            inside.Add(prevPage);
            inside.Add(nextPage);
            inside.Add(pageLabel);

            // Always pressable. A draft the player cannot leave is worse than a
            // draft they decline, and an empty pool has to have an exit.
            var descend = Ui.Button("DraftDescendButton", UiStrings.DraftDescend,
                new UiVec(320f, 64f), 24, Place.At(0f, -350f));
            screen.DescendButton = descend;
            inside.Add(descend);

            var frame = Ui.Panel("DraftFrame", Place.At(0f, 0f), UiSize.Fixed(1500f, 820f), inside)
                .Coloured("#241736F5");
            screen.Frame = frame;

            var content = Ui.Panel("DraftContent", Place.At(0f, 0f), UiSize.Fill, frame);
            screen.Root = Ui.Modal("RelicDraftPanel", "#0A0614F0", content).Inactive();
            return screen;
        }

        private UiNode BuildCard(int index, float x)
        {
            // The selection ring is a SIBLING layer under the card rather than
            // a colour on the card itself, so "which is chosen" and "what
            // rarity is it" stay two channels. v1's item cells carried both on
            // one Image.color and the two states fought.
            var selection = Ui.Solid($"DraftCard{index}Selection", "#F2DB9E00",
                    Place.Stretch(-6f, -6f, -6f, -6f), UiSize.Fill)
                .AsDecor()
                .AllowOverflow("the ring sits 6px OUTSIDE its card on every side - that overhang is the whole signal, since a border drawn inside the card would be competing with the card's own edge rather than framing it");

            var backing = Ui.Solid($"DraftCard{index}Backing", "#2E2244", Place.Stretch(), UiSize.Fill)
                .AsDecor();

            var icon = Ui.Sprite($"DraftCard{index}Icon", null,
                    Place.At(0f, 110f), UiSize.Fixed(160f, 160f))
                .AsDecor();

            var name = Ui.Label($"DraftCard{index}Name", UiString.Runtime, new UiVec(340f, 60f), 24,
                    "#EDE6FF", Place.At(0f, -10f))
                .AsDecor();

            var rarity = Ui.Label($"DraftCard{index}Rarity", UiStrings.DraftRarity, new UiVec(340f, 30f), 16,
                    "#B8A8D9", Place.At(0f, -60f))
                .AsDecor();

            var body = Ui.Label($"DraftCard{index}Body", UiString.Runtime, new UiVec(330f, 110f), 15,
                    "#9C8FC4", Place.At(0f, -140f))
                .AsDecor();

            var card = Ui.Button($"DraftCard{index}", UiString.Runtime,
                new UiVec(CardWidth, CardHeight), 12, Place.At(x, 20f));

            foreach (var layer in new[] { selection, backing, icon, name, rarity, body })
            {
                card.Children.Add(layer);
            }

            Cards.Add(card);
            CardSelections.Add(selection);
            CardIcons.Add(icon);
            CardNames.Add(name);
            CardRarities.Add(rarity);
            CardBodies.Add(body);
            return card;
        }
    }
}
