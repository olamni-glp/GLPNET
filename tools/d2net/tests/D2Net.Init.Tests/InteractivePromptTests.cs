using System.IO;
using D2Net.Init;

namespace D2Net.Init.Tests;

public class InteractivePromptTests
{
    private static InteractivePrompter MakePrompter(string scriptedInput, out StringWriter outBuf)
    {
        outBuf = new StringWriter();
        return new InteractivePrompter(new StringReader(scriptedInput), outBuf, nonInteractive: false);
    }

    [Fact]
    public void AcceptApprovesEntireSuggestedList()
    {
        var prompter = MakePrompter("a\n", out var buf);
        var suggested = new ProposedExclusion[]
        {
            new(".git", ExclusionKind.Tool, "well-known"),
            new("archive_2024", ExclusionKind.Pattern, "matches"),
        };

        var result = prompter.ApproveExclusions(suggested, acceptSuggestedFlag: false);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Path == ".git");
    }

    [Fact]
    public void RemoveByRowNumberRemovesItem()
    {
        var prompter = MakePrompter("r 2\na\n", out var buf);
        var suggested = new ProposedExclusion[]
        {
            new(".git", ExclusionKind.Tool, "well-known"),
            new("archive_2024", ExclusionKind.Pattern, "matches"),
            new("old_stuff", ExclusionKind.Pattern, "matches"),
        };

        var result = prompter.ApproveExclusions(suggested, acceptSuggestedFlag: false);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e.Path == "archive_2024");
    }

    [Fact]
    public void QuitThrowsPromptCancelled()
    {
        var prompter = MakePrompter("q\n", out var buf);
        var suggested = new ProposedExclusion[] { new(".git", ExclusionKind.Tool, "well-known") };

        Assert.Throws<PromptCancelledException>(
            () => prompter.ApproveExclusions(suggested, acceptSuggestedFlag: false));
    }

    [Fact]
    public void ListRedisplaysCurrentList()
    {
        var prompter = MakePrompter("l\nr 1\nl\na\n", out var buf);
        var suggested = new ProposedExclusion[]
        {
            new(".git", ExclusionKind.Tool, "well-known"),
            new("archive_2024", ExclusionKind.Pattern, "matches"),
        };

        var result = prompter.ApproveExclusions(suggested, acceptSuggestedFlag: false);

        // Should have shown the suggestion list multiple times (initial + l + after-remove + l)
        var redisplayCount = CountSubstring(buf.ToString(), "Suggested exclusions");
        Assert.True(redisplayCount >= 3, $"expected ≥3 displays, got {redisplayCount}");
        Assert.Single(result);
        Assert.Equal("archive_2024", result[0].Path);
    }

    [Fact]
    public void AcceptSuggestedFlagBypassesPrompt()
    {
        var prompter = MakePrompter("", out var _);
        var suggested = new ProposedExclusion[] { new(".git", ExclusionKind.Tool, "well-known") };

        // No input: would throw at ReadLine() if it tried to prompt
        var result = prompter.ApproveExclusions(suggested, acceptSuggestedFlag: true);
        Assert.Single(result);
    }

    [Fact]
    public void NonInteractiveBypassesPrompt()
    {
        var p = new InteractivePrompter(new StringReader(""), new StringWriter(), nonInteractive: true);
        var suggested = new ProposedExclusion[] { new(".git", ExclusionKind.Tool, "well-known") };
        var result = p.ApproveExclusions(suggested, acceptSuggestedFlag: false);
        Assert.Single(result);
    }

    [Fact]
    public void NonInteractiveThrowsOnMissingInput()
    {
        var p = new InteractivePrompter(new StringReader(""), new StringWriter(), nonInteractive: true);
        var opts = new InitOptions("/repo", null, null, null,
            Array.Empty<string>(), false, false, false, true, 54329);
        Assert.Throws<ArgumentException>(() => p.FillMissingInputs(opts));
    }

    private static int CountSubstring(string haystack, string needle)
    {
        int count = 0, pos = 0;
        while ((pos = haystack.IndexOf(needle, pos)) >= 0) { count++; pos += needle.Length; }
        return count;
    }
}
