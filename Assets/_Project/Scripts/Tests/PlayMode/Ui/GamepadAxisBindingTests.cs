using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // HARDWARE PLAY-TEST ROUND 1, ITEM 4: "arrow buttons should work as well
    // as joystick."
    //
    // Two readings, and both are here, but they are proven in different
    // places because only one of them is a thing code can see.
    //
    // WHAT A SCRIPTED BaseInput CANNOT PROVE, said plainly rather than faked:
    // the legacy Input system merges every axis sharing a name BEFORE any
    // module reads it -- Input.GetAxisRaw("Vertical") returns the largest
    // magnitude across all entries called Vertical. So by the time a value
    // reaches NavigationInputModule there is no such thing as "a D-pad frame"
    // or "an arrow frame"; there is one number. A test that set
    // ScriptedBaseInput.Vertical = 1 and called it a D-pad press would be
    // asserting its own fiction. The dispatch half is therefore pinned once,
    // as "a full-deflection frame moves exactly once"
    // (StickThresholdGamepadNavigationTests owns the held-stick cases), and
    // the BINDING half -- the part that was actually missing -- is pinned
    // here against ProjectSettings/InputManager.asset itself.
    //
    // WHAT COULD NOT BE VERIFIED AT ALL WITHOUT A PAD IN HAND, and is stated
    // rather than asserted: that the 6th and 7th joystick axes really are the
    // D-pad on the owner's controllers, and that up on the 7th reads
    // positive. Those are facts about drivers, not about this project. The
    // Xbox-on-Windows mapping is what the entries below encode; if up and
    // down come out swapped on the pad, `invert` on the Vertical entry is the
    // one-line fix. A DualSense reports the same two axes under Windows raw
    // input in Unity's legacy system, but nothing in this repository can
    // confirm that and this test does not pretend to.
    public class GamepadAxisBindingTests
    {
        // Every axis block in InputManager.asset, as a name -> list of
        // field maps. Parsed rather than reached for through UnityEditor:
        // this is a PlayMode test and the file is plain YAML.
        private static List<Dictionary<string, string>> AxesNamed(string name)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "InputManager.asset"));
            Assert.IsTrue(File.Exists(path), "no ProjectSettings/InputManager.asset beside this project's Assets folder");

            var blocks = new List<Dictionary<string, string>>();
            Dictionary<string, string> current = null;

            foreach (var raw in File.ReadAllLines(path))
            {
                if (raw.TrimStart().StartsWith("- serializedVersion:"))
                {
                    current = new Dictionary<string, string>();
                    blocks.Add(current);
                    continue;
                }

                if (current == null) continue;

                int colon = raw.IndexOf(':');
                if (colon < 0) continue;

                var key = raw.Substring(0, colon).Trim();
                var value = raw.Substring(colon + 1).Trim();
                current[key] = value;
            }

            return blocks.Where(b => b.TryGetValue("m_Name", out var n) && n == name).ToList();
        }

        private static bool IsJoystickAxis(Dictionary<string, string> axis, string index) =>
            axis.TryGetValue("type", out var type) && type == "2"
            && axis.TryGetValue("axis", out var a) && a == index;

        // `axis:` is ZERO-BASED in the serialized file while the inspector
        // names the same field one-based ("6th axis"), which is exactly the
        // off-by-one that would silently bind a trigger instead of a hat.
        // The two entries this project already had are the anchor: the left
        // stick is axis 0 and axis 1, and the inspector calls those X and Y.
        // So the 6th and 7th axes -- D-Pad X and D-Pad Y on an Xbox pad under
        // Windows -- are 5 and 6 here.
        private const string DpadXAxis = "5";
        private const string DpadYAxis = "6";

        [Test]
        public void TheLeftStickIsStillBoundAndIsWhatTheZeroBasedReadingIsAnchoredOn()
        {
            Assert.IsTrue(AxesNamed("Horizontal").Any(a => IsJoystickAxis(a, "0")),
                "the left stick's X is the 1st axis, serialized 0");
            Assert.IsTrue(AxesNamed("Vertical").Any(a => IsJoystickAxis(a, "1")),
                "the left stick's Y is the 2nd axis, serialized 1");
        }

        [Test]
        public void TheDpadIsBoundToHorizontalAndVertical_SoItReachesTheSameDispatcherTheStickDoes()
        {
            var horizontal = AxesNamed("Horizontal").FirstOrDefault(a => IsJoystickAxis(a, DpadXAxis));
            Assert.IsNotNull(horizontal,
                "Horizontal needs a Joystick Axis entry on the 6th axis (D-Pad X) -- without it the D-pad " +
                "reaches nothing at all, which is the whole of the owner's report");

            var vertical = AxesNamed("Vertical").FirstOrDefault(a => IsJoystickAxis(a, DpadYAxis));
            Assert.IsNotNull(vertical, "Vertical needs a Joystick Axis entry on the 7th axis (D-Pad Y)");

            // NOT INVERTED, unlike the left stick's own Y entry. The stick's
            // raw Y reads negative when pushed up and so carries invert: 1;
            // the D-pad hat reports up as positive under Windows. This is the
            // one line to flip if up and down come out swapped on the pad --
            // see this file's own header on why no test can settle it.
            Assert.AreEqual("0", vertical["invert"],
                "the D-pad hat reports up as positive, so it must NOT carry the left stick's invert");
        }

        // THE OFF-BY-ONE THIS FILE ALREADY NAMES, on two more entries. The
        // Inspector calls the triggers the 9th and 10th axes on an Xbox pad
        // under Windows, so serialized they are 8 and 9 -- anchored on the
        // left stick above exactly as the D-pad's are. The 3rd axis is the
        // COMBINED trigger (LT positive, RT negative) and is deliberately not
        // used: one axis cannot say that both are pulled, and a section step
        // wants a per-trigger edge.
        private const string TriggerLeftAxisIndex = "8";
        private const string TriggerRightAxisIndex = "9";

        [Test]
        public void TheTriggersAreBoundAsSeparateAxes_NotAsTheCombinedOne()
        {
            var left = AxesNamed("TriggerLeft").FirstOrDefault(a => IsJoystickAxis(a, TriggerLeftAxisIndex));
            Assert.IsNotNull(left,
                "TriggerLeft needs a Joystick Axis entry on the 9th axis (LT) -- without it a trigger pull " +
                "reaches nothing, and NavigationInputModule's section step can never fire");

            var right = AxesNamed("TriggerRight").FirstOrDefault(a => IsJoystickAxis(a, TriggerRightAxisIndex));
            Assert.IsNotNull(right, "TriggerRight needs a Joystick Axis entry on the 10th axis (RT)");

            Assert.AreNotEqual("2", left["axis"],
                "the 3rd axis is the COMBINED trigger -- binding it would make LT and RT the same control");
        }

        // START, and the fact that ESCAPE DRIVES IT TOO. The second half is
        // not incidental: escape is also Cancel's positiveButton, which is
        // the whole reason NavigationInputModule has to decide which of the
        // two a frame's press was spent on rather than letting both run.
        [Test]
        public void SystemMenuIsBoundToStartAndToEscape()
        {
            var entry = AxesNamed("SystemMenu").FirstOrDefault();
            Assert.IsNotNull(entry, "ProjectSettings/InputManager.asset has no SystemMenu axis at all");
            Assert.AreEqual("escape", entry["positiveButton"],
                "a keyboard player needs escape to reach the menu, same key it always was");
            Assert.AreEqual("joystick button 7", entry["altPositiveButton"],
                "Start on an Xbox pad under Windows -- the owner's 2026-09-19 call, unverified on hardware " +
                "(AUDIT.md #166)");

            var cancel = AxesNamed("Cancel").FirstOrDefault(a => a["positiveButton"] == "escape");
            Assert.IsNotNull(cancel,
                "Cancel still binds escape, which is why the dispatcher spends a frame on one axis or the " +
                "other -- if this ever stops being true, NavigationInputModule's guard can be dropped");
            Assert.AreNotEqual("joystick button 7", cancel["altPositiveButton"],
                "Start must not also be Cancel on a pad, or B and Start would mean the same thing again");
        }

        [Test]
        public void TheKeyboardArrowsAreStillBound()
        {
            var horizontal = AxesNamed("Horizontal").FirstOrDefault(a => a["type"] == "0");
            Assert.IsNotNull(horizontal, "Horizontal needs a key entry for the arrow keys");
            Assert.AreEqual("left", horizontal["negativeButton"]);
            Assert.AreEqual("right", horizontal["positiveButton"]);

            var vertical = AxesNamed("Vertical").FirstOrDefault(a => a["type"] == "0");
            Assert.IsNotNull(vertical, "Vertical needs a key entry for the arrow keys");
            Assert.AreEqual("down", vertical["negativeButton"]);
            Assert.AreEqual("up", vertical["positiveButton"]);
        }

        // THE JOIN between the two halves, and the reason the D-pad needed no
        // code at all: every branch of the dispatcher reads these two NAMES,
        // so anything the legacy system merges under them arrives at the same
        // place. Fight's own branch reads verticalAxis directly
        // (NavigationInputModule.ProcessFight), which is why the entries
        // above cover the fight column as well as every menu.
        [UnityTest]
        public IEnumerator TheDispatcherReadsTheAxesTheseEntriesAreNamedFor()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");

            Assert.AreEqual("Horizontal", module.horizontalAxis,
                "the dispatcher's horizontal axis name must be the one InputManager.asset binds the D-pad to");
            Assert.AreEqual("Vertical", module.verticalAxis,
                "and likewise vertical, which is also what ProcessFight's own read uses");
        }

        // A FULL-DEFLECTION FRAME -- which is the only kind a D-pad or an
        // arrow key ever produces -- moves exactly once. This is the half a
        // scripted BaseInput genuinely can prove, and it is the same assertion
        // for both devices because by this point they are the same number.
        [UnityTest]
        public IEnumerator AFullDeflectionFrame_MovesExactlyOnce()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            var input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = input;

            yield return null;
            yield return null;

            Assert.AreEqual("StartRunGate", EventSystem.current.currentSelectedGameObject.name,
                "fixture: the hub opens on the gate");

            // Held for a real second, the way a finger holds a D-pad.
            input.Horizontal = 1f;
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                input.ClearOneFrameFlags();
            }
            input.Horizontal = 0f;
            yield return null;

            Assert.AreEqual("RelicsBuilding", EventSystem.current.currentSelectedGameObject.name,
                "one press of a digital control is one Move along the hub's ring, not a slide around it");
        }
    }
}
