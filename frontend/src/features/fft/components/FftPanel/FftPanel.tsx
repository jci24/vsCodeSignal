import type { JSX } from 'react'
import { useMemo, useState } from 'react'

import { AnalysisLineChart } from '@/shared/charts/AnalysisLineChart'

import { useFftData, useFftSeriesData } from '../../hooks/useFftData'
import styles from './FftPanel.module.scss'

interface FftPanelProps {
  comparisonFileIds?: string[]
  compact?: boolean
  fileId: string
}

type FrequencyScale = 'linear' | 'log'

export function FftPanel({
  comparisonFileIds = [],
  compact = false,
  fileId,
}: FftPanelProps): JSX.Element {
  const [frequencyScale, setFrequencyScale] = useState<FrequencyScale>('linear')
  const overlayFileIds = useMemo(
    () => (comparisonFileIds.length > 0 ? [fileId, ...comparisonFileIds] : []),
    [comparisonFileIds, fileId],
  )
  const {
    data: primaryData,
    errorMessage: primaryErrorMessage,
    isLoading: isPrimaryLoading,
  } = useFftData(fileId)
  const {
    data: seriesData,
    errorMessage: seriesErrorMessage,
    isLoading: isSeriesLoading,
  } = useFftSeriesData(overlayFileIds)

  const chartPoints = useMemo(
    () => {
      const bins = primaryData?.bins ?? []
      const peakMagnitude = bins.reduce(
        (highest, bin) => Math.max(highest, bin.magnitude),
        Number.NEGATIVE_INFINITY,
      )

      return bins
        .filter((bin) => (frequencyScale === 'log' ? bin.frequencyHz > 0 : true))
        .map((bin) => ({
          x: bin.frequencyHz,
          y: Number.isFinite(peakMagnitude) ? bin.magnitude - peakMagnitude : bin.magnitude,
        }))
    },
    [frequencyScale, primaryData?.bins],
  )

  const logAxisMin = useMemo(() => {
    if (frequencyScale !== 'log') {
      return undefined
    }

    const bins = seriesData[0]?.bins ?? primaryData?.bins ?? []
    return bins.find((bin) => bin.frequencyHz > 0)?.frequencyHz ?? 1
  }, [frequencyScale, primaryData?.bins, seriesData])
  const isCompareMode = seriesData.length > 1

  if (isPrimaryLoading || (overlayFileIds.length > 0 ? isSeriesLoading : false)) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Loading FFT</p>
        <p className={styles.stateCopy}>Preparing the frequency-domain view.</p>
      </div>
    )
  }

  if (primaryErrorMessage || seriesErrorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>FFT unavailable</p>
        <p className={styles.stateCopy}>{primaryErrorMessage ?? seriesErrorMessage}</p>
      </div>
    )
  }

  if (!primaryData || primaryData.bins.length === 0) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>No FFT data</p>
        <p className={styles.stateCopy}>This file did not produce a frequency spectrum.</p>
      </div>
    )
  }

  const colorScale = ['#111827', '#0f766e', '#1d4ed8', '#b45309', '#7c3aed', '#be123c']
  const normalizeBins = (bins: typeof primaryData.bins) => {
    const peakMagnitude = bins.reduce(
      (highest, bin) => Math.max(highest, bin.magnitude),
      Number.NEGATIVE_INFINITY,
    )

    return bins
      .filter((bin) => (frequencyScale === 'log' ? bin.frequencyHz > 0 : true))
      .map((bin) => ({
        x: bin.frequencyHz,
        y: Number.isFinite(peakMagnitude) ? bin.magnitude - peakMagnitude : bin.magnitude,
      }))
  }
  const chartSeries =
    seriesData.length > 0
      ? seriesData.map((entry, index) => ({
          color: colorScale[index % colorScale.length],
          id: entry.fileId,
          name: entry.sourcePath,
          opacity: index === 0 ? 1 : 0.72,
          points: normalizeBins(entry.bins),
          width: index === 0 ? 1.75 : 1.3,
        }))
      : [
          {
            color: colorScale[0],
            id: primaryData.fileId,
            name: primaryData.sourcePath,
            points: chartPoints,
            width: 1.75,
          },
        ]

  return (
    <div className={styles.root}>
      <div className={styles.panelHeader}>
        <div className={styles.panelMeta}>
          <p className={styles.panelLabel}>Magnitude (dB, peak = 0)</p>
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
        </div>
        <div aria-label="FFT frequency scale" className={styles.scaleToggle} role="tablist">
          <button
            aria-selected={frequencyScale === 'linear'}
            className={styles.scaleButton}
            data-active={frequencyScale === 'linear'}
            onClick={() => setFrequencyScale('linear')}
            role="tab"
            type="button"
          >
            Linear
          </button>
          <button
            aria-selected={frequencyScale === 'log'}
            className={styles.scaleButton}
            data-active={frequencyScale === 'log'}
            onClick={() => setFrequencyScale('log')}
            role="tab"
            type="button"
          >
            Log Freq
          </button>
        </div>
      </div>

      <AnalysisLineChart
        className={styles.chart}
        compact={compact}
        series={chartSeries}
        xAxisMin={logAxisMin}
        xAxisType={frequencyScale === 'log' ? 'log' : 'value'}
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
  return `${Math.round(value)} dB`
}
