%% glp_link_quic_ffi — OS-port seam onto the genuine-QUIC side-process (feature 050, T055).
%%
%% The Gleam QUIC-WS transport leaf (`glp/link/transports/quic_ws`) needs a REQUEST/RESPONSE
%% seam (send one frame / receive one frame), whereas `gleam_quic/src/glpq_ffi.erl` `relay/3`
%% wires the side-process straight onto our own stdio. Same port mechanics, different shape:
%% this module owns the port in a dedicated process and exposes send/recv/close.
%%
%% PROFILE A (Decision 8, constitution II): QUIC is terminated by the verified C#
%% `glp_quick_host`, NOT simulated here — `quic_termination = side_process`, genuinely real.
%%
%% Framing on each leg:
%%   * IPC leg (here <-> host stdio): line-delimited UTF-8, so an opaque 025 frame rides as
%%     BASE64 (`--binary` mode, added to the host for T055). Raw binary cannot survive this
%%     leg — CRC bytes, length prefixes and embedded newlines would be mangled.
%%   * WIRE leg (host <-> peer over QUIC/WS): the host base64-DECODES before sending, so the
%%     wire carries the RAW frame — byte-identical to what C# `glp_link`'s own QuicTransport
%%     puts on it. Cross-runtime parity (US5) is preserved by construction.
%%
%% DEMUX (the load-bearing detail): the port merges the child's stderr into stdout
%% (`stderr_to_stdout`), so data and control lines share one stream. Control lines are a CLOSED
%% set of tokens — READY / LINK_UP / LINK_CLOSED / ERR / FAULT / CLIENT_UP / CLIENT_DOWN —
%% each either bare or followed by a SPACE. The base64 alphabet (A-Za-z0-9+/=) contains NO
%% space, so "known token, then space-or-end" is an UNAMBIGUOUS discriminator. Prefix matching
%% alone would not be: "ERRxyz" is legal base64.
-module(glp_link_quic_ffi).
-export([open/3, send/2, recv/2, close/1, get_env/1]).

%% get_env(Name) -> {ok, Value} | {error, nil}   (Gleam Result)
get_env(Name) ->
    case os:getenv(to_list(Name)) of
        false -> {error, nil};
        V -> {ok, unicode:characters_to_binary(V)}
    end.

-define(LINE_BUF, 4194304).

%% open(Exe, Args, TimeoutMs) -> {ok, Pid} | {error, Reason :: binary()}
%% Spawns the side-process and blocks until it reports LINK_UP (the link is established) or
%% fails. Returns the owning process; every later call addresses that process.
open(Exe, Args, TimeoutMs) ->
    Caller = self(),
    Pid = spawn(fun() -> boot(Caller, to_list(Exe), [to_list(A) || A <- Args]) end),
    receive
        {Pid, link_up} -> {ok, Pid};
        {Pid, {failed, Why}} -> {error, Why}
    after TimeoutMs ->
        exit(Pid, kill),
        {error, <<"timed out waiting for LINK_UP from the QUIC side-process">>}
    end.

boot(Caller, Exe, Args) ->
    %% `spawn_executable` does NOT search PATH — a bare name like "dotnet" is enoent. Resolve
    %% it the way a shell would, and say so plainly when it genuinely is not installed.
    case resolve_exe(Exe) of
        false ->
            Caller ! {self(), {failed, iolist_to_binary(
                io_lib:format("executable not found on PATH: ~ts", [Exe]))}};
        Path ->
            try
                Port = open_port(
                    {spawn_executable, Path},
                    [{args, Args}, binary, exit_status, stderr_to_stdout, {line, ?LINE_BUF}]
                ),
                await_link_up(Caller, Port, <<>>)
            catch _:E ->
                Caller ! {self(), {failed, iolist_to_binary(
                    io_lib:format("cannot spawn ~ts: ~p", [Path, E]))}}
            end
    end.

%% Absolute/relative path with a separator -> use as-is; a bare name -> PATH lookup.
resolve_exe(Exe) ->
    case filename:pathtype(Exe) of
        relative ->
            case filename:split(Exe) of
                [_Single] -> os:find_executable(Exe);
                _ -> Exe
            end;
        _ -> Exe
    end.

