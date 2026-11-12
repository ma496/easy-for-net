/** Represents an image payload as a base64 string with its original file name and MIME content type. */
export interface ImageDto {
  imageBase64: string
  fileName: string
  contentType: string
}
