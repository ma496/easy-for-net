import { Badge, Button, IconButton, CodeShowcase } from '@/components/ui'
import { ArrowRightIcon, BellIcon, CheckIcon, DownloadIcon, FilterIcon, MailIcon, MoreHorizontalIcon, PencilIcon, Plus, RefreshCwIcon, SaveIcon, SendIcon, Trash2Icon } from 'lucide-react'
import { ShowcasePreview, ShowcaseRow } from '../../_components/showcase-preview'

const badgeColors = ['primary', 'secondary', 'success', 'warning', 'danger', 'info', 'dark'] as const

/**
 * Showcase of the Button, IconButton and Badge components: every variant, status colour, outline style, size, icon placement, loading and disabled state, and the action rows they compose into — each with its source snippet.
 */
export const ButtonsExample = () => {
  const variantsCode = `<Button>Primary</Button>
<Button variant="outline">Outline</Button>
<Button variant="soft">Soft</Button>
<Button variant="ghost">Ghost</Button>
<Button variant="link">Link</Button>`

  const statusCode = `<Button variant="success">Success</Button>
<Button variant="warning">Warning</Button>
<Button variant="danger">Danger</Button>
<Button variant="info">Info</Button>
<Button variant="dark">Dark</Button>

<Button variant="outline-primary">Primary</Button>
<Button variant="outline-success">Success</Button>
<Button variant="outline-warning">Warning</Button>
<Button variant="outline-danger">Danger</Button>
<Button variant="outline-info">Info</Button>
<Button variant="outline-dark">Dark</Button>`

  const sizesCode = `<Button size="sm">Small</Button>
<Button>Default</Button>
<Button size="lg">Large</Button>

<Button rounded="full">Pill</Button>`

  const iconsCode = `{/* Leading icon */}
<Button icon={<Plus />}>New user</Button>
<Button variant="outline" icon={<DownloadIcon />}>Export</Button>

{/* Trailing icon: put it in the children, flipped for right-to-left */}
<Button variant="soft">
  Continue
  <ArrowRightIcon className="size-4 rtl:-scale-x-100" />
</Button>`

  const iconButtonsCode = `<IconButton icon={<Plus />} aria-label="Add" />
<IconButton icon={<PencilIcon />} variant="outline" aria-label="Edit" />
<IconButton icon={<BellIcon />} variant="soft" aria-label="Notifications" />
<IconButton icon={<MoreHorizontalIcon />} variant="ghost" aria-label="More" />
<IconButton icon={<Trash2Icon />} variant="outline-danger" aria-label="Delete" />

<IconButton icon={<Plus />} size="sm" aria-label="Add" />
<IconButton icon={<Plus />} size="lg" aria-label="Add" />
<IconButton icon={<Plus />} rounded="full" aria-label="Add" />`

  const statesCode = `<Button isLoading>Saving</Button>
<Button variant="outline" isLoading>Loading</Button>
<IconButton icon={<RefreshCwIcon />} variant="outline" isLoading aria-label="Refresh" />

<Button disabled>Disabled</Button>
<Button variant="outline" disabled>Disabled</Button>`

  const compositionCode = `{/* Form footer: secondary action first, primary last */}
<div className="flex justify-end gap-2">
  <Button variant="outline">Cancel</Button>
  <Button icon={<SaveIcon />}>Save changes</Button>
</div>

{/* Destructive confirmation */}
<div className="flex justify-end gap-2">
  <Button variant="outline">Keep it</Button>
  <Button variant="danger" icon={<Trash2Icon />}>Delete project</Button>
</div>

{/* Toolbar */}
<div className="flex items-center gap-1">
  <IconButton icon={<FilterIcon />} variant="ghost" aria-label="Filter" />
  <IconButton icon={<RefreshCwIcon />} variant="ghost" aria-label="Refresh" />
  <Button size="sm" icon={<Plus />}>Add</Button>
</div>`

  const badgesCode = `{/* type="solid" (default): soft tint */}
<Badge variant="success">Active</Badge>

{/* type="strong": filled */}
<Badge variant="success" type="strong">Active</Badge>

{/* type="outline": border only */}
<Badge variant="success" type="outline">Active</Badge>

{/* variant: primary | secondary | success | warning | danger | info | dark */}`

  return (
    <div className="space-y-6">
      <CodeShowcase
        title="Variants"
        description="Primary for the one main action, outline for the action beside it, soft and ghost for quieter controls."
        code={variantsCode}
        preview={
          <ShowcasePreview>
            <Button>Primary</Button>
            <Button variant="outline">Outline</Button>
            <Button variant="soft">Soft</Button>
            <Button variant="ghost">Ghost</Button>
            <Button variant="link">Link</Button>
          </ShowcasePreview>
        }
      />

      <CodeShowcase
        title="Status colours"
        description="Solid and outline buttons for actions that carry a meaning: confirm, caution, destroy, inform."
        code={statusCode}
        preview={
          <ShowcasePreview stack>
            <ShowcaseRow label="Solid">
              <Button variant="success">Success</Button>
              <Button variant="warning">Warning</Button>
              <Button variant="danger">Danger</Button>
              <Button variant="info">Info</Button>
              <Button variant="dark">Dark</Button>
            </ShowcaseRow>
            <ShowcaseRow label="Outline">
              <Button variant="outline-primary">Primary</Button>
              <Button variant="outline-success">Success</Button>
              <Button variant="outline-warning">Warning</Button>
              <Button variant="outline-danger">Danger</Button>
              <Button variant="outline-info">Info</Button>
              <Button variant="outline-dark">Dark</Button>
            </ShowcaseRow>
          </ShowcasePreview>
        }
      />

      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="Sizes"
          description="Small for dense toolbars and tables, large for hero and auth surfaces."
          code={sizesCode}
          preview={
            <ShowcasePreview stack>
              <ShowcaseRow label="Size">
                <Button size="sm">Small</Button>
                <Button>Default</Button>
                <Button size="lg">Large</Button>
              </ShowcaseRow>
              <ShowcaseRow label="Pill">
                <Button rounded="full" size="sm">
                  Small
                </Button>
                <Button rounded="full" variant="outline">
                  Default
                </Button>
                <Button rounded="full" variant="soft" size="lg">
                  Large
                </Button>
              </ShowcaseRow>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="With icons"
          description="A leading icon through the icon prop; a trailing one in the children."
          code={iconsCode}
          preview={
            <ShowcasePreview>
              <Button icon={<Plus />}>New user</Button>
              <Button variant="outline" icon={<DownloadIcon />}>
                Export
              </Button>
              <Button variant="success" size="sm" icon={<CheckIcon />}>
                Approve
              </Button>
              <Button variant="info" size="sm" icon={<SendIcon />}>
                Send
              </Button>
              <Button variant="soft">
                Continue
                <ArrowRightIcon className="size-4 rtl:-scale-x-100" />
              </Button>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Icon buttons"
          description="Square, icon-only controls. Always give them an aria-label."
          code={iconButtonsCode}
          preview={
            <ShowcasePreview stack>
              <ShowcaseRow label="Variant">
                <IconButton icon={<Plus />} aria-label="Add" />
                <IconButton icon={<PencilIcon />} variant="outline" aria-label="Edit" />
                <IconButton icon={<BellIcon />} variant="soft" aria-label="Notifications" />
                <IconButton icon={<MoreHorizontalIcon />} variant="ghost" aria-label="More" />
                <IconButton icon={<Trash2Icon />} variant="outline-danger" aria-label="Delete" />
                <IconButton icon={<MailIcon />} variant="info" aria-label="Mail" />
              </ShowcaseRow>
              <ShowcaseRow label="Size">
                <IconButton icon={<Plus />} size="sm" aria-label="Add" />
                <IconButton icon={<Plus />} aria-label="Add" />
                <IconButton icon={<Plus />} size="lg" aria-label="Add" />
                <IconButton icon={<Plus />} rounded="full" variant="outline" aria-label="Add" />
              </ShowcaseRow>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Loading and disabled"
          description="isLoading swaps in a spinner and disables the button until the work is done."
          code={statesCode}
          preview={
            <ShowcasePreview stack>
              <ShowcaseRow label="Loading">
                <Button isLoading>Saving</Button>
                <Button variant="outline" isLoading>
                  Loading
                </Button>
                <Button variant="danger" size="sm" isLoading>
                  Deleting
                </Button>
                <IconButton icon={<RefreshCwIcon />} variant="outline" isLoading aria-label="Refresh" />
              </ShowcaseRow>
              <ShowcaseRow label="Disabled">
                <Button disabled>Disabled</Button>
                <Button variant="outline" disabled>
                  Disabled
                </Button>
                <Button variant="soft" disabled>
                  Disabled
                </Button>
              </ShowcaseRow>
            </ShowcasePreview>
          }
        />
      </div>

      <CodeShowcase
        title="Compositions"
        description="How buttons pair up in real screens: form footers, destructive confirmations and toolbars."
        code={compositionCode}
        preview={
          <ShowcasePreview stack>
            <ShowcaseRow label="Form">
              <Button variant="outline">Cancel</Button>
              <Button icon={<SaveIcon />}>Save changes</Button>
            </ShowcaseRow>
            <ShowcaseRow label="Confirm">
              <Button variant="outline">Keep it</Button>
              <Button variant="danger" icon={<Trash2Icon />}>
                Delete project
              </Button>
            </ShowcaseRow>
            <ShowcaseRow label="Toolbar">
              <div className="flex items-center gap-1 rounded-lg border border-border bg-surface p-1">
                <IconButton icon={<FilterIcon />} variant="ghost" size="sm" aria-label="Filter" />
                <IconButton icon={<RefreshCwIcon />} variant="ghost" size="sm" aria-label="Refresh" />
                <IconButton icon={<DownloadIcon />} variant="ghost" size="sm" aria-label="Export" />
                <Button size="sm" icon={<Plus />}>
                  Add
                </Button>
              </div>
            </ShowcaseRow>
          </ShowcasePreview>
        }
      />

      <CodeShowcase
        title="Badges"
        description={'Short status labels: a soft tint by default, filled with type="strong", or outlined.'}
        code={badgesCode}
        preview={
          <ShowcasePreview stack>
            <ShowcaseRow label="Soft">
              {badgeColors.map((color) => (
                <Badge key={color} className="capitalize" variant={color}>
                  {color}
                </Badge>
              ))}
            </ShowcaseRow>
            <ShowcaseRow label="Strong">
              {badgeColors.map((color) => (
                <Badge key={color} className="capitalize" variant={color} type="strong">
                  {color}
                </Badge>
              ))}
            </ShowcaseRow>
            <ShowcaseRow label="Outline">
              {badgeColors.map((color) => (
                <Badge key={color} className="capitalize" variant={color} type="outline">
                  {color}
                </Badge>
              ))}
            </ShowcaseRow>
          </ShowcasePreview>
        }
      />
    </div>
  )
}
