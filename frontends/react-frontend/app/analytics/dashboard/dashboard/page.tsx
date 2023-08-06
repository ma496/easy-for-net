'use client'

import { usePathname } from "next/navigation"

const Dashboard = () => {
  const pathname = usePathname()

  return (
    <span>{pathname}</span>
  )
}

export default Dashboard
