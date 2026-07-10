//// Dependency-policy tripwire (feature 050, T012 / FR-007 / analyze C1): the
//// OTP-abstraction package must never appear in gleam.toml or manifest.toml —
//// its proc_lib use is outside AtomVM's BEAM/OTP subset (F1 dossier §3), and
//// the AtomVM-compatibility-by-construction posture of this port depends on
//// plain spawn + Subjects only. Fails LOUDLY the moment anyone adds it,
//// directly or transitively.
////
//// File reads use the direct OTP `file:read_file/1` FFI (established 038
//// precedent — no new dependency); `gleam test` runs from the package root.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/list
import gleam/string
import gleeunit/should

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

/// Packages that abstract OTP behaviours (forbidden by FR-007).
const forbidden_packages = ["gleam_otp"]

fn dependency_lines(path: String) -> List(String) {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  string.split(text, "\n")
  // Comment lines may legitimately DISCUSS the exclusion — strip them so the
  // tripwire fires on actual dependency entries only.
  |> list.filter(fn(line) { !string.starts_with(string.trim(line), "#") })
}

pub fn gleam_toml_has_no_otp_abstraction_test() {
  dependency_lines("gleam.toml")
  |> list.each(fn(line) {
    forbidden_packages
    |> list.each(fn(package) {
      string.contains(line, package)
      |> should.be_false
    })
  })
}

pub fn manifest_toml_has_no_otp_abstraction_test() {
  dependency_lines("manifest.toml")
  |> list.each(fn(line) {
    forbidden_packages
    |> list.each(fn(package) {
      string.contains(line, package)
      |> should.be_false
    })
  })
}
