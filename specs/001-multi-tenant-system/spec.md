# Multi-tenant system

## Summary

The application becomes multi-tenant: every piece of business data belongs to exactly one tenant, and
a signed-in person only ever sees the data of the tenant they are currently acting in. One user
account may belong to several tenants and switches between them without signing in again; inside each
tenant their roles and permissions are evaluated independently, so being an administrator of one
tenant grants nothing in another. A platform-administration tier sits above tenants and can create,
rename, suspend, reactivate and delete them, while tenant administrators manage only their own
tenant's members and roles. Isolation is enforced by the system itself rather than by each query
author, so a record created in one tenant cannot be read, listed, exported, counted, updated or
deleted from another — including through identifier guessing, bulk statements, file downloads,
notifications and scheduled background work.

## Actors and permissions

- **Platform administrator** — operates above tenancy. May list every tenant, create tenants, update
  their name and identifier, suspend and reactivate them, delete them, and administer the membership
  of any tenant without being a member. Also the only actor permitted to view platform-wide
  operational surfaces such as the background-job dashboard. Platform-level authority is never
  obtainable through a role granted inside a tenant.
- **Tenant administrator** — a member of one tenant holding that tenant's administration permissions.
  May view their tenant, manage its members and their role assignments, and manage the tenant's roles.
  Has no visibility of any other tenant and cannot change the tenant's own lifecycle status.
- **Tenant member** — an authenticated user acting inside a tenant they belong to. Sees only that
  tenant's data; their effective permissions are the union of the permissions of the roles granted to
  them *in that tenant*.
- **Authenticated user with no usable membership** — may authenticate, use account self-service
  (profile, password change) and create a tenant of their own through self-service onboarding, but is
  refused every other tenant-scoped operation with an explanatory message. Creating a tenant makes
  them its administrator and confers no authority anywhere else.
- **Anonymous visitor** — unchanged: public pages, sign-in, sign-up, password recovery and email
  verification, none of which require a tenant.

Each tenant-management capability above is a separately granted permission, enforced on the API and
mirrored in the web client so that unauthorized screens and navigation entries are not offered.

## User scenarios

1. **Provisioning a tenant.** *Given* a platform administrator signed in, *when* they create a tenant
   with a display name and a unique URL-safe identifier, *then* the tenant is stored as active with a
   system-created administrator role, and it appears in the tenant list with its creator and creation
   time recorded.

2. **Duplicate identifier.** *Given* a tenant already uses the identifier `acme`, *when* a platform
   administrator creates another tenant with the identifier `ACME`, *then* the request is rejected
   with a field-level error against the identifier and a translated message, and no tenant is created.

3. **Adding a member.** *Given* a tenant administrator of Acme and an existing user account,
   *when* they add that account to Acme with the role "Manager", *then* the user gains a membership in
   Acme with that role, and the user's other memberships are unchanged.

4. **Acting inside a tenant.** *Given* a user who belongs to both Acme and Globex, *when* they sign in
   and act in Acme, *then* every list, search and detail screen shows only Acme records, and their
   permissions come only from their Acme roles even though they are an administrator of Globex.

5. **Switching tenants.** *Given* the same user viewing an Acme list, *when* they switch the active
   tenant to Globex, *then* the screens reload with Globex data only, previously cached Acme rows are
   never displayed, and they are not asked to sign in again.

6. **Cross-tenant identifier guessing.** *Given* a member of Globex holding the identifier of an Acme
   record, *when* they request that record, update it or delete it, *then* the system responds exactly
   as it would for a record that does not exist, and no Acme data is disclosed or modified.

7. **Suspension mid-session.** *Given* an Acme member with a live session, *when* a platform
   administrator suspends Acme, *then* the member's next request in the Acme context is refused with a
   defined error code, the user interface explains that the tenant is suspended and offers to switch
   to another tenant, and the user is not silently signed out.

8. **Removing the last administrator.** *Given* Acme has exactly one member holding tenant
   administration, *when* someone attempts to remove that membership or strip that role, *then* the
   request is rejected with a defined error code so the tenant cannot be orphaned.

9. **Same role name in two tenants.** *Given* Acme has a role named "Manager", *when* a Globex
   administrator creates a role named "Manager" in Globex, *then* it is created successfully and the
   two roles remain independent.

10. **Scheduled cleanup.** *Given* recurring maintenance work that removes expired records, *when* it
    runs outside any user request, *then* it processes the tenants it is intended to process by
    explicit instruction rather than silently seeing no rows or every tenant's rows.

11. **Files.** *Given* a Globex member who learns the stored name of an Acme attachment, *when* they
    request it, *then* the download is refused with a defined error code and the file is not served.

12. **Notifications.** *Given* a user belonging to Acme and Globex, *when* an Acme-wide announcement is
    raised, *then* it appears and counts as unread only while the user acts in Acme.

13. **Self-service onboarding.** *Given* a person who has just signed up and belongs to no tenant,
    *when* they create a tenant with a display name and identifier, *then* the tenant is created
    active, they become its administrator, it becomes their active tenant, and they gain no authority
    over any other tenant and no platform authority.

14. **Choosing a tenant at sign-in.** *Given* a user who belongs to both Acme and Globex, *when* they
    sign in, *then* no tenant is active yet and they are asked to choose one before any tenant-scoped
    screen opens.

