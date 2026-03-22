import type { JSX } from 'react'

import { AnalysisLineChart } from '@/shared/charts/AnalysisLineChart'

import { useFftData } from '../../hooks/useFftData'
import styles from './FftPanel.module.scss'

interface FftPanelProps {
  fileId: string
}

export function FftPanel({ fileId }: FftPanelProps): JSX.Element {
  const { data, errorMessage, isLoading } = useFftData(fileId)

  if (isLoading) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Loading FFT</p>
        <p className={styles.stateCopy}>Preparing the frequency-domain view.</p>
      </div>
    )
  }

  if (errorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>FFT unavailable</p>
        <p className={styles.stateCopy}>{errorMessage}</p>
      </div>
    )
  }

  if (!data || data.bins.length === 0) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>No FFT data</p>
        <p className={styles.stateCopy}>This file did not produce a frequency spectrum.</p>
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
            name: 'FFT',
            points: data.bins.map((bin) => ({
              x: bin.frequencyHz,
              y: bin.magnitude,
            })),
          },
        ]}
        xAxisFormatter={formatFrequency}
        yAxisFormatter={formatMagnitude}
      />
    </div>
  )
}

function formatFrequency(value: number): string {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(1)}k`
  }

  return `${Math.round(value)}`
}

function formatMagnitude(value: number): string {
  return `${Math.round(value)}`
}
