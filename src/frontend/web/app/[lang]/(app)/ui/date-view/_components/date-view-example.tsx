'use client'

import { DateView, CodeShowcase } from '@/components/ui'
import { ShowcasePreview } from '../../_components/showcase-preview'

/**
 * Interactive client-side showcase component that demonstrates the DateView component in basic, custom-format, and edge-case (null/invalid/timestamp) scenarios.
 */
export const DateViewExample = () => {
  const now = new Date()
  const dateString = '2023-12-25'
  const timestamp = 1704067200000 // 2024-01-01

  const codeExamples = {
    basic: `import { DateView } from '@/components/ui'

<DateView date={new Date()} />`,

    formats: `import { DateView } from '@/components/ui'

<DateView date={new Date()} format="yyyy-MM-dd" />
<DateView date={new Date()} format="MM/dd/yyyy HH:mm" />
<DateView date={new Date()} format="PPP" />`,

    invalid: `import { DateView } from '@/components/ui'

<DateView date={null} />
<DateView date="invalid-date" />`,
  }

  return (
    <div className="space-y-6">
      <CodeShowcase
        title="Basic usage"
        description="The current date in the default format."
        code={codeExamples.basic}
        preview={
          <ShowcasePreview>
            <span className="text-sm font-medium text-foreground tabular-nums">
              <DateView date={now} />
            </span>
          </ShowcasePreview>
        }
      />

      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="Custom formats"
          description="Any date-fns format string."
          code={codeExamples.formats}
          preview={
            <ShowcasePreview stack className="p-0 sm:p-0">
              <DateRows
                rows={[
                  { label: 'yyyy-MM-dd', date: now, format: 'yyyy-MM-dd' },
                  { label: 'MM/dd/yyyy HH:mm', date: now, format: 'MM/dd/yyyy HH:mm' },
                  { label: 'PPP', date: now, format: 'PPP' },
                ]}
              />
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Edge cases"
          description="Empty and invalid values render safely; strings and timestamps are parsed."
          code={codeExamples.invalid}
          preview={
            <ShowcasePreview stack className="p-0 sm:p-0">
              <DateRows
                rows={[
                  { label: 'Null date', date: null },
                  { label: 'Undefined date', date: undefined },
                  { label: 'Invalid date string', date: 'invalid-date' },
                  { label: 'String date (2023-12-25)', date: dateString },
                  { label: 'Timestamp', date: timestamp },
                ]}
              />
            </ShowcasePreview>
          }
        />
      </div>
    </div>
  )
}

/** One labelled example in a DateRows list. */
interface DateRow {
  label: string
  date: Date | string | number | null | undefined
  format?: string
}

/**
 * DateRows lists DateView examples as label / value rows divided by hairlines; the value sits at the end on wide screens and under its label on phones.
 */
const DateRows = ({ rows }: { rows: DateRow[] }) => (
  <dl className="w-full divide-y divide-border">
    {rows.map((row) => (
      <div key={row.label} className="flex flex-col gap-0.5 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4 sm:px-5">
        <dt className="font-mono text-[13px] text-muted-foreground">{row.label}</dt>
        <dd className="text-sm font-medium text-foreground tabular-nums">
          <DateView date={row.date} format={row.format} />
        </dd>
      </div>
    ))}
  </dl>
)
