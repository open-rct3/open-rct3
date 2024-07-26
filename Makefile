all:
	dub build

.PHONY: install
install: release
# TODO: Bundle the app for this OS

.PHONY: release
release:
	dub build -b release

.PHONY: ovl
ovl:
	dub build --root=lib/ovl

.PHONY: dumper
dumper:
	dub build --root=lib/ovl/dumper
