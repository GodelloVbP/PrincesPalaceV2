using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The widgets one relic list paints into, gathered from a controller's own
    // arrays. Any array may be null, and any slot in it: a fixture built
    // without art, or a list that shows no description (the draft's held
    // strip), simply leaves that part out.
    internal readonly struct RelicListWidgets
    {
        public readonly GameObject[] Rows;
        public readonly Image[] Icons;
        public readonly TMP_Text[] Names;
        public readonly TMP_Text[] Rarities;
        public readonly TMP_Text[] Descriptions;

        public readonly Button PrevButton;
        public readonly Button NextButton;
        public readonly TMP_Text PageLabel;
        public readonly GameObject EmptyHint;

        // Art by relic id, the table the owning scene baked.
        public readonly IconEntry[] IconTable;

        public RelicListWidgets(GameObject[] rows, Image[] icons, TMP_Text[] names, TMP_Text[] rarities,
                                TMP_Text[] descriptions, Button prevButton, Button nextButton,
                                TMP_Text pageLabel, GameObject emptyHint, IconEntry[] iconTable)
        {
            Rows = rows;
            Icons = icons;
            Names = names;
            Rarities = rarities;
            Descriptions = descriptions;
            PrevButton = prevButton;
            NextButton = nextButton;
            PageLabel = pageLabel;
            EmptyHint = emptyHint;
            IconTable = iconTable;
        }
    }

    // THE ONE RELIC PRESENTER (plan C24). It paints a RelicListModel into a
    // controller's widgets: icon, name, rarity in its band's colour, the
    // description through SetContent (DescriptionRenderer), the pager and the
    // empty hint. The Reckoning's RELICS tab, the RUN pane and the relic
    // draft's held strip differ only in which widgets they hand it.
    //
    // A plain class the owning controller builds, not a MonoBehaviour: the
    // widgets stay ordinary serialized arrays on the controller (bound by name
    // like every other control), and the presenter holds no state the
    // controller does not.
    internal sealed class RelicListPresenter
    {
        private readonly RelicListWidgets _w;
        private RelicListModel _model;

        // Raised after the page changed by the pager, so the owner can re-wire
        // navigation over the rows that are now showing.
        public System.Action Paged;

        public RelicListPresenter(RelicListWidgets widgets)
        {
            _w = widgets;
            _model = new RelicListModel(new List<RelicRow>(), Capacity);
        }

        public RelicListModel Model => _model;

        // Rows on one page: however many the scene built.
        public int Capacity => _w.Rows == null ? 0 : _w.Rows.Length;

        // Hooks the pager's buttons. Called once, from the owner's Wire.
        public void WirePager()
        {
            if (_w.PrevButton != null) _w.PrevButton.onClick.AddListener(() => Step(-1));
            if (_w.NextButton != null) _w.NextButton.onClick.AddListener(() => Step(1));
        }

        // Paints the relics held, in order, keeping the reader's page where it
        // still exists.
        public void Show(IReadOnlyList<string> ids) => Show(RelicRows.From(ids));

        public void Show(IReadOnlyList<RelicRow> rows)
        {
            _model = new RelicListModel(rows, Capacity).WithPageOf(_model);
            Repaint();
        }

        public bool Step(int direction)
        {
            if (!_model.Step(direction)) return false;

            Repaint();
            Paged?.Invoke();
            return true;
        }

        // Turns to the page holding `relicId` and returns its slot on it, or -1.
        public int ShowRelic(string relicId)
        {
            int slot = _model.ShowRelic(relicId);
            if (slot >= 0) Repaint();
            return slot;
        }

        public void Repaint()
        {
            if (_w.EmptyHint != null) _w.EmptyHint.SetActive(_model.IsEmpty);

            bool paged = _model.IsPaged;
            SetShown(_w.PrevButton, paged);
            SetShown(_w.NextButton, paged);

            if (_w.PageLabel != null)
            {
                _w.PageLabel.gameObject.SetActive(paged);
                if (paged) _w.PageLabel.Set(UiStrings.RelicPage, _model.Page + 1, _model.PageCount);
            }

            var page = _model.Visible;
            for (int i = 0; i < Capacity; i++)
            {
                bool present = i < page.Count;
                if (_w.Rows[i] != null) _w.Rows[i].SetActive(present);
                if (!present) continue;

                Paint(i, page[i]);
            }
        }

        private void Paint(int slot, RelicRow row)
        {
            var name = At(_w.Names, slot);
            if (name != null) name.SetContent(row.Name);

            var rarity = At(_w.Rarities, slot);
            if (rarity != null)
            {
                rarity.SetContent(row.RarityText);
                if (row.Known) rarity.color = RarityColors.For(row.Rarity);
            }

            var description = At(_w.Descriptions, slot);
            if (description != null) description.SetContent(row.Description);

            ItemIcons.Apply(At(_w.Icons, slot), _w.IconTable, row.IconId);
        }

        private static void SetShown(Button button, bool shown)
        {
            if (button != null) button.gameObject.SetActive(shown);
        }

        private static T At<T>(T[] all, int index) where T : class =>
            all != null && index >= 0 && index < all.Length ? all[index] : null;
    }
}
