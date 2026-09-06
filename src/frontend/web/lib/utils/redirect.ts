/** Returns whether a redirect stays within this application. */
export const isValidRedirectPath = (path: string): boolean => {
  return path.startsWith('/') && !path.startsWith('//') && path !== '/signin'
}
