#!/bin/bash
# Format and check JS/TS files with deno

if [[ "$CLAUDE_FILE_PATH" =~ \.(js|ts|tsx|jsx|json)$ ]]; then
  deno fmt "$CLAUDE_FILE_PATH"
  deno check "$CLAUDE_FILE_PATH"
fi
