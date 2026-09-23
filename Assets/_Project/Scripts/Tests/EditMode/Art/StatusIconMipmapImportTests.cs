using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PrincesPalace.Domain.Tests
{
    // Pins StatusIconImportPostprocessor's mip/filter settings so a future
    // "just disable mips, it looked grainy once" edit -- or an importer
    // reset that silently reverts them -- fails loudly instead of quietly
    // re-introducing the aliasing the owner reported (2026-09-23): 256px
    // masters drawn at 20px on party plates and 36px on enemy rows need
    // trilinear-filtered mips, not a same-size bilinear sample.
    //
    // Reads the importer actually attached to a real asset under
    // Resources/Status/ rather than re-deriving the expected values, so this
    // fails the moment the postprocessor's OnPreprocessTexture stops setting
    // them -- the same trap IntentIconImportPostprocessor's sibling class
    // would hit if its own mips got flipped back off with nothing to catch it.
    public class StatusIconMipmapImportTests
    {
        private const string SampleStatusIconPath = "Assets/_Project/Resources/Status/chilled.png";

        [Test]
        public void StatusIcon_ImportsWithMipmapsAndTrilinearFiltering()
        {
            var importer = AssetImporter.GetAtPath(SampleStatusIconPath) as TextureImporter;
            Assert.IsNotNull(importer, $"no TextureImporter at '{SampleStatusIconPath}' -- fixture path moved?");

            Assert.IsTrue(importer.mipmapEnabled,
                "StatusIconImportPostprocessor must enable mips: these 256px masters draw at 20-36px " +
                "on screen, and without mips that minification aliases into visible grain.");
            Assert.AreEqual(FilterMode.Trilinear, importer.filterMode,
                "StatusIconImportPostprocessor must use Trilinear filtering to actually blend between " +
                "mip levels, matching UiKitImportPostprocessor and PortraitImportPostprocessor.");
        }
    }
}
