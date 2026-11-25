import { ImageResponse } from 'next/og'

/** The home-screen icon's size: Apple's 180px touch icon. */
export const size = { width: 180, height: 180 }

/** Served as a PNG, the format iOS expects for a touch icon. */
export const contentType = 'image/png'

/**
 * The Apple touch icon, generated at build time: the brand mark (`app/icon.svg`, `LogoMark`) drawn full
 * bleed, since iOS rounds the corners itself. Served from the site root at `/apple-icon`, which
 * `proxy.ts`'s matcher keeps out of locale routing.
 */
export default function AppleIcon() {
  const bar = (width: number, top: number) => <div style={{ position: 'absolute', left: 50, top, width, height: 20, borderRadius: 10, background: '#fff' }} />

  return new ImageResponse(
    (
      <div style={{ display: 'flex', width: '100%', height: '100%', background: '#f97316', position: 'relative' }}>
        {bar(80, 48)}
        {bar(54, 80)}
        {bar(80, 112)}
      </div>
    ),
    size,
  )
}
