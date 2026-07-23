%% glp_link_zmq_ffi — erlzmq (NIF over libzmq) wrapper for the Gleam ZMQ transport
%% leaf (feature 059, owner ruling 2026-07-23; docs/research/fullscope-gleam/
%% phase2-verify/rulings.md — ZMQ mandatory, contract extended to include zmq).
%%
%% Mirrors the shape of glp_link_tcp_ffi: each function returns a Gleam-friendly
%% {ok, _} | {error, Reason} so the Gleam side maps them onto Result. One ZMQ
%% `pair` socket per link end (exclusive 1-to-1 duplex — the bilateral seam,
%% FR-005). The socket handle handed back to Gleam is the {Context, Socket} pair
%% so close/1 can tear down both.
%%
%% erlzmq is a NIF over native libzmq and is provisioned only in WSL (see
%% glp_gleam/profile_zmq/README.md), exactly like the Profile-C QUIC quicer NIF.
%% This module still COMPILES with no erlzmq present (Erlang resolves the
%% `erlzmq:*` module-qualified calls at runtime, not compile time), so the
%% Windows-native `gleam build`/test baseline is unaffected; the functions only
%% succeed where erlzmq is loaded.
-module(glp_link_zmq_ffi).
-export([zmq_bind/1, zmq_connect/1, zmq_send/2, zmq_recv/2, zmq_close/1]).

%% zmq_bind(Endpoint :: binary()) -> {ok, {Ctx, Sock}} | {error, Reason}
zmq_bind(Endpoint) ->
    with_socket(fun(Sock) -> erlzmq:bind(Sock, ep(Endpoint)) end).

%% zmq_connect(Endpoint :: binary()) -> {ok, {Ctx, Sock}} | {error, Reason}
zmq_connect(Endpoint) ->
    with_socket(fun(Sock) -> erlzmq:connect(Sock, ep(Endpoint)) end).

%% zmq_send({Ctx, Sock}, Data :: binary()) -> {ok, nil} | {error, Reason}
zmq_send({_Ctx, Sock}, Data) ->
    case erlzmq:send(Sock, Data) of
        ok -> {ok, nil};
        {error, _} = E -> E;
        Other -> {error, Other}
    end.

%% zmq_recv({Ctx, Sock}, TimeoutMs :: integer()) -> {ok, binary()} | {error, Reason}
%% Timeout < 0 blocks; otherwise a bounded recv via RCVTIMEO. erlzmq:recv/1 blocks;
%% erlzmq:recv/2 takes flags — we set RCVTIMEO on the socket for a bounded wait.
zmq_recv({_Ctx, Sock}, Timeout) when Timeout < 0 ->
    normalise_recv(erlzmq:recv(Sock));
zmq_recv({_Ctx, Sock}, Timeout) ->
    _ = erlzmq:setsockopt(Sock, rcvtimeo, Timeout),
    normalise_recv(erlzmq:recv(Sock)).

%% zmq_close({Ctx, Sock}) -> nil
zmq_close({Ctx, Sock}) ->
    _ = erlzmq:close(Sock),
    _ = erlzmq:term(Ctx),
    nil.

%% ---- helpers -------------------------------------------------------------

%% Create a context + a PAIR socket, run Bind/Connect, hand back {Ctx, Sock}.
with_socket(BindOrConnect) ->
    case erlzmq:context() of
        {ok, Ctx} ->
            case erlzmq:socket(Ctx, pair) of
                {ok, Sock} ->
                    case BindOrConnect(Sock) of
                        ok -> {ok, {Ctx, Sock}};
                        {error, _} = E ->
                            _ = erlzmq:close(Sock),
                            _ = erlzmq:term(Ctx),
                            E;
                        Other ->
                            _ = erlzmq:close(Sock),
                            _ = erlzmq:term(Ctx),
                            {error, Other}
                    end;
                {error, _} = E ->
                    _ = erlzmq:term(Ctx),
                    E
            end;
        {error, _} = E -> E
    end.

normalise_recv({ok, Bin}) when is_binary(Bin) -> {ok, Bin};
normalise_recv({ok, Bin}) -> {ok, iolist_to_binary(Bin)};
normalise_recv({error, _} = E) -> E;
normalise_recv(Other) -> {error, Other}.

%% erlzmq accepts a list or binary endpoint; normalise to a list ("tcp://H:P").
ep(Endpoint) when is_binary(Endpoint) -> binary_to_list(Endpoint);
ep(Endpoint) -> Endpoint.