## Acceptance criteria

### Tenant lifecycle

- **AC-001** The system shall represent each tenant as a persistent record carrying a human-readable
  display name, a unique URL-safe identifier, a lifecycle status and audit fields.
- **AC-002** When a platform administrator creates a tenant with a valid display name and identifier,
  the system shall persist it in the active state and return its assigned identity.
- **AC-003** If a tenant is created or renamed with an identifier that another tenant already uses,
  compared without regard to case and including tenants that have been soft-deleted, then the system
  shall reject the request, return a defined error code attributed to the identifier field, and
  persist nothing.
- **AC-004** If a tenant is created or updated with a display name or identifier that violates the
  declared length or character rules, then the system shall reject the request and return a
  field-level validation failure naming the offending field.
- **AC-005** When a platform administrator updates a tenant's display name or identifier, the system
  shall persist the change and record the updating user and the update time.
- **AC-006** When a platform administrator suspends an active tenant, the system shall set that tenant
  to suspended and retain all of its data unchanged.
- **AC-007** While a tenant is suspended, the system shall refuse every tenant-scoped operation
  performed in that tenant's context and return a defined error code.
- **AC-008** When a platform administrator reactivates a suspended tenant, the system shall restore
  normal access for that tenant's members without further action by them.
- **AC-009** When a platform administrator deletes a tenant, the system shall soft-delete it so that
  the tenant and its data are excluded from every query while remaining retained in storage.
- **AC-010** If a request names a tenant that does not exist or has been deleted, then the system shall
  refuse it with a defined error code and shall not reveal whether that tenant ever existed.
- **AC-011** If a request attempts to rename, suspend or delete the system-created bootstrap tenant,
  then the system shall reject it and return a defined error code.
- **AC-012** The system shall record the creating user and time and the last-updating user and time on
  every tenant record.

- **AC-100** The system shall require a tenant display name of 2 to 100 characters after surrounding
  whitespace is trimmed.
- **AC-101** The system shall require a tenant identifier of 3 to 50 characters composed only of
  lower-case ASCII letters, digits and hyphens, beginning and ending with a letter or digit and never
  carrying two consecutive hyphens.
- **AC-102** When a tenant identifier is persisted, the system shall store the trimmed value as entered
  and maintain a normalized lower-case form beside it, and shall perform every uniqueness comparison,
  lookup and search of AC-003 and AC-064 against that normalized form, exactly as user names and role
  names are normalized today.

- **AC-133** When an authenticated user creates a tenant through self-service onboarding with a valid
  display name and identifier, the system shall create the tenant in the active state, grant the
  creator an active membership in it, assign them the tenant's system-created administrator role, and
  make that tenant their active tenant.
- **AC-134** The system shall apply to self-service tenant creation the same display-name and
  identifier rules, the same duplicate comparison and the same error codes that apply to a tenant
  created by a platform administrator.
- **AC-135** The system shall grant a self-service tenant creator no platform-level permission, so
  that creating a tenant confers authority only inside the tenant created.
- **AC-136** If tenant creation is requested by an unauthenticated caller, then the system shall
  refuse it and return a defined error code, since self-service onboarding requires an existing
  account.
- **AC-137** The system shall permit an authenticated account to create a tenant through self-service
  onboarding irrespective of the memberships it already holds, and shall record the creating user on
  every tenant so created.

### Tenant membership

- **AC-013** Where a user belongs to more than one tenant, the system shall maintain each membership
  independently so that changing one does not affect the others.
- **AC-014** When a tenant administrator adds an existing user account to their tenant, the system
  shall create a membership and grant exactly the tenant roles named in the request.
- **AC-015** If a membership is requested for a user who is already a member of that tenant, then the
  system shall reject the request and return a defined error code.
- **AC-016** If a membership is requested for a user account that does not exist, then the system shall
  reject the request and return a defined error code.
- **AC-017** When a tenant administrator changes a member's role assignments, the system shall replace
  that member's assignments with exactly the roles supplied and record the change.
- **AC-018** When a tenant administrator removes a member, the system shall revoke that membership and
  the member's access to the tenant's data while leaving the user account and its other memberships
  intact.
- **AC-019** If removing a membership or changing its roles would leave a tenant with no member holding
  tenant-administration permission, then the system shall reject the request and return a defined
  error code.
- **AC-020** When a membership is removed or its tenant is suspended, the system shall cause the
  affected member's existing sessions to lose access to that tenant on their next request, without
  requiring a password change and without invalidating their access to other tenants.
- **AC-021** If a caller who is neither a member of the tenant nor a platform administrator attempts to
  read or manage that tenant's membership, then the system shall refuse the request and return a
  defined error code.

- **AC-103** The system shall treat a membership as active while the membership row exists, is not
  soft-deleted, and its tenant is neither suspended nor deleted; the system shall carry no further
  per-tenant membership state, so "active membership" throughout these criteria means exactly that.
- **AC-104** While a user account is globally deactivated, the system shall refuse to authenticate it
  and shall therefore refuse every tenant-scoped operation for it, irrespective of the memberships it
  holds, and shall leave those memberships unchanged.
