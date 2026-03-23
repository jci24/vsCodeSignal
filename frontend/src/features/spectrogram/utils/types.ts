import type { ITransformRecipe } from '@/features/transforms/utils/types'

export interface ISpectrogramRequest {
  fileId: string
  transforms?: ITransformRecipe
}

export interface ISpectrogramCell {
  frequencyIndex: number
  intensity: number
  timeIndex: number
}

export interface ISpectrogramResponse {
  cells: ISpectrogramCell[]
  fileId: string
  frequencies: number[]
  sourcePath: string
  times: number[]
}
