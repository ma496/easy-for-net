---
scope: frontend/web next/font
learned: 2026-09-29
---

# Clear .next/cache when next build fails on next/font/google

If the gate's next build fails with 'next/font/google queries have exactly one entry' / Can't resolve '@vercel/turbopack-next/internal/font/google/font', it is a stale Turbopack cache in src/frontend/web/.next/cache, not the network or the diff. rm -rf src/frontend/web/.next/cache and rerun verify; retrying without clearing fails the same way every time.
