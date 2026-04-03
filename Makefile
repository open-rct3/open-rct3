# OS detection for cross-platform bundling
ifeq ($(OS),Windows_NT)
    PLATFORM := Windows
else
    PLATFORM := $(shell uname -s)
endif

all: gui
	dotnet build OpenRCT3/OpenRCT3.csproj

.PHONY: install
install: release
ifeq ($(PLATFORM),Darwin)
	@cp -R OpenRCT3/bin/Release/net8.0-macos/osx-x64/OpenRCT3.app bin/OpenRCT3.app
else ifeq ($(PLATFORM),Windows)
	@cp OpenRCT3/bin/Release/net8.0-windows/OpenRCT3.exe bin/OpenRCT3.exe
endif

.PHONY: release
release: gui
	dotnet build OpenRCT3/OpenRCT3.csproj -c Release

.PHONY: gui
gui: ovl
	deno task build:desktop

.PHONY: website
website: ovl
	deno task build:website

.PHONY: debug
debug: ovl
	deno task build:plugins
	deno task build:desktop
	dotnet run --project OpenRCT3/OpenRCT3.csproj

.PHONY: ovl
ovl:
	dotnet build OpenCobra/OVL/OVL.csproj

.PHONY: dumper
dumper:
	dotnet run --project Dumper/Dumper.csproj

.PHONY: test
test: test-plugins
	deno check clients/desktop/main.ts
	deno task check:plugins
	dotnet test

.PHONY: test-plugins
test-plugins:
	dotnet run --project OpenCobra/Tests/TestRunner/OvlTestBench.csproj -- --plugins
