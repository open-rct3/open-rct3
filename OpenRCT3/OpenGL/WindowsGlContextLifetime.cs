using System.Collections.Generic;

namespace OpenRCT3.OpenGL;

internal sealed class WindowsGlContextLifetime(nint libraryHandle) {
  private readonly object lifetimeLock = new();
  private nint library = libraryHandle;
  private nint context;
  private bool disposed;

  public nint LibraryHandle {
    get {
      lock (lifetimeLock) {
        ObjectDisposedException.ThrowIf(disposed, this);
        return library;
      }
    }
  }

  public nint ContextHandle {
    get {
      lock (lifetimeLock) return disposed ? nint.Zero : context;
    }
  }

  public bool IsCurrent(Func<nint> getCurrentContext) {
    lock (lifetimeLock) {
      if (disposed || context == nint.Zero) return false;
      return getCurrentContext() == context;
    }
  }

  public void SetContext(nint handle) {
    lock (lifetimeLock) {
      ObjectDisposedException.ThrowIf(disposed, this);
      if (context != nint.Zero)
        throw new InvalidOperationException("An OpenGL context is already owned.");
      context = handle;
    }
  }

  public void RetainContext(nint handle) {
    if (handle == nint.Zero) return;
    lock (lifetimeLock) {
      ObjectDisposedException.ThrowIf(disposed, this);
      if (context == nint.Zero) {
        context = handle;
        return;
      }
      pendingContexts.Enqueue(handle);
    }
  }

  public bool TryReleaseContext(Func<nint, bool> release) {
    lock (lifetimeLock) {
      if (disposed || context == nint.Zero) return true;
      while (context != nint.Zero) {
        if (!release(context)) return false;
        context = pendingContexts.Count == 0
          ? nint.Zero
          : pendingContexts.Dequeue();
      }
      return true;
    }
  }

  public bool TryReleaseLibrary(Func<nint, bool> release) {
    lock (lifetimeLock) {
      if (disposed) return true;
      if (context != nint.Zero) return false;
      if (library != nint.Zero && !release(library)) return false;
      disposed = true;
      library = nint.Zero;
      return true;
    }
  }

  private readonly Queue<nint> pendingContexts = [];
}
