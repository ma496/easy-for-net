import { Loader } from '@/components/ui'

/**
 * AppLoading is a full-screen centered loading spinner shown while the application is bootstrapping.
 */
export const AppLoading = () => {
  return (
    <div className="flex h-screen items-center justify-center bg-[#fafafa] dark:bg-[#060818]">
      <Loader size="xl" />
    </div>
  )
}
