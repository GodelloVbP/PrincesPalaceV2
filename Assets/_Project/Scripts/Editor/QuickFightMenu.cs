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
    // WHICH MOB, remembered rather than hardcoded.
    //
    // This item said "Fight Giant Rat" and passed the literal "rat", which was
    // right for exactly as long as the rat was the only monster with art. An
    // author looking at a mob they are in the middle of writing wants THAT one,
    // and had to edit this file to get it.
    //
    // EditorPrefs, not SessionState: the DevForced* keys are one fight's
    // opinion and die with the Editor, but "the last thing I was working on"
    // should survive a restart -- that is the whole reason to remember it. Set
    // by tools/preview.ps1 through PreviewRequestWatcher, and by this menu.
    private const string LastPreviewedKey = "PrincesPalace.Dev.LastPreviewedEnemyId";

    // The rat is the fallback, not the subject: it is the one mob that has had
    // art for the whole life of the project, so a fresh checkout with no
    // preview history still gets a fight rather than an error.
    internal static string LastPreviewedEnemyId
    {
        get => EditorPrefs.GetString(LastPreviewedKey, "rat");
        set => EditorPrefs.SetString(LastPreviewedKey, string.IsNullOrEmpty(value) ? "rat" : value);
    }

    [MenuItem("Prince's Palace/Dev/Fight last previewed id")]
    public static void FightLastPreviewed()
    {
        StartPlaceholderFight(LastPreviewedEnemyId);
    }

    internal static void StartPlaceholderFight(string enemyId)
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
        LastPreviewedEnemyId = enemyId;
        EditorApplication.isPlaying = true;
    }
}
