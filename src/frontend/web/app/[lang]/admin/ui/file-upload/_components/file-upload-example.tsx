'use client'

import { CodeShowcase, IconButton } from '@/components/ui'
import { FileUpload, MultiFileUpload } from '@/components/ui/form'
import { Pencil, Trash2 } from 'lucide-react'
import { confirmDeleteAlert } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { useState } from 'react'

/**
 * Interactive client-side showcase component that demonstrates the FileUpload in basic, profile-avatar, and multi-file configurations with their source snippets.
 */
export const FileUploadExample = () => {
  const { t } = useTranslation()
  const [multiFiles, setMultiFiles] = useState<string[]>([])
  return (
    <div className="container mx-auto space-y-8 p-6">
      <div>
        <h1 className="text-2xl font-semibold">File Upload</h1>
        <p className="text-muted">Reusable and customizable file upload component.</p>
      </div>

      <CodeShowcase
        title="Basic"
        description="Simple upload with size limit and validation."
        code={`<FileUpload
  name="basic-upload"
  forceDelete={true}
  maxSizeBytes={2 * 1024 * 1024}
/>`}
        preview={
          <div className="panel w-105">
            <div className="flex items-center justify-center space-y-3">
              <FileUpload
                name="basic-upload"
                forceDelete={true}
                maxSizeBytes={2 * 1024 * 1024}
              />
            </div>
          </div>
        }
      />

      <CodeShowcase
        title="Profile Avatar"
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
          <img src={selectedFileUrl} alt="avatar" className="w-24 h-24 rounded-full object-cover border" />
        ) : (
          <div className="w-24 h-24 rounded-full border flex items-center justify-center text-muted">No Image</div>
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
          <div className="panel w-105">
            <div className="space-y-3 p-4">
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
                        <img src={selectedFileUrl} alt="avatar" className="h-24 w-24 rounded-full border object-cover" />
                      ) : (
                        <div className="text-muted flex h-24 w-24 items-center justify-center rounded-full border">No Image</div>
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
          </div>
        }
      />

      <CodeShowcase
        title="Multi-File Upload"
        description="Upload multiple files with drag-and-drop reordering, specific file replacement (Edit), and automatic server-side deletion."
        code={`const [files, setFiles] = useState<string[]>([])

<MultiFileUpload
  name="multi-upload"
  label="Gallary"
  fileNames={files}
  onFilesChanged={setFiles}
/>`}
        preview={
          <div className="panel w-full max-w-150">
            <MultiFileUpload
              name="multi-upload"
              label="Gallery"
              fileNames={multiFiles}
              onFilesChanged={setMultiFiles}
            />
          </div>
        }
      />
    </div>
  )
}
