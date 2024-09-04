all: desktop
	dub build

.PHONY: install
install: release
# TODO: Bundle the app for this OS

.PHONY: release
release: desktop
	dub build -b release

.PHONY: desktop
desktop: ovl isomorphic
	deno task build:desktop

.PHONY: website
website: ovl
	deno task build:website

.PHONY: isomorphic
isomorphic:
	deno task build:isomorphic

.PHONY: debug
debug: ovl
	deno task dev

.PHONY: ovl
ovl:
	dub build --root=lib/ovl

.PHONY: dumper
dumper:
	dub build --root=lib/ovl/dumper

.PHONY: test
test:
	dub test
	deno check -c clients/desktop/deno.json clients/desktop/main.ts