- **AC-105** When a globally deactivated account is reactivated, the system shall restore its access to
  exactly the memberships it already held, without adding it to or removing it from any tenant.
- **AC-106** When a user who was previously removed from a tenant is added to that tenant again, the
  system shall create a new active membership with exactly the roles named in the request, and shall
  not reject the request on account of the earlier removed membership.
- **AC-107** The system shall exclude removed memberships from the duplicate-membership comparison of
  AC-015, so that only an existing active membership makes a user already a member of that tenant.

### Tenant context and switching

- **AC-022** The system shall establish exactly one active tenant for every authenticated request that
  reaches a tenant-scoped operation.
- **AC-023** If an authenticated request reaches a tenant-scoped operation with no active tenant
  established, then the system shall refuse the operation and return a defined error code rather than
  operating across tenants.
- **AC-024** When a user signs in, the system shall make available the set of tenants in which that
  user holds an active membership.
- **AC-025** When a user selects a different tenant in which they hold an active membership, the system
  shall apply that selection to all subsequent requests without requiring the user to sign in again.
- **AC-026** If a user attempts to act in a tenant in which they hold no active membership, then the
  system shall refuse the request and return a defined error code, regardless of any permission they
  hold in another tenant.
- **AC-027** While a user is acting in a given tenant, the system shall evaluate their effective
  permissions solely from the roles granted to them in that tenant.
- **AC-028** When the active tenant changes, the system shall discard tenant-scoped data cached in the
  browser for the previous tenant so that no record from it is displayed afterwards.
- **AC-029** If a user's active tenant becomes suspended or deleted while they are using it, then the
  system shall keep the user authenticated, refuse tenant-scoped operations with a defined error code,
  and offer them the choice of another tenant they belong to.

- **AC-108** When a user switches the active tenant, the system shall determine their effective
  permissions for the newly active tenant from their membership and role assignments as they stand at
  request time, rather than from authorization established for the previously active tenant.
- **AC-109** If a request carries authorization that was established for a tenant other than the one it
  is acting in, then the system shall refuse the request with a defined error code rather than honour
  the carried roles or permissions.

- **AC-138** The system shall carry the active tenant inside the authenticated session and shall not
  accept it from the client on individual requests, so that a caller cannot change the tenant a
  request acts in by altering that request.
- **AC-139** When a user selects or switches the active tenant, the system shall re-establish their
  session carrying the newly selected tenant while leaving them authenticated, so that no credentials
  are re-entered.
- **AC-140** While a user holds an active membership in more than one tenant, the system shall
  establish no active tenant at sign-in and shall refuse tenant-scoped operations until the user
  selects one.

### Data isolation

- **AC-030** When the system persists a record of a tenant-scoped kind, it shall attribute that record
  to the active tenant without relying on the caller to supply it.
- **AC-031** The system shall restrict every read of tenant-scoped data to the active tenant, including
  detail lookups, lists, searches, counts and exports.
- **AC-032** If a request references a tenant-scoped record that belongs to a different tenant, then the
  system shall respond exactly as it does for a record that does not exist, and shall not modify it.
- **AC-033** The system shall apply the same tenant restriction to bulk create, update and delete
  operations, including those expressed as direct database statements rather than per-record
  operations.
- **AC-034** The system shall apply tenant restriction together with the existing exclusion of
  soft-deleted rows, so that neither restriction disables the other on records subject to both.
- **AC-035** When work runs outside a user request, such as scheduled or queued background work, the
  system shall require the tenant it acts for to be supplied explicitly rather than inferred from a
  request.
- **AC-036** Where an operation must legitimately span all tenants, the system shall require it to opt
  out of the tenant restriction explicitly and by name, leaving the exclusion of soft-deleted rows in
  force, so that an unscoped query is never the result of a missing tenant.
- **AC-037** If a tenant-scoped operation is reached with no tenant established and no explicit
  opt-out, then the system shall fail the operation rather than return unrestricted data.

### Roles and permissions

- **AC-038** The system shall scope roles and role assignments to a single tenant, so that two tenants
  may each define a role with the same name without conflict.
- **AC-039** If a role is created or renamed with a name already used within the same tenant, compared
  without regard to case and including roles that have been deleted from that tenant, then the system
  shall reject the request and return a defined error code.
- **AC-040** The system shall keep the catalogue of permission definitions global and identical for
  every tenant, and shall not allow it to be extended at run time.
- **AC-041** The system shall distinguish platform-level permissions from tenant-level permissions and
  shall not allow a platform-level permission to be granted through a tenant role.
- **AC-042** When a tenant is created, the system shall provision it with a system-created
  administrator role holding every tenant-level permission and assign that role to the tenant's first
  member.
- **AC-043** If a request attempts to delete a system-created tenant role or change its permissions,
  then the system shall reject the request and return a defined error code.
- **AC-044** The system shall enforce a named permission on every tenant-management operation and shall
  mirror those permission names in the web client so that the client gates the same operations.
- **AC-045** If a caller lacks the permission that a tenant-management operation requires, then the
  system shall refuse the request with the standard forbidden response and a defined error code.
- **AC-046** While a caller holds platform administration permission, the system shall allow them to
  list and administer every tenant irrespective of membership.
- **AC-047** If a caller who is not a platform administrator requests a platform-wide operational
  surface such as the background-job dashboard, then the system shall refuse access.

