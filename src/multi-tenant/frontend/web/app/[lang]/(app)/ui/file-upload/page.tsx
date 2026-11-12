import { getServerTranslation } from '@/i18n'
import { FileUploadExample } from './_components/file-upload-example'
import { PageContent } from '@/components/layouts'

/**
 * Props for the file upload showcase page, providing the localized route lang segment.
 */
interface FileUploadPageProps {
  params: Promise<{ lang: string }>
}

/**
 * Server-rendered UI showcase page for the FileUpload and MultiFileUpload components.
 * Resolves the localized title and renders the interactive file upload example inside the app page shell.
 */
const FileUploadPage = async ({ params }: FileUploadPageProps) => {
  const { lang } = await params
  const title = (await getServerTranslation(lang, 'page.ui.fileUpload.title')) || 'UI File Upload'

  return (
    <PageContent title={title} description="Single, fully custom and multi-file uploads with size limits and validation." plain>
      <FileUploadExample />
    </PageContent>
  )
}

export default FileUploadPage
