%% glp_link_tcp_ffi — gen_tcp wrapper for the Gleam TCP link transport
%% (feature 050, T049; US4).
%%
%% Passive-mode raw TCP over IPv4 loopback — the FFI the seam's synchronous
%% listen/connect/send/recv/close map onto (mirror of Dart `tcp_transport.dart` /
%% C# `TcpTransport.cs`). Passive sockets ({active,false}) make gen_tcp:recv a
%% blocking exact-length read, which fits the synchronous seam with no reader
%% process. Frames are length-prefixed ABOVE this (in tcp.gleam), so recv here is a
%% raw exact-N read.
%%
%% Return shapes are normalized to Gleam's Result convention: {ok, V} -> Ok(V),
%% {error, Reason} -> Error(Reason); the Nil-returning ops return the atom `nil`.
-module(glp_link_tcp_ffi).
-export([tcp_listen/1, tcp_accept/2, tcp_close_listener/1, tcp_connect/3,
         tcp_send/2, tcp_recv/2, tcp_shutdown_write/1, tcp_close/1]).

%% Bind a passive IPv4-loopback listener on Port (0 = OS-assigned).
tcp_listen(Port) ->
    gen_tcp:listen(Port, [binary, {active, false}, {packet, 0},
                          {reuseaddr, true}, {ip, {127, 0, 0, 1}}]).

%% Accept one client (blocking up to Timeout ms), disable Nagle so each frame ships
%% promptly (parity with Dart tcpNoDelay / C# NoDelay).
tcp_accept(LSock, Timeout) ->
    case gen_tcp:accept(LSock, Timeout) of
        {ok, Sock} ->
            inet:setopts(Sock, [{nodelay, true}]),
            {ok, Sock};
        Error ->
            Error
    end.

tcp_close_listener(LSock) ->
    gen_tcp:close(LSock),
    nil.

%% Dial 127.0.0.1:Port (localhost/127.0.0.1 forced to the IPv4 tuple so a connector
%% never lands on IPv6 ::1 while the listener is IPv4-only).
tcp_connect(Host, Port, Timeout) ->
    Addr = case Host of
               <<"localhost">> -> {127, 0, 0, 1};
               <<"127.0.0.1">> -> {127, 0, 0, 1};
               _ -> binary_to_list(Host)
           end,
    case gen_tcp:connect(Addr, Port, [binary, {active, false}, {packet, 0}], Timeout) of
        {ok, Sock} ->
            inet:setopts(Sock, [{nodelay, true}]),
            {ok, Sock};
        Error ->
            Error
    end.

tcp_send(Sock, Data) ->
    case gen_tcp:send(Sock, Data) of
        ok -> {ok, nil};
        Error -> Error
    end.

%% Blocking exact-Length read (infinity: recv parks until Length bytes or the peer
%% FINs → {error, closed}).
tcp_recv(Sock, Length) ->
    gen_tcp:recv(Sock, Length, infinity).

%% Graceful half-close: send FIN, keep the read side open so our own recv finishes on
%% the peer's FIN (parity with Dart Socket.close / C# shutdown).
tcp_shutdown_write(Sock) ->
    gen_tcp:shutdown(Sock, write),
    nil.

tcp_close(Sock) ->
    gen_tcp:close(Sock),
    nil.