- **AC-110** While a caller is acting in a tenant, the system shall restrict role lists, searches and
  counts to the roles belonging to that tenant.
- **AC-111** If a caller acting in a tenant reads, updates, deletes or changes the permissions of a role
  that belongs to another tenant, then the system shall respond exactly as it does for a role that does
  not exist, and shall not modify it.
- **AC-112** When a caller acting in a tenant creates a role, the system shall attribute that role to
  the active tenant without relying on the caller to supply the tenant.
- **AC-113** While a caller holds platform administration permission, the system shall allow them to
  list, read and administer roles across every tenant irrespective of membership.
- **AC-114** While a caller without platform administration permission requests the
  permission-definition catalogue, the system shall return only tenant-level permissions, so that a
  permission the caller could not grant through a tenant role is never offered on the role-permission
  surface.
- **AC-115** While a caller holds platform administration permission, the system shall return the whole
  permission-definition catalogue, with platform-level permissions distinguishable from tenant-level
  ones.
- **AC-116** When a member's role assignments in a tenant change, the system shall apply the change to
  that member's existing sessions from their next request, without requiring them to sign in again and
  without waiting for their authorization to expire.
- **AC-117** When a tenant role's permissions change or the role is deleted, the system shall apply the
  change to the existing sessions of every member holding that role from their next request.

### Identity and sign-in

- **AC-048** The system shall keep a user account's sign-in identifiers globally unique, so that one
  person authenticates with one account regardless of how many tenants they belong to.
- **AC-049** When a user signs in, the system shall authenticate them without requiring them to name a
  tenant as part of the credentials.
- **AC-050** If an authenticated user holds no membership in any active tenant, then the system shall
  refuse every tenant-scoped operation and present a message explaining that they belong to no active
  tenant.
- **AC-051** The system shall keep account self-service flows — sign-up, email verification, password
  recovery, password change and profile editing, including setting and viewing a profile image —
  usable without an active tenant.
- **AC-093** While a caller is acting in a tenant, the system shall restrict user-account
  administration lists, searches and counts to the accounts holding an active membership in that
  tenant.
- **AC-094** If a caller acting in a tenant reads, updates or deletes a user account that holds no
  membership in that tenant, then the system shall respond exactly as it does for an account that does
  not exist, and shall not modify it.
- **AC-095** While a caller holds platform administration permission, the system shall allow them to
  list, read and administer every user account irrespective of tenant membership.
- **AC-096** When a caller acting in a tenant creates a user account, the system shall grant that
  account an active membership in that tenant.

- **AC-118** When an anonymous visitor completes self-service sign-up, the system shall create a global
  user account that holds no tenant membership and shall join it to no existing tenant; creating a
  tenant is a separate explicit act the new account may perform afterwards under AC-133.
- **AC-119** When self-service sign-up is reached with no tenant context established, the system shall
  complete it normally, since sign-up is one of the account self-service flows AC-051 keeps usable
  without an active tenant.
- **AC-120** The system shall not grant an account created by self-service sign-up any tenant-scoped
  role, any tenant membership or any platform-level permission at the point of sign-up, and shall
  never grant it a platform-level permission through self-service onboarding thereafter.
- **AC-121** The system shall give every role that exists after seeding a declared scope — either the
  tenant it belongs to or platform level — so that no role remains that belongs to no tenant and
  carries no declared platform scope.
- **AC-122** While an account created by self-service sign-up holds no membership in any active tenant,
  the system shall treat it as an authenticated user with no usable membership: account self-service
  and self-service tenant creation stay available, and every other tenant-scoped operation is refused
  as AC-050 requires.

### Notifications

- **AC-052** The system shall attribute every tenant-scoped notification to one tenant and show it only
  to recipients acting in that tenant.
- **AC-053** The system shall support addressing a notification to a single member of a tenant and to
  every member of a tenant.
- **AC-054** The system shall preserve the ability to address a notification to every user of the
  platform and shall keep such notifications distinguishable from tenant-wide ones.
- **AC-055** When a user marks all notifications as read, the system shall mark only the notifications
  visible to them in the active tenant.
- **AC-056** The system shall calculate the unread notification count from the notifications visible in
  the active tenant only.

### File storage

- **AC-057** When a tenant-scoped file is uploaded, the system shall attribute it to the tenant active
  at the time of upload.
- **AC-058** If a user requests, replaces or deletes a stored tenant-scoped file attributed to a tenant
  other than the one they are acting in, then the system shall refuse the request and return a defined
  error code.
- **AC-059** The system shall prevent a known or guessed stored file name from yielding another
  tenant's content.
- **AC-060** While a tenant is suspended or deleted, the system shall stop serving that tenant's stored
  files while retaining them.
- **AC-097** The system shall treat a file that belongs to a user account rather than to a tenant's
  data, such as a profile image, as account-owned, attribute it to no tenant, and allow the account
  that owns it to upload and read it while acting in any tenant or in none.
- **AC-098** If an unauthenticated caller uploads, requests, replaces or deletes a stored file, then
  the system shall refuse the request and return the standard unauthenticated response.
- **AC-099** If an upload of a tenant-scoped file is reached with no active tenant established, then
  the system shall refuse it, return a defined error code, and store nothing.

