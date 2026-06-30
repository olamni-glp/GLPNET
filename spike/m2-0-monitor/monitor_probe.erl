%% M2-0 spike: verify erlang:monitor + 'DOWN' behavior.
%% start/0 is the AtomVM entry point; also runnable on stock OTP BEAM.
%% Probes: (1) monitor a normally-exiting process, (2) abnormally-exiting,
%% (3) an already-dead pid. Prints the observed message (or `timeout`) for each.
-module(monitor_probe).
-export([start/0]).

start() ->
    %% Case 1: monitored process exits NORMALLY -> expect {'DOWN',Ref,process,Pid,normal}
    {Pid1, Ref1} = spawn_monitor(fun() -> ok end),
    R1 = receive {'DOWN', Ref1, process, Pid1, Reason1} -> {down, Reason1}
         after 3000 -> timeout end,
    erlang:display({normal_exit, R1}),

    %% Case 2: monitored process exits ABNORMALLY -> expect {'DOWN',...,boom}
    {Pid2, Ref2} = spawn_monitor(fun() -> exit(boom) end),
    R2 = receive {'DOWN', Ref2, process, Pid2, Reason2} -> {down, Reason2}
         after 3000 -> timeout end,
    erlang:display({abnormal_exit, R2}),

    %% Case 3: monitor an ALREADY-DEAD pid -> expect immediate DOWN with noproc
    {Pid3, Ref0} = spawn_monitor(fun() -> ok end),
    receive {'DOWN', Ref0, process, Pid3, _} -> ok after 3000 -> ok end,
    Ref3 = erlang:monitor(process, Pid3),
    R3 = receive {'DOWN', Ref3, process, Pid3, Reason3} -> {down, Reason3}
         after 1500 -> timeout end,
    erlang:display({already_dead, R3}),

    erlang:display(done),
    ok.
