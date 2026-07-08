// T007 (feature 043-xsd-schema-language): restricted-regex NFA engine tests — written FIRST
// per the tests-first law; T006 makes them green.
//
// Contract: specs/043-xsd-schema-language/contracts/schema-dsl.md §Restricted pattern subset
// (research R6): literals, escapes \. \\ \-, '.', character classes (ranges, negation),
// grouping, alternation, quantifiers * + ? {n} {n,m}; implicitly anchored both ends. Anything
// else is an error NAMING the construct. Emptiness by NFA reachability; linear-time anchored
// match; language inclusion for compatibility checking (contracts/compat-evolution.md †).

using GlpRuntime.SchemaLang;

namespace GlpRuntime.SchemaLang.Tests;

public class PatternNfaTests
{
    private static PatternNfa ParseOk(string pattern)
    {
        var result = PatternNfa.Parse(pattern);
        Assert.Null(result.Error);
        Assert.NotNull(result.Nfa);
        return result.Nfa!;
    }

    private static PatternError ParseErr(string pattern)
    {
        var result = PatternNfa.Parse(pattern);
        Assert.NotNull(result.Error);
        Assert.Null(result.Nfa);
        return result.Error!;
    }

    // ------------------------------------------------------------------
    // Subset acceptance
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("[a-z][a-z0-9_]*")]          // the SC-006 walkthrough UserName pattern
    [InlineData("a|b|c")]
    [InlineData("(ab)+c?")]
    [InlineData("[^0-9]")]
    [InlineData("a{2}")]
    [InlineData("a{2,4}")]
    [InlineData(".")]
    [InlineData("\\.")]
    [InlineData("\\\\")]
    [InlineData("[a\\-z]")]                  // escaped literal dash inside a class
    [InlineData("(a|b)(c|d)*")]
    public void Subset_constructs_parse(string pattern) => ParseOk(pattern);

    // ------------------------------------------------------------------
    // Subset rejection — the error NAMES the unsupported construct
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("(?=a)b", "(?")]             // lookaround
    [InlineData("(?<name>a)", "(?")]         // named group
    [InlineData("a\\1", "\\1")]              // backreference
    [InlineData("\\d+", "\\d")]              // perl class escape
    [InlineData("^abc", "^")]                // explicit anchor (anchoring is implicit)
    [InlineData("abc$", "$")]                // explicit anchor
    [InlineData("a{2,}", "{2,}")]            // open-ended counted repeat not in subset
    [InlineData("a\\+", "\\+")]              // escape outside the supported set
    public void Unsupported_constructs_rejected_naming_construct(string pattern, string construct)
    {
        var error = ParseErr(pattern);
        Assert.Contains(construct, error.Construct);
    }

    [Fact]
    public void Unbalanced_group_is_an_error()
    {
        Assert.NotNull(PatternNfa.Parse("(ab").Error);
        Assert.NotNull(PatternNfa.Parse("ab)").Error);
    }

    [Fact]
    public void Reversed_class_range_is_an_error()
    {
        var error = ParseErr("[z-a]");
        Assert.Contains("z-a", error.Construct);
    }

    // ------------------------------------------------------------------
    // Emptiness (accept-state reachability)
    // ------------------------------------------------------------------

    [Fact]
    public void Empty_character_class_yields_empty_language()
    {
        var nfa = ParseOk("[]");
        Assert.True(nfa.IsEmptyLanguage());
    }

    [Fact]
    public void Empty_class_under_quantifier_plus_is_empty_language()
    {
        Assert.True(ParseOk("[]+").IsEmptyLanguage());
    }

    [Theory]
    [InlineData("a")]
    [InlineData("[a-z]*")]                   // accepts "" — non-empty language
    [InlineData("a|[]")]                     // one live branch
    [InlineData("[]*")]                      // matches exactly "" — non-empty language
    public void Nonempty_languages_detected(string pattern) =>
        Assert.False(ParseOk(pattern).IsEmptyLanguage());

    // ------------------------------------------------------------------
    // Anchored match semantics (implicit full anchoring, linear-time)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "xabc", false)]       // anchored at start
    [InlineData("abc", "abcx", false)]       // anchored at end
    [InlineData("[a-z][a-z0-9_]*", "gabi_42", true)]
    [InlineData("[a-z][a-z0-9_]*", "Gabi", false)]
    [InlineData("[a-z][a-z0-9_]*", "", false)]
    [InlineData("a*", "", true)]
    [InlineData("a+", "", false)]
    [InlineData("a{2,4}", "aa", true)]
    [InlineData("a{2,4}", "aaaa", true)]
    [InlineData("a{2,4}", "a", false)]
    [InlineData("a{2,4}", "aaaaa", false)]
    [InlineData("a{3}", "aaa", true)]
    [InlineData("a{3}", "aa", false)]
    [InlineData("(ab|cd)+", "abcdab", true)]
    [InlineData("(ab|cd)+", "abc", false)]
    [InlineData(".", "x", true)]
    [InlineData(".", "", false)]
    [InlineData("[^0-9]", "q", true)]
    [InlineData("[^0-9]", "7", false)]
    [InlineData("\\.", ".", true)]
    [InlineData("\\.", "x", false)]          // escaped dot is a literal, not any-char
    [InlineData("[a\\-z]", "-", true)]
    [InlineData("[a\\-z]", "b", false)]      // escaped dash is literal, not a range
    public void Anchored_match(string pattern, string input, bool expected) =>
        Assert.Equal(expected, ParseOk(pattern).Matches(input));

    // ------------------------------------------------------------------
    // Language inclusion (compat-evolution.md † — pattern widen/narrow)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("[a-c]", "[a-z]", PatternInclusion.Subset)]
    [InlineData("[a-z]", "[a-c]", PatternInclusion.NotSubset)]
    [InlineData("abc", "[a-z]{3}", PatternInclusion.Subset)]
    [InlineData("a+", "a*", PatternInclusion.Subset)]
    [InlineData("a*", "a+", PatternInclusion.NotSubset)]   // "" separates them
    [InlineData("[a-z][a-z0-9_]*", "[a-z][a-z0-9_]*", PatternInclusion.Subset)]
    [InlineData("a|b", "[a-b]", PatternInclusion.Subset)]
    [InlineData("[0-9]{2}", "[0-9]+", PatternInclusion.Subset)]
    [InlineData("[0-9]+", "[0-9]{2}", PatternInclusion.NotSubset)]
    [InlineData("[^a-z]", ".", PatternInclusion.Subset)]    // negated class ⊆ any-char
    [InlineData(".", "[^a-z]", PatternInclusion.NotSubset)] // any-char ⊄ negated class
    public void Language_inclusion(string a, string b, PatternInclusion expected) =>
        Assert.Equal(expected, PatternNfa.IsSubsetOf(ParseOk(a), ParseOk(b)));

    [Fact]
    public void Inclusion_is_reflexive_for_the_walkthrough_pattern()
    {
        var nfa = ParseOk("[a-z][a-z0-9_]*");
        Assert.Equal(PatternInclusion.Subset, PatternNfa.IsSubsetOf(nfa, nfa));
    }

    [Fact]
    public void Inclusion_beyond_the_state_cap_is_unknown_never_a_wrong_answer()
    {
        // Equal languages spelled differently: the product walk needs ≥ 2^17 subset pairs,
        // exceeding the inclusion state cap — the answer must be the explicit Unknown.
        var a = ParseOk("(a|b)*a(a|b){17}");
        var b = ParseOk("(a|b)*a(a|b){9}(a|b){8}");
        Assert.Equal(PatternInclusion.Unknown, PatternNfa.IsSubsetOf(a, b));
    }

    // ------------------------------------------------------------------
    // Bounded construction: counted-repeat blowups fail loudly, never OOM/stack-overflow
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("a{100000}")]                // deep expansion → would stack-overflow unbounded
    [InlineData("(a{1000}){1000}")]          // multiplicative expansion → would OOM unbounded
    [InlineData("(a{500}){500}(b{500}){500}")]
    public void Counted_repeat_blowups_are_rejected_at_the_state_cap(string pattern)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var error = ParseErr(pattern);
        sw.Stop();
        Assert.Contains("build cap", error.Message); // the loud, located error names the cap
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"rejection must be fast (pre-expansion), took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Counted_repeat_count_beyond_int_range_is_an_error_not_an_overflow()
    {
        var error = ParseErr("a{99999999999}");
        Assert.Contains("out of representable range", error.Message);
    }

    [Fact]
    public void Group_nesting_beyond_the_limit_is_a_located_error()
    {
        var error = ParseErr(new string('(', 100) + "a" + new string(')', 100));
        Assert.Contains("nesting too deep", error.Message);
    }
}
