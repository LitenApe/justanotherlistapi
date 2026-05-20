#!/bin/bash

set -u

echo "=== Updating NuGet packages ===";

if [ -f "./Directory.Packages.props" ]; then
  echo "Central package management detected: ./Directory.Packages.props"

  updates=$(find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" | while read proj; do
      dotnet list "$proj" package --outdated 2>/dev/null \
        | awk '/^[[:space:]]*>[[:space:]]+[[:alnum:]._-]+[[:space:]]+/ {print $2 " " $5}'
    done | sort -u)

  if [ -z "$updates" ]; then
    echo "No outdated central package versions found"
  else
    while read -r pkg latest; do
      [ -z "$pkg" ] && continue
      [ -z "$latest" ] && continue

      if grep -q "<PackageVersion Include=\"$pkg\"" ./Directory.Packages.props; then
        sed -E -i "s#(<PackageVersion Include=\"$pkg\" Version=\")[^\"]+(\" */>)#\\1$latest\\2#" ./Directory.Packages.props
        echo "   Updated central version: $pkg -> $latest"
      fi
    done <<< "$updates"

    echo "Running restore after central version updates"
    dotnet restore
  fi
else
find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" | while read proj; do
  echo "-> $proj"

  # Extract only package table rows that start with ">" and a valid package ID.
  packages=$(dotnet list "$proj" package --outdated 2>/dev/null \
    | awk '/^[[:space:]]*>[[:space:]]+[[:alnum:]._-]+[[:space:]]+/ {print $2}' \
    | sort -u)

  if [ -z "$packages" ]; then
    echo "   No outdated top-level packages found"
    continue
  fi

  while read -r pkg; do
    [ -z "$pkg" ] && continue

    output=$(dotnet add "$proj" package "$pkg" 2>&1)
    code=$?

    if [ $code -ne 0 ]; then
      if echo "$output" | grep -q "Cannot edit items in imported files"; then
        echo "   Skipping centrally-managed package: $pkg"
      else
        echo "   Failed to update package: $pkg"
        echo "$output"
      fi
    else
      echo "   Updated: $pkg"
    fi
  done <<< "$packages"
done
fi

echo ""
echo "=== Updating global tools ==="
dotnet tool list -g | tail -n +3 | awk '{print $1}' | xargs -I {} dotnet tool update -g {}

echo ""
echo "=== Updating local tools ==="
dotnet tool list | tail -n +3 | awk '{print $1}' | xargs -I {} dotnet tool update {}

echo ""
echo "Done!"
