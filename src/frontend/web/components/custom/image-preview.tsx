'use client'

import React, { useEffect, useState } from 'react'
import { useFileGetQuery } from '@/store/api/file-management'
import { cn } from '@/lib/utils'

/**
 * Props for the {@link ImagePreview} component, providing the image filename, alt text, optional fallback element, and object-fit style.
 */
interface ImagePreviewProps {
  imageName: string
  alt?: string
  className?: string
  fallback?: React.ReactNode
  objectFit?: 'cover' | 'contain' | 'fill' | 'none' | 'scale-down'
}

/**
 * Client-side component that fetches a binary image blob by filename, creates a temporary object URL, and renders an `<img>` (with optional fallback) while cleaning up the URL on unmount or change.
 */
export const ImagePreview = ({
  imageName,
  alt = 'Image Preview',
  className,
  fallback,
  objectFit = 'contain',
}: ImagePreviewProps) => {
  const [imageUrl, setImageUrl] = useState<string | undefined>()

  const { data: blob, isSuccess } = useFileGetQuery(
    { fileName: imageName },
    { skip: !imageName }
  )

  useEffect(() => {
    let url: string | undefined
    if (isSuccess && blob) {
      url = URL.createObjectURL(blob as Blob)
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setImageUrl(url)
    }

    return () => {
      if (url) {
        URL.revokeObjectURL(url)
      }
    }
  }, [blob, isSuccess, imageName])

  if (!isSuccess && !imageUrl) {
    return <>{fallback}</>
  }

  return (
    <img
      src={imageUrl}
      alt={alt}
      className={cn('h-full w-full', className)}
      style={{ objectFit }}
    />
  )
}

ImagePreview.displayName = 'ImagePreview'
