import type { JSX } from 'react'

import { AnalysisHeatmapChart } from '@/shared/charts/AnalysisHeatmapChart'

import { useSpectrogramData } from '../../hooks/useSpectrogramData'
import styles from './SpectrogramPanel.module.scss'

interface SpectrogramPanelProps {
  fileId: string
}

export function SpectrogramPanel({ fileId }: SpectrogramPanelProps): JSX.Element {
  const { data, errorMessage, isLoading } = useSpectrogramData(fileId)

  if (isLoading) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Loading spectrogram</p>
        <p className={styles.stateCopy}>Preparing the time-frequency view.</p>
      </div>
    )
  }

  if (errorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Spectrogram unavailable</p>
        <p className={styles.stateCopy}>{errorMessage}</p>
      </div>
    )
  }

  if (!data || data.cells.length === 0) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>No spectrogram data</p>
        <p className={styles.stateCopy}>This file did not produce a spectrogram view.</p>
      </div>
    )
  }

  return (
    <div className={styles.root}>
      <AnalysisHeatmapChart
        points={data.cells
          .map((cell) => ({
            frequencyHz: data.frequencies[cell.frequencyIndex],
            intensity: cell.intensity,
            timeSeconds: data.times[cell.timeIndex],
          }))
          .filter(
            (point) =>
              typeof point.frequencyHz === 'number' && typeof point.timeSeconds === 'number',
          )}
      />
    </div>
  )
}
