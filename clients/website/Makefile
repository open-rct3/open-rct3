all: build

.PHONY: install
install: release
# TODO: Bundle the app for this OS

.PHONY: release
release: build

.PHONY: start
start:
	npx @web/dev-server --port 8080 _site --root-dir _site --open

build: html css assets

_site:
	@mkdir -p _site

html: _site/index.html _site/play/index.html _site/404.html

# TODO: Use ./blogcfile.ini for global variables
_site/index.html: _site src/index.html src/templates/base.html
	blogc -l -t src/index.html -o _site/index.html -D SITE_TITLE=OpenRCT3

_site/404.html: _site src/templates/page.html src/templates/404.md
	blogc -l -t src/templates/page.html -o _site/404.html -D SITE_TITLE=OpenRCT3 src/templates/404.md

_site/play: _site
	@mkdir -p _site/play

_site/play/index.html: _site/play src/play.html src/templates/base.html
	blogc -l -t src/play.html -o _site/play/index.html -D SITE_TITLE=OpenRCT3 -D=Play

css: _site/site.css _site/game.css

_site/site.css: _site
	npx sass src/css/site.scss _site/site.css -s compressed --embed-source-map

_site/game.css: _site
	npx sass src/css/game.scss _site/game.css -s compressed --embed-source-map

assets: _site/favicon.ico _site/favicon.png _site/logo.png

_site/favicon.ico: src/public/favicon.ico
	cp src/public/favicon.ico _site/favicon.ico

_site/favicon.png: src/public/favicon.png
	cp src/public/favicon.png _site/favicon.png

_site/logo.png: src/public/logo.png
	cp src/public/logo.png _site/logo.png

.PHONY: clean
clean:
	rm -f _site/index.html _site/play/index.html
