'use client'
import { useId, useState } from 'react'
import { useLocalizedRouter } from '@/hooks/use-localized-router'
import { SearchableItem, searchableItems } from '@/searchable-items'
import { authUrls } from '@/auth-urls'
import { LocalizedLink } from '@/components/ui'
import { useTranslation } from '@/i18n'
import { useAppSelector } from '@/store/hooks'
import { Search } from 'lucide-react'
import { isAllowed } from '@/lib/utils'

/**
 * Header search input that fuzzy-matches the list of searchable (and authorized) navigation items, exposes a keyboard-navigable result list, and routes to the selected item on Enter.
 */
export const SearchComponent = () => {
  const router = useLocalizedRouter()
  const { t } = useTranslation()
  const [search, setSearch] = useState(true)
  const [searchQuery, setSearchQuery] = useState('')
  const [searchResults, setSearchResults] = useState<SearchableItem[]>([])
  const [activeIndex, setActiveIndex] = useState(-1)
  const authState = useAppSelector((state) => state.auth)

  const getSearchableItems = (query: string): SearchableItem[] => {
    if (!query || query.trim() === '') {
      return []
    }

    return searchableItems
      .filter((item) => {
        const authUrl = authUrls.find((a) => a.url === item.url)
        const isAuthorized = authUrl?.permissions ? isAllowed(authState, authUrl.permissions) : true
        return t(item.title).toLowerCase().includes(query.trim().toLowerCase()) && isAuthorized
      })
      .slice(0, 5)
  }

  const handleSearchChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const query = event.target.value
    setSearchQuery(query)
    setSearchResults(getSearchableItems(query))
    setActiveIndex(0)
  }

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      setActiveIndex((prevIndex) => (prevIndex + 1) % searchResults.length)
    } else if (event.key === 'ArrowUp') {
      setActiveIndex((prevIndex) => (prevIndex - 1 + searchResults.length) % searchResults.length)
    } else if (event.key === 'Enter' && activeIndex >= 0) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      router.push(searchResults[activeIndex].url as any)
      setSearchQuery('')
      setSearchResults([])
      setActiveIndex(-1)
    }
  }

  const highlightText = (text: string, query: string) => {
    const parts = text.split(new RegExp(`(${query})`, 'gi'))
    return parts.map((part, index) =>
      part.toLowerCase() === query.toLowerCase() ? (
        <span key={index} className="bg-yellow-200">
          {part}
        </span>
      ) : (
        part
      ),
    )
  }

  return (
    <form
      className={`${search && 'block!'} absolute inset-x-0 top-1/2 z-10 mx-4 hidden -translate-y-1/2 sm:relative sm:top-0 sm:mx-0 sm:block sm:translate-y-0`}
      onSubmit={(e) => {
        e.preventDefault()
        setSearch(false)
        if (searchResults.length > 0) {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          router.push(searchResults[0].url as any)
          setSearchQuery('')
          setSearchResults([])
        }
      }}
    >
      <div className="relative w-full sm:w-auto">
        <input
          type="text"
          id={useId()}
          className="peer form-input w-full bg-gray-100 pr-9 pl-9 placeholder:tracking-widest sm:bg-transparent sm:ltr:pr-4 sm:rtl:pl-4"
          placeholder={t('common.search')}
          value={searchQuery}
          onChange={handleSearchChange}
          onKeyDown={handleKeyDown}
        />
        {searchResults.length > 0 && (
          <ul className="absolute w-full bg-white text-black shadow-sm dark:bg-[#1b2e4b] dark:text-white-dark">
            {searchResults.map((item: SearchableItem, index) => (
              <li key={item.url} className={index === activeIndex ? 'bg-primary/10 text-primary hover:bg-primary/5' : 'hover:bg-primary/5 hover:text-primary'}>
                <LocalizedLink
                  // eslint-disable-next-line @typescript-eslint/no-explicit-any
                  href={item.url as any}
                  className="block px-4 py-2"
                  onClick={() => {
                    setSearchQuery('')
                    setSearchResults([])
                    setActiveIndex(-1)
                  }}
                >
                  {highlightText(t(item.title), searchQuery)}
                </LocalizedLink>
              </li>
            ))}
          </ul>
        )}
        <button type="button" className="absolute inset-0 right-auto h-9 w-9 appearance-none peer-focus:text-primary">
          <Search className="mx-auto text-gray-300 dark:text-gray-400" size={20} />
        </button>
      </div>
    </form>
  )
}

