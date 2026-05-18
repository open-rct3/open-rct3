# OS detection for cross-platform bundling
ifeq ($(OS),Windows_NT)
    PLATFORM := Windows
else
    PLATFORM := $(shell uname -s)
endif

all: gui
	dotnet build OpenRCT3/OpenRCT3.csproj

.PHONY: install
ifeq ($(PLATFORM),Darwin)
install: bin/OpenRCT3.app
else ifeq ($(PLATFORM),Windows)
install: bin/OpenRCT3.exe
endif

ifeq ($(PLATFORM),Darwin)
bin/OpenRCT3.app: release
	@cp -R OpenRCT3/bin/Release/net8.0-macos/osx-x64/OpenRCT3.app bin/OpenRCT3.app
else ifeq ($(PLATFORM),Windows)
bin/OpenRCT3.exe: release
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
debug:
	deno task build:plugins
	deno task build:desktop
	dotnet run --project OpenRCT3/OpenRCT3.csproj

.PHONY: ovl
ovl:
	dotnet build OpenCobra/OVL/OVL.csproj

.PHONY: dumper
dumper:
	dotnet run --project Dumper/Dumper.csproj

# Tests
# Sources and targets are automatically detected, i.e. re-builds are only performed if sources change

TESTS_SRC := $(wildcard OpenCobra/Tests/*.cs OpenCobra/Tests/*/*.cs)
# Extract TargetFramework from the csproj
TESTS_TFM := $(shell grep -oPm1 '(?<=<TargetFramework>)[^<]+' OpenCobra/Tests/Tests.csproj)
TESTS_DLL := OpenCobra/Tests/bin/Debug/$(TESTS_TFM)/Tests.dll

$(TESTS_DLL): $(TESTS_SRC)
	dotnet build OpenCobra/Tests/Tests.csproj

# Extract TargetFramework from the csproj
TFM := $(shell grep -oPm1 '(?<=<TargetFramework>)[^<]+' OpenCobra/Tests/TestRunner/OvlTestBench.csproj)
# Path to the compiled test runner using the detected TFM
OVL_TEST_BENCH_DLL := OpenCobra/Tests/TestRunner/bin/Debug/$(TFM)/OvlTestBench.dll

.PHONY: test
test: $(PLUGINS_OUT) $(TESTS_DLL) $(OVL_TEST_BENCH_DLL)
	deno check clients/desktop/main.ts
	deno task check:plugins
	dotnet test --no-build /p:SolutionDir=$(CURDIR)/

PLUGINS_SRC := $(wildcard plugins/*/index.ts)
PLUGINS_OUT := $(patsubst plugins/%/index.ts,bin/plugins/%.wasm,$(PLUGINS_SRC))
$(PLUGINS_OUT): $(PLUGINS_SRC)
	deno task build:plugins

$(OVL_TEST_BENCH_DLL): $(PLUGINS_OUT) $(TESTS_SRC)
# FIXME: This doesn't work on macOS
ifeq ($(PLATFORM),Windows)
	dotnet build OpenCobra/Tests/TestRunner/OvlTestBench.csproj
	dotnet run --project OpenCobra/Tests/TestRunner/OvlTestBench.csproj -- --plugins
endif
