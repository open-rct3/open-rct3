# Todo List

## Community

- [x] Create a private Discord server to coordinate our reverse engineering work (For me, `@Syn`, and `@TheMaskedMan00`)
- [ ] Add an automation to `.claude\skills` that automatically updates the list of issues and the OpenRCT3 project
      (https://github.com/orgs/open-rct3/projects/1)

---

## Phase 1: Engine & Rendering Scaffolding

### OVL Decoding

- [x] Confirm LODs relation in OVL enums (`OpenCobra/OVL/Enums.cs:36`) — see
      [.agents/summaries/ovl-enum-verification.md](.agents/summaries/ovl-enum-verification.md)
- [x] Verify NoShadow duplicate value (`OpenCobra/OVL/Enums.cs:49`) — see
      [.agents/summaries/ovl-enum-verification.md](.agents/summaries/ovl-enum-verification.md)
- [ ] Implement tracked rides support (`OpenCobra/OVL/Enums.cs:235`). **Partial:** `spl`/`tks`
      decode via `TrackData`, and `Ovl.SymbolReferences` exposes a `trr`'s cross-archive segment
      references. The `trr` resource body itself (trains, cars, per-ride metadata) is still not
      decoded.
- [x] Fix `Ovl` resource pointer/relocation resolution returning wrong bytes for some resources — see
      [.agents/summaries/completed-work/ovl-resource-relocation.md](.agents/summaries/completed-work/ovl-resource-relocation.md)
- [x] Create data model for inspector items (`OpenRCT3/ViewModels/Inspector.cs:14`)
- [ ] Handle OS-dependent and game-store-dependent game paths (`src/paths.d:34,49`)
- [ ] Investigate `mms`/`prt`/`psi` decoders' premise (assumed tex/flic/btbl-shaped, confirmed wrong)
      (`OpenCobra/OVL/Files/CharacterSkins.cs`, `OpenCobra/OVL/Files/ParticleEffects.cs`)
- [ ] Validate `BinaryReader.ReadBytes` returned the full requested size before `Marshal.PtrToStructure`
      in `BinaryReaderExtensions.Read<T>` (`OpenCobra/OVL/Files/TextureDecoding.cs:188`) — currently only
      validated for `Tex`
- [ ] Root-cause `gsi`/`shs` showing "0 LoaderStruct entries" in `Main.common.ovl` (same signature as the
      since-fixed `tex`/`fct` issue, not independently investigated)

### Engine & Rendering

- [ ] Update framebuffer on window resize/screen changes (`OpenRCT3/Platforms/macOS/GameViewController.cs:35`)
- [ ] Tear down graphics and other unmanaged resources (`OpenRCT3/Platforms/macOS/AppDelegate.cs:24`)
- [ ] Verify `GLState.IsCoreProfile` detection logic (`OpenCobra/GDK/GLState.cs:73`) — flagged as possibly
      incorrect when written
- [ ] Build GDK meshes directly from decoded `StaticShape` vertex/triangle data
      (`OpenCobra/OVL/Files/StaticShapes.cs`) for in-game rendering, not just Dumper preview
- [ ] `ImDraw` (`OpenCobra/GDK/ImDraw.cs`) has no real consumers yet — default line width, circle segment
      count, degenerate-line-direction collapse threshold, and `DynamicDraw` vs `StreamDraw` buffer usage
      are all unturned, deferred until a real caller (brush cursor, route/waypoint visualization) exists

### Camera & Input

- [ ] Wire Freelook/Isometric mouse-drag camera bindings to actual camera behavior (bindings exist in
      `OpenRCT3/Input/DefaultBindings.cs` but aren't consumed — only Normal mode is)
- [ ] Add an explicit `CameraMode` enum/dispatch — mode-dependent effects (e.g. Q/E snap vs. continuous
      rotate) are currently hand-coded per action rather than dispatched through a mode concept
- [ ] Use Windows Raw Input API (`RI_MOUSE_WHEEL`/`RI_MOUSE_HWHEEL`) for finer scroll-wheel granularity
      than WinForms' `WM_MOUSEWHEEL`-based `MouseWheel` event (`OpenRCT3/Input/InputController.cs:67`)
- [ ] Add gamepad bindings, a rebinding UI, and binding-conflict detection to the input system

### ECS & World Systems

- [ ] Implement progress bar UI for park loading (`OpenRCT3/Game.cs:122`) — Create a loading-screen UI that displays `Progress` while `ParkLoadSystem` runs asynchronously. May require making `World.Load` truly async or moving it off the render loop thread.
- [ ] Migrate additional systems to the pipeline (input, camera, water invalidation, etc.) — Identify and implement other systems that could move into the `ISystem`/`Scheduler` pipeline once the pattern has more real-world examples to follow.

### Rides & Track Splines

- [ ] Tighten `BakingConfig` tolerance defaults (`ChordHeightToleranceFraction`, `ChordHeightToleranceAbsoluteMinimum`,
      `BankRateThreshold`) against real piece geometry once authoring content exists to validate against
      (depends on the editor above to author that content, and the render-pipeline item above to see it in-game)
- [ ] Densify procedural curve/corkscrew geometry beyond the fixed 4 Catmull-Rom segments
      (`OpenRCT3/Rides/TrackSpline/ProceduralPieces.cs:GenerateCurve,GenerateCorkscrew`) — independent quality
      improvement, no blockers
- [ ] Flag a subrange of the track graph as a station platform or block-braking segment
      (`OpenRCT3/Rides/TrackSpline/SplineTypes.cs`) — a stated goal of the original track-spline plan that was
      never implemented (no station/block concept exists on `TrackGraph`/`TrackGraphNode`/`TrackPiece` today);
      foundational data-model gap needed before train scheduling can exist
- [ ] Add supporting geometry for `TrackPieceType.Switch` (alternate exit rail set + active-branch metadata on
      `TrackPiece`) once a consumer (train scheduling/block signaling) needs it
      (`OpenRCT3/Rides/TrackSpline/SplineTypes.cs:118`) — pairs with block-section flagging above; both are
      prerequisites for train scheduling, not scheduling itself, so lowest priority of the data-model items
- [ ] Implement the wheel bone-posing IK solver (two-bone/point-toward) that consumes `WheelIK`'s
      `BogiContactPoint` data (`OpenRCT3/Rides/TrackSpline/WheelIK.cs`) — this plan only produces contact-point
      queries; the actual skeletal posing is separate animation/skeleton-system work with no plan file yet.
      Consumes an API that's already complete, so it's unblocked but out of this list's own dependency chain
- [ ] Design 3D guest-pathfinding splines for flat-ride ramps/stairs/queues and tracked-ride station platforms
      — simpler than ride-track splines (no physics, no arc-length parameterization), purely geometric guidance
      to seating; separate feature from the dual-rail track model, no dependency on anything above
- [ ] Decode `TrackSection_S` / `TrackSection_W` (Soaked/Wild) `tks` layouts in
      `OpenCobra/OVL/Files/TrackData.cs`. The decoder only handles the 140-byte `TrackSection_V`, so
      Soaked/Wild-era coaster archives (`Track1`, `Track10`, `Track11`, and so on) read their six
      `SplineRefs` as zero and most of their sections come back `IsValid == false`. Blocks real
      coaster segment import. `.agents/tools/TrackDataVerifier/` flags the affected archives. The
      `tks-viewer` plugin's parser mirrors the `_V` layout and needs the same update.
- [ ] Build `TrackGraph` instances from a `TrackLibrary` & a track design (in-game placement, RCT3
      `.trk`, or RCT1 `.TD4` / RCT2 `.TD6` dropped into `Documents/RCT3/Coasters/`): segment
      chaining and world placement. `OpenRCT3/Rides/TrackLibrary.cs`'s `TrackLibrary.Read` produces
      the per-ride-type segment palette; nothing consumes it into a constructed ride yet. Depends on
      `features/track-spline-rendering`.
- [ ] Tune `SegmentConnectors.Derive` & `TrackConnector` geometry in `OpenRCT3/Rides/TrackLibrary.cs`
      against real chaining. Currently a rail-endpoint heuristic (position/tangent/bank/gauge from
      first/last nodes), untuned.
- [ ] Full track-geometry validation suite: end-to-end checks once both OVL decoding and
      track-spline rendering land (decoded segments, then chained graph, then baked samples,
      then in-engine).
- [ ] Classify the ~22 `addon: unknown` track archives in `.agents/summaries/track-rides.csv`. No
      `trr` resource references them, so `.agents/tools/TrackedRideCorrelator/`'s ride-correlation
      can't place them (Vanilla vs Soaked vs Wild).

## Phase 2: Gameplay

See the [Roadmap](https://github.com/open-rct3/open-rct3/wiki/Roadmap#phase-2-gameplay) for future phases.

---

## Infrastructure

- [ ] Fix CI failures on macOS runners (`.github/workflows/ovl.yml`)
- [ ] Enable
      [project coverage checks](https://docs.codecov.com/docs/common-recipe-list#set-project-coverage-checks-on-a-pull-request)
      to maintain code quality

## 💾 Memory Leaks

- [ ] OpenRCT3 in Windows launches and then immediately hangs with the wait cursor; there's likely a memory leak or
      OpenGL is not being used correctly.

## 📋 Documentation & Tooling

- [x] Bundle the app for all OSes (`Makefile:6`)
- [ ] Connect to globally installed roslyn-language-server (`_zed/settings.json:24`)

## Website & Frontend

- [ ] Use Lume SASS plugin (pending v2.2.4) (`clients/website/config.ts:11`)
- [ ] Refactor with WICC Observables (`clients/website/build.ts:31`)
- [ ] Prepend animated spinner to build output (`clients/website/build.ts:47`)
- [ ] Rebuild when files are created (`clients/website/dev.ts:53`)
- [ ] Use measured height for notification drawer minimum height (`clients/website/src/play.vto:96`)
- [ ] Fix drawer insertion code (`clients/website/src/play.vto:114`)
- [ ] Add main.js script tag (`clients/website/src/templates/base.vto:52`)
- [ ] Only render alert if cookie is unset (`clients/website/src/templates/partials/alert.vto:1`)
- [ ] Abstract error UI with details modal (`clients/website/src/templates/play.vto:47`)

---

### Pipe Dreams

#### Remote Play

The idea here is to support online play, i.e. play the game from your browser.

Somehow, an end-user will run the server on their machine, it will ingest OVLs from their local installation, run the
OpenCobra engine, and stream the game world's scene to the web client.

- [ ] Write spec for game's WebSocket protocol (`src/server/routes.d:101`)
- [ ] Switch from binary to JSON messages (`src/server/routes.d:102`)
- [ ] Receive client name and metadata in WS messages (`src/server/routes.d:103`)
- [ ] Use `std.json` with protocol primitives (`src/server/routes.d:112,162`)
- [ ] Validate credentials in auth endpoint (`src/server/routes.d:61`)
- [ ] Implement RFC 6750 bearer token auth (`src/server/routes.d:71`)
- [ ] Handle auth tokens from requested protocol (`src/server/routes.d:94`)
- [ ] Verify bearer auth token (`src/server/routes.d:68`)
- [ ] Implement content type negotiation for HTML responses (`src/server/package.d:30`)
