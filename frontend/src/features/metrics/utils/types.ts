import type { ITransformRecipe } from '@/features/transforms/utils/types'

export interface IMetricsRequest {
  fileId: string
  transforms?: ITransformRecipe
}

export interface IMetricsResponse {
  crestFactor: number
  dominantFrequencyHz: number
  dominantMagnitudeDb: number
  durationSeconds: number
  fileId: string
  peak: number
  rms: number
  sampleRateHz: number
  sourcePath: string
}
