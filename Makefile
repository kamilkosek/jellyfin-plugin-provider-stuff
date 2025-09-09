export VERSION ?= $(shell git describe --tags --abbrev=0 2>/dev/null || echo 1.1.0.0)
export GITHUB_REPO ?= kamilkosek/jellyfin-plugin-provider-stuff
export FILE ?= providerstuff-$(VERSION).zip

print:
	@echo $(VERSION)

zip:
	mkdir -p ./dist
	@if [ -f "./dist/$(FILE)" ]; then rm "./dist/$(FILE)"; fi
	zip -r -j "./dist/$(FILE)" Jellyfin.Plugin.ProviderStuff/bin/Release/net8.0/Jellyfin.Plugin.ProviderStuff.dll
	@if [ -d "packages" ]; then zip -ur "./dist/$(FILE)" packages/; fi
	cd Jellyfin.Plugin.ProviderStuff/bin/Release/net8.0/ && \
	dirs="$$(find . -type d -not -path '.' -print)"; \
	if [ -n "$$dirs" ]; then zip -ur "$(CURDIR)/dist/$(FILE)" $$dirs; fi

csum:
	md5 ./dist/$(FILE)

create-tag:
	@if git rev-parse "$(VERSION)" >/dev/null 2>&1; then \
		echo "Tag $(VERSION) already exists, skipping tag creation"; \
	else \
		echo "Creating tag $(VERSION)"; \
		git tag $(VERSION); \
		git push origin $(VERSION); \
	fi

create-gh-release:
	@if gh release view "$(VERSION)" >/dev/null 2>&1; then \
		echo "Release $(VERSION) already exists, updating asset"; \
		gh release upload "$(VERSION)" "./dist/$(FILE)" --clobber; \
	else \
		echo "Creating release $(VERSION)"; \
		gh release create $(VERSION) "./dist/$(FILE)" --generate-notes --verify-tag; \
	fi

update-version:
	VERSION=$(VERSION) node scripts/update-version.js

update-manifest:
	GITHUB_REPO=$(GITHUB_REPO) VERSION=$(VERSION) FILE=$(FILE) node scripts/validate-and-update-manifest.js

build:
	dotnet build Jellyfin.Plugin.ProviderStuff --configuration Release

release: print update-version build zip create-tag create-gh-release update-manifest