### Lists, paging, sorting and search

- **AC-061** The system shall return the tenant list and the tenant membership list in pages, honouring
  a caller-supplied page number and page size bounded by a documented maximum.
- **AC-062** The system shall allow those lists to be sorted by an explicitly permitted set of fields
  in either direction.
- **AC-063** If a list request names a sort field that is not among the permitted fields, then the
  system shall reject the request with a validation failure.
- **AC-064** The system shall support free-text search over a tenant's display name and identifier, and
  over a member's username and email address.
- **AC-065** The system shall support filtering the tenant list by lifecycle status.
- **AC-066** While the caller is not a platform administrator, the system shall return in the tenant
  list only the tenants in which that caller holds an active membership.

### Errors and messages

- **AC-067** The system shall define a distinct error code for each tenant failure it can produce —
  unknown tenant, suspended tenant, not a member, no active tenant established, duplicate tenant
  identifier, duplicate membership, removal of the last tenant administrator, protected system-created
  tenant or role, and cross-tenant file access.
- **AC-068** When a tenant operation fails, the system shall return a response carrying the error code
  and, where the failure is attributable to a single input field, the name of that field.
- **AC-069** The system shall display a translated message for every tenant error code the interface
  can receive, and shall never display a raw error code or an untranslated key to a user.
- **AC-070** If a tenant-scoped request fails because the tenant was suspended or the membership was
  revoked during the session, then the system shall explain the reason and offer a way to choose
  another tenant instead of signing the user out without explanation.

### Web experience and localization

- **AC-071** The system shall provide screens to list, create, update, suspend, reactivate and delete
  tenants, each gated by the permission its operation requires.
- **AC-072** The system shall provide a screen to manage a tenant's members and their role assignments
  within that tenant.
- **AC-073** The system shall display the active tenant persistently in the application chrome and,
  where the user belongs to more than one tenant, allow switching from there.
- **AC-074** While a user lacks the permission that a tenant screen requires, the system shall omit its
  navigation and search entries and shall refuse direct navigation to it.
- **AC-075** The system shall render every user-visible string introduced by this feature from a
  translation key, with no hard-coded text.
- **AC-076** The system shall define every translation key introduced by this feature in every locale
  the project ships, so that no locale falls back to a raw key.
- **AC-077** The system shall render the tenant screens correctly in right-to-left locales, with no
  direction-specific spacing or alignment assumptions.

- **AC-123** When a user signs in and holds an active membership in exactly one tenant, the system shall
  make that tenant active without prompting them to choose.
- **AC-124** The system shall preserve the active-tenant selection across page reloads and additional
  browser tabs, and for as long as the user's session itself survives, so the user is not asked to
  choose again during ordinary navigation.
- **AC-125** When a user signs out, the system shall discard the stored active-tenant selection together
  with the tenant-scoped data cached in the browser, so that the next user signing in on the same
  browser inherits no tenant selection and sees no previous tenant's records.
- **AC-126** If an authenticated user holding no usable membership enters the application, then the
  system shall route them to a dedicated screen that explains they belong to no active tenant and
  offers account self-service, self-service tenant creation and sign-out, rather than to any
  tenant-scoped screen.
- **AC-127** If the stored active-tenant selection names a tenant that has since been suspended or
  deleted, or in which the user no longer holds an active membership, then the system shall discard the
  selection and require a new choice instead of continuing to send it.

- **AC-141** The system shall present platform-administration screens inside the same web application
  as tenant screens, each gated by the permission its operation requires and omitted from navigation
  and search entries for callers who lack it.
- **AC-142** When a user holding an active membership in more than one tenant signs in, the system
  shall present a tenant-selection screen and shall not open a tenant-scoped screen until a tenant is
  chosen.
- **AC-143** The system shall offer an authenticated user holding no usable membership a screen from
  which they create their own tenant, reachable from the screen AC-126 routes them to.

### Persistence, audit and concurrency

- **AC-078** The system shall record the creating user and time and the last-updating user and time on
  tenants, memberships and tenant roles.
- **AC-079** The system shall soft-delete tenants and memberships, retaining the rows while excluding
  them from every query.
- **AC-080** If a tenant-scoped record is persisted without a tenant attribution, then the system shall
  reject the write rather than store an unattributed row.
- **AC-081** If two concurrent requests change the same member's role assignments, then the system
  shall persist one complete set of assignments rather than a mixture of the two.

- **AC-144** The system shall enforce the uniqueness comparisons of AC-003 and AC-039 with database
  constraints that cover retained rows, including soft-deleted tenants and deleted tenant roles, so
  that a name freed only by deletion cannot be taken again.

### Bootstrap and seeding

- **AC-082** When the system starts against an empty database, it shall create one system-created
  bootstrap tenant and place the seeded administrator account in it with tenant administration.
- **AC-083** The system shall make the seeded administrator account a platform administrator, distinct
  from and not implied by tenant administration.
- **AC-084** On every start, the system shall reconcile the global permission catalogue without
  deleting tenants, memberships or tenant-scoped roles.
- **AC-085** The system shall be reproducible from a first-time schema creation, so a newly generated
  project obtains the complete tenancy schema without any pre-existing migration history.
