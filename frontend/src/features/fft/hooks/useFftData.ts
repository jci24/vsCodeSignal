import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'
import {
  serializeTransformRecipe,
  type ITransformRecipe,
} from '@/features/transforms/utils/types'

import { fftService } from '../service/fftService'
import type { IFftRequest, IFftResponse } from '../utils/types'

const fftCache = new Map<string, IFftResponse>()

const getCacheKey = (request: IFftRequest): string =>
  `${request.fileId}:${serializeTransformRecipe(request.transforms)}`

const fetchFft = async (request: IFftRequest): Promise<IFftResponse> => {
  const cacheKey = getCacheKey(request)
  const cached = fftCache.get(cacheKey)

  if (cached) {
    return cached
  }

  const result = await fftService.getFft(request)
  fftCache.set(cacheKey, result)
  return result
}

export const useFftData = (fileId: string | null, transforms?: ITransformRecipe) => {
  const [data, setData] = useState<IFftResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const transformKey = serializeTransformRecipe(transforms)

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
        const result = await fetchFft({ fileId, transforms })

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
  }, [fileId, transformKey, transforms])

  return {
    data,
    errorMessage,
    isLoading,
  }
}

export const useFftSeriesData = (requests: IFftRequest[]) => {
  const [data, setData] = useState<IFftResponse[]>([])
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const requestsKey = requests
    .map((request) => `${request.fileId}:${serializeTransformRecipe(request.transforms)}`)
    .join('|')

  useEffect(() => {
    if (requests.length === 0) {
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
        const result = await Promise.all(requests.map((request) => fetchFft(request)))

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
  }, [requests, requestsKey])

  return {
    data,
    errorMessage,
    isLoading,
  }
}
