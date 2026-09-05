using System.IO;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // WHERE THE REPO IS, from wherever the host happened to start.
    //
    // The two hosts disagree about the working directory: Unity runs tests
    // from the project root, `dotnet test` from the test assembly's bin
    // folder six levels down. Walking up to the first ancestor that has
    // Assets/_Project/Scripts under it answers for both without either
    // knowing about the other, and without a path constant that would be
    // wrong for one of them.
    //
    // Lives in Shared/ because two Content fixtures need it and a second copy
    // of a filesystem walk is the kind of duplication that drifts silently:
    // one copy gains a marker folder the other does not, and the two fixtures
    // then disagree about which tree they are asserting on.
    internal static class RepoTree
    {
        internal static string Root()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate the repo root from the working directory.");
            return dir.FullName;
        }
    }
}
