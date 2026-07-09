%% glpq_quic — Profile C: genuine IN-PROCESS QUIC on the full BEAM via the `quicer` NIF
%% (feature 049 US2, completing 036 T032; provisioned by gleam_quic/profile_c/rebar.config).
%%
%% The CLIENT data plane runs entirely inside this BEAM node — no C# side-process. It mirrors the
%% C# QuicTransport contract exactly (specs/036-http3-quic-ws-link/contracts/wire-contract.md):
%%   QUIC (ALPN "h3", mutual shared-cert TLS, SPKI-SHA256 pin verified app-side mid-handshake)
%%   -> one client-opened bidi stream -> "GLPQUICK/1 CONNECT glp-link" bootstrap line
%%   -> RFC 6455 WebSocket frames (binary, FIN=1, unmasked sends — T017; masked accepted on recv)
%%   -> one WS message = one L5 JSON envelope (no 025 sublayer on this path, as in glp_quick_host).
%%
%% The stdio seam is identical to the C# host's client role (Program.cs RunClientAsync): data
%% envelopes on stdout, control tokens (READY/LINK_UP/WAIT/ERR/FAULT/LINK_CLOSED) on stderr, stdin
%% lines become sends, and stdin EOF does NOT tear the link down — the link's lifetime rules.
%% Failure tokens + exit codes match the host: usage=2 cert_mismatch=3 server_not_ready=4
%% udp_blocked=5 quic_unsupported=6 cert_load/bind=7 (FR-019 — clear, never a silent hang).
-module(glpq_quic).
-export([client/1]).
-include_lib("public_key/include/public_key.hrl").

-define(BOOTSTRAP_REQ, <<"GLPQUICK/1 CONNECT glp-link\r\n">>).
-define(BOOTSTRAP_ACK, <<"GLPQUICK/1 101 SWITCHING">>).
-define(MAX_FRAME, 16777216).  %% 16 MiB — the same bound as WebSocketOverQuic.MaxFramePayload
-define(IDLE_MS, 7200000).     %% 2 h — same as both C# endpoints (idle chat links stay up)
-define(TLS_ALERT_BAD_CERT, 42).

-record(s, {conn, stream, buf = <<>>, acc = none, acc_size = 0}).

