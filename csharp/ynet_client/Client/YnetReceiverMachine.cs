// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using Ynet.Client.Machine;

namespace Ynet.Client;

/// <summary>The outbound half of M6-b. Sending does not depend on an agent being attached.</summary>
public interface IYnetOutbound
{
    /// <summary>Send one message. Returns false when the plane refused it; never throws for a dead peer.</summary>
    bool Send(YnetMessage message);
}

/// <summary>Signals of the receiver machine. Values start above the reserved QEP range.</summary>
public static class YnetSignal
{
    public const int PlaneOpened = QSignal.UserSignalBase + 0;
    public const int MessageArrived = QSignal.UserSignalBase + 1;
    public const int AlertRaised = QSignal.UserSignalBase + 2;
    public const int Notified = QSignal.UserSignalBase + 3;
    public const int Fault = QSignal.UserSignalBase + 4;
    public const int Retry = QSignal.UserSignalBase + 5;
    public const int Stop = QSignal.UserSignalBase + 6;
}

/// <summary>
/// The glpnet M6 client: a QHSM that receives YNET messages, records a durable alert, and
/// announces it to the agent — none of which depends on an agent being present.
///
///     Top
///      ├── Booting            entry: open the plane
///      ├── Operational        handles Stop and Fault for every substate
///      │    ├── Idle          waiting for traffic
///      │    ├── Receiving     entry: record the alert durably   (the point of no loss)
///      │    └── Alerting      entry: announce it to the agent   (best effort, never blocking)
///      ├── Degraded           the plane faulted; Retry re-boots
///      └── Stopped            terminal
///
/// The ordering is the design, not an implementation detail: the alert is durable BEFORE the agent
/// is told, so an agent that is absent, asleep or mid-task cannot cause a message to be lost, and
/// a hook that fails leaves a pending alert rather than a gap.
/// </summary>
public sealed class YnetReceiverMachine : QActiveLite
{
    private readonly IYnetInbound _inbound;
    private readonly IYnetOutbound? _outbound;
    private readonly PendingAlertSpool _spool;
    private readonly AgentHook _hook;
    private readonly HookNotifier _notifier;
    private readonly List<string> _trace = new();

    private YnetMessage? _current;
    private PendingAlert? _currentAlert;
    private string? _faultReason;
    private string? _pendingFault;
    private string? _overflowSpoolError;
    private long _overflowed;

    public YnetReceiverMachine(
        IYnetInbound inbound,
        PendingAlertSpool spool,
        AgentHook hook,
        IYnetOutbound? outbound = null,
        int capacity = 1024)
        : base(capacity)
    {
        _inbound = inbound ?? throw new ArgumentNullException(nameof(inbound));
        _spool = spool ?? throw new ArgumentNullException(nameof(spool));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _notifier = new HookNotifier(_hook);
        _outbound = outbound;
        _inbound.Received += OnCarrierMessage;
    }

    /// <summary>Messages accepted from the carrier since construction.</summary>
    public long MessagesReceived { get; private set; }

    /// <summary>Alerts recorded durably since construction.</summary>
    public long AlertsRaised { get; private set; }

    /// <summary>The last hook attempt, including a failure reason. Null before the first attempt.</summary>
    public HookAttempt? LastHookAttempt => _notifier.Last;

    /// <summary>Block until every queued announcement has been attempted. For tests and shutdown.</summary>
    public bool WaitForNotifications(TimeSpan timeout) => _notifier.WaitForIdle(timeout);

    /// <summary>Announcements dropped because the notifier saturated. The ALERTS are still pending.</summary>
    public long NotificationsDropped => _notifier.Dropped;

    /// <summary>Messages the carrier offered that the bounded mailbox refused. Never silent.</summary>
    public long Overflowed => Interlocked.Read(ref _overflowed);

    /// <summary>Set only if even the durable overflow record could not be written.</summary>
    public string? OverflowSpoolError => _overflowSpoolError;

    /// <summary>Why a durable spool write failed, when one did. Non-null means the plane was closed.</summary>
    public string? SpoolError { get; private set; }

    /// <summary>Why the machine is degraded, when it is.</summary>
    public string? FaultReason => _faultReason;

    /// <summary>State names in the order they were entered and exited. Pinned by tests.</summary>
    public IReadOnlyList<string> Trace => _trace;

    /// <summary>Send on the outbound plane. Works whether or not an agent is attached (M6-b).</summary>
    public bool Send(YnetMessage message) => _outbound?.Send(message) ?? false;

    /// <summary>Alerts the agent has not yet drained.</summary>
    public IReadOnlyList<PendingAlert> Pending() => _spool.Undrained();

    /// <summary>The agent reports it has handled an alert. Idempotent.</summary>
    public bool DrainAlert(string alertId) => _spool.Drain(alertId);

    // ---- carrier callback -------------------------------------------------------------------

