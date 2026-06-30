%% M2-0 spike: verify erlang:monitor/2 + 'DOWN' behavior on AtomVM 0.6.6.
%% start/0 is the AtomVM entry point; also runs unchanged on stock OTP BEAM.
%%
%% Uses erlang:monitor(process, Pid) DIRECTLY (FR-001), with a go/exit
%% handshake so the monitor is established BEFORE the monitored process exits
%% (otherwise a fast normal exit races us and we'd observe noproc, not normal).
%% Probes three cases and prints the observed message (or `timeout`) for each:
%%   (1) monitored process exits NORMALLY   -> expect {down, normal}
%%   (2) monitored process exits ABNORMALLY -> expect {down, boom}
%%   (3) monitor an ALREADY-DEAD pid        -> expect {down, noproc}
%% Also records whether the spawn_monitor/1 convenience BIF is available
%% (it is on OTP; it is NOT on AtomVM 0.6.6 -- M2 code must use spawn + monitor).
-module(monitor_probe).
-export([start/0]).

start() ->
    %% Availability of the spawn_monitor/1 convenience BIF (secondary data point).
    SM = try erlang:spawn_monitor(fun() -> ok end) of
             {_P, _R} -> available
         catch Cls:Err -> {unavailable, Cls, Err} end,
    erlang:display({spawn_monitor, SM}),

    %% Case 1: monitor/2 a process that then exits NORMALLY.
    P1 = spawn(fun() -> receive go -> ok end end),
    Ref1 = erlang:monitor(process, P1),
    P1 ! go,
    R1 = receive {'DOWN', Ref1, process, P1, Reason1} -> {down, Reason1}
         after 3000 -> timeout end,
    erlang:display({normal_exit, R1}),

    %% Case 2: monitor/2 a process that then exits ABNORMALLY.
    P2 = spawn(fun() -> receive go -> exit(boom) end end),
    Ref2 = erlang:monitor(process, P2),
    P2 ! go,
    R2 = receive {'DOWN', Ref2, process, P2, Reason2} -> {down, Reason2}
         after 3000 -> timeout end,
    erlang:display({abnormal_exit, R2}),

    %% Case 3: monitor/2 an ALREADY-DEAD pid -> expect immediate DOWN noproc.
    P3 = spawn(fun() -> ok end),
    receive after 300 -> ok end,           %% let P3 die before monitoring it
    Ref3 = erlang:monitor(process, P3),
    R3 = receive {'DOWN', Ref3, process, P3, Reason3} -> {down, Reason3}
         after 1500 -> timeout end,
    erlang:display({already_dead, R3}),

    erlang:display(done),
    ok.
