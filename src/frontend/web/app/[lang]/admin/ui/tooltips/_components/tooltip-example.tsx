'use client'

import { Tooltip, Truncated, Button, CodeShowcase } from '@/components/ui'
import { InfoIcon, SettingsIcon, UserIcon, HeartIcon } from 'lucide-react'

/**
 * Interactive client-side showcase component that demonstrates the Tooltip component's basic usage, positioning directions, rich content, delays, animations, and integration with the Truncated text component.
 */
export const TooltipExample = () => {
  return (
    <div className="space-y-8 p-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-bold">Tooltips</h1>
        <p className="text-white-dark">A premium, accessible tooltip component triggered by hover events.</p>
      </div>

      <div className="grid grid-cols-1 gap-8 md:grid-cols-2">
        <CodeShowcase
          title="Basic Tooltip"
          description="A standard tooltip shown on top of the trigger."
          preview={
            <div className="flex gap-4">
              <Tooltip content="Tooltip message">
                <Button variant="outline">Hover me</Button>
              </Tooltip>
              <Tooltip content="Save changes" delay={0}>
                <Button icon={<HeartIcon size={16} />} variant="outline">Instant</Button>
              </Tooltip>
            </div>
          }
          code={`<Tooltip content="Tooltip message">
  <Button variant="outline">Hover me</Button>
</Tooltip>

<Tooltip content="Save changes" delay={0}>
  <Button icon={<HeartIcon size={16} />} variant="outline">Instant</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Directions"
          description="Tooltips can be positioned in four main directions."
          preview={
            <div className="grid grid-cols-2 gap-4">
              <Tooltip content="Tooltip on Top" position="top">
                <Button variant="outline" className="w-full">Top</Button>
              </Tooltip>
              <Tooltip content="Tooltip on Bottom" position="bottom">
                <Button variant="outline" className="w-full">Bottom</Button>
              </Tooltip>
              <Tooltip content="Tooltip on Left" position="left">
                <Button variant="outline" className="w-full">Left</Button>
              </Tooltip>
              <Tooltip content="Tooltip on Right" position="right">
                <Button variant="outline" className="w-full">Right</Button>
              </Tooltip>
            </div>
          }
          code={`<Tooltip content="Tooltip on Top" position="top">
  <Button variant="outline">Top</Button>
</Tooltip>

<Tooltip content="Tooltip on Bottom" position="bottom">
  <Button variant="outline">Bottom</Button>
</Tooltip>

<Tooltip content="Tooltip on Left" position="left">
  <Button variant="outline">Left</Button>
</Tooltip>

<Tooltip content="Tooltip on Right" position="right">
  <Button variant="outline">Right</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Custom Rich Content"
          description="Tooltips can render any JSX content, allowing for rich notifications."
          preview={
            <Tooltip
              content={
                <div className="flex items-start gap-2 max-w-50">
                  <div className="mt-0.5 rounded-full bg-blue-500/20 p-1 text-blue-500">
                    <InfoIcon size={14} />
                  </div>
                  <div>
                    <div className="font-bold">System Status</div>
                    <div className="text-[10px] text-white/70">All systems are operational. Scheduled maintenance in 2 hours.</div>
                  </div>
                </div>
              }
            >
              <div className="cursor-pointer rounded-full bg-gray-100 p-2 dark:bg-gray-800">
                <SettingsIcon size={20} />
              </div>
            </Tooltip>
          }
          code={`<Tooltip
  content={
    <div className="flex items-start gap-2 max-w-50">
      <div className="mt-0.5 rounded-full bg-blue-500/20 p-1 text-blue-500">
        <InfoIcon size={14} />
      </div>
      <div>
        <div className="font-bold">System Status</div>
        <div className="text-[10px] text-white/70">
          All systems are operational...
        </div>
      </div>
    </div>
  }
>
  <SettingsIcon />
</Tooltip>`}
        />

        <CodeShowcase
          title="Interaction Delays"
          description="Control how quickly the tooltip appears or disappears."
          preview={
            <div className="flex gap-4">
              <Tooltip content="Delayed appearance" delay={1000}>
                <Button variant="outline">Long Delay (1s)</Button>
              </Tooltip>
              <Tooltip content="Medium delay" delay={500}>
                <Button variant="outline">Medium (0.5s)</Button>
              </Tooltip>
            </div>
          }
          code={`<Tooltip content="Delayed appearance" delay={1000}>
  <Button variant="outline">Long Delay (1s)</Button>
</Tooltip>

<Tooltip content="Medium delay" delay={500}>
  <Button variant="outline">Medium (0.5s)</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Animations"
          description="Tooltips are instant by default, but can be animated using the 'animate' prop."
          preview={
            <div className="flex gap-4">
              <Tooltip content="Instant tooltip (default)">
                <Button variant="outline">Instant</Button>
              </Tooltip>
              <Tooltip content="Animated tooltip" animate>
                <Button variant="default">Animated</Button>
              </Tooltip>
            </div>
          }
          code={`<Tooltip content="Instant tooltip (default)">
  <Button variant="outline">Instant</Button>
</Tooltip>

<Tooltip content="Animated tooltip" animate>
  <Button variant="primary">Animated</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Truncated Text"
          description="Automatically truncates text and shows a tooltip if it exceeds a limit."
          preview={
            <div className="flex flex-col gap-4">
              <div className="flex items-center gap-2">
                <span className="font-semibold text-xs min-w-32">Default (limit 40):</span>
                <Truncated text="This is a very long text that will be truncated automatically because it exceeds the default limit of forty characters." />
              </div>
              <div className="flex items-center gap-2">
                <span className="font-semibold text-xs min-w-32">Within limit:</span>
                <Truncated text="This text is short." />
              </div>
              <div className="flex items-center gap-2">
                <span className="font-semibold text-xs min-w-32">Custom limit (20):</span>
                <Truncated
                  limit={20}
                  text="This text exceeds a custom limit of twenty characters."
                />
              </div>
              <div className="flex items-center gap-2">
                <span className="font-semibold text-xs text-primary min-w-32">Animated:</span>
                <Truncated
                  animate
                  text="This truncated text has the premium tooltip animation enabled via prop."
                  className="font-medium text-primary"
                />
              </div>
              <div className="flex items-center gap-2">
                <span className="font-semibold text-xs min-w-32">No underline:</span>
                <Truncated
                  limit={20}
                  underline={false}
                  text="Truncated without underline."
                />
              </div>
            </div>
          }
          code={`<Truncated text="This is a very long text that will be truncated..." />

<Truncated text="Short text within limit." />

<Truncated limit={20} text="Text exceeding custom limit." />

<Truncated animate text="Truncated with animation." className="text-primary" />

<Truncated underline={false} text="Truncated without underline." />`}
        />
      </div>

      <div className="flex flex-col gap-4">
        <h2 className="text-xl font-semibold">User Experience Showcase</h2>
        <div className="panel flex flex-wrap items-center justify-center gap-8 py-10">
          <Tooltip content="Mark as favorite" position="bottom" className="text-xs">
            <HeartIcon className="h-6 w-6 text-danger cursor-pointer transition-transform hover:scale-110" />
          </Tooltip>

          <Tooltip content="View Profile" position="top">
            <div className="flex items-center gap-3 cursor-pointer group">
              <div className="h-10 w-10 overflow-hidden rounded-full border-2 border-primary/20 transition-all group-hover:border-primary">
                <UserIcon className="h-full w-full p-2 text-gray-400 group-hover:text-primary" />
              </div>
              <span className="font-medium group-hover:text-primary transition-colors">John Doe</span>
            </div>
          </Tooltip>

          <Tooltip content={
            <div className="flex flex-col items-center">
              <div className="mb-1 text-center font-bold">Performance</div>
              <div className="h-1 w-24 bg-white/20 rounded-full overflow-hidden">
                <div className="h-full w-3/4 bg-success" />
              </div>
              <div className="mt-1 text-[10px]">75% - Running optimally</div>
            </div>
          }>
            <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gray-100 shadow-sm dark:bg-gray-800 cursor-help">
              <span className="text-lg font-bold text-success">A+</span>
            </div>
          </Tooltip>
        </div>
      </div>
    </div>
  )
}
