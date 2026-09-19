using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // THE SHARED PLAYMODE FIGHT LOAD, for the battle-speed timing fixtures
    // (docs/archive/PLAN_BATTLE_SPEED.md revision 3 point 1) that need a
    // deterministic PlayerSpeedMultiplier from the moment the scene opens.
    //
    // DEVIATION FROM THE PLAN AS WRITTEN, recorded here and in the plan's
    // own Deviations section: revision 3 assumed this helper already
    // existed ("LoadFight in Tests/PlayMode/Shared/ -- find it"). It did
    // not -- every one of the ~30 existing Fight-scene fixtures (Combat and
    // Art) defines its own local, private LoadFight-shaped coroutine, none
    // of them shared. Retrofitting all of them onto one helper is an
    // unrelated multi-file refactor with its own risk and review burden
    // (docs/WORKFLOW.md Sec4's freeze protocol would apply), so this file
    // is new -- built for T4/T5/T6/T8, the fixtures this plan actually
    // needs to be deterministic, rather than a repo-wide consolidation.
    //
    // PINS THE SOURCE ON BOTH SIDES OF THE LOAD, not just before it.
    // FightBootstrap.Start installs the production, settings-backed source
    // as part of building its own placeholder fight -- which runs during
    // THIS SAME scene load, after the pin below but before the two settle
    // frames finish. Re-pinning again after the load (and adopting) is what
    // actually makes the fixture deterministic; pinning only beforehand
    // would be overwritten by Start a moment later and every timing fixture
    // would silently read whatever GameSettings.BattleSpeed happens to
    // hold.
    internal static class FightSceneFixture
    {
        internal static IEnumerator LoadFight()
        {
            FightBeatPlayer.PlayerSpeedSource = () => 1f;

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            // Two settle frames, the convention every existing Fight-scene
            // fixture already uses: Start() runs one frame after a fresh
            // scene's objects activate (CODE_STANDARDS.md Sec8), and
            // FightBootstrap.Start is exactly the Start() this is waiting
            // out.
            yield return null;
            yield return null;

            // Re-pin AFTER FightBootstrap.Start has had its chance to
            // install the production source, then adopt explicitly rather
            // than waiting for the first beat -- so a fixture reading
            // FightBeatPlayer.PlayerSpeedMultiplier before it ever plays a
            // beat (T8) sees 1, not whatever OnEnable happened to catch.
            FightBeatPlayer.PlayerSpeedSource = () => 1f;

            // Static, like the seam it adopts from -- an instance is only
            // needed to confirm the scene actually has one to adopt for.
            if (Object.FindAnyObjectByType<FightBeatPlayer>() != null)
            {
                FightBeatPlayer.AdoptPlayerSpeed();
            }
        }
    }
}
