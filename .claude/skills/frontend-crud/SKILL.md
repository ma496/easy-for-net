---
name: frontend-crud
description: Build the standard list/create/update/delete screens for an entity in src/frontend/web — data table with URL-synced paging, sorting, search, filter panel and export, plus Formik + Yup create/update forms. Use when scaffolding CRUD UI for a new entity.
---

# CRUD screens

The users feature is the reference implementation; copy its structure:

```
app/[lang]/admin/(<group>)/<entity>/
  list/page.tsx
  list/_components/<entity>-table.tsx
  list/_components/<entity>-filter-panel.tsx
  list/_components/<entity>-filter-button.tsx
  create/page.tsx
  create/_components/<entity>-create-form.tsx
  update/[id]/page.tsx
  update/[id]/_components/<entity>-update-form.tsx
```

Page components (server) are covered by the `frontend-page` skill; the API slice by
`rtk-query-api`. This skill covers the client components.

## List table

`'use client'`, driven by `useTableUrlState` + `DataTableProvider` (TanStack Table under the hood).

```tsx
const url = useTableUrlState({
  filters: {
    isActive: parseAsStringEnum(['true', 'false'] as const).withOptions({ clearOnDefault: true, history: 'push' }),
    roleId: parseAsString.withOptions({ clearOnDefault: true, history: 'push' }),
  },
})
```

`useTableUrlState` keeps `page`, `size`, `search`, `sort`, `dir` and your filters in the query
string (nuqs) and returns TanStack-shaped adapters. Use:

| From the hook | For |
| --- | --- |
| `url.page`, `url.pageSize`, `url.sortField`, `url.sortDirection`, `url.search` | the query payload |
| `url.pagination` / `url.setPagination`, `url.sorting` / `url.setSorting` | `DataTableProvider` |
| `url.searchInput` / `url.setGlobalFilter` | the toolbar search box (debounced, 500 ms) |
| `url.filters.<key>`, `url.filters.setMany({...})`, `url.filters.clearFilters()` | the filter panel |
| `url.resetPage()` | after applying or clearing filters |

Fetch, then render:

```tsx
const { data, isFetching, error } = useUserListQuery({
  page: url.page,
  pageSize: url.pageSize,
  sortField: url.sortField ?? undefined,
  sortDirection: url.sortDirection === 'desc' ? SortDirection.Desc : SortDirection.Asc,
  search: url.search || undefined,
  isActive: getIsActiveValue(appliedFilters.isActive),
})

if (error) return <div className="flex justify-center items-center"><ApiErrorMessages error={error} /></div>

return (
  <DataTableProvider
    data={data?.items || []}
    rowCount={data?.total || 0}
    columns={columns}
    enableRowSelection={false}
    sorting={url.sorting} setSorting={url.setSorting}
    pagination={url.pagination} setPagination={url.setPagination}
    globalFilter={url.searchInput} setGlobalFilter={url.setGlobalFilter}
    isFetching={isFetching}
  >
    <DataTableToolbar>{/* filter button, create link, export dropdown */}</DataTableToolbar>
    {filtersOpen && <UserFilterPanel />}
    <DataTable />
    <DataTablePagination siblingCount={1} />
  </DataTableProvider>
)
```

Columns are built with `createColumnHelper<UserListDto>()`; the actions column is
`columnHelper.display({ id: 'actions', ... })`. Headers are translation keys
(`t('table.columns.email')`). Set `enableSorting: false` on computed columns — and remember any
sortable column must also be whitelisted in the backend list validator.

Permission gating:

```tsx
const authState = useAppSelector((state) => state.auth)
const canCreate = isAllowed(authState, [Allow.User_Create])
const canUpdate = isAllowed(authState, [Allow.User_Update])
const canDelete = isAllowed(authState, [Allow.User_Delete])
```

Delete uses the shared alert helpers:

```tsx
const result = await confirmDeleteAlert({ title: t('page.users.deleteTitle'), text: t('page.users.deleteConfirm') })
if (!result.isConfirmed) return
const response = await deleteUser({ id })
if (response.error) { apiErrorAlert(response.error); return }
if (response.data?.success) successToast.fire({ text: t('page.users.deleteSuccess') })
else if (response.data?.message) await errorAlert({ text: response.data.message })
```

