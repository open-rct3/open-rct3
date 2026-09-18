// Threading Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Threading;

/// <summary>
/// Represents an object which can only be safely used from its creation thread.
/// </summary>
/// <remarks>
/// The "creation thread" is that which was executing when this object was created.
/// </remarks>
public abstract class ThreadAffine {
  private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
  private readonly SynchronizationContext? context = SynchronizationContext.Current;

  /// <summary>
  /// Executes an action on the creation thread.
  /// </summary>
  /// <param name="action"></param>
  /// <exception cref="InvalidOperationException">
  /// Raised if called from a foreign thread.
  /// </exception>
  public void Invoke(Action action) {
    if (Environment.CurrentManagedThreadId == ownerThreadId) action.Invoke();
    else if (context != null) context.Send(_ => action.Invoke(), null);
    else throw new InvalidOperationException(
      $"Object was created on thread {ownerThreadId} but Invoke was called from thread {Environment.CurrentManagedThreadId}. A SynchronizationContext is not available to marshal the call.");
  }
}
