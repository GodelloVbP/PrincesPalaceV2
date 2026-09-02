using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Proves UiEmitter.ApplyTypography (Editor/SceneBuilder/UiEmitter.cs) --
    // the single resolution point EmitLabel and a themed button's own label
    // both go through -- actually reached the built scene.
    //
    // Reads the real MainMenu scene rather than calling UiEmitter directly:
    // UiEmitter lives in PrincesPalace.Editor, which no test assembly may
    // reference (CODE_STANDARDS.md 1, the same reason TypographyAssetTests
    // loads its assets by AssetDatabase path instead of through
    // SceneBuilder.FontFor/MaterialFor). This is the PlayMode half of that
    // same split: what the Editor-only resolver actually produced, seen the
    // way a player's build sees it.
    public class TypographyMigrationTests
    {
        private static IEnumerator LoadMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static TMP_Text FindText(string name) =>
            Resources.FindObjectsOfTypeAll<TMP_Text>()
                .FirstOrDefault(t => t.name == name && t.gameObject.scene.IsValid());

        [UnityTest]
        public IEnumerator ARoledLabel_UsesItsRolesFontAndMaterial()
        {
            yield return LoadMainMenu();

            var spec = Typography.Specs[TypographyRole.CeremonialTitle];
            var title = FindText("TitleLabel");

            Assert.IsNotNull(title, "MainMenuScreen's TitleLabel should exist in the built scene");
            Assert.AreEqual(spec.FontAssetName, title.font.name,
                "TitleLabel is Styled(CeremonialTitle) - it should wear that role's font, not SceneBuilder.UiFont");
            Assert.AreEqual(spec.MaterialName, title.fontSharedMaterial.name,
                "TitleLabel should wear the CeremonialTitle material preset");
        }

        [UnityTest]
        public IEnumerator ARoledLabel_BakesItsUppercaseTextWithoutTouchingTheSourceUiString()
        {
            yield return LoadMainMenu();

            var title = FindText("TitleLabel");

            Assert.AreEqual(UiStrings.GameTitle.Format().ToUpperInvariant(), title.text,
                "CeremonialTitle upper-cases - the BAKED text should read uppercase");
            Assert.AreEqual("Prince's Palace", UiStrings.GameTitle.Format(),
                "the source UiString itself must stay exactly as authored - only the emitted TMP text is transformed");
        }

        [UnityTest]
        public IEnumerator AThemedButtonsLabel_UsesButtonLabelsFontAndMaterial_AndIsUppercase()
        {
            yield return LoadMainMenu();

            var spec = Typography.Specs[TypographyRole.ButtonLabel];
            var play = FindText("PlayButtonLabel");

            Assert.IsNotNull(play, "PlayButton is Themed(), so it should carry a PlayButtonLabel child");
            Assert.AreEqual(spec.FontAssetName, play.font.name);
            Assert.AreEqual(spec.MaterialName, play.fontSharedMaterial.name);
            Assert.AreEqual(UiStrings.Play.Format().ToUpperInvariant(), play.text);
        }

        [UnityTest]
        public IEnumerator AnUnroledLabel_StillUsesUiFontWithItsDefaultMaterial()
        {
            yield return LoadMainMenu();

            // Slot0Number is authored with no .Styled() call - AddCardContent
            // never migrated. It must keep the exact pre-migration path.
            var number = FindText("Slot0Number");

            Assert.IsNotNull(number, "the first save slot's number badge should exist");
            Assert.AreNotEqual(Typography.Specs[TypographyRole.ButtonLabel].FontAssetName, number.font.name,
                "an unroled label must not pick up a typography role's font");
            Assert.AreEqual(number.font.material, number.fontSharedMaterial,
                "an unroled label keeps the font's own default material - ApplyTypography never touches it");
        }
    }
}
