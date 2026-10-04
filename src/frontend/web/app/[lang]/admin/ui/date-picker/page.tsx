import { getServerTranslation } from '@/i18n'
import { DatePickerExample } from './_components/date-picker-example'
import { AdminPageContent } from '@/components/layouts'

/**
 * Props for the date picker showcase page, providing the localized route lang segment.
 */
interface DatePickerPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the DatePicker and FormDatePicker components.
 * Resolves the localized title and renders the interactive date picker example inside the admin page shell.
 */
const DatePickerPage = async ({ params }: DatePickerPageProps) => {
  const { lang } = await params
  const title = await getServerTranslation(lang, 'page.ui.datePicker.title')

  return (
    <AdminPageContent title={title} description="Single, multiple and range date selection, standalone or bound to Formik." plain>
      <DatePickerExample />
    </AdminPageContent>
  )
}

export default DatePickerPage
