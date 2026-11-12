import { RequestBase } from '@/store/api'

/** Request parameters for deleting a file by its server-side name. */
export interface FileDeleteRequest extends RequestBase {
  fileName: string
}

/** Empty response from the delete-file endpoint. */
export interface FileDeleteResponse { }

/** Request parameters for fetching a file by its server-side name. */
export interface FileGetRequest extends RequestBase {
  fileName: string
}

/** Request body for the file-upload endpoint, wrapping the file to be uploaded in a FormData submission together with how it is to be attributed. */
export interface FileUploadRequest extends RequestBase {
  file: File

  /**
   * Whether the file belongs to the calling account - a profile image and the like - rather than to
   * the site's shared content. Omitted or false makes it a site file.
   */
  accountOwned?: boolean
}

/** Response from the file-upload endpoint: the stored file's id (what a record references) and its server-assigned file name (what the file-get endpoint reads). */
export interface FileUploadResponse {
  id: string
  fileName: string
}
