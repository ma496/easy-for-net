import { revalidateTag } from 'next/cache'
import { decideRevalidation } from '@/lib/revalidate-rule'
import { serverApiFetch } from '@/lib/server/api-fetch'
import type { GetUserInfoResponse } from '@/store/api/identity/account/account-dtos'

/**
 * `POST /admin/revalidate` with `{ tags: ['seo' | 'localization'] }`: drops the web server's cached
 * copies of the public pages' SEO metadata or anonymous dictionary, so an administrator's edit shows on
 * the next request. It lives under `/admin` (the browser's `/api` belongs to the API) and asks the API
 * who the caller is, forwarding their cookies to `GET /account/get-info`: clearing `seo` needs
 * `Seo.Update`, clearing `localization` needs `Localization.Update`.
 */
export async function POST(request: Request) {
  let body: unknown
  try {
    body = await request.json()
  } catch {
    return Response.json({ message: 'Invalid body' }, { status: 400 })
  }

  let info: GetUserInfoResponse | null = null
  try {
    const response = await serverApiFetch('/account/get-info', { forwardCookies: true, cache: 'no-store' })
    if (response.status === 401) return Response.json({ message: 'Sign in first' }, { status: 401 })
    if (response.ok) info = (await response.json()) as GetUserInfoResponse
  } catch {
    info = null
  }
  if (!info) return Response.json({ message: 'The API could not be reached' }, { status: 503 })

  const permissions = new Set(info.roles.flatMap((role) => role.permissions.map((permission) => permission.name)))
  const decision = decideRevalidation(body, permissions)
  if (decision.kind === 'badRequest') return Response.json({ message: 'Name the tags to revalidate: seo, localization' }, { status: 400 })
  if (decision.kind === 'forbidden') return Response.json({ message: 'Not allowed' }, { status: 403 })

  for (const tag of decision.tags) {
    revalidateTag(tag, { expire: 0 })
  }
  return Response.json({ revalidated: decision.tags })
}
