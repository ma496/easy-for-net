#!/bin/bash

# Exit on error
set -e

CSPROJ=tool/EasyForNetTool/EasyForNetTool.csproj

if [ -n "$(git status --porcelain)" ]; then
    echo "Working tree must be clean before publishing"
    exit 1
fi

BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [ "$BRANCH" = "HEAD" ]; then
    echo "Publish from a branch, not a detached HEAD"
    exit 1
fi

# Clean previous packages
echo "Cleaning previous packages..."
rm -f tool/EasyForNetTool/bin/Release/*.nupkg

# The project file is the version's source of truth; the latest tag only proposes the next one.
CURRENT_VERSION=$(sed -n 's|.*<Version>\([^<]*\)</Version>.*|\1|p' "$CSPROJ" | head -n 1)
if [ -z "$CURRENT_VERSION" ]; then
    echo "No <Version> found in $CSPROJ"
    exit 1
fi

LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "1.0.0")
LAST_TAG=${LAST_TAG#v}
# Increment patch version
MAJOR=$(echo $LAST_TAG | cut -d. -f1)
MINOR=$(echo $LAST_TAG | cut -d. -f2)
PATCH=$(echo $LAST_TAG | cut -d. -f3)
PATCH=$((PATCH + 1))
VERSION="$MAJOR.$MINOR.$PATCH"

# Allow manual version override
echo "Project file version: $CURRENT_VERSION, latest tag: v$LAST_TAG"
read -p "Enter version [$VERSION]: " VERSION_OVERRIDE
VERSION=${VERSION_OVERRIDE:-$VERSION}

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Version must be major.minor.patch, got '$VERSION'"
    exit 1
fi

# A tag that already exists must point at HEAD (a re-run after a failed push), with the version
# already committed there; anywhere else it names another release.
if git rev-parse "refs/tags/v$VERSION" >/dev/null 2>&1; then
    if [ "$(git rev-parse "refs/tags/v$VERSION^{commit}")" != "$(git rev-parse HEAD)" ]; then
        echo "Git tag v$VERSION already exists and points to a different commit"
        exit 1
    fi
    if [ "$CURRENT_VERSION" != "$VERSION" ]; then
        echo "Git tag v$VERSION is at HEAD, but $CSPROJ says $CURRENT_VERSION"
        exit 1
    fi
fi

echo "Running tests..."
dotnet test --project tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj -c Release

# Write the version into the project file, so the package, the tagged source and local builds agree.
if [ "$CURRENT_VERSION" != "$VERSION" ]; then
    echo "Setting $CSPROJ version to $VERSION..."
    # -b keeps the file's CRLF line endings under Git Bash; GNU sed elsewhere ignores it.
    sed -b -i "0,/<Version>[^<]*<\/Version>/s||<Version>$VERSION</Version>|" "$CSPROJ"
fi

echo "Building and packing version $VERSION..."
dotnet pack "$CSPROJ" -c Release

# Verify package exists
if ! ls tool/EasyForNetTool/bin/Release/*.nupkg 1> /dev/null 2>&1; then
    echo "Package was not created"
    exit 1
fi

# Confirm before publishing
read -p "Publish version $VERSION to NuGet? (y/N): " CONFIRM
if [[ ! "$CONFIRM" =~ ^[Yy]$ ]]; then
    git restore -- "$CSPROJ"
    echo "Publishing cancelled"
    exit 0
fi

# Commit the version, tag that commit, and push both before the package becomes available.
if [ -n "$(git status --porcelain -- "$CSPROJ")" ]; then
    echo "Committing version $VERSION..."
    git add -- "$CSPROJ"
    git commit -m "Bump version to $VERSION"
fi

if git rev-parse "refs/tags/v$VERSION" >/dev/null 2>&1; then
    echo "Reusing Git tag v$VERSION at HEAD..."
else
    echo "Creating Git tag v$VERSION..."
    git tag "v$VERSION"
fi
git push --atomic origin "$BRANCH" "v$VERSION"

if [ -n "$NUGET_API_KEY" ]; then
    dotnet nuget push tool/EasyForNetTool/bin/Release/*.nupkg --source "https://api.nuget.org/v3/index.json" --api-key "$NUGET_API_KEY"
else
    dotnet nuget push tool/EasyForNetTool/bin/Release/*.nupkg --source "https://api.nuget.org/v3/index.json"
fi

echo "Package published successfully!"
