import { appApi } from '@/store/api/_app-api'
import { FileDeleteRequest, FileDeleteResponse, FileGetRequest, FileUploadRequest, FileUploadResponse } from './files-dtos'

/**
 * RTK Query API for file management: upload a file (multipart FormData, carrying how the file is to
 * be attributed), fetch a file as a Blob, and delete a file by its server-side name.
 */
export const filesApi = appApi.injectEndpoints({
  overrideExisting: false,
  endpoints: (builder) => ({
    fileUpload: builder.mutation<FileUploadResponse, FileUploadRequest>({
      query: (input) => {
        const body = new FormData()
        body.append('file', input.file)
        // Only sent when the caller marks the file account-owned: the server defaults the flag to
        // false, so an ordinary upload keeps its existing shape and stays attributed to the active
        // tenant - and is refused outright when no tenant is active.
        if (input.accountOwned) {
          body.append('accountOwned', 'true')
        }
        return {
          url: '/file-management/upload',
          method: 'POST',
          body,
        }
      },
    }),
    fileGet: builder.query<Blob, FileGetRequest>({
      query: (input) => ({
        url: `/file-management/${input.fileName}`,
        method: 'GET',
        responseHandler: (response) => response.blob(),
      }),
    }),
    fileDelete: builder.mutation<FileDeleteResponse, FileDeleteRequest>({
      query: (input) => ({
        url: `/file-management/${input.fileName}`,
        method: 'DELETE',
      }),
    }),
  }),
})

export const { useFileUploadMutation, useFileGetQuery, useLazyFileGetQuery, useFileDeleteMutation } = filesApi