- **AC-145** The system shall seed no role intended for automatic grant to self-service sign-ups, so
  that after seeding every role either belongs to a tenant or carries platform scope as AC-121
  requires.

### Test coverage

- **AC-086** The system shall include an integration test for every tenant and membership operation,
  covering its success path and each of its failure branches.
- **AC-087** The system shall include a test proving that a member of one tenant cannot read, list,
  update or delete a record created in another tenant.
- **AC-088** The system shall include a test proving that a caller lacking a required tenant permission
  is refused, using a role created for the test rather than a role that holds every permission.
- **AC-089** The system shall include a test proving that suspending a tenant refuses its members'
  subsequent tenant-scoped requests and that reactivation restores them.
- **AC-090** The system shall include tests that create the tenants, users and records they assert on,
  so that they pass when the suite runs in parallel against a shared database.
- **AC-091** The system shall include an automated architectural check that fails the build when a
  persisted tenant-scoped kind is added without being subject to tenant restriction.
- **AC-092** The system shall include a web test covering permission evaluation for a user acting in a
  tenant, including a user whose permissions differ between two tenants.

- **AC-128** The system shall include a test proving that a member of one tenant cannot read, replace or
  delete a file attributed to another tenant, including by supplying its stored file name, and that an
  account-owned file such as a profile image stays readable by its owner while acting in any tenant and
  in none.
- **AC-129** The system shall include a test proving that a notification raised in one tenant is neither
  listed nor counted as unread while the recipient acts in another tenant, and that a platform-wide
  notification remains visible in both.
- **AC-130** The system shall include a test proving that marking all notifications as read, which is
  executed as a direct database statement, affects only the notifications visible in the active tenant
  and leaves the recipient's notifications in other tenants unread.
- **AC-131** The system shall include a test proving that work running outside a user request fails when
  no tenant is supplied explicitly, rather than processing every tenant's rows or none, and processes
  exactly the tenant supplied when one is given.
- **AC-132** The system shall include a web test proving that changing the active tenant discards the
  tenant-scoped cache, so that no record from the previous tenant is displayed afterwards.

- **AC-146** The system shall include a test proving that self-service tenant creation makes the
  creator an administrator of the tenant created, grants them no platform-level permission, and leaves
  every other tenant unaffected.
- **AC-147** The system shall include a test proving that the identifier of a soft-deleted tenant and
  the name of a deleted tenant role are both refused as duplicates.
- **AC-148** The system shall include a test proving that a request supplying a tenant of its own acts
  in the tenant carried by its session, and that a request whose session names a tenant the caller no
  longer belongs to is refused.
- **AC-149** The system shall include a test proving that a user holding memberships in two tenants
  has no active tenant immediately after signing in, and that selecting one grants access to exactly
  that tenant's data.

## Non-functional

- **Paging.** Tenant and membership lists reuse the existing paging contract: caller-supplied page and
  page size bounded by the current maximum page size of 100, an unpaged mode capped at the existing
  10,000-row ceiling, and a total count returned alongside the page.
- **Query cost.** Tenant restriction is applied as part of the query rather than in memory, and every
  column used for tenant restriction, filtering or sorting is indexed, so a tenant-scoped list does not
  degrade as the number of tenants grows. Uniqueness that becomes per-tenant is enforced by a
  composite database constraint covering retained rows, including soft-deleted ones, rather than by a
  filtered constraint over live rows and rather than by an application-level check alone.
- **Audit.** Tenant, membership and tenant-role rows carry created-by/created-at and
  updated-by/updated-at, populated centrally on save exactly as existing entities are.
- **Soft delete.** Tenants and memberships are soft-deleted and excluded from queries; recovery is an
  operator action against the database. Hard deletion and a retention window are out of scope.
- **Session invalidation.** Suspension of a tenant and revocation of a membership take effect at the
  affected user's next request rather than at token expiry, and require neither a password change nor
  any change to that user's access to other tenants.
- **Concurrency.** Membership and role-assignment updates replace the whole assignment set inside one
  transaction, so a concurrent update cannot leave a member holding a mixture of two requested sets.
- **Error surface.** Every failure produces the existing problem-response shape carrying an error code;
  no new HTTP status is introduced for which the web client has no handling.
- **Tenant addressing.** The active tenant travels inside the authenticated session, not as a value
  the client supplies on each request and not as a segment of the address, so the locale-prefixed
  route tree keeps its existing shape and no request can name a tenant of its own. Switching tenants
  re-establishes the session.
- **Naming and normalization.** A tenant display name is 2 to 100 characters and a tenant identifier
  is 3 to 50 characters of lower-case ASCII letters, digits and non-consecutive interior hyphens. The
  identifier is stored trimmed as entered with a normalized lower-case column beside it, that column is
  indexed and carries the uniqueness constraint, and validators enforce the same bounds on the API and
  in the web forms so a rejection is not the first place a user learns the rule.
- **Authorization freshness.** A change to a member's role assignments, a change to a tenant role's
  permissions and a tenant switch all take effect at the affected user's next request rather than at
  token expiry, by the same mechanism that already revokes a stale session, and without requiring a
  password change or affecting the user's access to other tenants.
- **Genericity.** Everything specified here ships to every newly generated project, so no tenant name,
  domain, deployment topology or customer-specific rule is hard-coded.

