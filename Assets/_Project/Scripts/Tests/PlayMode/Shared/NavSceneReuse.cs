using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE GAMEPAD-NAVIGATION HALF OF A SharedScene RESET, for the
    // *GamepadNavigationTests fixtures. SharedScene resets nothing inside a
    // reused scene (its own header); these are the two things every nav
    // fixture needs put back and that no screen's own buttons can reach.
    //
    // 1. The scripted input. A reused scene keeps its EventSystem and its
    //    NavigationInputModule, and the module keeps its private
    //    _lastMousePosition. A FRESH ScriptedBaseInput reads (0,0), which
    //    TrackInputDevice sees as the mouse moving and flips
    //    LastInputWasPad off for the next test. So the component already
    //    there is reused and returned to rest with its mouse position left
    //    where it was. The rate override (JourneyFixture.TakeOverInput's own
    //    comment has the measured gate) is set on every call: back-to-back
    //    tests on one module press within uGUI's stock 0.1s of each other.
    //
    // 2. Focus memory. A NavContext remembers its last selection by id
    //    (NavigationInputModule records it every frame) and a modal's context
    //    outlives its time on the stack, so a reused scene would hand the next
    //    test "where the last test left off" instead of the entry a fresh
    //    load lands on. ForgetFocusMemory clears every NavContext held by a
    //    component in the active scene, which is exactly a fresh load's state.
    //
    // EVERY RESET HERE, AND EVERY FIXTURE'S OWN, RUNS ONLY WHEN Reused. On a
    // fresh load they would be no-ops at best, and at worst they would supply
    // the very state a test is there to see production supply -- the menu's
    // default tab, the glossary's first category, the entry selection. Gated,
    // a forced-reload run (PP_SHARED_SCENE_RELOAD=1) checks exactly what the
    // fixtures checked before sharing.
    internal static class NavSceneReuse
    {
        private const float UnthrottledActionsPerSecond = 1e6f;

        private static Scene s_scene;
        private static string s_test;
        private static bool s_reused;

        // Whether this test is running on a scene an earlier test already
        // used -- the same Scene (by handle) the previous test noted, still
        // loaded. Decided once per test, on first read, which every fixture
        // here does straight after SharedScene.Ensure.
        public static bool Reused
        {
            get
            {
                string test = TestContext.CurrentContext.Test.ID;
                if (test != s_test)
                {
                    var active = SceneManager.GetActiveScene();
                    s_reused = active == s_scene && active.isLoaded;
                    s_scene = active;
                    s_test = test;
                }

                return s_reused;
            }
        }

        public static ScriptedBaseInput TakeOverInput()
        {
            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();

            var input = module.GetComponent<ScriptedBaseInput>();
            if (input == null) input = module.gameObject.AddComponent<ScriptedBaseInput>();
            else ReturnToRest(input);

            module.inputOverride = input;
            module.inputActionsPerSecond = UnthrottledActionsPerSecond;
            return input;
        }

        // From the fixture's [TearDown], in place of SharedScene.AfterTest:
        // also drops any axis the test left held, so the frames between this
        // test and the next do not keep feeding it to the reused module.
        public static void AfterTest(ScriptedBaseInput input)
        {
            SharedScene.AfterTest();
            if (input != null) ReturnToRest(input);
        }

        // Every level and edge back to zero; MousePosition deliberately kept
        // (point 1 above).
        public static void ReturnToRest(ScriptedBaseInput input)
        {
            input.ClearOneFrameFlags();
            input.MouseButton0Held = false;
            input.Horizontal = 0f;
            input.Vertical = 0f;
            input.TriggerLeft = 0f;
            input.TriggerRight = 0f;
        }

        // THE HUB'S MODALS, all closed through the hub's own public setters --
        // the one place several Hub-scene nav fixtures share. Anything a
        // single fixture opens beyond these (a dossier page, a party mode) is
        // that fixture's to put back.
        public static HubController CloseHubModals()
        {
            var hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            Assert.IsNotNull(hub, "the hub scene has no HubController");
            if (!Reused) return hub;

            // The menu remembers its tab across a close; a fresh load's Start()
            // selects the default one, so this does too.
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (menu != null && menu.IsOpen) menu.Close();
            if (menu != null) menu.Select(SystemMenuTabs.DefaultFor(menu.InDescent));
            if (hub.CharacterOverlayIsOpen) hub.SetCharacterOverlay(false);
            if (hub.GlossaryIsOpen) hub.SetGlossary(false);
            if (hub.DebugMenuIsOpen) hub.SetDebugMenu(false);
            return hub;
        }

        // Clears the selection too: with nothing selected and no memory, the
        // dispatcher's next Process() reselects the top context's entry, the
        // same path a fresh load's first frame takes. Call it AFTER the
        // fixture has closed whatever it opened, so the context it lands on
        // is the screen's root.
        public static void ForgetFocusMemory()
        {
            if (!Reused) return;
            EventSystem.current?.SetSelectedGameObject(null);

            var active = SceneManager.GetActiveScene();
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.gameObject.scene != active) continue;
                for (var type = behaviour.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                {
                    foreach (var field in type.GetFields(Fields | BindingFlags.DeclaredOnly))
                    {
                        if (field.FieldType == typeof(NavContext)) (field.GetValue(behaviour) as NavContext)?.Remember(null);
                    }
                }
            }
        }
    }
}
