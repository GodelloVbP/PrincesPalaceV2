using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The intent icon reaches the screen.
    //
    // Headless-safe on purpose: every assertion here is about state, not
    // pixels, so the commit gate runs it. Whether the glyph is legible is a
    // screenshot question -- but "is it switched on, does it carry a character,
    // and can the font actually draw that character" are all answerable without
    // a graphics device, and the last one is the trap: a missing glyph renders
    // as nothing at all on a chromeless icon, which looks exactly like the
    // feature never shipped.
    public class EnemyIntentIconTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private IEnumerator OpenTheScene()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            for (int i = 0; i < 120; i++) yield return null;
        }

        // Binds an encounter of EXACTLY this many monsters.
        //
        // The scene's own bootstrap is not deterministic enough to test against:
        // with no run it fields three DISTINCT monsters (the three with art),
        // and with a run it fields whatever the room rolled -- which is how the
        // suite passed at three enemies while the actual screenshot, a two-rat
        // room, was missing a badge. A fixture states the count instead of
        // hoping for it.
        private IEnumerator BindEncounterOf(int enemyCount)
        {
            yield return OpenTheScene();

            var party = ContentDatabase.Characters.Take(1).Select(c => c.id).ToList();
            List<string> enemies = ContentDatabase.Enemies.Take(enemyCount).Select(e => e.id).ToList();
            Assert.AreEqual(enemyCount, enemies.Count, "content does not have enough enemies for this fixture");

            var built = FightEncounterAdapter.Build(party, enemies, new Domain.Rng.SeededRandom(11));
            _fight.Bind(built.Session, EncounterClass.Normal);

            yield return null;
            yield return null;
        }

        private FightSession Session()
        {
            var session = typeof(FightController)
                .GetField("_session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_fight) as FightSession;
            Assert.IsNotNull(session, "could not reach the fight session");
            return session;
        }

        private int LivingEnemyCount() => Session().Encounter.Enemies.Count(e => e.IsAlive);

        // NOT a name prefix on its own. UiEmitter synthesises a button's caption
        // as a child called "<name>Label", so "starts with EnemyIntent" matches
        // the badge AND its own caption and counts every icon twice -- the same
        // blind spot AUDIT.md #39 records, where a name-based lookup silently
        // picked up an object the screen tree never declared. Anchored on the
        // component that only the badge itself carries.
        private GameObject[] Icons() =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .Where(t => t.name.StartsWith("EnemyIntent") && t.GetComponent<HoverIndex>() != null)
                  .Select(t => t.gameObject)
                  .ToArray();

        [UnityTest]
        public IEnumerator EveryLivingEnemyShowsAnIntentIcon_AtOneEnemy() { yield return CheckBadges(1); }

        [UnityTest]
        public IEnumerator EveryLivingEnemyShowsAnIntentIcon_AtTwoEnemies() { yield return CheckBadges(2); }

        [UnityTest]
        public IEnumerator EveryLivingEnemyShowsAnIntentIcon_AtThreeEnemies() { yield return CheckBadges(3); }

        private IEnumerator CheckBadges(int enemyCount)
        {
            yield return BindEncounterOf(enemyCount);

            var icons = Icons();
            CollectionAssert.IsNotEmpty(icons, "the fight scene has no EnemyIntent icons at all");

            // THE PAIRING, which is the assertion a count alone cannot make.
            //
            // The badge list was once built with Add() inside a loop that walks
            // the slots FAR TO NEAR for painter's order, so it came out
            // reversed: badge 0 was parented to Enemy2Slot. At three monsters
            // the count still matched and everything looked right, while every
            // badge described a different monster than the one it floated over.
            // At two it silently vanished, because the front rat's badge hung
            // off a slot that was switched off.
            // Read the WIRED ARRAY, not a hierarchy walk. Slots are declared far
            // to near for painter's order, so GetComponentsInChildren returns
            // them reversed -- asserting on traversal order would just be
            // testing uGUI's sibling ordering.
            var wired = (GameObject[])typeof(FightController)
                .GetField("enemyIntentIcons", System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance)
                .GetValue(_fight);

            for (int i = 0; i < wired.Length; i++)
            {
                Assert.AreEqual($"Enemy{i}Slot", wired[i].transform.parent.name,
                    $"badge {i} hangs off '{wired[i].transform.parent.name}' - the badge list is not indexed by " +
                    "slot, so every badge is describing the wrong monster");
            }

            int living = _fight.GetComponentsInChildren<Transform>(true) == null ? 0 : LivingEnemyCount();
            int shown = icons.Count(g => g.activeInHierarchy);

            // ONE PER LIVING ENEMY, not "at least one". The weaker assertion
            // passed while only the back monster was badged, which is exactly
            // the state a player would report as "the icons are broken".
            Assert.AreEqual(living, shown,
                $"{living} living enemies but {shown} intent icons on screen - every monster needs its own");

            // ACTIVE IS NOT VISIBLE. A badge parked outside the canvas, or one
            // sitting exactly on top of another, satisfies every assertion above
            // and still shows the player nothing.
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            var canvasRect = (canvas.transform as RectTransform).rect;

            var centres = new List<Vector2>();
            foreach (var icon in icons.Where(g => g.activeInHierarchy))
            {
                var rect = (RectTransform)icon.transform;
                var world = canvas.transform.InverseTransformPoint(rect.position);

                Assert.IsTrue(canvasRect.Contains(new Vector2(world.x, world.y)),
                    $"'{icon.name}' sits at {world} which is outside the canvas {canvasRect} - it is switched on and off screen");

                foreach (var other in centres)
                {
                    Assert.Greater(Vector2.Distance(other, new Vector2(world.x, world.y)), 20f,
                        $"'{icon.name}' is stacked on another badge - two monsters would look like one");
                }

                centres.Add(new Vector2(world.x, world.y));
            }
        }

        // The artwork is the badge now, so THIS is the guarantee that matters:
        // every kind must resolve to a real sprite. Resources.Load returns null
        // silently for a missing file, a wrong path, or a PNG that imported as a
        // plain Texture rather than a Sprite -- three separate ways to end up
        // with an invisible badge and nothing in the log.
        [Test]
        public void EveryIntentKindResolvesToArtworkThatActuallyLoaded()
        {
            var missing = System.Enum.GetValues(typeof(EnemyIntentKind))
                .Cast<EnemyIntentKind>()
                .Where(k => Resources.Load<Sprite>(EnemyIntentIcons.ResourceFor(k)) == null)
                .Select(k => $"{k} -> {EnemyIntentIcons.ResourceFor(k)}")
                .ToList();

            Assert.IsEmpty(missing,
                "these intent kinds have no loadable sprite, so they render as nothing: " +
                string.Join(", ", missing) +
                ". Check the file exists under Assets/_Project/Resources/ and that " +
                "IntentIconImportPostprocessor gave it textureType Sprite.");
        }

        // The path convention, asserted rather than assumed. Resources.Load
        // returns null for an Assets/ path or one carrying an extension, and
        // says nothing about it -- the exact silent failure ArtPathConvention
        // exists to stop in content.
        [Test]
        public void TheIconPathsFollowTheRuntimeLoadingConvention()
        {
            foreach (EnemyIntentKind kind in System.Enum.GetValues(typeof(EnemyIntentKind)))
            {
                string path = EnemyIntentIcons.ResourceFor(kind);
                Assert.IsFalse(path.StartsWith("Assets/"), $"{kind}: '{path}' must be Resources-relative");
                Assert.IsFalse(path.EndsWith(".png"), $"{kind}: '{path}' must not carry a file extension");
            }
        }

        // The fallback only helps if it is drawable, and this font is a static
        // atlas that silently renders unknown characters as nothing -- which is
        // how the first version of this feature shipped invisible.
        [Test]
        public void TheTextFallbackUsesCharactersTheFontActuallyHas()
        {
            var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            if (font == null) Assert.Ignore("no TMP font asset loaded in this context");

            foreach (EnemyIntentKind kind in System.Enum.GetValues(typeof(EnemyIntentKind)))
            {
                foreach (char c in EnemyIntentIcons.For(kind))
                {
                    Assert.IsTrue(font.HasCharacter(c),
                        $"the fallback for {kind} uses '{c}' (U+{(int)c:X4}), which this font cannot draw");
                }
            }
        }
    }
}
