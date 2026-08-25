"""Encryption envelope + one-time passphrase (067 FR-004, contracts/payload-contract.md §2).

Envelope: ``salt(16) || nonce(12) || ciphertext || tag(16)``

- KDF: scrypt(passphrase, salt=16 random bytes, N=2**15, r=8, p=1, dkLen=32)
- Cipher: AES-256-GCM, nonce = 12 random bytes, AAD = ASCII ``GQP1``

The passphrase is generated hub-side, displayed once beside the QR, and travels **out-of-band**
(spoken/typed). It is never written to disk and never appears in an audit row.

A wrong passphrase fails the GCM tag check, which raises :class:`BadPassphrase` — the caller must
discard all buffered material. No partial plaintext is ever produced (FR-004): AES-GCM verifies
the tag before returning any plaintext, so this property comes from the construction, not from
caller discipline.
"""

from __future__ import annotations

import secrets

from cryptography.exceptions import InvalidTag
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.kdf.scrypt import Scrypt

#: Additional authenticated data — binds the ciphertext to this payload-format version.
AAD = b"GQP1"

SALT_LEN = 16
NONCE_LEN = 12
KEY_LEN = 32

#: scrypt cost parameters. N=2**15 keeps derivation ~100ms on a desktop, which is the point: it
#: hardens a short human-transcribable passphrase against offline brute-force of a photographed
#: code within the session window.
SCRYPT_N = 2**15
SCRYPT_R = 8
SCRYPT_P = 1

#: Passphrase wordlist — deliberately short, unambiguous, easy to say aloud over a room or a
#: phone line. 256 words x 6 = ~48 bits, which is the out-of-band factor, not the only defence:
#: the session window (default 10 min) and single-redemption bound the attack surface.
_WORDS = (
    "amber anchor apple arrow autumn basil beacon birch bishop bloom bottle branch bridge bronze"
    " brook button cabin candle canvas carbon cedar chalk cherry cinder circle citron clover"
    " cobalt comet copper coral cotton crane crater crimson crystal cypress daisy damson dawn"
    " delta denim desert diamond dolphin domino dragon driftwood dune ember emerald ermine falcon"
    " fathom fennel fern fiddle finch flint florin forest fossil fountain foxglove gable galley"
    " garnet gauge geode ginger glacier granite gravel grotto guild hammer harbor harvest hazel"
    " heather hedge helix hollow honey hornet indigo ingot iris ironwood ivory jasmine jetty"
    " juniper kestrel kettle keystone lantern lattice laurel ledger lichen lilac linden lintel"
    " lobster locket lumber lyric magnet mallow mantle maple marble marlin marsh meadow medley"
    " mercury mesa mica millet mineral minnow mint mirror mistral mohair morrow mosaic moss"
    " mulberry mustard nectar nettle nickel nimbus nutmeg oakum obsidian ochre olive onyx opal"
    " orchard osprey otter oyster paddock pallet pampas papyrus parcel parsley pasture peat"
    " pebble pelican pennant pepper pewter pigeon pillar pine pinnacle pistol plover plum pollen"
    " poplar poppy portal pottery prairie primrose pumice quarry quartz quill quince radish"
    " rafter ramble rampart raven reed regent relic ribbon rill rivet robin rosemary rowan"
    " rudder saffron sage salmon sandal sapling sapphire scarlet sedge sequoia shale shallot"
    " shamrock sherry shingle sienna silver siskin slate sloop smelt sorrel spindle spruce"
    " starling stipple stirrup stonecrop sumac sundial swallow sycamore tallow tamarind tandem"
    " tansy tarragon teasel tempest terrace thicket thistle thorn thyme timber tinder topaz"
    " torrent trellis trillium trout tundra turret umber vellum verbena vervain vetch viola"
    " violet walnut warbler watermark wattle weald wheat whistle willow windlass wintergreen"
    " wisteria woodbine wren yarrow yellow yew zenith zephyr zinc"
).split()

PASSPHRASE_WORDS = 6


class BadPassphrase(Exception):
    """The supplied one-time passphrase did not decrypt the envelope.

    Raised on GCM tag failure. Callers MUST discard every buffered chunk and byte on this path.
    """


class MalformedEnvelope(Exception):
    """The envelope is too short or structurally invalid (before any key derivation)."""


def generate_passphrase(words: int = PASSPHRASE_WORDS) -> str:
    """Generate a one-time passphrase: ``words`` wordlist entries joined by ``-``.

    Uses :mod:`secrets`, never :mod:`random`. The result is displayed once, hub-side, and is
    never persisted.
    """
    if words < 1:
        raise ValueError(f"words must be >= 1; got {words}")
    return "-".join(secrets.choice(_WORDS) for _ in range(words))


def _derive_key(passphrase: str, salt: bytes) -> bytes:
    kdf = Scrypt(salt=salt, length=KEY_LEN, n=SCRYPT_N, r=SCRYPT_R, p=SCRYPT_P)
    return kdf.derive(passphrase.encode("utf-8"))


def seal(plaintext: bytes, passphrase: str) -> bytes:
    """Encrypt ``plaintext`` under ``passphrase``; return the wire envelope bytes."""
    salt = secrets.token_bytes(SALT_LEN)
    nonce = secrets.token_bytes(NONCE_LEN)
    key = _derive_key(passphrase, salt)
    ciphertext = AESGCM(key).encrypt(nonce, plaintext, AAD)
    return salt + nonce + ciphertext


def open_envelope(envelope: bytes, passphrase: str) -> bytes:
    """Decrypt an envelope produced by :func:`seal`.

    Raises :class:`MalformedEnvelope` if the framing is wrong, :class:`BadPassphrase` if the tag
    check fails. Never returns partial plaintext.
    """
    # 16 tag bytes is the AES-GCM minimum for a zero-length message; anything shorter cannot be a
    # well-formed envelope regardless of the passphrase.
    if len(envelope) < SALT_LEN + NONCE_LEN + 16:
        raise MalformedEnvelope(
            f"envelope too short: {len(envelope)} bytes, "
            f"need at least {SALT_LEN + NONCE_LEN + 16}"
        )
    salt = envelope[:SALT_LEN]
    nonce = envelope[SALT_LEN : SALT_LEN + NONCE_LEN]
    ciphertext = envelope[SALT_LEN + NONCE_LEN :]
    key = _derive_key(passphrase, salt)
    try:
        return AESGCM(key).decrypt(nonce, ciphertext, AAD)
    except InvalidTag as exc:
        raise BadPassphrase("bad_passphrase") from exc
