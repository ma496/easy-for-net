---
name: ui-component
description: Build or extend a shared React component in src/frontend/web/components — which folder it belongs in, the cva variant + cn pattern, forwardRef, Formik-aware form fields, icon-only controls with tooltips, portaled menus, barrel exports, and the dark-mode/RTL class rules. Use when adding a reusable component, a new form field, or a variant to an existing one.
---

# Shared components

## Which folder

| Folder | For | Examples |
| --- | --- | --- |
| `components/ui/` | Generic, app-agnostic primitives | `Button`, `IconButton`, `Badge`, `Card`, `Modal`, `Dropdown`, `Loader`, `Tabs`, `Tooltip`, `TreeView`, `Truncated`, `DateView`, `PriceView`, `Breadcrumbs`, `LocalizedLink`, `ApiErrorMessages` |
| `components/ui/form/` | Inputs — a bare one and, for most, a Formik-bound `Form*` twin | `Input`/`FormInput`, `Select`/`FormSelect`, `MultiSelect`/`FormMultiSelect`, `Checkbox`/`FormCheckbox`, `DatePicker`/`FormDatePicker`, `FormLazySelect`, `FormLazyMultiSelect`, `FileUpload`, `MultiFileUpload` |
| `components/ui/data-table/` | The table system | `DataTableProvider`, `DataTable`, `DataTableToolbar`, `DataTableToolbarButton`, `DataTableFilterButton`, `DataTableExportButton`, `DataTableRowActions`, `DataTablePagination`, `DataTableSortIcon`, `DataTableCheckboxCell` |
| `components/layouts/` | App shell and page chrome | `AdminPageContent`, `Sidebar`, `Header`, `Footer`, `MainContainer`, `SearchComponent`, `ServiceUnavailableView`, `ProviderComponent`, `TranslationProvider` |
| `components/custom/` | App-specific composites tied to domain concepts | `NavUser`, `TenantSwitcher`, `LanguageDropdown`, `ThemeChanger`, `ImagePreview`, `BackLink`, `CookieConsentDialog` |
| `components/notifications/` | The notification bell/panel/item trio | `NotificationBell`, `NotificationPanel`, `NotificationItem` |

Screen-specific pieces do **not** go here — they live in the route's `_components/` folder, or the
route group's `_components/` when several screens of one feature share them. Promote a component to
`components/` only once a second feature needs it.

## Anatomy

One component per kebab-case file, named export, JSDoc above it, props interface named
`<Component>Props` (or `I<Component>Props` where that is already the local habit, as `IBadgeProps`)
declared directly above. Add the export to the folder's barrel — consumers import from
`@/components/ui`, `@/components/ui/form`, `@/components/ui/data-table`, `@/components/layouts`,
`@/components/custom`, `@/components/notifications`, never from the deep path.

**Variants use `class-variance-authority` + `cn`** (trimmed from `button.tsx`):

```tsx
import React, { ButtonHTMLAttributes } from 'react'
import { VariantProps, cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import { Loader2 } from 'lucide-react'

const buttonVariants = cva('btn', {
  variants: {
    variant: { default: 'btn-primary', primary: 'btn-primary', outline: 'btn-secondary', secondary: 'btn-secondary', soft: 'btn-soft', ghost: 'btn-ghost', danger: 'btn-danger' },
    size: { default: "[&_svg:not([class*='size-'])]:size-4", sm: "btn-sm [&_svg:not([class*='size-'])]:size-3.5", lg: "btn-lg [&_svg:not([class*='size-'])]:size-5" },
    rounded: { default: '', full: 'rounded-full' },
  },
  defaultVariants: { variant: 'default', size: 'default', rounded: 'default' },
})

/** Props for the Button component… */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  isLoading?: boolean
  icon?: React.ReactNode
}

/** Button is a styled native button with variant/size/rounded options, a loading spinner and an optional leading icon. */
const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(({ className, variant, size, rounded, isLoading, disabled, children, icon, ...props }, ref) => (
  <button className={cn(buttonVariants({ variant, size, rounded }), className)} ref={ref} disabled={disabled || isLoading} {...props}>
    {isLoading && <Loader2 className="animate-spin" />}
    {icon && !isLoading && <span className="inline-flex shrink-0">{icon}</span>}
    {children}
  </button>
))
Button.displayName = 'Button'

export { Button, buttonVariants }
```

