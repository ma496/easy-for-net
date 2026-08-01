import { AppLoading } from '@/components/layouts/app-loading'

/**
 * Server-rendered loading UI shown while route segments under the [lang] segment are resolving, displaying the shared AppLoading component.
 */
const loading = () => {
  return <AppLoading />
}

export default loading
