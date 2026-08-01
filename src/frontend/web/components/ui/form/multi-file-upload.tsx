'use client'
import React, { useId, useState, useCallback, useEffect } from 'react'
import { Plus, Trash2, Loader2, Pencil } from 'lucide-react'
import { apiErrorAlert, cn, confirmDeleteAlert } from '@/lib/utils'
import { useTranslation } from '@/i18n'
import { IconButton } from '..'
import { useFileUploadMutation, useLazyFileGetQuery, useFileDeleteMutation } from '@/store/api/file-management'
import { ReactSortable } from 'react-sortablejs'

/** Props for the MultiFileUpload component, which uploads and reorders multiple files via the file-management API, exposing replace/delete actions for each file and notifying the parent of the new filename list. */
interface MultiFileUploadProps {
  label?: string
  name: string
  fileNames?: string[]
  onFilesChanged: (fileNames: string[]) => void
  maxSizeBytes?: number
  accept?: string
  forceDelete?: boolean
}

/**
 * MultiFileUpload is a client component that renders a grid of uploaded files with drag-to-reorder, replace, and delete actions, plus a dropzone tile for adding new files; it integrates with the file-management API and revokes object URLs on cleanup.
 */
export const MultiFileUpload = ({
  label,
  // name,
  fileNames = [],
  onFilesChanged,
  maxSizeBytes = 10 * 1024 * 1024,
  accept = 'image/*',
  forceDelete = true,
}: MultiFileUploadProps) => {
  const { t } = useTranslation()
  const inputId = useId()
  const [uploadFile, { isLoading: isUploading }] = useFileUploadMutation()
  const [deleteFile] = useFileDeleteMutation()
  const [lazyFileGet] = useLazyFileGetQuery()
  const [fileUrls, setFileUrls] = useState<{ [key: string]: string }>({})
  const [replacingIndex, setReplacingIndex] = useState<number | null>(null)
  const replaceInputRef = React.useRef<HTMLInputElement>(null)
  const replaceInputId = useId()

  const loadFileUrls = useCallback(async (names: string[]) => {
    const urls: { [key: string]: string } = {}
    for (const fileName of names) {
      if (!fileUrlsRef.current[fileName]) {
        const result = await lazyFileGet({ fileName })
        if (result.error) {
          apiErrorAlert(result.error, [404])
          continue
        }
        if (result.data) {
          const blob = result.data as Blob
          const url = URL.createObjectURL(blob)
          urls[fileName] = url
        }
      }
    }
    if (Object.keys(urls).length > 0) {
      setFileUrls((prev: { [key: string]: string }) => ({ ...prev, ...urls }))
    }
  }, [lazyFileGet])

  useEffect(() => {
    loadFileUrls(fileNames)
  }, [fileNames, loadFileUrls])

  const fileUrlsRef = React.useRef(fileUrls)
  useEffect(() => {
    fileUrlsRef.current = fileUrls
  }, [fileUrls])

  useEffect(() => {
    return () => {
      // Cleanup all object URLs on unmount
      Object.values(fileUrlsRef.current).forEach((url) => {
        if (typeof url === 'string' && url.startsWith('blob:')) {
          URL.revokeObjectURL(url)
        }
      })
    }
  }, [])

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || [])
    if (files.length === 0) return

    const newFileNames = [...fileNames]
    for (const file of files) {
      if (file.size > maxSizeBytes) {
        continue
      }

      const res = await uploadFile({ file })
      if (res.error) {
        apiErrorAlert(res.error)
        continue
      }
      if (res.data) {
        newFileNames.push(res.data.fileName)
        const url = URL.createObjectURL(file)
        setFileUrls((prev: { [key: string]: string }) => ({ ...prev, [res.data!.fileName]: url }))
      }
    }
    onFilesChanged(newFileNames)
    e.target.value = ''
  }

  const handleReplaceClick = (index: number) => {
    setReplacingIndex(index)
    setTimeout(() => {
      replaceInputRef.current?.click()
    }, 0)
  }

  const handleReplaceChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file || replacingIndex === null) return

    if (file.size > maxSizeBytes) {
      return
    }

    const oldFileName = fileNames[replacingIndex]
    const res = await uploadFile({ file })
    if (res.error) {
      apiErrorAlert(res.error)
    }
    if (res.data) {
      if (forceDelete && oldFileName) {
        await deleteFile({ fileName: oldFileName })
      }
      const newFileNames = [...fileNames]
      newFileNames[replacingIndex] = res.data.fileName
      onFilesChanged(newFileNames)

      const url = URL.createObjectURL(file)
      setFileUrls((prev: { [key: string]: string }) => ({ ...prev, [res.data!.fileName]: url }))

      if (fileUrls[oldFileName]?.startsWith('blob:')) {
        URL.revokeObjectURL(fileUrls[oldFileName])
      }
    }
    setReplacingIndex(null)
    e.target.value = ''
  }

  const handleRemove = async (index: number) => {
    const fileNameToRemove = fileNames[index]
    const result = await confirmDeleteAlert({
      title: t('file.deleteTitle'),
      text: t('file.deleteConfirm'),
    })

    if (result.isConfirmed) {
      if (forceDelete) {
        await deleteFile({ fileName: fileNameToRemove })
      }
      const newFileNames = [...fileNames]
      newFileNames.splice(index, 1)
      onFilesChanged(newFileNames)

      if (fileUrls[fileNameToRemove]) {
        URL.revokeObjectURL(fileUrls[fileNameToRemove])
        const newUrls = { ...fileUrls }
        delete newUrls[fileNameToRemove]
        setFileUrls(newUrls)
      }
    }
  }

  return (
    <div className="space-y-4">
      {label && <label htmlFor={inputId} className="label form-label">{label}</label>}

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
        <ReactSortable
          list={fileNames.map(name => ({ id: name, name }))}
          setList={(newList) => onFilesChanged(newList.map(item => item.name))}
          className="contents"
          animation={200}
          ghostClass="opacity-50"
        >
          {fileNames.map((fileName, index) => (
            <div key={fileName} className="group relative aspect-square cursor-move overflow-hidden rounded-lg border border-white-light dark:border-[#17263c] bg-gray-100 dark:bg-gray-800">
              {fileUrls[fileName] ? (
                <img src={fileUrls[fileName]} alt="" className="h-full w-full object-contain" />
              ) : (
                <div className="flex h-full w-full items-center justify-center">
                  <Loader2 className="h-6 w-6 animate-spin text-primary" />
                </div>
              )}
              <div className="absolute inset-0 flex items-center justify-center gap-2 bg-black/40 opacity-0 transition-opacity group-hover:opacity-100">
                <IconButton
                  variant="info"
                  size="sm"
                  rounded="full"
                  icon={<Pencil className="h-4 w-4" />}
                  onClick={(e) => {
                    e.stopPropagation()
                    handleReplaceClick(index)
                  }}
                  title={t('common.edit')}
                />
                <IconButton
                  variant="danger"
                  size="sm"
                  rounded="full"
                  icon={<Trash2 className="h-4 w-4" />}
                  onClick={(e) => {
                    e.stopPropagation()
                    handleRemove(index)
                  }}
                  title={t('common.delete')}
                />
              </div>
            </div>
          ))}
        </ReactSortable>

        <label
          htmlFor={inputId}
          className={cn(
            "flex aspect-square cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed border-white-light transition-colors hover:border-primary hover:bg-primary/5 dark:border-[#17263c] dark:hover:border-primary",
            isUploading && "pointer-events-none opacity-50"
          )}
        >
          {isUploading ? (
            <Loader2 className="h-8 w-8 animate-spin text-primary" />
          ) : (
            <>
              <Plus className="mb-2 h-8 w-8 text-gray-400" />
              <span className="text-xs text-gray-400">{t('file.upload')}</span>
            </>
          )}
          <input
            id={inputId}
            type="file"
            multiple
            accept={accept}
            className="hidden"
            onChange={handleFileChange}
            disabled={isUploading}
          />
          <input
            id={replaceInputId}
            ref={replaceInputRef}
            type="file"
            accept={accept}
            className="hidden"
            onChange={handleReplaceChange}
            disabled={isUploading}
          />
        </label>
      </div>
    </div>
  )
}