Rules that fall out of this:

- **Always merge with `cn(...)`** (clsx + tailwind-merge) so a caller's `className` can override.
- **Always extend the native element's HTML attributes** and spread `...props` — callers pass
  `type`, `aria-*`, `onClick` without new props being added.
- **Export the variants object** next to the component (`buttonVariants`, `badgeVariants`,
  `iconButtonVariants`) so other components can reuse the classes.
- **`forwardRef` + `displayName`** for anything that wraps a DOM element (`Button`, `IconButton`,
  `Card`, `DataTableToolbarButton` — which must forward so it can serve as a Headless UI
  `MenuButton` via `as`). Simple presentational components (`Badge`, `AdminPageContent`) skip it.
- Cross-cutting combinations belong in `compoundVariants` (see `Badge`'s solid/outline matrix)
  rather than in conditional logic inside the component.
- Compound components expose subcomponents from the same file: `Card`/`CardHeader`/`CardTitle`/
  `CardContent`/`CardFooter` as named exports, and `Modal` with static `Modal.Header` /
  `Modal.Footer` sharing `onClose` through a local `ModalContext`.

## Icon-only controls and menus

- A control with no visible text carries its label twice: as the `Tooltip` `content` and as
  `aria-label`. `DataTableToolbarButton` is the model — `label` drives both, `href` renders a
  `LocalizedLink` instead of a button, `active` marks an on toggle, and a disabled control keeps the
  not-allowed cursor.
- Menus and popovers use Headless UI (`Menu`/`MenuButton`/`MenuItems`) with `anchor`, so they are
  portaled and never clipped by a table's scroll container, and pick the anchor side from direction:
  `anchor={isRTL ? 'bottom start' : 'bottom end'}` (`DataTableRowActions`, `DataTableExportButton`).
- A list of actions a caller filters by permission takes a `hidden` flag per entry and renders nothing
  when all are hidden, so call sites list every action and gate each inline.
- Any link inside a component is a `LocalizedLink` taking an unprefixed path.

## Form fields

Most inputs exist twice: a bare version taking `value`/`onChange` (and `error`) for use outside
Formik — the filter panels use the bare `Select` — and a `Form*` version bound by field name.
`FormLazySelect`/`FormLazyMultiSelect` exist only Formik-bound; `FileUpload`/`MultiFileUpload` only
bare, since they upload on selection and report through `onUploaded` (see `file-storage`). Add both
variants when you add an ordinary field type.

```tsx
'use client'

import { useField, useFormikContext } from 'formik'
import { cn } from '@/lib/utils'
import { useId } from 'react'

export const FormInput = ({ label, name, id, showValidation = true, className, icon, autoComplete = 'off', required = false, ...props }: FormInputProps) => {
  const [field, meta] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const generatedId = useId()
  const inputId = id ?? generatedId

  return (
    <div className={cn(className, (isDirty || submitCount > 0) && (hasError ? 'has-error' : ''))}>
      {label && (
        <label htmlFor={inputId} className="label form-label">
          {label}
          {required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <div className="relative text-muted-foreground">
        <input {...field} {...props} id={inputId} name={name} autoComplete={autoComplete} className={cn('form-input', icon && 'ps-10')} />
        {icon && <span className="pointer-events-none absolute inset-s-3 top-1/2 -translate-y-1/2">{icon}</span>}
      </div>
      {showValidation && hasError && <div className="mt-1.5 text-xs font-medium text-danger">{meta.error}</div>}
    </div>
  )
}
```

The conventions to keep: errors show only once the field is dirty **or** the form has been
submitted; the wrapper gets `has-error`; `useId()` is called unconditionally and links label and
input; `required` renders a `*`; `autoComplete` defaults to `'off'`; the label text is passed in
already translated — components call `t()` only for their own fixed copy (an aria label, "no
results"), never for caller-supplied text.

## Styling

- **Colors are semantic tokens, never values.** `styles/tailwind.css` defines each color twice — on
  `:root` for light and on `.dark` for dark — and exposes it to Tailwind through `@theme`:
  `bg-background`, `bg-surface` / `bg-surface-2` / `bg-surface-3`, `text-foreground`,
  `text-muted-foreground`, `text-subtle-foreground`, `border-border`, `border-input`, `ring-ring`,
  `bg-overlay`, and the accents `primary`, `secondary`, `success`, `warning`, `danger`, `info` (soft
  tints with opacity: `bg-primary/10 text-primary`). Because the tokens switch with the theme,
  **dark mode needs no `dark:` color classes** — never pair a light color with a `dark:` override,
  and never write a hex value or a stock palette class (`gray-*`, `slate-*`, `red-*`, `bg-white`,
  `text-black`). A shade the tokens lack becomes a new token in `styles/tailwind.css`.
  `styles/tokens.test.ts` fails on any of these in `app/` and `components/`.
- The theme ships component classes built on the tokens — `panel`, `panel-2`, `btn` with
  `btn-primary|secondary|soft|ghost|danger|…` and `btn-sm|lg`, `icon-btn`, `chip-btn`, `badge` with
  `badge-<color>` (soft), `badge-solid-<color>`, `badge-outline-<color>`, `form-input|select|textarea|
  checkbox|radio|label`, `has-error`, `menu-surface`, `table-base`, `kbd`. Prefer them over
  hand-rolled Tailwind (v4), and add utilities only for layout and spacing.
- Shape and type: controls `rounded-md` and 36px high, cards `rounded-xl` with a hairline border and
  at most `shadow-xs` (shadows are for floating layers), page titles `text-2xl font-semibold
  tracking-tight`, section titles `text-base font-semibold`, numbers `tabular-nums`.
- **RTL is mandatory**: use logical utilities — `ms-`/`me-`, `ps-`/`pe-`, `inset-s-`/`inset-e-`,
  `text-start`/`text-end`, `border-s`/`border-e`, `rounded-s`/`rounded-e` — never `ml-`/`pl-`/`left-`/
  `text-left`. Where a logical utility does not exist, pair the `ltr:` and `rtl:` variants
  (`ltr:text-left rtl:text-right`). Components that must branch in JS read
  `useAppSelector((state) => state.theme.rtlClass) === 'rtl'`. `i18n/tenant-screens.test.ts` fails on
  physical utilities (and `translate-x-`) in the tenancy, select-tenant, localization and settings
  screens and `TenantSwitcher`.
- Icons come from `lucide-react`, sized with classes (`h-4 w-4`) or the `size` prop.

## Client vs server

Add `'use client'` as the first line whenever the component uses hooks, context, browser APIs or
event handlers — that covers most of `components/ui`. Pure wrappers such as `Button`, `IconButton`,
`Card` and `AdminPageContent` stay server-compatible; do not add the directive unnecessarily.

## Checklist

- [ ] Right folder, kebab-case file, named export, JSDoc, `<Component>Props`
- [ ] `cva` variants + `cn(...)` merge + native attributes spread
- [ ] `forwardRef` and `displayName` when wrapping a DOM element
- [ ] Icon-only control has a tooltip and an `aria-label`; menus are anchored/portaled
- [ ] Semantic color tokens only (no hex, stock palette or `dark:` color classes) and logical (RTL-safe) utilities
- [ ] Exported from the folder's barrel
- [ ] For a form field: the bare and the `Form*` variant, wired to Formik the same way
- [ ] `npm run lint` and `npx tsc --noEmit` pass
