---
name: file-storage
description: Upload, serve and delete files — the FileManagement feature (IFileService/IStorageProvider), payload size limits, storing a file reference on an entity, and the FileUpload/ImagePreview components on the web side. Use when a feature needs attachments, avatars or any binary content.
---

# Files

The `FileManagement` feature owns everything binary. Other features reference a file by the
**stored filename string** and never touch the filesystem themselves.

## Backend

Two layers, both `[AllowOutside]` so other features may depend on them:

- `IStorageProvider` — the physical backend (`SaveAsync`, `GetAsync`, `DeleteAsync`, `Exists`).
  `LocalStorageProvider` writes to `<ContentRoot>/uploads`, rejecting any name that is not a bare
  filename or that would escape the folder.
- `IFileService` — the application-level API (`UploadAsync`, `DownloadAsync`, `DeleteAsync`). It
  generates the unique stored name (`{Guid}{extension}`) and resolves the content type on download.

Both implementations are `[NoDirectUse]` — inject the interfaces. Swapping storage (S3, Azure Blob)
means writing a new `IStorageProvider` and changing one line in `FileManagementFeature.AddServices`;
nothing else in the codebase should need to change.

Endpoints live under the `file-management` prefix:

| Route | Notes |
| --- | --- |
| `POST /file-management/upload` | `AllowFileUploads()`, request has an `IFormFile File`, returns the stored `FileName` |
| `GET /file-management/{fileName}` | streams via `Send.StreamAsync(content, contentType)`; authenticated, no permission |
| `DELETE /file-management/{fileName}` | `Permissions(Allow.File_Delete)`, returns `Send.NoContentAsync` |

### Referencing a file from your feature

Store the returned filename on your entity as a plain nullable string (`User.Image` is the existing
example) — no foreign key, no separate table. Upload first, then save the entity with the name the
upload returned. When the owning row is deleted and the file should go too, call
`IFileService.DeleteAsync` explicitly; nothing cascades.

Validate what you accept in the endpoint's validator — extension and size for your own use case —
because the upload endpoint itself is deliberately generic.

### Size limits

`Payload:MaximumSize` (default 25 MB) drives three things at once: Kestrel's
`MaxRequestBodySize`, `FormOptions.MultipartBodyLengthLimit`, and the global
`ToLargePayloadProcessor`, which short-circuits oversized requests with a 413 carrying
`ErrorCodes.PayloadTooLarge`. Change the setting, not the individual limits, and remember it is
validated on start (1 byte – 1 GB).

## Web

`filesApi` (`store/api/file-management/files`) is intentionally **untagged** — there is no listing to
invalidate:

```ts
const [uploadFile] = useFileUploadMutation()          // builds FormData internally
const [fetchFile] = useLazyFileGetQuery()             // responseHandler → blob()
const [deleteFile] = useFileDeleteMutation()
```

Components: `FileUpload` and `MultiFileUpload` in `@/components/ui/form` for picking files, and
`ImagePreview` in `@/components/custom` for showing one back. A typical form uploads on selection,
keeps the returned filename in form state, and submits that string with the rest of the payload —
so a failed submit does not lose the upload.

To display a stored file, build the URL from the API base (`environment.apiUrl`) plus
`/file-management/{fileName}`; the request carries credentials, so the user must be signed in.

## Checklist

- [ ] Upload through `IFileService` / `useFileUploadMutation`, never direct filesystem access
- [ ] Entity stores the returned filename string; deletion of the row deletes the file if it should
- [ ] Extension/size validation added for your feature's own rules
- [ ] `Payload:MaximumSize` adjusted (not Kestrel/FormOptions individually) if bigger files are needed
- [ ] New storage backend implemented as an `IStorageProvider` and registered in the feature module
