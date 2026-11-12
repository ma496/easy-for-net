---
name: file-storage
description: Upload, serve and delete files — the FileManagement feature (IFileService/IStorageProvider), application vs account-owned files on StoredFile, the payload size limit, serving a file publicly (the SEO image route and OpenPublicAsync), storing a file reference on an entity, and the FileUpload/MultiFileUpload/FormImageUpload/ImagePreview components on the web side. Use when a feature needs attachments, avatars, sharing images or any binary content.
---

# Files

The `FileManagement` feature owns everything binary. Other features reference a file by the
**stored filename string** (or, for a file served publicly, by its `StoredFile` id) and never touch
the filesystem themselves.

## Backend

Two layers, both `[AllowOutside]` so other features may depend on them:

- `IStorageProvider` — the physical backend (`SaveAsync`, `GetAsync`, `DeleteAsync`, `Exists`). It
  knows nothing about owners. `LocalStorageProvider` writes to `<ContentRoot>/uploads`, rejecting
  any name that is not a bare filename or that would escape the folder.
- `IFileService` — the application-level API:
  - `UploadAsync(stream, fileName, contentType, accountOwned = false, ct)` → `UploadedFile(Id, FileName)`
    (the stored name is `{Guid}{extension}`), after recording a `StoredFile` row.
  - `DownloadAsync(fileName, ct)` → `FileDownloadResult { Status, Content, ContentType, OriginalFileName }`.
  - `OpenPublicAsync(fileId, ct)` → the same result with **no access check at all** (see below).
  - `DeleteAsync(fileName, ct)` → `FileDeleteResult { Status }`.

Both implementations are `[NoDirectUse]` — inject the interfaces. Swapping storage (S3, Azure Blob)
means writing a new `IStorageProvider` and changing one line in `FileManagementFeature.AddServices`;
ownership lives in `FileService`, so who can read what does not change.

### Ownership

Every upload writes a `StoredFile` that decides every later read and delete — a stored name is an
address, never a permission:

| Upload | `OwnerUserId` | Readable by |
| --- | --- | --- |
| default (`accountOwned: false`) — an application file | null | any signed-in caller |
| `accountOwned: true` (avatars, per-user files) | the caller | that account alone |

An account-owned upload needs a signed-in caller (`UserIdNullException` otherwise). Download/delete
return a `FileAccessStatus`: `Granted`, `NotFound`, `AccessDenied` (someone else's account-owned
file). Always branch on `Status`; the file endpoints map `NotFound` → 404 and `AccessDenied` → 403
`permissionDenied`.

Because every signed-in visitor can read an application file by name, a file that belongs to one
visitor — anything a visitor uploads about themselves — must be `accountOwned`. Signed-in visitors are
not only administrators here: sign-up is open by default.

### Endpoints

Under the `file-management` prefix (`FileGroup`):

| Route | Notes |
| --- | --- |
| `POST /file-management/upload` | `AllowFileUploads()`; form fields `file` (`IFormFile`) and optional `accountOwned`; returns `{ id, fileName }`; authenticated, no permission |
| `GET /file-management/{fileName}` | streams via `Send.StreamAsync`; authenticated, no permission — ownership is the check |
| `DELETE /file-management/{fileName}` | `Permissions(Allow.File_Delete)`, returns `Send.NoContentAsync` |

`{fileName}` must be a bare file name (validator: `fileName == Path.GetFileName(fileName)`).

### Serving a file to anonymous visitors

The file endpoints need a session, so a crawler or an anonymous visitor cannot read through them.
Public content goes through an anonymous endpoint of the feature that decided it is public, which
calls `IFileService.OpenPublicAsync(fileId)`. That method checks nothing, so the endpoint must:

- answer only for files it references itself — `SeoImageEndpoint` (`GET /seo/images/{fileId}`) serves
  a file only while a live `SeoPage.OgImageFileId` or the `Seo` setting's `DefaultOgImageFileId`
  names it;
- answer every other identifier, an existing file included, with a plain 404 (`Send.NotFoundAsync`),
  so it never confirms which files exist;
- set its own `Cache-Control` (`public, max-age=3600` for the SEO image).

Never add an anonymous route that takes a stored file name or an arbitrary id and streams it.

### Referencing a file from your feature

