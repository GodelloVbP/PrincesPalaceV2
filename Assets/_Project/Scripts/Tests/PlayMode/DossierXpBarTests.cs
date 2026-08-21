using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The dossier's XP bar, and whether it stays on its own track.
    //
    // THE BUG THIS EXISTS FOR: the fill was driven by anchors --
    // `anchorMax.x = fraction` with sizeDelta zeroed -- which looks equivalent
    // to setting a width and is not. ANCHORS ARE RELATIVE TO THE PARENT. The
    // parent is column A, not the 226px track, so a bar meant to cross the
    // track crossed that fraction of the whole column: out through its own
    // "258 left" caption, past the column divider, and on into the loadout
    // stage beside it.
    //
    // The Options slider's fill records the identical mistake in its own
    // comment, made and fixed while this one was still live -- which is the
    // reason this is a test rather than another comment. It measures the two
    // RECTS against each other, so it holds whatever drives the fill: anchors,
    // width, or something neither of us has thought of.
    public class DossierXpBarTests
    {
        private SystemMenuController _menu;

        [UnityTest]
        public IEnumerator TheFillNeverLeavesItsTrack()
        {
            yield return OpenTheDossier();

            var track = RectNamed("DossierXpTrack");
            var fill = RectNamed("DossierXpFill");

            var trackCorners = Corners(track);
            var fillCorners = Corners(fill);

            Assert.GreaterOrEqual(fillCorners.xMin, trackCorners.xMin - 0.5f,
                "the XP fill starts left of its own track");
            Assert.LessOrEqual(fillCorners.xMax, trackCorners.xMax + 0.5f,
                $"the XP fill runs {fillCorners.xMax - trackCorners.xMax:F0}px past the right end of " +
                "its track - it is being sized against something other than the track");
        }

        // Every character, because the fill is only wrong at fractions that
        // happen to be large: a character early in a level would have looked
        // fine while one near the top of it painted across the next column.
        [UnityTest]
        public IEnumerator ItStaysOnTheTrackForEveryCharacter()
        {
            yield return OpenTheDossier();

            var next = _menu.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "DossierNextCharacter");
            Assert.IsNotNull(next, "the dossier has no next-character button");

            for (int i = 0; i < 4; i++)
            {
                var track = Corners(RectNamed("DossierXpTrack"));
                var fill = Corners(RectNamed("DossierXpFill"));

                Assert.LessOrEqual(fill.xMax, track.xMax + 0.5f,
                    $"character {i}: the XP fill runs past its track");
                Assert.LessOrEqual(fill.width, track.width + 0.5f,
                    $"character {i}: the XP fill is wider than the track it sits on");

                next.onClick.Invoke();
                yield return null;
            }
        }

        // The whole dossier fills the pane, which is what the fill bug made
        // impossible to see: a bar escaping its column was hidden among 118px
        // of dead margin on either side of a screen that did not fill its box.
        [UnityTest]
        public IEnumerator TheDossierFillsTheContentPane()
        {
            yield return OpenTheDossier();

            var root = RectNamed("DossierPanel") ?? RectNamed("CharacterDossier");

            // FAILS rather than ignores. This used to skip itself when neither
            // name was found -- a test that cannot fail for the reason it
            // exists, since a renamed root is exactly the change most likely to
            // break the thing it measures.
            Assert.IsNotNull(root,
                "neither DossierPanel nor CharacterDossier is in the scene - if the root was renamed, " +
                "update the names here rather than letting the pane's size go unchecked");

            Assert.AreEqual(SystemMenuLayout.PanelWidth, root.rect.width, 1f,
                "the dossier is not as wide as the pane it is hosted in");
            Assert.AreEqual(SystemMenuLayout.ContentHeight, root.rect.height, 1f,
                "the dossier is not as tall as the pane it is hosted in");
        }

        // ---- fixture ------------------------------------------------------------

        // ---- the reward track line ----------------------------------------------
        //
        // The track was PAYABLE before it was LEGIBLE. Levels grant Favor, stat
        // points, extra starting relics and a wider item offer, and until this
        // line existed nothing on any screen said so -- a player could pass
        // level 25 and start drafting two relics with no way to learn why,
        // which reads as a bug rather than as a reward.
        //
        // Through the real dossier rather than by calling the controller: the
        // failure worth catching is the label never reaching the screen, and a
        // test that read PaintTrackLine's output directly would pass with the
        // node unbound.
        [UnityTest]
        public IEnumerator TheDossierSaysWhatTheNextLevelIsWorth()
        {
            yield return OpenTheDossier();

            var label = TextNamed("DossierTrackNext");
            Assert.IsNotNull(label, "the dossier has no reward-track line");

            Assert.IsNotEmpty(label.text, "the reward-track line is blank");
            StringAssert.Contains("LEVEL", label.text.ToUpperInvariant(),
                $"the track line does not name a level: '{label.text}'");
        }

        // The line has to stay inside column A or it runs under the loadout
        // beside it -- the same failure the XP fill above is here for, and the
        // reason this label is the widest thing in that column.
        [UnityTest]
        public IEnumerator TheTrackLineStaysInsideItsColumn()
        {
            yield return OpenTheDossier();

            var line = Corners(RectNamed("DossierTrackNext"));
            var track = Corners(RectNamed("DossierXpTrack"));

            Assert.LessOrEqual(line.xMax, track.xMax + 60f,
                "the reward-track line runs past the column the XP bar sits in");
            Assert.GreaterOrEqual(line.yMax, 0f - 10000f);
            Assert.Less(line.yMax, track.yMin + 0.5f,
                "the reward-track line overlaps the XP bar above it");
        }

        // ---- spending a stat point ------------------------------------------------
        //
        // AUDIT #53: Character.Invest was implemented, commented about a "+"
        // per score, tested, and called by nothing. Levelling's only reward was
        // a point that could not be spent. These drive the "+" that ends that.

        private Button ButtonNamed(string name) =>
            _menu.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == name);

        private static Character First() =>
            SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);

        [UnityTest]
        public IEnumerator ThePlusIsHiddenWhenThereIsNothingToSpend()
        {
            yield return OpenTheDossier();

            First().unspentStatPoints = 0;
            Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include).Refresh();
            yield return null;

            Assert.IsFalse(ButtonNamed("DossierAttrPlus0").gameObject.activeSelf,
                "a character with no points to spend is shown a plus");
            Assert.IsEmpty(TextNamed("DossierUnspentPoints").text);
        }

        [UnityTest]
        public IEnumerator HavingPointsShowsThePlusAndSaysHowMany()
        {
            yield return OpenTheDossier();

            First().unspentStatPoints = 3;
            Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include).Refresh();
            yield return null;

            Assert.IsTrue(ButtonNamed("DossierAttrPlus0").gameObject.activeSelf,
                "a character with points is not shown a plus to spend them");
            StringAssert.Contains("3", TextNamed("DossierUnspentPoints").text,
                "the dossier does not say how many points are waiting");
        }

        [UnityTest]
        public IEnumerator PressingThePlusSpendsAPointIntoThatScore()
        {
            yield return OpenTheDossier();

            var character = First();
            character.unspentStatPoints = 2;
            character.investedAbilityScores = default;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.Refresh();
            yield return null;

            ButtonNamed("DossierAttrPlus0").onClick.Invoke();
            yield return null;

            character = First();
            Assert.AreEqual(1, character.unspentStatPoints, "pressing the plus did not spend a point");
            Assert.AreEqual(1, character.InvestedPointTotal, "the point was spent but landed nowhere");
        }

        // THE TRAP THE CELL ORDER SETS. The six cells are filled highest-first
        // per character, so cell 0 holds whatever this character's best ability
        // happens to be -- binding the score at wire time instead of resolving
        // it at click time would spend into whatever was strongest when the
        // screen was built.
        [UnityTest]
        public IEnumerator ThePlusSpendsIntoTheScoreShownUnderIt()
        {
            yield return OpenTheDossier();

            var character = First();
            character.unspentStatPoints = 1;
            character.investedAbilityScores = default;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.Refresh();
            yield return null;

            // Whatever the top-left cell is labelled with is the score that
            // must move.
            string key = TextNamed("DossierAttrKey0").text.Trim();
            Assert.IsNotEmpty(key, "the first attribute cell has no key label to read");

            ButtonNamed("DossierAttrPlus0").onClick.Invoke();
            yield return null;

            character = First();
            var moved = PrincesPalace.Domain.Stats.AbilityScores.All
                .Where(s => character.investedAbilityScores[s] > 0)
                .ToList();

            Assert.AreEqual(1, moved.Count, "the point went into more than one score, or none");
            StringAssert.StartsWith(moved[0].ToString().Substring(0, 3).ToUpperInvariant(),
                key.ToUpperInvariant(),
                $"the plus on the cell labelled '{key}' spent into {moved[0]} instead");
        }

        // PRESSING "+" TWICE PUTS BOTH POINTS IN THE SAME PLACE.
        //
        // The attribute grid is sorted highest-first, and spending changes the
        // order -- so re-sorting on every refresh made the cells reshuffle
        // under the cursor: raise a score tied with the one above it and the
        // two swap, sending the second click into a different ability with
        // nothing on screen warning the player. The order is now frozen while
        // one character is on screen.
        [UnityTest]
        public IEnumerator PressingThePlusTwiceSpendsBothPointsIntoOneScore()
        {
            yield return OpenTheDossier();

            var character = First();
            character.unspentStatPoints = 2;
            character.investedAbilityScores = default;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.Refresh();
            yield return null;

            // THE INVARIANT, not one worked example. Which cell reorders
            // depends on how close together this character's scores happen to
            // be, so asserting a particular reshuffle would pass or fail on
            // content rather than on behaviour. What must hold for every
            // character is that the labels do not move.
            var before = Enumerable.Range(0, 6).Select(i => TextNamed($"DossierAttrKey{i}").text).ToList();

            // CELL 4 SPECIFICALLY. Sheep is authored STR 7 / DEX 7, tied and
            // broken by declaration order, so they sit in cells 3 and 4 -- and
            // raising DEX to 8 promotes it past STR. That is the cheapest real
            // reshuffle this content can produce, and picking the top cell
            // instead would prove nothing because raising the highest score
            // leaves it highest.
            ButtonNamed("DossierAttrPlus4").onClick.Invoke();
            yield return null;

            var after = Enumerable.Range(0, 6).Select(i => TextNamed($"DossierAttrKey{i}").text).ToList();

            CollectionAssert.AreEqual(before, after,
                "the attribute grid reordered after spending a point, so the next click lands on a " +
                "different ability than the one the player aimed at");

            // And the second click still lands where the first did.
            ButtonNamed("DossierAttrPlus4").onClick.Invoke();
            yield return null;

            character = First();
            var moved = PrincesPalace.Domain.Stats.AbilityScores.All
                .Where(sc => character.investedAbilityScores[sc] > 0)
                .ToList();

            Assert.AreEqual(0, character.unspentStatPoints, "both points should have been spent");
            Assert.AreEqual(1, moved.Count,
                $"the two points landed in {moved.Count} different abilities");
            Assert.AreEqual(2, character.investedAbilityScores[moved[0]]);
        }

        private TMPro.TMP_Text TextNamed(string name) =>
            _menu.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: true)
                .FirstOrDefault(t => t.name == name);

        private IEnumerator OpenTheDossier()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            _menu.Open();
            _menu.Select(SystemMenuTab.CharacterInventory);
            yield return null;
            yield return null;
        }

        private RectTransform RectNamed(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name) as RectTransform;

        // World-space, because the two rects have different parents and pivots
        // and comparing anchoredPosition would be comparing two coordinate
        // systems.
        private static Rect Corners(RectTransform rect)
        {
            Assert.IsNotNull(rect, "a rect this test needs was not found");

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
