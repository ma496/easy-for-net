'use client'

import { useState } from 'react'
import { Formik, Form, ErrorMessage } from 'formik'
import * as Yup from 'yup'

import {
  FormInput,
  FormTextarea,
  FormPasswordInput,
  FormSelect,
  FormMultiSelect,
  FormCheckbox,
  FormRadio,
  FormDatePicker,
  Input,
  Textarea,
  PasswordInput,
  Select,
  MultiSelect,
  Checkbox,
  Radio,
  DatePicker,
} from '@/components/ui/form'
import { Button, Card, CardHeader, CardTitle, CodeShowcase } from '@/components/ui'
import { ShowcasePreview } from '../../_components/showcase-preview'
import { CheckIcon, Lock, User, Mail, MapPin, Tag, Briefcase } from 'lucide-react'

const validationSchema = Yup.object({
  username: Yup.string().required('Username is required').min(3, 'Min 3 characters'),
  email: Yup.string().email('Invalid email').required('Email is required'),
  password: Yup.string().required('Password is required').min(8, 'Min 8 characters'),
  bio: Yup.string().max(500, 'Max 500 characters'),
  country: Yup.string().required('Country is required'),
  skills: Yup.array().min(1, 'Select at least one skill'),
  newsletter: Yup.boolean(),
  gender: Yup.string().required('Gender is required'),
  birthDate: Yup.date().required('Birth date is required').max(new Date(), 'Cannot be future date'),
  projectType: Yup.string().required('Project type is required'),
})

/**
 * Interactive client-side showcase component that demonstrates both Formik-integrated and standalone form components, including a full validated form, variant comparisons, and disabled-state examples.
 */
