# ADR 004: Durable media storage and upload validation

## Decision

Administrative uploads cross a single `IMediaStorageService` boundary. The service decodes and validates the bytes, strips metadata, auto-orients, re-encodes each accepted source as WebP, and stores it under a random object key. It records asset ownership, checksum, dimensions, MIME type, variants, state, and timestamps in `content.media_assets`.

`Local` storage is deliberately accepted only for `Development`; every other environment must configure the S3-compatible provider with secret-backed credentials. Public URLs always derive from `MediaStorage:PublicBaseUrl`, never from an incoming HTTP `Host` header.

GIF, multi-frame, malformed, spoofed-type, oversized and oversized-decoded images are rejected. This release intentionally uses a no-op `IMediaMalwareScanner` implementation as a non-blocking seam; deployments which require scanning must replace it before media is marked available.

## Lifecycle and failure handling

The database row is first recorded as `Pending`. Objects are written next; a storage failure marks the row `Failed` and attempts compensation. A database failure after objects are written leaves the pending row observable for conservative cleanup rather than claiming a valid asset. Only `Available` assets can be read as metadata.

The hourly cleanup worker deletes only old, unreferenced Pending, Failed, or Available assets. It checks product images, banners, and blog images before deleting, so a shared URL is retained until its final reference disappears. Rejected/deleted asset records remain as lifecycle audit data.

## Existing local uploads

Files under the former `wwwroot/uploads` path are not automatically imported because their content and ownership were never validated or tracked. Keep them readable during a transition, then run a one-off operator migration that decodes each file through the new service, updates each stored URL after successful persistence, validates references, and removes the legacy file only after the retention window. New uploads use `/media/{random-key}` immediately.

For local S3-compatible development, set the S3 values in `.env` and run `docker compose --profile storage up`. The `minio-init` tool creates a development bucket and download policy; production buckets, CDN domains, and credentials are deployment-owned.
