using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The widgets one "Gained" strip paints into (RelicChipTree), gathered from
    // the owning controller's own serialized fields.
    internal readonly struct RelicChipWidgets
    {
        public readonly GameObject Strip;
        public readonly RectTransform Caption;

        // Centre the caption and chips in the strip rather than starting them
        // at its left edge: the Reckoning centres them over Continue, the
        // event page lines them up with the choice rows' left edge.
        public readonly bool Centred;
        public readonly Button[] Chips;
        public readonly Image[] Icons;
        public readonly TMP_Text[] Names;
        public readonly GameObject Tooltip;
        public readonly TMP_Text TooltipName;
        public readonly TMP_Text TooltipMeta;
        public readonly TMP_Text TooltipBody;
        public readonly IconEntry[] IconTable;

        public RelicChipWidgets(GameObject strip, RectTransform caption, bool centred, Button[] chips, Image[] icons,
            TMP_Text[] names, GameObject tooltip, TMP_Text tooltipName, TMP_Text tooltipMeta, TMP_Text tooltipBody,
            IconEntry[] iconTable)
        {
            Strip = strip;
            Caption = caption;
            Centred = centred;
            Chips = chips;
            Icons = icons;
            Names = names;
            Tooltip = tooltip;
            TooltipName = tooltipName;
            TooltipMeta = tooltipMeta;
            TooltipBody = tooltipBody;
            IconTable = iconTable;
        }
    }

    // THE RELIC CHIP STRIP (docs/PLAN_BUGS_QA.md C2), the relic
    // presenter seam's second widget set beside RelicListPresenter: the same
    // RelicRows model, painted as one chip per relic instead of one row. The
    // Reckoning and the event page differ only in the widgets they hand it and
    // in what "the primary action" is.
    //
    // IT REMEMBERS WHAT IT SHOWED. Displayed is exactly the ids on screen, and
    // that, not the run's pending list, is what the owner acknowledges when the
    // player leaves the surface: a receipt it never showed stays pending.
    //
    // The tooltip goes through the owner's TooltipFocusRouter, so pad focus and
    // the pointer share one show/hide and one arbitration.
    internal sealed class RelicChipStrip
    {
        private readonly RelicChipWidgets _w;
        private readonly List<string> _displayed = new List<string>();
        private readonly List<RelicRow> _rows = new List<RelicRow>();
        private readonly float[] _nameInsets = new float[RelicChipTree.ChipCount];

        // Raised when a chip is pressed (pad Submit or click): the owner moves
        // focus to its primary action. A chip is something to read, not a
        // choice, so pressing one means "done reading".
        public System.Action Pressed;

        public RelicChipStrip(RelicChipWidgets widgets)
        {
            _w = widgets;
        }

        public IReadOnlyList<string> Displayed => _displayed;

        public bool Any => _displayed.Count > 0;

        // The chips on screen, in order: the owner's nav rail.
        public List<Selectable> Selectables
        {
            get
            {
                var list = new List<Selectable>();
                for (int i = 0; i < _displayed.Count && _w.Chips != null && i < _w.Chips.Length; i++)
                {
                    if (_w.Chips[i] != null) list.Add(_w.Chips[i]);
                }

                return list;
            }
        }

        // Once, from the owner's Wire.
        public void Wire(TooltipFocusRouter router)
        {
            if (_w.Chips == null) return;

            for (int i = 0; i < _w.Chips.Length; i++)
            {
                var chip = _w.Chips[i];
                if (chip == null) continue;

                int index = i;
                chip.onClick.AddListener(() => Pressed?.Invoke());

                if (router == null) continue;
                router.Register(chip, entered => ShowTooltip(index, entered));

                var node = chip.gameObject;
                var hover = node.GetComponent<HoverIndex>();
                if (hover == null) hover = node.AddComponent<HoverIndex>();
                hover.Index = index;
                hover.Changed = (_, entered) => router.Pointer(node, entered);
            }
        }

        // Paints the first ChipCount ids, in grant order; hidden when empty.
        public void Show(IReadOnlyList<string> ids)
        {
            _displayed.Clear();
            _rows.Clear();

            int capacity = _w.Chips == null ? 0 : Mathf.Min(_w.Chips.Length, RelicChipTree.ChipCount);
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (_rows.Count >= capacity) break;
                    if (string.IsNullOrEmpty(id)) continue;
                    _displayed.Add(id);
                    _rows.Add(RelicRows.Row(id));
                }
            }

            HideTooltip();

            for (int i = 0; i < capacity; i++)
            {
                bool present = i < _rows.Count;
                if (_w.Chips[i] != null) _w.Chips[i].gameObject.SetActive(present);
                if (!present) continue;

                // A relic with no art has no icon slot: its name moves left
                // into the space rather than leaving a hole before it.
                bool hasIcon = ItemIcons.Apply(At(_w.Icons, i), _w.IconTable, _rows[i].IconId);
                _nameInsets[i] = hasIcon ? RelicChipTree.NameInset : RelicChipTree.IconInset + 4f;

                var name = At(_w.Names, i);
                if (name != null)
                {
                    name.SetContent(_rows[i].Name);
                    name.rectTransform.offsetMin = new Vector2(_nameInsets[i], name.rectTransform.offsetMin.y);
                }
            }

            if (_w.Strip != null) _w.Strip.SetActive(_rows.Count > 0);
            LayOut();
        }

        public void HideTooltip()
        {
            if (_w.Tooltip != null) _w.Tooltip.SetActive(false);
        }

        // Each chip as wide as its name wants, up to an equal share of the strip.
        private void LayOut()
        {
            var strip = _w.Strip == null ? null : _w.Strip.transform as RectTransform;
            if (strip == null || _rows.Count == 0) return;

            var wanted = new List<float>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var name = At(_w.Names, i);
                float text = name == null ? 0f : name.GetPreferredValues(_rows[i].Name, 0f, 0f).x;
                wanted.Add(_nameInsets[i] + text + RelicChipTree.NameRightPad);
            }

            var slots = RelicChipLayout.Lay(wanted, RelicChipTree.ChipAreaWidth(strip.rect.width),
                RelicChipTree.ChipGap, RelicChipTree.MinChipWidth);

            var last = slots[slots.Count - 1];
            float used = RelicChipTree.CaptionWidth + RelicChipTree.CaptionGap + last.Left + last.Width;
            float offset = _w.Centred ? Mathf.Max(0f, (strip.rect.width - used) * 0.5f) : 0f;

            if (_w.Caption != null)
            {
                _w.Caption.anchoredPosition = new Vector2(offset, _w.Caption.anchoredPosition.y);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var rect = _w.Chips[i] == null ? null : _w.Chips[i].transform as RectTransform;
                if (rect == null) continue;

                rect.anchoredPosition = new Vector2(
                    offset + RelicChipTree.CaptionWidth + RelicChipTree.CaptionGap + slots[i].Left,
                    rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(slots[i].Width, rect.sizeDelta.y);
            }
        }

        // The one show/hide, reached through the owner's router from the
        // pointer and from pad focus alike.
        private void ShowTooltip(int index, bool entered)
        {
            if (_w.Tooltip == null) return;

            if (!entered || index < 0 || index >= _rows.Count)
            {
                HideTooltip();
                return;
            }

            var row = _rows[index];
            if (_w.TooltipName != null) _w.TooltipName.SetContent(row.Name);
            if (_w.TooltipMeta != null)
            {
                _w.TooltipMeta.SetContent(row.RarityText);
                if (row.Known) _w.TooltipMeta.color = RarityColors.For(row.Rarity);
            }

            if (_w.TooltipBody != null) _w.TooltipBody.SetContent(row.Description);

            FitTooltip();
            PlaceTooltip(index);
            _w.Tooltip.SetActive(true);
        }

        private void FitTooltip()
        {
            var box = _w.Tooltip.transform as RectTransform;
            if (box == null) return;

            float inner = RelicChipTree.TooltipWidth - RelicChipTree.TooltipPad * 2f;
            float body = RelicChipTree.TooltipMinBodyHeight;
            if (_w.TooltipBody != null)
            {
                body = Mathf.Max(body, _w.TooltipBody.GetPreferredValues(_w.TooltipBody.text, inner, 0f).y);
            }

            float height = Mathf.Min(RelicChipTree.TooltipMaxHeight,
                RelicChipTree.TooltipPad * 2f + RelicChipTree.TooltipHeaderHeight + body);

            if (_w.TooltipBody != null)
            {
                var bodyRect = _w.TooltipBody.rectTransform;
                bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x,
                    height - RelicChipTree.TooltipPad * 2f - RelicChipTree.TooltipHeaderHeight);
            }

            box.sizeDelta = new Vector2(RelicChipTree.TooltipWidth, height);
        }

        // ABOVE the chip. Everything from the strip's top edge down is kept out
        // of TooltipPlacement: beside a chip would hide the chip next door, and
        // under the strip is the surface's primary action (Continue, the
        // choice rows), which the box must never cover.
        // Worked in the tooltip's parent space, centred on that parent, which
        // is where the tree anchors the box (centre anchors, centre pivot).
        private void PlaceTooltip(int index)
        {
            var box = _w.Tooltip.transform as RectTransform;
            var parent = box == null ? null : box.parent as RectTransform;
            var chip = _w.Chips[index] == null ? null : _w.Chips[index].transform as RectTransform;
            var strip = _w.Strip == null ? null : _w.Strip.transform as RectTransform;
            if (parent == null || chip == null || strip == null) return;

            var chipRect = InParent(parent, chip);
            var stripRect = InParent(parent, strip);
            var half = parent.rect.size * 0.5f;

            const float Margin = 8f;
            float belowTop = stripRect.yMax + half.y;
            var keepOut = new UiRect(new UiVec(0f, -half.y + belowTop * 0.5f), new UiVec(half.x * 2f, belowTop));

            var at = TooltipPlacement.Beside(
                chipRect.center.x, chipRect.center.y, chipRect.width, chipRect.height,
                box.sizeDelta.x, box.sizeDelta.y,
                interiorLeft: -half.x + Margin, interiorRight: half.x - Margin,
                interiorBottom: -half.y + Margin, interiorTop: half.y - Margin,
                keepOut: new[] { keepOut });

            box.anchoredPosition = new Vector2(at.X, at.Y);
        }

        // A rect's corners in `parent`'s local space, measured from the
        // parent's own centre.
        private static Rect InParent(RectTransform parent, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var centre = parent.rect.center;

            var min = (Vector2)parent.InverseTransformPoint(corners[0]) - centre;
            var max = (Vector2)parent.InverseTransformPoint(corners[2]) - centre;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static T At<T>(T[] all, int index) where T : class =>
            all != null && index >= 0 && index < all.Length ? all[index] : null;
    }
}
