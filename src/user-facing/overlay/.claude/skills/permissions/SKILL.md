---
name: permissions
description: Add, rename or remove a permission end-to-end across the API and the web app (Allow.cs, the feature's IPermissionDefinitionProvider, the endpoint, allow.ts, auth-urls.ts, nav-items), including how Admin.Access gates the /admin area and how a grant change revokes sessions. Use whenever authorization for a screen or endpoint changes.
---

# Adding a permission

A permission touches five places in a fixed order. Missing one leaves either an endpoint that
nobody can call, or a menu entry that 403s. The examples use a hypothetical `Invoices` slice.

## 1. Backend constant

`src/backend/Source/Permissions/Allow.cs` — constant name `Entity_Action`, value `"Entity.Action"`:

```csharp
public const string Invoice_Delete = "Invoice.Delete";
```

## 2. Declare it in the owning feature's provider

`src/backend/Source/Features/<Feature>/Core/<Feature>PermissionsProvider.cs`. The provider builds a
display hierarchy: a parent node per entity, one child per action. Only **leaf** nodes become real
permissions (`IPermissionDefinitionService.GetFlattenedPermissions` returns leaves only), so never
point `Permissions(...)` at a parent name. `Backend.Permissions` is a global using (`Meta.cs`).

```csharp
namespace Backend.Features.Invoices.Core;

public class InvoicesPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Invoices";

    public void Define(PermissionDefinitionContext context)
    {
        var invoicesPermissions = context.AddPermission("Invoices", "Invoices");
        invoicesPermissions.AddChild(Allow.Invoice_View, "View");
        invoicesPermissions.AddChild(Allow.Invoice_Delete, "Delete");
    }
}
```

`GroupName` is the section heading on the "change role permissions" screen. `Program.cs` registers
every `IPermissionDefinitionProvider` in the assembly by reflection, so there is nothing to register.
Permissions that guard the application as a whole rather than one feature — `Admin.Access` and
`Hangfire.Dashboard` — are declared in `Permissions/AdministrationPermissionsProvider.cs`.

The provider sits beside the slice's `<Feature>SettingsProvider.cs` for run-time settings
(`settings`). A setting needs no permission of its own: every setting is read and edited through
`/settings` under `Settings.View` / `Settings.Update`.

## 3. Enforce it on the endpoint

```csharp
public override void Configure()
{
    Delete("{id}");
    Group<InvoicesGroup>();
    Permissions(Allow.Invoice_Delete);
}
```

Always the constant, never a string literal. Permissions are the only authorization input: there is
no role check and no endpoint attribute beside it. The endpoint names the permission of what it does;
it does not also require `Admin.Access`, which is the web app's gate on the admin area (below). An
endpoint the public site calls without a session is `AllowAnonymous()` and returns only what any
visitor may see.

## 4. Mirror the constant in the web app

`src/frontend/web/allow.ts` — the map must stay identical to `Allow.cs`:

```ts
export const Allow = {
  Invoice_Delete: 'Invoice.Delete',
} as const
```

`allow.test.ts` restates the API's whole catalogue and fails when the two maps differ — add the new
constant there too.

## 5. Gate the UI

- **Route guard:** add an entry to `src/frontend/web/auth-urls.ts`. Admin screens go through the
  `admin(path, ...permissions)` helper, which prefixes the path with `/admin` (`adminPath`) and puts
  `Allow.Admin_Access` in front of the screen's own permissions:
  `admin('/invoices', Allow.Invoice_View)`, `admin('/invoices/{id}/update', Allow.Invoice_Update)`
  (`{id}` matches one segment). An admin path no entry names still needs `Admin.Access`
  (`fallbackPermissions`). The signed-in visitor's own screens are `{ url: accountPath('/…') }` with
  no permission. `proxy.ts` only forces sign-in for `/admin` and `/account` (`isAuthRequired`);
  `AuthGuard` in the admin and account layouts sends a signed-in caller lacking the permissions to
  `/unauthorized` (`decideGuardRedirect`). The public site (`app/[lang]/(site)`) is never gated.
- The sidebar, global search and breadcrumbs hide entries whose `authUrls` permissions the caller
  lacks, so an entry with no `authUrls` match shows to everyone in the admin area.
- **In-page checks:** `const canDelete = isAllowed(authState, [Allow.Invoice_Delete])` with
  `authState = useAppSelector((state) => state.auth)` and `isAllowed` from `@/lib/utils`. Wrap the
  button/link in `{canDelete && …}`. `isAllowed` requires **all** listed permissions. Links into the
  admin area from outside it (the user menu, sign-in's redirect) check `Allow.Admin_Access`.
- Menu/search entries in `nav-items.ts` and `searchable-items.ts` when the permission unlocks a
  new destination, with urls built by `adminPath(...)`.

## Admin.Access

`Admin.Access` is what separates an administrator from an ordinary signed-in visitor: a visitor who
signed up holds the `User` role (`General.SignupRoleName`), which is seeded with no permission, so
the admin area stays closed to them. Grant it to a role together with the screen permissions that
role should use; on its own it opens only the admin dashboard and the screens no entry names.
Giving the `User` role any permission gives it to every visitor who signs up.

## What happens at runtime

`ShareData/DataSeeder.SeedAsync` reconciles the database with the code-declared definitions on **every
startup**: it inserts new permissions, updates changed display names, deletes permissions that no
longer exist and strips them from every role, then grants the whole catalogue to the system-created
`Admin` role (and removes anything it no longer declares). So:

- Adding a permission: the `Admin` role gets it on the next start; other roles do **not** — an
  administrator grants it via *Roles → Change permissions*, or a test seeds it (`TestsDataSeeder`
  assigns every permission to its `Test`, `TestOne` and `TestTwo` roles).
- Renaming a permission constant's **value** is a delete + insert: every role loses the old grant.
  Prefer keeping the value and changing only the display name.
- Deleting a permission: remove it from all five places, or the seeder will keep deleting a row the
  provider keeps re-adding.
- The `Admin` role is system-created: `ChangePermissionsEndpoint` refuses to edit it
  (`systemCreatedRolePermissionsCannotBeChanged`).

Permissions are **not** in the JWT or cookie, which carry only the account and `sid`. They are stored
in the caller's session (`Backend.Features.Identity.Core.Sessions.SessionRecord`, read from the role
grants by `SessionGrants` when the session is created), read from the session store on every request
and projected onto the request's principal as `permission` claims (`ClaimConstants.Permission`) by
`SessionClaims.Project`, which is what `Permissions(Allow.X)` and the Hangfire dashboard filter check.
A grant change ends the affected sessions at once, through
`Backend.Features.Identity.Core.Sessions.ISessionRevocationService` after the change commits:
`ChangePermissionsEndpoint` and `RoleDeleteEndpoint` end every holder's sessions
(`RevokeUsersAsync`), and `UserUpdateEndpoint` ends a user's sessions when their roles change or the
account is deactivated (`RevokeUserAsync`). The old access token answers 401 and its refresh is
refused. A new endpoint that changes what a role grants or who holds it must revoke the same way.
`DataSeeder`'s startup reconciliation revokes nothing, so a permission it adds or strips reaches a
session only when that session is next replaced. The web app reads permissions from
`/account/get-info` as `roles[].permissions[]` — the session's own — which is what `isAllowed`
checks.

## Checklist

- [ ] Constant in `Permissions/Allow.cs`
- [ ] Child node in the feature's `IPermissionDefinitionProvider`
- [ ] `Permissions(Allow.X)` on every endpoint it guards
- [ ] Same key/value in `src/frontend/web/allow.ts` and `allow.test.ts`
- [ ] `auth-urls.ts` entry (through `admin(...)`) for any new guarded admin route, and a case in `auth-urls.test.ts`
- [ ] `isAllowed(...)` checks around the affected buttons/links
- [ ] `nav-items.ts` / `searchable-items.ts` updated if a destination was added
- [ ] Endpoint test asserting the permission is enforced (`TestRoles.LimitedRoleId`, the `plain` account, or a role from `CreateRoleAsync` holding only the other permissions)
