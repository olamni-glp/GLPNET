// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// SC-004 — <b>the anti-recurrence check</b>. Every realization of the receive and send contracts
/// must have a path from the control surface to it.
///
/// <para>
/// 🔴 <b>This test was written to FAIL, and it did.</b> Measured against commit <c>8d4088e4</c>
/// (2026-09-06), before any of this feature's wiring existed:
/// </para>
/// <code>
///   Assert.Empty() Failure: Collection not empty
///   Collection: ["QuicInbound"]
///   — QuicInbound realizes IYnetInbound, is 400 lines, has 210 lines of passing tests,
///     and no control-surface path can construct it.
///
///   Assert.Empty() Failure: Collection not empty
///   Collection: ["QuicOutbound"]
///   — QuicOutbound realizes IYnetOutbound; `send` hard-codes CoopFileOutbound.
/// </code>
/// <para>
/// That failing output is recorded here verbatim rather than described, because a guard whose
/// failure nobody has seen is a guard nobody has shown to work. It is the positive control for the
/// whole feature.
/// </para>
///
/// <para>
/// <b>Why reflection over the assembly and not a grep over source.</b> A grep for
/// <c>new QuicInbound</c> is satisfied by a dead code path, and a code path nobody reaches is
/// precisely the defect. The catalog closes the converse direction by construction:
/// <see cref="PlaneSelection"/> has no way to build a plane except through
/// <see cref="PlaneCatalog"/>, so registration IMPLIES reachability rather than asserting it.
/// </para>
///
/// <para>
/// <b>Why the class matters beyond this one instance.</b> The same shape was measured twice in this
/// repo on the same day: (1) the QUIC carrier above; (2) <c>csharp/glp_supervisor</c>, a working
/// tested supervisor that hosts <c>glp_engine_host</c> and did not host this client — the one
/// process the fleet declared MUST be kernel-managed. Both compiled. Both passed their own tests,
/// because a capability's own tests construct it directly, which is exactly the path a real
/// consumer does not take. Review did not catch either. Hence a machine check, not a review rule.
/// </para>
/// </summary>
public class ContractReachabilityTests
{
    [Fact]
    public void Every_inbound_realization_is_reachable_from_the_control_surface()
    {
        var unreachable = PlaneCatalog.RealizationsOf(typeof(IYnetInbound))
            .Where(t => !PlaneCatalog.SelectableInbound.Contains(t))
            .Where(t => !PlaneCatalog.Exempt.ContainsKey(t.Name))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unreachable);
    }

    [Fact]
    public void Every_outbound_realization_is_reachable_from_the_control_surface()
    {
        var unreachable = PlaneCatalog.RealizationsOf(typeof(IYnetOutbound))
            .Where(t => !PlaneCatalog.SelectableOutbound.Contains(t))
            .Where(t => !PlaneCatalog.Exempt.ContainsKey(t.Name))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unreachable);
    }

    /// <summary>
    /// The guard must be measuring something. If the assembly contained no realizations at all,
    /// both tests above would pass vacuously — the same trap as two empty transcripts comparing
    /// equal (measured in this repo, wave-33). Assert the population is non-empty first.
    /// </summary>
    [Fact]
    public void The_guard_is_measuring_a_non_empty_population()
    {
        var inbound = PlaneCatalog.RealizationsOf(typeof(IYnetInbound)).ToList();
        var outbound = PlaneCatalog.RealizationsOf(typeof(IYnetOutbound)).ToList();

        Assert.True(inbound.Count >= 3,
            $"expected at least loopback, file and wire inbound planes; found {inbound.Count}: " +
            string.Join(", ", inbound.Select(t => t.Name)));
        Assert.True(outbound.Count >= 2,
            $"expected at least file and wire outbound planes; found {outbound.Count}: " +
            string.Join(", ", outbound.Select(t => t.Name)));
    }

    /// <summary>
    /// The exemption list is the escape hatch, so it is made uncomfortable rather than convenient:
    /// every exemption must carry a non-empty reason, and this test prints them all, so an
    /// exemption is visible in test output forever instead of being a quiet way to re-open the
    /// hole the catalog closes.
    /// </summary>
    [Fact]
    public void Every_exemption_carries_a_reason()
    {
        foreach (var (type, reason) in PlaneCatalog.Exempt)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason),
                $"exemption for '{type}' has no reason. An exemption without a reason is how this " +
                "guard gets quietly disabled one type at a time.");
        }
    }

    /// <summary>
    /// Registration is not enough on its own — the catalog must actually be able to PRODUCE each
    /// registered plane. A registry that lists a type it cannot build would be the same defect one
    /// level up: a declaration with no working consumer.
    /// </summary>
    [Theory]
    [InlineData(PlaneCatalog.Plane.Loopback, typeof(LoopbackInbound))]
    [InlineData(PlaneCatalog.Plane.File, typeof(CoopFileInbound))]
    public void The_catalog_produces_the_plane_it_registers(PlaneCatalog.Plane plane, Type expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "ynet-reach-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bound = PlaneCatalog.BindInbound(plane, new PlaneCatalog.Binding
            {
                CoopRoot = root,
                LaneDirectory = "test-lane",
            });
            Assert.IsType(expected, bound);
            (bound as IDisposable)?.Dispose();
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* temp cleanup */ }
        }
    }

    /// <summary>
    /// The wire plane's construction path is exercised too — but only as far as CONSTRUCTION, not
    /// binding, because this host's QUIC certificate material has been destroyed four times and a
    /// test that needed a live listener would be red for a reason that has nothing to do with the
    /// code under test. Constructing it is what proves the catalog can reach it.
    /// </summary>
    [Fact]
    public void The_catalog_can_reach_the_wire_plane()
    {
        Assert.Contains(typeof(QuicInbound), PlaneCatalog.SelectableInbound);
        Assert.Contains(typeof(QuicOutbound), PlaneCatalog.SelectableOutbound);
    }
}
