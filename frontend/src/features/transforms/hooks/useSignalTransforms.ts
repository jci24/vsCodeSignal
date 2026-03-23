import { useCallback, useMemo, useState } from 'react'

import {
  defaultTransformRecipe,
  hasActiveTransforms,
  type ITransformRecipe,
} from '../utils/types'

type TransformMap = Record<string, ITransformRecipe>

const recipesEqual = (left: ITransformRecipe, right: ITransformRecipe): boolean =>
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
    resetTransforms,
    setGainDb,
    setNormalize,
    setTrimSilence,
  }
}
