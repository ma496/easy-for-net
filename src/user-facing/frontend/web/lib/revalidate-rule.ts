import { Allow } from '@/allow'

/** The cache tags the revalidate route accepts, each with the permission a caller needs to clear it. */
export const REVALIDATABLE_TAGS: Record<string, string> = {
  seo: Allow.Seo_Update,
  localization: Allow.Localization_Update,
}

/** What the revalidate route decides for one request. */
export type RevalidationDecision = { kind: 'revalidate'; tags: string[] } | { kind: 'badRequest' } | { kind: 'forbidden' }

/**
 * Decides a revalidation request: the body must name at least one tag, every tag must be one of
 * {@link REVALIDATABLE_TAGS}, and the caller's permissions must include the one each tag needs. Pure,
 * so the rule is testable without a request or an API.
 *
 * @param body The parsed request body.
 * @param permissions The caller's permission names, as `GET /account/get-info` reports them.
 */
export const decideRevalidation = (body: unknown, permissions: ReadonlySet<string>): RevalidationDecision => {
  const tags = body && typeof body === 'object' && Array.isArray((body as { tags?: unknown }).tags) ? (body as { tags: unknown[] }).tags : null
  if (!tags || tags.length === 0 || !tags.every((tag): tag is string => typeof tag === 'string' && Object.hasOwn(REVALIDATABLE_TAGS, tag))) {
    return { kind: 'badRequest' }
  }
  const unique = [...new Set(tags)]
  return unique.every((tag) => permissions.has(REVALIDATABLE_TAGS[tag])) ? { kind: 'revalidate', tags: unique } : { kind: 'forbidden' }
}
