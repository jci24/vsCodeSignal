export interface IWaveformRequest {
  fileId: string
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
