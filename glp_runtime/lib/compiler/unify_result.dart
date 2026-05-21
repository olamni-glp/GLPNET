import 'ast.dart' show Term;

/// Result of compile-time GLP unification for partial evaluation.
sealed class UnifyResult {}

class UnifySuccess extends UnifyResult {
  final Map<String, Term> substitution;
  UnifySuccess(this.substitution);
}

class UnifyFail extends UnifyResult {
  final String reason;
  UnifyFail(this.reason);
}

class UnifySuspend extends UnifyResult {
  final Set<String> unboundReaders;
  UnifySuspend(this.unboundReaders);
}
