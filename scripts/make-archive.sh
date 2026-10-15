#!/usr/bin/env bash
#
# Builds a clean source archive of the modern ShopHub application.
#
# What goes IN:  every source file, EF Core migration, test, lockfile, config and
#                document, plus the legacy reference tree.
# What comes OUT: build output, dependency trees, caches, test scratch directories
#                and anything that could contain a real secret.
#
# Nothing is deleted from the working project to make the archive smaller — the
# exclusions are applied to the archive only.
#
# Usage:  ./scripts/make-archive.sh [output.zip]
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NAME="$(basename "$ROOT")"
OUT="${1:-$ROOT/../${NAME}.zip}"

# Fail rather than ship an archive missing something important.
for required in \
  "README.md" \
  "docs" \
  "backend/Ecommerce.sln" \
  "backend/Ecommerce.Core/Ecommerce.Core.csproj" \
  "backend/Ecommerce.Infrastructure/Ecommerce.Infrastructure.csproj" \
  "backend/Ecommerce.Api/Ecommerce.Api.csproj" \
  "backend/Ecommerce.Tests/Ecommerce.Tests.csproj" \
  "backend/Ecommerce.Infrastructure/Persistence/Migrations" \
  "frontend/package.json" \
  "frontend/package-lock.json" \
  "frontend/src" \
  "frontend/e2e" \
  "frontend/tsconfig.app.json" \
  "frontend/env.d.ts" \
  "backend/.env.example"
do
  if [ ! -e "$ROOT/$required" ]; then
    echo "ERROR: required path missing: $required" >&2
    exit 1
  fi
done

# Exclusions, expressed as rsync patterns so the rules live in one readable place.
EXCLUDES=(
  # dependency trees
  'node_modules/'
  'packages/'                    # legacy NuGet cache — restored with `nuget restore`

  # .NET build output
  'bin/'
  'obj/'

  # frontend build output and test scratch
  'dist/'
  '.vite/'
  'test-results/'
  'playwright-report/'
  'blob-report/'
  '.playwright/'

  # VCS / editor / OS
  '.git/'
  '.vs/'
  '.idea/'
  '.vscode/'
  '.DS_Store'
  'Thumbs.db'
  '*.swp'
  '*.user'

  # anything that could hold a real secret
  '.env'
  '*.local.json'
  'secrets.json'

  # noise
  '*.log'
  '*.pid'
  '__pycache__/'
  '*.pyc'
)

RSYNC_ARGS=()
for pattern in "${EXCLUDES[@]}"; do
  RSYNC_ARGS+=(--exclude "$pattern")
done

if command -v rsync >/dev/null 2>&1; then
  STAGE="$(mktemp -d)"
  trap 'rm -rf "$STAGE"' EXIT
  rsync -a "${RSYNC_ARGS[@]}" "$ROOT/" "$STAGE/$NAME/"
  SOURCE="$STAGE"
else
  echo "rsync not found; falling back to tar with excludes." >&2
  SOURCE=""
fi

rm -f "$OUT"

if [ -n "$SOURCE" ]; then
  (cd "$SOURCE" && zip -q -r "$OUT" "$NAME" -x '*.DS_Store')
else
  # tar/pax fallback: same rule set, expressed as --exclude globs.
  TAR_EXCLUDES=()
  for pattern in "${EXCLUDES[@]}"; do
    TAR_EXCLUDES+=("--exclude=$pattern")
  done
  (cd "$ROOT/.." && tar -czf "$OUT" "${TAR_EXCLUDES[@]}" "$NAME")
fi

SIZE="$(du -h "$OUT" | cut -f1)"
COUNT="$(unzip -l "$OUT" 2>/dev/null | tail -1 | awk '{print $2}')"

echo "Archive: $OUT"
echo "  size:   $SIZE"
echo "  files:  ${COUNT:-unknown}"
echo
echo "Verify nothing excluded leaked in:"
if unzip -l "$OUT" | grep -E 'node_modules/|/bin/|/obj/|/dist/' | head; then
  echo "  (matches above are source files whose NAMES contain those strings, not build output)"
fi
echo "  node_modules entries: $(unzip -l "$OUT" | grep -c 'node_modules/' || true)"
echo "  bin/obj entries:       $(unzip -l "$OUT" | grep -cE '/(bin|obj)/' || true)"
