using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // NavContext.LazyEntry: off keeps the dispatcher's "something is always
    // selected" rule; on leaves a null selection null until the first move
    // press, which focuses the entry without also stepping off it.
    public class LazyEntryNavigationTests
    {
        private GameObject _root;
        private ScriptedBaseInput _input;
        private Button _a;
        private Button _b;
        private Button _stray;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("LazyEntryFixture");
            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(NavigationInputModule));
            esGO.transform.SetParent(_root.transform);
            var module = esGO.GetComponent<NavigationInputModule>();
            _input = esGO.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;
            // Newest-enabled EventSystem is not current; a stray one from an
            // earlier fixture would starve Process() (NavigationDispatcherTests).
            EventSystem.current = esGO.GetComponent<EventSystem>();

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(_root.transform);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            _a = MakeButton(canvasGO.transform, "A", new Vector2(-150f, 0f));
            _b = MakeButton(canvasGO.transform, "B", new Vector2(150f, 0f));
            _stray = MakeButton(canvasGO.transform, "Stray", new Vector2(0f, 200f));
            var navA = _a.navigation;
            navA.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            navA.selectOnRight = _b;
            _a.navigation = navA;
            var navB = _b.navigation;
            navB.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            navB.selectOnLeft = _a;
            _b.navigation = navB;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        private static Button MakeButton(Transform parent, string name, Vector2 at)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.anchoredPosition = at;
            return go.GetComponent<Button>();
        }

        private NavContext Push(bool lazy)
        {
            var context = new NavContext(_a.gameObject,
                new Dictionary<string, object> { { "A", _a.gameObject }, { "B", _b.gameObject } }, cancel: null)
            { LazyEntry = lazy };
            NavigationInputModule.Contexts.Push(context);
            return context;
        }

        private GameObject Selected => EventSystem.current.currentSelectedGameObject;

        private IEnumerator Frame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        [UnityTest]
        public IEnumerator WithTheFlagOffTheEntryIsSelectedAtOnce()
        {
            Push(lazy: false);
            yield return Frame();

            Assert.AreSame(_a.gameObject, Selected);
        }

        [UnityTest]
        public IEnumerator WithTheFlagOnNothingIsSelectedUntilAMovePress()
        {
            Push(lazy: true);
            yield return Frame();
            yield return Frame();

            Assert.IsNull(Selected);
        }

        [UnityTest]
        public IEnumerator ALazyContextClearsAStraySelectionItDoesNotDeclare()
        {
            Push(lazy: true);
            EventSystem.current.SetSelectedGameObject(_stray.gameObject);
            yield return Frame();

            Assert.IsNull(Selected);
        }

        [UnityTest]
        public IEnumerator TheFirstMovePressFocusesTheEntryAndDoesNotStepOffIt()
        {
            Push(lazy: true);
            yield return Frame();

            _input.Horizontal = 1f;
            yield return Frame();
            Assert.AreSame(_a.gameObject, Selected, "the first press should focus the entry, not move to B");

            _input.Horizontal = 0f;
            yield return Frame();
            yield return new WaitForSecondsRealtime(0.4f); // uGUI's own repeat gate
            _input.Horizontal = 1f;
            yield return Frame();
            Assert.AreSame(_b.gameObject, Selected, "ordinary navigation continues from the entry");
        }
    }
}
