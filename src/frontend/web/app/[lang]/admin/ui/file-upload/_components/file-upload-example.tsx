'use client'

import { CodeShowcase, IconButton } from '@/components/ui'
import { FileUpload, MultiFileUpload } from '@/components/ui/form'
import { Pencil, Trash2 } from 'lucide-react'
import { confirmDeleteAlert } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { useState } from 'react'
import Image from 'next/image'
import { ShowcasePreview } from '../../_components/showcase-preview'

/**
 * Interactive client-side showcase component that demonstrates the FileUpload in basic, profile-avatar, and multi-file configurations with their source snippets.
 */
export const FileUploadExample = () => {
  const { t } = useTranslation()
  const [multiFiles, setMultiFiles] = useState<string[]>([])
  return (
    <div className="space-y-6">
      <div className="grid items-start gap-6 xl:grid-cols-2">
        <CodeShowcase
          title="Basic"
          description="Simple upload with size limit and validation."
          code={`<FileUpload
  name="basic-upload"
  forceDelete={true}
  maxSizeBytes={2 * 1024 * 1024}
/>`}
          preview={
            <ShowcasePreview>
              <div className="flex justify-center">
                <FileUpload name="basic-upload" forceDelete={true} maxSizeBytes={2 * 1024 * 1024} />
              </div>
            </ShowcasePreview>
          }
        />

        <CodeShowcase
          title="Profile avatar"
          description="Fully custom UI: circular image with edit and delete icons."
          code={`<FileUpload
  accept="image/*"
  forceDelete={true}
  onUploaded={async (res) => {
    console.log(res)
  }}
>
  {({ open, isUploading, isDeleting, deleteFile, selectedFileUrl }) => (
    <div className="space-y-3">
      <div className="flex items-center justify-center">
        {selectedFileUrl ? (
          <Image src={selectedFileUrl} alt="avatar" width={96} height={96} unoptimized className="size-24 rounded-full border border-border object-cover" />
        ) : (
          <div className="grid size-24 place-items-center rounded-full border border-border bg-surface-2 text-xs text-muted-foreground">No image</div>
        )}
      </div>
      <div className="flex items-center justify-center gap-3">
        <IconButton
          variant="outline"
          rounded="full"
          onClick={open}
          aria-label="Edit"
          title="Edit"
          icon={<Pencil className="h-4 w-4" />}
          isLoading={isUploading}
          disabled={isUploading || isDeleting}
        />
        <IconButton
          variant="outline-danger"
          rounded="full"
          onClick={async () => {
            const result = await confirmDeleteAlert({
              title: t('file.deleteTitle'),
              text: t('file.deleteConfirm'),
            })
            if (result.isConfirmed) {
              await deleteFile()
            }
          }}
          aria-label="Delete"
          title="Delete"
          icon={<Trash2 className="h-4 w-4" />}
          isLoading={isDeleting}
          disabled={isUploading || isDeleting}
        />
      </div>
    </div>
  )}
</FileUpload>`}
          preview={
            <ShowcasePreview>
              <div className="w-full max-w-md">
                <FileUpload
                  name="avatar-upload"
                  accept="image/*"
                  forceDelete={true}
                  maxSizeBytes={2 * 1024 * 1024}
                  validateFile={(file) => {
                    if (!file.type.startsWith('image/')) return 'Only images are allowed'
                    return undefined
                  }}
                  onUploaded={async (res) => {
                    console.log(res)
                  }}
                >
                  {({ open, isUploading, isDeleting, deleteFile, response, selectedFileUrl }) => (
                    <div className="space-y-3">
                      <div className="flex items-center justify-center">
                        {selectedFileUrl ? (
                          <Image src={selectedFileUrl} alt="avatar" width={96} height={96} unoptimized className="size-24 rounded-full border border-border object-cover" />
                        ) : (
                          <div className="grid size-24 place-items-center rounded-full border border-border bg-surface-2 text-xs text-muted-foreground">No image</div>
                        )}
                      </div>
                      <div className="flex items-center justify-center gap-3">
                        <IconButton
                          variant="outline"
                          rounded="full"
                          onClick={open}
                          aria-label="Edit"
                          title="Edit"
                          icon={<Pencil className="h-4 w-4" />}
                          isLoading={isUploading}
                          disabled={isUploading || isDeleting}
                        />
                        <IconButton
                          variant="outline-danger"
                          rounded="full"
                          onClick={async () => {
                            const result = await confirmDeleteAlert({
                              title: t('file.deleteTitle'),
                              text: t('file.deleteConfirm'),
                            })
                            if (result.isConfirmed) {
                              await deleteFile()
                            }
                          }}
                          aria-label="Delete"
                          title="Delete"
                          icon={<Trash2 className="h-4 w-4" />}
                          isLoading={isDeleting}
                          disabled={isUploading || isDeleting || !response?.fileName}
                        />
                      </div>
                    </div>
                  )}
                </FileUpload>
              </div>
            </ShowcasePreview>
          }
        />
      </div>

      <CodeShowcase
        title="Multi-file upload"
        description="Upload multiple files with drag-and-drop reordering, specific file replacement (Edit), and automatic server-side deletion."
        code={`const [files, setFiles] = useState<string[]>([])

<MultiFileUpload
  name="multi-upload"
  label="Gallery"
  fileNames={files}
  onFilesChanged={setFiles}
/>`}
        preview={
          <ShowcasePreview>
            <div className="w-full">
              <MultiFileUpload name="multi-upload" label="Gallery" fileNames={multiFiles} onFilesChanged={setMultiFiles} />
            </div>
          </ShowcasePreview>
        }
      />
    </div>
  )
}
