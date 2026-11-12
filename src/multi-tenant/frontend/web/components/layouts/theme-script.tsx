'use client'

/**
 * Runs before first paint so a dark-mode visitor never sees a light flash: reads the saved theme
 * (light, dark or system) and sets the `dark` class on <html> that every color token keys off.
 * App.tsx keeps the class in step afterwards.
 */
const themeScript = `(function(){try{var t=localStorage.getItem('theme')||'system';var d=t==='dark'||(t==='system'&&matchMedia('(prefers-color-scheme: dark)').matches);document.documentElement.classList.toggle('dark',d);}catch(e){}})()`

/**
 * The inline theme script for the root layout's <head>.
 * Only the server-rendered copy needs to run. When the client renders the layout afresh (switching
 * language swaps the `[lang]` root layout), React would create a script it never executes and warns
 * about it, so the client renders it as an inert data block instead; App.tsx sets the class there.
 */
export function ThemeScript() {
  return (
    <script
      type={typeof window === 'undefined' ? 'text/javascript' : 'text/plain'}
      suppressHydrationWarning
      dangerouslySetInnerHTML={{ __html: themeScript }}
    />
  )
}
