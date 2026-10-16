'use client'

import React, { AnchorHTMLAttributes } from 'react'
import { ArrowUpRight } from 'lucide-react'
import { useTranslation } from '@/i18n'
import { cn } from '@/lib/utils'

/** Props for WorkspaceLink: a plain anchor to another workspace's address, with an optional class for the trailing arrow. */
export interface WorkspaceLinkProps extends Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href' | 'target' | 'rel'> {
  /** The workspace's absolute address - a tenant's `url` or the `platformUrl`, never one composed here. */
  href: string
  /** Extra classes for the trailing arrow, such as a hover colour driven by the link's `group`. */
  arrowClassName?: string
}

/**
 * WorkspaceLink opens another workspace - a tenant's own address or the platform's - in a new tab. Each
 * host keeps its own session, so another workspace is never entered in place: it is a different site,
 * reached through a plain anchor rather than a `LocalizedLink`, which would keep the visitor on this
 * origin. A trailing arrow marks the new tab, and screen readers are told the same in words.
 */
export const WorkspaceLink = React.forwardRef<HTMLAnchorElement, WorkspaceLinkProps>(({ href, className, arrowClassName, children, ...props }, ref) => {
  const { t } = useTranslation()

  return (
    <a ref={ref} href={href} target="_blank" rel="noopener noreferrer" className={className} {...props}>
      {children}
      <ArrowUpRight aria-hidden="true" className={cn('size-4 shrink-0 text-muted-foreground rtl:-scale-x-100', arrowClassName)} />
      <span className="sr-only">{t('common.opensInNewTab') || '(opens in a new tab)'}</span>
    </a>
  )
})
WorkspaceLink.displayName = 'WorkspaceLink'

/** Props for WorkspaceAddress. */
interface WorkspaceAddressProps {
  /** The address to show, as text. */
  url: string
  className?: string
}

/**
 * WorkspaceAddress shows an address as text: isolated and left-to-right, so it reads correctly inside a
 * right-to-left sentence, in a monospaced face that breaks anywhere rather than overflowing a phone.
 */
export const WorkspaceAddress = ({ url, className }: WorkspaceAddressProps) => (
  <bdi dir="ltr" className={cn('font-mono text-xs break-all', className)}>
    {url}
  </bdi>
)

/** The stand-in a translated sentence is given for a value {@link withNode} puts back as an element: U+2063, an invisible separator no translation contains. */
export const NODE_MARK = String.fromCharCode(0x2063)

/**
 * Puts `node` where a translated sentence carries {@link NODE_MARK} - pass the mark as the variable's
 * value, `t('key', { name: NODE_MARK })` - so a name or an address can sit inside the sentence as its
 * own element (a `<bdi>` that keeps its own direction). A sentence that lost its placeholder in
 * translation still shows the value, after it.
 */
export const withNode = (text: string, node: React.ReactNode): React.ReactNode => {
  const at = text.indexOf(NODE_MARK)
  if (at < 0) {
    return (
      <>
        {text} {node}
      </>
    )
  }
  return (
    <>
      {text.slice(0, at)}
      {node}
      {text.slice(at + NODE_MARK.length)}
    </>
  )
}
