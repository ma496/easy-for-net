---
name: permissions
description: Add, rename or remove a permission end-to-end across the API and the web app (Allow.cs, the feature's IPermissionDefinitionProvider, the endpoint, allow.ts, auth-urls.ts, nav-items), including gating one on the tenant's plan with RequireFeatures. Use whenever authorization for a screen or endpoint changes.
---

# Adding a permission

A permission touches five places in a fixed order. Missing one leaves either an endpoint that
nobody can call, or a menu entry that 403s. The examples use a hypothetical `Invoices` slice.

Every permission also declares the **scope** it can be exercised in, and a session carries only the
permissions of the scope it is acting in. Get the scope wrong and the permission is simply never
present where it is needed. See *Scopes* below before choosing one, and the `multi-tenancy` skill for
what tenant and platform scope mean at run time.

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
        var invoicesPermissions = context.AddPermission("Invoices", "Invoices");   // Tenant scope
        invoicesPermissions.AddChild(Allow.Invoice_View, "View");
        invoicesPermissions.AddChild(Allow.Invoice_Delete, "Delete");
    }
}
```

`GroupName` is the section heading on the "change role permissions" screen. `Program.cs` registers
every `IPermissionDefinitionProvider` in the assembly by reflection, so there is nothing to register.

A child takes its parent's scope unless it states its own, so the group is the place to declare the
scope once and the child is the place to make an exception (`TenancyPermissionsProvider`):

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
    Group<InvoicesGroup>();
    Permissions(Allow.Invoice_Delete);
}
```

Always the constant, never a string literal. Permissions are the only authorization input: there is
no "tenant only" or "platform only" endpoint attribute — the permission's scope does that job.

## 4. Mirror the constant in the web app

`src/frontend/web/allow.ts` — the map must stay identical to `Allow.cs`:

```ts
export const Allow = {
  Invoice_Delete: 'Invoice.Delete',
} as const
```

`allow.test.ts` pins the tenancy and entitlement permissions against the API's values; extend it when
you add one of those.

## 5. Gate the UI

- Route guard: add an entry to `src/frontend/web/auth-urls.ts`
  (`{ url: '/admin/invoices/list', permissions: [Allow.Invoice_View] }`; `{id}` matches one segment).
  `App.tsx` sends a signed-in caller lacking the permissions to `/unauthorized`; `proxy.ts` only uses
  `isAuthRequired` from the same file to force sign-in. The sidebar, global search and breadcrumbs also
  hide entries whose `authUrls` permissions the caller lacks.
- In-page checks: `const canDelete = isAllowed(authState, [Allow.Invoice_Delete])` with
  `authState = useAppSelector((state) => state.auth)` and `isAllowed` from `@/lib/utils`. Wrap the
  button/link in `{canDelete && …}`. `isAllowed` requires **all** listed permissions.
- Menu/search entries in `nav-items.ts` and `searchable-items.ts` when the permission unlocks a
  new destination.
- Scope routing in `lib/utils/tenant-routing.ts`: a screen under `/admin` is tenant-only by default.
  If its permission is `Both` and the screen should work for a platform account acting in no tenant,
  add its prefix to `platformAccessiblePathPrefixes`; if it is `Platform`-only, add it to
  `platformOnlyPathPrefixes` as well so a caller inside a tenant is not offered it. `isPathAvailable`
  drives the sidebar, search, breadcrumbs and the `App.tsx` redirect.

## Scopes

`PermissionScope` (`Permissions/PermissionScope.cs`) has three values, and `Tenant` is the default,
so a permission declared without one belongs to the tenant tier:

| Scope | Exercisable | Use it for |
|---|---|---|
| `Tenant` | only while acting inside a tenant | operations about one tenant's own data |
| `Platform` | only by a platform account acting in no tenant | operations about the installation itself |
| `Both` | in either scope | an operation that answers about the platform's own data in platform scope and a tenant's inside a tenant |

`SessionGrants` (`Features/Identity/Core/SessionGrants.cs`) narrows the permissions a session is
created with to the scope it acts in — at sign-in, at every refresh and on a tenant switch or
exit — so the scope decides where a permission exists at all:

- a platform account acting in no tenant carries `Platform` + `Both` from its platform roles
  (`Role.TenantId == null`);
- anyone acting inside a tenant carries `Tenant` + `Both` from that tenant's own roles only — a
  platform account included, which enters only a tenant it is a member of and acts there on the roles
  its membership holds, never on its platform roles;
- an ordinary account with no active tenant carries nothing (`SessionGrants.ExercisesPermissions`).

Consequences worth knowing before you choose:

- A `Platform` permission can never be granted through a tenant role. `ChangePermissionsEndpoint`
  refuses it with `ErrorCodes.PlatformPermissionNotGrantable`, and each tenant's system-created
  administrator role is built from `GetPermissionNamesInScope(PermissionScope.Tenant)`.
- A `Tenant` permission can never be granted through a platform role. `ChangePermissionsEndpoint`
  refuses it with `ErrorCodes.TenantPermissionNotGrantable`, and the seeded platform administrator role
  is built from `GetPermissionNamesInScope(PermissionScope.Platform)`.
- `GET /permissions/define` returns only the scope the caller is in (platform scope for a platform
  account acting in no tenant, tenant scope otherwise), so the role-permission tree never offers
  something the caller could not grant.
