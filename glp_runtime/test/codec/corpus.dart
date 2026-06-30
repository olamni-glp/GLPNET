// Shared in-scope result corpus — feature 038-result-codec-and-framecodec-ride.
//
// The non-gated M1 result corpus (FB-M1-17 deref / FB-M1-41 status+bindings /
// FB-M1-42 suspended) authored from the Dart reference (source of truth, R9).
// Floats (ConstReal, gated ED-6), 64-bit-int edges, and cyclic terms are NOT here —
// they are quarantined in a separate gated section (tasks T039–T041, R11).
//
// Each entry is a named logical result. These same values feed the round-trip,
// no-heap, in-process-vs-bytes, and (later) golden byte-parity tests.

import 'dart:convert';
import 'dart:typed_data';

import 'package:glp_runtime/codec/result_envelope.dart';

// --- tiny term builders for readable corpus entries ---
Term atom(String a) => ConstTerm(ConstAtom(a));
Term int_(int n) => ConstTerm(ConstInt(n));
Term str(String s) => ConstTerm(ConstString(s));
Term struct(String f, List<Term> args) => StructTerm(f, args);
Term varRef(String agent, int local) => VarRef(GlobalVarId(agent, local));

/// A GLP list as cons/nil cells: `[a,b,c]` = `.(a, .(b, .(c, nil)))`.
Term glpList(List<Term> elems) {
  Term acc = atom('nil');
  for (var i = elems.length - 1; i >= 0; i--) {
    acc = struct('.', [elems[i], acc]);
  }
  return acc;
}

/// The non-gated in-scope corpus: name → envelope.
final Map<String, ResultEnvelope> nonGatedCorpus = {
  // --- trivial ---
  'empty_success': ResultEnvelope(status: ExecutionStatus.success),

  // --- FB-M1-41 status + single bindings of each constant kind ---
  'success_atom': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {'X': atom('foo')},
  ),
  'success_int': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {'N': int_(42)},
  ),
  'success_int_negative': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {'N': int_(-7)},
  ),
  'success_string': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {'S': str('hello, world')},
  ),

  // --- FB-M1-17 deref: nested bound structures + lists ---
  'success_nested_struct': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {
      'T': struct('point', [int_(1), int_(2)]),
    },
  ),
  'success_list': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {
      'L': glpList([int_(1), int_(2), int_(3)]),
    },
  ),
  'success_deep_nested': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {
      'Tree': struct('node', [
        struct('node', [atom('leaf'), int_(1)]),
        struct('node', [atom('leaf'), int_(2)]),
      ]),
    },
  ),

  // --- multiple bindings: canonical (declaration) order matters for parity ---
  'multi_binding': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {
      'A': atom('alpha'),
      'B': int_(2),
      'C': str('gamma'),
    },
  ),

  // --- FB-M1-17/14 var→writer: a remaining unbound VarRef carries a GlobalVarId ---
  'var_to_writer': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {
      'Z': struct('pair', [atom('a'), varRef('agent1', 9)]),
    },
    varToWriter: {'W': const GlobalVarId('agent1', 9)},
  ),

  // --- FB-M1-42 suspended goal + blocking-reader set ---
  'suspended': ResultEnvelope(
    status: ExecutionStatus.suspended,
    suspended: const [
      GlobalVarId('agent1', 3),
      GlobalVarId('agent2', 5),
    ],
  ),
  'suspended_with_binding': ResultEnvelope(
    status: ExecutionStatus.suspended,
    resolvedBindings: {'Partial': struct('waiting_on', [varRef('agent1', 11)])},
    varToWriter: {'Q': const GlobalVarId('agent1', 11)},
    suspended: const [GlobalVarId('agent1', 11)],
  ),

  // --- failed with error ---
  'failed': ResultEnvelope(
    status: ExecutionStatus.failed,
    error: 'no matching clause for goal foo/2',
  ),

  // --- captured output (carried; excluded from byte-parity, included in round-trip) ---
  'with_captured': ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {'R': atom('done')},
    captured: Uint8List.fromList(utf8.encode('line one\nline two\n')),
  ),
};

/// The byte-parity subset (SC-002): non-gated entries with EMPTY `captured`, so the
/// golden hex needs no masking (R4 excludes captured from the parity criterion). This
/// is the corpus the cross-runtime golden (`contracts/golden/corpus.hex`) pins.
final Map<String, ResultEnvelope> goldenCorpus = Map.fromEntries(
  nonGatedCorpus.entries.where((e) => e.value.captured.isEmpty),
);
