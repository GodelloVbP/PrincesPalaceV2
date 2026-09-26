using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // QA 2026-09-26: in a real run fight the turn-order ribbon drew every
    // enemy's portrait and the party as bare letters ("O", "S", "B").
    //
    // FightBootstrap calls Bind and THEN BindPartyArt. Bind paints the whole
    // HUD while the party-art map is still empty, so every party chip
    // resolves to its letter fallback; BindPartyArt then repainted only the
    // stage, and the ribbon kept the letters until something else happened
    // to repaint the HUD (the player's first press). The fix makes
    // BindPartyArt repaint everything that reads party art.
    //
    // No frame is yielded between the calls and the assertions on purpose:
    // the claim is that the two calls, in FightBootstrap's order, leave the
    // ribbon correct on their own -- not that some later repaint rescues it.
    public class TurnOrderRibbonArtTests
    {
        private FightBeatPlayer _beats;

        [UnityTearDown]
        public IEnumerator Restore()
        {
            LogAssert.ignoreFailingMessages = true;
            if (_beats != null) _beats.EndFight();
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator BindThenBindPartyArt_DrawsEveryPartyChipAsAPortrait_NotALetter()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");
            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the fight scene has no beat player");

            // FightBootstrap's own placeholder fight is mid-beat; flush it so
            // nothing it plays repaints the HUD behind this test's back.
            _beats.Flush();
            yield return null;
            yield return null;

            var party = ContentDatabase.Characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Data.BattleSpritePath))
                .Select(c => c.id).ToList();
            var enemies = PreviewFight.EnemiesWithArt(2);
            Assert.GreaterOrEqual(party.Count, 1, "no character has battle art");
            Assert.AreEqual(2, enemies.Count, "fewer than two enemies have art");

            var built = FightEncounterAdapter.Build(party, enemies, new SeededRandom(26));
            Assert.IsNotNull(built?.Session, "the encounter could not be built");

            // FightBootstrap's order, verbatim.
            fight.Bind(built.Session, EncounterClass.Normal);
            fight.BindPartyArt(built.Party, built.PartyArt);

            var chips = fight.GetComponentsInChildren<Transform>(includeInactive: true);
            int filled = 0;
            for (int i = 0; ; i++)
            {
                var icon = chips.FirstOrDefault(t => t.name == $"InitiativeIcon{i}")?.GetComponent<Image>();
                var label = chips.FirstOrDefault(t => t.name == $"InitiativeLabel{i}")?.GetComponent<TMP_Text>();
                if (icon == null || label == null) break;

                Assert.AreEqual("", label.text,
                    $"chip {i} fell back to its letter: some combatant in the queue resolved to no art");
                if (icon.sprite != null) filled++;
            }

            Assert.Greater(filled, party.Count,
                "the ribbon should carry a portrait for every party member and at least one enemy");
        }
    }
}
