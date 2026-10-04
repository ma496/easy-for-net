import { Badge, Button, Card, CardContent, CardFooter, CardHeader, CardTitle, CodeShowcase, IconButton } from '@/components/ui'
import { ArrowUpRightIcon, CheckIcon, FolderOpenIcon, MoreHorizontalIcon, Plus, UsersIcon } from 'lucide-react'
import { ShowcasePreview } from '../../_components/showcase-preview'

const stats = [
  { label: 'Active users', value: '2,431', change: '+12%', trend: 'success' as const },
  { label: 'Storage used', value: '68.4 GB', change: '+3%', trend: 'warning' as const },
  { label: 'Failed sign-ins', value: '17', change: '-40%', trend: 'success' as const },
]

const members = [
  { initials: 'AK', name: 'Amira Khan', email: 'amira.khan@example.com', role: 'Owner', variant: 'primary' as const },
  { initials: 'JL', name: 'Jonas Lindqvist', email: 'jonas@example.com', role: 'Admin', variant: 'info' as const },
  { initials: 'MO', name: 'Maria Oliveira-Fernandes', email: 'maria.oliveira-fernandes@a-very-long-company-domain.example.com', role: 'Member', variant: 'secondary' as const },
]

/**
 * Showcase of the Card family (Card, CardHeader, CardTitle, CardContent, CardFooter) and the compositions built from it: basic, with actions, stat tiles, lists, pricing, accent and empty-state cards, each with its source snippet.
 */
