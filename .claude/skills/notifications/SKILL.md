---
name: notifications
description: Raise an in-app notification from the API and make it render in the web app — INotificationService, user-targeted vs global notifications, the translation-key contract for title/message, groups and metadata, and the polling badge. Use when a feature needs to tell users something happened.
---

# In-app notifications

## Raising one

Inject `INotificationService` (from `Backend.Features.Notifications.Core`) and call one of two
methods:

```csharp
await notificationService.NewUserNotificationAsync(
    userId,
    NotificationType.Warning,
    titleKey: "notifications.inventoryBelowLimit.title",
    messageKey: "notifications.inventoryBelowLimit.message",
    group: "inventory",
    metadata: "{\"itemName\":\"Widget A\",\"currentQty\":5}",
    cancellationToken);

await notificationService.NewGlobalNotificationAsync(NotificationType.Info,
    "notifications.welcome.title", "notifications.welcome.message", cancellationToken: cancellationToken);
```

`NotificationType` is `Info | Warning | Error | Success` and drives the icon/colour in the UI.

**Cross-feature note:** `INotificationService` lives in the Notifications feature, so another
feature may only call it if the interface is marked `[AllowOutside]` — otherwise
`FeatureDependencyTests` fails. Check before wiring, and mark the interface rather than reaching for
`AppDbContext` directly. See the `backend-feature` skill.

## The translation-key contract

`TitleKey` and `MessageKey` are **i18n keys, not text** — the web app renders them with
`t(notification.titleKey)`. So raising a notification always means adding the matching keys to
`src/frontend/web/public/locales/en.json` (and every other shipped locale):

```json
"notifications": {
  "inventoryBelowLimit": { "title": "Low Inventory Alert", "message": "Item is below the minimum stock level" }
}
```

Never pass user-facing English through `titleKey`/`messageKey`, and never interpolate values into
the key — variable parts go in `Metadata`.

`Group` is a short lowercase slug used to filter the list; add its label under
`notifications.groups.<slug>`. `Metadata` is an opaque JSON string for details the UI may show; keep
it small and stable.

## User-targeted vs global

- `UserId` set → one recipient, read state tracked by the `IsRead` flag on the row.
- `UserId` null → broadcast to everyone; per-user read state lives in `NotificationVisits`
  (unique on `NotificationId` + `UserId`), because one row is shared by all users.

Anything that counts or marks notifications has to handle both branches — `GetUnreadCountAsync`
combines "my unread rows" with "global rows I have not visited", and `NotificationMarkAsReadEndpoint`
either flips `IsRead` or inserts a visit row. Copy that shape rather than inventing a third scheme.

Notifications are `AuditableEntity<Guid>` + `ISoftDelete`, so deleting one soft-deletes it and the
global query filter hides it.

## Endpoints

Under `Features/Notifications/Endpoints/Notifications` with the `notifications` prefix: list
(filters by `isRead` and `group`), get, delete, `{id}/mark-as-read`, `{id}/mark-as-unread`,
`mark-all-as-read`, `unread-count`, `groups`. They are authenticated but carry **no permission** —
every signed-in user sees their own notifications. Follow that when adding one.

## Web side

- `notificationsApi` (`store/api/notifications/notifications`) uses the `Notifications` tag type;
  mutations invalidate the collection plus the touched row. `notificationGetUnreadCount` is
  deliberately untagged because it polls.
- `useNotificationHub()` polls the unread count every 30 s and mirrors it into
  `notificationsSlice.unreadCount`. It is mounted once in the app shell — do not mount it per screen.
- `components/notifications/` holds `NotificationBell` (badge), `NotificationPanel` (dropdown list)
  and `NotificationItem` (single row, renders `t(titleKey)` / `t(messageKey)`), with full pages under
  `app/[lang]/admin/notifications/`.

## Checklist

- [ ] Called `NewUserNotificationAsync` / `NewGlobalNotificationAsync` with key strings, not text
- [ ] `notifications.<name>.title` and `.message` added to every shipped locale
- [ ] `notifications.groups.<slug>` added if a new group was introduced
- [ ] `[AllowOutside]` in place if the caller is in another feature
- [ ] Any new endpoint handles both the user-targeted and the global branch
