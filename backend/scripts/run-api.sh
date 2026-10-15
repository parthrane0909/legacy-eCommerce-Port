#!/usr/bin/env bash
#
# Starts the ShopHub API against the configured database.
#
# Configuration precedence (highest first):
#   1. Environment variables already exported by the caller
#   2. backend/.env   (git-ignored; copy from .env.example)
#   3. The development defaults below
#
# Secrets are never committed: .env is git-ignored and .env.example holds placeholders only.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$(dirname "$SCRIPT_DIR")"

# Load .env if present. Existing environment variables win, so CI overrides still work.
if [[ -f "$BACKEND_DIR/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$BACKEND_DIR/.env"
  set +a
fi

export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

# --- Database -------------------------------------------------------------
# Resolution order:
#   1. SHOPHUB_DB_CONNECTION (from .env, or exported by CI)
#   2. the Ecommerce.Api user-secrets store (git-ignored)
# No password is hardcoded in this script.
if [[ -z "${SHOPHUB_DB_CONNECTION:-}" ]]; then
  SHOPHUB_DB_CONNECTION="$(
    dotnet user-secrets --project "$BACKEND_DIR/Ecommerce.Api" list 2>/dev/null |
      sed -n 's/^ConnectionStrings:DefaultConnection = //p'
  )"
fi

if [[ -n "${SHOPHUB_DB_CONNECTION:-}" ]]; then
  export ConnectionStrings__DefaultConnection="$SHOPHUB_DB_CONNECTION"
fi

# --- JWT ------------------------------------------------------------------
export Jwt__Issuer="${SHOPHUB_JWT_ISSUER:-ShopHubApi}"
export Jwt__Audience="${SHOPHUB_JWT_AUDIENCE:-ShopHubClient}"
export Jwt__SigningKey="${SHOPHUB_JWT_SIGNINGKEY:-dev-only-signing-key-change-me-0123456789abcdef}"
export Jwt__AccessTokenMinutes="${SHOPHUB_JWT_ACCESS_MINUTES:-15}"
export Jwt__RefreshTokenDays="${SHOPHUB_JWT_REFRESH_DAYS:-7}"

# --- CORS -----------------------------------------------------------------
export Cors__AllowedOrigins__0="${SHOPHUB_CORS_ORIGIN:-http://localhost:5173}"

# --- Rate limiting --------------------------------------------------------
export RateLimiting__Enabled="${SHOPHUB_RATE_LIMITING:-true}"
export RateLimiting__AuthPermitLimit="${SHOPHUB_AUTH_RATE_LIMIT:-10}"
export RateLimiting__GlobalPermitLimit="${SHOPHUB_GLOBAL_RATE_LIMIT:-300}"

# --- Seeded development accounts -----------------------------------------
# Development-only. The API never uses these in production, and nothing is
# seeded unless Database:AutoMigrate is enabled.
export Seed__AdminEmail="${SHOPHUB_SEED_ADMIN_EMAIL:-admin@shophub.local}"
export Seed__AdminPassword="${SHOPHUB_SEED_ADMIN_PASSWORD:-Admin@12345}"
export Seed__CustomerEmail="${SHOPHUB_SEED_CUSTOMER_EMAIL:-customer@shophub.local}"
export Seed__CustomerPassword="${SHOPHUB_SEED_CUSTOMER_PASSWORD:-Customer@12345}"

# Fail fast with a clear message rather than a cryptic EF Core error.
if [[ -z "${ConnectionStrings__DefaultConnection}" ]]; then
  echo "error: no database connection string configured." >&2
  echo "       copy backend/.env.example to backend/.env and set SHOPHUB_DB_CONNECTION" >&2
  exit 1
fi

cd "$BACKEND_DIR"
exec dotnet run --project Ecommerce.Api --urls "${SHOPHUB_API_URL:-http://localhost:5099}"
