import { useCallback, useMemo, useState } from 'react'

import {
  defaultTransformRecipe,
  hasActiveTransforms,
  type FilterMode,
  type IFilterRecipe,
  type ITransformRecipe,
} from '../utils/types'

type TransformMap = Record<string, ITransformRecipe>

const recipesEqual = (left: ITransformRecipe, right: ITransformRecipe): boolean =>
  left.filter.mode === right.filter.mode &&
  Math.abs(left.filter.cutoffHz - right.filter.cutoffHz) < 0.001 &&
  Math.abs(left.filter.lowCutoffHz - right.filter.lowCutoffHz) < 0.001 &&
  Math.abs(left.filter.highCutoffHz - right.filter.highCutoffHz) < 0.001 &&
  Math.abs(left.filter.q - right.filter.q) < 0.001 &&
  left.normalize === right.normalize &&
  left.trimSilence === right.trimSilence &&
  Math.abs(left.gainDb - right.gainDb) < 0.001

export const useSignalTransforms = (fileId: string | null) => {
  const [recipesByFileId, setRecipesByFileId] = useState<TransformMap>({})

  const activeTransforms = useMemo<ITransformRecipe>(() => {
    if (!fileId) {
      return defaultTransformRecipe
    }

    return recipesByFileId[fileId] ?? defaultTransformRecipe
  }, [fileId, recipesByFileId])

  const updateTransforms = useCallback(
    (updater: (current: ITransformRecipe) => ITransformRecipe): void => {
      if (!fileId) {
        return
      }

      setRecipesByFileId((current) => {
        const previous = current[fileId] ?? defaultTransformRecipe
        const next = updater(previous)

        if (!hasActiveTransforms(next)) {
          if (!(fileId in current)) {
            return current
          }

          const { [fileId]: _removed, ...remaining } = current
          return remaining
        }

        if (recipesEqual(previous, next) && current[fileId]) {
          return current
        }

        return {
          ...current,
          [fileId]: next,
        }
      })
    },
    [fileId],
  )

  const setNormalize = useCallback(
    (normalize: boolean): void => {
      updateTransforms((current) => ({
        ...current,
        normalize,
      }))
    },
    [updateTransforms],
  )

  const setTrimSilence = useCallback(
    (trimSilence: boolean): void => {
      updateTransforms((current) => ({
        ...current,
        trimSilence,
      }))
    },
    [updateTransforms],
  )

  const setGainDb = useCallback(
    (gainDb: number): void => {
      updateTransforms((current) => ({
        ...current,
        gainDb,
      }))
    },
    [updateTransforms],
  )

  const setFilterMode = useCallback(
    (mode: FilterMode): void => {
      updateTransforms((current) => ({
        ...current,
        filter: {
          ...current.filter,
          mode,
        },
      }))
    },
    [updateTransforms],
  )

  const setFilterCutoffHz = useCallback(
    (cutoffHz: number): void => {
      updateTransforms((current) => ({
        ...current,
        filter: {
          ...current.filter,
          cutoffHz,
        },
      }))
    },
    [updateTransforms],
  )

  const setFilterLowCutoffHz = useCallback(
    (lowCutoffHz: number): void => {
      updateTransforms((current) => {
        const nextLow = Math.min(lowCutoffHz, current.filter.highCutoffHz - 50)

        return {
          ...current,
          filter: {
            ...current.filter,
            lowCutoffHz: nextLow,
          },
        }
      })
    },
    [updateTransforms],
  )

  const setFilterHighCutoffHz = useCallback(
    (highCutoffHz: number): void => {
      updateTransforms((current) => {
        const nextHigh = Math.max(highCutoffHz, current.filter.lowCutoffHz + 50)

        return {
          ...current,
          filter: {
            ...current.filter,
            highCutoffHz: nextHigh,
          },
        }
      })
    },
    [updateTransforms],
  )

  const setFilterQ = useCallback(
    (q: number): void => {
      updateTransforms((current) => ({
        ...current,
        filter: {
          ...current.filter,
          q,
        },
      }))
    },
    [updateTransforms],
  )

  const setFilterRecipe = useCallback(
    (filter: IFilterRecipe): void => {
      updateTransforms((current) => ({
        ...current,
        filter,
      }))
    },
    [updateTransforms],
  )

  const resetFiltering = useCallback((): void => {
    updateTransforms((current) => ({
      ...current,
      filter: defaultTransformRecipe.filter,
    }))
  }, [updateTransforms])

  const resetTransforms = useCallback((): void => {
    if (!fileId) {
      return
    }

    setRecipesByFileId((current) => {
      if (!(fileId in current)) {
        return current
      }

      const { [fileId]: _removed, ...remaining } = current
      return remaining
    })
  }, [fileId])

  return {
    activeTransforms,
    hasActiveTransforms: hasActiveTransforms(activeTransforms),
    resetFiltering,
    resetTransforms,
    setFilterCutoffHz,
    setFilterHighCutoffHz,
    setFilterLowCutoffHz,
    setFilterMode,
    setFilterQ,
    setFilterRecipe,
    setGainDb,
    setNormalize,
    setTrimSilence,
  }
}
