using UnityEditor;
using UnityEngine;

namespace PrincesPalace.Editor.Rigging
{
    // Pilot entrypoint chaining import + prefab build for the rat, invoked
    // headlessly via -executeMethod during Phase 3 verification. The real
    // GenerationRun.RunAll "BuildRigs" step (Phase 3/7 of the plan) will loop
    // this over every RIGS manifest entry; for the pilot it's just the rat.
    public static class RigBuildPilot
    {
        public static void Run()
        {
            try
            {
                RigImporter.ImportRat();
                RigPrefabBuilder.BuildRat();
                Debug.Log("BUILD-COMPLETE: Rigs");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("RigBuildPilot FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
