import { Sidebar } from "@/components/layout/sidebar"
import { Header } from "@/components/layout/header"
import React, { ReactNode } from "react"

type AdminLayout = {
  children: ReactNode
}

export default function AdminLayout({children}: AdminLayout) {
  return (
    <div className="grid min-h-screen w-full md:grid-cols-[220px_1fr] lg:grid-cols-[280px_1fr]">
      <Sidebar />
      <div className="flex flex-col">
        <Header />
        <main className="flex flex-1 flex-col gap-4 p-4 lg:gap-6 lg:p-6">
          {children}
        </main>
      </div>
    </div>
  )
}
