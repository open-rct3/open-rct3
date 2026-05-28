// ECS Query Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections;

namespace OpenCobra.GDK.ECS;

public readonly ref struct Query<T1, T2> where T1 : struct where T2 : struct {
  private readonly World world;

  internal Query(World world) => this.world = world;

  public Enumerator GetEnumerator() => new(world);

  public class Enumerator(World world) : IEnumerator<Entity> {
    private int index = -1;

    public Entity Current => world.Entities.ElementAt(index).Key;
    object IEnumerator.Current => Current;

    public void Dispose() => GC.SuppressFinalize(this);
    public bool MoveNext() => ++index < world.Entities.Keys.Count;
    public void Reset() => index = -1;
  }
}
