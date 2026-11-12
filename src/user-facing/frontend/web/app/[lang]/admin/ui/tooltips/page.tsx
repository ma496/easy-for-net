import { getServerTranslation } from '@/i18n'
import { TooltipExample } from './_components/tooltip-example'
import { PageContent } from '@/components/layouts'

/**
 * Props for the tooltip showcase page, providing the localized route lang segment.
 */
interface TooltipPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the Tooltip and Truncated components.
 * Resolves the localized title and renders the interactive tooltip example inside the app page shell.
 */
const TooltipPage = async ({ params }: TooltipPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.ui.tooltip.title')) || 'UI Tooltip'

  return (
    <PageContent title={title} description="Hover tooltips in every direction, and text that truncates with its full value in a tooltip." plain>
      <TooltipExample />
    </PageContent>
  )
}

export default TooltipPage
