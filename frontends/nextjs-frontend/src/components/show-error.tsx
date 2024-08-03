import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { clearError } from '@/store/slices/errorSlice'

export function ShowError() {
  const isError = useAppSelector(s => s.error.isError)
  const errorMessage = useAppSelector(s => s.error.message)
  const errorTitle = useAppSelector(s => s.error.title)
  const dispatch = useAppDispatch()

  return (
    <Dialog open={isError} onOpenChange={() => dispatch(clearError())}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>
            <pre>
              <span className="text-red-500">{errorTitle}</span>
            </pre>
          </DialogTitle>
        </DialogHeader>
        <pre>
          <p className="text-red-400 mt-2">{errorMessage}</p>
        </pre>
      </DialogContent>
    </Dialog>
  )
}
