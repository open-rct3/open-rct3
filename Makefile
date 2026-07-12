# OS detection for cross-platform bundling
ifeq ($(OS),Windows_NT)
  PLATFORM := Windows
else
  PLATFORM := $(shell uname -s)
endif

all: release

# ===========
# Publishing
# ===========

.PHONY: install
ifeq ($(PLATFORM),Darwin)
install: bin/OpenRCT3.app
else ifeq ($(PLATFORM),Windows)
install: bin/OpenRCT3.exe
endif

# FIXME: Use `dotnet publish` to generate the bundle, this doesn't include all dependencies
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

.PHONY: ovl
ovl:
	dotnet build OpenCobra/OVL/OVL.csproj -c Release

# ==========
# Debugging
# ==========

.PHONY: dumper
dumper:
	dotnet run --project Dumper/Dumper.csproj

.PHONY: debug
debug: plugins
	dotnet run --project OpenRCT3/OpenRCT3.csproj

# Website

WEBSITE_DIR := clients/website
WEBSITE_SRC := $(wildcard clients/website/*.ts) $(wildcard clients/website/src/*.*) $(wildcard clients/website/src/css/*.scss) $(wildcard clients/website/src/templates/*.vto) $(wildcard clients/website/src/templates/partials/*.vto)

.PHONY: website
website: ovl $(WEBSITE_DIR)/_site

$(WEBSITE_DIR)/_site: $(WEBSITE_SRC)
	deno task build:website

# FIXME: ParseError: Unexpected argument 'snapshot'
.PHONY: percy
percy: website
	deno run -A npm:@percy/cli snapshot -b "https://rct3.chancesnow.me" $(WEBSITE_DIR)/_site

# =========================================================
# Tests
#
# Re-builds are only performed if sources change
# =========================================================

# Plugins

PLUGINS_SRC := $(wildcard plugins/*/index.ts)
PLUGINS_OUT := $(patsubst plugins/%/index.ts,bin/plugins/%.wasm,$(PLUGINS_SRC))
$(PLUGINS_OUT): $(PLUGINS_SRC)
	deno task build:plugins

.PHONY: plugins
plugins: $(PLUGINS_OUT)

.PHONY: test-plugins
test-plugins: $(PLUGINS_OUT)
	deno task check:plugins
	deno task test:plugins

# .NET Tests

TESTS_PROJ := OpenCobra/Tests/Tests.csproj
TEST_BENCH_PROJ := OpenCobra/Tests/TestRunner/OvlTestBench.csproj

# $(wildcard) alone doesn't recurse, and shelling out to `find` is not portable here - on Windows
# it silently resolves to cmd.exe's builtin FIND.EXE instead of GNU find depending on PATH/shell
# context ("FIND: Parameter format not correct"), and even GNU find's quoted -path globs can get
# shell-expanded before find sees them depending on how $(shell ...) invokes its shell. `rwildcard`
# recurses using only Make's own builtin $(wildcard), so there's no external-tool/PATH dependency
# at all. Source dirs matter here because a change to e.g. OpenCobra/OVL/Files/*.cs (a
# ProjectReference of Tests.csproj) must also trigger a rebuild, not just Tests' own sources.
rwildcard = $(wildcard $1$2) $(foreach d,$(wildcard $1*),$(call rwildcard,$d/,$2))

# Integration/TestRunner are separate projects (see Tests.csproj's own
# <Compile Remove="Integration\**"/TestRunner\**" />) so excluded here too.
TESTS_SRC := $(filter-out %/bin/% %/obj/% OpenCobra/Tests/Integration/% OpenCobra/Tests/TestRunner/%,\
  $(call rwildcard,OpenCobra/Tests/,*.cs) $(call rwildcard,OpenCobra/OVL/,*.cs) $(call rwildcard,OpenCobra/GDK/,*.cs))
# Extract TargetFramework from the csproj
TESTS_TFM := $(shell grep -oEm1 "<TargetFramework>[^<]+" OpenCobra/Tests/Tests.csproj | sed "s/<TargetFramework>//")
TESTS_DLL := OpenCobra/Tests/bin/Debug/$(TESTS_TFM)/Tests.dll

$(TESTS_DLL): $(TESTS_PROJ) $(TESTS_SRC)
	dotnet build OpenCobra/Tests/Tests.csproj

OPENRCT3_TESTS_PROJ := OpenRCT3.Tests/OpenRCT3.Tests.csproj
OPENRCT3_TESTS_SRC := $(filter-out %/bin/% %/obj/%,\
  $(call rwildcard,OpenRCT3.Tests/,*.cs) $(call rwildcard,OpenRCT3/,*.cs) $(call rwildcard,OpenCobra/OVL/,*.cs) $(call rwildcard,OpenCobra/GDK/,*.cs))
ifeq ($(PLATFORM),Darwin)
  OPENRCT3_TESTS_TFM := net9.0-macos
else ifeq ($(PLATFORM),Windows)
  OPENRCT3_TESTS_TFM := net8.0-windows10.0.17763.0
else
  OPENRCT3_TESTS_TFM := net8.0
endif
OPENRCT3_TESTS_DLL := OpenRCT3.Tests/bin/Debug/$(OPENRCT3_TESTS_TFM)/OpenRCT3.Tests.dll

$(OPENRCT3_TESTS_DLL): $(OPENRCT3_TESTS_PROJ) $(OPENRCT3_TESTS_SRC)
	dotnet build $(OPENRCT3_TESTS_PROJ) -p:SolutionDir=$(CURDIR)/

# Extract TargetFramework from the project files
TEST_BENCH_TFM := $(shell grep -oEm1 "<TargetFramework>[^<]+" $(TEST_BENCH_PROJ) | sed "s/<TargetFramework>//")
# Path to the compiled test runner using the detected TFM
TEST_BENCH_DLL := OpenCobra/Tests/TestRunner/bin/Debug/$(TEST_BENCH_TFM)/OvlTestBench.dll

# Validate TFM resolution
TFM_ERROR := Could not determine .NET target framework!
ifeq ($(TESTS_TFM),)
  $(error $(TFM_ERROR))
else ifeq ($(TEST_BENCH_TFM),)
  $(error $(TFM_ERROR))
else
  $(info Using '$(TESTS_TFM)' to compile $(TESTS_PROJ))
  $(info Using '$(TEST_BENCH_TFM)' to compile $(TEST_BENCH_PROJ))
endif

.PHONY: test
test: $(TESTS_DLL) $(OPENRCT3_TESTS_DLL)
	deno check clients/desktop/main.ts
	dotnet test OpenRCT3.tests.slnf --no-build /p:SolutionDir=$(CURDIR)

.PHONY: cover
cover: $(TESTS_DLL)
	dotnet test $(TESTS_PROJ) --no-build \
	  /p:SolutionDir=$(CURDIR) \
	  --collect:"XPlat Code Coverage;Format=lcov" \
	  --results-directory "$(CURDIR)/coverage"

.PHONY: integration
$(TEST_BENCH_DLL): $(PLUGINS_OUT) test-plugins $(TEST_BENCH_PROJ) $(TESTS_SRC)
integration: $(TEST_BENCH_DLL)
	dotnet run --project $(TEST_BENCH_PROJ) -- --plugins
	dotnet test OpenCobra/Tests/Integration/IntegrationTests.csproj
