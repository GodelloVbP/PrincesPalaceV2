using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // Balance-bot handoff, PART B: "verify with a grep/test that no Button node
    // in any screen tree resolves to the fallback sprite". UiEmitter.EmitButton
    // resolves a Button to SceneBuilder.ButtonSprite() (button_cropped.png, the
    // gold-outline placeholder frame) exactly when node.Theme is null AND
    // node.SpriteKey is empty AND node.Chromeless is false -- see EmitButton's
    // own branch, `sprite = Chromeless ? null : SpriteKey empty ? ButtonSprite()
    // : LoadSpriteByKey(SpriteKey)`. Restated here rather than referenced,
    // because UiEmitter lives in the Editor assembly and this suite (Domain-
    // only, per docs/CODE_STANDARDS.md "Layering") cannot see it.
    //
    // Every screen is built straight from its own static Build(), not through
    // ScreenRegistry -- that registry is Editor-only (UnityEditor/UnityEngine
    // references), and Tests/EditMode is constrained to Domain. Screens whose
    // Build() needs a runtime count (MainMenuScreen's slot roster) are given
    // the same authored count MainMenuScreenTests uses.
    public class ButtonFallbackLintTests
    {
        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in Walk(child)) yield return descendant;
            }
        }

        private static IEnumerable<UiNode> AllScreenRoots()
        {
            yield return CharacterDossierScreen.Build().Root;
            yield return DebugMenuScreen.Build().Root;
            yield return DefeatScreen.Build().Root;
            yield return ExitsScreen.Build().Root;
            yield return FightScreen.Build().Root;
            yield return GlossaryScreen.Build().Root;
            yield return HubScreen.Build().Root;
            yield return MainMenuScreen.Build(new MainMenuInputs(5)).Root;
            yield return MapScreen.Build().Root;
            yield return OptionsScreen.Build().Root;
            yield return ReckoningScreen.Build().Root;
            yield return RelicDraftScreen.Build().Root;
            yield return RewardTrackScreen.Build().Root;
            yield return RunStatsScreen.Build().Root;
            yield return SystemMenuScreen.Build().Root;
            yield return TalentScreen.Build().Root;
        }

        // Mirrors UiEmitter.EmitButton's fallback condition exactly.
        private static bool ResolvesToFallbackPlate(UiNode node) =>
            node.Kind == UiNodeKind.Button
            && !node.Theme.HasValue
            && string.IsNullOrEmpty(node.SpriteKey)
            && !node.Chromeless;

        [Test]
        public void NoButtonInAnyScreenResolvesToTheGoldFallbackPlate()
        {
            var offenders = new List<string>();

            foreach (var root in AllScreenRoots())
            {
                foreach (var node in Walk(root))
                {
                    if (ResolvesToFallbackPlate(node))
                    {
                        offenders.Add($"{root.Name}/{node.Name}");
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "these buttons carry no Theme, no SpriteKey and are not NoChrome, so UiEmitter.EmitButton " +
                "falls them back to the shared button_cropped.png gold plate -- give each one a .Themed(...)/" +
                ".ThemedPlate(...), a real SpriteKey, or .NoChrome() if it should draw nothing at all: " +
                string.Join(", ", offenders));
        }
    }
}
