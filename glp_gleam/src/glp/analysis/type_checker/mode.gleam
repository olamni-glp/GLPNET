//// glp/analysis/type_checker/mode — data-flow modes for moded type checking
//// (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/mode.dart.
//// Modes represent data-flow direction per paper Definition 4.2:
//// consume (↓) = input = caller writes, callee reads (`Type?` syntax);
//// produce (↑) = output = callee writes, caller reads (default, no `?`).

pub type Mode {
  /// Callee writes, caller reads (default, no `?` marker) = produce (↑).
  Output
  /// Caller writes, callee reads (`Type?` syntax) = consume (↓).
  Input
}

/// Mode dual operator per Definition 5.1 — used at call boundaries: the
/// caller's output is the callee's input.
pub fn dual(mode: Mode) -> Mode {
  case mode {
    Output -> Input
    Input -> Output
  }
}

/// Alias for `dual` (paper notation).
pub fn flip(mode: Mode) -> Mode {
  dual(mode)
}

/// Combine an embedded mode with its parent mode (involution property): when
/// a type appears inside another type's definition, same modes cancel to
/// Output, different modes combine to Input — XOR (Dart `combineMode`).
pub fn combine_mode(parent: Mode, embedded: Mode) -> Mode {
  case parent == embedded {
    True -> Output
    False -> Input
  }
}

/// Dart `Mode.toString()`.
pub fn to_string(mode: Mode) -> String {
  case mode {
    Output -> "output"
    Input -> "input"
  }
}
