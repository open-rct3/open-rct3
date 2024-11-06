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
website: ovl clients/website/_site/index.html clients/website/_site/play.html
	deno task build:website

SITE_TITLE := OpenRCT3

clients/website/_site/index.html: clients/website/src/index.html
	blogc -l -t clients/website/src/index.html -o $@ -D SITE_TITLE="${SITE_TITLE}"
clients/website/_site/play.html: clients/website/src/templates/base.html clients/website/src/play.html
	blogc -l -t clients/website/src/play.html -o $@ -D SITE_TITLE="${SITE_TITLE}"

.PHONY: serve-website
serve-website: ovl
	deno task dev:website

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
