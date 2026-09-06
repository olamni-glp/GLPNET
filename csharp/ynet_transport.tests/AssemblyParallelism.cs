// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using Xunit;

// 🔴 THIS ASSEMBLY DOES NOT RUN ITS COLLECTIONS IN PARALLEL, AND THAT IS A STATEMENT OF FACT ABOUT
// THE TESTS, NOT A WORKAROUND FOR A FLAKY ONE.
//
// The mechanism, measured 2026-09-06 on an IDLE machine:
//
//   * `YnetSession.Accept` and `InProcessDuplexChannel` are SYNCHRONOUS AND BLOCKING by design — a
//     handshake waits for its peer. Several integration tests therefore park a thread-pool thread
//     for the whole handshake (`Task.Run(() => YnetSession.Accept(...))` followed by
//     `.GetAwaiter().GetResult()`, which blocks a second thread — CoreTransportTests.cs:127 and
//     NatTraversalTests.cs:73; xUnit's own analyzer flags the latter as xUnit1031).
//   * The .NET thread pool injects new threads at roughly ONE PER SECOND once its minimum is
//     exhausted. Blocked threads are not returned.
//   * Other tests in this assembly assert on WALL-CLOCK TIMEOUTS (the capability probe, the QUIC
//     handshake). When the blocking tests hold the pool, those timeouts elapse before their work is
//     ever scheduled, and they report the honest-but-wrong answer "unavailable".
//
// The measurement that settles it:
//
//   parallel collections ON  ->  214-216 / 217, and WHICH tests fail CHANGES RUN TO RUN
//   parallel collections OFF ->  217 / 217, reproducibly
//
// The varying failure set is the tell: no single test is broken. The suite was measuring the
// scheduler. A green that depends on how many other tests happen to be running is not a control,
// and this repo has already been bitten once by a probe that passed against code it was meant to
// catch.
//
// Two fixes were tried and MEASURED NOT TO WORK before this one, and both are recorded so nobody
// repeats them: (1) moving the stub sidecar's accept loop to a dedicated thread — it addressed only
// the double, not the pool; (2) removing sync-over-async from the product's own probe — a genuine
// defect, fixed and KEPT in IrohSidecarProvider, but not the cause here.
//
// The durable fix is to make `Accept` non-blocking so the tests need no pool thread at all. That is
// a product change with its own spec and review, tracked on the roadmap; until it lands, running
// these collections sequentially is the truthful description of tests that contend for real OS
// sockets and real pool threads.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
