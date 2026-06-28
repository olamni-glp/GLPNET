%% glpq_ffi — the OS-port relay for the Gleam Profile-A stack (feature 036, US3).
%%
%% Spawns the native genuine-QUIC side-process (the C# `glp_quick_host`) as an Erlang port and
%% relays line-delimited L5 envelopes between our stdio (facing the Python control plane) and the
%% side-process. The side-process's stderr is merged in (stderr_to_stdout) and demuxed by the first
%% byte: a line starting with `{` is a data envelope (-> our stdout); anything else is a control
%% line — READY / LINK_UP / ERR / FAULT / CLIENT_* — (-> our stderr). This keeps the observable
%% stdio behaviour byte-identical to the C# stack so the two are interchangeable (SC-006).
-module(glpq_ffi).
-export([relay/3]).

%% relay(Dotnet, Dll, HostArgs) -> ExitStatus :: integer()
relay(Dotnet, Dll, Args) ->
    PortArgs = [to_list(Dll) | [to_list(A) || A <- Args]],
    Port = open_port(
        {spawn_executable, to_list(Dotnet)},
        [{args, PortArgs}, binary, exit_status, stderr_to_stdout, {line, 1048576}]
    ),
    Parent = self(),
    _Reader = spawn(fun() -> stdin_loop(Parent) end),
    loop(Port).

loop(Port) ->
    receive
        {Port, {data, {eol, Line}}} ->
            classify(Line),
            loop(Port);
        {Port, {data, {noeol, Frag}}} ->
            %% a line fragment longer than the {line,N} bound — emit as control text
            io:put_chars(standard_error, [Frag]),
            loop(Port);
        {Port, {exit_status, Status}} ->
            Status;
        {stdin, Data} ->
            %% forward one line (already \n-terminated by io:get_line) to the side-process stdin
            try port_command(Port, Data) catch _:_ -> ok end,
            loop(Port);
        stdin_eof ->
            try port_close(Port) catch _:_ -> ok end,
            0
    end.

%% Demux: `{`-prefixed lines are data envelopes; everything else is control.
classify(<<"{", _/binary>> = Line) ->
    io:put_chars(standard_io, [Line, "\n"]);
classify(Line) ->
    io:put_chars(standard_error, [Line, "\n"]).

%% Read our own stdin line-by-line in a helper process and forward to the relay loop.
stdin_loop(Parent) ->
    case io:get_line(standard_io, "") of
        eof -> Parent ! stdin_eof;
        {error, _} -> Parent ! stdin_eof;
        Line -> Parent ! {stdin, Line}, stdin_loop(Parent)
    end.

to_list(B) when is_binary(B) -> unicode:characters_to_list(B);
to_list(L) when is_list(L) -> L.
