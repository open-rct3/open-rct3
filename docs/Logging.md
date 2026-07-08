# Logs

<!-- TODO: Write an intro paragraph for lay users -->

Logs are written to:

- **File**: `%APPDATA%/OpenRCT3/logs/app.log`
  - Windows: `%USERPROFILE%\AppData\Roaming\OpenRCT3\logs\app.log`
  - macOS: `~/Library/Application Support/OpenRCT3/logs/app.log`
  - Linux: `~/.local/share/OpenRCT3/logs/app.log`
- **Console**: stdout (when running in a terminal)
- **Chainsaw**: UDP at `127.0.0.1:7071` for live tailing

We recommend the following tools for live log tailing:

- [Loginator](https://github.com/dabeku/Loginator/releases/latest)
- [UDP Log Viewer](https://github.com/mrRobot62/udp-viewer/releases/latest)
- [LogViewPlus](https://www.logviewplus.com)
