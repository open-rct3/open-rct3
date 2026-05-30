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

# Debug the Game

.PHONY: debug
debug:
	deno task build:plugins
	deno task build:desktop
	dotnet run --project OpenRCT3/OpenRCT3.csproj

# Game GUI

.PHONY: gui
gui: ovl
	deno task build:desktop

# Website

WEBSITE_DIR := clients/website
WEBSITE_SRC := $(wildcard clients/website/src/*.md) $(wildcard clients/website/src/*.vto) $(wildcard clients/website/src/css/*.scss) $(wildcard clients/website/src/templates/*.vto) $(wildcard clients/website/src/templates/partials/*.vto)

.PHONY: website
website: $(WEBSITE_DIR)/_site

$(WEBSITE_DIR)/_site: $(WEBSITE_SRC)
	deno task build:website

# FIXME: ParseError: Unexpected argument 'snapshot'
.PHONY: percy
percy: website
	deno run -A npm:@percy/cli snapshot -b "https://rct3.chancesnow.me" $(WEBSITE_DIR)/_site

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

.PHONY: test-ovl
test-ovl:
	dotnet test OpenCobra/Tests/Tests.csproj /p:SolutionDir=$(CURDIR)/

.PHONY: test-plugins
test-plugins:
# FIXME: This doesn't work on macOS
ifeq ($(PLATFORM),Windows)
	dotnet build OpenCobra/Tests/TestRunner/OvlTestBench.csproj /p:Profile=cli
	dotnet run --project OpenCobra/Tests/TestRunner/OvlTestBench.csproj -- --plugins
endif
