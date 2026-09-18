namespace OpenRCT3.OpenGL;

internal sealed class MacGlContextLifetime(nint libraryHandle) {
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

  public void SetCurrentContext(nint handle) {
    lock (lifetimeLock) {
      ObjectDisposedException.ThrowIf(disposed, this);
      context = handle;
    }
  }

  public void ThrowIfDisposed() {
    lock (lifetimeLock) ObjectDisposedException.ThrowIf(disposed, this);
  }

  public Handles Release() {
    lock (lifetimeLock) {
      if (disposed) return default;
      disposed = true;
      var handles = new Handles(library, context);
      library = nint.Zero;
      context = nint.Zero;
      return handles;
    }
  }

  public readonly record struct Handles(nint Library, nint Context);
}
