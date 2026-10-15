#!/usr/bin/env bash
# Starts the API for end-to-end tests with rate limiting relaxed.
#
# The Playwright suite drives a whole browser session per test from one IP, which comfortably
# exceeds the default per-minute budget and would produce spurious 429s. Both limits are raised
# here. Throttling itself is still covered by RateLimitingTests, which builds its own host with
# the limiter at its default settings.
set -euo pipefail

export SHOPHUB_AUTH_RATE_LIMIT="${SHOPHUB_AUTH_RATE_LIMIT:-100000}"
export SHOPHUB_GLOBAL_RATE_LIMIT="${SHOPHUB_GLOBAL_RATE_LIMIT:-100000}"

exec "$(dirname "$0")/run-api.sh"