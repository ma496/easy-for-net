'use client'

import { useState } from 'react'
import { Formik, Form } from 'formik'
import * as Yup from 'yup'
import { DatePicker, FormDatePicker } from '@/components/ui/form'
import { Button, Card, CodeShowcase } from '@/components/ui'
import { Calendar } from 'lucide-react'
import { DateRange } from 'react-day-picker'

const validationSchema = Yup.object({
  birthDate: Yup.date().required('Birth date is required').max(new Date(), 'Birth date cannot be in the future'),
  appointmentDate: Yup.date().required('Appointment date is required').min(new Date(), 'Appointment date must be in the future'),
  eventRange: Yup.object({
    from: Yup.date().required('Start date is required'),
    to: Yup.date().required('End date is required').min(Yup.ref('from'), 'End date must be after start date'),
  }),
  multipleEvents: Yup.array().of(Yup.date()),
  projectDuration: Yup.object({
    from: Yup.date().nullable(),
    to: Yup.date()
      .nullable()
      .when('from', {
        is: (from: Date) => from != undefined,
        then: (schema) => schema.min(Yup.ref('from'), 'End date must be after start date'),
        otherwise: (schema) => schema,
      }),
  }).nullable(),
})

/**
 * Interactive client-side showcase component that demonstrates the DatePicker in single, multiple, and range modes along with Formik integration and a props reference table.
 */