export const CardsExample = () => {
  const basicCardCode = `<Card className="w-full max-w-md">
  <CardHeader>
    <CardTitle>Card title</CardTitle>
    <p className="text-sm text-muted-foreground">A muted line saying what the card holds.</p>
  </CardHeader>
  <CardContent>
    <p className="text-sm">Your content here.</p>
  </CardContent>
</Card>`

  const actionsCardCode = `<Card className="w-full max-w-md">
  <CardHeader className="flex-row items-start justify-between gap-4">
    <div className="min-w-0">
      <CardTitle>Workspace name</CardTitle>
      <p className="text-sm text-muted-foreground">Shown in the sidebar and in emails.</p>
    </div>
    <IconButton icon={<MoreHorizontalIcon />} variant="ghost" size="sm" aria-label="More" />
  </CardHeader>
  <CardContent>...</CardContent>
  <CardFooter className="justify-end border-t border-border pt-5 sm:pt-5">
    <Button variant="outline">Cancel</Button>
    <Button>Save</Button>
  </CardFooter>
</Card>`

  const statCardsCode = `<div className="grid gap-4 sm:grid-cols-3">
  <Card className="w-full">
    <CardContent className="space-y-2 pt-5 sm:pt-6">
      <p className="text-[13px] text-muted-foreground">Active users</p>
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-2xl font-semibold tabular-nums">2,431</span>
        <Badge variant="success">+12%</Badge>
      </div>
    </CardContent>
  </Card>
  ...
</div>`

  const listCardCode = `<Card className="w-full max-w-lg">
  <CardHeader className="flex-row items-center justify-between gap-4">
    <CardTitle>Members</CardTitle>
    <Button size="sm" variant="outline" icon={<Plus />}>Invite</Button>
  </CardHeader>
  <ul className="divide-y divide-border border-t border-border">
    {members.map((m) => (
      <li key={m.email} className="flex items-center gap-3 px-5 py-3 sm:px-6">
        <span className="grid size-9 shrink-0 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
          {m.initials}
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{m.name}</p>
          <p className="truncate text-xs text-muted-foreground">{m.email}</p>
        </div>
        <Badge variant={m.variant}>{m.role}</Badge>
      </li>
    ))}
  </ul>
</Card>`

  const pricingCardCode = `<Card className="w-full max-w-sm">
  <CardHeader>
    <div className="flex items-center justify-between gap-2">
      <CardTitle>Pro</CardTitle>
      <Badge type="strong">Popular</Badge>
    </div>
    <p className="text-sm text-muted-foreground">For growing teams.</p>
    <p className="mt-3 text-3xl font-semibold tabular-nums">
      $99<span className="text-sm font-normal text-muted-foreground">/month</span>
    </p>
  </CardHeader>
  <CardContent>
    <ul className="space-y-2 text-sm">
      <li className="flex items-center gap-2">
        <CheckIcon className="size-4 text-success" /> Advanced analytics
      </li>
      ...
    </ul>
  </CardContent>
  <CardFooter>
    <Button className="w-full">Upgrade</Button>
  </CardFooter>
</Card>`

  const accentCardsCode = `{/* Neutral: the default surface */}
<Card className="w-full">...</Card>

{/* Soft accent: tinted surface for a highlighted note */}
<Card className="w-full border-primary/20 bg-primary/5">...</Card>

{/* Solid accent: the loudest card on the page, use once */}
<Card className="w-full border-primary bg-primary text-primary-foreground">...</Card>`

  const emptyCardCode = `<Card className="w-full">
  <CardContent className="flex flex-col items-center gap-3 py-12 text-center sm:py-12">
    <span className="grid size-12 place-items-center rounded-xl bg-surface-2 text-muted-foreground">
      <FolderOpenIcon className="size-6" />
    </span>
    <div>
      <p className="text-sm font-semibold">No projects yet</p>
      <p className="mt-1 text-sm text-muted-foreground">Projects you create will appear here.</p>
    </div>
    <Button size="sm" icon={<Plus />}>New project</Button>
  </CardContent>
</Card>`

  return (
    <div className="space-y-6">
      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="Basic card"
          description="A title, a muted line and the content."
          code={basicCardCode}
          preview={
            <ShowcasePreview>
              <Card className="w-full max-w-md">
                <CardHeader>
                  <CardTitle>Card title</CardTitle>
                  <p className="text-sm text-muted-foreground">A muted line saying what the card holds.</p>
                </CardHeader>
                <CardContent>
                  <p className="text-sm">Your content here.</p>
                </CardContent>
              </Card>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Header action and footer"
          description="A menu at the end of the header and a divided footer for the card's actions."
          code={actionsCardCode}
          preview={
            <ShowcasePreview>
              <Card className="w-full max-w-md">
                <CardHeader className="flex-row items-start justify-between gap-4">
                  <div className="min-w-0">
                    <CardTitle>Workspace name</CardTitle>
                    <p className="text-sm text-muted-foreground">Shown in the sidebar and in emails.</p>
                  </div>
                  <IconButton icon={<MoreHorizontalIcon />} variant="ghost" size="sm" aria-label="More" />
                </CardHeader>
                <CardContent>
                  <input className="form-input" defaultValue="Acme Inc." aria-label="Workspace name" />
                </CardContent>
                <CardFooter className="justify-end border-t border-border pt-5 sm:pt-5">
                  <Button variant="outline">Cancel</Button>
                  <Button>Save</Button>
                </CardFooter>
              </Card>
            </ShowcasePreview>
          }
        />
      </div>

      <CodeShowcase
        title="Stat cards"
        description="A label, a large tabular number and a trend badge, in a responsive grid."
        code={statCardsCode}
        preview={
          <ShowcasePreview>
            <div className="grid w-full gap-4 sm:grid-cols-3">
              {stats.map((stat) => (
                <Card key={stat.label} className="w-full">
                  <CardContent className="space-y-2 pt-5 sm:pt-6">
                    <p className="text-[13px] text-muted-foreground">{stat.label}</p>
                    <div className="flex items-baseline justify-between gap-2">
                      <span className="text-2xl font-semibold tracking-tight tabular-nums">{stat.value}</span>
                      <Badge variant={stat.trend}>{stat.change}</Badge>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </ShowcasePreview>
        }
      />

      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="List card"
          description="Rows divided by hairlines; long names and emails truncate rather than wrap."
          code={listCardCode}
          preview={
            <ShowcasePreview>
              <Card className="w-full max-w-lg overflow-hidden">
                <CardHeader className="flex-row items-center justify-between gap-4">
                  <CardTitle className="flex items-center gap-2">
                    <UsersIcon className="size-4 text-muted-foreground" />
                    Members
                  </CardTitle>
                  <Button size="sm" variant="outline" icon={<Plus />}>
                    Invite
                  </Button>
                </CardHeader>
                <ul className="divide-y divide-border border-t border-border">
                  {members.map((member) => (
                    <li key={member.email} className="flex items-center gap-3 px-5 py-3 sm:px-6">
                      <span className="grid size-9 shrink-0 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">{member.initials}</span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium text-foreground">{member.name}</p>
                        <p className="truncate text-xs text-muted-foreground">{member.email}</p>
                      </div>
                      <Badge variant={member.variant}>{member.role}</Badge>
                    </li>
                  ))}
                </ul>
              </Card>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Pricing card"
          description="A plan with its price, what it includes and one full-width action."
          code={pricingCardCode}
          preview={
            <ShowcasePreview>
              <Card className="w-full max-w-sm">
                <CardHeader>
                  <div className="flex items-center justify-between gap-2">
                    <CardTitle>Pro</CardTitle>
                    <Badge type="strong">Popular</Badge>
                  </div>
                  <p className="text-sm text-muted-foreground">For growing teams.</p>
                  <p className="mt-3 text-3xl font-semibold tracking-tight tabular-nums">
                    $99<span className="text-sm font-normal text-muted-foreground">/month</span>
                  </p>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-2 text-sm">
                    {['Advanced analytics', 'Priority support', 'Custom integrations', 'Unlimited storage'].map((feature) => (
                      <li key={feature} className="flex items-center gap-2">
                        <CheckIcon className="size-4 shrink-0 text-success" />
                        {feature}
                      </li>
                    ))}
                  </ul>
                </CardContent>
                <CardFooter>
                  <Button className="w-full">Upgrade</Button>
                </CardFooter>
              </Card>
            </ShowcasePreview>
          }
        />
      </div>

      <CodeShowcase
        title="Accent cards"
        description="The neutral surface, a soft accent tint for a highlighted note, and a solid accent for the one thing that must stand out."
        code={accentCardsCode}
        preview={
          <ShowcasePreview>
            <div className="grid w-full gap-4 md:grid-cols-3">
              <Card className="w-full">
                <CardContent className="space-y-1 pt-5 sm:pt-6">
                  <h3 className="text-sm font-semibold text-foreground">Neutral</h3>
                  <p className="text-sm text-muted-foreground">The default surface for most content.</p>
                </CardContent>
              </Card>
              <Card className="w-full border-primary/20 bg-primary/5">
                <CardContent className="space-y-1 pt-5 sm:pt-6">
                  <h3 className="text-sm font-semibold text-primary">Soft accent</h3>
                  <p className="text-sm text-muted-foreground">A tinted note that draws the eye gently.</p>
                </CardContent>
              </Card>
              <Card className="w-full border-primary bg-primary text-primary-foreground">
                <CardContent className="space-y-1 pt-5 sm:pt-6">
                  <h3 className="flex items-center gap-1 text-sm font-semibold">
                    Solid accent
                    <ArrowUpRightIcon className="size-4 rtl:-scale-x-100" />
                  </h3>
                  <p className="text-sm opacity-85">The loudest card on a page. Use it once.</p>
                </CardContent>
              </Card>
            </div>
          </ShowcasePreview>
        }
      />

      <CodeShowcase
        title="Empty state"
        description="What a card shows when it has nothing to list yet: an icon tile, a title, a muted line and the action that fills it."
        code={emptyCardCode}
        preview={
          <ShowcasePreview>
            <Card className="w-full max-w-lg">
              <CardContent className="flex flex-col items-center gap-3 py-12 text-center sm:py-12">
                <span className="grid size-12 place-items-center rounded-xl bg-surface-2 text-muted-foreground">
                  <FolderOpenIcon className="size-6" />
                </span>
                <div>
                  <p className="text-sm font-semibold text-foreground">No projects yet</p>
                  <p className="mt-1 text-sm text-muted-foreground">Projects you create will appear here.</p>
                </div>
                <Button size="sm" icon={<Plus />}>
                  New project
                </Button>
              </CardContent>
            </Card>
          </ShowcasePreview>
        }
      />
    </div>
  )
}
