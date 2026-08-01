import { Button } from '@/components/ui/button'
import { AlertCircleIcon, ArrowRightIcon, BellIcon, CheckIcon, MailIcon, Plus, SendIcon } from 'lucide-react'
import { IconButton } from '@/components/ui/icon-button'
import { CodeShowcase } from '@/components/ui/code-showcase'

/**
 * Interactive showcase component that demonstrates the available variants, sizes, icons, loading states, rounded styles, icon-only, and custom-styled buttons alongside their source snippets.
 */
export const ButtonsExample = () => {
  const basicButtonsCode = `<Button>Default</Button>
<Button variant="outline">Outline</Button>
<Button variant="info">Info</Button>
<Button variant="success">Success</Button>
<Button variant="warning">Warning</Button>
<Button variant="danger">Danger</Button>
<Button variant="secondary">Secondary</Button>
<Button variant="dark">Dark</Button>`

  const buttonSizesCode = `<Button size="sm">Small</Button>
<Button size="default">Default</Button>
<Button size="lg">Large</Button>`

  const iconButtonsCode = `<Button icon={<ArrowRightIcon />}>
  With Icon
</Button>
<Button variant="outline" icon={<MailIcon />}>
  Send Email
</Button>
<Button variant="success" icon={<CheckIcon />}>
  Confirm
</Button>`

  const loadingButtonsCode = `<Button isLoading>Loading...</Button>
<Button variant="info" isLoading>Processing</Button>
<Button variant="success" isLoading>Saving</Button>`

  const roundedButtonsCode = `<Button rounded="full">Rounded</Button>
<Button variant="info" rounded="full">Rounded Info</Button>
<Button variant="success" rounded="full">Rounded Success</Button>`

  const iconOnlyButtonsCode = `<IconButton icon={<Plus />} />
<IconButton icon={<BellIcon />} variant="secondary" />
<IconButton icon={<Plus />} variant="info" rounded="full" />
<IconButton icon={<Plus />} isLoading />`

  const customStyledCode = `<Button className="bg-linear-to-r from-purple-500 to-pink-500 hover:from-pink-500 hover:to-purple-500 text-white">
  Gradient Button
</Button>
<Button className="bg-emerald-500 shadow-lg shadow-emerald-500/50 hover:bg-emerald-600">
  Shadow Effect
</Button>
<Button className="relative overflow-hidden bg-violet-500 hover:bg-violet-600 before:absolute before:inset-0 before:bg-white/20 before:-translate-x-full hover:before:translate-x-full before:transition-transform before:duration-300">
  Shine Effect
</Button>`

  return (
    <div className="container mx-auto space-y-8 p-6">
      <div className="mb-8">
        <h1 className="mb-2 text-3xl font-bold">Button Components</h1>
        <p className="text-white-dark">Interactive button elements with various styles, sizes, and states.</p>
      </div>

      <div className="grid gap-8">
        {/* Basic Button Variants */}
        <CodeShowcase
          title="Button Variants"
          description="Different button styles and color schemes"
          code={basicButtonsCode}
          preview={
            <div className="flex flex-wrap justify-center gap-3">
              <Button>Default</Button>
              <Button variant="outline">Outline</Button>
              <Button variant="info">Info</Button>
              <Button variant="success">Success</Button>
              <Button variant="warning">Warning</Button>
              <Button variant="danger">Danger</Button>
              <Button variant="secondary">Secondary</Button>
              <Button variant="dark">Dark</Button>
            </div>
          }
        />

        {/* Button Sizes */}
        <CodeShowcase
          title="Button Sizes"
          description="Different button sizes for various use cases"
          code={buttonSizesCode}
          preview={
            <div className="flex flex-wrap items-center justify-center gap-3">
              <Button size="sm">Small</Button>
              <Button>Default</Button>
              <Button size="lg">Large</Button>
            </div>
          }
        />

        {/* Icon Buttons */}
        <CodeShowcase
          title="Buttons with Icons"
          description="Buttons enhanced with icons for better visual communication"
          code={iconButtonsCode}
          preview={
            <div className="flex flex-wrap justify-center gap-3">
              <Button icon={<ArrowRightIcon />}>With Icon</Button>
              <Button variant="outline" icon={<MailIcon />}>
                Send Email
              </Button>
              <Button variant="success" icon={<CheckIcon />}>
                Confirm
              </Button>
              <Button variant="info" size="sm" icon={<SendIcon size={14} />}>
                Small with Icon
              </Button>
              <Button variant="danger" size="lg" icon={<AlertCircleIcon />}>
                Large with Icon
              </Button>
            </div>
          }
        />

        {/* Loading States */}
        <CodeShowcase
          title="Loading States"
          description="Buttons with loading indicators for async operations"
          code={loadingButtonsCode}
          preview={
            <div className="flex flex-wrap justify-center gap-3">
              <Button isLoading>Loading...</Button>
              <Button variant="info" isLoading>
                Processing
              </Button>
              <Button variant="success" isLoading>
                Saving
              </Button>
              <Button variant="warning" size="sm" isLoading>
                Small Loading
              </Button>
              <Button variant="danger" size="lg" isLoading>
                Large Loading
              </Button>
            </div>
          }
        />

        {/* Rounded Buttons */}
        <CodeShowcase
          title="Rounded Buttons"
          description="Buttons with rounded-sm styling"
          code={roundedButtonsCode}
          preview={
            <div className="flex flex-wrap justify-center gap-3">
              <Button rounded="full">Rounded</Button>
              <Button variant="info" rounded="full">
                Rounded Info
              </Button>
              <Button variant="success" rounded="full">
                Rounded Success
              </Button>
              <Button variant="outline" rounded="full">
                Rounded Outline
              </Button>
            </div>
          }
        />

        {/* Icon Only Buttons */}
        <CodeShowcase
          title="Icon Only Buttons"
          description="Compact buttons with only icons"
          code={iconOnlyButtonsCode}
          preview={
            <div className="flex flex-wrap items-center justify-center gap-3">
              <IconButton icon={<Plus />} />
              <IconButton icon={<Plus />} size="sm" />
              <IconButton icon={<Plus />} size="lg" />
              <IconButton icon={<BellIcon />} variant="secondary" />
              <IconButton icon={<Plus />} variant="info" rounded="full" />
              <IconButton icon={<Plus />} isLoading />
            </div>
          }
        />

        {/* Custom Styled Buttons */}
        <CodeShowcase
          title="Custom Styled Buttons"
          description="Buttons with custom CSS classes and effects"
          code={customStyledCode}
          preview={
            <div className="flex flex-wrap justify-center gap-3">
              <Button className="bg-linear-to-r from-purple-500 to-pink-500 text-white hover:from-pink-500 hover:to-purple-500">Gradient Button</Button>
              <Button className="bg-emerald-500 shadow-lg shadow-emerald-500/50 hover:bg-emerald-600">Shadow Effect</Button>
              <Button className="relative overflow-hidden bg-violet-500 before:absolute before:inset-0 before:-translate-x-full before:bg-white/20 before:transition-transform before:duration-300 hover:bg-violet-600 hover:before:translate-x-full">
                Shine Effect
              </Button>
            </div>
          }
        />

        {/* Interactive Examples */}
        <CodeShowcase
          title="Interactive Examples"
          description="Real-world button usage examples"
          code={`<div className="flex gap-3">
  <Button variant="outline" icon={<MailIcon />}>
    Contact Us
  </Button>
  <Button variant="success" icon={<CheckIcon />}>
    Save Changes
  </Button>
  <Button variant="danger" icon={<AlertCircleIcon />}>
    Delete Item
  </Button>
</div>`}
          preview={
            <div className="space-y-4">
              <div className="flex flex-wrap justify-center gap-3">
                <Button variant="outline" icon={<MailIcon />}>
                  Contact Us
                </Button>
                <Button variant="success" icon={<CheckIcon />}>
                  Save Changes
                </Button>
                <Button variant="danger" icon={<AlertCircleIcon />}>
                  Delete Item
                </Button>
              </div>
              <div className="flex flex-wrap justify-center gap-3">
                <Button size="sm" variant="secondary">
                  Cancel
                </Button>
                <Button size="sm" variant="info" isLoading>
                  Uploading...
                </Button>
                <Button size="sm" variant="success">
                  Complete
                </Button>
              </div>
            </div>
          }
        />
      </div>
    </div>
  )
}
