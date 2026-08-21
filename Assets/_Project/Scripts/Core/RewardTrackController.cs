using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // Paints the reward track and scrolls it to where the player is.
    //
    // NOTHING HERE PLACES A NODE. Every dot, caption and level number was
    // emitted at its real coordinate, because level 40 sits at the same x
    // forever -- so the only thing that moves is the content rect, and the only
    // thing that changes is colour and text. That is what keeps a hundred-node
    // screen to one anchoredPosition and three loops.
    //
    // Reads `level` for what is EARNED and `claimedTrackLevel` for what is
    // PAID, and they are not the same question: the watermark trails the level
    // until the next payout lands, and a track that coloured by the watermark
    // would show a player who just levelled that their new reward has not
    // arrived. What they earned is what they see.
    public class RewardTrackController : MonoBehaviour
    {
        [SerializeField] internal RectTransform viewport;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal RectTransform railFill;
        [SerializeField] internal TMP_Text summary;
        [SerializeField] internal Button closeButton;

        private bool _wired;

        [SerializeField] internal Image[] dots;
        [SerializeField] internal TMP_Text[] captions;
        [SerializeField] internal TMP_Text[] levelNumbers;

        private static readonly Color DotToCome = Hex(RewardTrackScreen.DotToCome);
        private static readonly Color DotDone = Hex(RewardTrackScreen.DotDone);
        private static readonly Color DotHere = Hex(RewardTrackScreen.DotHere);
        private static readonly Color TextToCome = Hex(RewardTrackScreen.TextToCome);
        private static readonly Color TextDone = Hex(RewardTrackScreen.TextDone);
        private static readonly Color TextHere = Hex(RewardTrackScreen.TextHere);

        private void OnEnable()
        {
            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void Close() => gameObject.SetActive(false);

        public void Refresh()
        {
            int level = SquadTrack.BestLevel();

            PaintNodes(level);
            PaintRail(level);
            PaintSummary(level);
            ScrollTo(level);
        }

        private void PaintNodes(int level)
        {
            if (dots == null) return;

            for (int i = 0; i < dots.Length; i++)
            {
                int nodeLevel = RewardTrackLayout.FirstLevel + i;
                var entry = RewardTrack.At(nodeLevel);

                bool reached = nodeLevel <= level;
                bool here = nodeLevel == level;

                if (dots[i] != null)
                {
                    dots[i].color = here ? DotHere : reached ? DotDone : DotToCome;
                }

                if (captions != null && i < captions.Length && captions[i] != null)
                {
                    captions[i].SetContent(RewardTrackNames.Of(entry));
                    captions[i].color = here ? TextHere : reached ? TextDone : TextToCome;
                }

                if (levelNumbers != null && i < levelNumbers.Length && levelNumbers[i] != null)
                {
                    levelNumbers[i].SetContent(nodeLevel.ToString());
                    levelNumbers[i].color = here ? TextHere : reached ? TextDone : TextToCome;
                }
            }
        }

        // The lit half of the rail, stretched to the player's own node.
        //
        // Left-pivoted and driven by WIDTH, never by anchors -- anchors are
        // relative to the parent, and the parent here is a 19,000px content
        // rect. The dossier's XP bar records what that mistake looks like when
        // it ships.
        private void PaintRail(int level)
        {
            if (railFill == null) return;

            float width = level < RewardTrackLayout.FirstLevel
                ? 0f
                : RewardTrackLayout.NodeX(level);

            railFill.sizeDelta = new Vector2(width, railFill.sizeDelta.y);
        }

        private void PaintSummary(int level)
        {
            if (summary == null) return;

            int next = RewardTrack.NextRewardLevel(level);
            if (next <= 0)
            {
                summary.Set(UiStrings.TrackSummaryComplete, level);
                return;
            }

            summary.Set(UiStrings.TrackSummary, level, next, RewardTrackNames.Of(RewardTrack.At(next)));
        }

        // OPENS ON THE PLAYER, not on level 2.
        //
        // A hundred-level rail opened at its left edge shows a wall of things
        // already collected and nothing about what is next, which is the one
        // question the screen exists to answer. RewardTrackLayout.ScrollFor
        // clamps at both ends so levels near either extreme still fill the
        // window.
        private void ScrollTo(int level)
        {
            if (content == null || viewport == null) return;

            float x = RewardTrackLayout.ScrollFor(level, viewport.rect.width);
            content.anchoredPosition = new Vector2(x, content.anchoredPosition.y);
        }

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
    }
}
