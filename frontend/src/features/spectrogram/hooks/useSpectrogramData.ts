import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'
import {
  serializeTransformRecipe,
  type ITransformRecipe,
} from '@/features/transforms/utils/types'

import { spectrogramService } from '../service/spectrogramService'
import type { ISpectrogramRequest, ISpectrogramResponse } from '../utils/types'

const spectrogramCache = new Map<string, ISpectrogramResponse>()

const getCacheKey = (request: ISpectrogramRequest): string =>
  `${request.fileId}:${serializeTransformRecipe(request.transforms)}`

const fetchSpectrogram = async (
  request: ISpectrogramRequest,
): Promise<ISpectrogramResponse> => {
  const cacheKey = getCacheKey(request)
  const cached = spectrogramCache.get(cacheKey)

  if (cached) {
    return cached
  }

  const result = await spectrogramService.getSpectrogram(request)
  spectrogramCache.set(cacheKey, result)
  return result
}

export const useSpectrogramData = (
  fileId: string | null,
  transforms?: ITransformRecipe,
) => {
  const [data, setData] = useState<ISpectrogramResponse | null>(null)
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

    const loadSpectrogram = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await fetchSpectrogram({ fileId, transforms })

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
  }, [fileId, transformKey, transforms])

  return {
    data,
    errorMessage,
    isLoading,
  }
}
