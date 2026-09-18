---
name: drive-native-app
description: Inspect OpenRCT3 through its native MCP without taking over desktop input.
---

# Native OpenRCT3 inspection

All running-game inspection must use OpenRCT3 MCP, including launch, screenshots, state, camera, diagnostics, and logs. Never use desktop-input helpers, global mouse or keyboard injection, focus changes, window repositioning, direct automation-pipe clients, or process-memory probes.

Run the unit gate at integration boundaries. Build and launch the exact worktree through MCP in Release for interactive play. Verify process and assembly identity, then capture the application framebuffer. If the branch lacks a required MCP capability, implement it first or explicitly leave native validation pending. Do not use the removed AppDriver fallback.
