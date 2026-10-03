using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // OverArtEdge re-fits its backing plate every LateUpdate but writes it only
    // when the fit moved, so a static HUD label does not rebuild the canvas
    // 60 times a second (plan C7 / T14).
    public class OverArtEdgeBackingWriteTests
    {
        private GameObject _canvas;
        private RectTransform _parent;
        private TextMeshProUGUI _label;
        private OverArtEdge _edge;
        private RectTransform _backing;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("OverArtEdgeCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var parentGo = new GameObject("Parent", typeof(RectTransform));
            _parent = parentGo.GetComponent<RectTransform>();
            _parent.SetParent(_canvas.transform, false);
            _parent.sizeDelta = new Vector2(800f, 400f);

            var backingGo = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            _backing = backingGo.GetComponent<RectTransform>();
            _backing.SetParent(_parent, false);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.SetActive(false);
            labelGo.transform.SetParent(_parent, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.text = "STANDING";
            _label.fontSize = 24f;
            _label.color = Color.white;
            _edge = labelGo.AddComponent<OverArtEdge>();
            _edge.Backing = backingGo.GetComponent<Image>();
            labelGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null) Object.Destroy(_canvas);
        }

        private IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                _label.ForceMeshUpdate();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AStaticLabelWritesTheBackingOnceThenNever()
        {
            yield return Frames(3);
            Assert.IsTrue(_backing.gameObject.activeSelf, "the plate shows once the label has text");
            int afterFirst = _edge.WriteCountForTest;
            Assert.GreaterOrEqual(afterFirst, 1);

            yield return Frames(60);
            Assert.AreEqual(afterFirst, _edge.WriteCountForTest, "60 static frames wrote the backing again");
        }

        [UnityTest]
        public IEnumerator ATextChangeWritesTheBackingThatFrame()
        {
            yield return Frames(3);
            int before = _edge.WriteCountForTest;
            Vector2 sizeBefore = _backing.sizeDelta;

            _label.text = "STANDING STANDING STANDING";
            yield return Frames(2);

            Assert.Greater(_edge.WriteCountForTest, before);
            Assert.Greater(_backing.sizeDelta.x, sizeBefore.x + 1f, "a longer line must widen the plate");
        }

        [UnityTest]
        public IEnumerator MovingAndScalingTheLabelWritesTheBackingWithoutATextChange()
        {
            yield return Frames(3);
            int before = _edge.WriteCountForTest;
            Vector2 posBefore = _backing.anchoredPosition;
            Vector2 sizeBefore = _backing.sizeDelta;

            _label.rectTransform.anchoredPosition += new Vector2(100f, 0f);
            yield return Frames(2);
            Assert.Greater(_edge.WriteCountForTest, before, "a moved label left its plate behind");
            Assert.AreEqual(posBefore.x + 100f, _backing.anchoredPosition.x, 0.05f);
            Assert.AreEqual(sizeBefore.x, _backing.sizeDelta.x, 0.05f);

            before = _edge.WriteCountForTest;
            _label.rectTransform.localScale = new Vector3(2f, 2f, 1f);
            yield return Frames(2);
            Assert.Greater(_edge.WriteCountForTest, before, "a scaled label left its plate unscaled");
            Assert.Greater(_backing.sizeDelta.x, sizeBefore.x * 1.5f);
        }

        [UnityTest]
        public IEnumerator DisablingAndReEnablingRestoresThePlate()
        {
            yield return Frames(3);
            Vector2 size = _backing.sizeDelta;
            Vector2 pos = _backing.anchoredPosition;

            _label.gameObject.SetActive(false);
            Assert.IsFalse(_backing.gameObject.activeSelf, "OnDisable hides the plate");
            _label.gameObject.SetActive(true);
            yield return Frames(3);

            Assert.IsTrue(_backing.gameObject.activeSelf);
            Assert.AreEqual(size.x, _backing.sizeDelta.x, 0.05f);
            Assert.AreEqual(size.y, _backing.sizeDelta.y, 0.05f);
            Assert.AreEqual(pos.x, _backing.anchoredPosition.x, 0.05f);
            Assert.AreEqual(pos.y, _backing.anchoredPosition.y, 0.05f);
        }
    }
}
