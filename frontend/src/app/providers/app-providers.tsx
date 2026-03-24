import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type PropsWithChildren } from 'react'

export function AppProviders({ children }: PropsWithChildren) {
  const isTest = typeof process !== 'undefined' && process.env.NODE_ENV === 'test'
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            gcTime: isTest ? Number.POSITIVE_INFINITY : 5 * 60 * 1000,
            refetchOnWindowFocus: false,
            retry: isTest ? false : 1,
            staleTime: isTest ? Number.POSITIVE_INFINITY : 30 * 1000,
          },
        },
      }),
  )

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}
