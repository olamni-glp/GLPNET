import '../../runtime/heap_fcp.dart';
import '../../runtime/terms.dart';
import '../seam/link_address.dart';
import '../seam/link_fault.dart';
import '../seam/link_id.dart';
import '../seam/link_role.dart';
import '../seam/link_scheme.dart';

/// Maps between GLP ground terms and the host link value-types. The GLP surface is
/// `LinkId ::= link_id(Scheme, Endpoint, Nonce)`,
/// `Endpoint ::= String ; ep(String, Integer)`,
/// `Nonce ::= Integer ; String`, `Role ∈ {listener, connector}`, and the
/// fault lattice `ok | closed(LinkId,Reason) | tempFail(LinkId,Reason) |
/// permFail(LinkId,Reason)` (contracts/link-primitives.md §1, rulings-log
/// "fault term closed(LinkId,Reason); graceful reason eos").
///
/// Parsing expects already-dereferenced GROUND terms — the GLP wrappers guard
/// `ground/1` before the kernel runs, so a malformed/unbound term reaching
/// here is a caller bug (it throws) rather than something to tolerate (CLAUDE.md
/// "robustness is a workaround in disguise").
class LinkTerms {
  LinkTerms._();

  static const String _linkIdFunctor = 'link_id';
  static const String _endpointFunctor = 'ep';
  static const String _requestFunctor = 'request';
  static const String gracefulReason = 'eos';
  static const String abruptReason = 'abrupt';

  // ---- parse: term → host value ----

  /// Recursively dereference [term] against the heap into a VarRef-free
  /// ground tree, so the `Parse*` helpers below see the bound constants at every depth.
  /// REQUIRED before parsing: the compiler represents a ground `link_id(Scheme, …)` with
  /// each struct ARG as a [VarRef] into a bound cell, so a shallow
  /// `heap.dereference` resolves only the outer struct and leaves `Scheme`/`Endpoint`
  /// as VarRefs (the xUnit kernels passed ground [ConstTerm] args directly, hiding this).
  /// The GLP wrappers guard `ground/1`, so every nested cell is bound; an unbound cell at any
  /// depth is a caller bug → [ArgumentError] (the kernel aborts).
  static Term groundResolve(HeapFCP heap, Term term) {
    final Term d = heap.dereference(term);
    if (d is ConstTerm) {
      return d;
    } else if (d is StructTerm) {
      final args = List<Term>.filled(d.args.length, ConstTerm('nil'));
      for (int i = 0; i < d.args.length; i++) {
        args[i] = groundResolve(heap, d.args[i]);
      }
      return StructTerm(d.functor, args);
    } else if (d is VarRef) {
      throw ArgumentError(
          'unbound cell ${d.addr} in a term expected ground (the ground/1 guard should have excluded it)');
    } else {
      throw ArgumentError('cannot ground-resolve ${d.runtimeType}');
    }
  }

  /// Parse a ground `link_id(Scheme, Endpoint, Nonce)` term.
  static LinkId parseLinkId(Term term) {
    if (term is! StructTerm ||
        term.functor != _linkIdFunctor ||
        term.args.length != 3) {
      throw ArgumentError('expected link_id/3 struct, got ${_describe(term)}');
    }
    final scheme = LinkScheme.of(_constString(term.args[0], 'LinkId.Scheme'));
    final endpoint = parseEndpoint(term.args[1]);
    final nonce = _parseNonce(term.args[2]);
    return LinkId(scheme, endpoint, nonce);
  }

  /// Parse a ground `Scheme` string (the first component of `rendezvous(Scheme, Endpoint)`).
  static LinkScheme parseScheme(Term term) =>
      LinkScheme.of(_constString(term, 'Scheme'));

  /// Parse a ground `Endpoint ::= String ; ep(String, Integer)` term (used by both LinkId and rendezvous).
  static LinkAddress parseEndpoint(Term term) {
    if (term is StructTerm &&
        term.functor == _endpointFunctor &&
        term.args.length == 2) {
      return LinkAddress.endpoint(_constString(term.args[0], 'ep host'),
          _constLong(term.args[1], 'ep port'));
    }
    return LinkAddress.path(_constString(term, 'Endpoint'));
  }

  static LinkNonce _parseNonce(Term term) {
    final v = _const(term, 'Nonce');
    if (v is int) {
      return LinkNonce.int_(v);
    } else if (v is String) {
      return LinkNonce.str(v);
    } else {
      throw ArgumentError(
          'Nonce must be Integer or String, got ${v?.runtimeType ?? "null"}');
    }
  }

  /// Parse the ground close `Reason` (`Reason ::= String`): the atom `abrupt`
  /// from `link_close/1`, or a user reason from `link_close/2`. The wrapper
  /// grounds it, so a non-constant reaching here is a caller bug (it throws).
  static String parseReason(Term term) => _constString(term, 'Reason');

  /// Parse the ground establishment role atom (`listener`/`connector`).
  static LinkRole parseRole(Term term) {
    final s = _constString(term, 'Role');
    switch (s) {
      case 'listener':
        return LinkRole.listener;
      case 'connector':
        return LinkRole.connector;
      default:
        throw ArgumentError("Role must be listener|connector, got '$s'");
    }
  }

  // ---- build: host value → term ----

