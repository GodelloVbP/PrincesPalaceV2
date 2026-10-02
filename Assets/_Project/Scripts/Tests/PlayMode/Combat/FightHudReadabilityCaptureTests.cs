using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // THE FIGHT HUD'S READABILITY STATES, as pictures: the spell detail card
    // hovered on every skill of a real level-20 character (every row icon
    // needs a value beside it), the low-HP edge band, and a ward cast on its
    // own beat. The status tooltip is StatusRowCaptureTests' "hover" shot.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // FightHudReadabilityCaptureTests. PNGs land in tools/screenshots/runtime/.
    public class FightHudReadabilityCaptureTests
    {
        private FightController _fight;
        private HudCaptureRig _rig;
        private FightSession _session;
        private IReadOnlyList<Domain.Combat.CombatantState> _party;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name) => _rig.Named(name);

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        private IEnumerator OpenFight(string characterId)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _rig = new HudCaptureRig(_fight, $"hudread_{characterId}_", "FightHudReadabilityCapture");

            for (int i = 0; i < 120; i++) yield return null;

            var character = new Character(characterId) { level = 20, claimedTrackLevel = 20 };
            var enemy = ContentDatabase.Enemies.First(e => !string.IsNullOrWhiteSpace(e.Data.SpritePath)).id;
            var built = FightEncounterAdapter.Build(
                new List<string> { characterId }, new List<string> { enemy }, new SeededRandom(11),
                partyCharacters: new List<Character> { character });
            foreach (var member in built.Party)
            {
                if (member.PrimaryPool != null) member.PrimaryPool.Current = member.PrimaryPool.Max;
                if (member.SignaturePool != null) member.SignaturePool.Current = member.SignaturePool.Max;
            }

            _session = built.Session;
            _party = built.Party;
            _fight.Bind(built.Session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party,
                built.Party.Select(p => ContentDatabase.Characters
                        .FirstOrDefault(c => c.Data.DisplayName == p.Name)?.Data.BattleSpritePath).ToList());
            yield return null;
            yield return null;
        }

        private static string Slug(string name) =>
            new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        // The detail card on every skill row, one picture each.
        private IEnumerator HoverEveryRow(string characterId)
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            yield return OpenFight(characterId);
            Click("Verb1");
            yield return null;

            int rows = _fight.CurrentRowsForTest().Count;
            Assert.Greater(rows, 0, "the character has no skill rows");
            for (int i = 0; i < rows && i < 8; i++)
            {
                _fight.HoverRowForTest(i);
                // The card fades in; a shot two frames after the hover catches it half drawn.
                yield return new WaitForSecondsRealtime(0.5f);
                yield return _rig.Shoot($"detail_{i}_{Slug(_fight.CurrentDetailForTest().Name)}");
            }
        }

        [UnityTest]
        public IEnumerator CaptureTheDetailCardOnShawnsSkills() { yield return HoverEveryRow("sheep"); }

        [UnityTest]
        public IEnumerator CaptureTheDetailCardOnOdettesSkills() { yield return HoverEveryRow("owl"); }

        // An ally under the threshold: the vignette is an edge band.
        [UnityTest]
        public IEnumerator CaptureTheLowHpVignette()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            yield return OpenFight("sheep");
            _party[0].CurrentHealth = Mathf.Max(1, _party[0].MaxHealth / 10);
            Click("Verb1");
            yield return null;
            Click("Verb0");
            yield return null;
            yield return _rig.Shoot("low_hp_vignette");
        }

        // A ward cast, photographed across its own beat at the real speed.
        [UnityTest]
        public IEnumerator CaptureAWardAndAStatusAppearingOnTheCastBeat()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            yield return OpenFight("sheep");
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Click("Verb1");
            yield return null;

            int rows = _fight.CurrentRowsForTest().Count;
            int ward = -1;
            for (int i = 0; i < rows; i++)
            {
                _fight.HoverRowForTest(i);
                if (_fight.CurrentDetailForTest().Name.Contains("Tuck")) { ward = i; break; }
            }

            Assert.GreaterOrEqual(ward, 0, "the character has no Tuck In (the ward) skill to cast");
            Click($"CharacterSkill{ward}");
            if (Named("TargetPrompt").activeInHierarchy) Click("EnemyPlate0");

            for (int shot = 0; shot < 4; shot++)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                yield return _rig.Shoot($"ward_cast_{shot}");
            }
        }
    }
}