## Reuse and existing behaviour

Nothing tenancy-related exists today — a case-insensitive search for tenant/organization/workspace
across `src/` matches only an unrelated tree-view demo and a French word — so all of the following are
existing mechanisms to extend rather than duplicate:

- **Global query filtering** already exists once, in `src/backend/Source/Data/AppDbContext.cs`, where
  `SoftDeleteFilter` installs a single unnamed filter for every `ISoftDelete` type. The project targets
  EF Core 10, which allows several independently named filters on one entity type and lets a query
  suppress one of them by name, so tenant restriction is registered as its own named filter beside the
  soft-delete one rather than fused into it — that is what lets AC-036's opt-out relax tenancy alone
  while AC-034 keeps soft-deleted rows excluded. The same file's `SaveChanges`/`SaveChangesAsync`
  already stamp audit fields and normalized properties; tenant attribution belongs in that existing
  pass.
- **Marker interfaces that drive DbContext behaviour** already exist in
  `src/backend/Source/Data/Entities/Base/` (`ISoftDelete.cs`, `IHasNormalizedProperties.cs`,
  `AuditableEntity.cs`). A non-feature namespace is required for the tenancy marker because
  `src/backend/Tests/Architect/FeatureDependencyTests.cs` fails the build on any cross-feature type
  reference.
- **Ambient per-request context** already exists as `ICurrentUserService`
  (`src/backend/Source/Features/Identity/Core/CurrentUserService.cs`), the one `[AllowOutside]`
  identity abstraction that `AppDbContext` consumes. Its limitation is load-bearing here: it reads the
  HTTP context and therefore yields nothing inside Hangfire jobs, the data seeder and the design-time
  context factory — the reason AC-035 to AC-037 exist.
- **Claims and session invalidation** already exist in `src/backend/Source/Helper.cs`
  (`CreateClaims`, `CreateSessionVersion`) and
  `src/backend/Source/Middleware/SessionValidationMiddleware.cs`, which is the only place a live
  session is revoked today and the natural home for AC-020 and AC-029.
- **Uniqueness constraints that must change** are declared in
  `src/backend/Source/Features/Identity/Core/Entities/Configuration/UserConfiguration.cs` (unique on
  normalized username and email — kept global per AC-048) and `RoleConfiguration.cs` (unique on
  normalized role name — becomes per-tenant per AC-038).
- **Error reporting** already funnels through `src/backend/Source/ErrorHandling/ErrorCodes.cs`,
  `ThrowError`, `src/backend/Source/Processors/ExceptionProcessor.cs` and, on the web,
  `src/frontend/web/lib/utils/api-error-helpers.ts` with `error.server.*` keys in the eight files
  under `src/frontend/web/public/locales/`. `ExceptionProcessor` derives the reported field from the
  last segment of a constraint name, which composite tenant-scoped indexes affect.
- **Listing, paging and sorting** already exist as `ListRequestDto<TId>`/`ListDto<T>` in
  `src/backend/Source/Base/Dto/` and `Process(...)` in
  `src/backend/Source/Extensions/IQueryableExtension.cs`; `UserListEndpoint.cs` is the shape to follow
  and must not be re-invented; it is gated by `Allow.User_View` alone today and searches every account
  in the database with no tenant predicate, which is what AC-093 to AC-095 change.
- **Permission declaration** already exists as `src/backend/Source/Permissions/Allow.cs`, the
  per-feature `IPermissionDefinitionProvider` implementations, the startup reconciliation in
  `src/backend/Source/Data/DataSeeder.cs`, and the mirror at `src/frontend/web/allow.ts` with route
  gating in `auth-urls.ts`, `nav-items.ts` and `searchable-items.ts`.
- **Notifications** already implement a two-scope model (a recipient set, or none meaning everyone) in
  `src/backend/Source/Features/Notifications/`, including a raw SQL statement in
  `NotificationMarkAllAsReadEndpoint.cs` that no query filter reaches — the concrete case behind
  AC-033 and AC-055.
- **File storage** already abstracts a provider behind `IStorageProvider`/`IFileService`
  (`src/backend/Source/Features/FileManagement/Core/`), with `LocalStorageProvider.GetSafePath`
  rejecting any name that is not a bare file name. `FileUploadEndpoint` and `FileGetEndpoint` declare
  no permission and are therefore reachable anonymously today, while `FileDeleteEndpoint` requires
  `Allow.File_Delete`; upload is also the single route behind profile images, since
  `Identity.Core.Entities.User.Image` stores a name produced by it. That is the behaviour AC-057 to
  AC-059 and AC-097 to AC-099 change.
- **Web data access** already runs through the single API definition in
  `src/frontend/web/store/api/_app-api.ts` with its refresh-and-sign-out flow; new tenant endpoints
  attach to it, and AC-028 concerns its cache, not a second client.
- **Test scaffolding** already exists as `src/backend/Tests/AppTestsBase.cs`,
  `src/backend/Tests/SharedContextFixture.cs` and `src/backend/Tests/Seeder/TestsDataSeeder.cs`, whose
  seeded roles currently hold every permission — the reason AC-088 requires a purpose-built role.

## Out of scope

