// 049 T006 — characterization of the SHIPPED PolicyMatcher against the shared decision vectors
// (specs/049-wave1-guard-link-acceptance/contracts/vectors.json, linked into the test output).
//
// FR-005/SC-003: the GLP guard must agree 100% with these decisions; the matcher is the reference
// and csharp/glp_crdtmsg/route/PolicyMatcher.cs is READ-ONLY (FR-006). guard_only vectors (the
// Suspend arm — unbound reachability) are skipped here BY DESIGN: a set-valued API cannot
// represent an unbound reader; only the guard side runs them.

using System.Text.Json;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class PolicyVectorParityTests
{
    private sealed record VectorPolicy(string[] targets, string[] waypoints, string[] excludes);

    private sealed record Vector(string id, VectorPolicy policy, JsonElement reachable,
        string? expected_matcher, string expected_guard, bool guard_only, string note);

    private sealed record VectorFile(string[] ignored);

    private static IReadOnlyList<Vector> LoadVectors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "vectors.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
        return doc.RootElement.GetProperty("vectors").EnumerateArray()
            .Select(v => v.Deserialize<Vector>(opts)!)
            .ToList();
    }

    public static TheoryData<string> MatcherVectorIds()
    {
        var data = new TheoryData<string>();
        foreach (var v in LoadVectors().Where(v => !v.guard_only))
            data.Add(v.id);
        return data;
    }

    [Theory]
    [MemberData(nameof(MatcherVectorIds))]
    public void Shipped_matcher_decision_matches_the_shared_vector(string id)
    {
        var v = LoadVectors().Single(x => x.id == id);
        Assert.False(v.guard_only);
        Assert.NotNull(v.expected_matcher);

        var policy = new RoutingPolicy(v.policy.targets, v.policy.waypoints, v.policy.excludes);
        var reachable = v.reachable.EnumerateArray().Select(e => e.GetString()!).ToHashSet();

        var d = PolicyMatcher.Evaluate(policy, reachable);

        Assert.Equal(v.expected_matcher == "deliver", d.Deliver);
        if (!d.Deliver)
            Assert.Equal(DropReason.NoRoute, d.Drop); // logged-never-silent taxonomy (C23)

        // FR-005 invariant: matcher outcome and guard expectation are two faces of one decision.
        Assert.Equal(v.expected_matcher == "deliver" ? "success" : "fail", v.expected_guard);
    }

    [Fact]
    public void Vector_file_invariants_hold()
    {
        var vectors = LoadVectors();
        Assert.True(vectors.Count >= 12, "seed set must not shrink (contract: vectors may be added, none removed)");
        Assert.Equal(vectors.Count, vectors.Select(v => v.id).Distinct().Count());
        foreach (var v in vectors)
        {
            if (v.guard_only)
            {
                Assert.Null(v.expected_matcher);       // matcher side has no arm for unbound reachability
                Assert.True(v.reachable.ValueKind is JsonValueKind.String or JsonValueKind.Object,
                    $"{v.id}: guard_only reachable must be \"unbound\" or a prefix/tail object");
            }
            else
            {
                Assert.NotNull(v.expected_matcher);
                Assert.Equal(JsonValueKind.Array, v.reachable.ValueKind); // ground reachable set
                Assert.NotEqual("suspend", v.expected_guard);             // suspend is guard-only behavior
            }
        }
    }
}
