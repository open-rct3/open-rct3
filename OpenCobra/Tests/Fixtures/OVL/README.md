# OVL test fixtures

Real `.common.ovl`/`.unique.ovl` archive pairs, embedded into the `Tests` assembly so OVL
parsing bugs can be reproduced and regression-tested without a full RCT3 install
(`RCT3_PATH`-gated integration tests in `OpenCobra/Tests/Integration/` still cover the
full install separately).

- **`CustomScenery/`** — fan-made custom scenery packs, one subfolder per pack. Preferred
  over adding actual base-game OVLs for general parsing coverage, since community CS is
  freely downloadable and each pack's own README documents its source/author for
  attribution. See `CustomScenery/README.md` before adding a pack.
- **`CFRs/`** — custom flat rides, same one-subfolder-per-pack/README convention as
  `CustomScenery/`, just split out since a ride's OVLs (car/station/etc.) are a
  different shape than scenery.
- No `BaseGame/` folder currently exists — none of the fixtures here are Frontier
  Developments' own game data. If one is ever added for a reason custom scenery/rides
  can't cover, give it the same care around copyright as the rest of this tree.

Any `.ovl` dropped under this tree is picked up automatically by `Tests.csproj`'s
`EmbeddedResource` glob — no project-file edits needed. `TexturesTests.cs` discovers
fixtures by embedded-resource name suffix (`*.common.ovl`), so it exercises every pack
added here without further code changes.
