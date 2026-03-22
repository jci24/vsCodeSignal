import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'

import { waveformService } from '../service/waveformService'
import type { IWaveformResponse } from '../utils/types'

const waveformCache = new Map<string, IWaveformResponse>()

export const useWaveformData = (fileId: string | null) => {
  const [data, setData] = useState<IWaveformResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    if (!fileId) {
      setData(null)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    const cached = waveformCache.get(fileId)

    if (cached) {
      setData(cached)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    let isCancelled = false

    const loadWaveform = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await waveformService.getWaveform({ fileId })

        if (isCancelled) {
          return
        }

        waveformCache.set(fileId, result)
        setData(result)
      } catch (error) {
        if (isCancelled) {
          return
        }

        if (error instanceof ApiError) {
          setErrorMessage(error.message)
        } else {
          setErrorMessage('Waveform could not be loaded.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadWaveform()

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
