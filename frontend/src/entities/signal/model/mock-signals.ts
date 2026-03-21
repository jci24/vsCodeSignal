import type {
  SelectionWindow,
  SignalCapture,
  SignalPoint,
  SignalSeries,
} from '@/entities/signal/model/types'

function buildSeries({
  amplitude,
  color,
  id,
  name,
  offset = 0,
  phase = 0,
  quality,
}: {
  amplitude: number
  color: string
  id: string
  name: string
  offset?: number
  phase?: number
  quality: SignalSeries['quality']
}): SignalSeries {
  const points: SignalPoint[] = Array.from({ length: 160 }, (_, index) => {
    const timeMs = index * 10
    const base = Math.sin(index / 7 + phase) * amplitude
    const harmonic = Math.cos(index / 15 + phase) * amplitude * 0.22
    const drift = index > 118 ? (index - 118) * 0.012 : 0

    return {
      timeMs,
      value: Number((offset + base + harmonic + drift).toFixed(3)),
    }
  })

  return {
    color,
    id,
    name,
    points,
    quality,
    sampleRateHz: 2400,
    unit: 'g',
  }
}

export const primaryCapture: SignalCapture = {
  channels: [
    buildSeries({
      amplitude: 0.48,
      color: '#0f9ab0',
      id: 'imu-raw',
      name: 'IMU raw',
      quality: 'stable',
    }),
    buildSeries({
      amplitude: 0.36,
      color: '#c96a17',
      id: 'torque-filtered',
      name: 'Torque filtered',
      offset: 0.18,
      phase: 0.75,
      quality: 'watch',
    }),
  ],
  id: 'capture-01',
  name: 'Compressor bench / session 07',
  source: 'signal-pack/bench-session-07.csv',
}

export const selectedWindow: SelectionWindow = {
  endMs: 1320,
  startMs: 1140,
}
