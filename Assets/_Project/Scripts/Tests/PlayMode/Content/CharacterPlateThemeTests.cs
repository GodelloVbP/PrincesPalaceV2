using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // WHAT COLOUR EACH SHIPPED CHARACTER ACTUALLY IS, read back off the built
    // asset rather than off characters.json.
    //
    // The theme makes the same two hops every other content field does --
    // JSON to a ScriptableObject via ContentBuilder, asset back to a
    // ResolvedCharacter when a fight starts -- and this is the hop
    // CharacterEntryResolverTests cannot see, because it stops at the
    // resolver. A field dropped from the field-for-field copy on either side
    // would leave every character wearing default(ButtonTheme), which is
    // Gold, which looks authored.
    //
    // BY ID AND AS LITERALS: sheep Silver, bear Crimson, owl Blue. Bear was
    // Gold until 2026-09-10 -- the owner's call, at the same time the theme
    // stopped being a frame sprite and became the character's identity
    // colour (Bjorn is the party's red).
    public class CharacterPlateThemeTests
    {
        [TestCase("sheep", ButtonTheme.Silver)]
        [TestCase("bear", ButtonTheme.Crimson)]
        [TestCase("owl", ButtonTheme.Blue)]
        public void EachShippedCharacterWearsItsAuthoredIdentityColour(string id, ButtonTheme expected)
        {
            var definition = ContentDatabase.GetCharacter(id);
            Assert.IsNotNull(definition, $"'{id}' is not in the built content");

            Assert.AreEqual(expected, definition.Data.PlateTheme,
                $"'{id}' lost or changed its plateTheme somewhere between characters.json and the asset");
        }

        // THE THREE ARE MUTUALLY DISTINCT, which is the property the feature
        // actually depends on -- three cards side by side in the HUD column,
        // each meant to be recognisable as its occupant. Asserted rather than
        // inferred from the three cases above, because a future roster with
        // two Blues would still pass those individually.
        [Test]
        public void NoTwoShippedCharactersShareAnIdentityColour()
        {
            var characters = ContentDatabase.Characters;
            Assert.IsNotEmpty(characters, "no characters in content, so this guards nothing");

            for (int i = 0; i < characters.Count; i++)
            {
                for (int j = i + 1; j < characters.Count; j++)
                {
                    Assert.AreNotEqual(characters[i].Data.PlateTheme, characters[j].Data.PlateTheme,
                        $"'{characters[i].id}' and '{characters[j].id}' both wear " +
                        $"{characters[i].Data.PlateTheme} -- their HUD cards would be indistinguishable");
                }
            }
        }
    }
}
