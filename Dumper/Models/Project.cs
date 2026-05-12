// Project
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using OpenCobra.OVL;

namespace Dumper.Models;

[Serializable]
public sealed class Project : IDisposable, ICollection<Ovl>, INotifyPropertyChanged, INotifyPropertyChanging, IObservable<Ovl>, ISerializable {
  public const string UnnamedProject = "New Project";
  private string name = UnnamedProject;
  private readonly ObservableCollection<Ovl> archives = [];
  private readonly ObservableCollection<long> archiveHashes = [];
  private readonly List<IDisposable> subscriptions = [];

  public event PropertyChangingEventHandler? PropertyChanging;
  public event PropertyChangedEventHandler? PropertyChanged;
  public event EventHandler<string>? Renamed;

  public string Name {
    get => name;
    set {
      OnPropertyChanging();
      name = value;
      Renamed?.Invoke(this, value);
      SetField(ref name, value);
    }
  }
  IReadOnlyCollection<Ovl> Archives => archives.AsReadOnly();

  public Project() => subscriptions.Add(
    archives.ToObservable().Subscribe(ovl => archiveHashes[archives.IndexOf(ovl)] = ovl.GetHashCode())
  );

  public void Dispose() {
    subscriptions.ForEach(x => x.Dispose());
    subscriptions.Clear();
  }

  /// <summary>
  /// Listen for changes to this project's collection of OVL archives.
  /// </summary>
  public IDisposable Subscribe(IObserver<Ovl> observer) => archives.ToObservable()
    .CombineLatest(archiveHashes.ToObservable(), (archive, hash) => archive)
    .Subscribe(observer);

  #region ICollection<Archive> Members
  public IEnumerator<Ovl> GetEnumerator() => archives.GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)archives).GetEnumerator();

  public void Add(Ovl item) {
    OnPropertyChanging(nameof(Archives));
    archives.Add(item);
    archiveHashes.Add(item.GetHashCode());
    OnPropertyChanged(nameof(Archives));
  }

  public void Clear() {
    archives.Clear();
    archiveHashes.Clear();
  }

  public bool Contains(Ovl item) => archives.Contains(item);

  public void CopyTo(Ovl[] array, int arrayIndex) => archives.CopyTo(array, arrayIndex);

  public bool Remove(Ovl item) {
    OnPropertyChanging(nameof(Archives));
    archiveHashes.RemoveAt(archives.IndexOf(item));
    var result = archives.Remove(item);
    OnPropertyChanged(nameof(Archives));
    return result;
  }

  /// <summary>
  /// Total number of OVL archives.
  /// </summary>
  public int Count => archives.Count;

  public bool IsReadOnly => (archives as ICollection<Ovl>)?.IsReadOnly ?? true;
  #endregion

  #region INotifyPropertyChanged Members

  private void OnPropertyChanging([CallerMemberName] string? propertyName = null) =>
    PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

  private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }
  #endregion

  public void GetObjectData(SerializationInfo info, StreamingContext context) {
    info.AddValue("name", Name);
    info.AddValue("archives", Archives.Select(x => x.Keys));
  }
}
