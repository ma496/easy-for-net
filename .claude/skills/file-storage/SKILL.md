---
name: file-storage
description: Upload, serve and delete files — the FileManagement feature (IFileService/IStorageProvider), tenant vs account-owned attribution on StoredFile, payload size limits and the plan's FileManagement features, storing a file reference on an entity, and the FileUpload/MultiFileUpload/ImagePreview components on the web side. Use when a feature needs attachments, avatars or any binary content.
---

# Files

The `FileManagement` feature owns everything binary. Other features reference a file by the
**stored filename string** and never touch the filesystem themselves.

## Backend

Two layers, both `[AllowOutside]` so other features may depend on them:

- `IStorageProvider` — the physical backend (`SaveAsync`, `GetAsync`, `DeleteAsync`, `Exists`). It
  knows nothing about tenants. `LocalStorageProvider` writes to `<ContentRoot>/uploads`, rejecting
  any name that is not a bare filename or that would escape the folder.
- `IFileService` — the application-level API:
  - `UploadAsync(stream, fileName, contentType, accountOwned = false, ct)` → the stored name
    (`{Guid}{extension}`), after recording a `StoredFile` row.
  - `DownloadAsync(fileName, ct)` → `FileDownloadResult { Status, Content, ContentType, OriginalFileName }`.
  - `DeleteAsync(fileName, ct)` → `FileDeleteResult { Status }`.

Both implementations are `[NoDirectUse]` — inject the interfaces. Swapping storage (S3, Azure Blob)
means writing a new `IStorageProvider` and changing one line in `FileManagementFeature.AddServices`;
attribution lives in `FileService`, so who can read what does not change.

### Attribution

Every upload writes a `StoredFile` (`IMayHaveTenant`) that decides every later read and delete — a
stored name is an address, never a permission:

| Upload | `TenantId` | `OwnerUserId` | Readable by |
| --- | --- | --- | --- |
| default (`accountOwned: false`) | the active tenant | null | callers acting in that tenant |
| `accountOwned: true` (avatars, per-user files) | null | the caller | that account, in any tenant or none |

A tenant-scoped upload needs an active tenant (the endpoint refuses with `noActiveTenant`
otherwise). Download/delete return a `FileAccessStatus`: `Granted`, `NotFound`, `CrossTenantAccess`,
`TenantSuspended` (content kept, not served), `TenantNotFound`. Always branch on `Status`; the file
endpoints map `NotFound` → 404 and the rest → 400 with `crossTenantFileAccess` / `tenantSuspended` /
`tenantNotFound`. Uploading account-owned content from code that holds unsaved tenant-scoped
additions in the same `AppDbContext`: save those first. Tenancy concepts: `multi-tenancy` skill.

### Endpoints

Under the `file-management` prefix (`FileGroup`):

| Route | Notes |
| --- | --- |
| `POST /file-management/upload` | `AllowFileUploads()`; form fields `file` (`IFormFile`) and optional `accountOwned`; returns `{ fileName }`; authenticated, no permission |
| `GET /file-management/{fileName}` | streams via `Send.StreamAsync`; authenticated, no permission — attribution is the check |
| `DELETE /file-management/{fileName}` | `Permissions(Allow.File_Delete)`, returns `Send.NoContentAsync` |

`{fileName}` must be a bare file name (validator: `fileName == Path.GetFileName(fileName)`).

### Referencing a file from your feature

Store the returned filename on your entity as a plain nullable string (`User.Image` is the existing
example) — no foreign key, no navigation to `StoredFile` (it is private to the slice). Upload first,
then save the entity with the name the upload returned. When the value is replaced or the owning row
deleted and the file should go too, call `IFileService.DeleteAsync` explicitly
(`UpdateProfileEndpoint` deletes the old image); nothing cascades.

Pick the attribution to match the owner: data belonging to a tenant uploads tenant-scoped; data
belonging to a person across tenants uploads `accountOwned`. Validate what you accept in your own
endpoint's validator — extension and size for your use case — because the upload endpoint is
deliberately generic.

### Size limits and plan features

Two layers, both enforced:

- `Payload:MaximumSize` (default 25 MB) drives Kestrel's `MaxRequestBodySize`,
  `FormOptions.MultipartBodyLengthLimit`, and the global `ToLargePayloadProcessor`, which answers an
  oversized request with 413 `payloadTooLarge`. Change the setting, not the individual limits; it is
  validated on start (1 byte – 1 GB).
- Inside a tenant, `FileUploadEndpoint` also checks the plan: `FileManagement.Enabled` via
  `IFeatureChecker.CheckEnabledAsync` (403 `featureDisabled`) and `FileManagement.MaxFileSizeMb`
  (403 `featureLimitExceeded`, thrown as `FeatureLimitExceededException`). This applies to
  account-owned uploads made inside a tenant too; an upload in platform scope answers to
  `Payload:MaximumSize` alone. The `Files` permission group (incl. `File_Delete`) requires
  `FileManagement.Enabled`. See the `feature-management` skill.

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
  (current value), `onUploaded(res)`, `onClear`, `accept`, `maxSizeBytes`, `validateFile`,
  `accountOwned`, `forceDelete` (default `true`: replacing/clearing deletes the old file on the
  server — pass `false` when the entity save decides that), and a render-prop `children` for custom
  UI. Failures go through `apiErrorAlert`.
- `MultiFileUpload` (`@/components/ui/form`) — `fileNames`, `onFilesChanged(names)`, `maxSizeBytes`,
  `accept`, `forceDelete`.
- `ImagePreview` (`@/components/custom`) — `imageName`, `alt`, `fallback`, `objectFit`; fetches the
  blob through `useFileGetQuery` and renders an object URL.

Both upload components enforce the stricter of `maxSizeBytes` and `usePlanMaxUploadBytes()`
(`hooks/use-plan-max-upload-bytes.ts`, reading `FileManagement.MaxFileSizeMb` from the caller's
features when a tenant is active; helpers in `lib/utils/upload-limit.ts`). A typical Formik form
keeps the returned filename in form state (`onUploaded={(res) => setFieldValue('image', res.fileName)}`)
and submits that string with the rest of the payload — see `profile/_components/update-profile.tsx`.

Display a stored file through `ImagePreview` / `useFileGetQuery`, not a bare `<img src>` to the API:
the GET needs the caller's credentials and attribution, and a cross-tenant name answers 400.

## Checklist

- [ ] Upload through `IFileService` / `useFileUploadMutation`, never direct filesystem access
- [ ] Attribution chosen deliberately: tenant-scoped (default) or `accountOwned`
- [ ] Every `DownloadAsync`/`DeleteAsync` caller branches on `FileAccessStatus`
- [ ] Entity stores the returned filename string; replacing/deleting it deletes the file if it should
- [ ] Extension/size validation added for your feature's own rules
- [ ] `Payload:MaximumSize` adjusted (not Kestrel/FormOptions individually) if bigger files are needed; plan limit considered
- [ ] New storage backend implemented as an `IStorageProvider` and registered in the feature module
