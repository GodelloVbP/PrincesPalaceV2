// Compiled ONLY into the dotnet test host (see PrincesPalace.Domain.Tests.csproj).
// Never into Unity, and never into the Domain assembly, which is
// noEngineReferences and must stay that way.
//
// Why this exists: RewardTrackLayoutTests is a pure geometry suite over
// RewardTrackLayout, which is Domain code with no engine in it at all. Its
// only tie to Unity is two calls to Mathf.Sqrt and Mathf.Abs on the ASSERTION
// side. Excluding the whole class over two float functions would drop a real
// layout suite out of the fast loop for nothing; splitting the file would
// mean editing tests to work around the tooling, which is backwards.
//
// THE RULE FOR THIS FILE: pure functions over float, with semantics identical
// to UnityEngine's, and nothing else. Never add anything that touches engine
// state, time, randomness, serialization or assets. A shim for those would
// let a test pass here for a reason it would not pass in Unity, which is the
// one thing that would make the dotnet host untrustworthy. If a test needs
// more than arithmetic from UnityEngine, exclude it instead.
//
// Both members below are Unity's own documented implementations: Mathf.Sqrt
// and Mathf.Abs forward straight to System.Math on float.

namespace UnityEngine
{
    internal static class Mathf
    {
        public static float Sqrt(float f) => (float)System.Math.Sqrt(f);

        public static float Abs(float f) => System.Math.Abs(f);
    }
}
