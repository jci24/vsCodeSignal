export type SignalPoint = {
  timeMs: number
  value: number
}

export type SignalSeries = {
  color: string
  id: string
  name: string
  points: SignalPoint[]
  quality: 'stable' | 'watch' | 'anomaly'
  sampleRateHz: number
  unit: string
}

export type SignalCapture = {
  channels: SignalSeries[]
  id: string
  name: string
  source: string
}

export type SelectionWindow = {
  endMs: number
  startMs: number
}
