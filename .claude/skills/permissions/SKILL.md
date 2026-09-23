---
name: permissions
description: Add, rename or remove a permission end-to-end across the API and the web app (Allow.cs, the feature's IPermissionDefinitionProvider, the endpoint, allow.ts, auth-urls.ts, nav-items), including gating one on the tenant's plan with RequireFeatures. Use whenever authorization for a screen or endpoint changes.
---

# Adding a permission

A permission touches five files in a fixed order. Missing one leaves either an endpoint that
nobody can call, or a menu entry that 403s.

Every permission also declares the **scope** it can be exercised in, and a session carries only the
permissions of the scope it is acting in. Get the scope wrong and the permission is simply never
present where it is needed. See *Scopes* below before choosing one.

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
        var usersPermissions = context.AddPermission("Users", "Users", PermissionScope.Both);
        usersPermissions.AddChild(Allow.User_View, "View");
        usersPermissions.AddChild(Allow.User_Create, "Create");
    }
}
```

`GroupName` is what the "change role permissions" screen shows as the section heading. A new
feature needs its own provider class; it is discovered by reflection in `Program.cs`, so there is
nothing to register.

A child takes its parent's scope unless it states its own, so the group is the place to declare the
scope once and the child is the place to make an exception:

```csharp
var tenantsPermissions = context.AddPermission("Tenants", "Tenants", PermissionScope.Platform);
tenantsPermissions.AddChild(Allow.Tenant_Create, "Create");                             // Platform
tenantsPermissions.AddChild(Allow.Tenant_Detail, "Detail", PermissionScope.Both);       // exception
```

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

## Scopes

`PermissionScope` (`Permissions/PermissionScope.cs`) has three values, and `Tenant` is the default,
so a permission declared without one belongs to the tenant tier:

| Scope | Exercisable | Use it for |
|---|---|---|
| `Tenant` | only while acting inside a tenant | operations about one tenant's own data |
| `Platform` | only by a platform account acting in no tenant | operations about the installation itself |
| `Both` | in either scope | an operation that answers about the platform's own data in platform scope and a tenant's inside a tenant |

`SessionGrants` narrows the permission claims a session is minted with to the scope it acts in - at
sign-in, at every token renewal and on a tenant switch - so the scope decides where a permission
exists at all:

- a platform account acting in no tenant carries `Platform` + `Both`;
- anyone acting inside a tenant carries `Tenant` + `Both` from that tenant's own roles only - a
  platform account included, which enters only a tenant it is a member of and acts there on the roles
  its membership holds, never on its platform roles;
- an ordinary account with no active tenant carries nothing.

Consequences worth knowing before you choose:

- A `Platform` permission can never be granted through a tenant role. `ChangePermissionsEndpoint`
  refuses it with `ErrorCodes.PlatformPermissionNotGrantable`, and each tenant's system-created
  administrator role is built from `GetPermissionNamesInScope(PermissionScope.Tenant)`.
- A `Tenant` permission can never be granted through a platform role, because a platform role counts
  only in platform scope. `ChangePermissionsEndpoint` refuses it with
  `ErrorCodes.TenantPermissionNotGrantable`, and the seeded platform administrator role is built from
  `GetPermissionNamesInScope(PermissionScope.Platform)`.
- `GET /permissions/define` returns only the scope the caller is in, so the role-permission tree
  never offers something the caller could not grant.
- Changing an existing permission's scope takes effect on the next startup: the seeder reconciles the
  stored `Permissions.Scope` column from the definitions exactly as it reconciles display names.

## The platform tier is not a permission

`User.IsPlatform` is a column on the account. It names the tier the account belongs to and nothing it
may do - what it may do is decided, as for every account, by its roles narrowed to the scope it is
acting in. Authorize on permissions; read the tier only where the tier itself is the question:

- `POST /account/token` - a platform account naming no tenant signs in with none, where an ordinary one is asked to name one;
- `HangfireAuthorizationFilter` - the background-job dashboard;
- `POST /tenants/exit` - leaving one again (a `Platform` permission could not work here: inside a
  tenant the session carries the tenant scope alone);
- `POST /users` - an account created in platform scope is a platform account, one created inside a
  tenant is not;
- `GET /permissions/define` and `GET /account/get-info` - which scope to answer for.

It reaches a request as the `is_platform` claim, written beside the role and permission claims when
the session is minted, and reaches the web app as `isPlatform` on the account-info response.
On the client it gates UX, never authorization: the "enter tenant" action in the tenants table (shown
only on tenants the account is a member of) and the "exit tenant" control in the header switcher.

Entering a tenant is not on that list. `POST /tenants/switch`, a sign-in naming a tenant and a token
refresh all require a live membership of the tenant whatever the tier, so a platform account works
inside a tenant only once it has been added as a member - from platform scope, through
`POST /tenants/{id}/members`.

## Gating a permission on the tenant's plan

A permission may also declare the features that must be enabled for it to be exercisable at all:

```csharp
var filesPermission = context.AddPermission("Files", "Files", PermissionScope.Both)
                             .RequireFeatures(FeatureNames.FileManagement_Enabled);
filesPermission.AddChild(Allow.File_Delete, "Delete");
```

Declared on a group node it reaches every permission beneath it, cumulatively with anything an
ancestor already requires. A permission whose feature is off for the acting tenant is not minted into
the session, is not offered on any role's permission surface, and is not reported to the web app - so
nothing else has to change: the endpoint's own `Permissions(Allow.X)` and the client's `isAllowed`
already refuse. The change takes effect at the caller's next session renewal, exactly as a permission
change does.

Two rules an architecture test enforces:

- a `PermissionScope.Platform` permission may **not** require a feature. Platform scope is inside no
  tenant and therefore inside no plan, so the requirement could never apply and would read as though
  it did;
- every feature a permission names must actually be declared, because a misspelt name would resolve
  to nothing and hide the permission from every tenant, permanently and silently.

And one rule nothing can enforce for you: **never gate the permissions that administer the
entitlement system itself** (`Allow.Edition_*`, `Allow.FeatureValue_*`), or a feature switched off
could not be switched back on. See the `feature-management` skill for declaring features.

## What happens at runtime

`ShareData/DataSeeder.SeedAsync` reconciles the database with the code-declared definitions on **every
startup**: it inserts new permissions, updates changed display names and scopes, deletes permissions
that no longer exist and strips them from every role, then grants the full set to the platform
`Admin` role and the tenant-scope subset to each tenant's own `Admin` role. So:

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
- [ ] Child node in the feature's `IPermissionDefinitionProvider`, with the right `PermissionScope`
- [ ] `Permissions(Allow.X)` on every endpoint it guards
- [ ] Same key/value in `src/frontend/web/allow.ts`
- [ ] `auth-urls.ts` entry for any new guarded route
- [ ] `isAllowed(...)` checks around the affected buttons/links
- [ ] `nav-items.ts` / `searchable-items.ts` updated if a destination was added
- [ ] Endpoint test asserting the permission is enforced
- [ ] If it is gated on a plan, `.RequireFeatures(...)` on the definition and a test for the disabled case
