import { fetchRobots } from '@/lib/server/seo'
import { toRobotsTxt } from '@/lib/seo/robots'

/** Rendered on request, never at build time: whether the site may be indexed is a setting edited at run time. */
export const dynamic = 'force-dynamic'

/**
 * `/robots.txt`, from `GET /seo/robots`. A route handler rather than `robots.ts`, because the
 * administrator's extra lines are raw text that `MetadataRoute.Robots` cannot express.
 */
export async function GET() {
  const robots = await fetchRobots()
  return new Response(toRobotsTxt(robots), { headers: { 'Content-Type': 'text/plain; charset=utf-8' } })
}
