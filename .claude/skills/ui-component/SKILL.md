---
name: ui-component
description: Build or extend a shared React component in src/frontend/web/components — which folder it belongs in, the cva variant + cn pattern, forwardRef, Formik-aware form fields, barrel exports, and the dark-mode/RTL class rules. Use when adding a reusable component, a new form field, or a variant to an existing one.
---

# Shared components

## Which folder

| Folder | For | Examples |
| --- | --- | --- |
| `components/ui/` | Generic, app-agnostic primitives | `Button`, `Badge`, `Card`, `Modal`, `Dropdown`, `Loader`, `Tooltip`, `TreeView`, `Truncated`, `DateView`, `PriceView`, `LocalizedLink`, `ApiErrorMessages` |
| `components/ui/form/` | Inputs — a plain one and a Formik-bound `Form*` twin | `Input`/`FormInput`, `Select`/`FormSelect`, `MultiSelect`/`FormMultiSelect`, `Checkbox`/`FormCheckbox`, `DatePicker`/`FormDatePicker`, `FileUpload` |
| `components/ui/data-table/` | The table system | `DataTableProvider`, `DataTable`, `DataTableToolbar`, `DataTablePagination`, `DataTableSortIcon`, `DataTableCheckboxCell` |
| `components/layouts/` | App shell and page chrome | `AdminPageContent`, `Sidebar`, `Header`, `Footer`, `MainContainer`, `ProviderComponent`, `TranslationProvider` |
| `components/custom/` | App-specific composites tied to domain concepts | `NavUser`, `LanguageDropdown`, `ThemeChanger`, `ImagePreview`, `BackLink`, `CookieConsentDialog` |
| `components/notifications/` | The notification bell/panel/item trio | |

Screen-specific pieces do **not** go here — they live in the route's `_components/` folder. Promote
a component to `components/` only once a second screen needs it.

## Anatomy

One component per kebab-case file, named export, JSDoc above it, props interface named
`<Component>Props` (or `I<Component>Props` where that is already the local habit) declared directly
above. Add the export to the folder's `index.ts` barrel — consumers import from
`@/components/ui`, `@/components/ui/form`, `@/components/layouts`, never from the deep path.

**Variants use `class-variance-authority` + `cn`:**

```tsx
import { VariantProps, cva } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const buttonVariants = cva('btn cursor-pointer inline-flex items-center justify-center', {
  variants: {
    variant: { default: 'btn-primary', outline: 'btn-outline-primary', danger: 'btn-danger' },
    size: { default: '', sm: 'btn-sm', lg: 'btn-lg' },
  },
  defaultVariants: { variant: 'default', size: 'default' },
})

/** Props for the Button component… */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  isLoading?: boolean
  icon?: React.ReactNode
}

/** Button is a styled native button with variant/size options… */
const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, isLoading, disabled, children, ...props }, ref) => (
    <button className={cn(buttonVariants({ variant, size }), className)} ref={ref} disabled={disabled || isLoading} {...props}>
      {children}
    </button>
  ),
)
Button.displayName = 'Button'

export { Button, buttonVariants }
```

Rules that fall out of this:

- **Always merge with `cn(...)`** (clsx + tailwind-merge) so a caller's `className` can override.
- **Always extend the native element's HTML attributes** and spread `...props` — callers pass
  `type`, `aria-*`, `onClick` without new props being added.
- **Export the variants object** next to the component (`buttonVariants`, `badgeVariants`) so other
  components can reuse the classes.
- **`forwardRef` + `displayName`** for anything that wraps a DOM element (`Button`, `Card`,
  `Dropdown`). Simple presentational components (`Badge`, `AdminPageContent`) skip it.
- Cross-cutting combinations belong in `compoundVariants` (see `Badge`'s solid/outline matrix)
  rather than in conditional logic inside the component.
- Compound components expose subcomponents from the same file and share state through a local
  React context — `Card`/`CardHeader`/`CardTitle`/`CardContent`/`CardFooter`, and `Modal` with
  `ModalHeader`/`ModalFooter` via `ModalContext`.

## Form fields

Every input exists twice: a bare version taking `error`/`value` props for use outside Formik, and a
`Form*` version bound by field name. Add both when you add a field type.

```tsx
'use client'
import { useField, useFormikContext } from 'formik'
import { useId } from 'react'
import { cn } from '@/lib/utils'

export const FormInput = ({ label, name, id, showValidation = true, className, icon, required = false, ...props }: FormInputProps) => {
  const [field, meta] = useField(name)
  const { submitCount } = useFormikContext()
  const isDirty = meta.initialValue !== meta.value
  const hasError = (isDirty || submitCount > 0) && meta.error
  const inputId = id ?? useId()

  return (
    <div className={cn(className, (isDirty || submitCount > 0) && (hasError ? 'has-error' : ''))}>
      {label && (
        <label htmlFor={inputId} className="label form-label">
          {label}{required && <span className="ms-1 text-danger">*</span>}
        </label>
      )}
      <input {...field} {...props} id={inputId} name={name} className={cn('form-input', icon && 'ps-10')} />
      {showValidation && hasError && <div className="mt-1 text-danger">{meta.error}</div>}
    </div>
  )
}
```

The conventions to keep: errors show only once the field is dirty **or** the form has been
submitted; the wrapper gets `has-error`; `useId()` links label and input; `required` renders a
`*`; `autoComplete` defaults to `'off'`; the label text is passed in already translated — components
do not call `t()` for caller-supplied copy.

## Styling

- The theme ships semantic CSS classes — `btn`, `btn-primary`, `form-input`, `form-label`, `panel`,
  `panel-2`, `badge`, `has-error`, `text-danger`, `text-white-dark`, `dark`/`white-light`. Prefer
  them over hand-rolled Tailwind, and add utilities only for layout and spacing.
- **Dark mode is mandatory**: every color decision needs its `dark:` counterpart
  (`text-dark dark:text-white-light`, `bg-white dark:bg-[#191e3a]`).
- **RTL is mandatory**: use logical utilities — `ms-`/`me-`, `ps-`/`pe-`, `inset-s-`/`inset-e-` —
  never `ml-`/`pl-`/`left-`. Components that must branch on direction read
  `useAppSelector((state) => state.theme.rtlClass) === 'rtl'` (see how `Dropdown` picks its
  placement).
- Icons come from `lucide-react`, sized with classes (`h-4 w-4`) or the `size` prop.

## Client vs server

Add `'use client'` as the first line whenever the component uses hooks, context, browser APIs or
event handlers — that covers nearly everything in `components/ui`. Pure layout wrappers such as
`AdminPageContent` and `Card` stay server-compatible; do not add the directive unnecessarily.

## Checklist

- [ ] Right folder, kebab-case file, named export, JSDoc, `<Component>Props`
- [ ] `cva` variants + `cn(...)` merge + native attributes spread
- [ ] `forwardRef` and `displayName` when wrapping a DOM element
- [ ] `dark:` classes and logical (RTL-safe) spacing utilities
- [ ] Exported from the folder's `index.ts`
- [ ] For a form field: both the bare and the `Form*` variant, wired to Formik the same way
- [ ] `npm run lint` passes
