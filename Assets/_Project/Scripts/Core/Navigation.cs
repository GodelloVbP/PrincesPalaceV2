using UnityEngine.SceneManagement;

namespace PrincesPalace
{
    // Which scene comes next.
    //
    // The names live HERE, once, rather than as string literals at each call
    // site. A mistyped scene name is a runtime exception with no compile-time
    // warning, and v1 had them scattered across controllers.
    //
    // Deliberately thin. This is not a state machine and should not become one:
    // what the game does next is a decision each screen makes for itself, and
    // centralising the DECISION as well as the loading is how a navigation
    // helper turns into a second copy of the game's flow.
    public static class Navigation
    {
        public const string MainMenu = "MainMenu";
        public const string Hub = "Hub";
        public const string Fight = "Fight";
        public const string Map = "Map";
        public const string Talents = "Talents";

        // Overridable so a test can assert WHERE a button would go without
        // actually tearing down the scene it is running in -- loading a scene
        // mid-test destroys the objects the assertions are about.
        public static System.Action<string> LoadOverride;

        public static void Go(string scene)
        {
            if (LoadOverride != null)
            {
                LoadOverride(scene);
                return;
            }

            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        public static void Reset() => LoadOverride = null;
    }
}
