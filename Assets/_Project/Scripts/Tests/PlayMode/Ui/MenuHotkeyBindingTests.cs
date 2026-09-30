using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The hotkeys are Input Manager buttons, so a hotkey the table names and
    // ProjectSettings/InputManager.asset does not define would throw on the
    // first frame the dispatcher reads it -- in a player, not in a test that
    // scripts the input. This reads the file itself, the way
    // GamepadAxisBindingTests does for the D-pad and the triggers.
    public class MenuHotkeyBindingTests
    {
        private static List<Dictionary<string, string>> Axes()
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

                int colon = raw.IndexOf(':');
                if (current == null || colon < 0) continue;

                current[raw.Substring(0, colon).Trim()] = raw.Substring(colon + 1).Trim();
            }

            return blocks;
        }

        private static Dictionary<string, string> Only(string name)
        {
            var found = Axes().Where(a => a.TryGetValue("m_Name", out var n) && n == name).ToList();
            Assert.AreEqual(1, found.Count, $"InputManager.asset should define exactly one '{name}' axis");
            return found[0];
        }

        [Test]
        public void EveryMenuHotkeyTheTableNamesHasAnAxis()
        {
            Assert.GreaterOrEqual(SystemMenuTabs.Hotkeys.Count, 4, "the hotkey table is empty; this test would be vacuous");

            var defined = Axes().Select(a => a["m_Name"]).ToHashSet();
            foreach (var hotkey in SystemMenuTabs.Hotkeys)
            {
                Assert.IsTrue(defined.Contains(hotkey.Axis),
                    $"{hotkey.Axis} is read every frame but InputManager.asset does not define it");
            }
        }

        [TestCase("HotkeyC", "c")]
        [TestCase("HotkeyI", "i")]
        [TestCase("HotkeyP", "p")]
        [TestCase("HotkeyR", "r")]
        [TestCase("HotkeyG", "g")]
        public void AKeyboardHotkeyIsBoundToItsLetter(string axis, string key)
        {
            var entry = Only(axis);
            Assert.AreEqual(key, entry["positiveButton"]);
            Assert.AreEqual("0", entry["type"], $"{axis} should be a Key or Mouse Button entry");
        }

        // The Back/View button is the pad's 7th button under the legacy
        // mapping, serialized zero-based as joystick button 6; Start is 7 and
        // already drives SystemMenu.
        [Test]
        public void ViewIsBoundToJoystickButtonSix()
        {
            var view = Only("View");

            Assert.AreEqual("joystick button 6", view["altPositiveButton"]);
            Assert.AreEqual("0", view["type"]);
            Assert.AreEqual("joystick button 7", Only("SystemMenu")["altPositiveButton"],
                "fixture: Start is still joystick button 7");
        }
    }
}
