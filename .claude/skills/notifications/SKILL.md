---
name: notifications
description: Raise an in-app notification from the API and make it render in the web app — INotificationService, user / tenant-wide / platform-wide addressing and the tenant scope each needs, the translation-key contract for title/message, groups and metadata, and the polling badge. Use when a feature needs to tell users something happened.
---

# In-app notifications

## Raising one

Inject `INotificationService` (from `Backend.Features.Notifications.Core`) and call the method for
the audience:

```csharp
// one account, in the active scope - a tenant, or platform scope
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

**Scope.** Attribution comes from `ITenantContext`, never from an argument, and every method throws
`TenantScopeNotEstablishedException` with no scope at all. `NewUser…` stamps the active scope: inside
a tenant the recipient sees the row only while acting in that tenant, and in platform scope the row
names no tenant and the recipient sees it only while acting in platform scope - this is how a platform
account is told something personally. `NewTenant…` needs an actual tenant and throws
`InvalidOperationException` in platform scope, where its row would read as the platform-wide broadcast.
`NewGlobal…` opens platform scope itself and writes `TenantId = null`. From a request the scope is
already set. To address the account in a tenant other than the one the request acts in (a platform
administrator adding a member from platform scope, say) or from a background job, open it first with
`using (tenantContext.BeginTenant(tenantId)) { ... }` (see `background-jobs`, and `multi-tenancy` for
the model), after saving any other pending work, because attribution is stamped at save time.

**Cross-feature use:** `INotificationService` and `NotificationType` are `[AllowOutside]`, so another
slice injects the service directly (`TenantMemberAddEndpoint` tells an account it joined a
tenant). The entities stay private to the slice - never reach for `dbContext.Notifications` from
outside it. See the `backend-feature` skill.

## The translation-key contract

`TitleKey` and `MessageKey` are **i18n keys, not text** — `NotificationItem` renders them with
`t(notification.titleKey)`. So raising a notification always means adding the matching keys to
`src/backend/Source/Features/Localization/Core/Resources/en.json` (and every other shipped resource
file there):

```json
"notifications": {
  "inventoryBelowLimit": { "title": "Low Inventory Alert", "message": "Item is below the minimum stock level" }
}
```

Never pass user-facing English through `titleKey`/`messageKey`, and never interpolate values into
the key — variable parts go in `Metadata`. The web app interpolates the top-level string and number
values of a JSON-object `Metadata` into the text (`notificationVariables` in `lib/utils`), so a
message `"You have been added to ${tenantName}"` raised with
`JsonSerializer.Serialize(new { tenantName = tenant.Name })` shows the name.

`Group` is a short lowercase slug used to filter the list; add its label under
`notifications.groups.<slug>` (the UI falls back to the raw slug). `Metadata` is an opaque JSON
string for details the UI may show; keep it small and stable.

## Addressing and read state

| Row | `TenantId` | `UserId` | Read state |
| --- | --- | --- | --- |
| user, in a tenant | active tenant | recipient | `IsRead` on the row |
| user, in platform scope | null | recipient | `IsRead` on the row |
| tenant-wide | active tenant | null | one `NotificationVisit` per user |
| platform-wide | null | null | one `NotificationVisit` per user |

`NotificationVisits` is unique on (`NotificationId`, `UserId`), because one audience row is shared by
all its readers.

What a caller sees is "rows of the active scope addressed to them or to nobody, plus the platform-wide
rows" — so a personal row never crosses between a tenant and platform scope, and only the platform-wide
broadcast is seen everywhere. That is the internal `VisibleTo(userId, activeTenantId)` in
`Core/NotificationQueries.cs`, applied over `.AcrossAllTenants()` because the tenant filter alone would
hide the platform-wide rows from a caller acting in a tenant. Anything that reads, counts or marks
notifications goes through it (the one hand-written SQL statement in mark-all-as-read spells the same
rule out for audience rows) and handles both read-state branches:
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
permission** — every signed-in user sees their own notifications. Follow that when adding one. An
ordinary account whose tenant was dropped at token renewal acts in platform scope and is answered
with the platform-wide broadcasts alone; that is harmless, and the web app sends it to
`/select-tenant` rather than polling.

## Web side

- `notificationsApi` (`store/api/notifications/notifications`) uses the `Notifications` tag type;
  mutations invalidate the collection plus the touched row. `notificationGetUnreadCount` provides
  `{ type: 'Notifications', id: 'UNREAD_COUNT' }`, so every mutation refetches the badge at once;
  a notification mutation must keep invalidating the `Notifications` type, or the badge waits for the poll.
- `useNotificationHub()` polls the unread count every 30 s and mirrors it into
  `notificationsSlice.unreadCount`. It skips polling when the caller has no active tenant and is not
  a platform account acting in none (that caller is on `/select-tenant`). It is mounted once, in `components/layouts/header.tsx` — do not
  mount it per screen.
- `components/notifications/` holds `NotificationBell` (badge), `NotificationPanel` (dropdown list)
  and `NotificationItem` (single row, renders `t(titleKey, notificationVariables(metadata))` and the
  same for `messageKey`), with full pages under
  `app/[lang]/admin/notifications/` (`list`, `[id]`).

## Testing

Derive from `NotificationsTestsBase` (it carries `[Collection("Notifications")]`, because
platform-wide rows reach every account) and use its `CreateUserNotificationAsync`,
`CreateTenantNotificationAsync`, `CreatePlatformUserNotificationAsync`, `CreateGlobalNotificationAsync`
and `IsVisitedAsync` helpers. `NotificationPlatformScopeTests` shows asking one platform account from
both sides of the boundary: `ClientForAsync(username)` signs it in to platform scope,
`ClientForAsync(username, tenantId)` to its tenant.

## Checklist

- [ ] Called `NewUserNotificationAsync` / `NewTenantNotificationAsync` / `NewGlobalNotificationAsync` with key strings, not text
- [ ] The scope the recipient will read it in is active: their tenant, or platform scope for a platform account (jobs and cross-tenant callers open one explicitly)
- [ ] `notifications.<name>.title` and `.message` added to every backend resource file
- [ ] `notifications.groups.<slug>` added if a new group was introduced
- [ ] Any new endpoint reads through `AcrossAllTenants().VisibleTo(...)` and handles both read-state branches
