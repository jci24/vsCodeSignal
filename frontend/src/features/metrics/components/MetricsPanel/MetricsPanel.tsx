import type { JSX } from 'react'

import type { ITransformRecipe } from '@/features/transforms/utils/types'

import { useMetricsData } from '../../hooks/useMetricsData'
import styles from './MetricsPanel.module.scss'

interface MetricsPanelProps {
  fileId: string
  transforms?: ITransformRecipe
}

export function MetricsPanel({ fileId, transforms }: MetricsPanelProps): JSX.Element {
  const { data, errorMessage, isLoading } = useMetricsData(fileId, transforms)

  if (isLoading) {
    return (
      <div className={styles.state}>
        <p className={styles.eyebrow}>Metrics</p>
        <h4 className={styles.stateTitle}>Calculating signal metrics</h4>
        <p className={styles.stateCopy}>Computing RMS, peak, crest factor, and dominant frequency.</p>
      </div>
    )
  }

  if (errorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.eyebrow}>Metrics</p>
        <h4 className={styles.stateTitle}>Metrics unavailable</h4>
        <p className={styles.stateCopy}>{errorMessage}</p>
      </div>
    )
  }

  if (!data) {
    return (
      <div className={styles.state}>
        <p className={styles.eyebrow}>Metrics</p>
        <h4 className={styles.stateTitle}>No metrics yet</h4>
        <p className={styles.stateCopy}>Select an audio file to calculate signal metrics.</p>
      </div>
    )
  }

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <p className={styles.eyebrow}>Metrics</p>
        <h4 className={styles.title}>Selected signal summary</h4>
        <p className={styles.copy}>Peak and RMS are shown in dBFS for the current file.</p>
      </div>

      <div className={styles.metricGrid}>
        <Metric label="RMS (dBFS)" value={formatDbfs(data.rms)} />
        <Metric label="Peak (dBFS)" value={formatDbfs(data.peak)} />
        <Metric
          hint="Peak / RMS"
          label="Crest factor"
          value={`${data.crestFactor.toFixed(2)}x`}
        />
        <Metric label="Dominant freq" value={formatFrequency(data.dominantFrequencyHz)} />
        <Metric label="Duration" value={formatDuration(data.durationSeconds)} />
        <Metric label="Sample rate" value={`${data.sampleRateHz.toLocaleString()} Hz`} />
      </div>
    </div>
  )
}

interface MetricProps {
  hint?: string
  label: string
  value: string
}

function Metric({ hint, label, value }: MetricProps): JSX.Element {
  return (
    <div className={styles.metric}>
      <span className={styles.metricLabel}>{label}</span>
      <strong className={styles.metricValue}>{value}</strong>
      {hint ? <span className={styles.metricHint}>{hint}</span> : null}
    </div>
  )
}

function formatDbfs(value: number): string {
  if (value <= 1e-9) {
    return '-∞ dBFS'
  }

  const dbfs = 20 * Math.log10(value)
  const rounded = Number(dbfs.toFixed(1))
  const prefix = rounded > 0 ? '+' : ''

  return `${prefix}${rounded.toFixed(1)} dBFS`
}

function formatFrequency(value: number): string {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(2)} kHz`
  }

  return `${value.toFixed(1)} Hz`
}

function formatDuration(value: number): string {
  const totalSeconds = Math.max(0, Math.round(value))
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  if (minutes === 0) {
    return `${seconds}s`
  }

  return `${minutes}m ${seconds}s`
}
