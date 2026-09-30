Receive notifications over SignalR in the web app and poll only as a fallback
Depends-on: 03-notification-hub-and-publishing

The web app polls `GET /notifications/unread-count` every 30 s from every tab. This task connects `useNotificationHub` to the hub from task 03, applies pushed events to the badge and the list, reconnects with jittered back-off, and keeps polling only as a fallback while disconnected.

## Scope
- Add `@microsoft/signalr`. `useNotificationHub` (still mounted once, in `components/layouts/header.tsx`) opens one connection to `/hubs/notifications` when `canReadNotifications` holds — WebSockets only, negotiation skipped, authenticated by the auth cookie — and stops it on sign-out and when that rule stops holding.
- `notificationReceived`: increment `notificationsSlice.unreadCount` and invalidate the `Notifications` list tag, but **not** the `UNREAD_COUNT` tag. `unreadCountChanged`: set the count.
- The badge shows `99+` above 99.
- `withAutomaticReconnect` with exponential back-off plus random jitter, capped at 60 s. After every connect and reconnect, refetch the unread count once.
- A connection refused with 401 goes through the same refresh path `baseQueryWithReauth` uses (so its mutex still serializes it), then reconnects; when refresh fails the existing sign-out applies.
- A tenant switch or exit stops and restarts the connection, so it joins the new scope's groups.
- Polling: none while connected; every 60 s with jitter while disconnected; none while the tab is hidden, with one refetch when it becomes visible.
- Vitest for the pure pieces: the jittered back-off schedule, the capped badge text, and the reducer handling of `notificationReceived` / `unreadCountChanged`.
- Update the file comment on `use-notification-hub.ts`, the `hooks/` line in `CLAUDE.md` (it says "polling, not a socket") and the same line in `tool/EasyForNetTool/new-project-claude.md`, and the `notifications` skill's web section (connection lifecycle, `99+` badge, fallback polling).

## Out of scope — do not touch
- Every backend file — tasks 01 to 04 own them.
- The notification list's request and response shapes, its paging, and the notification pages' layout beyond the badge text.
- Sharing one connection across tabs, toast pop-ups, Web Push, per-user preferences.
- Sign-in, refresh and tenant-switch flows beyond reconnecting the hub.

## Done when
- `npm run verify` passes, including lint, `npx tsc --noEmit` and the new Vitest tests.
- With the API and web app running (PostgreSQL + Redis), two browsers signed in as members of the same tenant see a tenant-wide notification on the badge within a second, with no `unread-count` request in the network tab while connected.
- A notification raised through a second API instance on another port reaches a browser connected to the first.
- Signing out closes the browser's WebSocket; switching tenant reconnects and the badge reflects the new scope.
- With the API stopped, the client retries with growing, jittered delays and falls back to polling every ~60 s; on restart it reconnects and refetches the count once.
- The badge, notification panel and list page still render correctly in dark mode and right-to-left, and a user without notification permission opens no connection.
