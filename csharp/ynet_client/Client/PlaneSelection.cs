// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

namespace Ynet.Client;

/// <summary>
/// The outcome of asking for a plane: what was requested, what is actually live, and — when those
/// differ — what failed and why.
///
/// <para>
/// 🔴 This is a <b>value</b>, not a side effect, and every status line is rendered from it. That is
/// what makes "the reported plane is derived from the live carrier object" a structural property
/// rather than a promise a future edit can quietly break. The client already had the opposite
/// defect on record: before 2026-09-05 its <c>run</c> verb bound the loopback plane
/// unconditionally and printed "running" while able to hear nothing but itself.
/// </para>
/// </summary>
public sealed record PlaneBinding
{
    public required PlaneCatalog.Plane Requested { get; init; }
    public required PlaneCatalog.Plane Live { get; init; }
    public required IYnetInbound Inbound { get; init; }

    /// <summary>Non-null exactly when a requested plane could not be bound and a lower one was
    /// used instead.</summary>
    public string? DegradedReason { get; init; }

    public bool IsDegraded => DegradedReason is not null;

    /// <summary>
    /// The line that announces the client is running.
    ///
    /// 🔴 A degradation is stated <b>on this line</b>, not in a log the operator does not read.
    /// Ruling Q-G34-02 (2026-09-06): a fallback the operator has to go looking for is a silent
    /// fallback, which is the defect this feature exists to close.
    /// </summary>
    public string RunningLine()
    {
        var live = $"ynet_client: receiver running   plane={Inbound.PlaneName}";
        if (!IsDegraded) return live;
        return live +
               $"   ⚠ DEGRADED: '{Requested}' was requested and could NOT be bound — {DegradedReason}" +
               $"   (running on '{Live}' instead)";
    }
}

/// <summary>
/// Binds the requested plane, or degrades to the file plane and says so.
///
/// One place decides, so <c>run</c>, <c>poll</c>, <c>send</c> and <c>doctor</c> cannot drift into
/// four different answers about which plane is live. They already had: before this feature
/// <c>run</c> selected between two planes while the other three hard-coded the file plane, and none
/// of the four could reach the wire at all.
/// </summary>
public static class PlaneSelection
{
    /// <summary>
    /// Bind <paramref name="requested"/>, falling back to the file plane if the wire cannot be
    /// bound.
    ///
    /// <para>
    /// Fallback is <b>wire → file only</b> (ruling Q-G34-02 → C). It exists because this host has
    /// had its QUIC certificate material destroyed four separate times, and a client that refused
    /// to start on such a host would leave it with no receiver at all — strictly worse than the
    /// shared-volume receiving it has today. The file plane has nothing below it, so a file-plane
    /// failure is a refusal (FR-004c), not a further degradation.
    /// </para>
    /// </summary>
    /// <param name="notify">Where a degradation is announced to the fleet. Best-effort by
    /// construction: the whole point of degrading is to keep a damaged host receiving, so failing
    /// to tell anyone must never stop it.</param>
    public static PlaneBinding Bind(
        PlaneCatalog.Plane requested,
        PlaneCatalog.Binding binding,
        IDegradedNotifier? notify = null)
    {
        ArgumentNullException.ThrowIfNull(binding);

        try
        {
            return new PlaneBinding
            {
                Requested = requested,
                Live = requested,
                Inbound = PlaneCatalog.BindInbound(requested, binding),
            };
        }
        catch (Exception ex) when (CanDegrade(requested) && IsBindFailure(ex))
        {
            var reason = $"{ex.GetType().Name}: {ex.Message}";

            // The fallback itself may fail — a host with no COOP root has no file plane either.
            // In that case there is nothing left to run, and saying so is the only honest answer.
            IYnetInbound fallback;
            try
            {
                fallback = PlaneCatalog.BindInbound(PlaneCatalog.Plane.File, binding);
            }
            catch (Exception inner)
            {
                throw new InvalidOperationException(
                    $"'{requested}' could not be bound ({reason}), and the file plane could not be " +
                    $"bound either ({inner.GetType().Name}: {inner.Message}). There is no plane left " +
                    "to fall back to, so this client would receive nothing. NOT STARTED.", inner);
            }

            // Best-effort, and deliberately swallowing everything: see the param doc.
            try { notify?.Degraded(requested, PlaneCatalog.Plane.File, reason); }
            catch { /* a notice that cannot be written must never stop a client that can run */ }

            return new PlaneBinding
            {
                Requested = requested,
                Live = PlaneCatalog.Plane.File,
                Inbound = fallback,
                DegradedReason = reason,
            };
        }
    }

    /// <summary>Only a request that INCLUDES the wire may degrade; the file plane has nothing
    /// below it, and loopback cannot fail to bind.</summary>
    private static bool CanDegrade(PlaneCatalog.Plane p) =>
        p is PlaneCatalog.Plane.Wire or PlaneCatalog.Plane.Both;

    /// <summary>
    /// A bind failure is degradable; a programming error is not.
    ///
    /// Narrow on purpose. Catching everything here would turn a genuine defect in the client into
    /// a quiet degradation, which is exactly the shape of failure this feature exists to remove —
    /// it would just move it one level up.
    /// </summary>
    private static bool IsBindFailure(Exception ex) =>
        ex is InvalidOperationException
           or System.Net.Sockets.SocketException
           or IOException
           or UnauthorizedAccessException
           or System.Security.Cryptography.CryptographicException
           or ArgumentException;
}

/// <summary>Where a degradation is announced so that fleet-wide loss of the wire is visible as a
/// count rather than as N individually-honest hosts.</summary>
public interface IDegradedNotifier
{
    void Degraded(PlaneCatalog.Plane requested, PlaneCatalog.Plane live, string reason);
}
