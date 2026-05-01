using System.Collections.Generic;
using System.IO;
using D2Net.Init;

namespace D2Net.Init.Tests;

public class AddExcludePathValidatorTests
{
    [Fact]
    public void Canonicalise_StripsTrailingSlash()
    {
        Assert.Equal("foo", PathValidator.Canonicalise("foo/"));
        Assert.Equal("foo/bar", PathValidator.Canonicalise("foo/bar/"));
    }

    [Fact]
    public void Canonicalise_NormalisesBackslashes()
    {
        Assert.Equal("foo/bar", PathValidator.Canonicalise(@"foo\bar"));
        Assert.Equal("foo/bar", PathValidator.Canonicalise(@"foo\bar\"));
    }

    [Fact]
    public void Canonicalise_RejectsEmpty()
    {
        Assert.Throws<System.ArgumentException>(() => PathValidator.Canonicalise(""));
        Assert.Throws<System.ArgumentException>(() => PathValidator.Canonicalise("   "));
    }

    [Fact]
    public void Canonicalise_CollapsesRunsOfSlashes()
    {
        Assert.Equal("foo/bar", PathValidator.Canonicalise("foo//bar"));
        Assert.Equal("foo/bar/baz", PathValidator.Canonicalise("foo///bar//baz"));
    }

    [Fact]
    public void ResolveUnderSource_AcceptsRelativeUnderSource()
    {
        var src = Path.GetFullPath(Path.GetTempPath());
        Assert.Equal("test_archive", PathValidator.ResolveUnderSource("test_archive", src));
        Assert.Equal("lib/foo", PathValidator.ResolveUnderSource("lib/foo", src));
    }

    [Fact]
    public void ResolveUnderSource_RejectsClimbing()
    {
        var src = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "deep", "src"));
        Assert.Null(PathValidator.ResolveUnderSource("../escape", src));
        Assert.Null(PathValidator.ResolveUnderSource("../../totally-escape", src));
    }

