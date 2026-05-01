using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// US2 / FR-006: removing an exclusion whose .dart files remain covered by
/// a surviving ancestor exclusion succeeds (exit 0), removes the row, but
/// inserts zero dart_files rows. The summary reports the case clearly.
/// </summary>
public class RemoveExcludeAncestorSurvivalTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void RemoveChild_WhileAncestorSurvives_NoReindex()
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart")
            .AddDartFile("nested/deep/x.dart")
            .AddDartFile("nested/deep/y.dart");

        var port = PortPicker.NextFreePort();
        using (repo)
        {
            // Init: nothing manual.
            var (icode, _, _) = Run(
                new[] { "--source", "glp_runtime", "--target-extension", "_net",
                        "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                        "--non-interactive", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, icode);

            // Add `nested` and `nested/deep` as manual exclusions.
            var (acode, _, _) = Run(
                new[] { "--add-exclude", "nested",
                        "--add-exclude", "nested/deep",
                        "--bridge-port", port.ToString() },
                repo.Root);
            // Note: --add-exclude has its own redundancy semantics: nested/deep
            // would be reported as covered-by-ancestor("nested") and NOT
            // inserted. So after this, only `nested` is in the list. Adjust
            // the test to exercise actual ancestor-survival on remove side.
            Assert.Equal(ExitCodes.Success, acode);

            // Add `nested/deep` again separately — but feature 007's add-exclude
            // refuses to insert it under existing `nested`. So we need a
            // different setup where both rows independently exist.
            // Approach: directly insert via a second --add-exclude after init,
            // ordered so nested/deep is added BEFORE nested.

            // Reset by re-init:
            var (rebuildCode, _, _) = Run(
                new[] { "--FORCE", "--DELETE-EXISTING",
                        "--source", "glp_runtime", "--target-extension", "_net",
                        "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                        "--non-interactive", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, rebuildCode);

            // Add nested/deep first, then nested. Both end up in the list
            // because the SECOND add (nested) wins ancestor-classification
            // for the third hypothetical descendant, but the existing
            // nested/deep row stays intact.
            var (a1, _, _) = Run(
                new[] { "--add-exclude", "nested/deep", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, a1);
            var (a2, _, _) = Run(
                new[] { "--add-exclude", "nested", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, a2);

            // Now both `nested` and `nested/deep` are in the list.
            // Remove nested/deep — its files remain covered by the surviving
            // `nested` ancestor; zero rows should be inserted.
            var (rcode, rso, _) = Run(
                new[] { "--remove-exclude", "nested/deep", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, rcode);
            Assert.Contains("removed:                1", rso);
            Assert.Contains("covered-by-ancestor:    1", rso);
            Assert.Contains("inserted:               0 dart_files row(s)", rso);
            Assert.Contains("nested/deep -- covered by ancestor \"nested\"", rso);
        }
    }
}
