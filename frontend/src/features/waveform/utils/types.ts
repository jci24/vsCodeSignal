import type { ITransformRecipe } from '@/features/transforms/utils/types'

export interface IWaveformRequest {
  fileId: string
  transforms?: ITransformRecipe
}

export interface IWaveformPoint {
  amplitude: number
  timeSeconds: number
}

export interface IWaveformResponse {
  fileId: string
  points: IWaveformPoint[]
  sampleRateHz: number
  sourcePath: string
}
