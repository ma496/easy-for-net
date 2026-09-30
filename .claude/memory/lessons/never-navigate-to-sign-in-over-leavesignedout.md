---
scope: sign-out
learned: 2026-09-30
task: 05-web-realtime-notifications
---

# Never navigate to sign-in over leaveSignedOut

Sign-out ends the session server-side and closes its hub connections at once, so any client code that reacts to a lost connection or a 401 during sign-out finds no session to refresh and would redirect to /signin?redirect=<old page> over leaveSignedOut's own navigation. Route every sign-in redirect through signinRedirectAfterFailedRefresh and skip background fetches while isLeavingSignedOut() (store/signed-out-navigation.ts).
