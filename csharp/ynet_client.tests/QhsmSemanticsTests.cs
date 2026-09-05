// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// M6-a says a real hierarchical state machine, "not a loop with a switch". These tests are what
// makes that claim falsifiable: a switch statement passes none of them, because none of them
// assert on the resulting state alone — they assert on the ORDER of entry and exit actions, which
// is the only externally visible difference between a hierarchy and a flat dispatch.

using Ynet.Client.Machine;

namespace Ynet.Client.Tests;

public sealed class QhsmSemanticsTests
{
    private const int Go = QSignal.UserSignalBase + 0;
    private const int Deep = QSignal.UserSignalBase + 1;
    private const int Self = QSignal.UserSignalBase + 2;
    private const int Out = QSignal.UserSignalBase + 3;

    /// <summary>
    ///   Top
    ///    ├── A
    ///    │    └── A1  (A's default substate)
    ///    └── B
    ///         └── B1  (B's default substate)
    /// </summary>
    private sealed class Fixture : Qhsm
    {
        public readonly List<string> Log = new();

        protected override QStateResult InitialTransition() => Tran(A);

        public QStateResult A(QEvt e)
        {
            switch (e.Signal)
            {
                case QSignal.Entry: Log.Add("A+"); return Handled();
                case QSignal.Exit: Log.Add("A-"); return Handled();
                case QSignal.Init: return Tran(A1);
                case Out: return Tran(B);
                default: return Super(Top);
            }
        }

        public QStateResult A1(QEvt e)
        {
            switch (e.Signal)
            {
                case QSignal.Entry: Log.Add("A1+"); return Handled();
                case QSignal.Exit: Log.Add("A1-"); return Handled();
                case Go: return Tran(B);
                case Deep: return Tran(B1);
                case Self: return Tran(A1);
                default: return Super(A);
            }
        }

        public QStateResult B(QEvt e)
        {
            switch (e.Signal)
            {
                case QSignal.Entry: Log.Add("B+"); return Handled();
                case QSignal.Exit: Log.Add("B-"); return Handled();
                case QSignal.Init: return Tran(B1);
                default: return Super(Top);
            }
        }

        public QStateResult B1(QEvt e)
        {
            switch (e.Signal)
            {
                case QSignal.Entry: Log.Add("B1+"); return Handled();
                case QSignal.Exit: Log.Add("B1-"); return Handled();
                default: return Super(B);
            }
        }
    }

    [Fact]
    public void Start_enters_outermost_first_and_drills_into_the_default_substate()
    {
        var m = new Fixture();
        m.Start();

        Assert.Equal(new[] { "A+", "A1+" }, m.Log);
        Assert.True(m.IsIn(m.A1));
        Assert.True(m.IsIn(m.A));       // a leaf is "in" its ancestors — the hierarchy is real
        Assert.False(m.IsIn(m.B));
    }

    [Fact]
    public void Transition_exits_innermost_first_then_enters_outermost_first()
    {
        var m = new Fixture();
        m.Start();
        m.Log.Clear();

        m.Dispatch(new QEvt(Go));   // A1 -> B, LCA is Top

        // A switch statement would produce the state B1 without this ordering.
        Assert.Equal(new[] { "A1-", "A-", "B+", "B1+" }, m.Log);
        Assert.True(m.IsIn(m.B1));
    }

    [Fact]
    public void Transition_to_a_nested_target_enters_the_ancestor_before_the_target()
    {
        var m = new Fixture();
        m.Start();
        m.Log.Clear();

        m.Dispatch(new QEvt(Deep));  // A1 -> B1 directly

        Assert.Equal(new[] { "A1-", "A-", "B+", "B1+" }, m.Log);
    }

    [Fact]
    public void External_self_transition_exits_and_re_enters()
    {
        var m = new Fixture();
        m.Start();
        m.Log.Clear();

        m.Dispatch(new QEvt(Self));  // A1 -> A1

        Assert.Equal(new[] { "A1-", "A1+" }, m.Log);
        Assert.True(m.IsIn(m.A1));
    }

    [Fact]
    public void An_event_handled_by_a_superstate_exits_only_down_to_that_superstate()
    {
        var m = new Fixture();
        m.Start();
        m.Log.Clear();

        m.Dispatch(new QEvt(Out));   // A1 does not handle Out; A does, and transitions to B

        Assert.Equal(new[] { "A1-", "A-", "B+", "B1+" }, m.Log);
    }

    [Fact]
    public void An_unhandled_event_reaches_top_and_is_reported_unhandled()
    {
        var m = new Fixture();
        m.Start();
        m.Log.Clear();

        var handled = m.Dispatch(new QEvt(QSignal.UserSignalBase + 99));

        Assert.False(handled);
        Assert.Empty(m.Log);          // negative control: nothing was entered or exited
        Assert.True(m.IsIn(m.A1));
    }

    [Fact]
    public void Dispatch_before_start_is_refused()
    {
        var m = new Fixture();
        Assert.Throws<InvalidOperationException>(() => m.Dispatch(new QEvt(Go)));
    }

    [Fact]
    public void Reserved_signals_cannot_be_dispatched_from_outside()
    {
        var m = new Fixture();
        m.Start();
        Assert.Throws<ArgumentException>(() => m.Dispatch(new QEvt(QSignal.Entry)));
    }

    [Fact]
    public void Starting_twice_is_refused()
    {
        var m = new Fixture();
        m.Start();
        Assert.Throws<InvalidOperationException>(() => m.Start());
    }
}
