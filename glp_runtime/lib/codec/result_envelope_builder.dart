// Engine→envelope builder + server-side deep-resolve — feature 038.
//
// Bridges the live Dart engine (heap + DrainResult) to the heap-independent
// ResultEnvelope value (data-model §1, contract §6). Kept in its own module so the
// codec value types (result_envelope.dart) stay engine-free.
//
// Deep-resolve (T017): walk a runtime term over the heap to depth-32 — matching the
// C# reference `_ResolveDeepForTrace` (glp_engine.cs:607; depth-32, recurse into
// StructTerm args over the shallow `heap.dereference`) — and map it to the codec
// Term model. Unlike the C# reference, hitting the depth bound yields an EXPLICIT
// `$truncated` marker term, never a silent cut (R5 / Constitution II).
//
// Builder (T018): assemble the envelope from the engine result + DrainResult.
// Owner-ruled mappings (2026-06-30):
//   - every `rt.ConstTerm(String)` is an ATOM (Dart GLP has no heap-level string
//     literal; tag 0x05);
//   - `GlobalVarId.agentId` = a per-glpnet-instance unique id; `localId` = the local
//     writer/heap addr;
//   - the `suspended` set = `DrainResult.blockingReaders` (reader addrs) mapped to
//     GlobalVarId, sorted+deduped.

import 'dart:typed_data';

import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/terms.dart' as rt;
import 'package:glp_runtime/runtime/scheduler.dart' as sched;

import 'result_envelope.dart';

/// Deep-resolve depth bound — matches the Dart/C# reference (`_ResolveDeepForTrace`).
const int deepResolveDepth = 32;

/// The explicit truncation marker emitted at the depth bound (R5, contract §6).
/// A normal, decodable term — deterministic and parity-stable; never a silent cut.
Term truncatedMarker() => StructTerm(r'$truncated', const []);

/// Map a runtime constant value to the codec Constant model.
/// Owner-ruled (#2): every String is an atom; Dart GLP carries no heap-level string.
Constant _mapConstValue(Object? value) {
  if (value is int) return ConstInt(value);
  if (value is double) return ConstReal(value);
  if (value is String) return ConstAtom(value);
  // bool / null / other have no GLP result-term representation — surface, never mask.
  throw ResultCodecException(
      'deep-resolve: unsupported constant value of runtime type '
      '${value.runtimeType} ($value)');
}

/// Server-side deep-resolve of a runtime term to the heap-independent codec Term.
///
/// [instanceId] is this glpnet instance's unique id (the `agentId` of every
/// GlobalVarId minted here). Resolves bound vars via the shallow `heap.dereference`
/// and recurses into StructTerm args to [deepResolveDepth]; an unbound VarRef becomes
/// `VarRef(GlobalVarId(instanceId, addr))`; the depth bound yields [truncatedMarker].
Term deepResolveTerm(
  GlpRuntime runtime,
  rt.Term term,
  String instanceId, {
  int depth = 0,
}) {
  if (depth > deepResolveDepth) return truncatedMarker();
  final d = runtime.heap.dereference(term);
  if (d is rt.StructTerm) {
    final args = <Term>[
      for (final a in d.args)
        deepResolveTerm(runtime, a, instanceId, depth: depth + 1),
    ];
    return StructTerm(d.functor, args);
  } else if (d is rt.ConstTerm) {
    return ConstTerm(_mapConstValue(d.value));
  } else if (d is rt.VarRef) {
    // still unbound after deref → global writer identity (R7)
    return VarRef(GlobalVarId(instanceId, d.addr));
  } else {
    // MutualRefTerm / ModuleTerm etc. are outside the envelope term model.
    throw ResultCodecException(
        'deep-resolve: unsupported runtime term ${d.runtimeType}');
  }
}

/// Map the runtime ExecutionStatus to the codec ExecutionStatus.
ExecutionStatus _mapStatus(sched.ExecutionStatus status) {
  switch (status) {
    case sched.ExecutionStatus.succeeded:
      return ExecutionStatus.success;
    case sched.ExecutionStatus.suspended:
      return ExecutionStatus.suspended;
    case sched.ExecutionStatus.failed:
      return ExecutionStatus.failed;
  }
}

/// Build the heap-independent [ResultEnvelope] from a finished engine run (T018).
///
/// - [queryVarWriters]: query var name → its writer heap id (the engine's
///   `queryVarWriters`). A bound writer → a deep-resolved binding; an unbound writer
///   → a `var→writer` global-id entry.
/// - [drainResult]: the scheduler's final `DrainResult`; `blockingReaders` (reader
///   addrs) become the `suspended` GlobalVarId set (sorted, deduped).
/// - [instanceId]: this glpnet instance's unique id (the GlobalVarId `agentId`).
ResultEnvelope buildResultEnvelope({
  required GlpRuntime runtime,
  required Map<String, int> queryVarWriters,
  required sched.DrainResult drainResult,
  required String instanceId,
  List<int>? captured,
  String? error,
}) {
  final resolvedBindings = <String, Term>{};
  final varToWriter = <String, GlobalVarId>{};

  for (final entry in queryVarWriters.entries) {
    final name = entry.key;
    final writerId = entry.value;
    if (runtime.heap.isBound(writerId)) {
      resolvedBindings[name] =
          deepResolveTerm(runtime, rt.VarRef(writerId), instanceId);
    } else {
      varToWriter[name] = GlobalVarId(instanceId, writerId);
    }
  }

  // suspended = blocking-reader addrs → global ids, deterministic (sorted) + deduped
  final suspendedAddrs = drainResult.blockingReaders.toList()..sort();
  final suspended = <GlobalVarId>[
    for (final addr in suspendedAddrs) GlobalVarId(instanceId, addr),
  ];

  return ResultEnvelope(
    status: _mapStatus(drainResult.status),
    resolvedBindings: resolvedBindings,
    varToWriter: varToWriter,
    suspended: suspended,
    captured: captured == null ? null : Uint8List.fromList(captured),
    error: error,
  );
}
