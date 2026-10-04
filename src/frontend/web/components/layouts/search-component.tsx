'use client'
import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useLocalizedRouter } from '@/hooks'
import { SearchableItem, searchableItems } from '@/searchable-items'
import { authUrls } from '@/auth-urls'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { CornerDownLeft, FileText, Search } from 'lucide-react'
import { cn, isAllowed, isPathAvailable } from '@/lib/utils'
import { navItems, NavItem } from '@/nav-items'

/** Each navigation url's icon - a child without one of its own takes its parent's - so a search result shows the icon its screen has in the sidebar. */
const iconByUrl = (() => {
  const icons = new Map<string, NavItem['icon']>()
  const visit = (items: NavItem[], inherited?: NavItem['icon']) =>
    items.forEach((item) => {
      const icon = item.icon ?? inherited
      if (icon && !icons.has(item.url)) icons.set(item.url, icon)
      if (item.children) visit(item.children, icon)
    })
  visit(navItems.flatMap((entry) => ('items' in entry ? entry.items : [entry])))
  return icons
})()

/**
 * Header search: a trigger (a search field look-alike from md up, an icon below it) that opens a
 * command palette over the page, also on Ctrl/⌘+K. The palette lists the searchable navigation
 * items the caller may open - all of them while the query is empty, the matches once typed - is
 * driven by the arrow keys, and routes to the chosen item on Enter or click.
 */
export const SearchComponent = () => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const inputId = useId()
  const authState = useAppSelector((state) => state.auth)

  const available = useMemo(
    () =>
      searchableItems.filter((item) => {
        const authUrl = authUrls.find((a) => a.url === item.url)
        const isAuthorized = authUrl?.permissions ? isAllowed(authState, authUrl.permissions) : true
        return isAuthorized && isPathAvailable(authState.user, item.url)
      }),
    [authState],
  )

  const results = useMemo(() => {
    const q = query.trim().toLowerCase()
    return (q ? available.filter((item) => t(item.title).toLowerCase().includes(q)) : available).slice(0, 8)
  }, [available, query, t])

  // The element focused before the palette opened, given focus back when it closes.
  const returnFocusRef = useRef<HTMLElement | null>(null)

  const openPalette = useCallback(() => {
    returnFocusRef.current = document.activeElement as HTMLElement | null
    setOpen(true)
  }, [])

  // Every way out (Esc, Ctrl/⌘+K, the backdrop, choosing a result) resets the query, so the next open starts clean.
  const close = useCallback(() => {
    setOpen(false)
    setQuery('')
    setActiveIndex(0)
    returnFocusRef.current?.focus()
  }, [])

  const go = (item: SearchableItem) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    router.push(item.url as any)
    close()
  }

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        if (open) close()
        else openPalette()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, close, openPalette])

  // Esc closes from anywhere in the dialog, and Tab stays inside it: the input is the only stop, since the arrow keys move through the results.
  const handleDialogKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault()
      close()
    } else if (event.key === 'Tab') {
      event.preventDefault()
      inputRef.current?.focus()
    }
  }

  useEffect(() => {
    if (open) requestAnimationFrame(() => inputRef.current?.focus())
  }, [open])

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActiveIndex((index) => (results.length ? (index + 1) % results.length : 0))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActiveIndex((index) => (results.length ? (index - 1 + results.length) % results.length : 0))
    } else if (event.key === 'Enter' && results[activeIndex]) {
      event.preventDefault()
      go(results[activeIndex])
    }
  }

  const highlightText = (text: string) => {
    const q = query.trim()
    if (!q) return text
    const escaped = q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    return text.split(new RegExp(`(${escaped})`, 'gi')).map((part, index) =>
      part.toLowerCase() === q.toLowerCase() ? (
        <mark key={index} className="bg-transparent font-semibold text-inherit">
          {part}
        </mark>
      ) : (
        part
      ),
    )
  }

  return (
    <>
      <button
        type="button"
        onClick={openPalette}
        className="hidden h-9 w-56 items-center gap-2 rounded-md border border-border bg-surface px-3 text-sm text-subtle-foreground shadow-xs transition-colors hover:bg-surface-2 hover:text-muted-foreground md:flex lg:w-64"
      >
        <Search size={15} className="shrink-0" />
        <span className="flex-1 truncate text-start">{t('common.search')}</span>
        <span className="kbd">Ctrl K</span>
      </button>
      <button type="button" className="icon-btn md:hidden" aria-label={t('common.search')} onClick={openPalette}>
        <Search size={18} />
      </button>

      {open &&
        createPortal(
          <div className="fixed inset-0 z-[100] flex items-start justify-center px-4 pt-[12vh]" role="dialog" aria-modal="true" aria-label={t('common.search')} onKeyDown={handleDialogKeyDown}>
            <div className="absolute inset-0 animate-fade-in bg-overlay backdrop-blur-sm" onClick={close} />
            <div className="relative w-full max-w-xl animate-scale-in overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
              <div className="flex items-center gap-3 border-b border-border px-4">
                <Search size={18} className="shrink-0 text-subtle-foreground" />
                <input
                  ref={inputRef}
                  id={inputId}
                  type="text"
                  autoComplete="off"
                  className="h-12 w-full bg-transparent text-[15px] text-foreground outline-none placeholder:text-subtle-foreground"
                  placeholder={t('common.search')}
                  value={query}
                  onChange={(event) => {
                    setQuery(event.target.value)
                    setActiveIndex(0)
                  }}
                  onKeyDown={handleKeyDown}
                />
                <button type="button" className="kbd cursor-pointer" onClick={close}>
                  Esc
                </button>
              </div>
              <ul className="max-h-[50vh] overflow-y-auto p-1.5">
                {results.length === 0 && <li className="px-3 py-8 text-center text-sm text-muted-foreground">{t('common.noResults')}</li>}
                {results.map((item, index) => {
                  const Icon = iconByUrl.get(item.url) ?? FileText
                  return (
                    <li key={item.url}>
                      <button
                        type="button"
                        onMouseMove={() => setActiveIndex(index)}
                        onClick={() => go(item)}
                        className={cn(
                          'flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-start text-sm transition-colors',
                          index === activeIndex ? 'bg-primary/10 text-primary' : 'text-foreground',
                        )}
                      >
                        <span
                          className={cn('flex size-7 shrink-0 items-center justify-center rounded-md border', index === activeIndex ? 'border-primary/30 bg-primary/10' : 'border-border bg-surface-2')}
                        >
                          <Icon size={14} />
                        </span>
                        <span className="flex-1 truncate">{highlightText(t(item.title))}</span>
                        {index === activeIndex && <CornerDownLeft size={14} className="shrink-0 opacity-70 rtl:-scale-x-100" />}
                      </button>
                    </li>
                  )
                })}
              </ul>
            </div>
          </div>,
          document.body,
        )}
    </>
  )
}
