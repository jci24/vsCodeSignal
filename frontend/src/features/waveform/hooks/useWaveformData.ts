import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'
import {
  serializeTransformRecipe,
  type ITransformRecipe,
} from '@/features/transforms/utils/types'

import { waveformService } from '../service/waveformService'
import type { IWaveformRequest, IWaveformResponse } from '../utils/types'

const waveformCache = new Map<string, IWaveformResponse>()

const getCacheKey = (request: IWaveformRequest): string =>
  `${request.fileId}:${serializeTransformRecipe(request.transforms)}`

const fetchWaveform = async (request: IWaveformRequest): Promise<IWaveformResponse> => {
  const cacheKey = getCacheKey(request)
  const cached = waveformCache.get(cacheKey)

  if (cached) {
    return cached
  }

  const result = await waveformService.getWaveform(request)
  waveformCache.set(cacheKey, result)
  return result
}

export const useWaveformData = (
  fileId: string | null,
  transforms?: ITransformRecipe,
) => {
  const [data, setData] = useState<IWaveformResponse | null>(null)
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

    const loadWaveform = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await fetchWaveform({ fileId, transforms })

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
  }, [fileId, transformKey, transforms])

  return {
    data,
    errorMessage,
    isLoading,
  }
}

export const useWaveformSeriesData = (requests: IWaveformRequest[]) => {
  const [data, setData] = useState<IWaveformResponse[]>([])
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

    const loadWaveforms = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await Promise.all(requests.map((request) => fetchWaveform(request)))

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
          setErrorMessage('Waveform could not be loaded.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadWaveforms()

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
