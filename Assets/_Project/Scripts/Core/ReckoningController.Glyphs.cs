using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // THE PROCESSED ART ON THE REWARD SCREENS (docs/PLAN_REWARD_SCREENS.md
    // section 5): the cache by page class, the glyph + count groups on a
    // card's delta line, the whetstone's face and the door's face. The sprites
    // are one IconEntry table (RewardArt.TableIds) wired at build time.
    public partial class ReckoningController
    {
        [SerializeField] internal IconEntry[] rewardArt;
        [SerializeField] internal Image cacheArt;
        [SerializeField] internal GameObject[] offerFaces;
        [SerializeField] internal Image[] offerDeltaGlyphs;
        [SerializeField] internal Image[] honeGlyphs;
        [SerializeField] internal TMP_Text[] honeCounts;
        [SerializeField] internal Image leaveDoorGlyph;
        [SerializeField] internal Image leaveHoneGlyph;
        [SerializeField] internal TMP_Text leaveHoneCount;
        [SerializeField] internal Image leaveCoinGlyph;
        [SerializeField] internal TMP_Text leaveCoinCount;

        private const float GlyphGap = 4f;
        private const float GroupGap = 16f;

        // ---- the cache -------------------------------------------------------------

        // The bundle or tin for this page's class, with the seam's light where
        // the art leaks: along the tin's lid line, or at the bundle's knot (a
        // bundle has no seam to open).
        private void PaintCache()
        {
            if (_page == null || cacheArt == null) return;

            bool relic = _page.Kind == RewardPageKind.Relic;
            ItemIcons.Apply(cacheArt, rewardArt, RewardArt.CacheId(relic, _page.SourceClass));
            if (cacheSeam == null) return;

            var sprite = cacheArt.sprite;
            float aspect = sprite == null ? 1f : sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            float width = Mathf.Min(ReckoningScreen.CacheWidth, ReckoningScreen.CacheHeight * aspect);
            float height = width / aspect;

            var rect = cacheSeam.rectTransform;
            rect.sizeDelta = new Vector2(width * (relic ? 0.8f : 0.3f), rect.sizeDelta.y);
            rect.anchoredPosition = relic
                ? new Vector2(0f, -0.02f * height)
                : new Vector2(0.12f * width, -0.06f * height);
        }

        // ---- a card's delta line ------------------------------------------------------

        private void PaintDeltaGlyph(int i, RewardCardFace face, PendingCard card)
        {
            if (offerDeltaGlyphs == null || i >= offerDeltaGlyphs.Length) return;

            string id = card.Outcome == CardOutcome.Paid ? RewardArt.Coins
                : face.HasDelta ? face.DeltaGlyphId
                : null;
            ItemIcons.Apply(offerDeltaGlyphs[i], rewardArt, id);
        }

        // ---- glyph + count groups -----------------------------------------------------

        // One glyph with an optional number, seated side by side with the other
        // groups of the same control and centred as a whole.
        private struct GlyphGroup
        {
            public Image Glyph;
            public TMP_Text Count;
            public string Id;
            public string Text;

            public GlyphGroup(Image glyph, TMP_Text count, string id, string text)
            {
                Glyph = glyph;
                Count = count;
                Id = id;
                Text = text;
            }
        }

        private void PaintGroups(IList<GlyphGroup> groups)
        {
            float total = 0f;
            int shown = 0;
            var widths = new float[groups.Count];
            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                bool show = group.Id != null;
                ItemIcons.Apply(group.Glyph, rewardArt, group.Id);
                if (group.Count != null)
                {
                    bool counted = show && !string.IsNullOrEmpty(group.Text);
                    group.Count.gameObject.SetActive(counted);
                    if (counted) group.Count.SetContent(group.Text);
                    widths[g] = counted ? GlyphGap + Mathf.Ceil(group.Count.GetPreferredValues(group.Text).x) : 0f;
                }

                if (!show) continue;
                widths[g] += RewardArt.GlyphSize;
                total += widths[g];
                shown++;
            }

            if (shown > 1) total += GroupGap * (shown - 1);

            float x = -total * 0.5f;
            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                if (group.Id == null) continue;

                SetX(group.Glyph, x + RewardArt.GlyphSize * 0.5f);
                if (group.Count != null && group.Count.gameObject.activeSelf)
                {
                    float textWidth = widths[g] - RewardArt.GlyphSize - GlyphGap;
                    var rect = group.Count.rectTransform;
                    rect.sizeDelta = new Vector2(textWidth + 2f, rect.sizeDelta.y);
                    rect.anchoredPosition = new Vector2(x + RewardArt.GlyphSize + GlyphGap + (textWidth + 2f) * 0.5f,
                        rect.anchoredPosition.y);
                }

                x += widths[g] + GroupGap;
            }
        }

        private static void SetX(Graphic graphic, float x)
        {
            if (graphic == null) return;

            var rect = graphic.rectTransform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }

        // ---- the whetstone and the door -------------------------------------------------

        // The whetstone alone, or the coins and the card's price when every shown
        // slot is spent and the choice pays gold instead.
        private void PaintHoneFace(int i, bool gold, int price)
        {
            if (honeGlyphs == null || i >= honeGlyphs.Length) return;

            var count = honeCounts != null && i < honeCounts.Length ? honeCounts[i] : null;
            PaintGroups(new[]
            {
                new GlyphGroup(honeGlyphs[i], count, gold ? RewardArt.Coins : RewardArt.Whetstone,
                    gold ? UiStrings.ReckoningCount.Format(price) : ""),
            });
        }

        // The door, then what leaving pays now: a whetstone count and a coin
        // count. The forward arrow is text, and the groups are hidden then.
        private void PaintDoorFace(int hones, int gold, bool forward)
        {
            PaintGroups(new[]
            {
                new GlyphGroup(leaveDoorGlyph, null, forward ? null : RewardArt.Door, ""),
                new GlyphGroup(leaveHoneGlyph, leaveHoneCount,
                    !forward && hones > 0 ? RewardArt.Whetstone : null, UiStrings.ReckoningCount.Format(hones)),
                new GlyphGroup(leaveCoinGlyph, leaveCoinCount,
                    !forward && gold > 0 ? RewardArt.Coins : null, UiStrings.ReckoningCount.Format(gold)),
            });
        }

        // The card's face and its back are one or the other.
        private void ShowBack(int i, bool back)
        {
            if (offerBacks != null && i < offerBacks.Length && offerBacks[i] != null)
                offerBacks[i].gameObject.SetActive(back);
            if (offerFaces != null && i < offerFaces.Length && offerFaces[i] != null)
                offerFaces[i].SetActive(!back);
        }
    }
}
