#!/bin/bash

# Exit on error
set -e

if [ -n "$(git status --porcelain)" ]; then
    echo "Working tree must be clean before publishing"
    exit 1
fi

# Clean previous packages
echo "Cleaning previous packages..."
rm -f tool/EasyForNetTool/bin/Release/*.nupkg

# Get latest Git tag for versioning
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "1.0.0")
LAST_TAG=${LAST_TAG#v}
# Increment patch version
MAJOR=$(echo $LAST_TAG | cut -d. -f1)
MINOR=$(echo $LAST_TAG | cut -d. -f2)
PATCH=$(echo $LAST_TAG | cut -d. -f3)
PATCH=$((PATCH + 1))
VERSION="$MAJOR.$MINOR.$PATCH"

# Allow manual version override
read -p "Enter version [$VERSION]: " VERSION_OVERRIDE
VERSION=${VERSION_OVERRIDE:-$VERSION}

# Build and pack with version
echo "Running tests..."
dotnet test tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj -c Release

echo "Building and packing version $VERSION..."
dotnet pack tool/EasyForNetTool/EasyForNetTool.csproj -c Release -p:Version=$VERSION -p:PackageVersion=$VERSION

# Verify package exists
if ! ls tool/EasyForNetTool/bin/Release/*.nupkg 1> /dev/null 2>&1; then
    echo "Package was not created"
    exit 1
fi

# Confirm before publishing
read -p "Publish version $VERSION to NuGet? (y/N): " CONFIRM
if [[ ! "$CONFIRM" =~ ^[Yy]$ ]]; then
    echo "Publishing cancelled"
    exit 0
fi

# Create and push the matching template tag before the package becomes available.
echo "Creating Git tag v$VERSION..."
if git rev-parse "refs/tags/v$VERSION" >/dev/null 2>&1; then
    if [ "$(git rev-parse "refs/tags/v$VERSION^{commit}")" != "$(git rev-parse HEAD)" ]; then
        echo "Git tag v$VERSION already exists and points to a different commit"
        exit 1
    fi
    echo "Reusing Git tag v$VERSION at HEAD..."
else
    git tag "v$VERSION"
fi
git push origin "v$VERSION"

if [ -n "$NUGET_API_KEY" ]; then
    dotnet nuget push tool/EasyForNetTool/bin/Release/*.nupkg --source "https://api.nuget.org/v3/index.json" --api-key "$NUGET_API_KEY"
else
    dotnet nuget push tool/EasyForNetTool/bin/Release/*.nupkg --source "https://api.nuget.org/v3/index.json"
fi

echo "Package published successfully!"
