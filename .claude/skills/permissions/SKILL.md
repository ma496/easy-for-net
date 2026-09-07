---
name: permissions
description: Add, rename or remove a permission end-to-end across the API and the web app (Allow.cs, the feature's IPermissionDefinitionProvider, the endpoint, allow.ts, auth-urls.ts, nav-items). Use whenever authorization for a screen or endpoint changes.
---

# Adding a permission

A permission touches five files in a fixed order. Missing one leaves either an endpoint that
nobody can call, or a menu entry that 403s.

## 1. Backend constant

`src/backend/Source/Permissions/Allow.cs` — constant name `Entity_Action`, value `"Entity.Action"`:

```csharp
public const string Notification_Delete = "Notification.Delete";
```

## 2. Declare it in the owning feature's provider

`src/backend/Source/Features/<Feature>/Core/<Feature>PermissionsProvider.cs`. The provider builds a
display hierarchy: a parent node per entity, one child per action. Only **leaf** nodes become real
permissions (`GetFlattenedPermissions` ignores parents), so never point `Permissions(...)` at a
parent name.

```csharp
public class IdentityPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Identity";

    public void Define(PermissionDefinitionContext context)
    {
        var usersPermissions = context.AddPermission("Users", "Users");
        usersPermissions.AddChild(Allow.User_View, "View");
        usersPermissions.AddChild(Allow.User_Create, "Create");
    }
}
```

`GroupName` is what the "change role permissions" screen shows as the section heading. A new
feature needs its own provider class; it is discovered by reflection in `Program.cs`, so there is
nothing to register.

## 3. Enforce it on the endpoint

```csharp
public override void Configure()
{
    Delete("{id}");
    Group<NotificationsGroup>();
    Permissions(Allow.Notification_Delete);
}
```

Always the constant, never a string literal.

## 4. Mirror the constant in the web app

`src/frontend/web/allow.ts` — the map must stay identical to `Allow.cs`:

```ts
export const Allow = {
  Notification_Delete: 'Notification.Delete',
} as const
```

## 5. Gate the UI

- Route guard: add an entry to `src/frontend/web/auth-urls.ts` (`{ url: '/admin/notifications/list', permissions: [Allow.Notification_View] }`).
  `proxy.ts` reads this to redirect unauthenticated/unauthorized users; `{id}` is a wildcard segment.
- In-page checks: `const canDelete = isAllowed(authState, [Allow.Notification_Delete])` with
  `authState = useAppSelector((state) => state.auth)` and `isAllowed` from `@/lib/utils`. Wrap the
  button/link in `{canDelete && …}`. `isAllowed` requires **all** listed permissions.
- Menu/search entries in `nav-items.ts` and `searchable-items.ts` when the permission unlocks a
  new destination.

## What happens at runtime

`Data/DataSeeder.SeedAsync` reconciles the database with the code-declared definitions on **every
startup**: it inserts new permissions, updates changed display names, deletes permissions that no
longer exist and strips them from every role, then grants the full set to the `Admin` role. So:

- Adding a permission: existing non-admin roles do **not** get it — an administrator grants it via
  *Roles → Change permissions*, or a test seeds it (`TestsDataSeeder` assigns every permission to
  its test roles).
- Renaming a permission constant's **value** is a delete + insert: every role loses the old grant.
  Prefer keeping the value and changing only the display name.
- Deleting a permission: remove it from all five places, or the seeder will keep deleting a row the
  provider keeps re-adding.

Permissions reach the client as claims (`ClaimConstants.Permission`) inside the JWT/cookie, and are
also returned by `/account/get-info` as `roles[].permissions[]` — which is what `isAllowed` reads.

## Checklist

- [ ] Constant in `Permissions/Allow.cs`
- [ ] Child node in the feature's `IPermissionDefinitionProvider`
- [ ] `Permissions(Allow.X)` on every endpoint it guards
- [ ] Same key/value in `src/frontend/web/allow.ts`
- [ ] `auth-urls.ts` entry for any new guarded route
- [ ] `isAllowed(...)` checks around the affected buttons/links
- [ ] `nav-items.ts` / `searchable-items.ts` updated if a destination was added
- [ ] Endpoint test asserting the permission is enforced
