# TODOs

## 📋 Documentation & Tooling
- [x] Bundle the app for all OSes (`Makefile:6`)
- [ ] Add Zed agent rules to CLAUDE.md

---

Organized by [Roadmap](https://github.com/open-rct3/open-rct3/wiki/Roadmap) phases.

## 🔴 Phase 1: Engine & Rendering Scaffolding (Current Priority)

### Game Framework & OVL Format
- [ ] Confirm LODs relation in OVL enums (`OpenCobra/OVL/Enums.cs:36`) — see [.opencode/plans/ovl-enum-verification.md](.opencode/plans/ovl-enum-verification.md)
- [ ] Verify NoShadow duplicate value (`OpenCobra/OVL/Enums.cs:49`) — see [.opencode/plans/ovl-enum-verification.md](.opencode/plans/ovl-enum-verification.md)
- [ ] Implement tracked rides support (`OpenCobra/OVL/Enums.cs:235`) — **deferred**; will implement after OVL decoder is ready
- [x] Do not assume icon format, look it up from source (`OpenRCT3/Icons.cs:45`)
- [x] Create data model for inspector items (`OpenRCT3/ViewModels/Inspector.cs:14`)
- [ ] Handle OS-dependent and game-store-dependent game paths (`src/paths.d:34,49`)

### Engine & Rendering
- [ ] Update framebuffer on window resize/screen changes (`OpenRCT3/Platforms/macOS/GameViewController.cs:35`)
- [ ] Tear down graphics and other unmanaged resources (`OpenRCT3/Platforms/macOS/AppDelegate.cs:24`)

### Server/Engine Communication
- [ ] Write spec for game's WebSocket protocol (`src/server/routes.d:101`)
- [ ] Switch from binary to JSON messages (`src/server/routes.d:102`)
- [ ] Receive client name and metadata in WS messages (`src/server/routes.d:103`)
- [ ] Use `std.json` with protocol primitives (`src/server/routes.d:112,162`)
- [ ] Remove `sockets.removeFromArray` workaround (`src/server/routes.d:116`)

### Desktop Client Foundation
- [ ] Implement platform-specific window code for macOS/Windows/Linux (`clients/desktop/src/platform/window.ts:32-34`)
- [ ] Use bundled resource path on macOS (`clients/desktop/src/env.ts:30`)
- [ ] Fix window flickering on Windows 10 launch (`clients/desktop/main.ts:25`)

### Infrastructure
- [ ] Fix CI failures on macOS runners (`.github/workflows/ovl.yml:14`)
- [ ] Replace `fs.mkdir` with `fs.mkdirp` (`scripts/build-plugins.ts:92`)

---

## 🟡 Phase 2: Gameplay Implementation

### Multiplayer/Server
- [ ] Validate credentials in auth endpoint (`src/server/routes.d:61`)
- [ ] Verify bearer auth token (`src/server/routes.d:68`)
- [ ] Implement RFC 6750 bearer token auth (`src/server/routes.d:71`)
- [ ] Handle auth tokens from requested protocol (`src/server/routes.d:94`)
- [ ] Remove flash from session when reading data (`src/server/package.d:28`)
- [ ] Implement content type negotiation for HTML responses (`src/server/package.d:30`)

### Desktop Client Enhancements
- [ ] Extract IPC primitives into a shared library (`clients/desktop/main.ts:29`)
- [ ] Expose API on `Window.navigator` object (`clients/desktop/resources/content-script.js:5`)

### Dependencies & Tooling
- [ ] Add Deno Windows compilation support (`clients/desktop/main.ts:8`)
- [ ] Use Supabase on JSR (`clients/desktop/main.ts:9`)

---

## 🟢 Phase 3: Plugins & UX Polish

### Website & Frontend
- [ ] Use Lume SASS plugin (pending v2.2.4) (`clients/website/config.ts:11`)
- [ ] Refactor with WICC Observables (`clients/website/build.ts:31`)
- [ ] Prepend animated spinner to build output (`clients/website/build.ts:47`)
- [ ] Use WMR programmatically (`clients/website/dev.ts:10`)
- [ ] Refactor to spinner interface (`clients/website/dev.ts:25`)
- [ ] Don't exit for recoverable build errors (`clients/website/dev.ts:44`)
- [ ] Rebuild when files are created (`clients/website/dev.ts:53`)
- [ ] Extract on-top-for-sr into mixin (`clients/website/src/css/site.scss:311`)
- [ ] Use measured height for notification drawer minimum height (`clients/website/src/play.vto:96`)
- [ ] Fix drawer insertion code (`clients/website/src/play.vto:114`)
- [ ] Add main.js script tag (`clients/website/src/templates/base.vto:52`)
- [ ] Only render alert if cookie is unset (`clients/website/src/templates/partials/alert.vto:1`)
- [ ] Abstract error UI with details modal (`clients/website/src/templates/play.vto:47`)
- [ ] Connect to globally installed roslyn-language-server (`_zed/settings.json:24`)
