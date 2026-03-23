import type { JSX } from 'react'

import type { ITransformRecipe } from '@/features/transforms/utils/types'
import { AnalysisHeatmapChart } from '@/shared/charts/AnalysisHeatmapChart'
import { LoadingSpinner } from '@/shared/ui/loading-spinner'

import { useSpectrogramData } from '../../hooks/useSpectrogramData'
import styles from './SpectrogramPanel.module.scss'

interface SpectrogramPanelProps {
  compact?: boolean
  comparisonFileId?: string | null
  fileId: string
  transforms?: ITransformRecipe
}

export function SpectrogramPanel({
  compact = false,
  comparisonFileId = null,
  fileId,
  transforms,
}: SpectrogramPanelProps): JSX.Element {
  const {
    data: primaryData,
    errorMessage: primaryErrorMessage,
    isLoading: isPrimaryLoading,
  } = useSpectrogramData(fileId, transforms)
  const {
    data: comparisonData,
    errorMessage: comparisonErrorMessage,
    isLoading: isComparisonLoading,
  } = useSpectrogramData(comparisonFileId)
  const compareData = comparisonFileId && comparisonData ? comparisonData : null
  const primaryChartKey = [
    fileId,
    compact ? 'compact' : 'full',
    primaryData?.times.length ?? 0,
    primaryData?.frequencies.length ?? 0,
    primaryData?.cells.length ?? 0,
  ].join(':')
  const compareChartKey = [
    comparisonFileId ?? 'none',
    compact ? 'compact' : 'full',
    compareData?.times.length ?? 0,
    compareData?.frequencies.length ?? 0,
    compareData?.cells.length ?? 0,
  ].join(':')

  if (isPrimaryLoading || (comparisonFileId ? isComparisonLoading : false)) {
    return (
      <div className={styles.state}>
        <LoadingSpinner className={styles.loadingSpinner} label="Loading spectrogram" />
        <p className={styles.stateCopy}>Preparing the time-frequency view.</p>
      </div>
    )
  }

  if (primaryErrorMessage || comparisonErrorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>Spectrogram unavailable</p>
        <p className={styles.stateCopy}>{primaryErrorMessage ?? comparisonErrorMessage}</p>
      </div>
    )
  }

  if (!primaryData || primaryData.cells.length === 0) {
    return (
      <div className={styles.state}>
        <p className={styles.stateTitle}>No spectrogram data</p>
        <p className={styles.stateCopy}>This file did not produce a spectrogram view.</p>
      </div>
    )
  }

  if (compareData) {
    return (
      <div className={styles.root}>
        <div className={styles.compareGrid}>
          <div className={styles.comparePanel}>
            <p className={styles.compareLabel}>Selected</p>
            <AnalysisHeatmapChart
              compact={compact}
              frequencies={primaryData.frequencies}
              key={primaryChartKey}
              points={primaryData.cells}
              times={primaryData.times}
            />
          </div>

          <div className={styles.comparePanel}>
            <p className={styles.compareLabel}>Compare</p>
            <AnalysisHeatmapChart
              compact={compact}
              frequencies={compareData.frequencies}
              key={compareChartKey}
              points={compareData.cells}
              times={compareData.times}
            />
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className={styles.root}>
      <AnalysisHeatmapChart
        compact={compact}
        frequencies={primaryData.frequencies}
        key={primaryChartKey}
        points={primaryData.cells}
        times={primaryData.times}
      />
    </div>
  )
}
