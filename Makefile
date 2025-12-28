all: gui
	dotnet build OpenRCT3/OpenRCT3.csproj

.PHONY: install
install: release
# TODO: Bundle the app for this OS

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
	deno task dev

.PHONY: ovl
ovl:
	dotnet build OpenCobra/OVL/OVL.csproj

.PHONY: dumper
dumper:
	dotnet run --project Dumper/Dumper.csproj

.PHONY: test
test:
	deno check clients/desktop/main.ts
	dotnet test
