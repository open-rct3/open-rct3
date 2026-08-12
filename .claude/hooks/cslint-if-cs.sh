#!/bin/bash
# Lint C# files with dotnet cslint

if [[ "$CLAUDE_FILE_PATH" == *.cs ]]; then
  dotnet cslint "$CLAUDE_FILE_PATH"
fi
