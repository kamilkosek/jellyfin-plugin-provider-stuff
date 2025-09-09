export VERSION ?= $(shell git describe --tags --abbrev=0 2>/dev/null || echo 1.1.0.0)
export GITHUB_REPO ?= kamilkosek/jellyfin-plugin-provider-stuff
export FILE ?= providerstuff-$(VERSION).zip

print:
	@echo $(VERSION)

zip:
	mkdir -p ./dist
	zip -r -j "./dist/$(FILE)" Jellyfin.Plugin.ProviderStuff/bin/Release/net8.0/Jellyfin.Plugin.ProviderStuff.dll packages/
	cd Jellyfin.Plugin.ProviderStuff/bin/Release/net8.0/ && \
	dirs="$$(find . -type d -not -path '.' -print)"; \
	if [ -n "$$dirs" ]; then zip -ur "$(CURDIR)/dist/$(FILE)" $$dirs; fi

csum:
	md5 ./dist/$(FILE)

create-tag:
	@# Create an annotated tag only if it doesn't already exist locally or remotely
	@if git show-ref --tags --quiet -- "refs/tags/$(VERSION)"; then \
		echo "Tag $(VERSION) already exists locally."; \
	else \
		echo "Creating annotated tag $(VERSION)"; \
		git tag -a "$(VERSION)" -m "Release $(VERSION)"; \
	fi
	@if git ls-remote --tags origin | grep -q "refs/tags/$(VERSION)$"; then \
		echo "Tag $(VERSION) already exists on origin."; \
	else \
		echo "Pushing tag $(VERSION) to origin"; \
		git push origin "$(VERSION)"; \
	fi

create-gh-release:
	gh release create $(VERSION) "./dist/$(FILE)" --generate-notes --verify-tag

update-version:
	VERSION=$(VERSION) node scripts/update-version.js

update-manifest:
	GITHUB_REPO=$(GITHUB_REPO) VERSION=$(VERSION) FILE=$(FILE) node scripts/validate-and-update-manifest.js

build:
	dotnet build Jellyfin.Plugin.ProviderStuff --configuration Release

release: print update-version build zip create-tag create-gh-release update-manifest

# CI-friendly release (assumes tag already exists); avoids (re)creating tags in CI
ci-release: print update-version build zip create-gh-release update-manifest
