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

/** Request body for the file-upload endpoint, wrapping the file to be uploaded in a FormData submission. */
export interface FileUploadRequest extends RequestBase {
  file: File
}

/** Response from the file-upload endpoint, returning the server-assigned file name. */
export interface FileUploadResponse {
  fileName: string
}
