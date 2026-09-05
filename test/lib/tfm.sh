# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
#
# ONE resolver for the C# REPL's target framework and binary path.
#
# Why this file exists
# --------------------
# The TFM was hard-coded as `net10.0` in SEVEN places: run_all_tests.sh and six
# sibling scripts (test/link/run_link_tests.sh, test/link/run_link_tests_cross.sh,
# test/parity/cross_runtime/lib.sh, test/parity/run_differential.sh,
# test/service_box/history_drill.sh, test/service_box/resume_drill.sh).
#
# On 2026-08-28 commit e9cb6f7f retargeted "all 23 csharp projects" to net11.0 on
# a denominator that EXCLUDED the three projects under out/csharp/ — one of which
# is this very REPL. run_all_tests.sh was then taught to DERIVE the TFM from the
# csproj, and the six siblings were not. The result is the worst available shape:
# the suite's own guard correctly refuses a stale binary and reports UNSEARCHABLE,
# while a drill run standalone silently executes an eleven-day-old net10.0 exe and
# reports PASS. A guard that only one of seven callers can see is not a guard.
#
# DISCIPLINE §1.3 — fix infrastructure, not symptoms: a fix that must be repeated
# in every file that uses a feature means the feature's infrastructure is broken.
# So the resolver is extracted here and sourced, rather than pasted seven times.
#
# Usage:
#     . "$(dirname "${BASH_SOURCE[0]}")/../lib/tfm.sh"     # adjust depth per caller
#     CSREPL="$(glp_repl_exe "$REPO_ROOT")" || { ...refuse... }

# Read <TargetFramework> out of a csproj. Prints nothing and returns 1 when the
# file is absent or carries no TFM — callers MUST treat that as "could not
# resolve", never as a default. Substituting a default here is exactly how the
# net10.0 pin survived a retarget.
csproj_tfm() {
    local _f="$1" _tfm
    [ -f "$_f" ] || return 1
    _tfm=$(sed -n 's:.*<TargetFramework>\([^<]*\)</TargetFramework>.*:\1:p' "$_f" | head -1)
    [ -n "$_tfm" ] || return 1
    printf '%s' "$_tfm"
}

# Absolute path to the built C# REPL for a repo root, with the TFM derived from
# the csproj. Returns 1 (printing nothing) if the TFM cannot be resolved, so a
# caller that ignores the status gets an empty path and fails loudly on use
# rather than falling back to a stale binary from an older framework.
glp_repl_exe() {
    local _root="$1" _tfm
    _tfm=$(csproj_tfm "$_root/out/csharp/glp_repl/glp_repl.csproj") || return 1
    printf '%s' "$_root/out/csharp/glp_repl/bin/Debug/$_tfm/glp_repl.exe"
}
