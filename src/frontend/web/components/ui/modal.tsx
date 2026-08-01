import { Dialog, DialogPanel, Transition, TransitionChild } from '@headlessui/react'
import { Fragment, ReactNode, Children, isValidElement, createContext, useContext } from 'react'

/** Allowed maximum widths for the Modal panel. */
type ModalSize = 'sm' | 'lg' | 'xl'

interface BaseProps {
  children: ReactNode
  className?: string
}

/** Props for the top-level Modal component controlling open state, size, and close behavior. */
interface ModalProps extends BaseProps {
  isOpen: boolean
  onClose: () => void
  size?: ModalSize
}

/** Props for the Modal.Header subcomponent, which optionally renders a close button bound to the parent modal. */
interface ModalHeaderProps extends BaseProps {
  showCloseButton?: boolean
}

type ModalFooterProps = BaseProps

// Create context for modal
const ModalContext = createContext<{ onClose: () => void } | null>(null)

/**
 * ModalHeader renders the top bar of a Modal with a title area and an optional close button that invokes the parent modal's onClose handler.
 */
const ModalHeader = ({ children, className = '', showCloseButton = true }: ModalHeaderProps) => {
  const context = useContext(ModalContext)

  return (
    <div className={`flex items-center justify-between bg-[#fbfbfb] px-5 py-3 dark:bg-[#121c2c] ${className}`}>
      <div className="text-lg font-bold">{children}</div>
      {showCloseButton && context?.onClose && (
        <button onClick={context.onClose} type="button" className="cursor-pointer text-white-dark transition-colors duration-200 hover:text-dark" aria-label="Close modal">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="24"
            height="24"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      )}
    </div>
  )
}

/**
 * ModalFooter renders the bottom bar of a Modal as a right-aligned flex row, typically used for action buttons.
 */
const ModalFooter = ({ children, className = '' }: ModalFooterProps) => {
  return <div className={`flex items-center justify-end gap-2 px-5 py-3 ${className}`}>{children}</div>
}

/**
 * Modal is a headless-ui-based dialog with size variants (sm/lg/xl) that composes Modal.Header, content, and Modal.Footer children into a centered, animated panel with a dark backdrop.
 */
export const Modal = ({ isOpen, onClose, children, size = 'lg', className = '' }: ModalProps) => {
  const maxWidthClass = {
    sm: 'max-w-sm',
    lg: 'max-w-xl',
    xl: 'max-w-5xl',
  }[size]

  // Group children by type (header, content, footer)
  const { header, footer, content } = Children.toArray(children).reduce(
    (acc, child) => {
      if (isValidElement(child)) {
        if (child.type === ModalHeader) {
          return { ...acc, header: child }
        }
        if (child.type === ModalFooter) {
          return { ...acc, footer: child }
        }
      }
      return { ...acc, content: [...acc.content, child] }
    },
    { header: null, footer: null, content: [] } as {
      header: ReactNode
      footer: ReactNode
      content: ReactNode[]
    },
  )

  return (
    <Transition appear show={isOpen} as={Fragment}>
      <Dialog as="div" open={isOpen} onClose={onClose}>
        <div className="fixed inset-0 z-998 overflow-y-auto">
          {/* Overlay */}
          <TransitionChild as={Fragment} enter="ease-out duration-300" enterFrom="opacity-0" enterTo="opacity-100" leave="ease-in duration-200" leaveFrom="opacity-100" leaveTo="opacity-0">
            <div className="fixed inset-0 bg-black/60" aria-hidden="true" />
          </TransitionChild>

          {/* Modal */}
          <div className="flex min-h-screen items-center justify-center p-4">
            <TransitionChild
              as={Fragment}
              enter="ease-out duration-300"
              enterFrom="opacity-0 scale-95"
              enterTo="opacity-100 scale-100"
              leave="ease-in duration-200"
              leaveFrom="opacity-100 scale-100"
              leaveTo="opacity-0 scale-95"
            >
              <DialogPanel className={`w-full ${maxWidthClass} panel my-8 overflow-hidden rounded-lg border-0 bg-white p-0 text-black shadow-xl dark:bg-gray-800 dark:text-white-dark ${className}`}>
                <ModalContext.Provider value={{ onClose }}>
                  {header}
                  {content.length > 0 && <div className="p-5 text-base font-medium text-[#1f2937] ltr:text-left rtl:text-right dark:text-white-dark/70">{content}</div>}
                  {footer}
                </ModalContext.Provider>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </Transition>
  )
}

// Compound components
Modal.Header = ModalHeader
Modal.Footer = ModalFooter

export type { ModalProps, ModalHeaderProps, ModalFooterProps, ModalSize }
