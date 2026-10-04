import { cn } from '@/lib/utils'
import React from 'react'

/**
 * Props for the Card root component, a styled container that accepts standard div attributes.
 */
interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  children: React.ReactNode
  className?: string
}

/**
 * Card is a rounded, bordered container with light/dark styling that composes the CardHeader, CardTitle, CardContent, and CardFooter subcomponents.
 * It forwards a ref to the underlying div element.
 */
const Card = React.forwardRef<HTMLDivElement, CardProps>(({ children, className, ...props }, ref) => {
  return (
    <div
      ref={ref}
      className={cn('w-fit rounded-xl border border-border bg-surface text-foreground shadow-xs', className)}
      {...props}
    >
      {children}
    </div>
  )
})

/**
 * CardHeader renders the top section of a Card as a flex column container with padding.
 * It forwards a ref to the underlying div element.
 */
const CardHeader = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(({ className, ...props }, ref) => (
  <div ref={ref} className={cn('flex w-full flex-col gap-1 p-5 sm:p-6', className)} {...props} />
))

/**
 * CardTitle renders a heading-3 element used to label a Card's content.
 * It forwards a ref to the underlying heading element.
 */
const CardTitle = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLHeadingElement>>(({ className, ...props }, ref) => (
  <h3 ref={ref} className={cn('text-base font-semibold tracking-tight text-foreground', className)} {...props} />
))

/**
 * CardContent renders the main body area of a Card with horizontal padding and no top padding.
 * It forwards a ref to the underlying div element.
 */
const CardContent = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(({ className, ...props }, ref) => <div ref={ref} className={cn('w-full p-5 pt-0 sm:p-6 sm:pt-0', className)} {...props} />)

/**
 * CardFooter renders a flex row area at the bottom of a Card, typically used for actions.
 * It forwards a ref to the underlying div element.
 */
const CardFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(({ className, ...props }, ref) => (
  <div ref={ref} className={cn('flex w-full items-center gap-2 p-5 pt-0 sm:p-6 sm:pt-0', className)} {...props} />
))

Card.displayName = 'Card'
CardHeader.displayName = 'CardHeader'
CardTitle.displayName = 'CardTitle'
CardContent.displayName = 'CardContent'
CardFooter.displayName = 'CardFooter'

export { Card, CardHeader, CardTitle, CardContent, CardFooter }
