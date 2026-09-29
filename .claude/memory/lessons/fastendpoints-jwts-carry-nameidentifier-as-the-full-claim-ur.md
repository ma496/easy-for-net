---
scope: token
learned: 2026-09-29
task: 01-redis-session-store-and-per-request-validation
---

# FastEndpoints JWTs carry NameIdentifier as the full claim URI

Tokens minted through FastEndpoints CreateTokenWith/CreateCustomToken write ClaimTypes.NameIdentifier under its full schemas.xmlsoap.org URI, not 'nameid', while a raw 'sid' stays 'sid'. Tests that decode a token payload should use TestsHelper.PayloadOf and assert on those exact keys; session authority lives in ISessionStore, not in the token.