%% Entry from the Gleam main: Args are the host CLI args as binaries. Returns the exit status.
client(Args) ->
    ok = io:setopts(standard_io, [binary, {encoding, latin1}]),
    try
        run(parse(Args, #{retry => false}))
    catch
        throw:{err, Token, Detail, Code} ->
            ctl("ERR ~s ~s", [Token, Detail]),
            Code
    end.

%% ------------------------------------------------------------------ args
parse([<<"--role">>, <<"client">> | R], M) -> parse(R, M);
parse([<<"--role">>, Other | _], _) ->
    throw({err, usage, io_lib:format("profile-c supports --role client only (got ~s); the reference server is the C# host", [Other]), 2});
parse([<<"--addr">>, A | R], M) -> parse(R, M#{addr => binary_to_list(A)});
parse([<<"--port">>, P | R], M) -> parse(R, M#{port => binary_to_integer(P)});
parse([<<"--cert">>, C | R], M) -> parse(R, M#{cert => binary_to_list(C)});
parse([<<"--retry">> | R], M) -> parse(R, M#{retry => true});
parse([Unknown | _], _) ->
    throw({err, usage, io_lib:format("unknown arg '~s'", [Unknown]), 2});
parse([], #{addr := _, port := _, cert := _} = M) -> M;
parse([], _) ->
    throw({err, usage, "--addr, --port and --cert are required", 2}).

%% ------------------------------------------------------------------ main
run(#{addr := Addr, port := Port, cert := CertDir} = Opts) ->
    Retry = maps:get(retry, Opts, false),
    CertFile = filename:join(CertDir, "glpquick.pem"),
    KeyFile = filename:join(CertDir, "glpquick.key"),
    PinFile = filename:join(CertDir, "glpquick.fingerprint"),
    [filelib:is_file(F) orelse throw({err, cert_load, io_lib:format("missing ~s", [F]), 7})
     || F <- [CertFile, KeyFile, PinFile]],
    {ok, PinRaw} = file:read_file(PinFile),
    Pin = string:trim(PinRaw),
    case application:ensure_all_started(quicer) of
        {ok, _} -> ok;
        {error, Reason} ->
            throw({err, quic_unsupported, io_lib:format("quicer NIF failed to start: ~p", [Reason]), 6})
    end,
    ctl("READY client ep(~s, ~b)", [Addr, Port]),
    DeadlineMs = erlang:monotonic_time(millisecond) + (case Retry of true -> 30000; false -> 8000 end),
    Conn = connect_loop(Addr, Port, CertFile, KeyFile, Pin, Retry, DeadlineMs),
    {ok, Stream} = quicer:start_stream(Conn, #{active => true}),
    Rest = bootstrap(Stream),
    ctl("LINK_UP quic-inproc://~s:~b", [Addr, Port]),
    Self = self(),
    _Stdin = spawn_link(fun() -> stdin_loop(Self) end),
    loop(#s{conn = Conn, stream = Stream, buf = Rest}).

%% ------------------------------------------------------------------ connect + pin verify
%% The C# semantics (QuicTransport.PinValidationCallback): built-in chain/hostname validation is
%% waived (verify => none), and trust is EXACTLY the SPKI SHA-256 pin — enforced mid-handshake via
%% quicer's custom_verify (INDICATE_CERTIFICATE_RECEIVED) + complete_cert_validation. A pin
%% mismatch rejects the handshake (TLS alert bad_certificate) — never a blanket accept.
connect_loop(Addr, Port, CertFile, KeyFile, Pin, Retry, DeadlineMs) ->
    ConnOpts = #{alpn => ["h3"],
                 verify => none,
                 custom_verify => true,
                 certfile => CertFile,   %% mutual: present the shared cert (server pins us too)
                 keyfile => KeyFile,
                 idle_timeout_ms => ?IDLE_MS},
    case quicer:connect(Addr, Port, ConnOpts, 8000) of
        {ok, Conn, {CertDer, _Chain}} when is_binary(CertDer) ->
            verify_pin(Conn, CertDer, Pin);
        {ok, Conn, CertDer} when is_binary(CertDer) ->
            verify_pin(Conn, CertDer, Pin);
        {ok, Conn} ->
            %% Handshake completed without indicating the peer cert — cannot pin => reject
            %% (mirrors the C# callback's `certificate is null -> false`; never trust blindly).
            _ = quicer:shutdown_connection(Conn),
            throw({err, cert_mismatch, "peer certificate not indicated by the transport", 3});
        {error, transport_down, Reason} ->
            retry_or_fail(map_transport_down(Reason), Addr, Port, CertFile, KeyFile, Pin, Retry, DeadlineMs);
        {error, timeout} ->
            retry_or_fail({server_not_ready, "handshake timeout", 4}, Addr, Port, CertFile, KeyFile, Pin, Retry, DeadlineMs);
        {error, Other} ->
            throw({err, udp_blocked, io_lib:format("connect failed: ~p", [Other]), 5})
    end.

verify_pin(Conn, CertDer, Pin) ->
    case spki_pin(CertDer) =:= Pin of
        true ->
            ok = quicer:complete_cert_validation(Conn, true, 0),
            receive
                {quic, connected, Conn, _} -> Conn;
                {quic, transport_shutdown, Conn, Reason} ->
                    {Token, Detail, Code} = map_transport_down(Reason),
                    throw({err, Token, Detail, Code})
            after 8000 ->
                throw({err, server_not_ready, "no connected event after cert validation", 4})
            end;
        false ->
            _ = quicer:complete_cert_validation(Conn, false, ?TLS_ALERT_BAD_CERT),
            throw({err, cert_mismatch, "presented SPKI pin does not match the shared pin", 3})
    end.

retry_or_fail({Token, Detail, Code} = Err, Addr, Port, CertFile, KeyFile, Pin, Retry, DeadlineMs) ->
    Retriable = Token =:= server_not_ready,
    case Retry andalso Retriable andalso erlang:monotonic_time(millisecond) < DeadlineMs of
        true ->
            ctl("WAIT server_not_ready ~s — retrying", [Detail]),
            timer:sleep(500),
            connect_loop(Addr, Port, CertFile, KeyFile, Pin, Retry, DeadlineMs);
        false ->
            _ = Err,
            throw({err, Token, Detail, Code})
    end.

%% Map msquic transport-shutdown status atoms onto the FR-019 tokens the C# host uses.
map_transport_down(Reason) ->
    Status = shutdown_status(Reason),
    case Status of
        connection_refused -> {server_not_ready, "connection_refused", 4};
        connection_timeout -> {server_not_ready, "connection_timeout", 4};
        connection_idle -> {server_not_ready, "connection_idle", 4};
        bad_certificate -> {cert_mismatch, "peer rejected our certificate (bad_certificate)", 3};
        cert_expired -> {cert_mismatch, "cert_expired", 3};
        cert_untrusted_root -> {cert_mismatch, "cert_untrusted_root", 3};
        cert_no_cert -> {cert_mismatch, "cert_no_cert", 3};
        handshake_failure -> {cert_mismatch, "tls handshake_failure", 3};
        tls_error -> {cert_mismatch, "tls_error", 3};
        alpn_neg_failure -> {quic_unsupported, "alpn_neg_failure", 6};
        unreachable -> {udp_blocked, "unreachable", 5};
        _ -> {udp_blocked, io_lib:format("transport_down: ~p", [Reason]), 5}
    end.

shutdown_status(#{status := S}) -> S;
shutdown_status(#{error := E}) when is_atom(E) -> E;
shutdown_status(_) -> unknown.

%% The SPKI SHA-256 pin: base64(SHA-256(DER SubjectPublicKeyInfo)) — identical to the Python
%% tool (cert.py) and the C# QuicTransport.SpkiPin. DER is canonical, so re-encoding the decoded
%% SPKI reproduces the original bytes.
spki_pin(CertDer) ->
    #'Certificate'{tbsCertificate = #'TBSCertificate'{subjectPublicKeyInfo = Spki}} =
        public_key:pkix_decode_cert(CertDer, plain),
    SpkiDer = public_key:der_encode('SubjectPublicKeyInfo', Spki),
    base64:encode(crypto:hash(sha256, SpkiDer)).

%% ------------------------------------------------------------------ bootstrap (ConnectBootstrap)
%% Connector side: one ASCII request line, expect the one-line accept (analogous to the RFC 6455
%% Upgrade, reduced — ConnectBootstrap.cs). Returns any bytes that followed the reply line.
bootstrap(Stream) ->
    case quicer:send(Stream, ?BOOTSTRAP_REQ) of
        {ok, _} -> ok;
        SendErr -> throw({err, link_failed, io_lib:format("bootstrap send failed: ~p", [SendErr]), 1})
    end,
    {Line, Rest} = read_line(Stream, <<>>),
    case Line of
        ?BOOTSTRAP_ACK -> Rest;
        Other -> throw({err, link_failed, io_lib:format("WS bootstrap rejected: '~s'", [Other]), 1})
    end.

read_line(Stream, Buf) ->
    case binary:split(Buf, <<"\n">>) of
        [Line, Rest] -> {string:trim(Line, trailing, "\r"), Rest};
        [_] when byte_size(Buf) > 256 ->
            throw({err, link_failed, "bootstrap line exceeded the bound", 1});
        [_] ->
            receive
                {quic, Data, Stream, _} when is_binary(Data) ->
                    read_line(Stream, <<Buf/binary, Data/binary>>);
                {quic, transport_shutdown, _, Reason} ->
                    {Token, Detail, Code} = map_transport_down(Reason),
                    throw({err, Token, Detail, Code});
                {quic, Closed, _, _} when Closed =:= closed; Closed =:= stream_closed;
                                          Closed =:= peer_send_shutdown ->
                    throw({err, link_failed, "stream closed during bootstrap", 1})
            after 8000 ->
                throw({err, server_not_ready, "no bootstrap reply", 4})
            end
    end.

%% ------------------------------------------------------------------ the link loop
loop(#s{conn = Conn, stream = Stream} = S) ->
    receive
        {quic, Data, Stream, _} when is_binary(Data) ->
            case drain(S#s{buf = <<(S#s.buf)/binary, Data/binary>>}) of
                {continue, S1} -> loop(S1);
                {halt, Code} -> Code
            end;
        {stdin, Line0} ->
            Line = string:trim(Line0, trailing, "\r\n"),
            case Line of
                <<>> -> loop(S);
                _ ->
                    case ws_send(Stream, Line) of
                        ok -> loop(S);
                        {error, Reason} ->
                            ctl("FAULT Transient quic/ws send failed: ~p", [Reason]),
                            ctl("LINK_CLOSED", []),
                            0
                    end
            end;
        stdin_eof ->
            loop(S);  %% the link outlives stdin (one-shot/piped shells) — Program.cs parity
        {quic, peer_send_shutdown, Stream, _} -> closed(0);
        {quic, stream_closed, Stream, _} -> closed(0);
        {quic, send_shutdown_complete, Stream, _} -> loop(S);
        {quic, closed, Conn, _} -> closed(0);
        {quic, transport_shutdown, Conn, Reason} ->
            ctl("FAULT Transient transport_shutdown: ~p", [Reason]),
            closed(0);
        _Other ->
            loop(S)  %% dgram/nst/flow-control events — not part of this link's contract
    end.

closed(Code) ->
    ctl("LINK_CLOSED", []),
    Code.

%% Parse as many complete RFC 6455 frames as the buffer holds (WebSocketOverQuic semantics:
%% FIN/continuation reassembly, ping->pong, close -> graceful end, size caps).
drain(#s{buf = Buf} = S) ->
    case ws_frame(Buf) of
        more ->
            {continue, S};
        {frame, Fin, Op, Payload, Rest} ->
            case handle_frame(Fin, Op, Payload, S#s{buf = Rest}) of
                {continue, S1} -> drain(S1);
                Halt -> Halt
            end;
        {bad, Why} ->
            ctl("FAULT Transient quic/ws recv failed: ~s", [Why]),
            {halt, closed(0)}
    end.

handle_frame(_Fin, 8, _Payload, _S) ->             %% close — peer-initiated WS teardown
    {halt, closed(0)};
handle_frame(_Fin, 9, Payload, #s{stream = Stream} = S) ->  %% ping -> pong (transparent)
    _ = ws_send_op(Stream, 10, Payload),
    {continue, S};
handle_frame(_Fin, 10, _Payload, S) ->             %% unsolicited pong — ignore
    {continue, S};
handle_frame(Fin, Op, Payload, #s{acc = Acc, acc_size = Sz} = S) when Op =:= 0; Op =:= 1; Op =:= 2 ->
    Sz1 = Sz + byte_size(Payload),
    if
        Sz1 > ?MAX_FRAME ->
            %% Total reassembled message exceeds the envelope bound: an authenticated peer streaming
            %% endless non-FIN fragments (each under the per-frame cap) must not grow BEAM memory
            %% without bound. Fault + tear down, matching the per-frame MAX_FRAME ceiling.
            ctl("FAULT Transient quic/ws recv failed: reassembled message ~b exceeds max ~b", [Sz1, ?MAX_FRAME]),
            {halt, closed(0)};
        true ->
            Acc1 = case Acc of none -> [Payload]; L -> [Payload | L] end,
            case Fin of
                true ->
                    deliver(iolist_to_binary(lists:reverse(Acc1))),
                    {continue, S#s{acc = none, acc_size = 0}};
                false ->
                    {continue, S#s{acc = Acc1, acc_size = Sz1}}
            end
    end;
handle_frame(_Fin, Op, _Payload, _S) ->
    ctl("FAULT Transient quic/ws recv failed: unsupported opcode 0x~.16b", [Op]),
    {halt, closed(0)}.

deliver(MsgBytes) ->
    io:put_chars(standard_io, [MsgBytes, "\n"]).

%% One complete frame from the front of Buf, or `more`, or a protocol fault.
ws_frame(<<B0, B1, Rest0/binary>> = _Buf) ->
    Fin = (B0 band 16#80) =/= 0,
    Op = B0 band 16#0F,
    Masked = (B1 band 16#80) =/= 0,
    Len7 = B1 band 16#7F,
    case ws_len(Len7, Rest0) of
        more -> more;
        {Len, _} when (Op band 8) =/= 0, Len > 125 ->
            {bad, io_lib:format("control frame length ~b exceeds 125", [Len])};
        {Len, _} when Len > ?MAX_FRAME ->
            {bad, io_lib:format("frame length ~b exceeds max ~b", [Len, ?MAX_FRAME])};
        {Len, Rest1} ->
            MaskLen = case Masked of true -> 4; false -> 0 end,
            case Rest1 of
                <<MaskKey:MaskLen/binary, Payload:Len/binary, Rest/binary>> ->
                    {frame, Fin, Op, unmask(Masked, MaskKey, Payload), Rest};
                _ ->
                    more
            end
    end;
ws_frame(_) ->
    more.

ws_len(Len7, Rest) when Len7 < 126 -> {Len7, Rest};
ws_len(126, <<Len:16/big, Rest/binary>>) -> {Len, Rest};
ws_len(127, <<Len:64/big, Rest/binary>>) -> {Len, Rest};
ws_len(_, _) -> more.

unmask(false, _Key, Payload) -> Payload;
unmask(true, <<K:4/binary>>, Payload) ->
    unmask_bytes(Payload, K, 0, <<>>).

unmask_bytes(<<>>, _K, _I, Acc) -> Acc;
unmask_bytes(<<B, Rest/binary>>, K, I, Acc) ->
    Kb = binary:at(K, I rem 4),
    unmask_bytes(Rest, K, I + 1, <<Acc/binary, (B bxor Kb)>>).

%% Send one opaque message as one RFC 6455 binary frame (FIN=1, unmasked — T017 parity).
ws_send(Stream, Payload) -> ws_send_op(Stream, 2, Payload).

ws_send_op(Stream, Op, Payload) ->
    Header = ws_header(Op, byte_size(Payload)),
    case quicer:send(Stream, [Header, Payload]) of
        {ok, _} -> ok;
        Err -> {error, Err}
    end.

ws_header(Op, Len) when Len =< 125 -> <<(16#80 bor Op), Len>>;
ws_header(Op, Len) when Len =< 65535 -> <<(16#80 bor Op), 126, Len:16/big>>;
ws_header(Op, Len) -> <<(16#80 bor Op), 127, Len:64/big>>.

%% ------------------------------------------------------------------ stdio
stdin_loop(Parent) ->
    case io:get_line(standard_io, "") of
        eof -> Parent ! stdin_eof;
        {error, _} -> Parent ! stdin_eof;
        Line -> Parent ! {stdin, Line}, stdin_loop(Parent)
    end.

ctl(Fmt, Args) ->
    io:format(standard_error, Fmt ++ "\n", Args).
