---
name: drive-native-app
description: Build, launch, debug, and screenshot OpenRCT3 — a native Windows Forms + OpenGL (Silk.NET/WGL) desktop app, not Electron/CLI/web. Use this instead of browser or CLI-automation tools whenever the task needs to see or interact with the running game window (visual verification, camera/rendering bugs, UI screenshots, manual repro of a WinForms/GLSurface issue).
---

# Driving the OpenRCT3 desktop app

OpenRCT3 (`OpenRCT3/OpenRCT3.csproj`) is a WinForms host (`GameWindow`, a `System.Windows.Forms.Form`)
with a child OpenGL surface (`GLSurface`) rendered via `Silk.NET.WGL`. There is no DevTools, no
Electron IPC, and no browser DOM — the only way to see or drive it is Win32: window handles,
`SendInput`, and screen capture. Chrome/browser MCP tools do not apply here.

All interaction goes through `scripts/AppDriver.ps1`, a self-contained PowerShell script (uses
inline C# via `Add-Type` for the Win32 P/Invoke calls). **Use the PowerShell tool, not Bash** —
this repo's Bash/Cygwin environment intermittently fails to spawn Windows GUI processes
(`uv_spawn`/fork errors), while PowerShell launches and controls the native window reliably.

Each action is a separate script invocation (PowerShell tool state doesn't persist between
calls). The target window is re-resolved every time via a PID file in `%TEMP%` (falls back to
a title search for `OpenRCT3` if the PID file is stale), so ordering across separate tool calls
is fine.

## STOP before taking control of the user's mouse and keyboard

`Screenshot`, `Click`/`DoubleClick`/`RightClick`, `MouseMove`, `Key`, and `Text` all call
`SetForegroundWindow` first, which **steals focus from whatever window the user is currently
looking at**. `Click`/`DoubleClick`/`RightClick`/`MouseMove` additionally **warp the user's real
system cursor** to click at, and `Key`/`Text` send input to whatever ends up focused.

This is not a hypothetical. This runs on the user's real, currently-in-use desktop — not a
sandboxed VM or CI runner — and there is no isolation between "the app being driven" and
whatever else is on screen. In practice this has already: captured an unrelated app's private
conversation into a screenshot because focus landed somewhere other than intended, and moved
the user's literal cursor and clicked while they could have been mid-task in another window.
There is no way to distinguish "safe, I have the desktop to myself" from "the user is actively
typing an email right now" from inside the script.

**Before the first `Screenshot`/`Click`/`DoubleClick`/`RightClick`/`MouseMove`/`Key`/`Text` call
in a turn, stop and ask the user in chat — do not just announce and proceed.** State plainly
that this will steal focus and (for click/move actions) move their real mouse cursor, then wait
for an explicit go-ahead before calling the script. Re-ask if there's been a long gap since the
last confirmation, or anything suggests the user has moved on to something else in the
meantime (e.g. the window went away on its own, an action failed unexpectedly, or a lot of time
has passed) — don't treat one earlier "yes" as blanket permission for the rest of the session.

`Build`, `Launch`, `Info`, and `Logs` don't touch focus or input and don't need this. `Close`
doesn't move focus/input either, but launching a build is itself only worth doing once the user
has agreed you'll be interacting with the window at all.

## Workflow

```powershell
# 1. Build (Debug by default; pass -Configuration Release if needed)
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Build

# 2. Launch — waits for the main window handle, prints PID/handle/rect
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Launch

# 3. Inspect — confirm the window exists, is foreground, and read its rect/client size
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Info

# 4. Screenshot — raises the window, captures it, prints the saved PNG path
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Screenshot -OutFile D:\path\to\shot.png
# Then use the Read tool on that PNG path to actually view it.

# 5. Interact
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Click -X 400 -Y 300 -ClientCoords
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action RightClick -X 400 -Y 300 -ClientCoords
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Key -Keys "{ESC}"          # SendKeys syntax
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Text -Text "hello"          # literal text, auto-escaped

# 6. Logs — tails %APPDATA%\OpenRCT3\logs\app.log (NLog; see OpenRCT3/nlog.config)
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Logs -Lines 80

# 7. Close — sends WM_CLOSE so GameWindow_FormClosing/Game.Quit() run cleanly;
#    add -Force to Stop-Process if it's hung.
powershell -File .claude\skills\drive-native-app\scripts\AppDriver.ps1 -Action Close
```

Screenshot/Click/MouseMove coordinates are screen coordinates by default; pass `-ClientCoords`
to give coordinates relative to the game's client area instead (matches what you'd read off a
screenshot, since the captured image is the full window including its title bar/border — use
`Info`'s `ClientSize` vs `ScreenRect` to line the two up).

## Gotchas

- **First-run install picker**: `Program.windows.cs` shows a blocking folder-picker dialog if
  `%APPDATA%\OpenRCT3\config.json` doesn't have a valid `InstallPath`. If `Launch` times out
  waiting for the main window, check whether a picker dialog appeared instead (a *different*
  visible window) — that means `config.json` is missing/invalid, not that the app crashed.
- **Working directory matters**: the exe loads `nlog.config` via a path relative to CWD, not
  the assembly location. `Launch` sets `-WorkingDirectory` to the exe's own folder — don't
  invoke `OpenRCT3.exe` directly from an arbitrary directory.
- **Screenshot uses `CopyFromScreen`, not `PrintWindow`**: the game draws via raw WGL
  `SwapBuffers`, which `PrintWindow`'s DC-replay path doesn't reliably capture. This means the
  window must actually be on-screen and unoccluded. `Set-Foreground` (used by every
  input/screenshot action) verifies the foreground switch actually landed via the
  `AttachThreadInput`-based `ForceForeground` helper and throws rather than continuing if it
  can't confirm it — plain `SetForegroundWindow` silently no-ops when called from a background
  process, and capturing/clicking without that check means acting on whatever window actually
  has focus instead of the game.
- **DPI**: the script sets Per-Monitor-V2 DPI awareness on itself so `GetWindowRect` and
  `CopyFromScreen` agree in physical pixels. If clicks land visibly off-target, compare
  `Info`'s `ScreenRect`/`ClientSize` against the screenshot's actual pixel dimensions first.
- **Unhandled exceptions block on a modal**: `Program.HandleException` shows an
  Abort/Retry/Ignore `MessageBox` on any unhandled exception, which will make `Launch` (or a
  later `Info`) see a window whose title isn't `OpenRCT3` — if an action can't find the target
  window, check for a stray `OpenRCT3 Error` dialog via a title search before assuming a crash
  with no window at all.
