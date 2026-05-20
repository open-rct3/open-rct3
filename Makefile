# OS detection for cross-platform bundling
ifeq ($(OS),Windows_NT)
  PLATFORM := Windows
else
  PLATFORM := $(shell uname -s)
endif

all: release

.PHONY: website
website: ovl
	deno task build:website

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
debug:
	deno task build:plugins
	deno task build:desktop
	dotnet run --project OpenRCT3/OpenRCT3.csproj

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

TESTS_SRC := $(wildcard OpenCobra/Tests/*.cs OpenCobra/Tests/*/*.cs)
# Extract TargetFramework from the csproj
TESTS_TFM := $(shell grep -oEm1 "<TargetFramework>[^<]+" OpenCobra/Tests/Tests.csproj | sed "s/<TargetFramework>//")
TESTS_DLL := OpenCobra/Tests/bin/Debug/$(TESTS_TFM)/Tests.dll

$(TESTS_DLL): $(TESTS_PROJ) $(TESTS_SRC)
	dotnet build OpenCobra/Tests/Tests.csproj

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
test: $(TESTS_DLL)
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
