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
using OVL;

namespace Dumper.Models;

[Serializable]
public sealed class Project : IDisposable, ICollection<Ovl>, INotifyPropertyChanged, IObservable<Ovl>, ISerializable {
  private string name = "New Project";
  private readonly ObservableCollection<Ovl> archives = new();
  private readonly ObservableCollection<long> archiveHashes = new();
  private readonly List<IDisposable> subscriptions = new();

  public event PropertyChangedEventHandler? PropertyChanged;
  public event EventHandler<string>? Renamed;

  public string Name {
    get => name;
    set {
      name = value;
      Renamed?.Invoke(this, value);
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
    }
  }
  IReadOnlyCollection<Ovl> Archives => archives.AsReadOnly();

  public Project() {
    subscriptions.Add(
      archives.ToObservable().Subscribe(ovl => {
        archiveHashes[archives.IndexOf(ovl)] = ovl.GetHashCode();
      })
    );
  }

  public void Dispose() {
    subscriptions.ForEach(x => x.Dispose());
    subscriptions.Clear();
  }

  /// <summary>
  /// Listen for changes to this project's collection of OVL archives.
  /// </summary>
  public IDisposable Subscribe(IObserver<Ovl> observer) {
    return archives.ToObservable()
      .CombineLatest(archiveHashes.ToObservable(), (archive, hash) => archive)
      .Subscribe(observer);
  }

  #region ICollection<Ovl> Members
  public IEnumerator<Ovl> GetEnumerator() {
    return archives.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() {
    return ((IEnumerable) archives).GetEnumerator();
  }

  public void Add(Ovl item) {
    archives.Add(item);
    archiveHashes.Add(item.GetHashCode());
  }

  public void Clear() {
    archives.Clear();
    archiveHashes.Clear();
  }

  public bool Contains(Ovl item) {
    return archives.Contains(item);
  }

  public void CopyTo(Ovl[] array, int arrayIndex) {
    archives.CopyTo(array, arrayIndex);
  }

  public bool Remove(Ovl item) {
    archiveHashes.RemoveAt(archives.IndexOf(item));
    return archives.Remove(item);
  }

  /// <summary>
  /// Total number of OVL archives.
  /// </summary>
  public int Count => archives.Count;

  public bool IsReadOnly => ((ICollection<Ovl>) archives).IsReadOnly;
  #endregion

  #region INotifyPropertyChanged Members

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }

  private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }
  #endregion

  public void GetObjectData(SerializationInfo info, StreamingContext context) {
    info.AddValue("name", Name);
    info.AddValue("archives", Archives.Select(x => x.FileName));
  }
}
