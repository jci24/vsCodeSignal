import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'

import { spectrogramService } from '../service/spectrogramService'
import type { ISpectrogramResponse } from '../utils/types'

const spectrogramCache = new Map<string, ISpectrogramResponse>()

export const useSpectrogramData = (fileId: string | null) => {
  const [data, setData] = useState<ISpectrogramResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    if (!fileId) {
      setData(null)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    const cached = spectrogramCache.get(fileId)

    if (cached) {
      setData(cached)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    let isCancelled = false

    const loadSpectrogram = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await spectrogramService.getSpectrogram({ fileId })

        if (isCancelled) {
          return
        }

        spectrogramCache.set(fileId, result)
        setData(result)
      } catch (error) {
        if (isCancelled) {
          return
        }

        if (error instanceof ApiError) {
          setErrorMessage(error.message)
        } else {
          setErrorMessage('Spectrogram could not be loaded.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadSpectrogram()

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
