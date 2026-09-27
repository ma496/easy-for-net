---
name: notifications
description: Raise an in-app notification from the API and make it render in the web app — INotificationService, user / tenant-wide / platform-wide addressing and the tenant scope each needs, the translation-key contract for title/message, groups and metadata, and the polling badge. Use when a feature needs to tell users something happened.
---

# In-app notifications

## Raising one

Inject `INotificationService` (from `Backend.Features.Notifications.Core`) and call the method for
the audience:

```csharp
// one member of the active tenant
await notificationService.NewUserNotificationAsync(
    userId,
    NotificationType.Warning,
    titleKey: "notifications.inventoryBelowLimit.title",
    messageKey: "notifications.inventoryBelowLimit.message",
    group: "inventory",
    metadata: "{\"itemName\":\"Widget A\",\"currentQty\":5}",
    cancellationToken);

// every member of the active tenant
await notificationService.NewTenantNotificationAsync(NotificationType.Info,
    "notifications.planChanged.title", "notifications.planChanged.message", "system", cancellationToken: cancellationToken);

// every user of the platform, whichever tenant they act in
await notificationService.NewGlobalNotificationAsync(NotificationType.Info,
    "notifications.welcome.title", "notifications.welcome.message", cancellationToken: cancellationToken);
```

`NotificationType` is `Info | Warning | Error | Success` and drives the icon/colour in the UI.

**Tenant scope.** Attribution comes from `ITenantContext`, never from an argument. `NewUser…` and
`NewTenant…` stamp the active tenant and throw `InvalidOperationException` in platform scope (and
`TenantScopeNotEstablishedException` with no scope at all); the recipient sees the row only while
acting in that tenant. `NewGlobal…` opens platform scope itself and writes `TenantId = null`. From a
request the scope is already set; from a background job open it first with
`tenantContext.BeginTenant(tenantId)` (see `background-jobs`, and `multi-tenancy` for the model).

**Cross-feature note:** `INotificationService` lives in the Notifications feature and is not marked
`[AllowOutside]`, so calling it from another feature fails `FeatureDependencyTests`. Mark the
interface `[AllowOutside]` (the entities stay private) rather than reaching for `AppDbContext`
directly. See the `backend-feature` skill.

## The translation-key contract

`TitleKey` and `MessageKey` are **i18n keys, not text** — `NotificationItem` renders them with
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
`notifications.groups.<slug>` (the UI falls back to the raw slug). `Metadata` is an opaque JSON
string for details the UI may show; keep it small and stable.

## Addressing and read state

| Row | `TenantId` | `UserId` | Read state |
| --- | --- | --- | --- |
| user | active tenant | recipient | `IsRead` on the row |
| tenant-wide | active tenant | null | one `NotificationVisit` per user |
| platform-wide | null | null | one `NotificationVisit` per user |

`NotificationVisits` is unique on (`NotificationId`, `UserId`), because one audience row is shared by
all its readers.

What a caller sees is "rows of the active tenant, or of no tenant, addressed to them or to nobody" —
the internal `VisibleTo(userId, activeTenantId)` in `Core/NotificationQueries.cs`, applied over
`.AcrossAllTenants()` because the tenant filter alone would hide the platform-wide rows. Anything
that reads, counts or marks notifications uses that shape and handles both read-state branches:
`GetUnreadCountAsync` combines "my unread rows" with "audience rows I have not visited", and
`NotificationMarkAsReadEndpoint` either flips `IsRead` or inserts a visit row. Copy it rather than
inventing a third scheme.

Notifications are `AuditableEntity<Guid>` + `ISoftDelete` + `IMayHaveTenant`; deleting soft-deletes
and the global query filter hides the row. The delete endpoint removes only the caller's own
user-targeted rows — an audience row cannot be deleted by one reader.

## Endpoints

Under `Features/Notifications/Endpoints/Notifications` with the `notifications` prefix: list
(`GET ""`, filters `isRead` and `group`), `GET {id}`, `DELETE {id}`, `POST {id}/mark-as-read`,
`POST {id}/mark-as-unread`, `POST mark-all-as-read`, `GET unread-count`, `GET groups`. There is no
create endpoint — notifications are raised by server code. They are authenticated but carry **no
permission** — every signed-in user sees their own notifications. Follow that when adding one.

## Web side

- `notificationsApi` (`store/api/notifications/notifications`) uses the `Notifications` tag type;
  mutations invalidate the collection plus the touched row. `notificationGetUnreadCount` is
  deliberately untagged because it polls.
- `useNotificationHub()` polls the unread count every 30 s and mirrors it into
  `notificationsSlice.unreadCount`. It skips polling when the caller has no active tenant and is not
  a platform account acting in none. It is mounted once, in `components/layouts/header.tsx` — do not
  mount it per screen.
- `components/notifications/` holds `NotificationBell` (badge), `NotificationPanel` (dropdown list)
  and `NotificationItem` (single row, renders `t(titleKey)` / `t(messageKey)`), with full pages under
  `app/[lang]/admin/notifications/` (`list`, `[id]`).

## Testing

Derive from `NotificationsTestsBase` (it carries `[Collection("Notifications")]`, because
platform-wide rows reach every account) and use its `CreateUserNotificationAsync`,
`CreateTenantNotificationAsync`, `CreateGlobalNotificationAsync` and `IsVisitedAsync` helpers.

## Checklist

- [ ] Called `NewUserNotificationAsync` / `NewTenantNotificationAsync` / `NewGlobalNotificationAsync` with key strings, not text
- [ ] A tenant scope is active for user and tenant-wide notifications (jobs open one explicitly)
- [ ] `notifications.<name>.title` and `.message` added to every shipped locale
- [ ] `notifications.groups.<slug>` added if a new group was introduced
- [ ] `[AllowOutside]` in place if the caller is in another feature
- [ ] Any new endpoint reads through `AcrossAllTenants().VisibleTo(...)` and handles both read-state branches
