import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'

import { fftService } from '../service/fftService'
import type { IFftResponse } from '../utils/types'

const fftCache = new Map<string, IFftResponse>()

const fetchFft = async (fileId: string): Promise<IFftResponse> => {
  const cached = fftCache.get(fileId)

  if (cached) {
    return cached
  }

  const result = await fftService.getFft({ fileId })
  fftCache.set(fileId, result)
  return result
}

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

    let isCancelled = false

    const loadFft = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await fetchFft(fileId)

        if (isCancelled) {
          return
        }

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

export const useFftSeriesData = (fileIds: string[]) => {
  const [data, setData] = useState<IFftResponse[]>([])
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const fileIdsKey = fileIds.join('|')

  useEffect(() => {
    if (fileIds.length === 0) {
      setData([])
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    let isCancelled = false

    const loadFftSeries = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await Promise.all(fileIds.map((fileId) => fetchFft(fileId)))

        if (isCancelled) {
          return
        }

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

    void loadFftSeries()

    return () => {
      isCancelled = true
    }
  }, [fileIdsKey])

  return {
    data,
    errorMessage,
    isLoading,
  }
}
