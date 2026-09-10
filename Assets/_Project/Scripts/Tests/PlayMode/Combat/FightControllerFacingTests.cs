using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // BUG HUNT FINDING 1 (2026-09-10): FightController.FacingOf never read
    // ResolvedCharacter.BattleSpriteFacing for a party combatant -- it fell
    // straight through SourceFor (enemy-only, see FightSession.Enemies.cs's
    // own note) to a hardcoded SpriteFacing.Right, with a comment claiming
    // that fallback was unreachable for a content-backed combatant. False
    // for the whole party: every one of them hit it, on every repaint. All
    // three shipped characters (sheep/bear/owl) happen to be authored
    // "Right" -- the same value as the hardcoded fallback -- so the bug
    // produced the correct picture by coincidence and stayed invisible.
    //
    // The fix threads BattleSpriteFacing through PlayerKit
    // (Domain/Combat/Session/CombatantKit.cs) the same seam PlateArt already
    // travels through, rather than a parallel array threaded beside
    // FightEncounterAdapter.PartyArt -- FacingOf now reads it off
    // FightSession.KitFor(combatant) for the party side.
    //
    // AGAINST A HAND-BUILT SESSION, not real content: every shipped
    // character is authored Right, so proving the Left case needs a kit
    // built with SpriteFacing.Left directly rather than a characters.json
    // edit that would change real game content for one test's sake. The
    // stand-up mirrors TransformFlashTests.StandTheFightUp -- load Fight
    // once (no save/run needed; FightBootstrap's own placeholder fight is
    // flushed and replaced), then FightEncounterAdapter.Build a real
    // one-member party so the encounter, kit and art are all real, and swap
    // only that member's PlayerKit for a copy carrying the facing under
    // test before handing the session to FightController.Bind.
    public class FightControllerFacingTests
    {
        private const ulong Seed = 20260910UL;

        private FightController _fight;
        private FightBeatPlayer _beats;

        [SetUp]
        public void Hurry()
        {
            FightController.BreathSpeedMultiplier = 0f;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            // Same reasoning as TransformFlashTests' own [UnityTearDown]
            // (AUDIT #106): this fixture's fights are left standing rather
            // than played to a close, so flush whatever beat player is live
            // before the next fixture's scene load finds it mid-playback.
            LogAssert.ignoreFailingMessages = true;
            if (_beats != null) _beats.EndFight();
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            FightController.BreathSpeedMultiplier = 1f;
        }

        // ---- the stand-up ---------------------------------------------------

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        // ONE real party member (Shawn/sheep, real content, real art on
        // disk), rebound with the facing under test. Returns the Image
        // drawing him on stage so the caller can read its mirror.
        private IEnumerator StandTheFightUpFacing(SpriteFacing facing)
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the fight scene has no controller");

            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the fight scene has no beat player");

            // FightBootstrap's own placeholder fight is mid-beat and holds
            // the controller busy, exactly as TransformFlashTests' stand-up
            // notes.
            _beats.Flush();
            yield return null;
            yield return null;

            var enemyIds = PreviewFight.EnemiesWithArt(1);
            Assert.IsNotEmpty(enemyIds, "no enemies in content to stand a fight up against");

            var built = FightEncounterAdapter.Build(
                new List<string> { "sheep" },
                enemyIds,
                new SeededRandom(Seed));

            Assert.IsNotNull(built?.Session, "sheep vs. " + string.Join(", ", enemyIds) +
                                              " could not be built into an encounter");
            Assert.AreEqual(1, built.Party.Count, "expected exactly the one party member this fixture asked for");

            // THE SWAP: a copy of the real kit FightEncounterAdapter.KitFor
            // built, with only Facing overridden. Everything else -- skills,
            // relics, plate identity -- stays real, so this proves facing
            // specifically, not a fixture standing in for the whole kit.
            var realKit = built.Session.KitFor(built.Party[0]);
            Assert.IsNotNull(realKit, "FightEncounterAdapter did not bind a kit for the party member it just built");

            var facedKit = new PlayerKit(realKit.Id, realKit.Role, realKit.Skills, realKit.Relics,
                realKit.AttackType, realKit.Level, realKit.SkillPowerMultiplier,
                realKit.PlateTheme, realKit.PlateArt, facing);

            var enemyKits = built.Session.Encounter.Enemies
                .Select(e => built.Session.SourceFor(e))
                .ToList();

            var session = new FightSession(built.Session.Encounter, new List<PlayerKit> { facedKit }, enemyKits,
                new SeededRandom(Seed));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;
        }

        private Image PartySprite()
        {
            var image = Named("Party0Sprite")?.GetComponent<Image>();
            Assert.IsNotNull(image, "no Party0Sprite in the fight scene");
            Assert.IsNotNull(image.sprite, "Party0Sprite has no sprite -- sheep never loaded from " +
                                            "Resources/Characters/sheep, so this cannot tell a mirror bug from missing art");
            Assert.AreNotSame(_fight.FallbackSprite, image.sprite,
                "sheep fell back to the plate -- the mirror this test reads is never applied to a fallback plate");
            return image;
        }

        // ---- the finding ------------------------------------------------------

        // THE REGRESSION THIS FINDING REPORTED: a party member authored
        // battleSpriteFacing "Left" must still render facing the enemy
        // (mirrored, negative localScale.x) -- not facing away from it,
        // which is what FacingOf's hardcoded Right fallback produced before
        // this fix, silently, for every left-facing party member ever
        // authored.
        [UnityTest]
        public IEnumerator APartyMemberAuthoredFacingLeftIsStillMirroredToFaceRight()
        {
            yield return StandTheFightUpFacing(SpriteFacing.Left);

            var sprite = PartySprite();
            Assert.Less(sprite.rectTransform.localScale.x, 0f,
                "sheep authored Left must be mirrored to face the enemy (negative localScale.x); a positive " +
                "or zero scale means FacingOf fell through to its Right fallback instead of reading the kit");
        }

        // THE CONTROL: the same rebuild, the same real content, only the
        // authored facing changed -- proving the assertion above is reading
        // a real mirror decision and not, say, an always-negative scale from
        // stand-off maths or the enemy slot's own side.
        [UnityTest]
        public IEnumerator APartyMemberAuthoredFacingRightRendersUnmirrored()
        {
            yield return StandTheFightUpFacing(SpriteFacing.Right);

            var sprite = PartySprite();
            Assert.GreaterOrEqual(sprite.rectTransform.localScale.x, 0f,
                "sheep authored Right must render unmirrored (non-negative localScale.x) standing on the party " +
                "side, which already faces the enemy without a flip");
        }
    }
}
