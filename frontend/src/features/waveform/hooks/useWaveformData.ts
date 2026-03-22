import { useEffect, useState } from 'react'

import { ApiError } from '@/api/client'

import { waveformService } from '../service/waveformService'
import type { IWaveformResponse } from '../utils/types'

const waveformCache = new Map<string, IWaveformResponse>()

const fetchWaveform = async (fileId: string): Promise<IWaveformResponse> => {
  const cached = waveformCache.get(fileId)

  if (cached) {
    return cached
  }

  const result = await waveformService.getWaveform({ fileId })
  waveformCache.set(fileId, result)
  return result
}

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

    let isCancelled = false

    const loadWaveform = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await fetchWaveform(fileId)

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
  }, [fileId])

  return {
    data,
    errorMessage,
    isLoading,
  }
}

export const useWaveformSeriesData = (fileIds: string[]) => {
  const [data, setData] = useState<IWaveformResponse[]>([])
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

    const loadWaveforms = async (): Promise<void> => {
      setIsLoading(true)
      setErrorMessage(null)

      try {
        const result = await Promise.all(fileIds.map((fileId) => fetchWaveform(fileId)))

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
  }, [fileIdsKey])

  return {
    data,
    errorMessage,
    isLoading,
  }
}
