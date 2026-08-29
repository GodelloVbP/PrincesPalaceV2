using PrincesPalace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Dev convenience: one menu click into a fight against exactly one named
// enemy, for looking at new combat art without navigating Hub -> Map -> room,
// and without a run or save slot to set up first.
//
// Deliberately NOT a second generated scene -- SceneBuilder.BuildAllScenes
// reassigns every scene's fileIDs on a rebuild, which is a lot of churn (and
// a real collision risk against anyone else's in-flight scene work) for a
// dev-only shortcut. Instead this opens the existing Fight.unity in Play
// mode and hands FightBootstrap.DevForcedEnemyId the id to field -- see that
// field's own comment. Editor-only; ships in no build.
public static class QuickFightMenu
{
    [MenuItem("Prince's Palace/Dev/Fight Giant Rat")]
    public static void FightGiantRat()
    {
        StartPlaceholderFight("rat");
    }

    private static void StartPlaceholderFight(string enemyId)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[QuickFightMenu] Already in Play mode -- stop first, then use this menu.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return; // user cancelled the save-changes prompt
        }

        EditorSceneManager.OpenScene(ScreenRegistry.FightScene, OpenSceneMode.Single);
        FightBootstrap.DevForcedEnemyId = enemyId;
        EditorApplication.isPlaying = true;
    }
}
