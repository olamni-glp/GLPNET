using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Ynet.Transport.Link;

/// <summary>
/// AES-256-GCM sealed session (FR-003) reused from the olamnit crypto-envelope baseline, with the
/// D2 hardenings applied: H2 = atomic monotonic send counter so a nonce is never reused (the
/// olamnit nonce-reuse risk), H3 = HKDF-SHA256 key derivation (stronger than the minimal inherited
/// KDF). REAL + TESTED (T008/T013).
///
/// The 96-bit GCM nonce = 32-bit fixed salt || 64-bit monotonic counter. Interlocked.Increment
/// guarantees no two sends on one seal share a counter (H2). A seal refuses once the counter space
/// is exhausted rather than wrapping (fail-closed, never nonce-reuse).
/// </summary>
public sealed class SessionSeal : IDisposable
{
    private readonly AesGcm _gcm;
    private readonly uint _salt;
    private long _counter; // atomic send counter (H2)

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private SessionSeal(byte[] key, uint salt)
    {
        _gcm = new AesGcm(key, TagSize);
        _salt = salt;
    }

    /// <summary>Directionality of a one-way seal — each direction derives a DISTINCT key.</summary>
    public enum Direction : byte { Initiator = 1, Responder = 2 }

    /// <summary>
    /// Derive a 256-bit session key via HKDF-SHA256 (H3) from a shared secret + context. The uint
    /// salt AND the direction are mixed into BOTH the HKDF salt and info (codexreview finding): two
    /// seals derived with the same (secret, context) but a different salt/direction therefore get
    /// DISTINCT keys, so even if their nonce streams coincide there is no (key, nonce) reuse. A seal
    /// instance is strictly one-directional; never re-derive with the same (secret, context, salt,
    /// direction).
    /// </summary>
    public static SessionSeal Derive(
        ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> context, uint salt,
        Direction direction = Direction.Initiator)
    {
        Span<byte> hkdfSalt = stackalloc byte[5];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(hkdfSalt, salt);
        hkdfSalt[4] = (byte)direction;

        // info = context || direction, so the derived key is bound to the direction too.
        Span<byte> info = stackalloc byte[context.Length + 1];
        context.CopyTo(info);
        info[^1] = (byte)direction;

        Span<byte> key = stackalloc byte[32];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, key, salt: hkdfSalt, info: info);
        return new SessionSeal(key.ToArray(), salt);
    }

    /// <summary>Seal a frame. Returns nonce || ciphertext || tag. Throws if the counter is exhausted.</summary>
    public byte[] Seal(ReadOnlySpan<byte> plaintext)
    {
        long n = Interlocked.Increment(ref _counter); // H2: unique, monotonic, atomic
        if (n < 0) throw new InvalidOperationException("send counter exhausted — reseal required (no nonce reuse).");

        Span<byte> nonce = stackalloc byte[NonceSize];
        BinaryPrimitives.WriteUInt32BigEndian(nonce, _salt);
        BinaryPrimitives.WriteInt64BigEndian(nonce[4..], n);

        var output = new byte[NonceSize + plaintext.Length + TagSize];
        nonce.CopyTo(output);
        var ciphertext = output.AsSpan(NonceSize, plaintext.Length);
        var tag = output.AsSpan(NonceSize + plaintext.Length, TagSize);
        _gcm.Encrypt(nonce, plaintext, ciphertext, tag);
        return output;
    }

    /// <summary>Open a sealed frame produced by <see cref="Seal"/>. Returns null on auth failure.</summary>
    public byte[]? Open(ReadOnlySpan<byte> sealed_)
    {
        if (sealed_.Length < NonceSize + TagSize) return null;
        var nonce = sealed_[..NonceSize];
        var ciphertext = sealed_[NonceSize..^TagSize];
        var tag = sealed_[^TagSize..];
        var plaintext = new byte[ciphertext.Length];
        try
        {
            _gcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (AuthenticationTagMismatchException)
        {
            return null; // tampered / wrong key — caller treats as a distinct failure, never silent downgrade
        }
    }

    public void Dispose() => _gcm.Dispose();
}
