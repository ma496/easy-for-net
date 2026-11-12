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
   * Whether the file belongs to the calling account rather than to the tenant's data - a profile
   * image and the like. Such a file is attributed to no tenant and to the calling account, so it
   * can be uploaded while acting in any tenant or in none. Omitted or false attributes the file to
   * the tenant active at the time of upload, and the upload is refused when no tenant is active.
   */
  accountOwned?: boolean
}

/** Response from the file-upload endpoint, returning the server-assigned file name. */
export interface FileUploadResponse {
  fileName: string
}
