import type { JSX } from 'react'
import { useMemo } from 'react'

import type { ITransformRecipe } from '@/features/transforms/utils/types'
import { AnalysisLineChart } from '@/shared/charts/AnalysisLineChart'

import { useWaveformData, useWaveformSeriesData } from '../../hooks/useWaveformData'
import styles from './WaveformPanel.module.scss'

interface WaveformPanelProps {
  comparisonFileIds?: string[]
  compact?: boolean
  fileId: string
  transforms?: ITransformRecipe
}

export function WaveformPanel({
  comparisonFileIds = [],
  compact = false,
  fileId,
  transforms,
}: WaveformPanelProps): JSX.Element {
  const overlayRequests = useMemo(
    () =>
      comparisonFileIds.length > 0
        ? [{ fileId, transforms }, ...comparisonFileIds.map((comparisonFileId) => ({ fileId: comparisonFileId }))]
        : [],
    [comparisonFileIds, fileId, transforms],
  )
  const {
    data: primaryData,
    errorMessage: primaryErrorMessage,
    isLoading: isPrimaryLoading,
  } = useWaveformData(fileId, transforms)
  const {
    data: seriesData,
    errorMessage: seriesErrorMessage,
    isLoading: isSeriesLoading,
  } = useWaveformSeriesData(overlayRequests)
  const isCompareMode = seriesData.length > 1

  if (isPrimaryLoading || (overlayRequests.length > 0 ? isSeriesLoading : false)) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Loading waveform</p>
        <p className={styles.stateCopy}>Preparing the time-domain view.</p>
      </div>
    )
  }

  if (primaryErrorMessage || seriesErrorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Waveform unavailable</p>
        <p className={styles.stateCopy}>{primaryErrorMessage ?? seriesErrorMessage}</p>
      </div>
    )
  }

  if (!primaryData || primaryData.points.length === 0) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>No waveform data</p>
        <p className={styles.stateCopy}>This file did not produce waveform samples.</p>
      </div>
    )
  }

  const colorScale = ['#111827', '#0f766e', '#1d4ed8', '#b45309', '#7c3aed', '#be123c']
  const chartSeries =
    seriesData.length > 0
      ? seriesData.map((entry, index) => ({
          color: colorScale[index % colorScale.length],
          id: entry.fileId,
          name: entry.sourcePath,
          opacity: index === 0 ? 1 : 0.72,
          points: entry.points.map((point) => ({
            x: point.timeSeconds,
            y: point.amplitude,
          })),
          width: index === 0 ? 1.75 : 1.3,
        }))
      : [
          {
            color: colorScale[0],
            id: primaryData.fileId,
            name: primaryData.sourcePath,
            points: primaryData.points.map((point) => ({
              x: point.timeSeconds,
              y: point.amplitude,
            })),
            width: 1.75,
          },
        ]

  return (
    <div className={styles.root}>
      {isCompareMode ? (
        <div className={styles.compareLegend}>
          {chartSeries.map((entry, index) => (
            <span className={styles.legendItem} key={entry.id}>
              <span
                className={styles.legendSwatch}
                style={{ background: colorScale[index % colorScale.length] }}
              />
              {index === 0 ? 'Selected' : entry.name}
            </span>
          ))}
        </div>
      ) : null}

      <AnalysisLineChart
        compact={compact}
        series={chartSeries}
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
