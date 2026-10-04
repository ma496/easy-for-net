'use client'

import { useState } from 'react'
import { Formik, Form } from 'formik'
import * as Yup from 'yup'
import { DatePicker, FormDatePicker } from '@/components/ui/form'
import { Button, Card, CardHeader, CardTitle, CodeShowcase } from '@/components/ui'
import { ShowcasePreview } from '../../_components/showcase-preview'
import { PropsTable } from '../../_components/props-table'
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

import { DateRange } from 'react-day-picker'

const [range, setRange] = useState<DateRange>()

<DatePicker
  mode="range"
  selected={range}
  onSelect={setRange}
  placeholder="Select date range"
/>`,
  }

  return (
    <div className="space-y-6">
      {/* Basic Date Picker */}
      <CodeShowcase
        title="Basic date picker"
        description="Single date selection, with and without the calendar icon."
        code={codeExamples.basic}
        preview={
          <ShowcasePreview stack>
            <div className="grid w-full gap-5 md:grid-cols-2">
              <div className="space-y-2">
                <DatePicker name="basicDate" selected={basicDate} onSelect={(date) => setBasicDate(date)} placeholder="Select a date" label="Event date" />
                {basicDate && <p className="text-xs text-muted-foreground">Selected: {basicDate.toDateString()}</p>}
              </div>
              <DatePicker name="basicDateNoIcon" selected={basicDate} onSelect={(date) => setBasicDate(date)} placeholder="Select without icon" showIcon={false} label="Without icon" />
            </div>
          </ShowcasePreview>
        }
      />

      <div className="grid items-start gap-6 xl:grid-cols-2">
        {/* Multiple Date Selection */}
        <CodeShowcase
          title="Multiple dates"
          description="Pick any number of separate dates."
          code={codeExamples.multiple}
          preview={
            <ShowcasePreview stack>
              <DatePicker name="multipleDates" mode="multiple" selected={multipleDate} onSelect={(dates) => setMultipleDate(dates || [])} placeholder="Select multiple dates" label="Event dates" />
              {multipleDate.length > 0 && (
                <div className="space-y-2">
                  <p className="text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">Selected dates</p>
                  <ul className="flex flex-wrap gap-1.5">
                    {multipleDate.map((date, index) => (
                      <li key={index} className="badge badge-primary">
                        {date.toDateString()}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </ShowcasePreview>
          }
        />

        {/* Range Date Selection */}
        <CodeShowcase
          title="Date range"
          description="A start and an end date in one control."
          code={codeExamples.range}
          preview={
            <ShowcasePreview stack>
              <DatePicker name="rangeDate" mode="range" selected={rangeDate} onSelect={(range) => setRangeDate(range)} placeholder="Select date range" label="Event duration" />
              {rangeDate?.from && (
                <div className="space-y-1">
                  <p className="text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">Selected range</p>
                  <p className="text-sm text-foreground tabular-nums">
                    {rangeDate.from.toDateString()}
                    {rangeDate.to && ` → ${rangeDate.to.toDateString()}`}
                  </p>
                </div>
              )}
            </ShowcasePreview>
          }
        />
      </div>

      {/* Formik Integration */}
      <CodeShowcase
        title="Formik integration and validation"
        description="Bound to Formik with Yup rules. Submit the empty form to see the error state."
        code={codeExamples.formik}
        preview={
          <ShowcasePreview stack>
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
                <Form className="space-y-5">
                  <div className="grid gap-5 sm:grid-cols-2">
                    <FormDatePicker name="birthDate" label="Birth date" placeholder="Select your birth date" required={true} />
                    <FormDatePicker name="appointmentDate" label="Appointment date" placeholder="Select appointment date" required={true} />
                    <FormDatePicker name="multipleEvents" mode="multiple" label="Event dates (multiple)" placeholder="Select multiple event dates" />
                    <FormDatePicker name="projectDuration" mode="range" label="Project duration (range)" placeholder="Select project date range" />
                  </div>

                  <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-5">
                    <Button type="button" variant="outline" onClick={() => console.log('Current values:', values)}>
                      Log values
                    </Button>
                    <Button type="submit" isLoading={isSubmitting}>
                      Submit form
                    </Button>
                  </div>
                </Form>
              )}
            </Formik>
          </ShowcasePreview>
        }
      />

      {/* Props Documentation */}
      <Card className="w-full">
        <CardHeader>
          <CardTitle>DatePicker props</CardTitle>
          <p className="text-sm text-muted-foreground">The standalone, controlled picker.</p>
        </CardHeader>
        <div className="px-5 pb-5 sm:px-6 sm:pb-6">
          <PropsTable
            rows={[
              { name: 'selected', type: 'Date | Date[] | DateRange', defaultValue: 'undefined', description: 'Currently selected date(s), by mode' },
              { name: 'onSelect', type: 'Function', defaultValue: 'undefined', description: 'Callback when a date is selected' },
              { name: 'placeholder', type: 'string', defaultValue: '"Select date..."', description: 'Placeholder text' },
              { name: 'disabled', type: 'boolean', defaultValue: 'false', description: 'Disable the date picker' },
              { name: 'showIcon', type: 'boolean', defaultValue: 'true', description: 'Show the calendar icon' },
              { name: 'mode', type: "'single' | 'multiple' | 'range'", defaultValue: "'single'", description: 'Selection mode' },
              { name: 'label', type: 'string', defaultValue: 'undefined', description: 'Optional label text' },
              { name: 'required', type: 'boolean', defaultValue: 'false', description: 'Display the required asterisk' },
            ]}
          />
        </div>
      </Card>

      <Card className="w-full">
        <CardHeader>
          <CardTitle>FormDatePicker props</CardTitle>
          <p className="text-sm text-muted-foreground">The Formik-bound picker; it reads and writes the field its name points at.</p>
        </CardHeader>
        <div className="space-y-5 px-5 pb-5 sm:px-6 sm:pb-6">
          <PropsTable
            rows={[
              { name: 'name', type: 'string', defaultValue: 'required', description: 'Field name for Formik' },
              { name: 'mode', type: "'single' | 'multiple' | 'range'", defaultValue: "'single'", description: 'Selection mode' },
              { name: 'showValidation', type: 'boolean', defaultValue: 'true', description: 'Show validation errors' },
              { name: 'required', type: 'boolean', defaultValue: 'false', description: 'Display the required asterisk' },
              { name: '...DatePickerProps', type: '-', defaultValue: '-', description: 'All DatePicker props except selected/onSelect' },
            ]}
          />

          <div className="space-y-2">
            <h4 className="text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">Expected field value types</h4>
            <dl className="grid gap-x-4 gap-y-1 sm:grid-cols-[10rem_1fr] sm:gap-y-2">
              <dt className="font-mono text-[13px] text-foreground">mode=&quot;single&quot;</dt>
              <dd className="mb-2 font-mono text-[13px] text-muted-foreground sm:mb-0">Date | undefined</dd>
              <dt className="font-mono text-[13px] text-foreground">mode=&quot;multiple&quot;</dt>
              <dd className="mb-2 font-mono text-[13px] text-muted-foreground sm:mb-0">Date[] | undefined</dd>
              <dt className="font-mono text-[13px] text-foreground">mode=&quot;range&quot;</dt>
              <dd className="font-mono text-[13px] text-muted-foreground">{'{ from?: Date; to?: Date } | undefined'}</dd>
            </dl>
          </div>
        </div>
      </Card>
    </div>
  )
}
