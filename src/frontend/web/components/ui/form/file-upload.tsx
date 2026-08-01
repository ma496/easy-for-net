'use client'

import React, { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react'
import { Upload, Trash2 } from 'lucide-react'
import { apiErrorAlert, cn, confirmDeleteAlert, errorAlert } from '@/lib/utils'
import { Button, type ButtonProps } from '../button'
import { IconButton } from '../icon-button'
import { useFileUploadMutation, useFileDeleteMutation, useLazyFileGetQuery } from '@/store/api/file-management/files/files-api'
import { FileUploadResponse } from '@/store/api/file-management/files/files-dtos'
import { useTranslation } from '@/i18n'

/** Props for the FileUpload component, which wraps a hidden file input, calls the file-management API to upload/delete files, and optionally renders a custom UI via a render-prop child. */
export interface FileUploadProps {
  variant?: ButtonProps['variant']
  size?: ButtonProps['size']
  rounded?: ButtonProps['rounded']
  label?: string
  name: string
  className?: string
  buttonText?: string
  icon?: React.ReactNode
  showFileName?: boolean
  onUploaded?: (response: FileUploadResponse) => void
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onError?: (error: any) => void
  fileName?: string
  accept?: string
  id?: string
  disabled?: boolean
  forceDelete?: boolean // file delete only when forceDelete is true
  maxSizeBytes?: number
  onClear?: () => void
  validateFile?: (file: File) => string | undefined
  showError?: boolean
  children?: (ctx: {
    open: () => void
    isUploading: boolean
    selectedFileName: string
    selectedFileUrl?: string
    response?: FileUploadResponse
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    error?: any
    deleteFile: () => Promise<void>
    isDeleting: boolean
    getFileUrl: () => Promise<string | undefined>
    inputId: string
    accept?: string
  }) => React.ReactNode
}

/**
 * FileUpload is a client component that integrates with the file-management API to upload a single file, expose its blob URL, and optionally delete the previous file when a new one is selected; it supports both a default button UI and a render-prop child for custom layouts.
 */
export const FileUpload = ({
  label,
  name,
  id,
  accept,
  className,
  buttonText = 'Upload',
  icon,
  showFileName = true,
  variant,
  size,
  rounded,
  disabled,
  forceDelete = true,
  onUploaded,
  onError,
  fileName,
  maxSizeBytes,
  onClear,
  validateFile,
  showError = true,
  children,
}: FileUploadProps) => {
  const defaultId = useId()
  const inputId = id ?? defaultId
  const { t } = useTranslation()
  const inputRef = useRef<HTMLInputElement>(null)
  const [selectedFileName, setSelectedFileName] = useState<string>('')
  const [selectedFileUrl, setSelectedFileUrl] = useState<string | undefined>(undefined)
  const [response, setResponse] = useState<FileUploadResponse | undefined>(undefined)
  const [uploadFile, { isLoading: isUploading }] = useFileUploadMutation()
  const [deleteFileTrigger, { isLoading: isDeleting }] = useFileDeleteMutation()
  const [lazyFileGet] = useLazyFileGetQuery()
  const lastFileNameRef = useRef<string | undefined>(undefined)
  const resolvedIcon = useMemo(() => icon ?? <Upload className="h-4 w-4" />, [icon])
  const hasCurrent = useMemo(() => !!selectedFileName || (forceDelete && !!(fileName ?? response?.fileName)), [selectedFileName, forceDelete, fileName, response])

  const handleClick = useCallback(() => {
    inputRef.current?.click()
  }, [])

  const handleChange = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0]
      if (!file) return

      if (maxSizeBytes && file.size > maxSizeBytes) {
        const err = new Error('File exceeds size limit')
        if (showError) {
          errorAlert({ text: t('file.tooLarge') })
        }
        onError?.(err)
        e.target.value = ''
        return
      }

      if (validateFile) {
        const message = validateFile(file)
        if (message) {
          const err = new Error(message)
          onError?.(err)
          e.target.value = ''
          return
        }
      }

      const oldFileName = fileName ?? response?.fileName

      if (selectedFileUrl?.startsWith('blob:')) {
        URL.revokeObjectURL(selectedFileUrl)
      }
      const res = await uploadFile({ file })
      if (res.data) {
        if (forceDelete && oldFileName) {
          await deleteFileTrigger({ fileName: oldFileName })
        }
        setSelectedFileName(file.name)
        const url = URL.createObjectURL(file)
        lastFileNameRef.current = res.data.fileName
        setSelectedFileUrl(url)
        setResponse(res.data)
        onUploaded?.(res.data)
      } else if (res.error) {
        apiErrorAlert(res.error)
      }
      if (inputRef.current) {
        inputRef.current.value = ''
      }
    },
    [uploadFile, onUploaded, onError, selectedFileUrl, maxSizeBytes, validateFile, forceDelete, fileName, response, deleteFileTrigger, t, showError],
  )

  const deleteFile = useCallback(async () => {
    const current = fileName ?? response?.fileName

    if (forceDelete) {
      if (!current) return
      const res = await deleteFileTrigger({ fileName: current })
      if (!res.error) {
        setSelectedFileName('')
        lastFileNameRef.current = undefined
        setSelectedFileUrl(undefined)
        setResponse(undefined)
      }
    } else {
      setSelectedFileName('')
      if (selectedFileUrl?.startsWith('blob:')) {
        URL.revokeObjectURL(selectedFileUrl)
      }
      setSelectedFileUrl(undefined)
      onClear?.()
    }
  }, [forceDelete, fileName, response, deleteFileTrigger, selectedFileUrl, onClear])

  const getFileUrl = useCallback(async (): Promise<string | undefined> => {
    const current = fileName ?? response?.fileName
    if (!current) return undefined
    const result = await lazyFileGet({ fileName: current })
    if (result.error) {
      apiErrorAlert(result.error, [404])
      return
    }
    if (result.data) {
      return URL.createObjectURL(result.data as Blob)
    }
    return undefined
  }, [fileName, response, lazyFileGet])

  useEffect(() => {
    return () => {
      if (selectedFileUrl?.startsWith('blob:')) {
        URL.revokeObjectURL(selectedFileUrl)
      }
    }
  }, [selectedFileUrl])

  useEffect(() => {
    let cancelled = false
    let currentObjectUrl: string | undefined

    const loadUrl = async () => {
      const current = fileName ?? response?.fileName
      if (!current) {
        if (lastFileNameRef.current !== undefined) {
          lastFileNameRef.current = undefined
          setSelectedFileUrl(undefined)
        }
        return
      }

      if (lastFileNameRef.current === current) {
        return
      }

      const result = await lazyFileGet({ fileName: current })
      if (result.error) {
        apiErrorAlert(result.error, [404])
        return
      }
      if (!cancelled) {
        if (result.data) {
          const url = URL.createObjectURL(result.data as Blob)
          currentObjectUrl = url
          lastFileNameRef.current = current
          setSelectedFileUrl(url)
        } else {
          lastFileNameRef.current = current
          setSelectedFileUrl(undefined)
        }
      } else if (result.data) {
        // If cancelled but we got data, revoke it immediately
        URL.revokeObjectURL(URL.createObjectURL(result.data as Blob))
      }
    }

    loadUrl()

    return () => {
      cancelled = true
      // Only revoke if it was created locally in this effect
      if (currentObjectUrl) {
        URL.revokeObjectURL(currentObjectUrl)
      }
    }
  }, [fileName, response, lazyFileGet])

  const handleDeleteClick = useCallback(async () => {
    const result = await confirmDeleteAlert({
      title: t('file.deleteTitle'),
      text: t('file.deleteConfirm'),
    })

    if (result.isConfirmed) {
      if (forceDelete) {
        await deleteFile()
      } else {
        setSelectedFileName('')
        if (selectedFileUrl?.startsWith('blob:')) {
          URL.revokeObjectURL(selectedFileUrl)
        }
        setSelectedFileUrl(undefined)
        onClear?.()
      }
    }
  }, [forceDelete, deleteFile, selectedFileUrl, onClear, t])

  const content = children ? (
    // eslint-disable-next-line react-hooks/refs
    children({
      open: handleClick,
      isUploading: isUploading,
      selectedFileName,
      selectedFileUrl,
      response,
      deleteFile,
      isDeleting,
      getFileUrl,
      inputId,
      accept,
    })
  ) : (
    <div className={cn(className)}>
      {label && (
        <label htmlFor={inputId} className="label form-label">
          {label}
        </label>
      )}

      <div className="flex items-center gap-3">
        <Button type="button" onClick={handleClick} variant={variant} size={size} rounded={rounded} isLoading={isUploading} disabled={disabled || isDeleting} icon={resolvedIcon}>
          {buttonText}
        </Button>

        {hasCurrent && (
          <IconButton
            type="button"
            variant="outline-danger"
            rounded="full"
            onClick={handleDeleteClick}
            isLoading={isDeleting}
            disabled={isUploading}
            aria-label="Delete"
            title="Delete"
            icon={<Trash2 className="h-4 w-4" />}
          />
        )}

        {showFileName && selectedFileName && (
          <span className="text-sm text-info" aria-live="polite">
            {selectedFileName}
          </span>
        )}
      </div>
    </div>
  )

  return (
    <>
      <input id={inputId} ref={inputRef} type="file" name={name} accept={accept} className="hidden" onChange={handleChange} />
      {content}
    </>
  )
}

FileUpload.displayName = 'FileUpload'
