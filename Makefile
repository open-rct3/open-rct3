all: desktop
	dub build

.PHONY: install
install: release
# TODO: Bundle the app for this OS

.PHONY: release
release: desktop
	dub build -b release

.PHONY: desktop
desktop:
	deno task -c clients/deno/deno.json compile

.PHONY: debug
debug:
	deno task -c clients/website/deno.json dev

.PHONY: ovl
ovl:
	dub build --root=lib/ovl

.PHONY: dumper
dumper:
	dub build --root=lib/ovl/dumper

.PHONY: test
test:
	dub test
	deno check -c clients/deno/deno.json clients/deno/main.ts