%% Consume control lines until the link is up (or the host gives up). Data cannot arrive before
%% LINK_UP, so anything data-shaped here would be a host-contract violation — surfaced, not eaten.
await_link_up(Caller, Port, Acc) ->
    receive
        {Port, {data, {noeol, F}}} -> await_link_up(Caller, Port, <<Acc/binary, F/binary>>);
        {Port, {data, {eol, F}}} ->
            Line = <<Acc/binary, F/binary>>,
            case classify(Line) of
                {control, <<"LINK_UP", _/binary>>} ->
                    Caller ! {self(), link_up},
                    loop(Port, <<>>, [], undefined);
                {control, <<"ERR", _/binary>> = Err} ->
                    Caller ! {self(), {failed, Err}},
                    try port_close(Port) catch _:_ -> ok end;
                _ -> await_link_up(Caller, Port, <<>>)
            end;
        {Port, {exit_status, S}} ->
            Caller ! {self(), {failed, iolist_to_binary(
                io_lib:format("QUIC side-process exited (status ~p) before LINK_UP", [S]))}}
    end.

%% The owning loop. `Q` is the FIFO of decoded frames already received; `Waiter` is a parked
%% recv (at most one — the seam is single-consumer, like the loopback leaf).
loop(Port, Acc, Q, Waiter) ->
    receive
        {Port, {data, {noeol, F}}} ->
            loop(Port, <<Acc/binary, F/binary>>, Q, Waiter);
        {Port, {data, {eol, F}}} ->
            Line = <<Acc/binary, F/binary>>,
            case classify(Line) of
                {data, B64} ->
                    case decode64(B64) of
                        {ok, Frame} -> deliver(Port, Q ++ [{frame, Frame}], Waiter);
                        error -> loop(Port, <<>>, Q, Waiter)   %% host never emits bad base64
                    end;
                {control, <<"LINK_CLOSED", _/binary>>} -> deliver(Port, Q ++ [eos], Waiter);
                {control, <<"FAULT", _/binary>> = C} -> deliver(Port, Q ++ [{fault, C}], Waiter);
                {control, <<"ERR", _/binary>> = C} -> deliver(Port, Q ++ [{fault, C}], Waiter);
                {control, _} -> loop(Port, <<>>, Q, Waiter)     %% READY / CLIENT_* — informational
            end;
        {Port, {exit_status, _}} ->
            deliver(Port, Q ++ [eos], Waiter);
        {send, From, Frame} ->
            R = try port_command(Port, [base64:encode(Frame), $\n]), ok catch _:_ -> {error, <<"send on a closed QUIC link">>} end,
            From ! {self(), {send_result, R}},
            loop(Port, Acc, Q, Waiter);
        {recv, From} ->
            deliver(Port, Q, From);
        close ->
            try port_close(Port) catch _:_ -> ok end,
            ok
    end.

%% Hand the head of the queue to a parked waiter, if both exist.
deliver(Port, [], Waiter) -> loop(Port, <<>>, [], Waiter);
deliver(Port, Q, undefined) -> loop(Port, <<>>, Q, undefined);
deliver(Port, [Item | Rest], Waiter) ->
    Waiter ! {self(), {recv_result, Item}},
    loop(Port, <<>>, Rest, undefined).

send(Pid, Frame) ->
    Pid ! {send, self(), Frame},
    receive {Pid, {send_result, R}} -> R
    after 30000 -> {error, <<"timed out sending to the QUIC side-process">>} end.

%% recv(Pid, TimeoutMs) -> {ok, Frame} | eos | {error, Reason}
recv(Pid, TimeoutMs) ->
    Pid ! {recv, self()},
    receive
        {Pid, {recv_result, {frame, F}}} -> {ok, F};
        {Pid, {recv_result, eos}} -> eos;
        {Pid, {recv_result, {fault, C}}} -> {error, C}
    after TimeoutMs -> {error, <<"timed out awaiting a frame from the QUIC side-process">>} end.

close(Pid) ->
    Pid ! close,
    ok.

%% --- demux -------------------------------------------------------------------
%% Control iff the line is a known token, bare or followed by a space (base64 has no space).
classify(Line) ->
    case is_control(Line, [<<"READY">>, <<"LINK_UP">>, <<"LINK_CLOSED">>, <<"ERR">>,
                           <<"FAULT">>, <<"CLIENT_UP">>, <<"CLIENT_DOWN">>]) of
        true -> {control, Line};
        false -> {data, Line}
    end.

is_control(_Line, []) -> false;
is_control(Line, [T | Rest]) ->
    N = byte_size(T),
    case Line of
        T -> true;                                   %% bare token
        <<T:N/binary, " ", _/binary>> -> true;        %% token + space
        _ -> is_control(Line, Rest)
    end.

decode64(B64) ->
    try {ok, base64:decode(B64)} catch _:_ -> error end.

to_list(B) when is_binary(B) -> unicode:characters_to_list(B);
to_list(L) when is_list(L) -> L.
