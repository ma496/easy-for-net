'use client'

import { DateView, CodeShowcase } from '@/components/ui'
import { Calendar } from 'lucide-react'

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
<DateView date="invalid-date" />`
  }

  return (
    <div className="space-y-6">
      <div className="mb-8">
        <div className="mb-4 flex items-center gap-3">
          <Calendar className="h-8 w-8 text-primary" />
          <h1 className="text-3xl font-bold text-black dark:text-white">Date View</h1>
        </div>
        <p className="text-gray-600 dark:text-gray-300">
          A component to display dates in a consistent and formatted way.
        </p>
      </div>

      <CodeShowcase
        title="Basic Usage"
        code={codeExamples.basic}
        preview={
          <div className="p-4 border rounded-md">
            <DateView date={now} />
          </div>
        }
      />

      <CodeShowcase
        title="Custom Formats"
        code={codeExamples.formats}
        preview={
          <div className="flex flex-col gap-2 p-4 border rounded-md w-full">
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">yyyy-MM-dd</span>
              <DateView date={now} format="yyyy-MM-dd" />
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">MM/dd/yyyy HH:mm</span>
              <DateView date={now} format="MM/dd/yyyy HH:mm" />
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">PPP</span>
              <DateView date={now} format="PPP" />
            </div>
          </div>
        }
      />

      <CodeShowcase
        title="Edge Cases"
        code={codeExamples.invalid}
        preview={
          <div className="flex flex-col gap-2 p-4 border rounded-md w-full">
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">Null Date</span>
              <DateView date={null} />
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">Undefined Date</span>
              <DateView date={undefined} />
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">Invalid Date String</span>
              <DateView date="invalid-date" />
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">String Date (2023-12-25)</span>
              <DateView date={dateString} />
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">Timestamp</span>
              <DateView date={timestamp} />
            </div>
          </div>
        }
      />
    </div>
  )
}