Export re-fetches with `all: true` through the lazy query, maps rows to a flat object, and calls
`exportData(format, rows, 'Users', 'users')` (`format` is `'excel' | 'csv'`).

## Filter panel

Two components: a `<Entity>FilterButton` for the toolbar (shows the active-filter count) and a
`<Entity>FilterPanel` rendered between the toolbar and the table. The panel is **draft state** —
keep `pendingFilters` in `useState`, sync it from the URL when the panel opens, and only write to
the URL on *Search*:

```tsx
const handleSearch = () => {
  url.filters.setMany({
    isActive: pendingFilters.isActive === '' ? null : pendingFilters.isActive,
    roleId: pendingFilters.roleId || null,
  })
  url.resetPage()
}

const handleClear = () => {
  setPendingFilters({ isActive: '', roleId: '' })
  url.filters.clearFilters()
  url.resetPage()
}
```

`null` clears a filter from the URL. Export the panel's `<Entity>Filters` interface so the table can
type its draft state. Options that come from the API (a role dropdown, for example) are loaded in
the panel with `useRoleListQuery({ all: true })`, guarded by `<Loader />` and `<ApiErrorMessages />`.

## Create / update forms

Formik + Yup, with the schema built from `t` so messages are localized:

```tsx
const createValidationSchema = (t: (key: string, params?: Record<string, string | number>) => string) =>
  Yup.object().shape({
    username: Yup.string()
      .required(t('validation.required'))
      .min(3, t('validation.minLength', { min: 3 })),
    roles: Yup.array().of(Yup.string())
      .required(t('validation.required'))
      .min(1, t('validation.atLeastOneSelected')),
  })

type FormValues = Yup.InferType<ReturnType<typeof createValidationSchema>>
```

Mirror the backend FluentValidation rules (lengths, required, email) so the user sees the error
before the round trip. Then:

```tsx
<Formik<FormValues> initialValues={{ /* ... */ }} validationSchema={validationSchema} onSubmit={onSubmit}>
  {() => (
    <Form noValidate className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <FormInput name="username" label={t('form.label.username')} placeholder={t('form.placeholder.username')} required autoFocus />
      <FormLazyMultiSelect<RoleListDto, RoleListRequest>
        name="roles"
        label={t('form.label.roles')}
        useLazyQuery={useLazyRoleListQuery}
        getLabel={(role) => role.name}
        getValue={(role) => role.id}
        pageSize={20}
        required
      />
      <FormCheckbox name="isActive" label={t('form.label.isActive')} />
      <div className="flex justify-end gap-4 sm:col-span-2">
        <Button type="button" variant="outline" onClick={() => router.push('/admin/users/list')} disabled={isSaving}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" isLoading={isSaving}>{t('common.submit')}</Button>
      </div>
    </Form>
  )}
</Formik>
```

Field components come from `@/components/ui/form`: `FormInput`, `FormPasswordInput`,
`FormTextarea`, `FormSelect`, `FormMultiSelect`, `FormLazySelect`, `FormLazyMultiSelect`,
`FormCheckbox`, `FormRadio`, `FormDatePicker`, `FileUpload`, `MultiFileUpload`. Use the `Form*`
variants inside Formik — the bare ones are for uncontrolled use.

Submit handler:

```tsx
const result = await createUser(payload)
if (result.error) { apiErrorAlert(result.error); return }
successToast.fire({ text: t('page.users.createSuccess') })
router.push('/admin/users/list')          // useLocalizedRouter, unprefixed path
```

The **update** form additionally loads the row and guards the render order:
`isLoading` → `<Loader />`, `error` → `<ApiErrorMessages error={...} />`, no data →
`t('error.server.userNotFound')`, and only then the form, with `initialValues` taken from the
fetched row.

## Don't forget

- Every label, placeholder, column header, toast and confirm string needs a key in
  `public/locales/*.json` — see the `localization` skill; error copy is keyed by backend error code,
  see `api-error-handling`.
- A field or widget the shared library does not have yet belongs in `components/ui` — see the
  `ui-component` skill — not in the route's `_components/`.
- Register list/create/update routes in `auth-urls.ts`, `nav-items.ts`, `searchable-items.ts`.
- `npm run lint` and `npm run build` before calling it done.
