// ECS Entity Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.ECS;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// <h2>Archetype Storage</h2>
/// <para>
/// <list type="bullet">
/// <item>Per-component dense arrays grouped by archetype (entity signature).</item>
/// <item>Cache-friendly iteration, lockless reads during parallel system execution.</item>
/// </list>
/// </para>
/// </remarks>
public readonly record struct Entity {
  private static uint lastId = 0;
  public static Entity Null => new(0);

  public readonly uint Id;

  public Entity() => Id = lastId++;
  public Entity(uint id) => Id = id;

  public bool IsNull => Id == 0;

  public override string ToString() => $"{Id}";
}
