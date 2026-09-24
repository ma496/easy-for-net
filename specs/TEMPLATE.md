Let tenant administrators archive a product instead of deleting it

Archived products stay in the database and in reports, but drop out of the product list and
cannot be ordered. Administrators can see them with a filter and restore them.

## Scope

- `Archived` flag (with the time it was set) on the `Product` entity, its configuration and a
  migration. Existing rows are not archived.
- `POST /products/{id}/archive` and `POST /products/{id}/restore`, each behind a new
  `Tenant`-scoped permission `Products.Archive`, declared under the Products group in the
  feature's permissions provider and mirrored in `allow.ts`.
- The product list endpoint excludes archived products unless the request asks for them;
  `archived` joins the whitelisted filter fields.
- On the web: an Archive / Restore action on each row, gated on the permission, and an
  "Archived" filter in the list's filter panel. Every new string is a translation key in
  every locale file.

## Out of scope — do not touch

- Deleting products — the existing delete endpoint and its behaviour stay as they are.
- Orders: whether an archived product may appear on an existing order is a later spec.

## Done when

- `npm run verify` passes.
- Integration tests cover: archive and restore succeed for a tenant administrator; a caller
  without `Products.Archive` gets 403; archiving a product from another tenant returns 404;
  the list omits archived products by default and includes them when asked.
- The web list shows the action only to callers holding the permission, and the filter
  round-trips through the URL.
