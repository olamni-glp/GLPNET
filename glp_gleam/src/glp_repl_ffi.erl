%% glp_repl_ffi — stdin line reader for the Gleam GLP REPL (feature 050, T031; US2).
%%
%% The one FFI the REPL needs: read a single line from standard input, returning
%% {ok, Binary} for a line (trailing newline included; the Gleam side trims) or
%% {error, nil} at EOF / on a read error. Gleam maps these to Result(String, Nil).
%% Scripted newline-fed stdin: repeated read_line/0 until EOF drives the loop.
-module(glp_repl_ffi).
-export([read_line/0]).

read_line() ->
    case io:get_line("") of
        eof ->
            {error, nil};
        {error, _} ->
            {error, nil};
        Data ->
            case unicode:characters_to_binary(Data) of
                Bin when is_binary(Bin) -> {ok, Bin};
                _ -> {error, nil}
            end
    end.
