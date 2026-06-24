//// glp/runtime — placeholder for the GLP `runtime` subsystem.
////
//// Empty-but-building (feature 033): no ported semantics yet. The heavy port lands
//// in a downstream feature (F4+), filling this module from its Dart source of truth.
////
//// Dart source of truth: glp_runtime/lib/runtime/

/// Subsystem marker — keeps this placeholder non-dangling (it builds clean instead
/// of warning as an empty module) until the port replaces it. Carries no GLP semantics.
pub const subsystem = "runtime"