- Changing an existing permission's scope takes effect on the next startup: the seeder reconciles the
  stored `Permissions.Scope` column from the definitions exactly as it reconciles display names.

## The platform tier is not a permission

`User.IsPlatform` names the tier an account belongs to, never what it may do; authorize on
permissions. Where the tier is read, and why, is listed in the `multi-tenancy` skill. On the client,
`isPlatform` from `/account/get-info` gates UX only — the "enter tenant" action in the tenants table
and the "exit tenant" control in the header switcher.

## Gating a permission on the tenant's plan

A permission may also declare the features that must be enabled for it to be exercisable at all
(`IdentityPermissionsProvider`):

```csharp
var usersPermissions = context.AddPermission("Users", "Users", PermissionScope.Both)
                              .RequireFeatures(FeatureNames.Identity_UserManagement);
usersPermissions.AddChild(Allow.User_View, "View");
```

Declared on a group node it reaches every permission beneath it, cumulatively with anything an
ancestor already requires. `IPermissionFeatureFilter` is the one place the rule is applied, and its
two consumers are `SessionGrants` (the permissions a session is created with) and
`GetDefinePermissionsEndpoint` (the catalogue a role is edited from); `GetInfoEndpoint` (what the web
app gates on) reports the session's own permissions, so it agrees by construction. So a permission
whose feature is off for the acting tenant is not in the session, not offered on any role, and not
reported to the web app — nothing else has to change: `Permissions(Allow.X)` and `isAllowed` already
refuse. Platform scope narrows nothing. A plan change ends the affected tenants' sessions at once, so
their users come back under the new plan at their next sign-in (see the `feature-management` skill).

Two rules the architecture tests in `Tests/Architect/PermissionFeatureDeclarationTests.cs` enforce:

- a `PermissionScope.Platform` permission may **not** require a feature — platform scope is inside no
  plan, so the requirement could never apply;
- every feature a permission names must actually be declared, because a misspelt name would resolve
  to nothing and hide the permission from every tenant, permanently and silently.

The permissions that administer the entitlement system (`Allow.Edition_*`, `Allow.FeatureValue_*`)
are `Platform`-scoped, which is what keeps them ungated — never move them to `Tenant` or `Both`, or a
feature switched off could not be switched back on. See the `feature-management` skill for declaring
features.

## What happens at runtime

`ShareData/DataSeeder.SeedAsync` reconciles the database with the code-declared definitions on **every
startup**: it inserts new permissions, updates changed display names and scopes, deletes permissions
that no longer exist and strips them from every role, then grants the platform-scope set to the
platform `Admin` role and the tenant-scope set to the **bootstrap** tenant's `Admin` role. Every other
tenant's system-created administrator role is filled by `ITenantAuthorizationService.
ProvisionTenantAdministratorRoleAsync`, which runs when the tenant is created and when it gains its
first member — not on startup. So:

- Adding a permission: existing non-admin roles do **not** get it, and neither do the administrator
  roles of tenants created before it existed — an administrator grants it via *Roles → Change
  permissions*, or a test seeds it (`TestsDataSeeder` assigns every permission to its test roles).
- Renaming a permission constant's **value** is a delete + insert: every role loses the old grant.
  Prefer keeping the value and changing only the display name.
- Deleting a permission: remove it from all five places, or the seeder will keep deleting a row the
  provider keeps re-adding.

Permissions are **not** in the JWT or cookie, which carry only the account and `sid`. They are stored
in the caller's session (`Backend.Features.Identity.Core.Sessions.SessionRecord`), read from the
session store on every request and projected onto the request's principal as `permission` claims
(`ClaimConstants.Permission`) by `SessionClaims.Project`, which is what `Permissions(Allow.X)` checks.
A grant change ends the affected sessions at once, through
`Backend.Features.Identity.Core.Sessions.ISessionRevocationService` after the change commits:
`ChangePermissionsEndpoint` and `RoleDeleteEndpoint` end the role holders' sessions in the role's scope,
and `UserUpdateEndpoint` ends a user's sessions in the acting scope when their roles change. The old
access token answers 401 and its refresh is refused. A new endpoint that changes what a role grants or
who holds it must revoke the same way. `DataSeeder`'s startup reconciliation revokes nothing, so a
permission it adds or strips reaches a session only when that session is next replaced. The web app
reads permissions from `/account/get-info` as `roles[].permissions[]` — the session's own — which is
what `isAllowed` checks.

## Checklist

- [ ] Constant in `Permissions/Allow.cs`
- [ ] Child node in the feature's `IPermissionDefinitionProvider`, with the right `PermissionScope`
- [ ] `Permissions(Allow.X)` on every endpoint it guards
- [ ] Same key/value in `src/frontend/web/allow.ts` (and `allow.test.ts` for tenancy/entitlement ones)
- [ ] `auth-urls.ts` entry for any new guarded route
- [ ] `isAllowed(...)` checks around the affected buttons/links
- [ ] `nav-items.ts` / `searchable-items.ts` updated if a destination was added
- [ ] `tenant-routing.ts` prefixes updated if the screen is usable in platform scope
- [ ] Endpoint test asserting the permission is enforced (a role holding only the other permissions)
- [ ] If it is gated on a plan, `.RequireFeatures(...)` on the definition and a test for the disabled case
