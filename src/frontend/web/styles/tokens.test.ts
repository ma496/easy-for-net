import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const webDirectory = fileURLToPath(new URL('..', import.meta.url))

/**
 * Colors in the web app are semantic tokens (styles/tailwind.css) that switch between light and dark by
 * themselves. A literal color, a stock palette class or a `dark:` color override bypasses them, so a
 * screen built with one looks right in a single theme only. These are the shapes that do.
 */
const forbidden: [RegExp, string][] = [
  [/\b(?:bg|text|border|ring|fill|stroke|from|to|via|divide|outline|shadow|placeholder)-\[(?:#|rgb|hsl)/, 'an arbitrary color value'],
  [/(?<![\w-])(?:[a-z-]+:)*(?:bg|text|border|ring|divide|from|to|via|fill|stroke|placeholder)-(?:gray|slate|zinc|neutral|stone|red|rose|green|emerald|teal|yellow|amber|orange|blue|sky|cyan|indigo|violet|purple|pink|fuchsia|lime)-\d{2,3}\b/, 'a stock palette color'],
  [/(?<![\w-])(?:[a-z-]+:)*dark:(?:[a-z-]+:)*(?:bg|text|border|ring|divide|shadow|fill|stroke|placeholder|from|to|via)-/, 'a dark: color override'],
  [/(?<![\w-])(?:bg|text|border)-(?:white-light|white-dark|dark-light)\b|(?<![\w-])(?:text|bg)-dark\b|(?<![\w-])(?:bg-white|text-black)(?![\w/-])/, 'a retired palette class'],
]

/** Every .tsx file under the given directory. */
const sources = (directory: string): string[] =>
  readdirSync(directory).flatMap((name) => {
    const path = join(directory, name)
    if (statSync(path).isDirectory()) return sources(path)
    return path.endsWith('.tsx') ? [path] : []
  })

/** The hex custom properties declared in one top-level block of the stylesheet (`:root` or `.dark`). */
const palette = (css: string, selector: string): Record<string, string> => {
  const block = css.match(new RegExp(`^${selector.replace('.', '\\.')} \\{([^}]*)\\}`, 'm'))?.[1] ?? ''
  return Object.fromEntries([...block.matchAll(/--([\w-]+):\s*(#[0-9a-fA-F]{6});/g)].map(([, name, value]) => [name, value]))
}

/** WCAG 2 contrast ratio between two opaque hex colours. */
const contrast = (a: string, b: string): number => {
  const luminance = (hex: string) => {
    const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4))
    return 0.2126 * r + 0.7152 * g + 0.0722 * b
  }
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (high + 0.05) / (low + 0.05)
}

describe('design tokens', () => {
  const css = readFileSync(join(webDirectory, 'styles', 'tailwind.css'), 'utf8')
  const accents = ['primary', 'secondary', 'success', 'warning', 'danger', 'info']

  it.each([':root', '.dark'])('keeps every text token readable (WCAG AA) in %s', (selector) => {
    const tokens = palette(css, selector)
    const pairs: [string, string][] = [
      ...['foreground', 'muted-foreground', 'subtle-foreground'].flatMap((text) => ['background', 'surface', 'surface-2'].map((ground): [string, string] => [text, ground])),
      ...accents.map((accent): [string, string] => [accent, 'surface']),
      ...accents.map((accent): [string, string] => [`${accent}-foreground`, accent]),
    ]
    const failing = pairs
      .filter(([text, ground]) => tokens[text] && tokens[ground])
      .map(([text, ground]) => ({ pair: `${text} on ${ground}`, ratio: Math.round(contrast(tokens[text], tokens[ground]) * 100) / 100 }))
      .filter(({ ratio }) => ratio < 4.5)

    expect(Object.keys(tokens).length).toBeGreaterThan(15)
    expect(failing).toEqual([])
  })

  const files = [...sources(join(webDirectory, 'app')), ...sources(join(webDirectory, 'components')), join(webDirectory, 'App.tsx')]

  it('finds the screens to check', () => {
    expect(files.length).toBeGreaterThan(50)
  })

  it('colors every screen and component with semantic tokens only', () => {
    const offences = files.flatMap((file) =>
      readFileSync(file, 'utf8')
        .split('\n')
        .flatMap((line, index) =>
          forbidden.filter(([pattern]) => pattern.test(line)).map(([, what]) => `${relative(webDirectory, file).replaceAll('\\', '/')}:${index + 1} uses ${what}: ${line.trim().slice(0, 120)}`),
        ),
    )

    expect(offences).toEqual([])
  })
})
