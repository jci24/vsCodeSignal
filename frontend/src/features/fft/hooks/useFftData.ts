import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'

import { fftService } from '../service/fftService'
import type { IFftResponse } from '../utils/types'

const fftCache = new Map<string, IFftResponse>()

export const useFftData = (fileId: string | null) => {
  const [data, setData] = useState<IFftResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    if (!fileId) {
      setData(null)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    const cached = fftCache.get(fileId)

    if (cached) {
      setData(cached)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    let isCancelled = false

    const loadFft = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await fftService.getFft({ fileId })

        if (isCancelled) {
          return
        }

        fftCache.set(fileId, result)
        setData(result)
      } catch (error) {
        if (isCancelled) {
          return
        }

        if (error instanceof ApiError) {
          setErrorMessage(error.message)
        } else {
          setErrorMessage('FFT could not be loaded.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadFft()

    return () => {
      isCancelled = true
    }
  }, [fileId])

  return {
    data,
    errorMessage,
    isLoading,
  }
}
