using System;
using System.Collections.Generic;

namespace OpenRCT3;

public sealed class Unsubscriber<T> : IDisposable {
  private readonly ISet<IObserver<T>> _observers;
  private readonly IObserver<T> _observer;

  internal Unsubscriber(
    ISet<IObserver<T>> observers,
    IObserver<T> observer) => (_observers, _observer) = (observers, observer
  );

  public void Dispose() => _observers.Remove(_observer);
}
