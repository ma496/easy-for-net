import React from 'react'

/**
 * Outer wrapper of the admin shell.
 */
export const MainContainer = ({ children }: { children: React.ReactNode }) => {
  return <div className="relative min-h-screen bg-background text-foreground">{children}</div>
}
