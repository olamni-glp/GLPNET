// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

namespace Ynet.Client.Machine;

/// <summary>
/// A hierarchical state machine with QEP semantics: states are handlers that name their
/// superstate, transitions exit from the source up to the least common ancestor and enter down to
/// the target, and every dispatch runs to completion before the next one is taken.
///
/// The ordering guarantees are the point of the class, and they are pinned by tests rather than
/// asserted: a transition between nested states must exit innermost-first and enter outermost-first,
/// an external self-transition must exit and re-enter, and a target with an initial transition must
/// drill into its default substate.
/// </summary>
public abstract class Qhsm
{
    private QStateHandler _state;
    private QStateHandler _temp;
    private bool _started;

    /// <summary>Guards against a handler dispatching into its own machine mid-transition.</summary>
    private bool _dispatching;

    protected Qhsm()
    {
        _state = Top;
        _temp = Top;
    }

    /// <summary>The current leaf state. Meaningful only after <see cref="Start"/>.</summary>
    public QStateHandler State => _state;

    /// <summary>True once <see cref="Start"/> has completed the initial transition.</summary>
    public bool Started => _started;

    /// <summary>The implicit root. Ignores everything; every other state reaches it via Super.</summary>
    protected QStateResult Top(QEvt e) => QStateResult.Ignored;

    /// <summary>Record a transition target.</summary>
    protected QStateResult Tran(QStateHandler target)
    {
        _temp = target;
        return QStateResult.Transition;
    }

    /// <summary>Record this state's superstate; the event was not consumed.</summary>
    protected QStateResult Super(QStateHandler super)
    {
        _temp = super;
        return QStateResult.Super;
    }

    /// <summary>The event was consumed here.</summary>
    protected static QStateResult Handled() => QStateResult.Handled;

    /// <summary>The machine's initial pseudostate: return <see cref="Tran"/> to the first state.</summary>
    protected abstract QStateResult InitialTransition();

    /// <summary>Run the initial transition and drill into default substates.</summary>
    public void Start()
    {
        if (_started) throw new InvalidOperationException("machine already started");

        var r = InitialTransition();
        if (r != QStateResult.Transition)
            throw new InvalidOperationException("InitialTransition must return Tran(target)");

        // Enter from just below Top down to the recorded target, then drill in.
        var target = _temp;
        foreach (var s in PathFromBelow(Top, target)) Invoke(s, QEvt.EntryEvt);
        _state = target;
        DrillInit();
        _started = true;
    }

    /// <summary>
    /// Dispatch one event, run-to-completion. Returns true when some state consumed it (a
    /// transition counts as consumption); false when it reached Top unhandled.
    /// </summary>
    public bool Dispatch(QEvt e)
    {
        if (!_started) throw new InvalidOperationException("Dispatch before Start");
        if (e.Signal < QSignal.UserSignalBase)
            throw new ArgumentException("reserved signal cannot be dispatched externally", nameof(e));
        if (_dispatching)
            throw new InvalidOperationException("re-entrant Dispatch violates run-to-completion");

        _dispatching = true;
        try
        {
            // Walk up from the current leaf until a state consumes the event.
            var s = _state;
            while (true)
            {
                var r = Invoke(s, e);
                if (r == QStateResult.Handled) return true;
                if (r == QStateResult.Transition)
                {
                    Transition(source: s, target: _temp);
                    return true;
                }
                if (r == QStateResult.Ignored) return false;   // reached Top
                s = _temp;                                      // Super: keep walking up
            }
        }
        finally
        {
            _dispatching = false;
        }
    }

    /// <summary>True when <paramref name="probe"/> is the current state or one of its ancestors.</summary>
    public bool IsIn(QStateHandler probe)
    {
        for (var s = _state; s != Top; s = SuperOf(s))
            if (s == probe) return true;
        return probe == Top;
    }

    // ---- transition machinery -------------------------------------------------------------

    private void Transition(QStateHandler source, QStateHandler target)
    {
        // Exit the states between the current leaf and the state that handled the event.
        for (var s = _state; s != source; s = SuperOf(s)) Invoke(s, QEvt.ExitEvt);

        if (source == target)
        {
            // External self-transition: exit and re-enter, so entry actions run again.
            Invoke(source, QEvt.ExitEvt);
            Invoke(target, QEvt.EntryEvt);
        }
        else
        {
            var lca = LeastCommonAncestor(source, target);
            for (var s = source; s != lca; s = SuperOf(s)) Invoke(s, QEvt.ExitEvt);
            foreach (var s in PathFromBelow(lca, target)) Invoke(s, QEvt.EntryEvt);
        }

        _state = target;
        DrillInit();
    }

    /// <summary>Run initial transitions until the machine rests in a leaf.</summary>
    private void DrillInit()
    {
        while (Invoke(_state, QEvt.InitEvt) == QStateResult.Transition)
        {
            var target = _temp;
            foreach (var s in PathFromBelow(_state, target)) Invoke(s, QEvt.EntryEvt);
            _state = target;
        }
    }

    private QStateHandler SuperOf(QStateHandler s)
    {
        var r = Invoke(s, QEvt.EmptyEvt);
        if (r == QStateResult.Ignored) return Top;      // s is Top
        if (r != QStateResult.Super)
            throw new InvalidOperationException("a state must answer the Empty signal with Super(...)");
        return _temp;
    }

    private QStateHandler LeastCommonAncestor(QStateHandler source, QStateHandler target)
    {
        var sourceChain = new List<QStateHandler>();
        for (var s = source; ; s = SuperOf(s))
        {
            sourceChain.Add(s);
            if (s == Top) break;
        }

        for (var t = target; ; t = SuperOf(t))
        {
            if (sourceChain.Contains(t)) return t;
            if (t == Top) break;
        }

        return Top;
    }

    /// <summary>States strictly below <paramref name="ancestor"/> down to and including <paramref name="target"/>, outermost first.</summary>
    private List<QStateHandler> PathFromBelow(QStateHandler ancestor, QStateHandler target)
    {
        var path = new List<QStateHandler>();
        for (var s = target; s != ancestor; s = SuperOf(s))
        {
            path.Add(s);
            if (s == Top) throw new InvalidOperationException("target is not a descendant of the ancestor");
        }
        path.Reverse();
        return path;
    }

    private QStateResult Invoke(QStateHandler s, QEvt e) => s(e);
}