Store the returned value on your entity — no foreign key, no navigation to `StoredFile` (it is private
to the slice):

- a file read back by signed-in callers: the filename string (`User.Image`, read through
  `GET /file-management/{fileName}`);
- a file served publicly: the `Guid` id (`SeoPage.OgImageFileId`), resolved by the public endpoint.

Upload first, then save the entity with what the upload returned. When the value is replaced or the
owning row deleted and the file should go too, call `IFileService.DeleteAsync` explicitly
(`UpdateProfileEndpoint` deletes the old image); nothing cascades.

Validate what you accept in your own endpoint's validator — extension and size for your use case —
because the upload endpoint is deliberately generic.

### Size limit

`Payload:MaximumSize` (default 25 MB) drives Kestrel's `MaxRequestBodySize`,
`FormOptions.MultipartBodyLengthLimit`, and the global `ToLargePayloadProcessor`, which answers an
oversized request with 413 `payloadTooLarge`. Change the setting, not the individual limits; it is
validated on start (1 byte – 1 GB). It is the only server-side limit on an upload.

## Web

`filesApi` (`store/api/file-management/files`) is intentionally **untagged** — there is no listing to
invalidate:

```ts
const [uploadFile] = useFileUploadMutation()   // { file, accountOwned? } → FormData internally
const { data: blob } = useFileGetQuery({ fileName }, { skip: !fileName })  // responseHandler → blob()
const [fetchFile] = useLazyFileGetQuery()
const [deleteFile] = useFileDeleteMutation()
```

Components:

- `FileUpload` (`@/components/ui/form`) — uploads on selection. Props include `name`, `fileName`
  (current value), `onUploaded(res)` (`res.id`, `res.fileName`), `onClear`, `accept`, `maxSizeBytes`,
  `validateFile`, `accountOwned`, `forceDelete` (default `true`: replacing/clearing deletes the old
  file on the server — pass `false` when the entity save decides that), and a render-prop `children`
  for custom UI. Failures go through `apiErrorAlert`.
- `MultiFileUpload` (`@/components/ui/form`) — `fileNames`, `onFilesChanged(names)`, `maxSizeBytes`
  (default 10 MB), `accept` (default `image/*`), `forceDelete`. It has no `accountOwned`, so its
  uploads are always application files.
- `FormImageUpload` (`@/components/custom`) — a Formik field holding a stored file's **id**, for an
  image served publicly (the SEO screen's sharing image, the SEO settings' default image). It uploads
  with `forceDelete={false}`, previews a fresh upload through the file-get endpoint and a saved one
  through `seoImageUrl(fileId)` (the public SEO image route).
- `ImagePreview` (`@/components/custom`) — `imageName`, `alt`, `fallback`, `objectFit`; fetches the
  blob through `useFileGetQuery` and renders an object URL.

`maxSizeBytes` is a client-side check only; keep it at or below `Payload:MaximumSize`
(`lib/utils/upload-limit.ts` formats a limit for a message). A typical Formik form keeps the returned
filename in form state (`onUploaded={(res) => setFieldValue('image', res.fileName)}`) and submits that
string with the rest of the payload — see `app/[lang]/(account)/account/profile/_components/update-profile.tsx`.

Display a signed-in caller's file through `ImagePreview` / `useFileGetQuery`, not a bare `<img src>`
to the API: the GET needs the caller's credentials and ownership. A public page shows only images a
public endpoint serves (the SEO image URL), never a `/file-management` address.

## Checklist

- [ ] Upload through `IFileService` / `useFileUploadMutation`, never direct filesystem access
- [ ] Ownership chosen deliberately: application file (default) or `accountOwned` for anything personal
- [ ] Every `DownloadAsync`/`DeleteAsync` caller branches on `FileAccessStatus`
- [ ] `OpenPublicAsync` called only from an anonymous endpoint that serves files it references itself and 404s everything else
- [ ] Entity stores the returned filename (or id, for public files); replacing/deleting it deletes the file if it should
- [ ] Extension/size validation added for your feature's own rules
- [ ] `Payload:MaximumSize` adjusted (not Kestrel/FormOptions individually) if bigger files are needed
- [ ] New storage backend implemented as an `IStorageProvider` and registered in the feature module
