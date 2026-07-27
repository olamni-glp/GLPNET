//// Tests for glp/link/primitives/link_kernels (T076) — the effectful link-kernel
//// recognition table the runner consults at the BODY Spawn label-miss.

import gleeunit/should
import glp/link/primitives/link_kernels

pub fn recognizes_all_seven_kernels_test() {
  link_kernels.is_link_kernel("_link_setup", 5) |> should.be_true
  link_kernels.is_link_kernel("_link_send", 3) |> should.be_true
  link_kernels.is_link_kernel("_link_request", 5) |> should.be_true
  link_kernels.is_link_kernel("_link_listen", 3) |> should.be_true
  link_kernels.is_link_kernel("_link_accept", 5) |> should.be_true
  link_kernels.is_link_kernel("_link_monitor", 2) |> should.be_true
  link_kernels.is_link_kernel("_link_close", 2) |> should.be_true
}

pub fn rejects_wrong_arity_test() {
  link_kernels.is_link_kernel("_link_setup", 3) |> should.be_false
  link_kernels.is_link_kernel("_link_close", 1) |> should.be_false
}

pub fn rejects_non_link_labels_test() {
  link_kernels.is_link_kernel("_send", 3) |> should.be_false
  link_kernels.is_link_kernel("consume", 2) |> should.be_false
}

pub fn name_constants_match_table_test() {
  link_kernels.is_link_kernel(link_kernels.setup_name, link_kernels.setup_arity)
  |> should.be_true
  link_kernels.is_link_kernel(link_kernels.close_name, link_kernels.close_arity)
  |> should.be_true
}