- Physical isolation strategies — a database or schema per tenant, per-tenant connection strings, or
  per-tenant background-job storage. The system stays on one database with a tenant discriminator.
- Billing, subscriptions, plans, per-tenant feature gating, and per-tenant quotas or rate limits.
- Email-based invitations for people who do not yet have an account, including invitation tokens,
  expiry and acceptance screens. Membership is granted to accounts that already exist.
- Tenant-branded public pages, and public sign-up pages addressed per tenant. Self-service creation of
  a tenant by an already-authenticated account is in scope under AC-133.
- Per-tenant branding, theming, custom domains and per-tenant locale defaults.
- Subdomain-based addressing of tenants, and the CORS, cookie-domain and deployment changes it implies.
- Migrating an existing single-tenant deployment's data into tenants; the template targets new
  projects, which start with the bootstrap tenant.
- Cross-tenant reporting, aggregation across tenants, and tenant data export or transfer.
- Hard deletion of a tenant, purge schedules and retention windows.
- Impersonation of a tenant member by a platform administrator.
- A per-tenant audit log beyond the audit columns entities already carry.

## Assumptions

- "Professional multi-tenant" means the mainstream shared-database, shared-schema model with a tenant
  discriminator on every tenant-scoped row, because the application is configured with a single
  database connection and generated projects create their schema from scratch.
- A user account is a global identity: one username, one email, one password, one sign-in. Tenancy is
  expressed by membership rather than by duplicating user rows per tenant. This preserves the existing
  global uniqueness of username and email and the existing account-recovery flows.
- A user may belong to several tenants and switches between them explicitly; exactly one tenant is
  active at a time.
- Roles are tenant-scoped, while the permission catalogue itself stays global and code-declared, since
  it is reconciled from code on every start.
- A platform-administration tier exists above tenants and the seeded administrator account occupies it;
  without such a tier nobody could create the first tenant.
- Existing user, role, notification and file structures gain tenancy rather than being replaced, and
  existing routes and payload shapes stay as they are except where a criterion above changes them.
- Tenant deletion is soft, matching how deletion is already modelled in the codebase.
- The bootstrap tenant and each tenant's default administrator role are protected the same way the
  seeded administrator user and role already are.
- Translation keys introduced here are authored in English and added to all eight shipped locale files.
- Files divide into two kinds: tenant-scoped files, which belong to a tenant's data, and account-owned
  files such as a profile image, which belong to a user account and to no tenant. Account-owned files
  are exempt from tenant attribution, so AC-051 stays true for a user holding no membership.
- User-account administration becomes tenant-scoped: acting inside a tenant, a caller sees and
  administers only that tenant's members, and only a platform administrator sees every account. Routes
  and payload shapes are unchanged; only the rows they return are.
- Creating a user account from inside a tenant grants that account a membership in the active tenant,
  since a tenant administrator would otherwise create an account they immediately cannot see.
- Several named restrictions may apply to the same persisted kind and one of them may be relaxed
  without the others, so tenancy and soft delete stay separately expressed and separately relaxable.
- File upload and download, which require no authentication today, require an authenticated caller once
  files carry tenant attribution.
- Role administration becomes tenant-scoped exactly as user administration does: acting inside a
  tenant, a caller lists and administers only that tenant's roles, and only a platform administrator
  sees roles across tenants. Routes and payload shapes are unchanged; only the rows they return are.
- The permission-definition catalogue stays global and code-declared, but the surface that presents it
  filters by the caller's tier, so a tenant administrator is never offered a platform-level permission
  that AC-041 would refuse on save.
- Membership carries no state of its own beyond existing or being removed: access to a tenant is
  governed by the membership row, the tenant's lifecycle status and the global account-active flag that
  already gates sign-in, so no per-tenant suspend/reactivate is introduced.
- Self-service sign-up creates a global account with no tenant and no role; creating a tenant is a
  separate act that account performs once signed in, after which it administers the tenant it created.
  A person therefore reaches a usable tenant either by creating one or by being added to one by
  somebody who already holds authority there.
- Tenant naming rules follow the conventions already used for user names and role names in this
  codebase — a bounded length, a trimmed stored value and a normalized column that carries the unique
  index — rather than introducing a second normalization style.
- The platform-wide role that self-service sign-up granted before this feature is retired rather than
  kept at platform scope or provisioned inside every tenant, so a new account holds no role at all
  until it creates or joins a tenant.
- The active tenant is carried in the authenticated session rather than in a request value or a URL
  segment, so the route tree is unchanged and switching tenants re-establishes the session.
- Platform administrators use the same web application as tenant users, with platform screens gated by
  platform permissions rather than separated into an area of their own.
- A tenant identifier and a tenant role name are reserved permanently once used: deletion does not
  release them, so the uniqueness constraints cover retained rows and no filtered index is required.
- No target deployment requires physical isolation, so the shared-schema model stands and no
  database-per-tenant path is designed for.
- English is authoritative for the strings introduced here and the remaining shipped locales are
  machine-assisted, so that no locale falls back to a raw key.
- Profile images stay account-owned and outside tenant attribution, so a user holding no membership can
  still set one and an image set while acting in one tenant is readable while acting in another.

## Open questions

None. Every question raised during specification has been answered with the requester; the decisions
are recorded under `## Assumptions` and in the criteria they changed.