  /// Build the GLP `link_id(Scheme, Endpoint, Nonce)` term. String components
  /// (scheme, host, string nonce) are RE-QUOTED — the inverse of [_unquote] —
  /// so the reconstructed term is byte-identical to the GLP source literal
  /// `link_id("tcp", ep("127.0.0.1", 9100), 1)` the compiler produces (string
  /// constants carry their quotes). Without this, a path-B request token round-trip
  /// yields a bare `link_id(tcp, …)` that fails the `=?=` match in
  /// `accept_link` against the served (quoted) LinkId, and any program matching a
  /// `closed(link_id(…), …)` fault against a literal would likewise mismatch.
  static Term toTerm(LinkId id) => StructTerm(_linkIdFunctor, <Term>[
        ConstTerm(_requote(id.scheme.name)),
        _endpointToTerm(id.endpoint),
        _nonceToTerm(id.nonce),
      ]);

  static Term _endpointToTerm(LinkAddress addr) {
    final p = addr.port;
    return p != null
        ? StructTerm(_endpointFunctor,
            <Term>[ConstTerm(_requote(addr.host)), ConstTerm(p)])
        : ConstTerm(_requote(addr.host));
  }

  static Term _nonceToTerm(LinkNonce nonce) => nonce.isInteger
      ? ConstTerm(nonce.intValue)
      : ConstTerm(_requote(nonce.stringValue!));

  // ---- in-band request token (T033 path-B handshake, OQ-A3) ----

  /// Build the in-band `request(LinkId, FromPeer)` handshake token.
  static Term requestToken(LinkId id, Term fromPeer) =>
      StructTerm(_requestFunctor, <Term>[toTerm(id), fromPeer]);

  /// Parse a ground `request(LinkId, FromPeer)` token (the listener's in-band first frame).
  static (LinkId id, Term fromPeer) parseRequestToken(Term term) {
    if (term is! StructTerm ||
        term.functor != _requestFunctor ||
        term.args.length != 2) {
      throw ArgumentError('expected request/2 token, got ${_describe(term)}');
    }
    return (parseLinkId(term.args[0]), term.args[1]);
  }

  // ---- fault lattice terms (FR-043/045; rulings-log fault vocab) ----

  static Term ok() => ConstTerm('ok');
  static Term closed(LinkId id, String reason) => _fault2('closed', id, reason);
  static Term tempFail(LinkId id, String reason) =>
      _fault2('tempFail', id, reason);
  static Term permFail(LinkId id, String reason) =>
      _fault2('permFail', id, reason);

  /// Map a seam-level [LinkFaultSignal] to its GLP monitor term.
  static Term fromSignal(LinkFaultSignal s) {
    switch (s.kind) {
      case LinkFaultKind.closed:
        return closed(s.link, s.reason);
      case LinkFaultKind.transient:
        return tempFail(s.link, s.reason);
      case LinkFaultKind.permanent:
        return permFail(s.link, s.reason);
    }
  }

  static Term _fault2(String functor, LinkId id, String reason) =>
      StructTerm(functor, <Term>[toTerm(id), ConstTerm(reason)]);

  // ---- const extraction helpers ----

  static Object? _const(Term term, String what) {
    if (term is ConstTerm) {
      final v = term.value;
      return v is String ? _unquote(v) : v;
    } else if (term is VarRef) {
      throw ArgumentError(
          '$what: unbound/unresolved VarRef — kernel must deep-deref before parsing');
    } else {
      throw ArgumentError(
          '$what: expected an atomic constant, got ${_describe(term)}');
    }
  }

  /// Strip the surrounding double-quotes a GLP STRING literal carries in its ConstTerm value.
  /// The compiler stores a string constant as `"value"` (quotes included) so the type
  /// checker can tell a String literal from a bare atom (parser.cs: "Wrap in quotes so type
  /// checker can distinguish strings from atoms"); a bare atom (e.g. `listener`,
  /// `abrupt`) carries no quotes. The kernels need the raw host value (e.g. the scheme
  /// token, the IPv4 host for `IPAddress.Parse`), so strip one surrounding pair if present.
  static String _unquote(String s) =>
      s.length >= 2 && s[0] == '"' && s[s.length - 1] == '"'
          ? s.substring(1, s.length - 1)
          : s;

  /// Inverse of [_unquote]: wrap a raw host string value in the
  /// surrounding double-quotes a GLP STRING constant carries, so a term rebuilt from
  /// host data is structurally identical to the source literal (idempotent — a value
  /// that already carries quotes is left as-is).
  static String _requote(String s) =>
      s.length >= 2 && s[0] == '"' && s[s.length - 1] == '"' ? s : '"$s"';

  static String _constString(Term term, String what) {
    final v = _const(term, what);
    if (v is String) return v;
    throw ArgumentError(
        '$what: expected a String constant, got ${_describe(term)}');
  }

  static int _constLong(Term term, String what) {
    final v = _const(term, what);
    if (v is int) return v;
    throw ArgumentError(
        '$what: expected an Integer constant, got ${_describe(term)}');
  }

  static String _describe(Term term) {
    if (term is ConstTerm) {
      return 'const(${term.value ?? "null"})';
    } else if (term is StructTerm) {
      return '${term.functor}/${term.args.length}';
    } else if (term is VarRef) {
      return 'var@${term.addr}';
    } else {
      return term.runtimeType.toString();
    }
  }
}