export const FormElementsExample = () => {
  // General component states
  const [generalInput, setGeneralInput] = useState('')
  const [generalTextarea, setGeneralTextarea] = useState('')
  const [generalPassword, setGeneralPassword] = useState('')
  const [generalSelect, setGeneralSelect] = useState('')
  const [generalMultiSelect, setGeneralMultiSelect] = useState<string[]>([])
  const [generalCheckbox, setGeneralCheckbox] = useState(false)
  const [generalRadio, setGeneralRadio] = useState('')

  // Demo select states
  const [basicSelect, setBasicSelect] = useState('')
  const [iconSearchSelect, setIconSearchSelect] = useState('')
  const [largeSelect, setLargeSelect] = useState('')
  const [multiSelectDemo, setMultiSelectDemo] = useState<string[]>([])

  // Error demo states
  const [errorSelect, setErrorSelect] = useState('')
  const [errorMultiSelect, setErrorMultiSelect] = useState<string[]>([])

  // Sample data
  const countries = [
    { label: 'United States', value: 'us' },
    { label: 'Canada', value: 'ca' },
    { label: 'United Kingdom', value: 'uk' },
    { label: 'Germany', value: 'de' },
    { label: 'France', value: 'fr' },
    { label: 'Japan', value: 'jp' },
    { label: 'Australia', value: 'au' },
  ]

  const skills = [
    { label: 'JavaScript', value: 'javascript' },
    { label: 'TypeScript', value: 'typescript' },
    { label: 'React', value: 'react' },
    { label: 'Vue.js', value: 'vue' },
    { label: 'Angular', value: 'angular' },
    { label: 'Node.js', value: 'nodejs' },
    { label: 'Python', value: 'python' },
    { label: 'Java', value: 'java' },
  ]

  const projectTypes = [
    { label: 'Web Application', value: 'web' },
    { label: 'Mobile App', value: 'mobile' },
    { label: 'Desktop Software', value: 'desktop' },
    { label: 'API/Backend', value: 'api' },
  ]

  const codeExamples = {
    formComponents: `// Form Components (with Formik integration)
import { FormInput, FormTextarea, FormPasswordInput } from '@/components/ui/form'
import { FormSelect, FormMultiSelect } from '@/components/ui/form'
import { FormCheckbox, FormRadio, FormDatePicker } from '@/components/ui/form'
import { Formik, Form } from 'formik'

<Formik
  initialValues={{
    username: '',
    email: '',
    password: '',
    bio: '',
    country: '',
    skills: [],
    newsletter: false,
    gender: '',
    birthDate: undefined
  }}
  validationSchema={validationSchema}
  onSubmit={handleSubmit}
>
  <Form>
    <FormInput name="username" label="Username" placeholder="Enter username" />
    <FormTextarea name="bio" label="Biography" rows={4} />
    <FormPasswordInput name="password" label="Password" />
    <FormSelect name="country" label="Country" options={countries} />
    <FormMultiSelect name="skills" label="Skills" options={skills} />
    <FormCheckbox name="newsletter" label="Subscribe to newsletter" />
    <FormRadio name="gender" value="male" label="Male" />
    <FormDatePicker name="birthDate" label="Birth Date" />
  </Form>
</Formik>`,

    generalComponents: `// General Components (standalone, no Formik)
import { Input, Textarea, PasswordInput } from '@/components/ui/form'
import { Select, MultiSelect } from '@/components/ui/form'
import { Checkbox, Radio } from '@/components/ui/form'

const [value, setValue] = useState("")
const [multiValue, setMultiValue] = useState([])

<Input
  value={value}
  onChange={(e) => setValue(e.target.value)}
  label="Username"
  placeholder="Enter username"
/>
<Textarea
  value={value}
  onChange={(e) => setValue(e.target.value)}
  label="Description"
/>
<Select
  value={value}
  onChange={setValue}
  options={options}
  label="Country"
/>
<MultiSelect
  value={multiValue}
  onChange={setMultiValue}
  options={options}
  label="Skills"
/>
<Checkbox
  checked={checked}
  onChange={(e) => setChecked(e.target.checked)}
  label="Accept terms"
/>`,

    inputVariants: `// Input variations
<Input
  label="Basic Input"
  placeholder="Type something..."
/>

<Input
  label="With Icon"
  placeholder="Email address..."
  icon={<Mail className="h-4 w-4" />}
/>

<Input
  label="With Error"
  placeholder="Username..."
  error="Username is required"
/>

<Input
  disabled
  label="Disabled Input"
  placeholder="Cannot edit..."
/>`,

    selectVariants: `// Select variations
<Select
  label="Basic Select"
  options={options}
  placeholder="Choose option..."
/>

<Select
  label="With Icon & Search"
  options={options}
  icon={<MapPin className="h-4 w-4" />}
  searchable={true}
/>

<Select
  label="Different Sizes"
  options={options}
  size="lg"
  clearable={false}
/>`,

    errorVariants: `// Pass error to show the error state; Select also needs touched
<Input
  name="email"
  label="Email"
  error="Invalid email"
/>

<PasswordInput name="password" label="Password" error="Min 8 characters" />

<Textarea name="bio" label="Biography" error="Max 500 characters" />

<Select
  name="country"
  label="Country"
  options={options}
  value={value}
  onChange={(name, value) => setValue(value)}
  error="Country is required"
  touched
/>

<MultiSelect name="skills" label="Skills" options={options} value={values} onChange={setValues} error="Select at least one skill" />

<Checkbox name="terms" label="Accept terms" error="You must accept the terms" />`,
  }

  return (
    <div className="space-y-6">
      {/* Complete Formik Form */}
      <CodeShowcase
        title="Complete Formik form"
        description="Every control bound to Formik with Yup rules. Submit it empty to see each field's error state."
        code={codeExamples.formComponents}
        preview={
          <ShowcasePreview stack>
            <Formik
              initialValues={{
                username: '',
                email: '',
                password: '',
                bio: '',
                country: '',
                skills: [],
                newsletter: false,
                gender: '',
                birthDate: undefined,
                projectType: '',
              }}
              validationSchema={validationSchema}
              onSubmit={(values, { setSubmitting }) => {
                alert(`Form submitted successfully!\n\nValues:\n${JSON.stringify(values, undefined, 2)}`)
                setSubmitting(false)
              }}
            >
              {({ isSubmitting, values }) => (
                <Form className="space-y-6">
                  <FormSection title="Account" description="How the person signs in.">
                    <FormInput name="username" label="Username" placeholder="Enter your username" icon={<User className="h-4 w-4" />} required={true} />
                    <FormInput name="email" type="email" label="Email address" placeholder="Enter your email" icon={<Mail className="h-4 w-4" />} required={true} />
                    <FormPasswordInput name="password" label="Password" placeholder="Enter your password" icon={<Lock className="h-4 w-4" />} required={true} />
                    <FormSelect name="country" label="Country" options={countries} placeholder="Select your country" icon={<MapPin className="h-4 w-4" />} required={true} />
                  </FormSection>

                  <FormSection title="Profile" description="What others see about them.">
                    <FormTextarea name="bio" label="Biography" placeholder="Tell us about yourself (optional)" rows={4} className="sm:col-span-2" />
                    <FormMultiSelect name="skills" label="Skills" options={skills} placeholder="Select your skills" icon={<Tag className="h-4 w-4" />} required={true} />
                    <FormSelect name="projectType" label="Project type" options={projectTypes} placeholder="Select project type" icon={<Briefcase className="h-4 w-4" />} required={true} />
                    <FormDatePicker name="birthDate" label="Birth date" placeholder="Select your birth date" required={true} />
                    <fieldset className="space-y-2">
                      <legend className="form-label">
                        Gender <span className="text-danger">*</span>
                      </legend>
                      <div className="flex flex-wrap gap-x-6 gap-y-2 pt-1">
                        <FormRadio name="gender" value="male" label="Male" showValidation={false} />
                        <FormRadio name="gender" value="female" label="Female" showValidation={false} />
                        <FormRadio name="gender" value="other" label="Other" showValidation={false} />
                      </div>
                      {/* One message for the group rather than one beside every radio. */}
                      <ErrorMessage name="gender">{(message) => <div className="text-xs font-medium text-danger">{message}</div>}</ErrorMessage>
                    </fieldset>
                    <FormCheckbox name="newsletter" label="Subscribe to newsletter and updates" className="sm:col-span-2" />
                  </FormSection>

                  <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-5">
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => {
                        const valuesText = JSON.stringify(values, undefined, 2)
                        alert(`Current values:\n\n${valuesText}`)
                      }}
                    >
                      Log values
                    </Button>
                    <Button type="submit" isLoading={isSubmitting}>
                      Submit form
                    </Button>
                  </div>

                  {/* Current Values Display */}
                  <ValuesWell title="Current form values" value={values} />
                </Form>
              )}
            </Formik>
          </ShowcasePreview>
        }
      />

      {/* General Components Demo */}
      <CodeShowcase
        title="Standalone components"
        description="The same controls without Formik, driven by your own state."
        code={codeExamples.generalComponents}
        preview={
          <ShowcasePreview stack>
            <div className="grid gap-5 sm:grid-cols-2">
              <Input name="general-username" label="Username" placeholder="Enter username" value={generalInput} onChange={(e) => setGeneralInput(e.target.value)} icon={<User className="h-4 w-4" />} />

              <PasswordInput
                name="general-password"
                label="Password"
                placeholder="Enter password"
                value={generalPassword}
                onChange={(e) => setGeneralPassword(e.target.value)}
                icon={<Lock className="h-4 w-4" />}
              />

              <Textarea
                name="general-description"
                label="Description"
                placeholder="Enter description"
                value={generalTextarea}
                onChange={(e) => setGeneralTextarea(e.target.value)}
                rows={3}
                className="sm:col-span-2"
              />

              <Select
                name="country"
                label="Country"
                options={countries}
                value={generalSelect}
                onChange={(name, value) => setGeneralSelect(value)}
                placeholder="Select country"
                icon={<MapPin className="h-4 w-4" />}
              />

              <MultiSelect
                name="general-skills"
                label="Skills"
                options={skills}
                value={generalMultiSelect}
                onChange={setGeneralMultiSelect}
                placeholder="Select skills"
                icon={<Tag className="h-4 w-4" />}
              />
            </div>

            <div className="flex flex-wrap items-center gap-x-8 gap-y-3">
              <Checkbox name="general-accept-terms" label="Accept terms and conditions" checked={generalCheckbox} onChange={(e) => setGeneralCheckbox(e.target.checked)} />

              <div className="flex flex-wrap gap-x-6 gap-y-2">
                <Radio name="demo-radio" value="option1" label="Option 1" checked={generalRadio === 'option1'} onChange={(e) => setGeneralRadio(e.target.value)} />
                <Radio name="demo-radio" value="option2" label="Option 2" checked={generalRadio === 'option2'} onChange={(e) => setGeneralRadio(e.target.value)} />
              </div>
            </div>

            <ValuesWell
              title="Current component states"
              value={{
                input: generalInput,
                textarea: generalTextarea,
                password: generalPassword ? '***hidden***' : '',
                select: generalSelect,
                multiSelect: generalMultiSelect,
                checkbox: generalCheckbox,
                radio: generalRadio,
              }}
            />
          </ShowcasePreview>
        }
      />

      {/* Component Variants */}
      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="Input variants"
          description="Plain, with an icon, in error and disabled."
          code={codeExamples.inputVariants}
          preview={
            <ShowcasePreview stack>
              <Input name="variant-basic" label="Basic input" placeholder="Type something..." />

              <Input name="variant-with-icon" label="With icon" placeholder="Email address..." icon={<Mail className="h-4 w-4" />} />

              <Input name="variant-with-error" label="With error" placeholder="Username..." error="Username is required" defaultValue="invalid" />

              <Input name="variant-disabled" disabled label="Disabled input" placeholder="Cannot edit..." defaultValue="Read only" />
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Select variants"
          description="Plain, searchable with an icon, large without a clear button, and multi-select."
          code={codeExamples.selectVariants}
          preview={
            <ShowcasePreview stack>
              <Select name="basic-select" label="Basic select" options={countries.slice(0, 4)} value={basicSelect} onChange={(name, value) => setBasicSelect(value)} placeholder="Choose option..." />

              <Select
                name="icon-search-select"
                label="With icon and search"
                options={countries}
                value={iconSearchSelect}
                onChange={(name, value) => setIconSearchSelect(value)}
                icon={<MapPin className="h-4 w-4" />}
                searchable={true}
                placeholder="Search countries..."
              />

              <Select
                name="large-select"
                label="Large size, no clear"
                options={countries.slice(0, 3)}
                value={largeSelect}
                onChange={(name, value) => setLargeSelect(value)}
                size="lg"
                clearable={false}
                placeholder="Select country..."
              />

              <MultiSelect name="multi-select-variants" label="Multi-select" options={skills.slice(0, 5)} value={multiSelectDemo} onChange={setMultiSelectDemo} placeholder="Select multiple..." />
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Validation errors"
          description="Every control's error state: a danger border and the message under the field."
          code={codeExamples.errorVariants}
          preview={
            <ShowcasePreview stack>
              <Input name="error-email" label="Email" placeholder="you@example.com" icon={<Mail className="h-4 w-4" />} error="Invalid email" defaultValue="not-an-email" required />

              <PasswordInput name="error-password" label="Password" placeholder="Enter password" icon={<Lock className="h-4 w-4" />} error="Min 8 characters" defaultValue="short" required />

              <Textarea name="error-bio" label="Biography" rows={2} error="Max 500 characters" defaultValue="A biography that ran far past its limit..." />

              <Select
                name="error-country"
                label="Country"
                options={countries}
                value={errorSelect}
                onChange={(name, value) => setErrorSelect(value)}
                placeholder="Select country"
                error={errorSelect ? null : 'Country is required'}
                touched
                required
              />

              <MultiSelect
                name="error-skills"
                label="Skills"
                options={skills}
                value={errorMultiSelect}
                onChange={setErrorMultiSelect}
                placeholder="Select skills"
                error={errorMultiSelect.length ? undefined : 'Select at least one skill'}
                required
              />

              <Checkbox name="error-terms" label="Accept terms and conditions" error="You must accept the terms" />
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Disabled"
          description="Disabled state for every standalone control."
          code={`<Textarea disabled label="Disabled textarea" />
<Select disabled label="Disabled select" ... />
<MultiSelect disabled label="Disabled multi-select" ... />
<DatePicker disabled label="Disabled date picker" ... />
<Checkbox disabled checked label="Disabled checkbox" />
<Radio disabled checked label="Disabled radio" />`}
          preview={
            <ShowcasePreview stack>
              <Textarea name="disabled-textarea" disabled label="Disabled textarea" placeholder="Cannot type here..." rows={2} />

              <Select name="disabled-select" disabled label="Disabled select" options={countries} value={countries[0].value} onChange={() => {}} />

              <MultiSelect name="disabled-multiselect" disabled label="Disabled multi-select" options={skills} value={[skills[0].value, skills[1].value]} onChange={() => {}} />

              <DatePicker name="disabled-date" label="Disabled date picker" disabled={true} selected={new Date()} onSelect={() => {}} />

              <div className="flex flex-wrap gap-x-6 gap-y-2 pt-1">
                <Checkbox name="disabled-checkbox" disabled checked label="Disabled checkbox" />
                <Radio name="disabled-radio" disabled checked value="on" label="Disabled radio" onChange={() => {}} />
              </div>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          className="xl:col-span-2"
          title="Disabled (Formik)"
          description="Disabled state for the Formik-bound controls."
          code={`<Formik initialValues={{...}} onSubmit={...}>
  <Form>
    <FormInput name="disabledInput" disabled label="Disabled FormInput" />
    <FormSelect name="disabledSelect" disabled label="Disabled FormSelect" options={countries} />
    {/* ... other components */}
  </Form>
</Formik>`}
          preview={
            <ShowcasePreview stack>
              <Formik
                initialValues={{
                  disabledInput: '',
                  disabledTextarea: '',
                  disabledSelect: countries[0].value,
                  disabledMultiSelect: [skills[0].value, skills[1].value],
                  disabledDate: new Date(),
                  disabledCheckbox: true,
                  disabledRadio: 'on',
                }}
                onSubmit={() => {}}
              >
                <Form className="grid w-full gap-5 sm:grid-cols-2">
                  <FormInput name="disabledInput" disabled label="Disabled FormInput" placeholder="Cannot type here..." />

                  <FormSelect name="disabledSelect" disabled label="Disabled FormSelect" options={countries} />

                  <FormTextarea name="disabledTextarea" disabled label="Disabled FormTextarea" placeholder="Cannot type here..." rows={2} className="sm:col-span-2" />

                  <FormMultiSelect name="disabledMultiSelect" disabled label="Disabled FormMultiSelect" options={skills} />

                  <FormDatePicker name="disabledDate" label="Disabled FormDatePicker" disabled={true} />

                  <div className="flex flex-wrap gap-x-6 gap-y-2">
                    <FormCheckbox name="disabledCheckbox" disabled label="Disabled FormCheckbox" />
                    <FormRadio name="disabledRadio" disabled value="on" label="Disabled FormRadio" />
                  </div>
                </Form>
              </Formik>
            </ShowcasePreview>
          }
        />
      </div>

      {/* Component Props Documentation */}
      <Card className="w-full">
        <CardHeader>
          <CardTitle>Components overview</CardTitle>
          <p className="text-sm text-muted-foreground">Formik-bound controls read and write their field by name; standalone ones are controlled by your own state.</p>
        </CardHeader>
        <div className="space-y-6 px-5 pb-5 sm:px-6 sm:pb-6">
          <ComponentGrid
            title="Form components (Formik)"
            items={[
              { name: 'FormInput', description: 'Text input with validation' },
              { name: 'FormTextarea', description: 'Multi-line text input' },
              { name: 'FormPasswordInput', description: 'Password with visibility toggle' },
              { name: 'FormSelect', description: 'Single-select dropdown' },
              { name: 'FormMultiSelect', description: 'Multi-select dropdown' },
              { name: 'FormCheckbox', description: 'Checkbox with variants' },
              { name: 'FormRadio', description: 'Radio button selection' },
              { name: 'FormDatePicker', description: 'Date selection component' },
            ]}
          />

          <ComponentGrid
            title="General components (standalone)"
            items={[
              { name: 'Input', description: 'Basic text input' },
              { name: 'Textarea', description: 'Multi-line text input' },
              { name: 'PasswordInput', description: 'Password input' },
              { name: 'Select', description: 'Single-select dropdown' },
              { name: 'MultiSelect', description: 'Multi-select dropdown' },
              { name: 'Checkbox', description: 'Checkbox input' },
              { name: 'Radio', description: 'Radio button' },
              { name: 'DatePicker', description: 'Single, multiple or range dates' },
            ]}
          />

          <div className="space-y-3">
            <h3 className="text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">Key features</h3>
            <ul className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
              {[
                ['Formik integration', 'Seamless form state management'],
                ['Validation support', 'Built-in error handling and display'],
                ['Accessibility', 'Labelled controls with proper focus management'],
                ['Dark mode', 'Full support for light and dark themes'],
                ['Customizable', 'Size variants and styling options'],
                ['Icons', 'Easy icon integration'],
                ['Search', 'Built-in search for select components'],
                ['TypeScript', 'Full type safety and IntelliSense'],
              ].map(([feature, detail]) => (
                <li key={feature} className="flex items-start gap-2">
                  <CheckIcon className="mt-0.5 size-4 shrink-0 text-success" />
                  <span className="text-muted-foreground">
                    <span className="font-medium text-foreground">{feature}:</span> {detail}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </Card>
    </div>
  )
}

/** Props for FormSection, one titled group of fields in the complete form example. */
interface FormSectionProps {
  title: string
  description: string
  children: React.ReactNode
}

/**
 * FormSection groups fields under a title and a muted line: the heading beside a two-column field grid from lg up, stacked above it below, separated from the previous section by a hairline.
 */
const FormSection = ({ title, description, children }: FormSectionProps) => (
  <section className="grid gap-5 border-t border-border pt-6 first:border-t-0 first:pt-0 lg:grid-cols-[12rem_1fr] lg:gap-8">
    <div>
      <h3 className="text-sm font-semibold text-foreground">{title}</h3>
      <p className="mt-1 text-[13px] text-muted-foreground">{description}</p>
    </div>
    <div className="grid min-w-0 gap-5 sm:grid-cols-2">{children}</div>
  </section>
)

/**
 * ValuesWell prints a value as formatted JSON in an inset well, so the live state of an example can be read beside it.
 */
const ValuesWell = ({ title, value }: { title: string; value: unknown }) => (
  <div className="rounded-lg border border-border bg-surface-2 p-4">
    <h4 className="mb-2 text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{title}</h4>
    <pre className="overflow-x-auto font-mono text-xs leading-relaxed text-muted-foreground">{JSON.stringify(value, undefined, 2)}</pre>
  </div>
)

/**
 * ComponentGrid lists components as small name / description tiles under an overline heading.
 */
const ComponentGrid = ({ title, items }: { title: string; items: { name: string; description: string }[] }) => (
  <div className="space-y-3">
    <h3 className="text-[11px] font-semibold tracking-wider text-subtle-foreground uppercase">{title}</h3>
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {items.map((item) => (
        <div key={item.name} className="rounded-lg border border-border bg-surface-2 p-3">
          <h4 className="font-mono text-[13px] font-medium text-foreground">{item.name}</h4>
          <p className="mt-1 text-xs text-muted-foreground">{item.description}</p>
        </div>
      ))}
    </div>
  </div>
)
