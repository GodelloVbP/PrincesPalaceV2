using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // A character's dossier portrait comes from content and Resources, and from
    // nothing else.
    //
    // WHAT THIS REPLACES, and why the replacement is not the same shape. The
    // portrait used to be an IconEntry[] that ScreenRegistry filled at
    // scene-build time from every character with a portraitPath. Nothing
    // pinned it: it was covered only by whatever happened to open the Character
    // pane, and the failure it allowed was silent -- a character authored after
    // the last scene build rendered a correct name, correct attributes and an
    // empty plate, with no amount of content rebuilding able to fill it
    // (docs/measurements/2026-09-step2-spell-character.md, exercise 3).
    //
    // THE ACCEPTANCE IS THAT NO SCENE IS INVOLVED. Every test below runs in a
    // PlayMode process that never loads the Hub, so nothing was ever baked; if
    // a portrait resolves here it resolved from ContentDatabase plus
    // Resources, which is exactly the route a character authored five minutes
    // ago takes.
    public class CharacterPortraitTests
    {
        [SetUp]
        public void DropTheCache()
        {
            // Not because anything here dirties it, but because a previous
            // fixture that installed its own roster would leave this one
            // reading that roster's faces.
            ContentDatabase.Reset();
        }

        [Test]
        public void EveryCharacterThatAuthorsAPortraitPathGetsASprite()
        {
            var authored = ContentDatabase.Characters
                .Where(c => c != null && c.Data != null && !string.IsNullOrWhiteSpace(c.Data.PortraitPath))
                .ToList();

            // VACUITY GUARD. A roster where nobody authored a portrait would
            // pass the loop below without loading a single sprite, which is
            // precisely the state this test exists to notice.
            Assert.GreaterOrEqual(authored.Count, 1,
                "No character in content authors a portraitPath, so the loop below proves nothing. " +
                "Either the roster lost its art or portraitPath stopped being read.");

            foreach (var character in authored)
            {
                Assert.IsNotNull(CharacterPortraits.For(character.id),
                    $"Character '{character.id}' names portraitPath '{character.Data.PortraitPath}' and it " +
                    "loaded nothing. Resources-relative, no extension, and the file has to sit under " +
                    "Assets/_Project/Resources/ -- see PortraitImportPostprocessor for the import settings " +
                    "that make Resources.Load<Sprite> return a Sprite rather than null.");
            }
        }

        // The same sprite the naive route returns, which is the check that the
        // cache and the content lookup have not quietly diverged from the path
        // the author typed.
        [Test]
        public void ThePortraitIsTheOneThePathNames()
        {
            var character = ContentDatabase.Characters
                .FirstOrDefault(c => c != null && c.Data != null && !string.IsNullOrWhiteSpace(c.Data.PortraitPath));
            Assert.IsNotNull(character, "fixture: content has at least one character with a portraitPath");

            var direct = Resources.Load<Sprite>(character.Data.PortraitPath.Trim());
            Assert.AreSame(direct, CharacterPortraits.For(character.id));
        }

        // Asked twice, loaded once. The dossier asks on every character switch.
        [Test]
        public void TheAnswerIsCachedRatherThanReloaded()
        {
            var character = ContentDatabase.Characters
                .FirstOrDefault(c => c != null && c.Data != null && !string.IsNullOrWhiteSpace(c.Data.PortraitPath));
            Assert.IsNotNull(character, "fixture: content has at least one character with a portraitPath");

            Assert.AreSame(CharacterPortraits.For(character.id), CharacterPortraits.For(character.id));
        }

        // Art is optional everywhere in this project, so "no portrait" is an
        // authoring choice and not an error. The dossier keeps its armour stand.
        [Test]
        public void AnUnknownCharacterIsNullRatherThanAThrow()
        {
            Assert.IsNull(CharacterPortraits.For("no_such_character"));
            Assert.IsNull(CharacterPortraits.For(""));
            Assert.IsNull(CharacterPortraits.For(null));
        }

        // DIAGNOSTIC, not the guarantee -- the guarantee is the first test,
        // which resolves a portrait with no scene in the process at all. This
        // one catches the specific regression of somebody re-adding a baked
        // roster beside the runtime one, where both would work and the baked
        // half would silently win for characters it happened to have.
        [Test]
        public void TheDossierCarriesNoBakedRosterOfFaces()
        {
            var baked = typeof(CharacterDossierController)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(IconEntry[]) && f.Name.ToLowerInvariant().Contains("portrait"))
                .Select(f => f.Name)
                .ToList();

            Assert.IsEmpty(baked,
                "CharacterDossierController has an IconEntry[] of portraits again: " + string.Join(", ", baked) +
                ". A baked array is a photograph of the roster taken at scene-build time; portraits load " +
                "through CharacterPortraits so a character authored between scene builds still has a face.");
        }
    }
}
