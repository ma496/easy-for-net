import React from 'react'

/**
 * Layout shell for all routes inside the (auth) route group, providing the shared page background and text color.
 * Each page picks its own frame: the split `AuthShell` for the signed-out screens, the single-column
 * `AccountShell` for the signed-in account screens (both in `_components/`).
 */
const AuthLayout = ({ children }: { children: React.ReactNode }) => {
  return <div className="min-h-screen bg-background text-foreground">{children}</div>
}

export default AuthLayout
