# TODOs

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
- [ ] Implement tracked rides support (`OpenCobra/OVL/Enums.cs:235`) — **deferred**; will implement after OVL decoder is
      ready
- [x] Fix `Ovl` resource pointer/relocation resolution returning wrong bytes for some resources — see
      [.agents/summaries/completed-work/ovl-resource-relocation.md](.agents/summaries/completed-work/ovl-resource-relocation.md)
- [x] Create data model for inspector items (`OpenRCT3/ViewModels/Inspector.cs:14`)
- [ ] Handle OS-dependent and game-store-dependent game paths (`src/paths.d:34,49`)

### Engine & Rendering

- [ ] Update framebuffer on window resize/screen changes (`OpenRCT3/Platforms/macOS/GameViewController.cs:35`)
- [ ] Tear down graphics and other unmanaged resources (`OpenRCT3/Platforms/macOS/AppDelegate.cs:24`)

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
