// Safe Weak Reference
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Threading;

/// <summary>
/// A type-safe weak reference that enforces safe access patterns.
/// </summary>
/// <remarks>
/// <para>
/// Wraps <see cref="WeakReference{T}"/> to ensure that all access to the referenced object
/// uses <see cref="TryGetTarget"/> atomically. This prevents the common race condition where
/// code checks <see cref="WeakReference{T}.IsAlive"/> and then later accesses <see cref="WeakReference{T}.Target"/>,
/// allowing the target to be garbage collected between the check and the dereference.
/// </para>
/// <para>
/// Production game engines (Unreal, Bevy, Godot, Unity) all follow the same pattern:
/// validate and dereference atomically at the point of use, never separately.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the weakly referenced object.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SafeWeakReference{T}"/> class that
/// references the specified target object.
/// </remarks>
/// <param name="target">The target object to reference.</param>
/// <param name="trackResurrection">Whether to track object resurrection.</param>
public class SafeWeakReference<T>(T target, bool trackResurrection) where T : class {
  private readonly WeakReference<T> inner = new WeakReference<T>(target, trackResurrection);

  /// <summary>
  /// Initializes a new instance of the <see cref="SafeWeakReference{T}"/> class that
  /// references the specified target object.
  /// </summary>
  /// <param name="target">The target object to reference.</param>
  public SafeWeakReference(T target) : this(target, trackResurrection: false) { }

  /// <summary>
  /// Gets a value indicating whether the target object has been garbage collected.
  /// </summary>
  /// <remarks>
  /// <strong>Note:</strong> Checking <see cref="IsAlive"/> is not a safe way to validate
  /// before accessing <see cref="TryGetTarget"/>. Use <see cref="TryGetTarget"/> instead,
  /// which validates and dereferences atomically.
  /// </remarks>
  public bool IsAlive => inner.TryGetTarget(out _);

  /// <summary>
  /// Gets the target object, throwing an <see cref="ObjectDisposedException"/> if the reference is dead.
  /// </summary>
  /// <exception cref="ObjectDisposedException">Thrown if the target has been garbage collected.</exception>
  /// <seealso cref="IsAlive"/>
  public T Target {
    get {
      if (inner.TryGetTarget(out var target))
        return target;
      throw new ObjectDisposedException(nameof(Target));
    }
  }

  /// <summary>
  /// Attempts to retrieve the target object. Returns true if the target is still alive.
  /// </summary>
  /// <remarks>
  /// This is the safe way to access the referenced object. It atomically checks if the
  /// target is alive and retrieves it in a single operation, preventing the race condition
  /// where the object is garbage collected between a separate validity check and dereference.
  /// </remarks>
  /// <param name="target">The target object, or null if it has been collected.</param>
  /// <returns>True if the target object is still alive; false otherwise.</returns>
  public bool TryGetTarget(out T? target) => inner.TryGetTarget(out target);
}
