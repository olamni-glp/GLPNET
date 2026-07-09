#!/usr/bin/env escript
%% T028 / FR-015 #7 regression harness (feature 049): drives glpq_ffi:relay/3 against a child that
%% emits a >1 MiB data envelope. The relay's {line,1048576} port splits that line into noeol
%% fragments; the reassembly fix must deliver it WHOLE to stdout (data plane) — the pre-fix relay
%% misrouted the fragments to stderr (silent data loss). Invoked by run_glpq_ffi_reassembly_test.sh.
main([BeamDir, Python, Script]) ->
    true = code:add_patha(BeamDir),
    PythonAbs = case os:find_executable(Python) of
        false -> Python;
        P -> P
    end,
    Status = glpq_ffi:relay(PythonAbs, Script, []),
    halt(Status);
main(_) ->
    io:put_chars(standard_error, "usage: glpq_ffi_reassembly_test.escript <beam-dir> <python> <script>\n"),
    halt(64).
