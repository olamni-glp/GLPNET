//// glp/compiler/module_hierarchy — the directory `self.glp` scope chain (feature
//// 059 T078 Part B; typed-glp-manual §19.6). Port of Dart
//// `glp_runtime/lib/runtime/module_hierarchy.dart` `discoverSelfChain`.
////
//// Given a target module file and a project root, returns the ancestor `self.glp`
//// files visible to it, ROOT-FIRST (outermost ancestor first, nearest last) — the
//// order the type checker merges them (`loader.load_with_scope`) so the NEAREST
//// `self.glp` wins on a conflict (shadowing). Only the ancestor path from the
//// target's own directory up to (and including) the root is walked, so SIBLING
//// directories' `self.glp` files are never in the chain (sibling isolation).

import gleam/string

@external(erlang, "filename", "dirname")
fn dirname(path: String) -> String

@external(erlang, "filename", "join")
fn path_join(dir: String, file: String) -> String

@external(erlang, "filelib", "is_regular")
fn is_regular(path: String) -> Bool

// A stable comparison key: forward slashes (Windows tolerates both; Erlang
// `filename` emits `/`), so `starts_with`/equality behave across platforms.
fn norm(path: String) -> String {
  string.replace(path, "\\", "/")
}

/// The ancestor `self.glp` chain for `target_file` within `root_dir`, ROOT-FIRST.
/// If `target_file` is itself a `self.glp`, its own directory is excluded (the walk
/// starts from the parent), matching Dart `discoverSelfChain`.
pub fn discover_self_chain(target_file: String, root_dir: String) -> List(String) {
  let root = norm(root_dir)
  let target_dir = dirname(target_file)
  let start = case string.ends_with(norm(target_file), "/self.glp") {
    True -> dirname(target_dir)
    False -> target_dir
  }
  walk_up(start, root, [])
}

// Ascend from `dir` to `root` (inclusive), collecting each existing `<dir>/self.glp`.
// PREPENDING as we ascend yields ROOT-FIRST order (the nearest dir is visited first
// and prepended first, so it ends up LAST; the root is visited last and prepended
// last, so it ends up FIRST). Stops once we ascend above the root.
fn walk_up(dir: String, root: String, acc: List(String)) -> List(String) {
  let cur = norm(dir)
  case string.starts_with(cur, root) {
    False -> acc
    True -> {
      let self_path = path_join(dir, "self.glp")
      let acc2 = case is_regular(self_path) {
        True -> [self_path, ..acc]
        False -> acc
      }
      case cur == root {
        True -> acc2
        False -> walk_up(dirname(dir), root, acc2)
      }
    }
  }
}
