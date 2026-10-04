'use client'

import { Tooltip, Truncated, Button, CodeShowcase, IconButton } from '@/components/ui'
import { InfoIcon, SettingsIcon, UserIcon, HeartIcon, CopyIcon, PencilIcon, Trash2Icon } from 'lucide-react'
import { ShowcasePreview, ShowcaseRow } from '../../_components/showcase-preview'

/**
 * Interactive client-side showcase component that demonstrates the Tooltip component's basic usage, positioning directions, rich content, delays, animations, and integration with the Truncated text component.
 */
export const TooltipExample = () => {
  return (
    <div className="space-y-6">
      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="Basic tooltip"
          description="Shown above the trigger on hover or focus."
          preview={
            <ShowcasePreview>
              <Tooltip content="Tooltip message">
                <Button variant="outline">Hover me</Button>
              </Tooltip>
              <Tooltip content="Save changes" delay={0}>
                <Button icon={<HeartIcon />} variant="outline">
                  Instant
                </Button>
              </Tooltip>
            </ShowcasePreview>
          }
          code={`<Tooltip content="Tooltip message">
  <Button variant="outline">Hover me</Button>
</Tooltip>

<Tooltip content="Save changes" delay={0}>
  <Button icon={<HeartIcon />} variant="outline">Instant</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Directions"
          description="Top, bottom, left or right of the trigger."
          preview={
            <ShowcasePreview>
              <div className="grid w-full max-w-xs grid-cols-2 gap-3">
                <Tooltip content="Tooltip on top" position="top">
                  <Button variant="outline" className="w-full">
                    Top
                  </Button>
                </Tooltip>
                <Tooltip content="Tooltip on bottom" position="bottom">
                  <Button variant="outline" className="w-full">
                    Bottom
                  </Button>
                </Tooltip>
                <Tooltip content="Tooltip on left" position="left">
                  <Button variant="outline" className="w-full">
                    Left
                  </Button>
                </Tooltip>
                <Tooltip content="Tooltip on right" position="right">
                  <Button variant="outline" className="w-full">
                    Right
                  </Button>
                </Tooltip>
              </div>
            </ShowcasePreview>
          }
          code={`<Tooltip content="Tooltip on top" position="top">
  <Button variant="outline">Top</Button>
</Tooltip>

<Tooltip content="Tooltip on bottom" position="bottom">
  <Button variant="outline">Bottom</Button>
</Tooltip>

<Tooltip content="Tooltip on left" position="left">
  <Button variant="outline">Left</Button>
</Tooltip>

<Tooltip content="Tooltip on right" position="right">
  <Button variant="outline">Right</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Icon buttons"
          description="The label an icon-only control needs, shown on hover."
          preview={
            <ShowcasePreview>
              <div className="flex items-center gap-1 rounded-lg border border-border bg-surface p-1">
                <Tooltip content="Edit">
                  <IconButton icon={<PencilIcon />} variant="ghost" size="sm" aria-label="Edit" />
                </Tooltip>
                <Tooltip content="Duplicate">
                  <IconButton icon={<CopyIcon />} variant="ghost" size="sm" aria-label="Duplicate" />
                </Tooltip>
                <Tooltip content="Delete">
                  <IconButton icon={<Trash2Icon />} variant="ghost" size="sm" className="text-danger" aria-label="Delete" />
                </Tooltip>
              </div>
            </ShowcasePreview>
          }
          code={`<Tooltip content="Edit">
  <IconButton icon={<PencilIcon />} variant="ghost" size="sm" aria-label="Edit" />
</Tooltip>

<Tooltip content="Delete">
  <IconButton icon={<Trash2Icon />} variant="ghost" size="sm" className="text-danger" aria-label="Delete" />
</Tooltip>`}
        />

        <CodeShowcase
          title="Rich content"
          description="Any JSX can be the content, for short status notes."
          preview={
            <ShowcasePreview>
              <Tooltip
                content={
                  <div className="flex max-w-56 items-start gap-2">
                    <div className="mt-0.5 rounded-full bg-info/20 p-1 text-info">
                      <InfoIcon size={14} />
                    </div>
                    <div>
                      <div className="font-semibold">System status</div>
                      <div className="text-[11px] font-normal text-background/70">All systems are operational. Scheduled maintenance in 2 hours.</div>
                    </div>
                  </div>
                }
              >
                <IconButton icon={<SettingsIcon />} variant="outline" rounded="full" aria-label="System status" />
              </Tooltip>
            </ShowcasePreview>
          }
          code={`<Tooltip
  content={
    <div className="flex max-w-56 items-start gap-2">
      <div className="mt-0.5 rounded-full bg-info/20 p-1 text-info">
        <InfoIcon size={14} />
      </div>
      <div>
        <div className="font-semibold">System status</div>
        <div className="text-[11px] font-normal text-background/70">
          All systems are operational...
        </div>
      </div>
    </div>
  }
>
  <IconButton icon={<SettingsIcon />} variant="outline" rounded="full" aria-label="System status" />
</Tooltip>`}
        />

        <CodeShowcase
          title="Delays"
          description="How long the pointer must rest before the tooltip appears."
          preview={
            <ShowcasePreview>
              <Tooltip content="Medium delay" delay={500}>
                <Button variant="outline">Medium (0.5s)</Button>
              </Tooltip>
              <Tooltip content="Delayed appearance" delay={1000}>
                <Button variant="outline">Long (1s)</Button>
              </Tooltip>
            </ShowcasePreview>
          }
          code={`<Tooltip content="Medium delay" delay={500}>
  <Button variant="outline">Medium (0.5s)</Button>
</Tooltip>

<Tooltip content="Delayed appearance" delay={1000}>
  <Button variant="outline">Long (1s)</Button>
</Tooltip>`}
        />

        <CodeShowcase
          title="Animation"
          description="Instant by default; the animate prop fades and scales it in."
          preview={
            <ShowcasePreview>
              <Tooltip content="Instant tooltip (default)">
                <Button variant="outline">Instant</Button>
              </Tooltip>
              <Tooltip content="Animated tooltip" animate>
                <Button>Animated</Button>
              </Tooltip>
            </ShowcasePreview>
          }
          code={`<Tooltip content="Instant tooltip (default)">
  <Button variant="outline">Instant</Button>
</Tooltip>

<Tooltip content="Animated tooltip" animate>
  <Button>Animated</Button>
</Tooltip>`}
        />
      </div>

      <CodeShowcase
        title="Truncated text"
        description="Cuts text past a character limit and shows the whole of it in a tooltip."
        preview={
          <ShowcasePreview stack>
            <ShowcaseRow label="Limit 40">
              <span className="text-sm text-foreground">
                <Truncated text="This is a very long text that will be truncated automatically because it exceeds the default limit of forty characters." />
              </span>
            </ShowcaseRow>
            <ShowcaseRow label="Within limit">
              <span className="text-sm text-foreground">
                <Truncated text="This text is short." />
              </span>
            </ShowcaseRow>
            <ShowcaseRow label="Limit 20">
              <span className="text-sm text-foreground">
                <Truncated limit={20} text="This text exceeds a custom limit of twenty characters." />
              </span>
            </ShowcaseRow>
            <ShowcaseRow label="Animated">
              <span className="text-sm">
                <Truncated animate text="This truncated text has the tooltip animation enabled via prop." className="font-medium text-primary" />
              </span>
            </ShowcaseRow>
            <ShowcaseRow label="No underline">
              <span className="text-sm text-foreground">
                <Truncated limit={20} underline={false} text="Truncated without underline." />
              </span>
            </ShowcaseRow>
          </ShowcasePreview>
        }
        code={`<Truncated text="This is a very long text that will be truncated..." />

<Truncated text="Short text within limit." />

<Truncated limit={20} text="Text exceeding custom limit." />

<Truncated animate text="Truncated with animation." className="text-primary" />

<Truncated underline={false} text="Truncated without underline." />`}
      />

      <CodeShowcase
        title="In context"
        description="Tooltips on an icon, a user chip and a score tile."
        preview={
          <ShowcasePreview className="gap-8">
            <Tooltip content="Mark as favorite" position="bottom" className="text-xs">
              <HeartIcon className="size-6 cursor-pointer text-danger transition-transform hover:scale-110" />
            </Tooltip>

            <Tooltip content="View profile" position="top">
              <div className="group flex cursor-pointer items-center gap-3">
                <div className="grid size-10 place-items-center rounded-full bg-primary/10 text-primary ring-2 ring-transparent transition-shadow group-hover:ring-primary/25">
                  <UserIcon className="size-5" />
                </div>
                <span className="text-sm font-medium text-foreground transition-colors group-hover:text-primary">John Doe</span>
              </div>
            </Tooltip>

            <Tooltip
              content={
                <div className="flex flex-col items-center">
                  <div className="mb-1 text-center font-semibold">Performance</div>
                  <div className="h-1 w-24 overflow-hidden rounded-full bg-background/20">
                    <div className="h-full w-3/4 bg-success" />
                  </div>
                  <div className="mt-1 text-[11px] font-normal">75% - Running optimally</div>
                </div>
              }
            >
              <div className="grid size-12 cursor-help place-items-center rounded-xl border border-border bg-surface-2">
                <span className="text-lg font-semibold text-success">A+</span>
              </div>
            </Tooltip>
          </ShowcasePreview>
        }
        code={`<Tooltip content="Mark as favorite" position="bottom">
  <HeartIcon className="size-6 text-danger" />
</Tooltip>

<Tooltip
  content={
    <div className="flex flex-col items-center">
      <div className="mb-1 font-semibold">Performance</div>
      <div className="h-1 w-24 overflow-hidden rounded-full bg-background/20">
        <div className="h-full w-3/4 bg-success" />
      </div>
      <div className="mt-1 text-[11px]">75% - Running optimally</div>
    </div>
  }
>
  <div className="grid size-12 place-items-center rounded-xl border border-border bg-surface-2">A+</div>
</Tooltip>`}
      />
    </div>
  )
}