    [Fact]
    public void ResolveUnderSource_AbsoluteUnderSourceAccepted()
    {
        var src = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "src007"));
        Directory.CreateDirectory(src);
        try
        {
            var inside = Path.Combine(src, "lib");
            var rel = PathValidator.ResolveUnderSource(inside.Replace('\\', '/'), src);
            Assert.Equal("lib", rel);
        }
        finally { try { Directory.Delete(src, recursive: true); } catch { } }
    }

    [Fact]
    public void IsUnder_RespectsDirectoryBoundary()
    {
        Assert.True(PathValidator.IsUnder("bin/foo.dart", "bin"));
        Assert.True(PathValidator.IsUnder("bin/sub/foo.dart", "bin"));
        Assert.False(PathValidator.IsUnder("binary.dart", "bin"));
        Assert.False(PathValidator.IsUnder("bin", "bin"));            // same path, not under
        Assert.False(PathValidator.IsUnder("foo", "bin"));
    }

    [Fact]
    public void ClassifyAgainstExisting_DetectsRedundantSelf()
    {
        var existing = new[] { "bin", "test_archive" };
        var (cls, anc) = PathValidator.ClassifyAgainstExisting("bin", existing);
        Assert.Equal(PathValidator.Classification.RedundantSelf, cls);
        Assert.Equal("bin", anc);
    }

    [Fact]
    public void ClassifyAgainstExisting_DetectsRedundantUnderAncestor()
    {
        var existing = new[] { "bin" };
        var (cls, anc) = PathValidator.ClassifyAgainstExisting("bin/archive", existing);
        Assert.Equal(PathValidator.Classification.RedundantUnderAncestor, cls);
        Assert.Equal("bin", anc);
    }

    [Fact]
    public void ClassifyAgainstExisting_AddedWhenNew()
    {
        var existing = new[] { "bin" };
        var (cls, _) = PathValidator.ClassifyAgainstExisting("docs", existing);
        Assert.Equal(PathValidator.Classification.Added, cls);
    }

    [Fact]
    public void ClassifyBatch_DedupesIntraBatchAncestor()
    {
        var paths = new[] { "bin", "bin/archive" };
        var existing = new HashSet<string>();
        var result = PathValidator.ClassifyBatch(paths, existing);

        // Lexicographic ascendant-first => bin processed first; bin/archive then redundant.
        Assert.Single(result.AddedAscending);
        Assert.Equal("bin", result.AddedAscending[0]);

        Assert.Equal(2, result.PerInput.Count);
        // First input "bin" -> Added
        Assert.Equal(PathValidator.Classification.Added, result.PerInput[0].Cls);
        // Second input "bin/archive" -> RedundantUnderAncestor("bin")
        Assert.Equal(PathValidator.Classification.RedundantUnderAncestor, result.PerInput[1].Cls);
        Assert.Equal("bin", result.PerInput[1].Ancestor);
    }

    [Fact]
    public void ClassifyBatch_DedupesIntraBatchDuplicate()
    {
        var paths = new[] { "foo", "foo" };
        var existing = new HashSet<string>();
        var result = PathValidator.ClassifyBatch(paths, existing);
        Assert.Single(result.AddedAscending);
        Assert.Equal(2, result.PerInput.Count);
        Assert.Equal(PathValidator.Classification.Added, result.PerInput[0].Cls);
        Assert.Equal(PathValidator.Classification.RedundantSelf, result.PerInput[1].Cls);
    }

    [Fact]
    public void ClassifyBatch_HandlesAlreadyExistingExclusion()
    {
        var paths = new[] { "test_archive" };
        var existing = new[] { "bin", "test_archive" };
        var result = PathValidator.ClassifyBatch(paths, existing);
        Assert.Empty(result.AddedAscending);
        Assert.Single(result.PerInput);
        Assert.Equal(PathValidator.Classification.RedundantSelf, result.PerInput[0].Cls);
    }

    [Fact]
    public void LooksLikeFilePath_DetectsExistingFile()
    {
        var src = Path.Combine(Path.GetTempPath(), "addexcl-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        try
        {
            File.WriteAllText(Path.Combine(src, "thing.txt"), "x");
            Assert.True(PathValidator.LooksLikeFilePath("thing.txt", src, "thing.txt"));
        }
        finally { try { Directory.Delete(src, recursive: true); } catch { } }
    }

    [Fact]
    public void LooksLikeFilePath_AcceptsExistingDirectory()
    {
        var src = Path.Combine(Path.GetTempPath(), "addexcl-dir-" + Guid.NewGuid().ToString("N"));
        var inner = Path.Combine(src, "subdir");
        Directory.CreateDirectory(inner);
        try
        {
            Assert.False(PathValidator.LooksLikeFilePath("subdir", src, "subdir"));
        }
        finally { try { Directory.Delete(src, recursive: true); } catch { } }
    }

    [Fact]
    public void LooksLikeFilePath_RejectsNonexistentSuffix()
    {
        var src = Path.Combine(Path.GetTempPath(), "addexcl-nfs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        try
        {
            Assert.True(PathValidator.LooksLikeFilePath("foo.dart", src, "foo.dart"));
            Assert.True(PathValidator.LooksLikeFilePath("foo.zip", src, "foo.zip"));
            Assert.True(PathValidator.LooksLikeFilePath("FOO.EXE", src, "FOO.EXE"));
        }
        finally { try { Directory.Delete(src, recursive: true); } catch { } }
    }

    [Fact]
    public void LooksLikeFilePath_AcceptsExtensionlessNonexistent()
    {
        var src = Path.Combine(Path.GetTempPath(), "addexcl-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        try
        {
            // Extensionless and doesn't exist — accepted as forward-looking exclusion
            Assert.False(PathValidator.LooksLikeFilePath("future_dir", src, "future_dir"));
            Assert.False(PathValidator.LooksLikeFilePath("Makefile", src, "Makefile"));
        }
        finally { try { Directory.Delete(src, recursive: true); } catch { } }
    }
}
