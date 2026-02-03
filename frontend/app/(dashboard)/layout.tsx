"use client"

import { useEffect } from "react"
import { useRouter } from "next/navigation"
import { useAuth } from "@/lib/firebase/context"
import { AppSidebar } from "@/components/layout/app-sidebar"
import { SidebarProvider, SidebarInset } from "@/components/ui/sidebar"

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode
}) {
  const { user, loading } = useAuth()
  const router = useRouter()

  useEffect(() => {
    if (!loading && !user) {
      router.push("/login")
    }
  }, [user, loading, router])

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    )
  }

  if (!user) {
    return null
  }

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <div className="flex min-h-screen flex-1 flex-col gap-4 p-4 lg:gap-6 lg:p-6 overflow-x-hidden">
          {children}
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
