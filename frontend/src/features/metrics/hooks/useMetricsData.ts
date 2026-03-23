import { useEffect, useRef, useState } from 'react'

import { ApiError } from '@/api/client'
import {
  serializeTransformRecipe,
  type ITransformRecipe,
} from '@/features/transforms/utils/types'

import { metricsService } from '../service/metricsService'
import type { IMetricsRequest, IMetricsResponse } from '../utils/types'

const metricsCache = new Map<string, IMetricsResponse>()

const getCacheKey = (request: IMetricsRequest): string =>
  `${request.fileId}:${serializeTransformRecipe(request.transforms)}`

const fetchMetrics = async (request: IMetricsRequest): Promise<IMetricsResponse> => {
  const cacheKey = getCacheKey(request)
  const cached = metricsCache.get(cacheKey)

  if (cached) {
    return cached
  }

  const result = await metricsService.getMetrics(request)
  metricsCache.set(cacheKey, result)
  return result
}

export const useMetricsData = (
  fileId: string | null,
  transforms?: ITransformRecipe,
) => {
  const [data, setData] = useState<IMetricsResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const transformKey = serializeTransformRecipe(transforms)
  const transformsRef = useRef<ITransformRecipe | undefined>(transforms)

  useEffect(() => {
    transformsRef.current = transforms
  }, [transformKey])

  useEffect(() => {
    if (!fileId) {
      setData(null)
      setErrorMessage(null)
      setIsLoading(false)
      return
    }

    let isCancelled = false

    const loadMetrics = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await fetchMetrics({ fileId, transforms: transformsRef.current })

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
          setErrorMessage('Metrics could not be loaded.')
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadMetrics()

    return () => {
      isCancelled = true
    }
  }, [fileId, transformKey])

  return {
    data,
    errorMessage,
    isLoading,
  }
}
