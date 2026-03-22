import type { JSX } from 'react'

import { AnalysisLineChart } from '@/shared/charts/AnalysisLineChart'

import { useWaveformData } from '../../hooks/useWaveformData'
import styles from './WaveformPanel.module.scss'

interface WaveformPanelProps {
  fileId: string
}

export function WaveformPanel({ fileId }: WaveformPanelProps): JSX.Element {
  const { data, errorMessage, isLoading } = useWaveformData(fileId)

  if (isLoading) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Loading waveform</p>
        <p className={styles.stateCopy}>Preparing the time-domain view.</p>
      </div>
    )
  }

  if (errorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Waveform unavailable</p>
        <p className={styles.stateCopy}>{errorMessage}</p>
      </div>
    )
  }

  if (!data || data.points.length === 0) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>No waveform data</p>
        <p className={styles.stateCopy}>This file did not produce waveform samples.</p>
      </div>
    )
  }

  return (
    <div className={styles.root}>
      <AnalysisLineChart
        series={[
          {
            color: '#111827',
            id: data.fileId,
            name: 'Waveform',
            points: data.points.map((point) => ({
              x: point.timeSeconds,
              y: point.amplitude,
            })),
          },
        ]}
        xAxisFormatter={formatSeconds}
        yAxisFormatter={formatAmplitude}
      />
    </div>
  )
}

function formatAmplitude(value: number): string {
  return value.toFixed(2)
}

function formatSeconds(value: number): string {
  if (value >= 10) {
    return `${value.toFixed(0)}s`
  }

  return `${value.toFixed(1)}s`
}
