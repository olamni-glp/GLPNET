import 'dart:async';

/// Per-link bounded send window for backpressure (FR-025, SC-013). The egress
/// drainer acquires one credit per in-flight frame and releases it when the frame
/// is acked/consumed by the peer. When all [capacity] credits are
/// out, the producer SUSPENDS (awaits [acquireAsync]) rather than
/// buffering unboundedly — the host-side reflection of FCP producer suspension, so
/// the outbound queue stays bounded (no OOM).
///
/// The default window is N=8 ([LinkOptions.backpressureWindow]),
/// scheme-overridable below the seam. Each link owns its own window, so a full
/// window on one link never blocks an independent link (no head-of-line blocking
/// across links — FR-025). Over-releasing (acking a frame that was never in
/// flight) throws, surfacing the bug at its cause rather than corrupting the count.
class SendWindow {
  /// The current number of free credits (mirrors `SemaphoreSlim.CurrentCount`).
  int _currentCount;

  /// FIFO queue of waiters parked in [acquireAsync] (mirrors the semaphore's
  /// async wait queue). Each completer is completed when a credit is released.
  final List<Completer<void>> _waiters = <Completer<void>>[];

  bool _disposed = false;

  /// The window size N — the maximum number of in-flight frames.
  final int capacity;

  /// [window] Window size N (default 8). Must be >= 1.
  SendWindow([int window = 8])
      : capacity = window,
        _currentCount = window {
    if (window < 1) {
      throw RangeError.value(window, 'window', 'window must be >= 1');
    }
  }

  /// Credits currently free (frames that may be sent without suspending).
  int get available => _currentCount;

  /// Frames currently in flight (credits acquired, not yet released).
  int get inFlight => capacity - _currentCount;

  /// Try to acquire a credit without suspending. Returns `false` when the
  /// window is full (the caller must then [acquireAsync] or back off).
  bool tryAcquire() {
    if (_currentCount > 0) {
      _currentCount--;
      return true;
    }
    return false;
  }

  /// Acquire a credit, SUSPENDING until one is free. This is the backpressure
  /// point: a producer outrunning the consumer parks here until an ack releases a
  /// credit.
  Future<void> acquireAsync() {
    if (_currentCount > 0) {
      _currentCount--;
      return Future<void>.value();
    }
    final waiter = Completer<void>();
    _waiters.add(waiter);
    return waiter.future;
  }

  /// Release a credit on ack/consume of one in-flight frame. Throws
  /// [SemaphoreFullError] if the window is already full (an ack
  /// for a frame that was never in flight — a double-ack bug).
  void release() {
    if (_waiters.isNotEmpty) {
      // Hand the credit directly to the next parked waiter (it stays "acquired").
      final waiter = _waiters.removeAt(0);
      waiter.complete();
      return;
    }
    if (_currentCount >= capacity) {
      throw SemaphoreFullError();
    }
    _currentCount++;
  }

  void dispose() => _disposed = true;

  bool get isDisposed => _disposed;
}

/// Thrown by [SendWindow.release] when the window is already full — the mirror
/// of .NET's `SemaphoreFullException` (an ack for a frame that was never in
/// flight, i.e. a double-ack bug).
class SemaphoreFullError extends StateError {
  SemaphoreFullError()
      : super('Adding the specified count to the semaphore would cause it to '
            'exceed its maximum count.');
}
