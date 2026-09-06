using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // CharacterVoice keys its table on the literal character id ("sheep"),
    // and characters.json owns that id, not this file. Takes(characterId,
    // line) degrades a miss to null by design (this file's own header: "a
    // character with no recorded voice simply makes no sound") -- which is
    // exactly why a rename in characters.json would leave Shawn voiceless
    // with no error anywhere else in the pipeline. This is that error.
    public class CharacterVoiceTests
    {
        [Test]
        public void EveryVoiceKeyIsALiveCharacterId()
        {
            foreach (string key in CharacterVoice.VoiceKeysForTest)
            {
                Assert.IsNotNull(ContentDatabase.GetCharacter(key),
                    $"CharacterVoice keys '{key}', but no character with that id exists in characters.json -- " +
                    "a rename would leave this character permanently voiceless with no error anywhere.");
            }
        }
    }
}
