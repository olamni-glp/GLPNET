// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// M6-a: a REAL hierarchical state machine, not a loop with a switch.
//
// This QEP-semantics core is deliberately written against the INTERFACE rather than copied from
// one of the four C# QHsm implementations measured under D:/yngenios/yngenios/l0 on 2026-09-05
// (l0/kernel, l0/olamnit.kernel.qp, l0/runtime.qp, l0/yngenios.core.qp). Broadcast
// 2026-09-05T10:50Z asked @yngcor to name the canonical one before 19 M6 clients each pick one.
// When it is named, THIS FILE IS THE ONE THAT IS DELETED and the machine in Client/ re-targets the
// canonical core: nothing outside Qhsm/ depends on this implementation.

namespace Ynet.Client.Machine;

/// <summary>Reserved QEP signals. User signals start at <see cref="UserSignalBase"/>.</summary>
public static class QSignal
{
    /// <summary>Asks a handler for its superstate; never handled by user code.</summary>
    public const int Empty = 0;

    /// <summary>Initial transition into a state's default substate.</summary>
    public const int Init = 1;

    /// <summary>State entry action.</summary>
    public const int Entry = 2;

    /// <summary>State exit action.</summary>
    public const int Exit = 3;

    /// <summary>First signal available to user machines.</summary>
    public const int UserSignalBase = 4;
}

/// <summary>An event dispatched to a machine. Immutable; payload is opaque to the core.</summary>
public sealed record QEvt(int Signal, object? Payload = null)
{
    internal static readonly QEvt EmptyEvt = new(QSignal.Empty);
    internal static readonly QEvt InitEvt = new(QSignal.Init);
    internal static readonly QEvt EntryEvt = new(QSignal.Entry);
    internal static readonly QEvt ExitEvt = new(QSignal.Exit);
}

/// <summary>What a state handler did with an event.</summary>
public enum QStateResult
{
    /// <summary>The handler consumed the event; no transition.</summary>
    Handled,

    /// <summary>The handler declined the event and named no superstate (top only).</summary>
    Ignored,

    /// <summary>The handler is taking a transition to the target it recorded.</summary>
    Transition,

    /// <summary>The handler did not consume the event; it recorded its superstate.</summary>
    Super,
}

/// <summary>A state, as a handler function. Hierarchy is expressed by returning a superstate.</summary>
public delegate QStateResult QStateHandler(QEvt e);
