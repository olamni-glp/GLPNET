import 'link_address.dart';
import 'link_scheme.dart';

/// The per-establishment uniqueness nonce. Mirrors the GLP
/// `Nonce ::= Integer ; String` component
/// (contracts/link-primitives.md §1, line 56). The integer and string forms are
/// kept distinct (an integer `5` is NOT the string `"5"`) so the value
/// equality that backs idempotency-at-identity (FR-007) matches the GLP term
/// exactly rather than a lossy canonical text.
class LinkNonce {
  /// True when this is the `Integer` form; false for the `String` form.
  final bool isInteger;

  /// The integer value, valid only when [isInteger].
  final int intValue;

  /// The string value, valid only when [isInteger] is false.
  final String? stringValue;

  const LinkNonce._(this.isInteger, this.intValue, this.stringValue);

  static LinkNonce int_(int value) => LinkNonce._(true, value, null);

  static LinkNonce str(String value) {
    // ignore: unnecessary_null_comparison, dead_code
    if (value == null) throw ArgumentError.notNull('value');
    return LinkNonce._(false, 0, value);
  }

  @override
  bool operator ==(Object other) =>
      other is LinkNonce &&
      isInteger == other.isInteger &&
      intValue == other.intValue &&
      stringValue == other.stringValue;

  @override
  int get hashCode => Object.hash(isInteger, intValue, stringValue);

  @override
  String toString() => isInteger ? intValue.toString() : stringValue!;
}

/// The stable, never-reused link identity. Mirrors the GLP
/// `LinkId ::= link_id(Scheme, Endpoint, Nonce)`
/// (contracts/link-primitives.md §1, lines 51-56).
///
/// Value equality (record) is the idempotency-at-identity basis (FR-007): a
/// re-setup with the same [LinkId] must return the already-established
/// link rather than open a conflicting duplicate, so [LinkId] keys the
/// per-instance LinkId→handle registry built in T030. The components are all
/// ground (the GLP wrappers guard `ground/1` before the host sees them), so a
/// [LinkId] is always fully determined.
class LinkId {
  final LinkScheme scheme;
  final LinkAddress endpoint;
  final LinkNonce nonce;

  const LinkId(this.scheme, this.endpoint, this.nonce);

  @override
  bool operator ==(Object other) =>
      other is LinkId &&
      scheme == other.scheme &&
      endpoint == other.endpoint &&
      nonce == other.nonce;

  @override
  int get hashCode => Object.hash(scheme, endpoint, nonce);

  @override
  String toString() => 'link_id($scheme, $endpoint, $nonce)';
}