export const DatePickerExample = () => {
  const [basicDate, setBasicDate] = useState<Date>()
  const [multipleDate, setMultipleDate] = useState<Date[]>([])
  const [rangeDate, setRangeDate] = useState<DateRange>()

  const codeExamples = {
    basic: `import { DatePicker } from '@/components/ui/form'

const [date, setDate] = useState<Date>()

<DatePicker
  selected={date}
  onSelect={setDate}
  placeholder="Select a date"
  showIcon={true}
/>`,

    formik: `import { FormDatePicker } from '@/components/ui/form'
import { Formik, Form } from 'formik'

// Single Date Mode
<Formik
  initialValues={{ birthDate: undefined }}
  onSubmit={handleSubmit}
>
  <Form>
    <FormDatePicker
      name="birthDate"
      label="Birth Date"
      placeholder="Select your birth date"
    />
  </Form>
</Formik>

// Multiple Date Mode
<Formik
  initialValues={{ eventDates: [] }}
  onSubmit={handleSubmit}
>
  <Form>
    <FormDatePicker
      name="eventDates"
      mode="multiple"
      label="Event Dates"
      placeholder="Select multiple dates"
    />
  </Form>
</Formik>

// Range Date Mode
<Formik
  initialValues={{ projectDuration: { from: undefined, to: undefined } }}
  onSubmit={handleSubmit}
>
  <Form>
    <FormDatePicker
      name="projectDuration"
      mode="range"
      label="Project Duration"
      placeholder="Select date range"
    />
  </Form>
</Formik>`,

    multiple: `import { DatePicker } from '@/components/ui/form'

const [dates, setDates] = useState<Date[]>([])

<DatePicker
  mode="multiple"
  selected={dates}
  onSelect={setDates}
  placeholder="Select multiple dates"
/>`,

    range: `import { DatePicker } from '@/components/ui/form'

const [range, setRange] = useState<{ from?: Date; to?: Date }>()

<DatePicker
  mode="range"
  selected={range}
  onSelect={setRange}
  placeholder="Select date range"
/>`,
  }

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="mb-8">
        <div className="mb-4 flex items-center gap-3">
          <Calendar className="h-8 w-8 text-primary" />
          <h1 className="text-3xl font-bold text-black dark:text-white">Date Picker Components</h1>
        </div>
        <p className="text-gray-600 dark:text-gray-300">
          Comprehensive date picker components built with react-day-picker, featuring light/dark mode support, Formik integration, and multiple selection modes.
        </p>
      </div>

      {/* Basic Date Picker */}
      <CodeShowcase
        title="Basic Date Picker"
        description="Single date selection with and without icons"
        code={codeExamples.basic}
        preview={
          <div className="grid w-full grid-cols-1 gap-6 lg:grid-cols-2">
            <div className="space-y-4">
              <h3 className="font-medium">Single Date Selection</h3>
              <DatePicker name="basicDate" selected={basicDate} onSelect={(date) => setBasicDate(date)} placeholder="Select a date" label="Event Date" />
              {basicDate && <p className="text-sm text-gray-600 dark:text-gray-300">Selected: {basicDate.toDateString()}</p>}
            </div>

            <div className="space-y-4">
              <h3 className="font-medium">With Icon Disabled</h3>
              <DatePicker name="basicDateNoIcon" selected={basicDate} onSelect={(date) => setBasicDate(date)} placeholder="Select without icon" showIcon={false} label="Simple Date Picker" />
            </div>
          </div>
        }
      />

      {/* Multiple Date Selection */}
      <CodeShowcase
        title="Multiple Date Selection"
        description="Select multiple dates from the calendar"
        code={codeExamples.multiple}
        preview={
          <div className="w-full space-y-4">
            <DatePicker name="multipleDates" mode="multiple" selected={multipleDate} onSelect={(dates) => setMultipleDate(dates || [])} placeholder="Select multiple dates" label="Event Dates" />
            {multipleDate.length > 0 && (
              <div className="text-sm text-gray-600 dark:text-gray-300">
                <p className="font-medium">Selected dates:</p>
                <ul className="mt-2 space-y-1">
                  {multipleDate.map((date, index) => (
                    <li key={index}>• {date.toDateString()}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        }
      />

      {/* Range Date Selection */}
      <CodeShowcase
        title="Date Range Selection"
        description="Select a date range with start and end dates"
        code={codeExamples.range}
        preview={
          <div className="w-full space-y-4">
            <DatePicker name="rangeDate" mode="range" selected={rangeDate} onSelect={(range) => setRangeDate(range)} placeholder="Select date range" label="Event Duration" />
            {rangeDate?.from && (
              <div className="text-sm text-gray-600 dark:text-gray-300">
                <p className="font-medium">Selected range:</p>
                <p className="mt-1">
                  From: {rangeDate.from.toDateString()}
                  {rangeDate.to && ` → To: ${rangeDate.to.toDateString()}`}
                </p>
              </div>
            )}
          </div>
        }
      />

      {/* Formik Integration */}
      <CodeShowcase
        title="Formik Integration"
        description="Date pickers with form validation and state management"
        code={codeExamples.formik}
        preview={
          <div className="w-full">
            <Formik
              initialValues={{
                birthDate: undefined,
                appointmentDate: undefined,
                eventRange: { from: undefined, to: undefined },
                multipleEvents: undefined,
                projectDuration: undefined,
              }}
              validationSchema={validationSchema}
              onSubmit={(values, { setSubmitting }) => {
                console.log('Form submitted:', values)
                alert('Form submitted! Check console for values.')
                setSubmitting(false)
              }}
            >
              {({ isSubmitting, values }) => (
                <Form className="space-y-6">
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <FormDatePicker name="birthDate" label="Birth Date" placeholder="Select your birth date" required={true} />

                    <FormDatePicker name="appointmentDate" label="Appointment Date" placeholder="Select appointment date" required={true} />
                  </div>

                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <FormDatePicker name="multipleEvents" mode="multiple" label="Event Dates (Multiple)" placeholder="Select multiple event dates" />

                    <FormDatePicker name="projectDuration" mode="range" label="Project Duration (Range)" placeholder="Select project date range" />
                  </div>

                  <div className="flex gap-4">
                    <Button type="submit" disabled={isSubmitting}>
                      {isSubmitting ? 'Submitting...' : 'Submit Form'}
                    </Button>
                    <Button type="button" variant="outline" onClick={() => console.log('Current values:', values)}>
                      Log Values
                    </Button>
                  </div>
                </Form>
              )}
            </Formik>
          </div>
        }
      />

      {/* Props Documentation */}
      <Card className="p-6">
        <h2 className="mb-6 text-xl font-semibold">Component Props</h2>

        <div className="space-y-8">
          <div>
            <h3 className="mb-4 text-lg font-medium">DatePicker Props</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-200 dark:border-gray-700">
                    <th className="p-2 text-left">Prop</th>
                    <th className="p-2 text-left">Type</th>
                    <th className="p-2 text-left">Default</th>
                    <th className="p-2 text-left">Description</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr>
                    <td className="p-2 font-mono">selected</td>
                    <td className="p-2">Date | undefined</td>
                    <td className="p-2">undefined</td>
                    <td className="p-2">Currently selected date(s)</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">onSelect</td>
                    <td className="p-2">Function</td>
                    <td className="p-2">undefined</td>
                    <td className="p-2">Callback when date is selected</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">placeholder</td>
                    <td className="p-2">string</td>
                    <td className="p-2">&quot;Select date...&quot;</td>
                    <td className="p-2">Placeholder text</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">disabled</td>
                    <td className="p-2">boolean</td>
                    <td className="p-2">false</td>
                    <td className="p-2">Disable the date picker</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">showIcon</td>
                    <td className="p-2">boolean</td>
                    <td className="p-2">true</td>
                    <td className="p-2">Show calendar icon</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">mode</td>
                    <td className="p-2">&#39;single&#39; | &#39;multiple&#39; | &#39;range&#39;</td>
                    <td className="p-2">&#39;single&#39;</td>
                    <td className="p-2">Selection mode</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">label</td>
                    <td className="p-2">string</td>
                    <td className="p-2">undefined</td>
                    <td className="p-2">Optional label text</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">required</td>
                    <td className="p-2">boolean</td>
                    <td className="p-2">false</td>
                    <td className="p-2">Display required asterisk</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div>
            <h3 className="mb-4 text-lg font-medium">FormDatePicker Props</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-200 dark:border-gray-700">
                    <th className="p-2 text-left">Prop</th>
                    <th className="p-2 text-left">Type</th>
                    <th className="p-2 text-left">Default</th>
                    <th className="p-2 text-left">Description</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr>
                    <td className="p-2 font-mono">name</td>
                    <td className="p-2">string</td>
                    <td className="p-2">required</td>
                    <td className="p-2">Field name for Formik</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">mode</td>
                    <td className="p-2">&#39;single&#39; | &#39;multiple&#39; | &#39;range&#39;</td>
                    <td className="p-2">&#39;single&#39;</td>
                    <td className="p-2">Selection mode</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">showValidation</td>
                    <td className="p-2">boolean</td>
                    <td className="p-2">true</td>
                    <td className="p-2">Show validation errors</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">required</td>
                    <td className="p-2">boolean</td>
                    <td className="p-2">false</td>
                    <td className="p-2">Display required asterisk</td>
                  </tr>
                  <tr>
                    <td className="p-2 font-mono">...DatePickerProps</td>
                    <td className="p-2">-</td>
                    <td className="p-2">-</td>
                    <td className="p-2">All DatePicker props except selected/onSelect</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div className="mt-6">
              <h4 className="mb-3 font-medium">Expected Field Value Types</h4>
              <div className="space-y-2 text-sm">
                <div>
                  <span className="font-mono">mode=&quot;single&quot;</span>: <code>Date | undefined</code>
                </div>
                <div>
                  <span className="font-mono">mode=&quot;multiple&quot;</span>: <code>Date[] | undefined</code>
                </div>
                <div>
                  <span className="font-mono">mode=&quot;range&quot;</span>: <code>{'{ from?: Date; to?: Date } | undefined'}</code>
                </div>
              </div>
            </div>
          </div>
        </div>
      </Card>
    </div>
  )
}
