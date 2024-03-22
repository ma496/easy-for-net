// import { MainLayout } from '@/components/layout/main'
// import { MainLayoutProvider } from '@/components/layout/main/context-provider'

// export default function AdminLayout({
//   children,
// }: {
//   children: React.ReactNode
// }) {
//   return (
//     <MainLayoutProvider>
//       <MainLayout>
//         {children}
//       </MainLayout>
//     </MainLayoutProvider>
//   )
// }

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <div>
      {children}
    </div>
  )
}
