# Debugging

OpenRCT3 is a cross-platform game with managed C# layers over native OpenGL graphics. Debugging typically involves the
managed runtime, native interop, and OVL archive parsing.

The game and its libraries use [NLog](https://nlog-project.org/) for structured logging. Configuration is in
[`nlog.config`](../OpenRCT3/nlog.config). See [Logs](./Logging.md) for details.

## Table of Contents

- [Starting Points](#starting-points)
- [Debug with Visual Studio](#debug-with-visual-studio)
- [Attaching to the Game Process](#attaching-to-the-game-process)
- [OVL Parsing](#ovl-parsing)

## Starting Points

<!-- "This project is huge! I'm lost! Where do I start?!" -->

The global exception handler is located in `OpenRCT3/Program.*.cs` ([Windows](../OpenRCT3/Program.windows.cs),
[macOS](../OpenRCT3/Program.macos.cs), [Linux](../OpenRCT3/Program.linux.cs)).

| Component     | File                                                      | Tip                        |
| ------------- | --------------------------------------------------------- | -------------------------- |
| Game loop     | [`OpenRCT3/Game.cs:135`](../OpenRCT3/Game.cs)             | Main game loop entry point |
| OVL loading   | [`OpenCobra/OVL/OVL.cs:90`](../OpenCobra/OVL/OVL.cs)      | `IngestArchive()`          |
| OpenGL errors | [`OpenCobra/GDK/GLError.cs`](../OpenCobra/GDK/GLError.cs) | Check `GL.GetError()`      |

## Debug with Visual Studio

1. Open `OpenRCT3.sln` in Visual Studio
2. Build and debug the `OpenRCT3` project:
   - Set the startup project to `OpenRCT3`
   - Debug the project (F5)

The project's [`launchSettings.json`](../OpenRCT3/Properties/launchSettings.json) enables both managed and native
debugging.

### Settings

Debug → Options → General:

- Uncheck "Enable Just My Code" - this forces VS to break on exceptions in any code, including native/native-called code
- Check "Enable source link"

## Attaching to the Game Process

For debugging the running game:

1. Build with `make debug` which produces an executable with symbols
2. In Visual Studio, Debug → Attach to Process
3. Select `OpenRCT3.exe`

## OVL Parsing

See [OVL Archive Format](./ovl/archive-format.md).

### Dumper

The Dumper tool (`Dumper/`) is a Windows application for visually inspecting OVL archives and their resources.

To debug the Dumper:

1. Open `OpenRCT3.sln` and set Dumper as the startup project
2. Build and run (Ctrl+F5)
3. Open an archive to load an OVL pair

#### Resource Viewer Plugins

The Dumper loads [WebAssembly](https://webassembly.org) plugins via [Extism](https://extism.org).

### OVL Parsing

OVL archives are loaded via [`Ovl.Load`](../OpenCobra/OVL/OVL.cs:90). The loader:

<!-- TODO: Reword these steps; they suck -->

1. Reads magic bytes (`0x4B524746`; `FGRK`) to verify the format
2. Parses version 1, 4, or 5 headers
3. Builds a unified address space from `.common.ovl` + `.unique.ovl` file pair
4. Resolves relocations via a two-level block search
5. Collates the loaded OVL data into flat structures:

   - Lists of `FileTypeBlock`s and `LoaderHeader`s
   - A dictionary mapping `OvlFile` to `OvlEntry`

To debug OVL issues:

1. Connect your log viewer via the Chainsaw UDP socket
2. The `Ovl.ReadResource()` method reads raw bytes; breakpoint there to inspect loaded data
