import type { ITransformRecipe } from '@/features/transforms/utils/types'

export interface IFftRequest {
  fileId: string
  transforms?: ITransformRecipe
}

export interface IFftBin {
  frequencyHz: number
  magnitude: number
}

export interface IFftResponse {
  bins: IFftBin[]
  fileId: string
  sourcePath: string
}
