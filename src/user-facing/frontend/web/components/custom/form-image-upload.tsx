'use client'

import { useId, useState, type ReactNode } from 'react'
import { useField } from 'formik'
import { ImageIcon, Trash2, Upload } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { Button, IconButton } from '@/components/ui'
import { FileUpload } from '@/components/ui/form'
import { environment } from '@/config/environment'
import { ImagePreview } from './image-preview'

/** Props for FormImageUpload: the Formik field holding the stored file's id, and its label and hint. */
interface FormImageUploadProps {
  /** The Formik field; it holds the uploaded file's id, or `''` for none. */
  name: string
  label: string
  hint?: string
  disabled?: boolean
  /** The largest file accepted, in bytes; 10 MB unless given. */
  maxSizeBytes?: number
  /** Shown at the end of the label row, such as a setting's source and "Changed" badges. */
  labelAddon?: ReactNode
}

/** Where a saved SEO image is served from: the API's public image route, which answers only for an image a page or the site defaults reference. */
export const seoImageUrl = (fileId: string) => `${environment.apiUrl}/seo/images/${encodeURIComponent(fileId)}`

/**
 * A Formik-aware image field: a wide preview of the stored image (or an empty tile), an Upload/Replace
 * button and a remove button, grouped under the label. Uploading goes through the file-management API
 * and writes the stored file's id into the field; removing empties it. The previous file is not deleted
 * here - the form is not saved yet - so a discarded edit never loses the image the record still points at.
 *
 * A just-uploaded image is previewed through the file-get endpoint by the name the upload returned; an
 * image the record already held is previewed through the public SEO image route by its id, and one that
 * fails to load shows the empty tile marked "Image unavailable" until the value changes.
 */
export const FormImageUpload = ({ name, label, hint, disabled = false, maxSizeBytes = 10 * 1024 * 1024, labelAddon }: FormImageUploadProps) => {
  const { t } = useTranslation()
  const labelId = useId()
  const [field, , helpers] = useField<string | null>(name)
  const fileId = field.value || ''
  // The upload this form made, while it is still the field's value.
  const [uploaded, setUploaded] = useState<{ id: string; fileName: string } | null>(null)
  const uploadedFileName = uploaded && uploaded.id === fileId ? uploaded.fileName : ''
  // The stored image that failed to load; keyed by id, so a different value is tried afresh.
  const [failedFileId, setFailedFileId] = useState('')
  const isStoredImageBroken = !!fileId && failedFileId === fileId

  return (
    <div role="group" aria-labelledby={labelId} className="min-w-0">
      <div className="mb-1.5 flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
        <span id={labelId} className="text-sm font-medium text-foreground">
          {label}
        </span>
        {labelAddon && <div className="flex flex-wrap items-center gap-1">{labelAddon}</div>}
      </div>
      <FileUpload
        name={name}
        accept="image/*"
        maxSizeBytes={maxSizeBytes}
        forceDelete={false}
        fileName={uploadedFileName || undefined}
        disabled={disabled}
        onUploaded={(response) => {
          setUploaded({ id: response.id, fileName: response.fileName })
          helpers.setValue(response.id)
        }}
        onClear={() => helpers.setValue('')}
      >
        {({ open, isUploading, selectedFileUrl }) => (
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <div className="flex aspect-[1.91/1] w-full shrink-0 items-center justify-center overflow-hidden rounded-lg border border-border bg-surface-2 sm:w-56">
              {selectedFileUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={selectedFileUrl} alt={label} className="size-full object-cover" />
              ) : uploadedFileName ? (
                <ImagePreview imageName={uploadedFileName} alt={label} className="size-full" objectFit="cover" fallback={<ImageIcon className="size-6 text-muted-foreground" />} />
              ) : fileId && !isStoredImageBroken ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={seoImageUrl(fileId)} alt={label} className="size-full object-cover" onError={() => setFailedFileId(fileId)} />
              ) : (
                <div className="flex flex-col items-center gap-1 px-2 text-center text-muted-foreground">
                  <ImageIcon className="size-6" aria-hidden="true" />
                  <span className="text-xs">{isStoredImageBroken ? t('page.seo.form.imageUnavailable') || 'Image unavailable' : t('page.seo.form.noImage') || 'No image'}</span>
                </div>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" variant="outline" size="sm" icon={<Upload className="size-4" />} onClick={open} isLoading={isUploading} disabled={disabled}>
                {fileId ? t('page.seo.form.replaceImage') || 'Replace image' : t('page.seo.form.uploadImage') || 'Upload image'}
              </Button>
              {fileId && !disabled && (
                <IconButton
                  variant="outline-danger"
                  size="sm"
                  icon={<Trash2 className="size-4" />}
                  aria-label={t('page.seo.form.removeImage') || 'Remove image'}
                  title={t('page.seo.form.removeImage') || 'Remove image'}
                  onClick={() => helpers.setValue('')}
                />
              )}
            </div>
          </div>
        )}
      </FileUpload>
      {hint && <p className="mt-1.5 text-xs text-muted-foreground">{hint}</p>}
    </div>
  )
}
