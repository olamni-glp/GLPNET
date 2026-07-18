//// glp/link/reliability/crc32 — standard IEEE 802.3 / zlib CRC-32 (reflected,
//// polynomial `0xEDB88320`, init/final `0xFFFFFFFF`). Feature 050 T046.
////
//// Byte-identical to `glp_runtime/lib/link/reliability/crc32.dart` and
//// `csharp/glp_link/reliability/Crc32.cs` (FR-060/061): the universally-specified
//// CRC variant, so no shared table needs syncing across runtimes. Pure Gleam
//// (stdlib + bit-syntax only, no FFI) → AtomVM-portable, like the 038 term codec.
////
//// The Dart reference precomputes a 256-entry table; here the per-index reduction
//// is computed inline (`table_entry`), which yields the identical checksum without
//// module-level mutable state. The running CRC is kept masked to 32 bits (always
//// non-negative), so `bitwise_shift_right` is logical and matches Dart's masked
//// unsigned `>> 8`.

import gleam/int

const polynomial = 0xEDB88320

const mask32 = 0xFFFFFFFF

/// CRC-32 of `data` (mirrors Dart `Crc32.compute` / C# `Crc32.Compute`).
pub fn compute(data: BitArray) -> Int {
  int.bitwise_and(int.bitwise_exclusive_or(fold(data, mask32), mask32), mask32)
}

fn fold(data: BitArray, crc: Int) -> Int {
  case data {
    <<b:int, rest:bits>> -> {
      let idx = int.bitwise_and(int.bitwise_exclusive_or(crc, b), 0xFF)
      let crc2 =
        int.bitwise_exclusive_or(
          table_entry(idx),
          int.bitwise_shift_right(crc, 8),
        )
      fold(rest, crc2)
    }
    _ -> crc
  }
}

/// The Dart `_buildTable()` entry for `index`, computed on demand: start `c = index`,
/// eight times `c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1`.
fn table_entry(index: Int) -> Int {
  table_step(index, 8)
}

fn table_step(c: Int, k: Int) -> Int {
  case k <= 0 {
    True -> int.bitwise_and(c, mask32)
    False -> {
      let c2 = case int.bitwise_and(c, 1) != 0 {
        True ->
          int.bitwise_exclusive_or(polynomial, int.bitwise_shift_right(c, 1))
        False -> int.bitwise_shift_right(c, 1)
      }
      table_step(c2, k - 1)
    }
  }
}