    private void OnCarrierMessage(YnetMessage m)
    {
        // The carrier thread only ENQUEUES. All state work happens on the machine's own thread,
        // so a burst of traffic cannot re-enter the machine and break run-to-completion.
        if (Post(new QEvt(YnetSignal.MessageArrived, m)) == AppendOutcome.Accepted) return;

        // OVERFLOW (codex cycle 1, P1). The first version posted a Fault into the SAME bounded
        // mailbox that had just refused a message — which is normally refused too, so the machine
        // never degraded and the message vanished while the carrier believed it delivered. A full
        // mailbox must therefore be reported on a path that needs no mailbox slot at all:
        //   1. the spool, which is independent of the mailbox, so the loss is DURABLE;
        //   2. a counter, so it is reportable;
        //   3. an internal event, which cannot be refused, so the machine actually degrades.
        Interlocked.Increment(ref _overflowed);
        try
        {
            _spool.Raise(
                $"OVERFLOW-{m.MessageId}", m.Origin,
                $"inbound mailbox at capacity ({Capacity}); message {m.MessageId} was NOT processed");
        }
        catch (Exception ex)
        {
            _overflowSpoolError = ex.GetType().Name + ": " + ex.Message;
        }

        RaiseFault($"inbound mailbox at capacity ({Capacity})");
    }

    /// <summary>Signal a fault on a path that cannot be refused by a full public mailbox.</summary>
    private void RaiseFault(string reason)
    {
        _pendingFault = reason;
        PostInternal(new QEvt(YnetSignal.Fault, reason));
    }

    // ---- states -----------------------------------------------------------------------------

    protected override QStateResult InitialTransition() => Tran(Booting);

    private QStateResult Booting(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Booting:entry");
                _inbound.Open();
                PostInternal(new QEvt(YnetSignal.PlaneOpened));
                return Handled();
            case QSignal.Exit:
                Mark("Booting:exit");
                return Handled();
            case YnetSignal.PlaneOpened:
                return Tran(Operational);
            default:
                return Super(Top);
        }
    }

    private QStateResult Operational(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Operational:entry");
                return Handled();
            case QSignal.Exit:
                Mark("Operational:exit");
                return Handled();
            case QSignal.Init:
                return Tran(Idle);
            case YnetSignal.Fault:
                _faultReason = e.Payload as string ?? "unspecified";
                return Tran(Degraded);
            case YnetSignal.Stop:
                return Tran(Stopped);
            default:
                return Super(Top);
        }
    }

    private QStateResult Idle(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Idle:entry");
                return Handled();
            case QSignal.Exit:
                Mark("Idle:exit");
                return Handled();
            case YnetSignal.MessageArrived:
                _current = e.Payload as YnetMessage;
                return _current is null ? Handled() : Tran(Receiving);
            default:
                return Super(Operational);
        }
    }

    private QStateResult Receiving(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Receiving:entry");
                var m = _current!;
                MessagesReceived++;
                // Durable BEFORE anyone is told. This is the line that makes an absent agent
                // survivable, and it is why the spool write is synchronous here.
                //
                // A FAILED durable write is a fault, not a shrug (codex cycle 1, P1). The first
                // version let the exception escape to DispatchGuarded, which published Faulted and
                // left the plane open — so a full disk or a permission error discarded the message
                // and the carrier went on delivering into the same hole. Losing the message is
                // exactly what this class exists to prevent, so the plane is closed instead:
                // refusing new traffic is recoverable, silently dropping it is not.
                try
                {
                    _currentAlert = _spool.Raise(m.MessageId, m.Origin, m.Summary);
                }
                catch (Exception ex)
                {
                    SpoolError = ex.GetType().Name + ": " + ex.Message;
                    RaiseFault($"durable spool write failed for {m.MessageId}: {SpoolError}");
                    return Handled();
                }

                AlertsRaised++;
                PostInternal(new QEvt(YnetSignal.AlertRaised));
                return Handled();
            case QSignal.Exit:
                Mark("Receiving:exit");
                return Handled();
            case YnetSignal.AlertRaised:
                return Tran(Alerting);
            default:
                return Super(Operational);
        }
    }

    private QStateResult Alerting(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Alerting:entry");
                // Announce only, and NOT on this thread (codex cycle 1, P1). The hook has a five
                // second bound, and the first version ran it inline on the single dispatch thread —
                // so a hanging agent stalled receipt for five seconds per alert, let the bounded
                // mailbox fill, and turned "an unavailable agent slows nothing down" into the
                // opposite. The alert is already durable, so the announcement can be late.
                var alert = _currentAlert!;
                _notifier.Enqueue(alert);
                PostInternal(new QEvt(YnetSignal.Notified));
                return Handled();
            case QSignal.Exit:
                Mark("Alerting:exit");
                return Handled();
            case YnetSignal.Notified:
                return Tran(Idle);
            default:
                return Super(Operational);
        }
    }

    private QStateResult Degraded(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Degraded:entry");
                _inbound.Close();
                return Handled();
            case QSignal.Exit:
                Mark("Degraded:exit");
                return Handled();
            case YnetSignal.Retry:
                _faultReason = null;
                return Tran(Booting);
            case YnetSignal.Stop:
                return Tran(Stopped);
            default:
                return Super(Top);
        }
    }

    private QStateResult Stopped(QEvt e)
    {
        switch (e.Signal)
        {
            case QSignal.Entry:
                Mark("Stopped:entry");
                _inbound.Close();
                return Handled();
            case QSignal.Exit:
                Mark("Stopped:exit");
                return Handled();
            default:
                return Super(Top);
        }
    }

    /// <summary>True when the machine is resting in Idle inside Operational.</summary>
    public bool IsIdle => IsIn(Idle);

    /// <summary>True when the machine has stopped.</summary>
    public bool IsStopped => IsIn(Stopped);

    /// <summary>True when the plane faulted.</summary>
    public bool IsDegraded => IsIn(Degraded);

    private void Mark(string what) => _trace.Add(what);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inbound.Received -= OnCarrierMessage;
            _notifier.Dispose();
        }
        base.Dispose(disposing);
    }
}
